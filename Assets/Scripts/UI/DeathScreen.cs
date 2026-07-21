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
    private Label _waveLabel;
    private Button _campButton;
    private Button _goAgainButton;
    private Button _retryWaveButton;

    public static bool IsVisible { get; private set; }

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
        _waveLabel = _root.Q<Label>("wave-amount");
        _campButton = _root.Q<Button>("camp-button");
        _goAgainButton = _root.Q<Button>("go-again-button");
        _retryWaveButton = _root.Q<Button>("retry-wave-button");

        if (_campButton != null)
        {
            _campButton.clicked += OnReturnToCampClicked;
        }

        if (_goAgainButton != null)
        {
            _goAgainButton.clicked += OnGoAgainClicked;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_retryWaveButton != null)
        {
            _retryWaveButton.clicked += OnRetryWaveClicked;
        }
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
    /// Show the death screen. knightName e.g. "Left Knight", killerName e.g. "Wolf".
    /// </summary>
    public void Show(string knightName, string killerName, int runGold, int waveReached)
    {
        if (!EnsureUI())
        {
            return;
        }

        if (_messageLabel != null)
        {
            _messageLabel.text = BuildMessage(knightName, killerName);
        }

        if (_goldLabel != null)
        {
            _goldLabel.text = runGold.ToString();
        }

        if (_waveLabel != null)
        {
            _waveLabel.text = Mathf.Max(1, waveReached).ToString();
        }

        // Dev builds: a Test Mode run gets a third option to re-fight the wave
        if (_retryWaveButton != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _retryWaveButton.style.display = TestRunConfig.ActiveRun ? DisplayStyle.Flex : DisplayStyle.None;
#else
            _retryWaveButton.style.display = DisplayStyle.None;
#endif
        }

        _root.style.display = DisplayStyle.Flex;
        IsVisible = true;

        if (_goAgainButton != null)
        {
            _goAgainButton.Focus();
        }
    }

    public void Hide()
    {
        IsVisible = false;

        if (_root != null)
        {
            _root.style.display = DisplayStyle.None;
        }
    }

    private static string BuildMessage(string knightName, string killerName)
    {
        string knight = string.IsNullOrEmpty(knightName) ? "A Knight" : knightName;

        if (string.IsNullOrEmpty(killerName))
        {
            return $"{knight} has fallen";
        }

        return $"{knight} died from {killerName}";
    }

    private void OnReturnToCampClicked()
    {
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

    private void OnGoAgainClicked()
    {
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
        var waveManager = WaveManager.ActiveInstance;
        var upgradeManager = Resources.Load<UpgradeManager>("UpgradeManager");
        if (waveManager != null && upgradeManager != null)
        {
            TestRunConfig.Set(waveManager.CurrentWaveNumber,
                upgradeManager.GetAppliedUpgrades(KnightTarget.LeftKnight),
                upgradeManager.GetAppliedUpgrades(KnightTarget.RightKnight));
            TestRunConfig.RetryWave = waveManager.CurrentWave;
        }

        OnGoAgainClicked(); // hide + reload the game scene
    }
#endif

    private void OnDestroy()
    {
        IsVisible = false;

        if (_campButton != null)
        {
            _campButton.clicked -= OnReturnToCampClicked;
        }

        if (_goAgainButton != null)
        {
            _goAgainButton.clicked -= OnGoAgainClicked;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_retryWaveButton != null)
        {
            _retryWaveButton.clicked -= OnRetryWaveClicked;
        }
#endif
    }
}
