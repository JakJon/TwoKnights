using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// The file select: three horizontal bars, one per save file, shown once at the
// start of a session before the camp menu takes over.
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

    private const string SLOT_CLASS = "file-slot";
    private const string SELECTED_CLASS = "file-slot--selected";
    private const string EMPTY_CLASS = "file-slot--empty";

    private static readonly string[] Numerals = { "I", "II", "III" };

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
    // it, which for a single-file player is simply the file they have. Falls
    // through to the first bar when nothing has been played at all.
    private int DefaultIndex()
    {
        int best = 0;
        double bestTime = -1;

        for (int slot = 1; slot <= SaveManager.SlotCount; slot++)
        {
            var data = SaveManager.PeekSlot(slot);
            if (data == null) continue;
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
        bool empty = data == null;
        int index = slot - 1;

        var bar = new VisualElement();
        bar.AddToClassList(SLOT_CLASS);
        if (empty) bar.AddToClassList(EMPTY_CLASS);

        var numeral = new Label(index < Numerals.Length ? Numerals[index] : slot.ToString());
        numeral.AddToClassList("file-slot-index");
        bar.Add(numeral);

        var main = new VisualElement();
        main.AddToClassList("file-slot-main");

        var name = new Label(empty ? "Empty File" : data.profileName);
        name.AddToClassList("file-slot-name");
        main.Add(name);

        var sub = new Label(empty ? "Begin a new vigil" : ProgressLine(data));
        sub.AddToClassList("file-slot-sub");
        main.Add(sub);

        bar.Add(main);

        // An empty file has no figures to show, and zeroes would read as a played
        // file that got nowhere rather than as one that was never started.
        if (!empty)
        {
            var stats = new VisualElement();
            stats.AddToClassList("file-slot-stats");
            stats.Add(BuildStat(new Label(PlayTime.Format(data.playTimeSeconds)), "PLAY TIME"));
            stats.Add(BuildStat(CrystalText.Build(data.totalCrystalsEarned, 16f), "CRYSTALS FOUND"));
            bar.Add(stats);
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

    private static string ProgressLine(SaveData data)
    {
        string wave = $"Furthest Wave {Mathf.Max(1, data.furthestWave)}";

        var catalog = MapCatalog.Instance;
        var map = catalog != null ? catalog.Find(data.lastPlayedMapId) : null;
        return map != null ? $"{wave}   ·   {map.DisplayName}" : wave;
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

        // Gold, quests, notices and map progress are all read at scene init, so
        // reload rather than refreshing each of them by hand — the same reason the
        // data wipe reloads the camp.
        Time.timeScale = 1f;
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
