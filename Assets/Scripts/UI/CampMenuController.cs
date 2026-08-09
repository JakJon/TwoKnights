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
    [SerializeField] private string equipmentButtonName = "equipment-button";
    [SerializeField] private string shopButtonName = "shop-button";
    [SerializeField] private string statsButtonName = "stats-button";
    [SerializeField] private string resetButtonName = "reset-button";
    [SerializeField] private string exitButtonName = "exit-button";

    [Header("Scene Names")]
    [SerializeField] private string gameplaySceneName = "Main";

    [Header("Sub Panels")]
    [SerializeField] private QuestPanel questPanel;
    [SerializeField] private StatsPanel statsPanel;
    [SerializeField] private MapSelectPanel mapSelectPanel;
    [SerializeField] private EquipmentPanel equipmentPanel;
    [SerializeField] private ShopPanel shopPanel;
    [SerializeField] private FileSelectPanel fileSelectPanel;
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
    private Label _goldLine;
    // A count+icon row, not a Label — see CrystalText
    private VisualElement _crystalLine;
    private Label _waveLine;
    private Label _questsLine;
    // Every notification dot in the camp: dot + halo, pulsed together
    private readonly List<(VisualElement dot, VisualElement glow)> _badges = new();
    private VisualElement _questsBadge;
    private VisualElement _questsBadgeGlow;
    private VisualElement _shopBadge;
    private VisualElement _shopBadgeGlow;
    private VisualElement _equipmentBadge;
    private VisualElement _equipmentBadgeGlow;
    private Button _shopButton;
    private IVisualElementScheduledItem _badgePulse;
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
    // Present in the UXML for all builds; only dev builds ever reveal it
    private const string TEST_BUTTON_NAME = "test-button";
    // Combo entered once = the hidden buttons stay revealed for the whole session
    private static bool _hiddenButtonsUnlocked;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private TestModePanel testModePanel;
#endif

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
        if (questPanel == null) questPanel = GetComponent<QuestPanel>();
        if (statsPanel == null) statsPanel = GetComponent<StatsPanel>();
        if (mapSelectPanel == null) mapSelectPanel = GetComponent<MapSelectPanel>();
        if (mapSelectPanel == null) mapSelectPanel = gameObject.AddComponent<MapSelectPanel>();
        // Added in code, like MapSelectPanel, so the camp scene needs no rewiring
        if (equipmentPanel == null) equipmentPanel = GetComponent<EquipmentPanel>();
        if (equipmentPanel == null) equipmentPanel = gameObject.AddComponent<EquipmentPanel>();
        if (shopPanel == null) shopPanel = GetComponent<ShopPanel>();
        if (shopPanel == null) shopPanel = gameObject.AddComponent<ShopPanel>();
        if (fileSelectPanel == null) fileSelectPanel = GetComponent<FileSelectPanel>();
        if (fileSelectPanel == null) fileSelectPanel = gameObject.AddComponent<FileSelectPanel>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Added in code so the scene needs no extra wiring
        testModePanel = GetComponent<TestModePanel>();
        if (testModePanel == null) testModePanel = gameObject.AddComponent<TestModePanel>();
#endif
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
        if (mapSelectPanel != null)
        {
            mapSelectPanel.OnCloseRequested += HandleSubPanelClosed;
            mapSelectPanel.OnMapChosen += HandleMapChosen;
        }
        if (equipmentPanel != null) equipmentPanel.OnCloseRequested += HandleSubPanelClosed;
        if (shopPanel != null) shopPanel.OnCloseRequested += HandleSubPanelClosed;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (testModePanel != null) testModePanel.OnCloseRequested += HandleSubPanelClosed;
