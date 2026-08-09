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
    private VisualElement _loadoutLeftList;
    private VisualElement _loadoutRightList;
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

        _pausePanel = _root.Q<VisualElement>("pause-panel");
        _mainActions = _root.Q<VisualElement>("main-actions");
        _confirmActions = _root.Q<VisualElement>("confirm-actions");
        _loadoutLeftList = _root.Q<VisualElement>("loadout-left-list");
        _loadoutRightList = _root.Q<VisualElement>("loadout-right-list");
        _waveLabel = _root.Q<Label>("pause-wave");
        _resumeButton = _root.Q<Button>("resume-button");
        _questsButton = _root.Q<Button>("pause-quests-button");
        _equipmentButton = _root.Q<Button>("pause-equipment-button");
        _quitButton = _root.Q<Button>("quit-button");
        _confirmYesButton = _root.Q<Button>("confirm-yes");
        _confirmNoButton = _root.Q<Button>("confirm-no");

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

        HideConfirmPrompt();
        HideMenu();
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

    // Fill both knight columns with the upgrades they've drafted this run, one row
    // per chain (highest tier only), color-railed by Order like the draft cards.
    private void UpdateLoadout()
    {
        var manager = Resources.Load<UpgradeManager>("UpgradeManager");
        PopulateColumn(_loadoutLeftList, manager, KnightTarget.LeftKnight);
        PopulateColumn(_loadoutRightList, manager, KnightTarget.RightKnight);
    }

    private void PopulateColumn(VisualElement list, UpgradeManager manager, KnightTarget target)
    {
        if (list == null)
        {
            return;
        }

        list.Clear();

        bool any = false;
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
                list.Add(chip);
                any = true;
            }
        }

        if (!any)
        {
            var empty = new Label("No upgrades yet");
            empty.AddToClassList("loadout-empty");
            list.Add(empty);
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
            default: return null; // Neutral: no color rail
        }
    }

    private void FocusResumeButton()
    {
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
