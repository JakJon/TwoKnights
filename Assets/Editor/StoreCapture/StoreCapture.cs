#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

// The store page's camera crew: a virtual controller that plays both knights, and
// the Recorder's start and stop buttons. Editor-only — nothing here ships in a
// build, and no game script knows it exists. The game sees an ordinary gamepad,
// which is the whole point: what gets recorded is the game being played, not the
// game being puppeted.
//
// Driven from outside over the Unity bridge:
//   StoreCapture.Prepare();                                   // once per play session
//   StoreCapture.BeginTake("mine", 11, "The Millstone", "Fireball 1,Fireball 2", "Frost Tip 1");
//   StoreCapture.StartRecording("C:/.../millstone");          // when the shot is worth having
//   StoreCapture.StopRecording();
//   StoreCapture.Finish();                                    // hand the editor back as it was found
public static class StoreCapture
{
    // ------------------------------------------------------------------ tuning
    // Public and static so a take can be re-tuned over the bridge without a recompile.

    // Frames a second the camera takes. Sixty for fights. Thirty for menus and NPC
    // scenes: they run on the real clock while the game is paused, so the camera has
    // to keep up with real time or everything on screen plays fast, and at thirty
    // the encoder can.
    public static float RecordFps = 60f;

    public static bool AutopilotOn;
    public static bool KeepAlive = true;
    public static bool UseSpecials = true;
    public static bool HoldFire = false;        // true parks both bows (menus, entrances)
    public static float HoldFireUntil = -1f;    // ...or parks them until this Time.time, so a crowd can gather
    public static bool AutoDialogue = false;    // press past an NPC's lines by itself
    public static float DialogueSeconds = 1.5f; // how long each page is left up first

    // Fixed marks to shoot at instead of enemies — the Pallid Keep's mirror board has
    // nothing on it to aim at, so the knights are told which panes to send arrows into.
    public static bool AimOverride = false;
    public static float AimSeconds = 1.8f;      // how long each mark is held before moving to the next
    private static Vector2[] _leftAims = new Vector2[0];
    private static Vector2[] _rightAims = new Vector2[0];

    public static float BlockRadius = 3.4f;     // a rock inside this, and closing, owns the shield
    public static float BlockSeconds = 0.55f;   // ...or one this close to landing, however far out
    public static float ContactRadius = 1.9f;   // a body this close gets the shield and the sword
    public static float SwordReach = 2.3f;
    public static float EngageRadius = 6.5f;    // nothing further out than this is shot at: the fight happens in the light
    public static float TurnRate = 620f;        // degrees a second the thumb moves when aiming
    public static float UrgentTurnRate = 1500f; // ...and when something is about to land
    public static float AimTolerance = 9f;      // loose only once the bow is this close to on
    public static float StickySeconds = 0.45f;  // how long a thumb stays with a target it chose
    public static float ArrowSpeed = 10f;
    public static float OtherSidePenalty = 3.5f;

    // ------------------------------------------------------------------ state

    private class Pilot
    {
        public bool left;
        public PlayerHealth health;
        public Transform body;
        public ShieldOrbit shield;
        public PlayerSpecial special;
        public float angle;             // where the thumb is holding the stick, in degrees
        public int targetId;
        public float targetUntil;
        public float nextSwingAt;
        public bool swingHeld;
        public bool fire;
        public bool specialTap;
        public float nextSpecialAt;
        public float nextSingleShotAt;
    }

    private static Gamepad _pad;
    private static Pilot _left, _right;
    private static readonly Dictionary<int, Vector2> _lastPos = new Dictionary<int, Vector2>();
    private static readonly Dictionary<int, Vector2> _velocity = new Dictionary<int, Vector2>();
    private static readonly Dictionary<GamepadButton, int> _taps = new Dictionary<GamepadButton, int>();
    // Trial targets that already have an arrow on the way, and when to stop believing in it.
    private static readonly Dictionary<int, float> _claimed = new Dictionary<int, float>();
    private static int _lastTickFrame = -1;
    // Counted in frames, not seconds: a lossless recording runs many times slower than
    // the clock, and a page left up for two real seconds is gone in a tenth of a
    // second of footage.
    private static int _lastDialogueFrame = -100000;

    private static RecorderController _recorder;
    private static RecorderControllerSettings _recorderSettings;
    private static int _recordStartFrame;
    private static string _recordPath;

    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly FieldInfo CurrentHealthField = typeof(PlayerHealth).GetField("currentHealth", Hidden);
    private static readonly FieldInfo HealthBarField = typeof(PlayerHealth).GetField("healthBar", Hidden);

    [InitializeOnLoadMethod]
    private static void Hook()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    // ------------------------------------------------------------------ session

