using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class PauseMenu : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private StyleSheet styleSheet;

    [Header("Input")]
    [SerializeField] private InputActionReference togglePauseAction;

    [Header("Scenes")]
    [SerializeField] private string mainSceneName = "Main";
    [SerializeField] private string campSceneName = "Camp";

    private VisualElement _root;
    private VisualElement _pausePanel;
    private VisualElement _mainActions;
    private VisualElement _confirmActions;
    private Label _waveLabel;
    private Button _resumeButton;
    private Button _questsButton;
    private Button _equipmentButton;
    private Button _quitButton;
    private Button _confirmYesButton;
    private Button _confirmNoButton;

    // The camp's quest log and equipment sheet, instanced into this document
    // from the same UXML templates. Added in code so the scene needs no
    // rewiring — the panels find their elements through this GameObject's
    // UIDocument, and both poll their own input off unscaled time, which is
    // what lets them work with the game frozen.
    private QuestPanel _questPanel;
    private EquipmentPanel _equipmentPanel;

    // The button to hand focus back to when a sub-panel closes
    private Button _returnFocus;

    private bool _isPaused;
    private bool _confirmingQuit;
    private float _previousTimeScale = 1f;

    private const string OVERLAY_CLASS = "pause-menu-overlay";

    public static bool IsPaused { get; private set; }

    // ---------- loadout columns ----------

    // Left list, centre menu, right list. Ordered so a step of one walks them,
    // which is all the horizontal navigation below actually is.
    private enum Column { Left = 0, Center = 1, Right = 2 }

    // One knight's side of the panel: the scrolling list of chains they own, and
    // the undressed readout underneath that describes whichever row is selected.
    private class LoadoutColumn
    {
        public ScrollView Scroll;
        public VisualElement List;
        public Label More;
        public Label DetailName;
        public Label DetailDesc;
        public VisualElement DetailStats;
        public readonly List<VisualElement> Rows = new List<VisualElement>();
        public readonly List<BaseUpgrade> Upgrades = new List<BaseUpgrade>();
        public int Index;
    }

    private readonly LoadoutColumn _left = new LoadoutColumn();
    private readonly LoadoutColumn _right = new LoadoutColumn();

    // Remembered so returning to the centre lands on the button you left from
    private int _centerIndex;

    // A list appearing under a resting cursor fires PointerEnter without the
    // player moving the mouse; that would yank focus off RESUME the instant you
    // pause. Ignore hover until the panel has settled.
    private float _hoverReadyTime;
    private const float HoverGrace = 0.25f;

    private static readonly string[] AllOrderClasses =
    {
        "order--serpent", "order--shadow", "order--ember", "order--guardian", "order--dawn",
        "order--frigid"
    };

    private void Awake()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        _questPanel = GetComponent<QuestPanel>();
        if (_questPanel == null) _questPanel = gameObject.AddComponent<QuestPanel>();
        _equipmentPanel = GetComponent<EquipmentPanel>();
        if (_equipmentPanel == null) _equipmentPanel = gameObject.AddComponent<EquipmentPanel>();
        _equipmentPanel.ReadOnly = true;
    }

    private void OnEnable()
    {
        SetupUI();
        RegisterInput(togglePauseAction, OnTogglePause, true);
        if (_questPanel != null) _questPanel.OnCloseRequested += HandleSubPanelClosed;
        if (_equipmentPanel != null) _equipmentPanel.OnCloseRequested += HandleSubPanelClosed;
    }

    private void OnDisable()
    {
        RegisterInput(togglePauseAction, OnTogglePause, false);
        if (_questPanel != null) _questPanel.OnCloseRequested -= HandleSubPanelClosed;
        if (_equipmentPanel != null) _equipmentPanel.OnCloseRequested -= HandleSubPanelClosed;
        ResumeGameInternal(resetInput: false);
    }

    private void SetupUI()
    {
        if (uiDocument == null)
        {
            Debug.LogWarning("PauseMenu missing UIDocument reference.");
            return;
        }

        _root = uiDocument.rootVisualElement;
        if (_root == null)
        {
            Debug.LogWarning("PauseMenu root visual element not found.");
            return;
        }

        if (styleSheet != null)
        {
            _root.styleSheets.Add(styleSheet);
        }

        // Navigation is driven explicitly below (three columns, empty lists
        // skipped), so the runtime focus walk has to go: left alone it moves the
        // highlight a second time on top of ours, and with a sub-panel open it
        // walks focus through the hidden card behind it. Submit and Cancel are
        // deliberately NOT swallowed — the centre buttons still activate through
        // the focused element, exactly as they always have.
        _root.RegisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);

        _pausePanel = _root.Q<VisualElement>("pause-panel");
        _mainActions = _root.Q<VisualElement>("main-actions");
        _confirmActions = _root.Q<VisualElement>("confirm-actions");
        _waveLabel = _root.Q<Label>("pause-wave");
        _resumeButton = _root.Q<Button>("resume-button");
        _questsButton = _root.Q<Button>("pause-quests-button");
        _equipmentButton = _root.Q<Button>("pause-equipment-button");
        _quitButton = _root.Q<Button>("quit-button");
        _confirmYesButton = _root.Q<Button>("confirm-yes");
        _confirmNoButton = _root.Q<Button>("confirm-no");

        BindColumn(_left, "left");
        BindColumn(_right, "right");

        if (_resumeButton != null)
        {
            _resumeButton.clicked += OnResumeClicked;
        }

        if (_questsButton != null)
        {
            _questsButton.clicked += OnQuestsClicked;
        }

        if (_equipmentButton != null)
        {
            _equipmentButton.clicked += OnEquipmentClicked;
        }

        if (_quitButton != null)
        {
            _quitButton.clicked += OnQuitClicked;
        }

        if (_confirmYesButton != null)
        {
            _confirmYesButton.clicked += OnConfirmYesClicked;
        }

        if (_confirmNoButton != null)
        {
            _confirmNoButton.clicked += OnConfirmNoClicked;
        }

        // Reaching the centre by mouse has to empty both readouts too, or a stale
        // description sits under a list nobody is in.
        foreach (var button in _root.Query<Button>(className: "menu-button").ToList())
        {
            button.RegisterCallback<FocusInEvent>(OnCenterButtonFocused);
        }

        HideConfirmPrompt();
        HideMenu();
    }

    private void BindColumn(LoadoutColumn col, string side)
    {
        col.Scroll = _root.Q<ScrollView>($"loadout-{side}-scroll");
        col.List = _root.Q<VisualElement>($"loadout-{side}-list");
        col.More = _root.Q<Label>($"loadout-{side}-more");
        col.DetailName = _root.Q<Label>($"detail-{side}-name");
        col.DetailDesc = _root.Q<Label>($"detail-{side}-desc");
        col.DetailStats = _root.Q<VisualElement>($"detail-{side}-stats");

        if (col.Scroll == null)
        {
            return;
        }

        col.Scroll.mode = ScrollViewMode.Vertical;
        col.Scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        col.Scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
        col.Scroll.verticalScroller.valueChanged += _ => RefreshMoreArrow(col);
        col.Scroll.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => RefreshMoreArrow(col));
    }

    private void RegisterInput(InputActionReference actionRef, System.Action<InputAction.CallbackContext> handler, bool enable)
    {
        if (actionRef == null)
        {
            return;
        }

        if (enable)
        {
            actionRef.action.performed += handler;
            actionRef.action.Enable();
        }
        else
        {
            actionRef.action.performed -= handler;
            actionRef.action.Disable();
        }
    }

    private void OnTogglePause(InputAction.CallbackContext context)
    {
        if (!IsInMainScene() || DeathScreen.IsVisible || WaveSurvivedPanel.IsVisible)
        {
            return;
        }

        // A sub-panel owns the screen: B backs out of it, and unpausing straight
        // into the game from underneath the quest log would strand it open
        if (SubPanelOpen)
        {
            return;
        }

        if (_isPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        if (_isPaused)
        {
            return;
        }

        if (!IsInMainScene() || DeathScreen.IsVisible || WaveSurvivedPanel.IsVisible)
        {
            return;
        }

        if (!EnsureUI())
        {
            return;
        }

        _previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        SetPausedState(true);
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiOpen);

        UpdateWaveLabel();
        UpdateLoadout();
        HideConfirmPrompt();
        CloseSubPanels();
        ShowMenu();
        _hoverReadyTime = Time.unscaledTime + HoverGrace;
        _centerIndex = 0;
        FocusResumeButton();
    }

    public void ResumeGame()
    {
        ResumeGameInternal(resetInput: true);
    }

    private void ResumeGameInternal(bool resetInput)
    {
        if (!_isPaused && !_confirmingQuit)
        {
            return;
        }

        if (resetInput)
        {
            if (Mathf.Approximately(Time.timeScale, 0f))
            {
                Time.timeScale = _previousTimeScale <= 0f ? 1f : _previousTimeScale;
            }
        }
        else if (Mathf.Approximately(Time.timeScale, 0f))
        {
            Time.timeScale = 1f;
        }

        SetPausedState(false);
        _confirmingQuit = false;
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiCancel);

        if (!EnsureUI())
        {
            return;
        }

        HideConfirmPrompt();
        CloseSubPanels();
        HideMenu();
    }

    // ---------- navigation ----------

    // Left list to centre menu to right list, wrapping both ways. A knight with
    // nothing drafted yet has no list to sit in, so that side is stepped over.
    private void OnNavigationMove(NavigationMoveEvent evt)
    {
        evt.StopPropagation();
        _root?.focusController?.IgnoreEvent(evt);

        if (!_isPaused || SubPanelOpen || !PausePanelVisible)
        {
            return;
        }

        switch (evt.direction)
        {
            case NavigationMoveEvent.Direction.Left:
                MoveHorizontal(-1);
                break;
            case NavigationMoveEvent.Direction.Right:
                MoveHorizontal(1);
                break;
            case NavigationMoveEvent.Direction.Up:
                MoveVertical(-1);
                break;
            case NavigationMoveEvent.Direction.Down:
                MoveVertical(1);
                break;
        }
    }

    private void MoveHorizontal(int step)
    {
        // The quit prompt lays YES and NO out side by side; horizontal walks
        // those two rather than abandoning the question.
        if (_confirmingQuit)
        {
            StepCenter(step);
            return;
        }

        int cursor = (int)CurrentColumn();
        for (int i = 0; i < 3; i++)
        {
            cursor = (cursor + step + 3) % 3;
            if (cursor == (int)Column.Center)
            {
                FocusCenter();
                PlayMove();
                return;
            }

            var col = cursor == (int)Column.Left ? _left : _right;
            if (col.Rows.Count > 0)
            {
                SelectRow(col, Mathf.Clamp(col.Index, 0, col.Rows.Count - 1));
                PlayMove();
                return;
            }
        }
    }

    private void MoveVertical(int step)
    {
        switch (CurrentColumn())
        {
            case Column.Left:
                StepRow(_left, step);
                break;
            case Column.Right:
                StepRow(_right, step);
                break;
            default:
                StepCenter(step);
                break;
        }
    }

    // Where focus actually is, rather than where we last put it — a mouse click
    // can move it without going through the code above.
    private Column CurrentColumn()
    {
        if (_confirmingQuit)
        {
            return Column.Center;
        }

        var focused = _root?.focusController?.focusedElement as VisualElement;
        if (focused != null)
        {
            if (_left.Rows.Contains(focused)) return Column.Left;
            if (_right.Rows.Contains(focused)) return Column.Right;
        }

        return Column.Center;
    }

    private void StepCenter(int step)
    {
        var buttons = VisibleActionButtons();
        if (buttons.Count == 0)
        {
            return;
        }

        int index = buttons.IndexOf(_root?.focusController?.focusedElement as Button);
        if (index < 0)
        {
            index = Mathf.Clamp(_centerIndex, 0, buttons.Count - 1);
        }

        _centerIndex = (index + step + buttons.Count) % buttons.Count;
        buttons[_centerIndex].Focus();
        PlayMove();
    }

    private void StepRow(LoadoutColumn col, int step)
    {
        if (col.Rows.Count == 0)
        {
            FocusCenter();
            return;
        }

        SelectRow(col, (col.Index + step + col.Rows.Count) % col.Rows.Count);
        PlayMove();
    }

    private List<Button> VisibleActionButtons()
    {
        var container = _confirmingQuit ? _confirmActions : _mainActions;
        return container == null ? new List<Button>() : container.Query<Button>().ToList();
    }

    private void FocusCenter()
    {
        ClearDetail(_left);
        ClearDetail(_right);

        var buttons = VisibleActionButtons();
        if (buttons.Count == 0)
        {
            return;
        }

        _centerIndex = Mathf.Clamp(_centerIndex, 0, buttons.Count - 1);
        buttons[_centerIndex].Focus();
    }

    private void SelectRow(LoadoutColumn col, int index)
    {
        if (index < 0 || index >= col.Rows.Count)
        {
            return;
        }

        col.Index = index;
        ClearDetail(_left);
        ClearDetail(_right);
        ShowDetail(col, index);

        var row = col.Rows[index];
        row.Focus();
        col.Scroll?.ScrollTo(row);
        RefreshMoreArrow(col);
    }

    private void OnCenterButtonFocused(FocusInEvent evt)
    {
        ClearDetail(_left);
        ClearDetail(_right);
    }

    private static void PlayMove()
    {
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiMove);
    }

    private bool PausePanelVisible => _pausePanel != null && _pausePanel.style.display != DisplayStyle.None;

    // ---------- sub-panels ----------

    private bool SubPanelOpen =>
        (_questPanel != null && _questPanel.IsVisible) ||
        (_equipmentPanel != null && _equipmentPanel.IsVisible);

    private void OnQuestsClicked()
    {
        if (!_isPaused || _questPanel == null) return;
        _returnFocus = _questsButton;
        SetPausePanelVisible(false);
        _questPanel.Show();
    }

    private void OnEquipmentClicked()
    {
        if (!_isPaused || _equipmentPanel == null) return;
        _returnFocus = _equipmentButton;
        SetPausePanelVisible(false);
        _equipmentPanel.Show();
    }

    private void HandleSubPanelClosed()
    {
        SetPausePanelVisible(true);
        _hoverReadyTime = Time.unscaledTime + HoverGrace;
        if (_returnFocus != null) _returnFocus.Focus();
        else FocusResumeButton();
    }

    private void CloseSubPanels()
    {
        if (_questPanel != null) _questPanel.Hide();
        if (_equipmentPanel != null) _equipmentPanel.Hide();
        _returnFocus = null;
        SetPausePanelVisible(true);
    }

    // Hiding the card outright, rather than just drawing over it: a displayed
    // button is still focusable, and the focus ring would keep walking through
    // RESUME and QUIT behind the open panel.
    private void SetPausePanelVisible(bool visible)
    {
        if (_pausePanel == null) return;
        _pausePanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void PauseForQuitConfirm()
    {
        _confirmingQuit = true;
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiConfirm);
        ClearDetail(_left);
        ClearDetail(_right);
        if (_mainActions != null)
        {
            _mainActions.style.display = DisplayStyle.None;
        }
        if (_confirmActions != null)
        {
            _confirmActions.style.display = DisplayStyle.Flex;
        }
        if (_confirmNoButton != null)
        {
            _centerIndex = Mathf.Max(0, VisibleActionButtons().IndexOf(_confirmNoButton));
            _confirmNoButton.Focus();
        }
    }

    private void HideConfirmPrompt()
    {
        _confirmingQuit = false;
        if (_mainActions != null)
        {
            _mainActions.style.display = DisplayStyle.Flex;
        }
        if (_confirmActions != null)
        {
            _confirmActions.style.display = DisplayStyle.None;
        }
    }

    private bool EnsureUI()
    {
        if (_root == null)
        {
            SetupUI();
        }

        return _root != null;
    }

    private void ShowMenu()
    {
        if (_root == null)
        {
            return;
        }

        _root.style.display = DisplayStyle.Flex;
    }

    private void HideMenu()
    {
        if (_root == null)
        {
            return;
        }

        _root.style.display = DisplayStyle.None;
    }

    private void UpdateWaveLabel()
    {
        if (_waveLabel == null)
        {
            return;
        }

        var waveManager = WaveManager.ActiveInstance;
        if (waveManager != null)
        {
            string label = $"WAVE {NumberConverter.ToRoman(waveManager.CurrentWaveNumber)}";
            var wave = waveManager.CurrentWave;
            if (wave != null && !string.IsNullOrEmpty(wave.WaveName))
            {
                label += $" · {wave.WaveName.ToUpperInvariant()}";
            }
            _waveLabel.text = label;
            _waveLabel.style.display = DisplayStyle.Flex;
        }
        else
        {
            _waveLabel.style.display = DisplayStyle.None;
        }
    }

    // Fill both knight columns with the upgrades they have drafted this run, one
    // row per chain (highest tier only), color-railed by Order like the draft cards.
    private void UpdateLoadout()
    {
        var manager = Resources.Load<UpgradeManager>("UpgradeManager");
        PopulateColumn(_left, manager, KnightTarget.LeftKnight);
        PopulateColumn(_right, manager, KnightTarget.RightKnight);
    }

    private void PopulateColumn(LoadoutColumn col, UpgradeManager manager, KnightTarget target)
    {
        col.Rows.Clear();
        col.Upgrades.Clear();
        col.Index = 0;

        if (col.List == null)
        {
            return;
        }

        col.List.Clear();

        if (manager != null)
        {
            foreach (var row in manager.GetAppliedUpgradeSummary(target))
            {
                var chip = new Label(row.Name);
                chip.AddToClassList("loadout-chip");
                string orderClass = OrderClass(row.Order);
                if (orderClass != null)
                {
                    chip.AddToClassList(orderClass);
                }

                // Focusable so one highlight serves gamepad and mouse alike, but a
                // plain element rather than a Button: Submit on a row does nothing,
                // because there is nothing here to activate.
                chip.focusable = true;

                var owner = col;
                int index = col.Rows.Count;
                chip.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    if (Time.unscaledTime < _hoverReadyTime) return;
                    SelectRow(owner, index);
                });

                col.List.Add(chip);
                col.Rows.Add(chip);
                col.Upgrades.Add(row.Upgrade);
            }
        }

        if (col.Rows.Count == 0)
        {
            var empty = new Label("No upgrades yet");
            empty.AddToClassList("loadout-empty");
            col.List.Add(empty);
        }

        if (col.Scroll != null)
        {
            col.Scroll.scrollOffset = Vector2.zero;
        }

        ClearDetail(col);
        RefreshMoreArrow(col);
    }

    // The list is capped in height; when it holds more than fits, a small caret
    // under it says so, and hides again once you have scrolled to the end.
    private void RefreshMoreArrow(LoadoutColumn col)
    {
        if (col.More == null)
        {
            return;
        }

        var scroller = col.Scroll?.verticalScroller;
        bool overflows = scroller != null && scroller.highValue > 1f;
        bool atEnd = !overflows || col.Scroll.scrollOffset.y >= scroller.highValue - 1f;
        col.More.style.display = overflows && !atEnd ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void ShowDetail(LoadoutColumn col, int index)
    {
        var upgrade = index >= 0 && index < col.Upgrades.Count ? col.Upgrades[index] : null;
        if (upgrade == null)
        {
            ClearDetail(col);
            return;
        }

        if (col.DetailName != null)
        {
            col.DetailName.text = upgrade.UpgradeName;
            SetOrderClass(col.DetailName, upgrade.Order);
        }

        if (col.DetailDesc != null)
        {
            col.DetailDesc.text = upgrade.Description;
        }

        if (col.DetailStats == null)
        {
            return;
        }

        col.DetailStats.Clear();
        foreach (var stat in upgrade.Stats)
        {
            string arrow = stat.isPositive ? "▲" : "▼";
            string text = string.IsNullOrEmpty(stat.labelText)
                ? $"{arrow} {stat.valueText}"
                : $"{arrow} {stat.valueText} {stat.labelText}";
            var label = new Label(text);
            label.AddToClassList("detail-stat");
            label.AddToClassList(stat.isPositive ? "detail-stat--buff" : "detail-stat--bane");
            col.DetailStats.Add(label);
        }
    }

    private void ClearDetail(LoadoutColumn col)
    {
        if (col.DetailName != null)
        {
            col.DetailName.text = string.Empty;
            SetOrderClass(col.DetailName, UpgradeOrder.Neutral);
        }

        if (col.DetailDesc != null)
        {
            col.DetailDesc.text = string.Empty;
        }

        col.DetailStats?.Clear();
    }

    private static void SetOrderClass(VisualElement element, UpgradeOrder order)
    {
        foreach (var cls in AllOrderClasses)
        {
            element.RemoveFromClassList(cls);
        }

        string wanted = OrderClass(order);
        if (wanted != null)
        {
            element.AddToClassList(wanted);
        }
    }

    private static string OrderClass(UpgradeOrder order)
    {
        switch (order)
        {
            case UpgradeOrder.Serpent: return "order--serpent";
            case UpgradeOrder.Shadow: return "order--shadow";
            case UpgradeOrder.Ember: return "order--ember";
            case UpgradeOrder.Guardian: return "order--guardian";
            case UpgradeOrder.Dawn: return "order--dawn";
            case UpgradeOrder.Frigid: return "order--frigid";
            default: return null; // Neutral: no color rail
        }
    }

    private void FocusResumeButton()
    {
        ClearDetail(_left);
        ClearDetail(_right);
        if (_resumeButton != null)
        {
            _resumeButton.Focus();
        }
    }

    private bool IsInMainScene()
    {
        var activeScene = SceneManager.GetActiveScene();
        return activeScene.IsValid() && activeScene.name == mainSceneName;
    }

    private void OnResumeClicked()
    {
        ResumeGame();
    }

    private void OnQuitClicked()
    {
        if (!_isPaused)
        {
            return;
        }

        PauseForQuitConfirm();
    }

    private void OnConfirmYesClicked()
    {
        HideConfirmPrompt();
        CloseSubPanels();
        HideMenu();
        Time.timeScale = 1f;
        SetPausedState(false);
        _confirmingQuit = false;
        SceneManager.LoadScene(campSceneName);
    }

    private void OnConfirmNoClicked()
    {
        HideConfirmPrompt();
        _centerIndex = 0;
        FocusResumeButton();
    }

    private void OnDestroy()
    {
        RegisterInput(togglePauseAction, OnTogglePause, false);
        ResumeGameInternal(resetInput: true);

        if (_resumeButton != null)
        {
            _resumeButton.clicked -= OnResumeClicked;
        }
        if (_questsButton != null)
        {
            _questsButton.clicked -= OnQuestsClicked;
        }
        if (_equipmentButton != null)
        {
            _equipmentButton.clicked -= OnEquipmentClicked;
        }
        if (_quitButton != null)
        {
            _quitButton.clicked -= OnQuitClicked;
        }
        if (_confirmYesButton != null)
        {
            _confirmYesButton.clicked -= OnConfirmYesClicked;
        }
        if (_confirmNoButton != null)
        {
            _confirmNoButton.clicked -= OnConfirmNoClicked;
        }
    }

    private void SetPausedState(bool paused)
    {
        _isPaused = paused;
        IsPaused = paused;
    }
}
