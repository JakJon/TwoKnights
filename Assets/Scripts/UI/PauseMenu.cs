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
    private VisualElement _mainActions;
    private VisualElement _confirmActions;
    private VisualElement _loadoutLeftList;
    private VisualElement _loadoutRightList;
    private Label _waveLabel;
    private Button _resumeButton;
    private Button _quitButton;
    private Button _confirmYesButton;
    private Button _confirmNoButton;

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
    }

    private void OnEnable()
    {
        SetupUI();
        RegisterInput(togglePauseAction, OnTogglePause, true);
    }

    private void OnDisable()
    {
        RegisterInput(togglePauseAction, OnTogglePause, false);
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

        _mainActions = _root.Q<VisualElement>("main-actions");
        _confirmActions = _root.Q<VisualElement>("confirm-actions");
        _loadoutLeftList = _root.Q<VisualElement>("loadout-left-list");
        _loadoutRightList = _root.Q<VisualElement>("loadout-right-list");
        _waveLabel = _root.Q<Label>("pause-wave");
        _resumeButton = _root.Q<Button>("resume-button");
        _quitButton = _root.Q<Button>("quit-button");
        _confirmYesButton = _root.Q<Button>("confirm-yes");
        _confirmNoButton = _root.Q<Button>("confirm-no");

        if (_resumeButton != null)
        {
            _resumeButton.clicked += OnResumeClicked;
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
        if (!IsInMainScene() || DeathScreen.IsVisible || QuestCompletePanel.IsVisible)
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

        if (!IsInMainScene() || DeathScreen.IsVisible || QuestCompletePanel.IsVisible)
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
        HideMenu();
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
