using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// The file select: three horizontal bars, one per save file, shown once at the
// start of a session before the camp menu takes over. Under the file's name is a
// row of small shapes standing for what it has finished, in place of the wave
// number and map name that used to be written out there (see BuildMarks).
//
// Like MapSelectPanel and TestModePanel this polls its own input rather than
// adding InputActionReferences, and CampMenuController goes quiet while it is up.
// At boot it takes no cancel — it is the first screen, so there is nothing behind
// it — but the camp's Exit button reopens it with cancel enabled, since then the
// menu is still sitting underneath.
public class FileSelectPanel : MonoBehaviour
{
    private const float RepeatDelay = 0.4f;
    private const float RepeatInterval = 0.12f;
    private const float EntryInputDelay = 0.25f;

    // A file that has barely been opened has no play time worth reporting, and a
    // figure like "1m" on a bar reads as a failure rather than as a new file. Same
    // idea for crystals: zero found is not a score, it is a file that has not got
    // there yet, so the whole cell stays away until there is something in it.
    private const double MinPlayTimeShown = 180;   // three minutes

    private const string SLOT_CLASS = "file-slot";
    private const string SELECTED_CLASS = "file-slot--selected";
    // Named for what it draws, not for what it means: the bar it goes on is a
    // slot nobody has started, which is not always a slot with no file behind it.
    private const string UNSTARTED_CLASS = "file-slot--empty";

    private static readonly string[] Numerals = { "I", "II", "III" };

    // The two square colours. Map circles take their colour from the map itself
    // (MapDefinition.CompletionMarkColor); these two have no asset to live on.
    private static readonly Color EquipmentSlotColor = new Color(0.8313726f, 0.6352941f, 0.2901961f);   // menu gold
    private static readonly Color SpecialSlotColor = new Color(0.6941176f, 0.5411765f, 0.8784314f);     // the game's crystal purple

    // Picked once, then never again for the rest of the run. Static so it also
    // survives the scene reload that switching files performs — otherwise
    // choosing file 2 would drop the player straight back into the file select.
    private static bool _chosenThisSession;

    /// <summary>Whether the camp should open on the file select rather than the menu.</summary>
    public static bool ShouldShowAtBoot => !_chosenThisSession;

    // Fires once per app run, before the first scene — never on the scene reload
    // that a file switch performs. That makes the flag correct whether or not the
    // editor is configured to reload the domain on entering play mode.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetForNewSession()
    {
        _chosenThisSession = false;
    }

    [SerializeField] private UIDocument uiDocument;

    private VisualElement _root;
    private VisualElement _panel;
    private VisualElement _column;

    private readonly List<VisualElement> _bars = new List<VisualElement>();
    private int _index;

    private float _inputReadyTime;
    private int _heldVertical;
    private float _nextVerticalRepeat;
    private bool _allowCancel;

    /// <summary>Raised once a file is live and the camp menu should take over.</summary>
    public Action OnCloseRequested;

    public bool IsVisible => _panel != null && _panel.style.display == DisplayStyle.Flex;