    /// <summary>
    /// Once per play session. Lets the game run and take input while the editor
    /// sits behind another window, and switches the autopilot on.
    /// </summary>
    public static string Prepare()
    {
        Application.runInBackground = true;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        EnsurePad();
        MuteRealPads();
        AutopilotOn = true;
        return "prepared: pad=" + _pad.name + " runInBackground=" + Application.runInBackground + " muted=" + _muted.Count;
    }

    /// <summary>Hands the editor back: no pad, no forced waves, no forced trials.</summary>
    public static string Finish()
    {
        AutopilotOn = false;
        if (_recorder != null && _recorder.IsRecording()) StopRecording();
        if (_pad != null && _pad.added) InputSystem.RemoveDevice(_pad);
        _pad = null;
        // Every pad, not only the ones this session remembers switching off: the list
        // is lost on a recompile, and a pad left off by an interrupted session would
        // otherwise stay dead until the editor is restarted.
        foreach (Gamepad pad in Gamepad.all)
        {
            if (!pad.enabled) InputSystem.EnableDevice(pad);
        }
        _muted.Clear();
        TestRunConfig.AutoPickWave = null;
        TestRunConfig.Map = null;
        TestRunConfig.ForcedTrial = null;
        TestRunConfig.ForcedTrialPhase = 1;
        Time.timeScale = 1f;
        return "finished";
    }

    // Real controllers are switched off for the session. One left on the desk
    // never stops talking — a Switch Pro pad sends a stream of noise — and
    // whichever pad spoke last is Gamepad.current, which is what every menu and
    // dialogue box reads. At recording speed the real pad always speaks last, and
    // the presses queued here land on a controller nobody is listening to.
    private static readonly List<InputDevice> _muted = new List<InputDevice>();

    private static void MuteRealPads()
    {
        foreach (Gamepad pad in Gamepad.all)
        {
            if (pad == _pad || !pad.enabled) continue;
            InputSystem.DisableDevice(pad);
            _muted.Add(pad);
        }
    }

    private static void EnsurePad()
    {
        if (_pad != null && _pad.added) return;
        _pad = InputSystem.AddDevice<Gamepad>("StoreCapturePad");
    }

    // ------------------------------------------------------------------ takes

    /// <summary>
    /// Starts a test run: this map, this wave number, this wave asset, these
    /// upgrades (asset names, low tier to high, comma separated). An empty wave
    /// name lets the run pick as it normally would.
    /// </summary>
    public static string BeginTake(string mapId, int wave, string waveName, string leftCsv, string rightCsv)
    {
        var manager = Resources.Load<UpgradeManager>("UpgradeManager");
        var missing = new List<string>();
        List<BaseUpgrade> left = Pick(manager, leftCsv, missing);
        List<BaseUpgrade> right = Pick(manager, rightCsv, missing);

        TestRunConfig.Set(wave, left, right);
        TestRunConfig.Map = string.IsNullOrEmpty(mapId) ? null : MapCatalog.Instance.Find(mapId);
        TestRunConfig.AutoPickWave = string.IsNullOrEmpty(waveName) ? "*" : waveName;

        _left = null;
        _right = null;
        _lastPos.Clear();
        _velocity.Clear();
        _claimed.Clear();
        HoldFireUntil = -1f;
        AimOverride = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene("Main");

        return "take queued: map=" + (TestRunConfig.Map != null ? TestRunConfig.Map.name : "<save's>") +
               " wave=" + wave + " pick=" + TestRunConfig.AutoPickWave +
               " L=" + left.Count + " R=" + right.Count +
               (missing.Count > 0 ? " MISSING=[" + string.Join(", ", missing) + "]" : "");
    }

    /// <summary>
    /// The Pallid Keep with something on the board: the Keep's own mirror layout
    /// and a camera-only wave of bats and slimes (StoreCaptureKeepWave), handed to
    /// the test run as the wave to play. Nothing is added to the map's setlist.
    /// </summary>
    public static string BeginKeepTake(int wave, string leftCsv, string rightCsv)
    {
        string queued = BeginTake("pallid_keep", wave, "", leftCsv, rightCsv);
        TestRunConfig.RetryWave = StoreCaptureKeepWave.Build();
        return queued + " keep wave forced";
    }

    /// <summary>A run on an empty board, for staging something the camera needs a quiet arena for.</summary>
    public static string BeginQuietTake(string mapId, int wave, string leftCsv, string rightCsv)
    {
        string queued = BeginTake(mapId, wave, "", leftCsv, rightCsv);
        TestRunConfig.RetryWave = StoreCaptureKeepWave.BuildQuiet();
        return queued + " quiet wave forced";
    }

