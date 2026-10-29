using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class DeathScreen : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private StyleSheet styleSheet;

    [Header("Scenes")]
    [SerializeField] private string mainSceneName = "Main";
    [SerializeField] private string campSceneName = "Camp";

    private VisualElement _root;
    private VisualElement _overlay;
    private Label _messageLabel;
    private Label _goldLabel;
    private Label _timeLabel;
    private Label _waveLabel;
    private Button _campButton;
    private Button _retryWaveButton;
    // Test Mode's row and its second button. In the UXML for every build; only a
    // test run in a dev build ever shows them.
    private VisualElement _deathPanel;
    private VisualElement _testActions;
    private Button _changeUpgradesButton;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Test Mode's own panel from the camp, instanced into this document from the
    // same UXML template and opened here by Change Upgrades. Added in code so the
    // scene needs no rewiring.
    private TestModePanel _testPanel;
#endif

    public static bool IsVisible { get; private set; }

    private void Awake()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        _testPanel = GetComponent<TestModePanel>();
        if (_testPanel == null) _testPanel = gameObject.AddComponent<TestModePanel>();
        _testPanel.InRun = true;
        _testPanel.OnCloseRequested += HandleTestPanelClosed;
#endif
    }

    private void OnEnable()
    {
        SetupUI();
    }

    private void OnDisable()
    {
        IsVisible = false;
    }

    private void SetupUI()
    {
        if (uiDocument == null)
        {
            Debug.LogWarning("DeathScreen missing UIDocument reference.");
            return;
        }

        _root = uiDocument.rootVisualElement;
        if (_root == null)
        {
            Debug.LogWarning("DeathScreen root visual element not found.");
            return;
        }

        if (styleSheet != null)
        {
            _root.styleSheets.Add(styleSheet);
        }

        _overlay = _root.Q<VisualElement>("death-screen");
        _messageLabel = _root.Q<Label>("death-message");
        _goldLabel = _root.Q<Label>("gold-amount");
        _timeLabel = _root.Q<Label>("time-amount");
        _waveLabel = _root.Q<Label>("wave-amount");
        _campButton = _root.Q<Button>("camp-button");
        _retryWaveButton = _root.Q<Button>("retry-wave-button");
        _deathPanel = _root.Q<VisualElement>("death-panel");
        _testActions = _root.Q<VisualElement>("death-test-actions");
        _changeUpgradesButton = _root.Q<Button>("change-upgrades-button");

        // The only way off this screen is back to camp (owner, 2026-10-05). There
        // used to be a Go Again button that started a new run from here.
        if (_campButton != null)
        {
            _campButton.clicked += OnReturnToCampClicked;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_retryWaveButton != null)
        {
            _retryWaveButton.clicked += OnRetryWaveClicked;
        }

        if (_changeUpgradesButton != null)
        {
            _changeUpgradesButton.clicked += OnChangeUpgradesClicked;
        }

        // This screen leaves moving between its buttons to the runtime's own focus
        // walk. Test Mode's panel reads the stick and A for itself, so while it is
        // up none of that may reach whatever is behind or inside it.
        _root.RegisterCallback<NavigationMoveEvent>(SwallowWhileTestPanelOpen, TrickleDown.TrickleDown);
        _root.RegisterCallback<NavigationSubmitEvent>(SwallowWhileTestPanelOpen, TrickleDown.TrickleDown);
        _root.RegisterCallback<NavigationCancelEvent>(SwallowWhileTestPanelOpen, TrickleDown.TrickleDown);
#endif

        Hide();
    }

    private bool EnsureUI()
    {
        // rootVisualElement can be null in the first frames after a UXML/USS
        // reimport, so retry setup lazily instead of trusting OnEnable
        if (_root == null || _overlay == null)
        {
            SetupUI();
        }

        return _overlay != null;
    }

    /// <summary>
    /// Show the death screen. knightName e.g. "Left Knight", killerName e.g. "Wolf",
    /// runSeconds is RunClock's count (time actually played), waveName e.g.
    /// "Rat Mischief" (the wave the player fell on).
    /// </summary>
    public void Show(string knightName, string killerName, int runGold, float runSeconds, int waveReached, string waveName = null)
    {
        if (!EnsureUI())
        {
            return;
        }

        if (_messageLabel != null)
        {
            _messageLabel.text = BuildMessage(knightName, killerName, waveName);
        }

        if (_goldLabel != null)
        {
            _goldLabel.text = runGold.ToString();
        }

        if (_timeLabel != null)
        {
            _timeLabel.text = RunClock.Format(runSeconds);
        }

        if (_waveLabel != null)
        {
            _waveLabel.text = Mathf.Max(1, waveReached).ToString();
        }

        // Dev builds: a Test Mode run gets a second row - re-fight the wave as you
        // were, or change the knights' upgrades first and then re-fight it
        if (_testActions != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _testActions.style.display = TestRunConfig.ActiveRun ? DisplayStyle.Flex : DisplayStyle.None;
#else
            _testActions.style.display = DisplayStyle.None;
#endif
        }

        if (_deathPanel != null)
        {
            _deathPanel.style.display = DisplayStyle.Flex;
        }

        _root.style.display = DisplayStyle.Flex;
        IsVisible = true;

        if (_campButton != null)
        {
            _campButton.Focus();
        }
    }

    public void Hide()
    {
        IsVisible = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_testPanel != null) _testPanel.Hide();
