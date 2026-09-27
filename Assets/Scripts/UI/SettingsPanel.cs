using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// The two mix sliders, shared by the camp menu and the pause screen the same way
/// QuestPanel and EquipmentPanel are: one class, driven by whichever document
/// instanced SettingsOverlay.uxml.
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

    private Row _music;
    private Row _sfx;
    private int _focused;

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
        public Func<float> Get;
        public Action<float> Set;
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

        _music = new Row
        {
            Root = root.Q<VisualElement>("settings-row-music"),
            Fill = root.Q<VisualElement>("settings-music-fill"),
            Value = root.Q<Label>("settings-music-value"),
            Get = () => AudioManager.Instance != null ? AudioManager.Instance.musicVolume : 0f,
            Set = v => AudioManager.Instance?.SetMusicVolume(v)
        };

        _sfx = new Row
        {
            Root = root.Q<VisualElement>("settings-row-sfx"),
            Fill = root.Q<VisualElement>("settings-sfx-fill"),
            Value = root.Q<Label>("settings-sfx-value"),
            Get = () => AudioManager.Instance != null ? AudioManager.Instance.sfxVolume : 0f,
            Set = v => AudioManager.Instance?.SetSfxVolume(v)
        };

        _closeButton = root.Q<Button>("settings-close-button");
        if (_closeButton != null) _closeButton.clicked += Close;
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
    }

    private void Close()
    {
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiCancel);
        // The setters only write PlayerPrefs in memory while a slider is moving.
        // This is the moment the player is done, so put it on disk.
        AudioManager.Instance?.SaveMixSettings();
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

        // Both accept and back leave: there is nothing on this screen to confirm,
        // so a player who presses A expecting "done" should get it.
        bool close = MenuGamepad.SubmitPressed(gp) || MenuGamepad.CancelPressed(gp)
                     || (kb != null && (kb.escapeKey.wasPressedThisFrame
                                        || kb.enterKey.wasPressedThisFrame
                                        || kb.spaceKey.wasPressedThisFrame));

        if (vertical != 0) Navigate(vertical);
        else if (horizontal != 0) Adjust(horizontal);
        else if (close) Close();
    }

    private void Navigate(int direction)
    {
        int next = Mathf.Clamp(_focused + direction, 0, 1);
        if (next == _focused) return;
        _focused = next;
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiMove);
        Refresh();
    }

    private void Adjust(int direction)
    {
        Row row = _focused == 0 ? _music : _sfx;
        if (row?.Get == null || row.Set == null) return;

        float next = Mathf.Clamp01(Mathf.Round((row.Get() + direction * Step) / Step) * Step);
        if (Mathf.Approximately(next, row.Get())) return;

        row.Set(next);
        Refresh();

        // The sound row auditions itself. Moving a volume slider you cannot hear is
        // guesswork, and the click is already the menu's own noise so it costs no
        // new asset. The music row needs none — the score is right there.
        if (_focused == 1) AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiMove);
    }

    private void Refresh()
    {
        Apply(_music, _focused == 0);
        Apply(_sfx, _focused == 1);
    }

    private static void Apply(Row row, bool focused)
    {
        if (row?.Root == null) return;
        float value = row.Get != null ? row.Get() : 0f;

        if (row.Fill != null) row.Fill.style.width = Length.Percent(value * 100f);
        if (row.Value != null) row.Value.text = Mathf.RoundToInt(value * 100f) + "%";
        row.Root.EnableInClassList("settings-row--focused", focused);
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