    /// <summary>
    /// Plays a quest's completion scene as the game does between waves: the NPC
    /// arrives, says their piece, and the reward lands. Shown, not granted — the
    /// scene only presents; nothing here completes the quest.
    /// </summary>
    public static string PlayQuestCompletion(string questId)
    {
        Quest quest = QuestDatabase.Get(questId);
        if (quest == null) return "no quest '" + questId + "'";
        Spawner spawner = Object.FindFirstObjectByType<Spawner>();
        if (spawner == null) return "no spawner in the scene";
        spawner.StartCoroutine(QuestScene.Play(quest, QuestScene.Kind.Completion, null));
        return "playing the completion of " + questId;
    }

    // What was on screen while the camera rolled, ten times a second: the frame,
    // trial orbs, range targets, enemies, where the real Ninja stood, and how many
    // rocks were closing on each knight. It is how a shot is found afterwards
    // without watching the whole take.
    private static readonly StringBuilder _log = new StringBuilder();

    public static string DumpLog()
    {
        string text = _log.ToString();
        _log.Length = 0;
        return text;
    }

    private static void Note()
    {
        int frame = Time.frameCount - _recordStartFrame;
        if (frame % 6 != 0) return;
        int orbs = 0;
        foreach (TrialOrb orb in Object.FindObjectsByType<TrialOrb>(FindObjectsSortMode.None))
        {
            if (Spawner.IsInView(orb.transform.position)) orbs++;
        }
        int targets = 0;
        foreach (RangeTarget target in Object.FindObjectsByType<RangeTarget>(FindObjectsSortMode.None))
        {
            if (target.Revealed && !target.WasHit) targets++;
        }
        int enemies = 0;
        foreach (EnemyBase enemy in Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
        {
            if (!enemy.IsDead && Spawner.IsInView(enemy.transform.position)) enemies++;
        }
        string ninja = "";
        foreach (NinjaTarget target in Object.FindObjectsByType<NinjaTarget>(FindObjectsSortMode.None))
        {
            if (target.IsClone || !target.Revealed) continue;
            ninja = target.Centre.x.ToString("0.0") + ":" + target.Centre.y.ToString("0.0");
        }
        int rocksLeft = 0, rocksRight = 0;
        foreach (ProjectileSettings rock in Object.FindObjectsByType<ProjectileSettings>(FindObjectsSortMode.None))
        {
            Vector2 pos = rock.transform.position;
            if (!Spawner.IsInView(pos)) continue;
            if (pos.x < 0f) rocksLeft++; else rocksRight++;
        }
        _log.Append(frame).Append(';').Append(orbs).Append(';').Append(targets).Append(';').Append(enemies).Append(';')
            .Append(ninja).Append(';').Append(rocksLeft).Append(';').Append(rocksRight).Append('|');
    }

    public static string ForceTrial(string key, int phase)
    {
        TestRunConfig.ForcedTrial = string.IsNullOrEmpty(key) ? null : key;
        TestRunConfig.ForcedTrialPhase = phase < 1 ? 1 : phase;
        return "forced trial: " + (TestRunConfig.ForcedTrial ?? "<none>") + " phase " + TestRunConfig.ForcedTrialPhase;
    }

    private static List<BaseUpgrade> Pick(UpgradeManager manager, string csv, List<string> missing)
    {
        var picked = new List<BaseUpgrade>();
        if (string.IsNullOrEmpty(csv)) return picked;
        foreach (string raw in csv.Split(','))
        {
            string wanted = raw.Trim();
            if (wanted.Length == 0) continue;
            BaseUpgrade found = null;
            foreach (BaseUpgrade upgrade in manager.AllUpgrades)
            {
                if (upgrade != null && upgrade.name == wanted) { found = upgrade; break; }
            }
            if (found != null) picked.Add(found); else missing.Add(wanted);
        }
        return picked;
    }

    /// <summary>
    /// Marks for each knight to shoot at in turn, as "x:y;x:y". An empty list
    /// leaves that knight to the ordinary targeting. Turns the override on.
    /// </summary>
    public static string SetAims(string left, string right, float seconds)
    {
        _leftAims = ParsePoints(left);
        _rightAims = ParsePoints(right);
        AimSeconds = seconds > 0.1f ? seconds : 1.8f;
        AimOverride = _leftAims.Length > 0 || _rightAims.Length > 0;
        return "aims: left " + _leftAims.Length + ", right " + _rightAims.Length + ", every " + AimSeconds + "s";
    }

    private static Vector2[] ParsePoints(string csv)
    {
        var points = new List<Vector2>();
        if (!string.IsNullOrEmpty(csv))
        {
            foreach (string pair in csv.Split(';'))
            {
                string[] xy = pair.Split(':');
                if (xy.Length != 2) continue;
                points.Add(new Vector2(float.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture),
                                       float.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture)));
            }
        }
        return points.ToArray();
    }

    /// <summary>Holds a button down for a few frames — menus, dialogue, the draft.</summary>
    public static string Tap(string button, int frames)
    {
        GamepadButton parsed;
        if (!System.Enum.TryParse(button, true, out parsed)) return "no such button: " + button;
        _taps[parsed] = frames < 1 ? 1 : frames;
        return "tap " + parsed + " x" + _taps[parsed];
    }

    // ------------------------------------------------------------------ the draft

    /// <summary>What the upgrade menu is offering right now, by asset name.</summary>
    public static string DraftInfo()
    {
        UpgradeMenu menu = Object.FindFirstObjectByType<UpgradeMenu>(FindObjectsInactive.Include);
        if (menu == null) return "no menu";
        var offered = typeof(UpgradeMenu).GetField("currentUpgrades", Hidden).GetValue(menu) as List<BaseUpgrade>;
        if (offered == null) return "menu has not drafted";
        var names = new List<string>();
        foreach (BaseUpgrade upgrade in offered) names.Add(upgrade != null ? upgrade.name : "<null>");
        return string.Join(" | ", names);
    }

    /// <summary>
    /// Deals the draft again, off camera, until the named upgrade is one of the
    /// three. It is still a hand the game could have dealt — only the waiting for
    /// it is skipped. Returns the card's position, or -1.
    /// </summary>
    public static int RerollFor(string upgradeAssetName, int maxTries)
    {
        UpgradeMenu menu = Object.FindFirstObjectByType<UpgradeMenu>(FindObjectsInactive.Include);
        if (menu == null) return -1;
        MethodInfo populate = typeof(UpgradeMenu).GetMethod("PopulateUpgrades", Hidden);
        FieldInfo offeredField = typeof(UpgradeMenu).GetField("currentUpgrades", Hidden);
        for (int attempt = 0; attempt <= maxTries; attempt++)
        {
            var offered = offeredField.GetValue(menu) as List<BaseUpgrade>;
            if (offered != null)
            {
                for (int i = 0; i < offered.Count; i++)
                {
                    if (offered[i] != null && offered[i].name == upgradeAssetName) return i;
                }
            }
            populate.Invoke(menu, null);
        }
        return -1;
    }

    // ------------------------------------------------------------------ recording

    public static string StartRecording(string pathWithoutExtension)
    {
        return StartRecording(pathWithoutExtension, "prores_lt");
    }

    public static string StartRecording(string pathWithoutExtension, bool proRes)
    {
        return StartRecording(pathWithoutExtension, proRes ? "prores_lt" : "h264");
    }

    /// <summary>
    /// 1920x1080 at a fixed sixty frames a second, game sound included. The fixed
    /// rate is what makes the footage smooth however fast the editor is really
    /// running. ProRes keeps the pixels clean for the edit — the Recorder's H.264
    /// smears the grass and softens every sprite edge, so it is only for looking
    /// at a take, never for keeping one. Formats: prores_lt, prores, prores_hq, h264.
    ///
    /// The path must NOT begin with the project's own path: the Recorder treats
    /// that prefix as "inside the project" even when it is only a sibling folder
    /// with a longer name, and writes the file into the project instead.
    /// </summary>
    public static string StartRecording(string pathWithoutExtension, string format)
    {
        if (_recorder != null && _recorder.IsRecording()) return "already recording " + _recordPath;

        _recorderSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();

        // "png": every frame written whole, with the sound beside it as a WAV. No
        // encoder touches the picture, so the pixels are exactly the game's. The
        // movie formats below soften every sprite edge, ProRes included.
        if (format == "png")
        {
            var stills = ScriptableObject.CreateInstance<ImageRecorderSettings>();
            stills.name = "StoreCapture frames";
            stills.Enabled = true;
            stills.OutputFormat = ImageRecorderSettings.ImageRecorderOutputFormat.PNG;
            stills.CaptureAlpha = false;
            stills.imageInputSettings = new GameViewInputSettings { OutputWidth = 1920, OutputHeight = 1080 };
            stills.OutputFile = pathWithoutExtension + "_<Frame>";
            _recorderSettings.AddRecorderSettings(stills);

            var sound = ScriptableObject.CreateInstance<AudioRecorderSettings>();
            sound.name = "StoreCapture sound";
            sound.Enabled = true;
            sound.OutputFile = pathWithoutExtension;
            _recorderSettings.AddRecorderSettings(sound);

            return Begin(pathWithoutExtension);
        }

        var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movie.name = "StoreCapture";
        movie.Enabled = true;
        if (format != "h264")
        {
            ProResEncoderSettings.OutputFormat proRes = ProResEncoderSettings.OutputFormat.ProRes422LT;
            if (format == "prores") proRes = ProResEncoderSettings.OutputFormat.ProRes422;
            if (format == "prores_hq") proRes = ProResEncoderSettings.OutputFormat.ProRes422HQ;
            // The heaviest ProRes: full colour resolution and a very high bitrate. It keeps
            // up with real time, which a frame-by-frame recording does not — and menus and
            // NPC scenes run on the real clock, so they can only be filmed at real speed.
            if (format == "prores_xq") proRes = ProResEncoderSettings.OutputFormat.ProRes4444XQ;
            if (format == "prores_4444") proRes = ProResEncoderSettings.OutputFormat.ProRes4444;
            movie.EncoderSettings = new ProResEncoderSettings { Format = proRes };
        }
        else
        {
            movie.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.Custom,
                EncodingProfile = CoreEncoderSettings.H264EncodingProfile.High,
                TargetBitRate = 60f
            };
        }
        movie.CaptureAudio = true;
        movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1920, OutputHeight = 1080 };
        movie.OutputFile = pathWithoutExtension;

        _recorderSettings.AddRecorderSettings(movie);
        return Begin(pathWithoutExtension);
    }

    private static string Begin(string pathWithoutExtension)
    {
        _recorderSettings.SetRecordModeToManual();
        _recorderSettings.FrameRate = RecordFps;
        _recorderSettings.FrameRatePlayback = FrameRatePlayback.Constant;
        _recorderSettings.CapFrameRate = true;
        _recorderSettings.ExitPlayMode = false;

        _recorder = new RecorderController(_recorderSettings);
        _recorder.PrepareRecording();
        bool started = _recorder.StartRecording();
        _recordStartFrame = Time.frameCount;
        _recordPath = pathWithoutExtension;
        return started ? "recording " + pathWithoutExtension : "FAILED to start recording " + pathWithoutExtension;
    }

    public static string StopRecording()
    {
        if (_recorder == null) return "not recording";
        int frames = Time.frameCount - _recordStartFrame;
        _recorder.StopRecording();
        _recorder = null;
        return "stopped " + _recordPath + " after " + frames + " frames (" + (frames / 60f).ToString("0.00") + "s)";
    }

    public static bool IsRecording => _recorder != null && _recorder.IsRecording();
    public static int RecordedFrames => _recorder != null ? Time.frameCount - _recordStartFrame : 0;

    // ------------------------------------------------------------------ status

    /// <summary>One line on where the run is, for whoever is driving from outside.</summary>
    public static string Status()
    {
        var sb = new StringBuilder();
        sb.Append("scene=").Append(SceneManager.GetActiveScene().name);
        sb.Append(" playing=").Append(EditorApplication.isPlaying);
        sb.Append(" t=").Append(Time.time.ToString("0.0"));
        sb.Append(" ts=").Append(Time.timeScale.ToString("0.##"));
        sb.Append(" frame=").Append(Time.frameCount);
        WaveManager waves = WaveManager.ActiveInstance;
        if (waves != null)
        {
            sb.Append(" wave=").Append(waves.CurrentWaveNumber);
            sb.Append(" name=").Append(waves.CurrentWave != null ? waves.CurrentWave.name : "<none>");
        }
        sb.Append(" enemies=").Append(Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None).Length);
        sb.Append(" rocks=").Append(Object.FindObjectsByType<ProjectileSettings>(FindObjectsSortMode.None).Length);
        FindKnights();
        if (_left != null) sb.Append(" L=").Append(_left.health.CurrentHealth).Append('/').Append(_left.health.MaxHealth);
        if (_right != null) sb.Append(" R=").Append(_right.health.CurrentHealth).Append('/').Append(_right.health.MaxHealth);
        sb.Append(" survived=").Append(WaveSurvivedPanel.IsVisible);
        sb.Append(" trial=").Append(TrialRunner.IsRunning);
        sb.Append(" speaking=").Append(TrialRunner.IsSpeaking || QuestScene.IsPlaying);
        sb.Append(" autopilot=").Append(AutopilotOn);
        sb.Append(" rec=").Append(IsRecording ? RecordedFrames.ToString() : "off");
        sb.Append(" testRun=").Append(TestRunConfig.ActiveRun);
        return sb.ToString();
    }

    // ------------------------------------------------------------------ the autopilot

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || !AutopilotOn) return;
        if (Time.frameCount == _lastTickFrame) return;
        _lastTickFrame = Time.frameCount;

        EnsurePad();
        if (IsRecording) Note();
        var state = new GamepadState();

        // Menus and dialogue stop the clock; the knights' hands come off the
        // sticks and only whatever was tapped goes through.
        bool speaking = TrialRunner.IsSpeaking || QuestScene.IsPlaying;
        bool fighting = Time.timeScale > 0f && !speaking;
        if (fighting)
        {
            FindKnights();
            if (_left != null && _right != null)
            {
                Sense(out List<Threat> rocks, out List<Threat> bodies, out List<Threat> orbs, out List<Threat> marks);
                Fly(_left, rocks, bodies, orbs, marks);
                Fly(_right, rocks, bodies, orbs, marks);

                bool parked = HoldFire || Time.time < HoldFireUntil;
                state.leftStick = StickFor(_left);
                state.rightStick = StickFor(_right);
                if (_left.fire && !parked) state = state.WithButton(GamepadButton.LeftShoulder);
                if (_right.fire && !parked) state = state.WithButton(GamepadButton.RightShoulder);
                state.leftTrigger = _left.swingHeld ? 1f : 0f;
                state.rightTrigger = _right.swingHeld ? 1f : 0f;
                if (_left.swingHeld) state = state.WithButton(GamepadButton.LeftTrigger);
                if (_right.swingHeld) state = state.WithButton(GamepadButton.RightTrigger);
                if (_left.specialTap) state = state.WithButton(GamepadButton.DpadUp);
                if (_right.specialTap) state = state.WithButton(GamepadButton.North);

                if (KeepAlive)
                {
                    TopUp(_left);
                    TopUp(_right);
                }
            }
        }
        else if (speaking && AutoDialogue && Time.frameCount - _lastDialogueFrame > DialogueSeconds * RecordFps)
        {
            _lastDialogueFrame = Time.frameCount;
            _taps[GamepadButton.South] = 2;
        }

        if (_taps.Count > 0)
        {
            var keys = new List<GamepadButton>(_taps.Keys);
            foreach (GamepadButton button in keys)
            {
                state = state.WithButton(button);
                int left = _taps[button] - 1;
                if (left <= 0) _taps.Remove(button); else _taps[button] = left;
            }
        }

        InputSystem.QueueStateEvent(_pad, state);

        // Menus and dialogue read Gamepad.current, and a pad whose state has not
        // changed does not stay current: on a quiet board a real controller on the
        // desk takes the title, and the presses queued here go unread.
        if (Gamepad.current != _pad) _pad.MakeCurrent();
    }

    private static Vector2 StickFor(Pilot pilot)
    {
        float rad = pilot.angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private static void FindKnights()
    {
        if (_left != null && _left.health != null && _right != null && _right.health != null) return;
        _left = null;
        _right = null;
        foreach (PlayerHealth health in Object.FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
        {
            var pilot = new Pilot
            {
                health = health,
                body = health.transform,
                shield = health.GetComponentInChildren<ShieldOrbit>(),
                special = health.GetComponent<PlayerSpecial>(),
                left = health.CompareTag("PlayerLeft")
            };
            if (pilot.shield != null) pilot.angle = pilot.shield.CurrentAngle;
            if (pilot.left) _left = pilot; else _right = pilot;
        }
    }

    private struct Threat
    {
        public int id;
        public Vector2 pos;
        public Vector2 vel;
        public bool once;       // a trial target that must be hit exactly one time
    }

    // Everything on the board, with how it moved since the last frame. Speed is
    // measured rather than asked for: half of what the game throws is moved by
    // hand in an Update, and has no body to ask.
    private static void Sense(out List<Threat> rocks, out List<Threat> bodies, out List<Threat> orbs, out List<Threat> marks)
    {
        rocks = new List<Threat>();
        bodies = new List<Threat>();
        orbs = new List<Threat>();
        marks = new List<Threat>();
        float dt = Time.deltaTime;

        foreach (ProjectileSettings rock in Object.FindObjectsByType<ProjectileSettings>(FindObjectsSortMode.None))
        {
            if (rock.Reflected) continue;
            rocks.Add(Track(rock.GetInstanceID(), rock.transform.position, dt));
        }
        foreach (EnemyBase enemy in Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
        {
            if (enemy.IsDead) continue;
            Vector2 pos = enemy.transform.position;
            if (!Spawner.IsInView(pos)) continue;
            bodies.Add(Track(enemy.GetInstanceID(), pos, dt));
        }
        foreach (CollectibleOrb orb in Object.FindObjectsByType<CollectibleOrb>(FindObjectsSortMode.None))
        {
            Vector2 pos = orb.transform.position;
            if (!Spawner.IsInView(pos)) continue;
            orbs.Add(Track(orb.GetInstanceID(), pos, dt));
        }

        // The trials' own targets. The Ninja's copy is never one of them, and a
        // venom orb that has been bitten once is left strictly alone.
        foreach (NinjaTarget ninja in Object.FindObjectsByType<NinjaTarget>(FindObjectsSortMode.None))
        {
            if (ninja.IsClone || ninja.WasHit || !ninja.Revealed) continue;
            Threat mark = Track(ninja.GetInstanceID(), ninja.Centre, dt);
            mark.once = true;
            marks.Add(mark);
        }
        foreach (RangeTarget target in Object.FindObjectsByType<RangeTarget>(FindObjectsSortMode.None))
        {
            if (target.WasHit || !target.Revealed) continue;
            Vector2 pos = target.transform.position;
            if (!Spawner.IsInView(pos)) continue;
            Threat mark = Track(target.GetInstanceID(), pos, dt);
            mark.once = true;
            marks.Add(mark);
        }
        foreach (TrialOrb orb in Object.FindObjectsByType<TrialOrb>(FindObjectsSortMode.None))
        {
            if (orb.Bitten) continue;
            Vector2 pos = orb.transform.position;
            if (!Spawner.IsInView(pos)) continue;
            Threat mark = Track(orb.GetInstanceID(), pos, dt);
            mark.once = true;
            marks.Add(mark);
        }
    }

    private static Threat Track(int id, Vector2 pos, float dt)
    {
        Vector2 vel = Vector2.zero;
        Vector2 last;
        if (dt > 0f && _lastPos.TryGetValue(id, out last))
        {
            Vector2 raw = (pos - last) / dt;
            Vector2 smoothed;
            vel = _velocity.TryGetValue(id, out smoothed) ? Vector2.Lerp(smoothed, raw, 0.35f) : raw;
        }
        _lastPos[id] = pos;
        _velocity[id] = vel;
        return new Threat { id = id, pos = pos, vel = vel };
    }

    private static void Fly(Pilot pilot, List<Threat> rocks, List<Threat> bodies, List<Threat> orbs, List<Threat> marks)
    {
        Vector2 centre = (Vector2)pilot.body.position + Vector2.up * 0.5f;
        float now = Time.time;
        float want = pilot.angle;
        float rate = TurnRate;
        bool hasShot = false;
        bool singleShot = false;
        Threat shotAt = default(Threat);

        // 1. Anything about to land owns the shield.
        bool urgent = false;
        float soonest = float.MaxValue;
        foreach (Threat rock in rocks)
        {
            Vector2 to = centre - rock.pos;
            float distance = to.magnitude;
            if (distance < 0.05f) continue;
            float closing = Vector2.Dot(rock.vel, to / distance);
            if (closing <= 0.3f) continue;
            // Is it actually coming at THIS knight, or going past to the other one?
            Vector2 heading = rock.vel.normalized;
            float miss = Mathf.Abs(to.x * heading.y - to.y * heading.x);
            if (miss > 1.3f) continue;
            float seconds = distance / closing;
            if (distance > BlockRadius && seconds > BlockSeconds) continue;
            if (seconds < soonest)
            {
                soonest = seconds;
                want = AngleTo(centre, rock.pos);
                urgent = true;
            }
        }

        // 2. A body at the shield gets the shield, and the sword.
        Threat nearest = default(Threat);
        float nearestDistance = float.MaxValue;
        foreach (Threat body in bodies)
        {
            float distance = Vector2.Distance(centre, body.pos);
            if (distance < nearestDistance) { nearestDistance = distance; nearest = body; }
        }
        bool crowded = nearestDistance < ContactRadius;
        if (!urgent && crowded)
        {
            want = AngleTo(centre, nearest.pos);
            rate = UrgentTurnRate;
            hasShot = true;
        }

        // 3. A trial's target: one arrow each, from whichever knight is nearer.
        if (!urgent && !crowded && marks.Count > 0)
        {
            Threat mark;
            if (ChooseMark(pilot, centre, marks, now, out mark))
            {
                float flight = Vector2.Distance(centre, mark.pos) / ArrowSpeed;
                want = AngleTo(centre, mark.pos + mark.vel * flight);
                rate = UrgentTurnRate;
                hasShot = true;
                singleShot = true;
                shotAt = mark;
            }
        }
        // 4. Otherwise pick something to shoot and stay with it a moment.
        else if (!urgent && !crowded)
        {
            Threat target;
            if (ChooseTarget(pilot, centre, bodies, orbs, now, out target))
            {
                float flight = Vector2.Distance(centre, target.pos) / ArrowSpeed;
                want = AngleTo(centre, target.pos + target.vel * flight);
                hasShot = true;
            }
        }
        // 5. Nothing to fight and marks to shoot at: work round them in turn.
        Vector2[] aims = pilot.left ? _leftAims : _rightAims;
        if (AimOverride && aims.Length > 0 && !urgent && !crowded && !hasShot)
        {
            Vector2 at = aims[(int)(now / AimSeconds) % aims.Length];
            want = AngleTo(centre, at);
            hasShot = true;
        }
        if (urgent) rate = UrgentTurnRate;

        pilot.angle = Mathf.MoveTowardsAngle(pilot.angle, want, rate * Time.deltaTime);
        float off = Mathf.Abs(Mathf.DeltaAngle(pilot.angle, want));

        if (singleShot)
        {
            // One press, then hands off until the arrow has had time to arrive.
            pilot.fire = false;
            if (off <= 2.5f && now >= pilot.nextSingleShotAt && !_claimed.ContainsKey(shotAt.id))
            {
                pilot.fire = true;
                float flight = Vector2.Distance(centre, shotAt.pos) / ArrowSpeed;
                _claimed[shotAt.id] = now + flight + 0.45f;
                pilot.nextSingleShotAt = now + 0.2f;
            }
        }
        else
        {
            pilot.fire = hasShot && off <= AimTolerance;
        }

        // The sword is a press, not a hold: down for one frame, up the next.
        if (pilot.swingHeld)
        {
            pilot.swingHeld = false;
        }
        else if (nearestDistance < SwordReach && now >= pilot.nextSwingAt &&
                 Mathf.Abs(Mathf.DeltaAngle(pilot.angle, AngleTo(centre, nearest.pos))) < 45f)
        {
            pilot.swingHeld = true;
            pilot.nextSwingAt = now + 0.25f;
        }

        pilot.specialTap = false;
        if (UseSpecials && pilot.special != null && pilot.special.specialBarFilled &&
            bodies.Count >= 3 && now >= pilot.nextSpecialAt)
        {
            pilot.specialTap = true;
            pilot.nextSpecialAt = now + 0.5f;
        }
    }

    private static bool ChooseMark(Pilot pilot, Vector2 centre, List<Threat> marks, float now, out Threat chosen)
    {
        chosen = default(Threat);

        // Forget arrows that should have landed by now and did not.
        if (_claimed.Count > 0)
        {
            var stale = new List<int>();
            foreach (KeyValuePair<int, float> claim in _claimed) if (now > claim.Value) stale.Add(claim.Key);
            foreach (int id in stale) _claimed.Remove(id);
        }

        Pilot other = pilot.left ? _right : _left;
        Vector2 otherCentre = (Vector2)other.body.position + Vector2.up * 0.5f;
        float best = float.MaxValue;
        bool found = false;
        foreach (Threat mark in marks)
        {
            if (_claimed.ContainsKey(mark.id)) continue;
            float mine = Vector2.Distance(centre, mark.pos);
            float theirs = Vector2.Distance(otherCentre, mark.pos);
            if (theirs < mine) continue;    // the other knight's to take
            if (mine < best) { best = mine; chosen = mark; found = true; }
        }
        return found;
    }

    private static bool ChooseTarget(Pilot pilot, Vector2 centre, List<Threat> bodies, List<Threat> orbs,
                                     float now, out Threat chosen)
    {
        chosen = default(Threat);

        // Stay with the last choice while it lives and the thumb's patience holds.
        if (pilot.targetId != 0 && now < pilot.targetUntil)
        {
            foreach (Threat body in bodies) if (body.id == pilot.targetId) { chosen = body; return true; }
            foreach (Threat orb in orbs) if (orb.id == pilot.targetId) { chosen = orb; return true; }
        }

        float best = float.MaxValue;
        bool found = false;
        foreach (Threat body in bodies)
        {
            float distance = Vector2.Distance(centre, body.pos);
            if (distance > EngageRadius) continue;
            float score = distance;
            // Each knight minds its own half first.
            if ((body.pos.x < -0.5f) != pilot.left && Mathf.Abs(body.pos.x) > 0.5f) score += OtherSidePenalty;
            if (score < best) { best = score; chosen = body; found = true; }
        }
        if (!found)
        {
            foreach (Threat orb in orbs)
            {
                float score = Vector2.Distance(centre, orb.pos);
                if ((orb.pos.x < 0f) != pilot.left) score += OtherSidePenalty;
                if (score < best) { best = score; chosen = orb; found = true; }
            }
        }
        if (found)
        {
            pilot.targetId = chosen.id;
            pilot.targetUntil = now + StickySeconds;
        }
        return found;
    }

    private static float AngleTo(Vector2 from, Vector2 to)
    {
        Vector2 d = to - from;
        return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
    }

    // Quietly, and only when it matters: a knight driven under three fifths is put
    // back to nine tenths. The bars still move, so a hit still reads as a hit — and
    // the margin is wide because one keg can take half a bar, and a dead knight
    // means a death screen in the middle of a take.
    private static void TopUp(Pilot pilot)
    {
        if (pilot.health == null || CurrentHealthField == null) return;
        int max = pilot.health.MaxHealth;
        if (pilot.health.CurrentHealth >= max * 0.6f) return;
        int restored = Mathf.RoundToInt(max * 0.9f);
        CurrentHealthField.SetValue(pilot.health, restored);
        var bar = HealthBarField != null ? HealthBarField.GetValue(pilot.health) as HealthBar : null;
        if (bar != null) bar.SetValue(restored);
    }
}
#endif
