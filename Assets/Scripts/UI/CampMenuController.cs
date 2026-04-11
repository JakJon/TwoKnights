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
    [SerializeField] private string exitButtonName = "exit-button";

    [Header("Scene Names")]
    [SerializeField] private string gameplaySceneName = "Main";

    [Header("Input Actions")]
    [SerializeField] private InputActionReference navigateUpAction;
    [SerializeField] private InputActionReference navigateDownAction;
    [SerializeField] private InputActionReference confirmAction;
    [SerializeField] private InputActionReference cancelAction;

    [Header("Input Settings")]
    [SerializeField] private float inputCooldown = 0.2f;

    private UIDocument _uiDocument;
    private VisualElement _root;
    private readonly List<Button> _menuButtons = new();
    private readonly List<Action> _buttonHandlers = new();
    private int _currentIndex;
    private float _lastInputTime;

    private const string SELECTED_CLASS = "menu-button--selected";

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        RegisterCallbacks();
        HookAction(navigateUpAction, OnNavigateUp, true);
        HookAction(navigateDownAction, OnNavigateDown, true);
        HookAction(confirmAction, OnConfirm, true);
        HookAction(cancelAction, OnCancel, true);
        SetSelectedIndex(Mathf.Clamp(_currentIndex, 0, _menuButtons.Count - 1));
    }

    private void OnDisable()
    {
        HookAction(navigateUpAction, OnNavigateUp, false);
        HookAction(navigateDownAction, OnNavigateDown, false);
        HookAction(confirmAction, OnConfirm, false);
        HookAction(cancelAction, OnCancel, false);
        UnregisterCallbacks();
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

        _menuButtons.Clear();
        _buttonHandlers.Clear();

        foreach (var button in _root.Query<Button>(className: "menu-button").ToList())
        {
            _menuButtons.Add(button);
            Action handler = null;

            switch (button.name)
            {
                case var name when name == returnButtonName:
                    handler = HandleReturnClicked;
                    break;
                case var name when name == exitButtonName:
                    handler = HandleExitClicked;
                    break;
            }

            if (handler != null)
            {
                button.clicked += handler;
            }

            var index = _menuButtons.Count - 1;
            button.RegisterCallback<MouseEnterEvent>(_ => SetSelectedIndex(index));
            button.RegisterCallback<FocusInEvent>(_ => SetSelectedIndex(index));

            _buttonHandlers.Add(handler);
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
            if (_menuButtons[i] != null && _buttonHandlers[i] != null)
            {
                _menuButtons[i].clicked -= _buttonHandlers[i];
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
        Navigate(-1);
    }

    private void OnNavigateDown(InputAction.CallbackContext context)
    {
        if (!CanProcessInput()) return;
        Navigate(1);
    }

    private void OnConfirm(InputAction.CallbackContext context)
    {
        if (!CanProcessInput()) return;
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

        if (navigateUpAction == null && navigateDownAction == null)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                Navigate(-1);
                usedInput = true;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                Navigate(1);
                usedInput = true;
            }
        }

        if (!usedInput && confirmAction == null && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Submit")))
        {
            ActivateCurrentButton();
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
        // If a specific button is selected, prefer activating that button's handler instead of exiting outright.
        if (_menuButtons.Count > 0 && _currentIndex < _menuButtons.Count)
        {
            var selectedButton = _menuButtons[_currentIndex];
            if (selectedButton != null && selectedButton.name == returnButtonName)
            {
                // Cancel while Return is highlighted should also return to battle.
                HandleReturnClicked();
                return;
            }
        }

        HandleExitClicked();
    }
}
