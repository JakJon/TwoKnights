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
        if (!IsInMainScene())
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

        if (!IsInMainScene())
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