#endif
        GoldManager.OnGoldChanged += HandleGoldChanged;
        CrystalBank.OnCrystalsChanged += HandleCrystalsChanged;
        QuestProgress.OnQuestCompleted += HandleQuestCompleted;
        QuestProgress.OnQuestUnlocked += HandleQuestCompleted;
        if (fileSelectPanel != null) fileSelectPanel.OnCloseRequested += HandleSubPanelClosed;
        RefreshStatusLines();
        ShowFileSelectIfNeeded();
    }

    // The first camp of a session opens on the file select instead of the menu.
    // Returning here from a run does not — the file is already chosen by then.
    private void ShowFileSelectIfNeeded()
    {
        if (fileSelectPanel == null || !FileSelectPanel.ShouldShowAtBoot) return;
        SetMenuContainerVisible(false);
        fileSelectPanel.Show();
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
        if (mapSelectPanel != null)
        {
            mapSelectPanel.OnCloseRequested -= HandleSubPanelClosed;
            mapSelectPanel.OnMapChosen -= HandleMapChosen;
        }
        if (equipmentPanel != null) equipmentPanel.OnCloseRequested -= HandleSubPanelClosed;
        if (shopPanel != null) shopPanel.OnCloseRequested -= HandleSubPanelClosed;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (testModePanel != null) testModePanel.OnCloseRequested -= HandleSubPanelClosed;
#endif
        GoldManager.OnGoldChanged -= HandleGoldChanged;
        CrystalBank.OnCrystalsChanged -= HandleCrystalsChanged;
        QuestProgress.OnQuestCompleted -= HandleQuestCompleted;
        QuestProgress.OnQuestUnlocked -= HandleQuestCompleted;
        if (fileSelectPanel != null) fileSelectPanel.OnCloseRequested -= HandleSubPanelClosed;
    }

    private void Update()
    {
        HandleFallbackInput();
        CheckHiddenButtonCombo();
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

        // The camp scene has no EventSystem, so UI Toolkit drives its own
        // navigation off the focused element, stacked on top of our
        // action-driven menu. Move did it first: every press moved the
        // highlight TWO items. Submit/Cancel are the same leak and worse —
        // they fire `clicked` on whatever Button holds focus, which with a
        // sub-panel open (menu container hidden, focus stale behind it) meant
        // one A press both worked the panel AND activated a camp button or the
        // panel's own close X, bouncing the player out of Test Mode / level
        // select. All camp navigation is driven explicitly (menu, quest, stats,
        // map, test panels) and confirm/cancel come from the Menu action map,
        // so swallow the runtime navigation events entirely.
        _root.RegisterCallback<NavigationMoveEvent>(SwallowNavigation, TrickleDown.TrickleDown);
        _root.RegisterCallback<NavigationSubmitEvent>(SwallowNavigation, TrickleDown.TrickleDown);
        _root.RegisterCallback<NavigationCancelEvent>(SwallowNavigation, TrickleDown.TrickleDown);

        _menuContainer = _root.Q<VisualElement>(menuContainerName);
        _goldLine = _root.Q<Label>("gold-line");
        _crystalLine = _root.Q<VisualElement>("crystal-line");
        _waveLine = _root.Q<Label>("wave-line");
        _questsLine = _root.Q<Label>("quests-line");
        _questsBadge = _root.Q<VisualElement>("quests-badge");
        _questsBadgeGlow = _root.Q<VisualElement>("quests-badge-glow");
        _shopBadge = _root.Q<VisualElement>("shop-badge");
        _shopBadgeGlow = _root.Q<VisualElement>("shop-badge-glow");
        _equipmentBadge = _root.Q<VisualElement>("equipment-badge");
        _equipmentBadgeGlow = _root.Q<VisualElement>("equipment-badge-glow");
        _badges.Clear();
        _badges.Add((_questsBadge, _questsBadgeGlow));
        _badges.Add((_shopBadge, _shopBadgeGlow));
        _badges.Add((_equipmentBadge, _equipmentBadgeGlow));
        StartBadgePulse();
        _menuButtons.Clear();
        _buttonHandlers.Clear();
        _clickedWrappers.Clear();
        _resetButton = null;
        _resetArmed = false;

        foreach (var button in _root.Query<Button>(className: "menu-button").ToList())
        {
            // Test Mode button: hidden and unnavigable until the combo unlocks
            // it; release builds never register it at all
            if (button.name == TEST_BUTTON_NAME)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (_hiddenButtonsUnlocked)
                {
                    button.style.display = DisplayStyle.Flex;
                    RegisterMenuButton(button, HandleTestModeClicked);
                }
#endif
                continue;
            }

            // Reset All Data gets the same treatment: a single confirm away
            // from wiping the save is too much to leave sitting in the menu, so
            // it only exists once the combo has been entered.
            if (button.name == resetButtonName)
            {
                if (_hiddenButtonsUnlocked)
                {
                    button.style.display = DisplayStyle.Flex;
                    _resetButton = button;
                    RegisterMenuButton(button, HandleResetClicked);
                }
                continue;
            }

            // Stats rides the same combo: a debug-flavoured readout that
            // shouldn't clutter the main camp list.
            if (button.name == statsButtonName)
            {
                if (_hiddenButtonsUnlocked)
                {
                    button.style.display = DisplayStyle.Flex;
                    RegisterMenuButton(button, HandleStatsClicked);
                }
                continue;
            }

            Action handler = null;

            switch (button.name)
            {
                case var name when name == returnButtonName:
                    handler = HandleReturnClicked;
                    break;
                case var name when name == questsButtonName:
                    handler = HandleQuestsClicked;
                    break;
                case var name when name == equipmentButtonName:
                    handler = HandleEquipmentClicked;
                    break;
                case var name when name == shopButtonName:
                    handler = HandleShopClicked;
                    _shopButton = button;
                    break;
                case var name when name == exitButtonName:
                    handler = HandleExitClicked;
                    break;
            }

            RegisterMenuButton(button, handler);
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

    // IgnoreEvent as well as StopPropagation: stopping propagation alone still
    // lets the focus controller run its default action for the event.
    private void SwallowNavigation(EventBase evt)
    {
        evt.StopPropagation();
        _root?.focusController?.IgnoreEvent(evt);
    }

    private void RegisterMenuButton(Button button, Action handler)
    {
        _menuButtons.Add(button);

        // clicked fires from pointer clicks AND from UI Toolkit navigation
        // submit on the focused button — the latter bypasses our input
        // cooldown entirely, so a first-frame phantom Submit (gamepad axis
        // sampled right after a scene load) could instantly activate the
        // focused Return button and bounce camp back into the game. Gate
        // every click through the same cooldown/grace window, and never let
        // one through while a sub-panel owns input: the camp stops touching
        // _lastInputTime then, so the cooldown alone would always be expired.
        Action wrapper = null;
        if (handler != null)
        {
            var captured = handler;
            wrapper = () =>
            {
                if (!CanProcessInput()) return;
                if (PanelPollsOwnInput()) return;
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
        if (PanelPollsOwnInput()) return;
        if (statsPanel != null && statsPanel.IsVisible)
        {
            statsPanel.ScrollUp();
            _lastInputTime = Time.unscaledTime;
            return;
        }
        Navigate(-1);
    }

    private void OnNavigateDown(InputAction.CallbackContext context)
    {
        if (!CanProcessInput()) return;
        if (PanelPollsOwnInput()) return;
        if (statsPanel != null && statsPanel.IsVisible)
        {
            statsPanel.ScrollDown();
            _lastInputTime = Time.unscaledTime;
            return;
        }
        Navigate(1);
    }

    private void OnConfirm(InputAction.CallbackContext context)
    {
        if (!CanProcessInput()) return;
        if (PanelPollsOwnInput()) return;
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
        if (PanelPollsOwnInput()) return;
        HandleCancel();
        _lastInputTime = Time.unscaledTime;
    }

    // Level select and Test Mode poll their own gamepad/keyboard input (both
    // need left/right, which the camp actions don't provide), so the camp menu
    // must go quiet while either is open or both would react to the same press
    private bool PanelPollsOwnInput()
    {
        // The file select is up before anything else and owns up/down/confirm
        if (fileSelectPanel != null && fileSelectPanel.IsVisible) return true;
        if (mapSelectPanel != null && mapSelectPanel.IsVisible) return true;
        // Every panel that needs left/right owns its own input: equipment to
        // switch knights, the quest log to step through reward squares, the shop
        // to change tabs. Only Stats still rides the camp's action set.
        if (equipmentPanel != null && equipmentPanel.IsVisible) return true;
        if (questPanel != null && questPanel.IsVisible) return true;
        if (shopPanel != null && shopPanel.IsVisible) return true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return testModePanel != null && testModePanel.IsVisible;
#else
        return false;
#endif
    }

    private void HandleFallbackInput()
    {
        if (!CanProcessInput()) return;
        if (PanelPollsOwnInput()) return;

        bool usedInput = false;
        bool statsPanelOpen = statsPanel != null && statsPanel.IsVisible;

        if (navigateUpAction == null && navigateDownAction == null)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                if (statsPanelOpen) statsPanel.ScrollUp();
                else Navigate(-1);
                usedInput = true;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                if (statsPanelOpen) statsPanel.ScrollDown();
                else Navigate(1);
                usedInput = true;
            }
        }

        if (!usedInput && confirmAction == null && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Submit")))
        {
            if (statsPanelOpen) statsPanel.Confirm();
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

    // A button the player cannot see must not be steppable, or the highlight
    // vanishes on a hidden row and Confirm opens something that isn't there. The
    // Shop is hidden before the first crystal, so this is a live case, not theory.
    private bool IsNavigable(int index)
    {
        if (index < 0 || index >= _menuButtons.Count) return false;
        var button = _menuButtons[index];
        if (button == null) return false;
        // Inline display is what this controller and the UXML actually set, and it
        // is true the instant it is assigned — resolvedStyle only catches up at the
        // next style resolution, which is too late when visibility changed this frame.
        var inline = button.style.display;
        if (inline.keyword == StyleKeyword.Null || inline.keyword == StyleKeyword.Undefined)
        {
            return button.resolvedStyle.display != DisplayStyle.None;
        }
        return inline.value != DisplayStyle.None;
    }

    private void Navigate(int direction)
    {
        if (_menuButtons.Count == 0) return;

        int next = _currentIndex;
        for (int step = 0; step < _menuButtons.Count; step++)
        {
            next = (next + direction + _menuButtons.Count) % _menuButtons.Count;
            if (IsNavigable(next)) break;
        }
        if (!IsNavigable(next)) return;   // nothing visible to move to

        _currentIndex = next;
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiMove);
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
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.uiConfirm);
        var handler = _buttonHandlers[_currentIndex];
        handler?.Invoke();
    }

    private bool CanProcessInput()
    {
        return Time.unscaledTime - _lastInputTime >= inputCooldown;
    }

    // Side status panels replace the old TMP canvas HUD: same managers, same
    // fallbacks to SaveData when a manager singleton isn't alive yet.
    private void RefreshStatusLines()
    {
        HandleGoldChanged(GoldManager.Instance != null ? GoldManager.Instance.Gold : SaveManager.Data.gold);
        HandleCrystalsChanged(CrystalBank.Balance);

        if (_waveLine != null)
        {
            _waveLine.text = $"Furthest Wave: {SaveManager.Data.furthestWave}";
        }

        if (_questsLine != null)
        {
            // Counts only what the player can SEE. The full total would leak how
            // much undiscovered content is behind the Order gates.
            var visible = QuestProgress.Visible.ToList();
            int completed = visible.Count(q => QuestProgress.IsCompleted(q.Id));
            _questsLine.text = $"Quests: {completed} / {visible.Count}";
        }

        RefreshShopVisibility();
        RefreshBadges();
    }

    // The shop is hidden outright until the first crystal is earned — a price list
    // is noise to someone with no way to pay. Re-checked on every status refresh
    // because a quest turned in from the log can pay out while the camp is open.
    private void RefreshShopVisibility()
    {
        if (_shopButton == null) return;

        // Only ever hidden -> shown: the unlock is latched, so the highlight can
        // never be sitting on the shop at the moment it disappears.
        _shopButton.style.display = CampNotices.ShopUnlocked
            ? DisplayStyle.Flex
            : DisplayStyle.None;
    }

    // Each dot stays lit until the screen behind it has actually been opened, so a
    // batch of new things can't be dismissed by glancing at one of them.
    private void RefreshBadges()
    {
        SetBadge(_questsBadge, _questsBadgeGlow, QuestProgress.HasUnseen);
        SetBadge(_shopBadge, _shopBadgeGlow, CampNotices.ShopHasNotice);
        SetBadge(_equipmentBadge, _equipmentBadgeGlow, CampNotices.EquipmentHasNotice);
    }

    private static void SetBadge(VisualElement dot, VisualElement glow, bool lit)
    {
        var display = lit ? DisplayStyle.Flex : DisplayStyle.None;
        if (dot != null) dot.style.display = display;
        if (glow != null) glow.style.display = display;
    }

    // UI Toolkit has no keyframe animations and transitions do not loop, so the
    // pulse is driven on a scheduler. Unscaled time, because the camp sets
    // timeScale to 0 whenever a run ends in a menu.
    private void StartBadgePulse()
    {
        if (_root == null) return;
        _badgePulse?.Pause();
        _badgePulse = _root.schedule.Execute(() =>
        {
            // Two beats out of phase: the dot brightens while the halo swells,
            // which reads as light coming off it rather than a blinking pixel.
            // One clock for every dot, so they breathe together instead of
            // drifting into a mess of independent blinks.
            float t = Time.unscaledTime * 2.2f;
            float wave = (Mathf.Sin(t) + 1f) * 0.5f;

            for (int i = 0; i < _badges.Count; i++)
            {
                var (dot, glow) = _badges[i];
                if (dot == null || dot.style.display == DisplayStyle.None) continue;

                dot.style.opacity = 0.72f + 0.28f * wave;
                dot.style.scale = new StyleScale(new Scale(Vector3.one * (0.9f + 0.18f * wave)));

                if (glow != null)
                {
                    glow.style.opacity = 0.5f - 0.34f * wave;
                    glow.style.scale = new StyleScale(new Scale(Vector3.one * (0.75f + 0.55f * wave)));
                }
            }
        }).Every(33);
    }

    private void HandleGoldChanged(int gold)
    {
        if (_goldLine != null) _goldLine.text = $"Gold: {gold}";
    }

    private void HandleCrystalsChanged(int crystals)
    {
        CrystalText.Fill(_crystalLine, crystals);
        // The first crystal is what reveals the shop, so react here too rather than
        // waiting for the next full status refresh
        RefreshShopVisibility();
        RefreshBadges();
    }

    private void HandleQuestCompleted(string questId)
    {
        RefreshStatusLines();
    }

    // Start no longer drops straight into a run — it opens the level select,
    // which decides which map the run plays
    private void HandleReturnClicked()
    {
        if (mapSelectPanel == null)
        {
            Debug.LogWarning("CampMenuController: no MapSelectPanel; starting the run directly.");
            StartRun();
            return;
        }

        SetMenuContainerVisible(false);
        mapSelectPanel.Show();
    }

    private void HandleMapChosen(MapDefinition map)
    {
        StartRun();
    }

    private void StartRun()
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

    private void HandleEquipmentClicked()
    {
        if (equipmentPanel == null) return;
        // Opening the screen is what counts as having seen the new gear — the dot
        // is cleared here, not when the item was granted
        CampNotices.MarkEquipmentSeen();
        RefreshBadges();
        SetMenuContainerVisible(false);
        equipmentPanel.Show();
    }

    private void HandleShopClicked()
    {
        if (shopPanel == null) return;
        CampNotices.MarkShopSeen();
        RefreshBadges();
        SetMenuContainerVisible(false);
        shopPanel.Show();
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
        // Reading the quest log is what marks quests seen, so the dot can only
        // be re-evaluated once the player leaves it
        RefreshStatusLines();
    }

    // Holding LT+RT+LB+RB together on the camp menu (or pressing F9 on keyboard,
    // for gamepad-free editor sessions) reveals the hidden buttons for the rest
    // of the session: Stats and Reset All Data in every build, plus Test Mode
    // in the editor and dev builds.
    private void CheckHiddenButtonCombo()
    {
        if (_hiddenButtonsUnlocked) return;
        if ((questPanel != null && questPanel.IsVisible) ||
            (statsPanel != null && statsPanel.IsVisible) ||
            (fileSelectPanel != null && fileSelectPanel.IsVisible)) return;

        var gamepad = Gamepad.current;
        bool combo = gamepad != null
            && gamepad.leftTrigger.isPressed && gamepad.rightTrigger.isPressed
            && gamepad.leftShoulder.isPressed && gamepad.rightShoulder.isPressed;

        var keyboard = Keyboard.current;
        bool devKey = keyboard != null && keyboard.f9Key.wasPressedThisFrame;

        if (!combo && !devKey) return;

        _hiddenButtonsUnlocked = true;
        RevealHiddenButtons();
        Debug.Log("[CampMenu] Hidden buttons unlocked.");
    }

    // Revealed mid-session the buttons append to the end of the navigation
    // order rather than sitting where the UXML puts them — acceptable for
    // buttons that all belong at the bottom of the list anyway.
    private void RevealHiddenButtons()
    {
        var stats = _root?.Q<Button>(statsButtonName);
        if (stats != null && !_menuButtons.Contains(stats))
        {
            stats.style.display = DisplayStyle.Flex;
            RegisterMenuButton(stats, HandleStatsClicked);
        }

        var reset = _root?.Q<Button>(resetButtonName);
        if (reset != null && !_menuButtons.Contains(reset))
        {
            reset.style.display = DisplayStyle.Flex;
            _resetButton = reset;
            RegisterMenuButton(reset, HandleResetClicked);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var test = _root?.Q<Button>(TEST_BUTTON_NAME);
        if (test != null && !_menuButtons.Contains(test))
        {
            test.style.display = DisplayStyle.Flex;
            RegisterMenuButton(test, HandleTestModeClicked);
        }
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void HandleTestModeClicked()
    {
        if (testModePanel == null) return;
        SetMenuContainerVisible(false);
        testModePanel.Show();
    }
#endif

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
        // The pick is a static and survives the scene reload, so without this a
        // map that the wipe just re-locked would still be the next run's map
        MapSelection.Clear();
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

    // Exit leaves the file, not the game: it drops back to the file select so
    // another save can be picked. Closing the app is the platform's job (alt-F4
    // / the window chrome), not a menu button that is one stray press from
    // ending the session.
    private void HandleExitClicked()
    {
        if (fileSelectPanel == null) return;
        SetMenuContainerVisible(false);
        fileSelectPanel.Show(allowCancel: true);
    }

    private void HandleCancel()
    {
        if (_resetArmed)
        {
            DisarmReset();
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
