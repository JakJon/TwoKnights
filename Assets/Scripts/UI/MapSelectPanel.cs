using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

// The level select: one tall pane per map in campaign order, navigated with
// left/right. Sits between the camp's Start button and the game scene.
//
// Like TestModePanel this polls its own input rather than adding left/right
// InputActionReferences — the camp's action set is up/down/confirm/cancel, and
// CampMenuController goes quiet while this panel is up.
public class MapSelectPanel : MonoBehaviour
{
    private const float RepeatDelay = 0.4f;
    private const float RepeatInterval = 0.12f;
    private const float EntryInputDelay = 0.25f;

    private const string PANE_CLASS = "map-pane";
    private const string SELECTED_CLASS = "map-pane--selected";
    private const string LOCKED_CLASS = "map-pane--locked";

    [SerializeField] private UIDocument uiDocument;

    private VisualElement _root;
    private VisualElement _panel;
    private VisualElement _paneRow;
    private Button _closeButton;

    private readonly List<MapDefinition> _maps = new List<MapDefinition>();
    private readonly List<VisualElement> _panes = new List<VisualElement>();
    private int _index;

    private float _inputReadyTime;
    private int _heldHorizontal;
    private float _nextHorizontalRepeat;

    /// <summary>Raised when the player commits to a map.</summary>
    public Action<MapDefinition> OnMapChosen;

    /// <summary>Raised on back/close, so the camp menu can take input again.</summary>
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

    private void OnDisable()
    {
        if (_closeButton != null) _closeButton.clicked -= Close;
    }

    private void BindUI()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;
        _root = uiDocument.rootVisualElement;
        if (_root == null) return;

        _panel = _root.Q<VisualElement>("map-select-panel");
        _paneRow = _root.Q<VisualElement>("map-pane-row");
        _closeButton = _root.Q<Button>("map-close-button");

