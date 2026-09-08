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
        // The scrim is a CHILD of the well rather than a sibling so it tracks the
        // artwork's bounds for free. That is also why the locked art is drained
        // with a background tint instead of opacity — opacity would cascade onto
        // the padlock and leave it as faint as the thing it is covering.
        if (!unlocked) image.Add(BuildLockBadge());
        // Earned by taking this map past MapProgressStore.StarWaveNumber. It sits
        // in the artwork's top-right corner, over the picture rather than beside
        // the name, so a shelf of maps can be read for stars at a glance.
        else if (MapProgressStore.HasStar(map.MapId)) image.Add(BuildStar());
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
        else if (!unlocked)
        {
            var hint = new Label(LockedHintFor(map));
            hint.AddToClassList("map-pane-tagline");
            pane.Add(hint);
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

    // What the player has to do to open this map. Authored per map when it can be
    // named concretely ("Defeat the Rat King"); otherwise derived, so a newly
    // added map is never locked behind an unexplained blank.
    private static string LockedHintFor(MapDefinition map)
    {
        if (!string.IsNullOrEmpty(map.LockedHint)) return map.LockedHint;
        var unlocker = MapProgressStore.UnlockerOf(map.MapId);
        return unlocker != null ? $"Clear the gate of {unlocker.DisplayName}" : "Not yet open";
    }

    // A padlock built from two boxes: the camp UI has no icon atlas, and a glyph
    // would depend on the serif font happening to ship one.
    private static VisualElement BuildLockBadge()
    {
        var scrim = new VisualElement();
        scrim.AddToClassList("map-pane-scrim");

        var padlock = new VisualElement();
        padlock.AddToClassList("map-lock");

        var shackle = new VisualElement();
        shackle.AddToClassList("map-lock-shackle");
        padlock.Add(shackle);

        var body = new VisualElement();
        body.AddToClassList("map-lock-body");
        padlock.Add(body);

        scrim.Add(padlock);
        return scrim;
    }

    // A six-pointed star built from three crossed bars. Same reasoning as the
    // padlock above: the camp UI has no icon atlas, and a star GLYPH would depend
    // on the serif font happening to ship one — on a platform where it does not,
    // the reward for twenty waves would render as a hollow box.
    private static VisualElement BuildStar()
    {
        var star = new VisualElement();
        star.AddToClassList("map-star");

        var halo = new VisualElement();
        halo.AddToClassList("map-star-halo");
        star.Add(halo);

        // The angles are set here rather than in USS. A rotated bar is the whole
        // shape, and a stylesheet property that silently failed to apply would
        // leave three bars stacked into one stripe rather than a star.
        foreach (float degrees in new[] { 0f, 60f, 120f })
        {
            var ray = new VisualElement();
            ray.AddToClassList("map-star-ray");
            ray.style.rotate = new StyleRotate(new Rotate(new Angle(degrees, AngleUnit.Degree)));
            star.Add(ray);
        }

        return star;
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
        // they are not a destination. Answer the press rather than eating it —
        // silence reads as a dropped input, not as a refusal.
        if (!MapProgressStore.IsUnlocked(map))
        {
            AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiCancel);
            return;
        }

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