    private void Awake()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        BindUI();
    }

    private void BindUI()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;
        _root = uiDocument.rootVisualElement;
        if (_root == null) return;

        _panel = _root.Q<VisualElement>("file-select-panel");
        _column = _root.Q<VisualElement>("file-slot-column");
    }

    /// <summary>
    /// Open the file select. <paramref name="allowCancel"/> is for the reopen from
    /// the camp's Exit button, where back should return to the menu.
    /// </summary>
    public void Show(bool allowCancel = false)
    {
        _allowCancel = allowCancel;
        if (_panel == null) BindUI();
        if (_panel == null)
        {
            Debug.LogWarning("FileSelectPanel: no 'file-select-panel' element in the camp UXML.");
            // Never strand the player on a hidden screen: if the panel is missing,
            // treat the file as chosen so the camp menu still opens.
            _chosenThisSession = true;
            OnCloseRequested?.Invoke();
            return;
        }

        Populate();
        _panel.style.display = DisplayStyle.Flex;
        _inputReadyTime = Time.unscaledTime + EntryInputDelay;
        _heldVertical = 0;
    }

    public void Hide()
    {
        if (_panel != null) _panel.style.display = DisplayStyle.None;
    }

    private void Populate()
    {
        _bars.Clear();
        if (_column == null) return;
        _column.Clear();

        for (int slot = 1; slot <= SaveManager.SlotCount; slot++)
        {
            _bars.Add(BuildBar(slot));
        }

        SetSelectedIndex(DefaultIndex());
    }

    // Open on the file most likely to be played: the one with the most time on
    // it, which for a single-file player is simply the file they have. Slots that
    // never got past the tutorial are skipped even though they have minutes on
    // them — they draw as "Create File", and starting the cursor on an offer when
    // a real file is sitting below it is the wrong first thing to point at. Falls
    // through to the first bar when nothing has been started at all.
    private int DefaultIndex()
    {
        int best = 0;
        double bestTime = -1;

        for (int slot = 1; slot <= SaveManager.SlotCount; slot++)
        {
            var data = SaveManager.PeekSlot(slot);
            if (data == null || !data.tutorialCompleted) continue;
            if (data.playTimeSeconds > bestTime)
            {
                bestTime = data.playTimeSeconds;
                best = slot - 1;
            }
        }

        return best;
    }

    private VisualElement BuildBar(int slot)
    {
        var data = SaveManager.PeekSlot(slot);
        // Not "is there a file on disk" but "has anyone actually started here".
        // A file is written to disk the moment its slot is picked, so quitting
        // part-way through the tutorial leaves one behind with nothing on it —
        // and that slot is still only an offer, which is also exactly what
        // pressing A on it does: Confirm hands an untaught file back to the
        // tutorial rather than to the camp. Drawing it as "File 2" would be the
        // bar disagreeing with the button.
        bool unstarted = data == null || !data.tutorialCompleted;
        int index = slot - 1;

        var bar = new VisualElement();
        bar.AddToClassList(SLOT_CLASS);
        if (unstarted) bar.AddToClassList(UNSTARTED_CLASS);

        var numeral = new Label(index < Numerals.Length ? Numerals[index] : slot.ToString());
        numeral.AddToClassList("file-slot-index");
        bar.Add(numeral);

        var main = new VisualElement();
        main.AddToClassList("file-slot-main");

        // An unstarted slot is not a file with nothing in it, it is an offer — so
        // the title says what picking it DOES rather than naming a file that has
        // not begun. "File 3" on such a bar reads as a file you already have.
        var name = new Label(unstarted ? "Create File" : data.profileName);
        name.AddToClassList("file-slot-name");
        main.Add(name);

        // A started file gets its marks under the name, sitting exactly where the
        // wave-and-map line used to. An unstarted one gets the same row with
        // nothing in it: there is nothing to show, but the row carries the height,
        // so its bar stands as tall as the ones beside it instead of shrinking to
        // just a title.
        main.Add(unstarted ? EmptyMarksRow() : BuildMarks(data));

        bar.Add(main);

        // An unstarted file has no figures to show, and zeroes would read as a
        // played file that got nowhere rather than as one that was never started.
        // The minutes an abandoned tutorial leaves behind are the same kind of
        // noise. A played file that has not reached either threshold is in the
        // same position, so each cell is added only once it has something to say.
        if (!unstarted)
        {
            var stats = new VisualElement();
            stats.AddToClassList("file-slot-stats");
            if (data.playTimeSeconds >= MinPlayTimeShown)
            {
                stats.Add(BuildStat(new Label(PlayTime.Format(data.playTimeSeconds)), "PLAY TIME"));
            }
            if (data.totalCrystalsEarned > 0)
            {
                stats.Add(BuildStat(CrystalText.Build(data.totalCrystalsEarned, 16f), "CRYSTALS FOUND"));
            }
            if (stats.childCount > 0) bar.Add(stats);
        }

        bar.RegisterCallback<MouseEnterEvent>(_ => SetSelectedIndex(index));
        bar.RegisterCallback<ClickEvent>(_ =>
        {
            SetSelectedIndex(index);
            Confirm();
        });

        _column.Add(bar);
        return bar;
    }

    private static VisualElement BuildStat(VisualElement value, string caption)
    {
        var cell = new VisualElement();
        cell.AddToClassList("file-slot-stat");

        // A plain Label needs the value styling; a CrystalText row brings its own
        // (count + icon), so it is added as-is.
        if (value is Label) value.AddToClassList("file-slot-stat-value");
        cell.Add(value);

        var label = new Label(caption);
        label.AddToClassList("file-slot-stat-caption");
        cell.Add(label);

        return cell;
    }

    // The file bar says nothing in words about how far a file has got. What it
    // shows instead is a short row of shapes, and what any of them mean is left
    // for the player to work out: a circle in a map's own colour for a map whose
    // TRUE boss has fallen, then a square for every slot the file has won beyond
    // the one it started with — gold for equipment, purple for specials. Starting
    // slots are never drawn, so a fresh file's row is bare and every shape on a
    // bar is something that was earned. This sits under the file's name, where the
    // wave number and map name used to be spelled out.
    private static VisualElement BuildMarks(SaveData data)
    {
        var marks = new VisualElement();
        marks.AddToClassList("file-slot-marks");

        var clears = new VisualElement();
        clears.AddToClassList("file-slot-mark-group");
        var catalog = MapCatalog.Instance;
        if (catalog != null)
        {
            // Campaign order, so the circles read left to right the way the run does
            foreach (var map in catalog.Maps)
            {
                if (map == null || !IsTrueCleared(data, map.MapId)) continue;
                clears.Add(Mark("file-slot-mark-circle", map.CompletionMarkColor));
            }
        }

        var slots = new VisualElement();
        slots.AddToClassList("file-slot-mark-group");
        for (int i = 1; i < Mathf.Max(1, data.equipmentSlots); i++)
        {
            slots.Add(Mark("file-slot-mark-square", EquipmentSlotColor));
        }
        for (int i = 1; i < Mathf.Max(1, data.specialSlots); i++)
        {
            slots.Add(Mark("file-slot-mark-square", SpecialSlotColor));
        }

        marks.Add(clears);
        // The hairline only earns its place with shapes on both sides of it — alone
        // it would read as a mark of its own and stand for nothing.
        if (clears.childCount > 0 && slots.childCount > 0)
        {
            var divider = new VisualElement();
            divider.AddToClassList("file-slot-mark-divider");
            marks.Add(divider);
        }
        marks.Add(slots);

        return marks;
    }

    /// <summary>
    /// The marks row with no marks on it. Same class, so it is the same height by
    /// construction rather than by a number kept in step with one.
    /// </summary>
    private static VisualElement EmptyMarksRow()
    {
        var marks = new VisualElement();
        marks.AddToClassList("file-slot-marks");
        return marks;
    }

    private static VisualElement Mark(string shapeClass, Color color)
    {
        var mark = new VisualElement();
        mark.AddToClassList("file-slot-mark");
        mark.AddToClassList(shapeClass);
        mark.style.backgroundColor = color;
        return mark;
    }

    // PeekSlot hands back a file that is NOT the loaded one, so this reads the
    // records straight off it rather than going through MapProgressStore — that
    // answers for the active save, which on this screen is whichever file the
    // game happened to boot into, not the bar being drawn.
    private static bool IsTrueCleared(SaveData data, string mapId)
    {
        if (data.maps == null || string.IsNullOrEmpty(mapId)) return false;
        foreach (var record in data.maps)
        {
            if (record != null && record.mapId == mapId) return record.trueCleared;
        }
        return false;
    }

    private void SetSelectedIndex(int index)
    {
        if (_bars.Count == 0) return;
        _index = Mathf.Clamp(index, 0, _bars.Count - 1);

        for (int i = 0; i < _bars.Count; i++)
        {
            if (i == _index) _bars[i].AddToClassList(SELECTED_CLASS);
            else _bars[i].RemoveFromClassList(SELECTED_CLASS);
        }
    }

    private void Navigate(int direction)
    {
        if (_bars.Count <= 1) return;
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiMove);
        SetSelectedIndex((_index + direction + _bars.Count) % _bars.Count);
    }

    private void Confirm()
    {
        int slot = _index + 1;
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiConfirm);
        _chosenThisSession = true;

        // Slot 1 is what everything already loaded on the way to this screen, so
        // picking it changes nothing and the camp can simply carry on.
        if (SaveManager.IsLoaded && SaveManager.ActiveSlot == slot)
        {
            SaveManager.Save();   // materialise the file so it stops reading as empty
            // A file that has never finished the tutorial never reaches the camp:
            // it goes straight to the arena to be taught. This one is already
            // loaded, so there is nothing to switch first.
            if (TutorialRun.TryBegin()) return;
            Hide();
            OnCloseRequested?.Invoke();
            return;
        }

        // Switching files from the camp leaves a file that has been played this
        // session: flush its pending time before the switch discards the object.
        // At boot there is nothing worth keeping, and writing slot 1 there would
        // materialise an untouched file as a played one.
        if (_allowCancel) PlayTime.Flush();

        SaveManager.SelectSlot(slot);
        SaveManager.Save();

        // The pick is a static that outlives the reload, so a map remembered from
        // the file we just left would otherwise become this file's next run.
        MapSelection.Clear();

        Time.timeScale = 1f;

        // Same rule as above, checked after the switch so it is the INCOMING
        // file's tutorial flag that decides — the outgoing one is nothing to do
        // with where this pick is going.
        if (TutorialRun.TryBegin()) return;

        // Gold, quests, notices and map progress are all read at scene init, so
        // reload rather than refreshing each of them by hand — the same reason the
        // data wipe reloads the camp.
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void Update()
    {
        if (!IsVisible) return;
        if (Time.unscaledTime < _inputReadyTime) return;

        var gp = Gamepad.current;
        var kb = Keyboard.current;

        bool upHeld = (gp != null && (gp.dpad.up.isPressed || gp.leftStick.up.isPressed))
                      || (kb != null && (kb.upArrowKey.isPressed || kb.wKey.isPressed));
        bool downHeld = (gp != null && (gp.dpad.down.isPressed || gp.leftStick.down.isPressed))
                        || (kb != null && (kb.downArrowKey.isPressed || kb.sKey.isPressed));

        int rawVertical = (downHeld ? 1 : 0) - (upHeld ? 1 : 0);
        int vertical = ApplyRepeat(rawVertical, ref _heldVertical, ref _nextVerticalRepeat);

        bool confirm = MenuGamepad.SubmitPressed(gp)
                       || (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame));

        // Only from Exit: at boot there is nowhere to go back to.
        bool cancel = _allowCancel
                      && (MenuGamepad.CancelPressed(gp)
                          || (kb != null && kb.escapeKey.wasPressedThisFrame));

        if (vertical != 0) Navigate(vertical);
        else if (confirm) Confirm();
        else if (cancel) Close();
    }

    private void Close()
    {
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiCancel);
        Hide();
        OnCloseRequested?.Invoke();
    }

    // Edge-trigger with hold-to-repeat, matching MapSelectPanel's feel
    private int ApplyRepeat(int held, ref int lastHeld, ref float nextRepeat)
    {
        if (held == 0)
        {
            lastHeld = 0;
            return 0;
        }

        if (held != lastHeld)
        {
            lastHeld = held;
            nextRepeat = Time.unscaledTime + RepeatDelay;
            return held;
        }

        if (Time.unscaledTime >= nextRepeat)
        {
            nextRepeat = Time.unscaledTime + RepeatInterval;
            return held;
        }

        return 0;
    }
}