#endif

        if (_root != null)
        {
            _root.style.display = DisplayStyle.None;
        }
    }

    // e.g. "Left Knight died from Wolf on Rat Mischief". The wave name used to sit
    // under the wave number; it now rides in the death message instead.
    private static string BuildMessage(string knightName, string killerName, string waveName)
    {
        string knight = string.IsNullOrEmpty(knightName) ? "A Knight" : knightName;
        string onWave = string.IsNullOrEmpty(waveName) ? string.Empty : $" on {waveName}";

        if (string.IsNullOrEmpty(killerName))
        {
            return $"{knight} has fallen{onWave}";
        }

        return $"{knight} died from {killerName}{onWave}";
    }

    private void OnReturnToCampClicked()
    {
        if (TestPanelOpen) return;

        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiConfirm);
        Hide();

        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.LoadCampScene();
        }
        else
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(campSceneName);
        }
    }

    // Hide and reload the game scene. Only the dev-build retry below calls this
    // now: the Go Again button that used to is gone (owner, 2026-10-05).
    private void RestartRun()
    {
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiConfirm);
        Hide();

        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.LoadGameScene();
        }
        else
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainSceneName);
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Rebuild the exact run state (wave number + both loadouts, snapshotted
    // from the live UpgradeManager so mid-run picks are included) and force
    // the wave that killed you as the next wave.
    private void OnRetryWaveClicked()
    {
        if (TestPanelOpen) return;

        var waveManager = WaveManager.ActiveInstance;
        var upgradeManager = Resources.Load<UpgradeManager>("UpgradeManager");
        if (waveManager != null && upgradeManager != null)
        {
            TestRunConfig.Set(waveManager.CurrentWaveNumber,
                upgradeManager.GetAppliedUpgrades(KnightTarget.LeftKnight),
                upgradeManager.GetAppliedUpgrades(KnightTarget.RightKnight));
            TestRunConfig.RetryWave = waveManager.CurrentWave;
        }

        RestartRun();
    }

    private bool TestPanelOpen => _testPanel != null && _testPanel.IsVisible;

    // Opens Test Mode's upgrade list over this screen, loaded with what the
    // knights were holding. Applying there rebuilds the run and re-fights the
    // wave with the new picks (TestModePanel.InRun); backing out returns here.
    private void OnChangeUpgradesClicked()
    {
        if (_testPanel == null || TestPanelOpen) return;

        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiConfirm);
        // Hidden rather than just drawn over: a displayed button can still be
        // focused, and A is the panel's own button.
        if (_deathPanel != null) _deathPanel.style.display = DisplayStyle.None;
        (_root?.focusController?.focusedElement as VisualElement)?.Blur();
        _testPanel.Show();
    }

    private void HandleTestPanelClosed()
    {
        if (_deathPanel != null) _deathPanel.style.display = DisplayStyle.Flex;
        if (_changeUpgradesButton != null) _changeUpgradesButton.Focus();
    }

    private void SwallowWhileTestPanelOpen(EventBase evt)
    {
        if (!TestPanelOpen) return;
        evt.StopPropagation();
        _root?.focusController?.IgnoreEvent(evt);
    }
#else
    private bool TestPanelOpen => false;
#endif

    private void OnDestroy()
    {
        IsVisible = false;

        if (_campButton != null)
        {
            _campButton.clicked -= OnReturnToCampClicked;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_retryWaveButton != null)
        {
            _retryWaveButton.clicked -= OnRetryWaveClicked;
        }

        if (_changeUpgradesButton != null)
        {
            _changeUpgradesButton.clicked -= OnChangeUpgradesClicked;
        }

        if (_testPanel != null) _testPanel.OnCloseRequested -= HandleTestPanelClosed;
#endif
    }
}
