using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(UIDocument))]
public class CampMenuController : MonoBehaviour
{
    [Header("Button Names")]
    [SerializeField] private string returnButtonName = "return-button";
    [SerializeField] private string questsButtonName = "quests-button";
    [SerializeField] private string statsButtonName = "stats-button";
    [SerializeField] private string resetButtonName = "reset-button";
    [SerializeField] private string exitButtonName = "exit-button";

    [Header("Scene Names")]
    [SerializeField] private string gameplaySceneName = "Main";

    [Header("Sub Panels")]
    [SerializeField] private QuestPanel questPanel;
    [SerializeField] private StatsPanel statsPanel;
    [SerializeField] private string menuContainerName = "menu-container";

    [Header("Input Actions")]
    [SerializeField] private InputActionReference navigateUpAction;
    [SerializeField] private InputActionReference navigateDownAction;
    [SerializeField] private InputActionReference confirmAction;
    [SerializeField] private InputActionReference cancelAction;

    [Header("Input Settings")]
    [SerializeField] private float inputCooldown = 0.2f;
    [Tooltip("Ignore all input this long after the menu appears — swallows held buttons and first-frame phantom input after a scene load")]
    [SerializeField] private float entryInputDelay = 0.4f;

    private UIDocument _uiDocument;
    private VisualElement _root;
    private VisualElement _menuContainer;
    private readonly List<Button> _menuButtons = new();
    private readonly List<Action> _buttonHandlers = new();
    private readonly List<Action> _clickedWrappers = new();
    private int _currentIndex;
    private float _lastInputTime;
    private Button _resetButton;
    private bool _resetArmed;