        if (_closeButton != null)
        {
            _closeButton.clicked -= Close;
            _closeButton.clicked += Close;
        }
    }

    public void Show()
    {
        if (_panel == null) BindUI();
        if (_panel == null)
        {
            Debug.LogWarning("MapSelectPanel: no 'map-select-panel' element in the camp UXML.");
            return;
        }

        Populate();
        _panel.style.display = DisplayStyle.Flex;
        // Swallow the button that opened this panel
        _inputReadyTime = Time.unscaledTime + EntryInputDelay;
        _heldHorizontal = 0;
    }

    public void Hide()
    {
        if (_panel != null) _panel.style.display = DisplayStyle.None;
    }

    private void Populate()
    {
        _maps.Clear();
        _panes.Clear();
        if (_paneRow == null) return;
        _paneRow.Clear();

        var catalog = MapCatalog.Instance;
        if (catalog != null)
        {
            foreach (var map in catalog.Maps)
            {
                if (map != null) _maps.Add(map);
            }
        }

        if (_maps.Count == 0)
        {
            var empty = new Label("No maps in the catalog.");
            empty.AddToClassList("map-empty-line");
            _paneRow.Add(empty);
            return;
        }

        for (int i = 0; i < _maps.Count; i++)
        {
            _panes.Add(BuildPane(_maps[i], i));
        }

        SetSelectedIndex(IndexOfRemembered());
    }

    // Reopen where the player last played, when that map is still selectable
    private int IndexOfRemembered()
    {
        var remembered = MapSelection.Selected;
        string rememberedId = remembered != null ? remembered.MapId : SaveManager.Data.lastPlayedMapId;

        for (int i = 0; i < _maps.Count; i++)
        {
            if (_maps[i].MapId == rememberedId && MapProgressStore.IsUnlocked(_maps[i])) return i;
        }
        for (int i = 0; i < _maps.Count; i++)
        {
            if (MapProgressStore.IsUnlocked(_maps[i])) return i;
        }
        return 0;
    }

    private VisualElement BuildPane(MapDefinition map, int index)
    {
        bool unlocked = MapProgressStore.IsUnlocked(map);

        var pane = new VisualElement();
        pane.AddToClassList(PANE_CLASS);
        if (!unlocked) pane.AddToClassList(LOCKED_CLASS);

        var image = new VisualElement();
        image.AddToClassList("map-pane-image");
        if (map.PreviewImage != null)
        {
            image.style.backgroundImage = new StyleBackground(map.PreviewImage);
        }
        pane.Add(image);

        var name = new Label(unlocked ? map.DisplayName : "? ? ?");
        name.AddToClassList("map-pane-name");
        pane.Add(name);

        if (unlocked && !string.IsNullOrEmpty(map.Tagline))
        {
            var tagline = new Label(map.Tagline);
            tagline.AddToClassList("map-pane-tagline");
            pane.Add(tagline);
        }

        var status = new Label(StatusText(map, unlocked));
        status.AddToClassList("map-pane-status");
        if (!unlocked) status.AddToClassList("map-pane-status--locked");
        else if (MapProgressStore.IsGateCleared(map.MapId)) status.AddToClassList("map-pane-status--cleared");
        pane.Add(status);

        pane.RegisterCallback<MouseEnterEvent>(_ => SetSelectedIndex(index));
        pane.RegisterCallback<ClickEvent>(_ =>
        {
            SetSelectedIndex(index);
            Confirm();
        });

        _paneRow.Add(pane);
        return pane;
    }

    private static string StatusText(MapDefinition map, bool unlocked)
    {
        if (!unlocked) return "LOCKED";
        if (MapProgressStore.IsTrueCleared(map.MapId)) return "CLEANSED";
        if (MapProgressStore.IsGateCleared(map.MapId)) return "GATE CLEARED";
        return "UNEXPLORED";
    }

    private void SetSelectedIndex(int index)
    {
        if (_panes.Count == 0) return;
        _index = Mathf.Clamp(index, 0, _panes.Count - 1);

        for (int i = 0; i < _panes.Count; i++)
        {
            if (i == _index) _panes[i].AddToClassList(SELECTED_CLASS);
            else _panes[i].RemoveFromClassList(SELECTED_CLASS);
        }
    }

    private void Navigate(int direction)
    {
        if (_panes.Count <= 1) return;
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiMove);
        SetSelectedIndex((_index + direction + _panes.Count) % _panes.Count);
    }

    private void Confirm()
    {
        if (_index < 0 || _index >= _maps.Count) return;
        var map = _maps[_index];

        // Locked maps stay visible so the campaign's shape is legible, but
        // they are not a destination
        if (!MapProgressStore.IsUnlocked(map)) return;

        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiConfirm);
        MapSelection.Select(map);
        OnMapChosen?.Invoke(map);
    }

    private void Close()
    {
        Hide();
        OnCloseRequested?.Invoke();
    }

    private void Update()
    {
        if (!IsVisible) return;
        if (Time.unscaledTime < _inputReadyTime) return;

        var gp = Gamepad.current;
        var kb = Keyboard.current;

        bool leftHeld = (gp != null && (gp.dpad.left.isPressed || gp.leftStick.left.isPressed))
                        || (kb != null && (kb.leftArrowKey.isPressed || kb.aKey.isPressed));
        bool rightHeld = (gp != null && (gp.dpad.right.isPressed || gp.leftStick.right.isPressed))
                         || (kb != null && (kb.rightArrowKey.isPressed || kb.dKey.isPressed));

        int rawHorizontal = (rightHeld ? 1 : 0) - (leftHeld ? 1 : 0);
        int horizontal = ApplyRepeat(rawHorizontal, ref _heldHorizontal, ref _nextHorizontalRepeat);

        bool confirm = MenuGamepad.SubmitPressed(gp)
                       || (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame));
        bool cancel = MenuGamepad.CancelPressed(gp)
                      || (kb != null && kb.escapeKey.wasPressedThisFrame);

        if (horizontal != 0) Navigate(horizontal);
        else if (confirm) Confirm();
        else if (cancel) Close();
    }

    // Edge-trigger with hold-to-repeat, matching TestModePanel's feel
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
