using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// The two mix sliders and the five for the knights' bars (size, width, inward, upward,
/// transparency —
/// see HudLayout), shared by the camp menu and the pause screen the same way
/// QuestPanel and EquipmentPanel are: one class, driven by whichever document
/// instanced SettingsOverlay.uxml.
///
/// In a run, the screen turns see-through while one of the bars' rows is selected,
/// so the player can watch the bars move behind it. In the camp there are no bars
/// to show, so it stays as it is.
///
/// The campfire camp adds a row under them all, Exit, which leaves the file for
/// the file select. It is there only when the host has handed in something for it
/// to do (<see cref="SetExitAction"/>) — the pause screen never does, so a run
/// cannot be dropped from here.
///
/// Polls the pad directly rather than taking InputActionReferences the way
/// CampMenuController does. Two reasons: the menus only bind up/down/confirm/cancel
/// and a slider needs LEFT and RIGHT, which would mean new actions in
/// InputActions.inputactions AND new reference fields wired into two scenes; and
/// polling is already the house style for a panel that owns the screen while it is
/// up — see FileSelectPanel, whose repeat feel this copies exactly.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    private const float RepeatDelay = 0.4f;
    private const float RepeatInterval = 0.12f;
    private const float EntryInputDelay = 0.25f;

    // One notch of the stick. Twenty steps across the bar is fine enough to find a
    // level you like and coarse enough to cross the whole range without holding.
    private const float Step = 0.05f;

    [SerializeField] private UIDocument uiDocument;

    private VisualElement _panel;
    private Button _closeButton;

    // The sliders from the top, in the order the document has them. A row the
    // document does not have is left out rather than left broken.
    private readonly List<Row> _rows = new List<Row>();
    private VisualElement _exitRow;
    private Action _exitAction;
    private int _focused;

    // Exit sits under the sliders, when the host has given it a job
    private int ExitRow => _rows.Count;
    private int LastRow => _exitAction != null && _exitRow != null ? ExitRow : _rows.Count - 1;

    private const string PeekPanelClass = "settings-panel--peek";
    private const string PeekRootClass = "settings-peek";

    private float _inputReadyTime;
    private int _heldVertical;
    private int _heldHorizontal;
    private float _nextVerticalRepeat;
    private float _nextHorizontalRepeat;

    // Fired when the panel closes so the host menu can take its selection back.
    public Action OnCloseRequested;

    public bool IsVisible => _panel != null && _panel.style.display == DisplayStyle.Flex;

    private class Row
    {
        public VisualElement Root;
        public VisualElement Fill;
        public Label Value;
        /// <summary>How far along its track the row is drawn, 0 to 1.</summary>
        public Func<float> Amount;
        /// <summary>The number at the end of the row.</summary>
        public Func<string> Text;
        /// <summary>One notch left (-1) or right (+1). False when nothing moved.</summary>
        public Func<int, bool> Nudge;
        /// <summary>The sound row plays a click as it moves, so the level can be heard.</summary>
        public bool Auditions;
        /// <summary>One of the bars' rows: the screen turns see-through while it is selected.</summary>
        public bool MovesHud;
    }

    private void Awake()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
    }

    private void BindUI()
    {
        if (uiDocument == null) return;
        var root = uiDocument.rootVisualElement;
        if (root == null) return;

        _panel = root.Q<VisualElement>("settings-panel");
        if (_panel == null) return;

        _rows.Clear();

        Func<float> music = () => AudioManager.Instance != null ? AudioManager.Instance.musicVolume : 0f;
        AddRow(root, "music", new Row
        {
            Amount = music,
            Text = () => Percent(music()),
            Nudge = d => StepVolume(d, music, v => AudioManager.Instance?.SetMusicVolume(v))
        });

        Func<float> sfx = () => AudioManager.Instance != null ? AudioManager.Instance.sfxVolume : 0f;
        AddRow(root, "sfx", new Row
        {
            Amount = sfx,
            Text = () => Percent(sfx()),
            Nudge = d => StepVolume(d, sfx, v => AudioManager.Instance?.SetSfxVolume(v)),
            Auditions = true
        });

        // The bars. Size walks a list of sizes the screen draws cleanly rather
        // than a smooth range, so its track fills by place in that list.
        AddRow(root, "hud-size", new Row
        {
            Amount = () =>
            {
                int steps = HudLayout.SizeSteps().Count;
                return steps > 1 ? (float)HudLayout.SizeIndex / (steps - 1) : 1f;
            },
            Text = () => Percent(HudLayout.Size),
            Nudge = HudLayout.StepSize,
            MovesHud = true
        });

        // The bars alone, longer (owner, 2026-10-14): the text and the bars'
        // height stay as they are
        AddRow(root, "hud-width", new Row
        {
            Amount = () => (HudLayout.BarWidth - 1f) / (HudLayout.MaxBarWidth - 1f),
            Text = () => Percent(HudLayout.BarWidth),
            Nudge = HudLayout.StepBarWidth,
            MovesHud = true
        });

        AddRow(root, "hud-inward", new Row
        {
            Amount = () => HudLayout.Inward,
            Text = () => Percent(HudLayout.Inward),
            Nudge = d => HudLayout.SetInward(Notch(HudLayout.Inward, d, HudLayout.PositionStep)),
            MovesHud = true
        });

        AddRow(root, "hud-upward", new Row
        {
            Amount = () => HudLayout.Upward,
            Text = () => Percent(HudLayout.Upward),
            Nudge = d => HudLayout.SetUpward(Notch(HudLayout.Upward, d, HudLayout.PositionStep)),
            MovesHud = true
        });

        // 0 to 100, five at a time (owner, 2026-10-16)
        AddRow(root, "hud-transparency", new Row
        {
            Amount = () => HudLayout.Transparency,
            Text = () => Percent(HudLayout.Transparency),
            Nudge = d => HudLayout.SetTransparency(Notch(HudLayout.Transparency, d, HudLayout.PositionStep)),
            MovesHud = true
        });

        // How strongly enemy rocks glow, 0 to double, ten at a time
        AddRow(root, "rock-glow", new Row
        {
            Amount = () => RockGlowSetting.Intensity / RockGlowSetting.Max,
            Text = () => Percent(RockGlowSetting.Intensity),
            Nudge = RockGlowSetting.Step
        });

        _closeButton = root.Q<Button>("settings-close-button");
        if (_closeButton != null) _closeButton.clicked += Close;

        _exitRow = root.Q<VisualElement>("settings-row-exit");
        if (_exitRow != null) _exitRow.RegisterCallback<ClickEvent>(_ => { if (_exitAction != null) Exit(); });
    }

    // Finds the row's three parts by the names SettingsOverlay.uxml gives them:
    // settings-row-<key>, settings-<key>-fill and settings-<key>-value.
    private void AddRow(VisualElement root, string key, Row row)
    {
        row.Root = root.Q<VisualElement>("settings-row-" + key);
        if (row.Root == null) return;
        row.Fill = root.Q<VisualElement>("settings-" + key + "-fill");
        row.Value = root.Q<Label>("settings-" + key + "-value");
        _rows.Add(row);
    }

    private static string Percent(float value) => Mathf.RoundToInt(value * 100f) + "%";

    // One notch along a 0-1 track, landing on the notches rather than between them
    private static float Notch(float value, int direction, float step)
    {
        return Mathf.Clamp01(Mathf.Round((value + direction * step) / step) * step);
    }

    private static bool StepVolume(int direction, Func<float> get, Action<float> set)
    {
        float next = Notch(get(), direction, Step);
        if (Mathf.Approximately(next, get())) return false;
        set(next);
        return true;
    }

    /// <summary>
    /// Gives the Exit row something to do, which is also what makes it appear.
    /// Null takes the row away again. The panel closes itself first and then calls
    /// <paramref name="onExit"/> INSTEAD of OnCloseRequested: the host is going
    /// somewhere else, not back to its menu.
    /// </summary>
    public void SetExitAction(Action onExit)
    {
        _exitAction = onExit;
        if (_panel != null) Refresh();
    }

    public void Show()
    {
        if (_panel == null) BindUI();
        if (_panel == null)
        {
            Debug.LogWarning("SettingsPanel: no 'settings-panel' element in this document's UXML.");
            // Never strand the player on a screen that is not there.
            OnCloseRequested?.Invoke();
            return;
        }

        _focused = 0;
        _panel.style.display = DisplayStyle.Flex;
        // Matches every other panel: a held Submit from the button that OPENED this
        // must not also be read as input inside it.
        _inputReadyTime = Time.unscaledTime + EntryInputDelay;
        _heldVertical = 0;
        _heldHorizontal = 0;
        Refresh();
    }

    public void Hide()
    {
        if (_panel != null) _panel.style.display = DisplayStyle.None;
        SetPeek(false);
    }

    private void Exit()
    {
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiConfirm);
        // Leaving this way is still leaving: put the mix on disk as Close does
        AudioManager.Instance?.SaveMixSettings();
        HudLayout.Save();
        RockGlowSetting.Save();
        Hide();
        _exitAction?.Invoke();
    }

    private void Close()
    {
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiCancel);
        // The setters only write PlayerPrefs in memory while a slider is moving.
        // This is the moment the player is done, so put it on disk.
        AudioManager.Instance?.SaveMixSettings();
        HudLayout.Save();
        RockGlowSetting.Save();
        Hide();
        OnCloseRequested?.Invoke();
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
        bool leftHeld = (gp != null && (gp.dpad.left.isPressed || gp.leftStick.left.isPressed))
                        || (kb != null && (kb.leftArrowKey.isPressed || kb.aKey.isPressed));
        bool rightHeld = (gp != null && (gp.dpad.right.isPressed || gp.leftStick.right.isPressed))
                         || (kb != null && (kb.rightArrowKey.isPressed || kb.dKey.isPressed));

        int vertical = ApplyRepeat((downHeld ? 1 : 0) - (upHeld ? 1 : 0),
                                   ref _heldVertical, ref _nextVerticalRepeat);
        int horizontal = ApplyRepeat((rightHeld ? 1 : 0) - (leftHeld ? 1 : 0),
                                     ref _heldHorizontal, ref _nextHorizontalRepeat);

        // Both accept and back leave: there is nothing on a slider to confirm,
        // so a player who presses A expecting "done" should get it. The one
        // exception is the Exit row, where accept means the row.
        bool accept = MenuGamepad.SubmitPressed(gp)
                      || (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame));
        bool back = MenuGamepad.CancelPressed(gp) || (kb != null && kb.escapeKey.wasPressedThisFrame);

        if (vertical != 0) Navigate(vertical);
        else if (horizontal != 0) Adjust(horizontal);
        else if (accept && _focused == ExitRow && _exitAction != null) Exit();
        else if (accept || back) Close();
    }

    private void Navigate(int direction)
    {
        int next = Mathf.Clamp(_focused + direction, 0, LastRow);
        if (next == _focused) return;
        _focused = next;
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiMove);
        Refresh();
    }

    private void Adjust(int direction)
    {
        // Left and right mean nothing on the Exit row
        if (_focused >= _rows.Count) return;

        Row row = _rows[_focused];
        if (row.Nudge == null || !row.Nudge(direction)) return;
        Refresh();

        // The sound row auditions itself. Moving a volume slider you cannot hear is
        // guesswork, and the click is already the menu's own noise so it costs no
        // new asset. The music row needs none — the score is right there. Nor do
        // the bars' rows: the bars themselves move.
        if (row.Auditions) AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiMove);
    }

    private void Refresh()
    {
        _focused = Mathf.Clamp(_focused, 0, LastRow);
        for (int i = 0; i < _rows.Count; i++) Apply(_rows[i], _focused == i);

        if (_exitRow != null)
        {
            _exitRow.style.display = _exitAction != null ? DisplayStyle.Flex : DisplayStyle.None;
            _exitRow.EnableInClassList("settings-row--focused", _focused == ExitRow);
        }

        SetPeek(_focused < _rows.Count && _rows[_focused].MovesHud && HudLayout.HasLiveHud);
    }

    private static void Apply(Row row, bool focused)
    {
        if (row?.Root == null) return;
        float amount = row.Amount != null ? Mathf.Clamp01(row.Amount()) : 0f;

        if (row.Fill != null) row.Fill.style.width = Length.Percent(amount * 100f);
        if (row.Value != null && row.Text != null) row.Value.text = row.Text();
        row.Root.EnableInClassList("settings-row--focused", focused);
    }

    // See-through while one of the bars' rows is selected in a run. The panel
    // drops its backing and the rows it is not on fade, and the class on the
    // document's root lets the host's own stylesheet clear whatever it draws
    // behind the panel (PauseMenu.uss takes its dark scrim away).
    private void SetPeek(bool peek)
    {
        if (_panel != null) _panel.EnableInClassList(PeekPanelClass, peek);
        var root = uiDocument != null ? uiDocument.rootVisualElement : null;
        if (root != null) root.EnableInClassList(PeekRootClass, peek);
    }

    // Edge-trigger with hold-to-repeat, matching FileSelectPanel's feel
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