    private const string SELECTED_CLASS = "menu-button--selected";
    private const string ARMED_CLASS = "menu-button--armed";
    private const string RESET_LABEL = "Reset All Data";
    private const string RESET_CONFIRM_LABEL = "Confirm Wipe?";

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
        if (questPanel == null) questPanel = GetComponent<QuestPanel>();
        if (statsPanel == null) statsPanel = GetComponent<StatsPanel>();
    }

    private void OnEnable()
    {
        // Arm the input grace period BEFORE anything can read input: without this,
        // _lastInputTime is 0 and the cooldown is long expired mid-session, so a
        // held button or a first-frame phantom Submit/Cancel (connected gamepads
        // read as actuated on the first sampled frame after a scene load) would
        // instantly activate the default-selected Return button and bounce the
        // camp straight back into the game scene.
        _lastInputTime = Time.unscaledTime + entryInputDelay - inputCooldown;

        RegisterCallbacks();
        HookAction(navigateUpAction, OnNavigateUp, true);
        HookAction(navigateDownAction, OnNavigateDown, true);
        HookAction(confirmAction, OnConfirm, true);
        HookAction(cancelAction, OnCancel, true);
        SetSelectedIndex(Mathf.Clamp(_currentIndex, 0, _menuButtons.Count - 1));
        if (questPanel != null) questPanel.OnCloseRequested += HandleSubPanelClosed;
        if (statsPanel != null) statsPanel.OnCloseRequested += HandleSubPanelClosed;
    }

    private void OnDisable()
    {
        HookAction(navigateUpAction, OnNavigateUp, false);
        HookAction(navigateDownAction, OnNavigateDown, false);
        HookAction(confirmAction, OnConfirm, false);
        HookAction(cancelAction, OnCancel, false);
        UnregisterCallbacks();
        if (questPanel != null) questPanel.OnCloseRequested -= HandleSubPanelClosed;
        if (statsPanel != null) statsPanel.OnCloseRequested -= HandleSubPanelClosed;
    }

    private void Update()
    {
        HandleFallbackInput();
    }

    private void RegisterCallbacks()
    {
        if (_uiDocument == null)
        {
            Debug.LogWarning("CampMenuController requires a UIDocument reference.");
            return;
        }

        _root = _uiDocument.rootVisualElement;
        if (_root == null)
        {
            Debug.LogWarning("CampMenuController could not access the root VisualElement.");
            return;
        }

        _menuContainer = _root.Q<VisualElement>(menuContainerName);
        _menuButtons.Clear();
        _buttonHandlers.Clear();
        _clickedWrappers.Clear();

        foreach (var button in _root.Query<Button>(className: "menu-button").ToList())
        {
            _menuButtons.Add(button);
            Action handler = null;

            switch (button.name)
            {
                case var name when name == returnButtonName:
                    handler = HandleReturnClicked;
                    break;
                case var name when name == questsButtonName:
                    handler = HandleQuestsClicked;
                    break;
                case var name when name == statsButtonName:
                    handler = HandleStatsClicked;
                    break;
                case var name when name == resetButtonName:
                    handler = HandleResetClicked;
                    _resetButton = button;
                    break;
                case var name when name == exitButtonName:
                    handler = HandleExitClicked;
                    break;
            }

            // clicked fires from pointer clicks AND from UI Toolkit navigation
            // submit on the focused button — the latter bypasses our input
            // cooldown entirely, so a first-frame phantom Submit (gamepad axis
            // sampled right after a scene load) could instantly activate the
            // focused Return button and bounce camp back into the game. Gate
            // every click through the same cooldown/grace window.
            Action wrapper = null;
            if (handler != null)
            {
                var captured = handler;
                wrapper = () =>
                {
                    if (!CanProcessInput()) return;
                    _lastInputTime = Time.unscaledTime;
                    captured();
                };
                button.clicked += wrapper;
            }

            var index = _menuButtons.Count - 1;
            button.RegisterCallback<MouseEnterEvent>(_ => SetSelectedIndex(index));
            button.RegisterCallback<FocusInEvent>(_ => SetSelectedIndex(index));

            _buttonHandlers.Add(handler);
            _clickedWrappers.Add(wrapper);
        }

        if (_menuButtons.Count == 0)
        {
            Debug.LogWarning("CampMenuController did not find any menu-button elements.");
        }
        else
        {
            SetSelectedIndex(0);
        }
    }

    private void UnregisterCallbacks()
    {
        for (int i = 0; i < _menuButtons.Count; i++)
        {
            if (_menuButtons[i] != null && i < _clickedWrappers.Count && _clickedWrappers[i] != null)
            {
                _menuButtons[i].clicked -= _clickedWrappers[i];
            }
        }
    }

    private void HookAction(InputActionReference actionRef, Action<InputAction.CallbackContext> handler, bool enable)
    {
        if (actionRef == null) return;
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

    private void OnNavigateUp(InputAction.CallbackContext context)
    {
        if (!CanProcessInput()) return;
        if (questPanel != null && questPanel.IsVisible)
        {
            questPanel.NavigateUp();
            _lastInputTime = Time.unscaledTime;
            return;
        }
        if (statsPanel != null && statsPanel.IsVisible)
        {
            _lastInputTime = Time.unscaledTime;
            return;
        }
        Navigate(-1);
    }

    private void OnNavigateDown(InputAction.CallbackContext context)
    {
        if (!CanProcessInput()) return;
        if (questPanel != null && questPanel.IsVisible)
        {
            questPanel.NavigateDown();
            _lastInputTime = Time.unscaledTime;
            return;
        }
        if (statsPanel != null && statsPanel.IsVisible)
        {
            _lastInputTime = Time.unscaledTime;
            return;
        }
        Navigate(1);
    }

    private void OnConfirm(InputAction.CallbackContext context)
    {
        if (!CanProcessInput()) return;
        if (questPanel != null && questPanel.IsVisible)
        {
            questPanel.Confirm();
            _lastInputTime = Time.unscaledTime;
            return;
        }
        if (statsPanel != null && statsPanel.IsVisible)
        {
            statsPanel.Confirm();
            _lastInputTime = Time.unscaledTime;
            return;
        }
        ActivateCurrentButton();
        _lastInputTime = Time.unscaledTime;
    }

    private void OnCancel(InputAction.CallbackContext context)
    {
        if (!CanProcessInput()) return;
        HandleCancel();
        _lastInputTime = Time.unscaledTime;
    }

    private void HandleFallbackInput()
    {
        if (!CanProcessInput()) return;

        bool usedInput = false;
        bool questPanelOpen = questPanel != null && questPanel.IsVisible;
        bool statsPanelOpen = statsPanel != null && statsPanel.IsVisible;

        if (navigateUpAction == null && navigateDownAction == null)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                if (questPanelOpen) questPanel.NavigateUp();
                else if (!statsPanelOpen) Navigate(-1);
                usedInput = true;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                if (questPanelOpen) questPanel.NavigateDown();
                else if (!statsPanelOpen) Navigate(1);
                usedInput = true;
            }
        }

        if (!usedInput && confirmAction == null && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Submit")))
        {
            if (questPanelOpen) questPanel.Confirm();
            else if (statsPanelOpen) statsPanel.Confirm();
            else ActivateCurrentButton();
            usedInput = true;
        }

        if (!usedInput && cancelAction == null && (Input.GetKeyDown(KeyCode.Escape) || Input.GetButtonDown("Cancel")))
        {
            HandleCancel();
            usedInput = true;
        }

        if (usedInput)
        {
            _lastInputTime = Time.unscaledTime;
        }
    }

    private void Navigate(int direction)
    {
        if (_menuButtons.Count == 0) return;

        _currentIndex = (_currentIndex + direction + _menuButtons.Count) % _menuButtons.Count;
        SetSelectedIndex(_currentIndex);
        _lastInputTime = Time.unscaledTime;
    }

    private void SetSelectedIndex(int index)
    {
        if (_menuButtons.Count == 0) return;

        _currentIndex = Mathf.Clamp(index, 0, _menuButtons.Count - 1);

        // Moving the selection away from an armed reset disarms it
        if (_resetArmed && _resetButton != null && _menuButtons[_currentIndex] != _resetButton)
        {
            DisarmReset();
        }

        for (int i = 0; i < _menuButtons.Count; i++)
        {
            if (_menuButtons[i] == null) continue;
            if (i == _currentIndex)
            {
                _menuButtons[i].AddToClassList(SELECTED_CLASS);
                _menuButtons[i].Focus();
            }
            else
            {
                _menuButtons[i].RemoveFromClassList(SELECTED_CLASS);
            }
        }
    }

    private void ActivateCurrentButton()
    {
        if (_menuButtons.Count == 0) return;
        var handler = _buttonHandlers[_currentIndex];
        handler?.Invoke();
    }

    private bool CanProcessInput()
    {
        return Time.unscaledTime - _lastInputTime >= inputCooldown;
    }

    private void HandleReturnClicked()
    {
        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.LoadGameScene();
        }
        else
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameplaySceneName);
        }
    }

    private void HandleQuestsClicked()
    {
        if (questPanel == null)
        {
            Debug.LogWarning("CampMenuController: Quest panel reference is not set.");
            return;
        }
        SetMenuContainerVisible(false);
        questPanel.Show();
    }

    private void HandleStatsClicked()
    {
        if (statsPanel == null)
        {
            Debug.LogWarning("CampMenuController: Stats panel reference is not set.");
            return;
        }
        SetMenuContainerVisible(false);
        statsPanel.Show();
    }

    private void HandleSubPanelClosed()
    {
        SetMenuContainerVisible(true);
        SetSelectedIndex(_currentIndex);
    }

    private void SetMenuContainerVisible(bool visible)
    {
        if (_menuContainer == null) return;
        _menuContainer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // Two-step wipe: first press arms the button, second press deletes the save
    // (gold, honor, rank, quests, stats, map/boss progress) and reloads the camp
    // so every manager reinitializes from a fresh SaveData.
    private void HandleResetClicked()
    {
        if (!_resetArmed)
        {
            _resetArmed = true;
            if (_resetButton != null)
            {
                _resetButton.text = RESET_CONFIRM_LABEL;
                _resetButton.AddToClassList(ARMED_CLASS);
            }
            return;
        }

        SaveManager.DeleteSave();
        Debug.Log("[CampMenu] Save data wiped. Reloading camp.");
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void DisarmReset()
    {
        _resetArmed = false;
        if (_resetButton != null)
        {
            _resetButton.text = RESET_LABEL;
            _resetButton.RemoveFromClassList(ARMED_CLASS);
        }
    }

    private void HandleExitClicked()
    {
        // Placeholder behavior: quit application. In editor, just log.
        Debug.Log("Exit selected from Camp menu.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void HandleCancel()
    {
        if (_resetArmed)
        {
            DisarmReset();
            return;
        }

        if (questPanel != null && questPanel.IsVisible)
        {
            questPanel.Hide();
            HandleSubPanelClosed();
            return;
        }
        if (statsPanel != null && statsPanel.IsVisible)
        {
            statsPanel.Hide();
            HandleSubPanelClosed();
            return;
        }

        // Cancel must never ACTIVATE anything from the top menu: gamepads with a
        // held or noisy button on the legacy "Cancel" mapping were instantly
        // launching the game (Return is the default selection) or quitting via
        // the old fall-through. Deliberate cancels just move the highlight to
        // Exit; actually leaving takes an explicit confirm on that button.
        for (int i = 0; i < _menuButtons.Count; i++)
        {
            if (_menuButtons[i] != null && _menuButtons[i].name == exitButtonName)
            {
                SetSelectedIndex(i);
                break;
            }
        }
    }
}
