using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System;
using UnityEngine.Serialization;

public class UpgradeMenu : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private StyleSheet styleSheet;
    
    [Header("Upgrade System")]
    [SerializeField] private UpgradeManager upgradeManager;

    [Header("Knight Portraits")]
    // Shown above each knight's name, with that knight's Order aura drifting off them.
    // Wired by Assets/Editor/UpgradeMenuWiring.cs so they survive a fresh scene.
    [SerializeField] private Sprite leftKnightPortrait;
    [SerializeField] private Sprite rightKnightPortrait;
    
    [Header("Waves")]
    [SerializeField] private WaveManager waveManager;
    
    [Header("Navigation Input Actions (D-Pad / Stick)")]
    [FormerlySerializedAs("dpadLeftAction")]
    [SerializeField] private InputActionReference navLeftAction;
    [FormerlySerializedAs("dpadRightAction")]
    [SerializeField] private InputActionReference navRightAction;
    [FormerlySerializedAs("dpadUpAction")]
    [SerializeField] private InputActionReference navUpAction;
    [FormerlySerializedAs("dpadDownAction")]
    [SerializeField] private InputActionReference navDownAction;
    
    [Header("Confirm Input Action")]
    [SerializeField] private InputActionReference confirmAction;
    
    [Header("Input Settings")]
    [SerializeField] private float inputCooldown = 0.2f;
    
    private VisualElement root;
    private List<VisualElement> menuItems = new List<VisualElement>();
    private Button confirmButton;
    private Label selectionStatusLabel;
    private Label waveNumberLabel;
    private List<BaseUpgrade> currentUpgrades;
    private int currentSelectedIndex = 0;
    private bool isConfirmButtonSelected = false;
    private float lastInputTime = 0f;
    private KnightTarget selectedKnight = KnightTarget.LeftKnight; // Target knight for this upgrade instance
    private int chosenUpgradeIndex = -1;
    private KnightTarget chosenKnight = KnightTarget.LeftKnight;
    
    private const string SELECTED_CLASS = "menu-item--selected";
    private const string CHOSEN_CLASS = "menu-item--chosen";
    private const string PUNCH_CLASS = "menu-item--punch";
    private const string CONFIRM_FLASH_CLASS = "menu-item--confirm-flash";
    private const string CARD_ENTER_CLASS = "card-enter";
    private const string CARD_SLIDING_CLASS = "card-sliding";
    private const string CONFIRM_SLIDING_CLASS = "confirm-sliding";

    // Entrance timing. A card starts every STAGGER and takes SLIDE to arrive, so the
    // three of them fill about a second and a half; the confirm button follows on a
    // shorter slide.
    //
    // SLIDE matches STAGGER, so each card starts travelling as the one before it lands
    // and every card gets the full half second it is meant to take. The whole entrance
    // is silent — the menu plays nothing until the pick is confirmed — so these
    // numbers answer to the eye alone now.
    //
    // The lead-in is not padding: the parked style has to be resolved and drawn for one
    // frame before the class comes off, or the first card jumps into place instead of
    // sliding.
    private const int ENTRANCE_LEAD_IN_MS = 40;
    private const int ENTRANCE_STAGGER_MS = 500;
    private const int ENTRANCE_SLIDE_MS = 500;
    private const int CONFIRM_SLIDE_MS = 300;

    // Blocks input while the confirm animation plays before the event fires
    private bool _confirmPending = false;

    // Status panel UI refs
    private Label leftHealthLabel;
    private Label leftSpecialLabel;
    private Label rightHealthLabel;
    private Label rightSpecialLabel;

    // ---------- owned-loadout columns ----------
    // The same rows-plus-detail readout the pause menu uses, so the two screens
    // answer "what have I already got?" identically. Left and right are real
    // navigation destinations here: left/right walks column to column and
    // up/down walks within whichever one the cursor is in.
    private enum Column { Left = 0, Center = 1, Right = 2 }
    private Column _column = Column.Center;

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

    private readonly LoadoutColumn _leftColumn = new LoadoutColumn();
    private readonly LoadoutColumn _rightColumn = new LoadoutColumn();

    private const string ROW_SELECTED_CLASS = "loadout-chip--selected";

    private static readonly string[] AllOrderClasses =
    {
        "order--serpent", "order--shadow", "order--ember", "order--guardian", "order--dawn",
        "order--frigid"
    };

    // Cached status data (applied when UI is ready)
    private float _leftCurHP, _leftMaxHP, _leftCurSP, _leftMaxSP;
    private List<string> _leftUpgradesCache = new List<string>();
    private float _rightCurHP, _rightMaxHP, _rightCurSP, _rightMaxSP;
    private List<string> _rightUpgradesCache = new List<string>();
    
    // Optional provider to fetch applied upgrade names per knight (inject from game systems)
    public Func<KnightTarget, IEnumerable<string>> UpgradeNamesProvider;

    // Cached player component refs (avoids repeated FindWithTag calls)
    private GameObject _leftPlayerGO, _rightPlayerGO;
    private PlayerHealth _leftHealth, _rightHealth;
    private PlayerSpecial _leftSpecial, _rightSpecial;
    
    // Event for when an upgrade is confirmed
    public event System.Action<int, KnightTarget> OnUpgradeConfirmed;

    // Order aura: portrait art + the layer its motes drift through, per knight.
    private VisualElement _leftPortraitArt, _rightPortraitArt;
    private OrderAura _leftAura, _rightAura;
    private IVisualElementScheduledItem _auraTicker;
    
    void Awake()
    {
        // Get UI Document component if not assigned
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        // Default provider for upgrade names from UpgradeManager if available
        if (upgradeManager != null)
        {
            UpgradeNamesProvider = (knight) => upgradeManager.GetAppliedUpgradeNames(knight);
        }
    }
    
    void Start()
    {
        SetupUI();

        // Initially hide the menu
        SetMenuVisible(false);
    }

    // uiDocument.rootVisualElement can still be null during Start (e.g. right after the
    // UXML/USS reimport), which used to leave the menu permanently uninitialized and the
    // raw template visible. Retry setup lazily until the panel exists.
    private bool EnsureUI()
    {
        if (root != null) return true;
        SetupUI();
        return root != null;
    }
    
    private void OnEnable()
    {
        // Enable actions
    HookAction(navLeftAction, OnNavigateLeft, true);
    HookAction(navRightAction, OnNavigateRight, true);
    HookAction(navUpAction, OnNavigateUp, true);
    HookAction(navDownAction, OnNavigateDown, true);
        HookAction(confirmAction, OnConfirmSelect, true);
    }
    
    private void OnDisable()
    {
        // Disable actions
    HookAction(navLeftAction, OnNavigateLeft, false);
    HookAction(navRightAction, OnNavigateRight, false);
    HookAction(navUpAction, OnNavigateUp, false);
    HookAction(navDownAction, OnNavigateDown, false);
        HookAction(confirmAction, OnConfirmSelect, false);
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
    
    // Navigation input callbacks (D-Pad / Stick)
    private void OnNavigateLeft(InputAction.CallbackContext context)
    {
        if (IsMenuVisible() && CanProcessInput())
        {
            NavigateLeft();
            lastInputTime = Time.unscaledTime;
        }
    }
    
    private void OnNavigateRight(InputAction.CallbackContext context)
    {
        if (IsMenuVisible() && CanProcessInput())
        {
            NavigateRight();
            lastInputTime = Time.unscaledTime;
        }
    }
    
    private void OnNavigateUp(InputAction.CallbackContext context)
    {
        if (IsMenuVisible() && CanProcessInput())
        {
            NavigateUp();
            lastInputTime = Time.unscaledTime;
        }
    }
    
    private void OnNavigateDown(InputAction.CallbackContext context)
    {
        if (IsMenuVisible() && CanProcessInput())
        {
            NavigateDown();
            lastInputTime = Time.unscaledTime;
        }
    }
    
    private void OnConfirmSelect(InputAction.CallbackContext context)
    {
        if (!IsMenuVisible() || !CanProcessInput()) return;

        // A loadout row is a readout, not a choice. Confirm on one snaps the
        // cursor back to the draft cards rather than doing nothing at all.
        if (_column != Column.Center)
        {
            FocusCenter();
            lastInputTime = Time.unscaledTime;
            return;
        }

        if (isConfirmButtonSelected)
        {
            if (chosenUpgradeIndex >= 0)
            {
                ConfirmUpgrade();
            }
        }
        else
        {
            // Choose the currently highlighted upgrade and move focus to confirm
            SelectUpgrade(currentSelectedIndex);
            isConfirmButtonSelected = true;
            UpdateSelection();
        }
        lastInputTime = Time.unscaledTime;
    }
    
    void SetupUI()
    {
        if (uiDocument == null) return;
        
        root = uiDocument.rootVisualElement;
        
        if (root == null) return;
        
        // Apply stylesheet if assigned
        if (styleSheet != null)
        {
            root.styleSheets.Add(styleSheet);
        }
        
        // Find UI elements
        menuItems = new List<VisualElement>();
        var itemOne = root.Q<VisualElement>("item-one");
        var itemTwo = root.Q<VisualElement>("item-two");
        var itemThree = root.Q<VisualElement>("item-three");

        if (itemOne != null) menuItems.Add(itemOne);
        if (itemTwo != null) menuItems.Add(itemTwo);
        if (itemThree != null) menuItems.Add(itemThree);

        confirmButton = root.Q<Button>("item-four");
        selectionStatusLabel = root.Q<Label>("selection-status");
        waveNumberLabel = root.Q<Label>("wave-number");

    // Status panels
    leftHealthLabel = root.Q<Label>("left-health");
    leftSpecialLabel = root.Q<Label>("left-special");
    rightHealthLabel = root.Q<Label>("right-health");
    rightSpecialLabel = root.Q<Label>("right-special");
    BindColumn(_leftColumn, "left");
    BindColumn(_rightColumn, "right");
    BindPortraits();

        if (menuItems.Count == 0) return;
        
        // Setup click handlers for the upgrade items
        for (int i = 0; i < menuItems.Count; i++)
        {
            int index = i; // Capture for closure
            menuItems[i].RegisterCallback<ClickEvent>(_ => {
                if (PauseMenu.IsPaused || _confirmPending) return;
                _column = Column.Center;
                currentSelectedIndex = index;
                isConfirmButtonSelected = false;
                SelectUpgrade(index);
                UpdateSelection();
            });
        }
        
        // Setup confirm button click handler
        if (confirmButton != null)
        {
            confirmButton.clicked += () => {
                if (PauseMenu.IsPaused || _confirmPending) return;
                _column = Column.Center;
                isConfirmButtonSelected = true;
                UpdateSelection();
                if (chosenUpgradeIndex >= 0)
                {
                    ConfirmUpgrade();
                }
            };
        }
        
        // Set initial selection
        UpdateSelection();
    // Clear status lists initially
    ClearStatusLists();
    // Apply any cached status values to UI
    ApplyStatusToUI();
    }
    
    void Update()
    {
        // Only handle fallback input when menu is visible and no Input Actions are assigned
        if (IsMenuVisible())
        {
            HandleFallbackInput();
        }
    }
    
    void HandleFallbackInput()
    {
    if (PauseMenu.IsPaused || menuItems.Count == 0 || !CanProcessInput()) return;
        
        // Fallback input handling for when InputActionReferences are not assigned
        bool inputProcessedThisFrame = false;
        
    // Handle D-pad/Stick/keyboard horizontal inputs (mapped to vertical navigation in column layout)
    if (!inputProcessedThisFrame && navLeftAction == null && navRightAction == null)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                NavigateLeft();
                lastInputTime = Time.unscaledTime;
                inputProcessedThisFrame = true;
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                NavigateRight();
                lastInputTime = Time.unscaledTime;
                inputProcessedThisFrame = true;
            }
        }
        
    // Handle D-pad/Stick/keyboard vertical navigation (up/down between upgrade items and confirm button)
    if (!inputProcessedThisFrame && navUpAction == null && navDownAction == null)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                NavigateUp();
                lastInputTime = Time.unscaledTime;
                inputProcessedThisFrame = true;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                NavigateDown();
                lastInputTime = Time.unscaledTime;
                inputProcessedThisFrame = true;
            }
        }
        
        // Handle confirmation and selection with legacy input
        if (confirmAction == null && (Input.GetButtonDown("Submit") || Input.GetButtonDown("Fire1")))
        {
            if (PauseMenu.IsPaused) return;
            if (isConfirmButtonSelected)
            {
                if (chosenUpgradeIndex >= 0)
                {
                    ConfirmUpgrade();
                }
            }
            else
            {
                if (IsUpgradeSelected())
                {
                    SelectUpgrade(currentSelectedIndex);
                    isConfirmButtonSelected = true;
                    UpdateSelection();
                }
            }
            lastInputTime = Time.unscaledTime;
        }
    }
    
    private bool CanProcessInput()
    {
        if (PauseMenu.IsPaused)
            return false;
        if (_confirmPending)
            return false;
        return Time.unscaledTime - lastInputTime >= inputCooldown;
    }
    
    private bool IsMenuVisible()
    {
        return root != null && root.style.display == DisplayStyle.Flex;
    }
    
    private bool IsUpgradeSelected()
    {
        return !isConfirmButtonSelected && currentSelectedIndex >= 0 && currentSelectedIndex < menuItems.Count;
    }
    
    // Left and right walk between the three columns - left knight's loadout,
    // the draft cards, right knight's loadout - exactly as the pause menu does.
    // A knight who has drafted nothing has no rows to sit in, so that side is
    // stepped over rather than focused on an empty list.
    void NavigateLeft() => MoveHorizontal(-1);
    void NavigateRight() => MoveHorizontal(1);

    private void MoveHorizontal(int step)
    {
        if (PauseMenu.IsPaused) return;

        int cursor = (int)_column;
        for (int i = 0; i < 3; i++)
        {
            cursor = (cursor + step + 3) % 3;
            if (cursor == (int)Column.Center)
            {
                FocusCenter();
                return;
            }

            var col = cursor == (int)Column.Left ? _leftColumn : _rightColumn;
            if (col.Rows.Count > 0)
            {
                _column = (Column)cursor;
                SelectRow(col, Mathf.Clamp(col.Index, 0, col.Rows.Count - 1));
                UpdateSelection();
                return;
            }
        }
    }

    private void FocusCenter()
    {
        _column = Column.Center;
        ClearDetail(_leftColumn);
        ClearDetail(_rightColumn);
        UpdateSelection();
    }

    // Up and down step within whichever column the cursor is in.
    void NavigateUp() => MoveVertical(-1);
    void NavigateDown() => MoveVertical(1);

    private void MoveVertical(int step)
    {
        if (PauseMenu.IsPaused) return;

        if (_column != Column.Center)
        {
            var col = _column == Column.Left ? _leftColumn : _rightColumn;
            if (col.Rows.Count == 0)
            {
                FocusCenter();
                return;
            }
            SelectRow(col, (col.Index + step + col.Rows.Count) % col.Rows.Count);
            UpdateSelection();
            return;
        }

        StepCenter(step);
    }

    // The draft cards and the confirm button below them. Unchanged behaviour:
    // up off the top and down off confirm both stay put.
    private void StepCenter(int step)
    {
        if (menuItems == null || menuItems.Count == 0) return;

        if (step < 0)
        {
            if (isConfirmButtonSelected)
            {
                isConfirmButtonSelected = false;
                currentSelectedIndex = menuItems.Count - 1;
            }
            else
            {
                currentSelectedIndex = Mathf.Max(0, currentSelectedIndex - 1);
            }
        }
        else
        {
            if (isConfirmButtonSelected)
            {
                // Stay on confirm
            }
            else if (currentSelectedIndex < menuItems.Count - 1)
            {
                currentSelectedIndex++;
            }
            else
            {
                isConfirmButtonSelected = true;
            }
        }

        UpdateSelection();
    }

    private bool IsValidIndex(int index)
    {
        return menuItems != null && index >= 0 && index < menuItems.Count;
    }

    // Horizontal/toggle helpers removed in favor of explicit Up/Down logic for column layout
    
    void UpdateSelection()
    {
        // The loadout rows carry their own highlight, and only ever one of the
        // three columns is lit at a time - a card and a chip both looking
        // selected would leave the player guessing what a button press does.
        HighlightRow(_leftColumn, _column == Column.Left);
        HighlightRow(_rightColumn, _column == Column.Right);

        if (menuItems == null || menuItems.Count == 0) return;

        // Remove selected class from all items
        for (int i = 0; i < menuItems.Count; i++) menuItems[i].RemoveFromClassList(SELECTED_CLASS);

        // Remove selected class from confirm button
        if (confirmButton != null) confirmButton.RemoveFromClassList(SELECTED_CLASS);

        if (_column != Column.Center) return;

        if (isConfirmButtonSelected)
        {
            // Highlight confirm button
            if (confirmButton != null) confirmButton.AddToClassList(SELECTED_CLASS);
        }
        else
        {
            // Highlight current upgrade item
            if (IsValidIndex(currentSelectedIndex)) menuItems[currentSelectedIndex].AddToClassList(SELECTED_CLASS);
        }
    }

    private static void HighlightRow(LoadoutColumn col, bool active)
    {
        for (int i = 0; i < col.Rows.Count; i++)
        {
            bool lit = active && i == col.Index;
            if (lit) col.Rows[i].AddToClassList(ROW_SELECTED_CLASS);
            else col.Rows[i].RemoveFromClassList(ROW_SELECTED_CLASS);
        }
    }
    
    void SelectUpgrade(int upgradeIndex)
    {
        if (currentUpgrades == null) return;
    if (PauseMenu.IsPaused) return;
        if (upgradeIndex < 0 || upgradeIndex >= menuItems.Count || upgradeIndex >= currentUpgrades.Count) return;

        // Clear previous selection - this should remove visual styling from any previously chosen upgrade
        ClearChosenUpgrade();

        // Set new selection
        chosenUpgradeIndex = upgradeIndex;
        chosenKnight = selectedKnight; // Apply to the predetermined knight for this wave

        // Update visual state for the newly chosen upgrade
        menuItems[upgradeIndex].AddToClassList(CHOSEN_CLASS);
        Pulse(menuItems[upgradeIndex]);

        // Heading stays static ("Upgrade <knight>") so the cards never shift vertically;
        // the chosen card's gold state carries the feedback instead.
    }

    // Quick press pulse: punch class in, removed a beat later (USS transition does the rest)
    private void Pulse(VisualElement element)
    {
        if (element == null) return;
        element.AddToClassList(PUNCH_CLASS);
        element.schedule.Execute(() => element.RemoveFromClassList(PUNCH_CLASS)).StartingIn(90);
    }
    
    void ClearChosenUpgrade()
    {
        // Clear visual styling from all menu items to ensure no lingering chosen state
        if (menuItems != null)
        {
            if (PauseMenu.IsPaused) return;
            for (int i = 0; i < menuItems.Count; i++) menuItems[i].RemoveFromClassList(CHOSEN_CLASS);
        }
        
        // Reset the chosen state
        chosenUpgradeIndex = -1;
    // Update the status prompt to reflect current target knight
    UpdateSelectionStatusPrompt();

    }
    
    void ConfirmUpgrade()
    {
        if (_confirmPending) return;
        if (chosenUpgradeIndex >= 0 && chosenUpgradeIndex < currentUpgrades.Count)
        {
            // Play the confirm animation (button punch + card flash), then fire the
            // event a beat later so the player sees their pick land before the menu closes
            _confirmPending = true;
            AudioManager.Instance?.PlaySFX(AudioManager.Instance.upgradeConfirm);
            Pulse(confirmButton);

            var chosenCard = chosenUpgradeIndex < menuItems.Count ? menuItems[chosenUpgradeIndex] : null;
            chosenCard?.AddToClassList(CONFIRM_FLASH_CLASS);

            int confirmedIndex = chosenUpgradeIndex;
            KnightTarget confirmedKnight = chosenKnight;
            root.schedule.Execute(() =>
            {
                _confirmPending = false;
                chosenCard?.RemoveFromClassList(CONFIRM_FLASH_CLASS);
                OnUpgradeConfirmed?.Invoke(confirmedIndex, confirmedKnight);
                // Refresh status lists now that an upgrade has been applied
                RefreshStatusFromScene();
            }).StartingIn(280);
        }
    }
    
    // Public method to show/hide the menu and populate upgrades
    public void SetMenuVisible(bool visible)
    {
        if (EnsureUI())
        {
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) StopAuraTicker();
            
            // Reset selection and populate upgrades when showing menu
            if (visible)
            {
                currentSelectedIndex = 0;
                isConfirmButtonSelected = false;
                _confirmPending = false;
                _column = Column.Center;
                // Decide which knight should receive the upgrade this wave
                if (waveManager != null)
                {
                    int completed = waveManager.CompletedWavesCount; // after wave N completes, this is N
                    // After wave 1 -> Left, wave 2 -> Right, alternating thereafter
                    var target = (completed % 2 == 1) ? KnightTarget.LeftKnight : KnightTarget.RightKnight;
                    SetKnightTargetForThisUpgrade(target);
                }
                else
                {
                    SetKnightTargetForThisUpgrade(KnightTarget.LeftKnight);
                }
                UpdateWaveNumberLine();
                ClearChosenUpgrade();
                // Pull latest status from scene components (health/special/upgrades)
                RefreshStatusFromScene();
                PopulateUpgrades();
                UpdateSelection();
                PlayEntranceAnimation();
                StartAuraTicker();

                // Force refresh the UI
                root.MarkDirtyRepaint();
            }
        }
    }

    // Point each portrait at its knight's sprite and hand its aura layer the colours
    // it will be drifting. Silently does nothing if the sprites were never assigned,
    // so an unwired scene degrades to the old nameplate rather than throwing.
    private void BindPortraits()
    {
        _leftPortraitArt = root.Q<VisualElement>("left-portrait-art");
        _rightPortraitArt = root.Q<VisualElement>("right-portrait-art");

        if (_leftPortraitArt != null && leftKnightPortrait != null)
            _leftPortraitArt.style.backgroundImage = new StyleBackground(leftKnightPortrait);
        if (_rightPortraitArt != null && rightKnightPortrait != null)
            _rightPortraitArt.style.backgroundImage = new StyleBackground(rightKnightPortrait);

        var leftBack = root.Q<VisualElement>("left-aura-back");
        var leftFront = root.Q<VisualElement>("left-aura");
        var rightBack = root.Q<VisualElement>("right-aura-back");
        var rightFront = root.Q<VisualElement>("right-aura");

        _leftAura = leftFront != null && leftBack != null
            ? new OrderAura(leftBack, leftFront, leftKnightPortrait) : null;
        _rightAura = rightFront != null && rightBack != null
            ? new OrderAura(rightBack, rightFront, rightKnightPortrait) : null;
    }

    // Recount each knight's Orders and feed the mote streams. Called whenever the menu
    // opens and again after a pick lands, so a fresh upgrade thickens its Order's stream
    // immediately rather than at the start of the next wave.
    private void RefreshAuras()
    {
        if (upgradeManager == null) return;
        _leftAura?.SetCounts(CountOrders(KnightTarget.LeftKnight));
        _rightAura?.SetCounts(CountOrders(KnightTarget.RightKnight));
    }

    // How many upgrades this knight has taken from each Order. Every pick counts,
    // including later ranks of a chain the knight already owns — a knight three deep
    // in one Order should read as three deep, not as owning one thing.
    private Dictionary<UpgradeOrder, int> CountOrders(KnightTarget knight)
    {
        var counts = new Dictionary<UpgradeOrder, int>();
        var applied = upgradeManager.GetAppliedUpgrades(knight);
        if (applied == null) return counts;

        foreach (var upgrade in applied)
        {
            // Neutral is the classless filler pool and has no Order to show off.
            if (upgrade == null || upgrade.Order == UpgradeOrder.Neutral) continue;
            counts.TryGetValue(upgrade.Order, out int n);
            counts[upgrade.Order] = n + 1;
        }
        return counts;
    }

    private void StartAuraTicker()
    {
        if (root == null) return;
        if (_auraTicker == null)
        {
            _auraTicker = root.schedule.Execute(() =>
            {
                _leftAura?.Tick();
                _rightAura?.Tick();
            }).Every(OrderAura.TickMs);
        }
        _auraTicker.Resume();
    }

    private void StopAuraTicker()
    {
        _auraTicker?.Pause();
        _leftAura?.Clear();
        _rightAura?.Clear();
    }

    // The visual signature of one Order, drifting off a knight's portrait. Every Order
    // draws itself differently — see the five Effect classes below — but they all share
    // the same contract: SetCount tells the effect how many upgrades the knight has
    // taken from that Order, and Tick advances whatever animation it runs.
    //
    // Strength is uncapped by design. Each effect keeps its per-element size and
    // brightness eased toward a ceiling, so nothing degenerates into a blob, and puts
    // the unbounded growth into a count instead: more bubbles, more flames, more
    // shields. The one exception is Shadow's pulse rate, which is floored — see there.
    private class OrderAura
    {
        public const int TickMs = 220;

        private readonly Dictionary<UpgradeOrder, OrderEffect> _effects =
            new Dictionary<UpgradeOrder, OrderEffect>();

        // back sits behind the knight, front in front of him; each effect picks the
        // layer that makes it read as part of the portrait rather than stuck on top.
        public OrderAura(VisualElement back, VisualElement front, Sprite portrait)
        {
            _effects[UpgradeOrder.Serpent] = new BubbleEffect(front);
            _effects[UpgradeOrder.Shadow] = new SilhouetteEffect(back, portrait);
            _effects[UpgradeOrder.Dawn] = new GlowEffect(back);
            _effects[UpgradeOrder.Ember] = new EmberEffect(front);
            _effects[UpgradeOrder.Guardian] = new OrbitEffect(front);
            // In FRONT, like Ember. The in-game ward sorts under the actors, but a
            // 104x84 portrait is almost entirely knight: measured on the back layer,
            // his sprite hid most of the ring and the effect read as two or three
            // specks. The rim has to be over him to be a rim at all.
            _effects[UpgradeOrder.Frigid] = new FrostEffect(front);
        }

        public void SetCounts(Dictionary<UpgradeOrder, int> counts)
        {
            foreach (var pair in _effects)
            {
                counts.TryGetValue(pair.Key, out int count);
                pair.Value.SetCount(count);
            }
        }

        public void Clear()
        {
            foreach (var pair in _effects) pair.Value.Clear();
        }

        public void Tick()
        {
            foreach (var pair in _effects) pair.Value.Tick();
        }
    }

    // Shared shape for the five Order effects.
    private abstract class OrderEffect
    {
        protected readonly VisualElement Layer;
        protected int Count;
        protected static readonly System.Random Rng = new System.Random();

        protected OrderEffect(VisualElement layer) { Layer = layer; }

        // How far along its growth curve this Order is: 0 at none, 0.28 at one
        // upgrade, 0.86 at six, never quite 1. Per-element size and brightness ride
        // this so they ease toward a ceiling instead of running away.
        protected float Depth => Count <= 0 ? 0f : 1f - Mathf.Pow(0.72f, Count);

        public virtual void SetCount(int count)
        {
            if (count == Count) return;
            Count = count;
            OnCountChanged();
        }

        protected virtual void OnCountChanged() { }
        public virtual void Tick() { }

        // Resets the count as well as tearing down the visuals. Without that, closing
        // and reopening the menu leaves SetCount seeing an unchanged number, skipping
        // OnCountChanged, and never rebuilding what Clear just removed.
        public void Clear()
        {
            Count = 0;
            OnClear();
        }

        protected virtual void OnClear() { }
    }

    // SERPENT — bubbles rising off the knight and popping out of view.
    // Growth is in the spawn rate, which is linear and never clamped.
    private class BubbleEffect : OrderEffect
    {
        private const int LifeMs = 1400;
        private float _owed;

        public BubbleEffect(VisualElement layer) : base(layer) { }

        protected override void OnClear()
        {
            _owed = 0f;
            RemoveOwned(Layer, "order-mote");
        }

        public override void Tick()
        {
            if (Layer == null || Count <= 0) return;

            // 0.22 bubbles a tick at one upgrade — one speck every second or so — and
            // another 0.22 for every upgrade after that, with the fraction carried over
            // so a single upgrade can spawn slower than one bubble per tick.
            _owed += Count * 0.22f;
            while (_owed >= 1f) { Spawn(); _owed -= 1f; }
        }

        private void Spawn()
        {
            float depth = Depth;
            float size = 2f + 3.5f * depth;
            float rise = 26f + 34f * depth;

            var bubble = new VisualElement();
            bubble.AddToClassList("order-mote");
            bubble.style.backgroundColor = WithAlpha(OrderColor(UpgradeOrder.Serpent), 0.10f + 0.62f * depth);
            bubble.style.width = size;
            bubble.style.height = size;
            bubble.style.left = Length.Percent(18f + (float)Rng.NextDouble() * 64f);
            bubble.style.bottom = 6f + (float)Rng.NextDouble() * 22f;
            bubble.style.transitionDuration = Times(LifeMs, LifeMs);
            Layer.Add(bubble);

            float drift = ((float)Rng.NextDouble() - 0.5f) * 16f;
            bubble.schedule.Execute(() =>
            {
                bubble.style.translate = new StyleTranslate(new Translate(drift, -rise));
                bubble.style.opacity = 0f;
            }).StartingIn(FrameMs);

            bubble.schedule.Execute(() => bubble.RemoveFromHierarchy()).StartingIn(FrameMs + LifeMs);
        }
    }

    // SHADOW — the knight's own outline behind him, filled flat purple, breathing, with a
    // ghost of it swelling outward and dissolving every so often. The breath quickens and
    // the ghosts come faster the deeper the Order runs.
    private class SilhouetteEffect : OrderEffect
    {
        // Floor on the beat. Uncapped speed becomes a strobe, which is both unreadable
        // and genuinely unpleasant to look at, so this is the one growth curve that stops.
        private const int MinHalfBeatMs = 150;
        private const int EchoMs = 1100;

        private readonly Sprite _portrait;
        private VisualElement _shape;
        private bool _swelled;
        private int _elapsed;
        private int _echoElapsed;

        public SilhouetteEffect(VisualElement layer, Sprite portrait) : base(layer)
        {
            _portrait = portrait;
        }

        protected override void OnClear()
        {
            _shape?.RemoveFromHierarchy();
            _shape = null;
            RemoveOwned(Layer, "order-echo");
            _elapsed = 0;
            _echoElapsed = 0;
            _swelled = false;
        }

        protected override void OnCountChanged()
        {
            if (Layer == null || _portrait == null) return;
            if (Count <= 0) { Clear(); return; }

            if (_shape == null)
            {
                _shape = NewShape();
                _shape.style.opacity = 0f;
                Layer.Add(_shape);
            }
            _shape.style.transitionDuration = Times(HalfBeatMs, HalfBeatMs);
        }

        private VisualElement NewShape()
        {
            var element = new VisualElement();
            element.AddToClassList("order-silhouette");
            element.style.backgroundImage = new StyleBackground(Silhouette(_portrait));
            return element;
        }

        // 900ms at one upgrade, tightening toward the floor as the knight commits.
        private int HalfBeatMs => Mathf.Max(MinHalfBeatMs, Mathf.RoundToInt(900f / (1f + 0.4f * (Count - 1))));

        // One ghost every 2.4s at first, closing to roughly one a second by six deep.
        private int EchoIntervalMs => Mathf.Max(320, Mathf.RoundToInt(2400f / (1f + 0.45f * (Count - 1))));

        public override void Tick()
        {
            if (_shape == null || Count <= 0) return;

            float depth = Depth;

            // The beat is not a multiple of the tick, so the swell runs off an
            // accumulator rather than assuming one flip per tick.
            _elapsed += OrderAura.TickMs;
            if (_elapsed >= HalfBeatMs)
            {
                _elapsed = 0;
                _swelled = !_swelled;

                float rest = 1.05f + 0.11f * depth;
                float peak = rest + 0.06f + 0.10f * depth;
                _shape.style.opacity = _swelled ? 0.44f + 0.40f * depth : 0.24f + 0.26f * depth;
                float scale = _swelled ? peak : rest;
                _shape.style.scale = new StyleScale(new Scale(new Vector2(scale, scale)));
            }

            _echoElapsed += OrderAura.TickMs;
            if (_echoElapsed >= EchoIntervalMs)
            {
                _echoElapsed = 0;
                SpawnEcho(depth);
            }
        }

        // A copy of the outline that swells past the knight and dissolves — his shape
        // pulling away from itself rather than a ring drawn around him.
        private void SpawnEcho(float depth)
        {
            var echo = NewShape();
            echo.AddToClassList("order-echo");
            echo.style.opacity = 0.38f + 0.42f * depth;
            echo.style.transitionDuration = Times(EchoMs, EchoMs);
            echo.style.transitionTimingFunction = Easings(EasingMode.EaseOutCubic);
            Layer.Add(echo);

            float grown = 1.20f + 0.30f * depth;
            echo.schedule.Execute(() =>
            {
                echo.style.scale = new StyleScale(new Scale(new Vector2(grown, grown)));
                echo.style.opacity = 0f;
            }).StartingIn(FrameMs);

            echo.schedule.Execute(() => echo.RemoveFromHierarchy()).StartingIn(FrameMs + EchoMs);
        }
    }


    // DAWN — light behind the knight. Two discs of the soft-falloff texture at different
    // widths, breathing out of step with each other, so the edge stays indefinite instead
    // of reading as the hard rim of a spotlight.
    private class GlowEffect : OrderEffect
    {
        private const int CoreBreathMs = 1760;
        // Deliberately not a multiple of the core's: the two never line up, so the glow
        // never settles into a single obvious pulse.
        private const int HaloBreathMs = 2420;

        private VisualElement _core, _halo;
        private bool _coreIn, _haloIn;
        private int _coreElapsed, _haloElapsed;

        public GlowEffect(VisualElement layer) : base(layer) { }

        protected override void OnClear()
        {
            _core?.RemoveFromHierarchy();
            _halo?.RemoveFromHierarchy();
            _core = _halo = null;
            _coreElapsed = _haloElapsed = 0;
            _coreIn = _haloIn = false;
        }

        protected override void OnCountChanged()
        {
            if (Layer == null) return;
            if (Count <= 0) { Clear(); return; }

            _halo ??= AddDisc(HaloBreathMs);
            _core ??= AddDisc(CoreBreathMs);

            float depth = Depth;
            // The eased term settles; the linear tail keeps it widening forever, slowly
            // enough that it stays a glow rather than swallowing the panel.
            float size = 54f + 40f * depth + 4f * Count;
            Size(_core, size, WithAlpha(new Color(1f, 0.95f, 0.82f), 0.30f + 0.45f * depth));
            Size(_halo, size * 1.9f, WithAlpha(new Color(1f, 0.88f, 0.68f), 0.14f + 0.26f * depth));
        }

        private VisualElement AddDisc(int breathMs)
        {
            var disc = new VisualElement();
            disc.AddToClassList("order-glow");
            disc.style.backgroundImage = new StyleBackground(SoftDisc());
            disc.style.transitionDuration = Times(breathMs, breathMs, breathMs);
            Layer.Add(disc);
            return disc;
        }

        // Centred on the knight's chest rather than on the box, so the light reads as
        // coming off him.
        private static void Size(VisualElement disc, float size, Color tint)
        {
            disc.style.width = size;
            disc.style.height = size;
            disc.style.left = Length.Percent(50f);
            disc.style.bottom = 30f;
            disc.style.marginLeft = -size / 2f;
            disc.style.marginBottom = -size / 2f;
            disc.style.unityBackgroundImageTintColor = tint;
        }

        public override void Tick()
        {
            if (_core == null || Count <= 0) return;

            _coreElapsed += OrderAura.TickMs;
            if (_coreElapsed >= CoreBreathMs)
            {
                _coreElapsed = 0;
                _coreIn = !_coreIn;
                Breathe(_core, _coreIn ? 1.05f : 0.96f);
            }

            _haloElapsed += OrderAura.TickMs;
            if (_haloElapsed >= HaloBreathMs)
            {
                _haloElapsed = 0;
                _haloIn = !_haloIn;
                Breathe(_halo, _haloIn ? 1.09f : 0.94f);
            }
        }

        private static void Breathe(VisualElement disc, float scale)
        {
            disc.style.scale = new StyleScale(new Scale(new Vector2(scale, scale)));
        }
    }

    // EMBER — a fire at the knight's feet that climbs the frame.
    //
    // Two populations, because they behave nothing alike. HEARTH sparks sit in the fire
    // at his feet and only wink in and out; they never travel. EDGE sparks are the ones
    // pinned to the left and right edges of the frame, and those genuinely burn upward —
    // they rise, cool from orange to dark red as they go, and are gone by the top. The
    // share of edge sparks grows with the Order, so the fire starts as a hearth and ends
    // up running up both sides.
    //
    // Every duration is rolled per spark. Shared timings are what made the earlier
    // version hop in unison rather than drift.
    private class EmberEffect : OrderEffect
    {
        // The fire's palette, hottest first. Sparks pick from it by size — the small
        // ones are the ones still burning bright.
        private static readonly Color PaleOrange = new Color32(255, 196, 116, 255);
        private static readonly Color Orange = new Color32(232, 124, 58, 255);
        private static readonly Color DarkRed = new Color32(146, 42, 26, 255);

        // The sprite's baseline sits above where the knight's feet read, so sparks
        // spawned at the bottom of the box floated. This drops the whole fire to the
        // ground he is actually standing on.
        private const float GroundOffset = -8f;

        private float _owed;

        public EmberEffect(VisualElement layer) : base(layer) { }

        protected override void OnClear()
        {
            _owed = 0f;
            RemoveOwned(Layer, "order-ember");
        }

        public override void Tick()
        {
            if (Layer == null || Count <= 0) return;

            // Dense on purpose: a fire is a scatter of sparks, not a handful of dots.
            _owed += Count * 2.2f;
            while (_owed >= 1f)
            {
                _owed -= 1f;
                // Edge sparks take over as the Order deepens — 28% of them at one
                // upgrade, 86% by six.
                if (Rng.NextDouble() < Depth) SpawnEdge();
                else SpawnHearth();
            }
        }

        // Colour by size: a 1px spark is still white-hot, a 4px one has cooled to embers.
        private static Color HeatFor(float size)
        {
            float t = Mathf.InverseLerp(1f, 4f, size);
            return t < 0.5f
                ? Color.Lerp(PaleOrange, Orange, t * 2f)
                : Color.Lerp(Orange, DarkRed, (t - 0.5f) * 2f);
        }

        private VisualElement NewSpark(float size, Color color, float leftPercent, float bottom)
        {
            var spark = new VisualElement();
            spark.AddToClassList("order-ember");
            spark.style.width = size;
            spark.style.height = size;
            spark.style.backgroundColor = color;
            spark.style.left = Length.Percent(leftPercent);
            spark.style.bottom = bottom;
            spark.style.opacity = 0f;
            Layer.Add(spark);
            return spark;
        }

        // In the fire itself: glows up, sits, dies. Never leaves the ground.
        private void SpawnHearth()
        {
            float depth = Depth;
            float size = 1f + (float)Rng.NextDouble() * 3f;
            // Clustered under the knight, loosening a little as the fire grows.
            float u = Mathf.Pow((float)Rng.NextDouble(), 2.2f - 0.7f * depth) * (Rng.Next(2) == 0 ? -1f : 1f);

            int up = 260 + Rng.Next(260);
            int hold = 120 + Rng.Next(320);
            int down = 260 + Rng.Next(400);

            var spark = NewSpark(size, HeatFor(size), 50f + u * 30f, GroundOffset + (float)Rng.NextDouble() * 7f);
            spark.style.transitionDuration = Times(up, up, up);

            float peak = (0.35f + 0.45f * depth) * (0.6f + (float)Rng.NextDouble() * 0.4f);
            // A breath of lift, not a jump — enough to say the air above a fire moves.
            float lift = 2f + (float)Rng.NextDouble() * 5f;

            spark.schedule.Execute(() =>
            {
                spark.style.opacity = peak;
                spark.style.translate = new StyleTranslate(new Translate(0f, -lift * 0.35f));
            }).StartingIn(FrameMs);

            spark.schedule.Execute(() =>
            {
                spark.style.transitionDuration = Times(down, down, down);
                spark.style.opacity = 0f;
                spark.style.translate = new StyleTranslate(new Translate(0f, -lift));
            }).StartingIn(FrameMs + up + hold);

            spark.schedule.Execute(() => spark.RemoveFromHierarchy())
                 .StartingIn(FrameMs + up + hold + down);
        }

        // On the frame's edge: rises the whole way and burns out near the top. There is
        // no downward leg at all — sparks going up a burning edge do not come back.
        private void SpawnEdge()
        {
            float depth = Depth;
            float size = 1f + (float)Rng.NextDouble() * 2.6f;
            float sign = Rng.Next(2) == 0 ? -1f : 1f;

            // Hugging the edge, with a little scatter so the column has width.
            float leftPercent = sign < 0f
                ? 3f + (float)Rng.NextDouble() * 9f
                : 88f + (float)Rng.NextDouble() * 9f;

            // The whole height of the frame at depth, a third of it early on.
            float rise = (34f + 62f * depth) * (0.7f + (float)Rng.NextDouble() * 0.6f);

            // The curve. A spark commits to one lateral direction — mostly inward, over
            // the knight, the way heat draws in above a fire — and its sideways travel
            // accelerates while its climb decelerates. Sideways speeding up against
            // upward slowing down is what bends the path into an arc; equal steps in both
            // would just be a diagonal.
            float curl = (-sign * (0.5f + (float)Rng.NextDouble() * 0.9f)
                          + (float)(Rng.NextDouble() - 0.5) * 0.5f) * (9f + 22f * depth);

            int fadeIn = 90 + Rng.Next(130);
            int climb = 420 + Rng.Next(380);
            int arc = 380 + Rng.Next(320);
            int burnOut = 340 + Rng.Next(360);

            var spark = NewSpark(size, HeatFor(size), leftPercent, GroundOffset + 2f + (float)Rng.NextDouble() * 10f);
            spark.style.transitionDuration = Times(fadeIn, fadeIn, fadeIn);

            float peak = (0.55f + 0.4f * depth) * (0.65f + (float)Rng.NextDouble() * 0.35f);

            spark.schedule.Execute(() => spark.style.opacity = peak).StartingIn(FrameMs);

            // Leg one: straight off the fire, barely leaning yet.
            spark.schedule.Execute(() =>
            {
                spark.style.transitionDuration = Times(climb, climb, climb);
                spark.style.transitionTimingFunction = Easings(EasingMode.EaseOutSine);
                spark.style.translate = new StyleTranslate(new Translate(curl * 0.18f, -rise * 0.46f));
                spark.style.backgroundColor = Color.Lerp(HeatFor(size), DarkRed, 0.35f);
            }).StartingIn(FrameMs + fadeIn);

            // Leg two: through the bend — most of the sideways travel happens here while
            // the climb is already flattening off.
            spark.schedule.Execute(() =>
            {
                spark.style.transitionDuration = Times(arc, arc, arc);
                spark.style.transitionTimingFunction = Easings(EasingMode.EaseInOutSine);
                spark.style.translate = new StyleTranslate(new Translate(curl * 0.62f, -rise * 0.80f));
                spark.style.backgroundColor = Color.Lerp(HeatFor(size), DarkRed, 0.7f);
            }).StartingIn(FrameMs + fadeIn + climb);

            // Leg three: drifting off sideways with almost no lift left, and out.
            spark.schedule.Execute(() =>
            {
                spark.style.transitionDuration = Times(burnOut, burnOut, burnOut);
                spark.style.transitionTimingFunction = Easings(EasingMode.EaseOutSine);
                spark.style.translate = new StyleTranslate(new Translate(curl, -rise));
                spark.style.backgroundColor = DarkRed;
                spark.style.opacity = 0f;
            }).StartingIn(FrameMs + fadeIn + climb + arc);

            spark.schedule.Execute(() => spark.RemoveFromHierarchy())
                 .StartingIn(FrameMs + fadeIn + climb + arc + burnOut);
        }
    }

    // GUARDIAN — shield diamonds circling the knight, the way the real ones orbit him on
    // the field. Each is driven individually rather than by spinning a container, so the
    // half of the ring passing behind him can be drawn smaller and dimmer and the orbit
    // reads with some depth to it. Every second upgrade adds another shield, and the
    // whole ring turns faster.
    //
    // NOT SPECIFIED BY THE BRIEF: Shadow, Dawn, Serpent and Ember were described,
    // Guardian was not. This mirrors the shield-orbit mechanic; say the word and it
    // becomes something else.
    private class OrbitEffect : OrderEffect
    {
        private readonly List<VisualElement> _shields = new List<VisualElement>();
        private readonly List<VisualElement> _gleams = new List<VisualElement>();
        private float _angle;

        public OrbitEffect(VisualElement layer) : base(layer) { }

        protected override void OnClear()
        {
            foreach (var shield in _shields) shield.RemoveFromHierarchy();
            foreach (var gleam in _gleams) gleam.RemoveFromHierarchy();
            _shields.Clear();
            _gleams.Clear();
            _angle = 0f;
        }

        protected override void OnCountChanged()
        {
            if (Layer == null) return;
            if (Count <= 0) { Clear(); return; }

            int wanted = 1 + Count / 2;
            while (_shields.Count < wanted)
            {
                // The gleam goes down first so the diamond rides on top of its own light.
                var gleam = new VisualElement();
                gleam.AddToClassList("order-gleam");
                gleam.style.backgroundImage = new StyleBackground(SoftDisc());
                Layer.Add(gleam);
                _gleams.Add(gleam);

                var shield = new VisualElement();
                shield.AddToClassList("order-shield");
                Layer.Add(shield);
                _shields.Add(shield);
            }
            while (_shields.Count > wanted)
            {
                _shields[_shields.Count - 1].RemoveFromHierarchy();
                _gleams[_gleams.Count - 1].RemoveFromHierarchy();
                _shields.RemoveAt(_shields.Count - 1);
                _gleams.RemoveAt(_gleams.Count - 1);
            }

            // translate, width, height and colour, all on the tick, so the ring glides
            // between placements instead of stepping five times a second.
            var glide = Times(OrderAura.TickMs, OrderAura.TickMs, OrderAura.TickMs, OrderAura.TickMs);
            foreach (var element in _shields) element.style.transitionDuration = glide;
            foreach (var element in _gleams) element.style.transitionDuration = glide;
            Place();
        }

        public override void Tick()
        {
            if (Count <= 0) return;
            // Degrees per tick, climbing with every upgrade and never clamped.
            _angle += 14f + 5f * Count;
            Place();
        }

        // How far above the panel's middle the ring rides.
        //
        // The aura layer is 104x84 with the 64px knight standing on its floor, so
        // `top: 50%` puts the orbit at 42px up — around his chest, where it read as a
        // belt he was wearing. Lifting it 26 puts the centre at 68, and since the
        // flattened ring only reaches about 9px above and below that, the whole thing
        // sits just clear of his head at 64 and stays inside the layer.
        private const float LiftY = 26f;

        private void Place()
        {
            float depth = Depth;
            float radius = 26f + 9f * depth;
            float baseSize = 3.5f + 3f * depth;
            Color steel = OrderColor(UpgradeOrder.Guardian);

            for (int i = 0; i < _shields.Count; i++)
            {
                float theta = Mathf.Deg2Rad * _angle + (Mathf.PI * 2f * i) / _shields.Count;
                float x = Mathf.Cos(theta) * radius;
                // Flattened, so the ring lies around the knight instead of spinning flat
                // against the panel. Negative moves UP in UI Toolkit — see LiftY.
                float y = Mathf.Sin(theta) * radius * 0.34f - LiftY;

                // sin(theta) > 0 is the far half of the orbit: smaller, dimmer, behind.
                float near = (1f - Mathf.Sin(theta)) * 0.5f;
                float size = baseSize * (0.68f + 0.5f * near);
                float alpha = (0.20f + 0.55f * depth) * (0.45f + 0.75f * near);

                Put(_shields[i], x, y, size);
                _shields[i].style.backgroundColor = WithAlpha(steel, Mathf.Min(alpha, 1f));

                // The light around a shield swells as it swings past the front.
                float gleamSize = size * 4.5f;
                Put(_gleams[i], x, y, gleamSize);
                _gleams[i].style.unityBackgroundImageTintColor =
                    WithAlpha(steel, Mathf.Min(alpha * 0.42f * near, 1f));
            }
        }

        private static void Put(VisualElement element, float x, float y, float size)
        {
            element.style.width = size;
            element.style.height = size;
            element.style.left = Length.Percent(50f);
            element.style.top = Length.Percent(50f);
            element.style.translate = new StyleTranslate(new Translate(x - size / 2f, y - size / 2f));
        }
    }

    // FRIGID - the Glacial Ward's ring of cold hung on the portrait, with the chill
    // falling through it. Frost is the one Order that does not rise: FrostFx's whole
    // thesis is that cold SINKS, settles, and dies pale, so every speck here drifts
    // downward and none of it licks upward the way Ember's sparks do. Borrowing
    // EmberEffect was a placeholder that made a Frigid knight look like he was burning
    // in blue; this is the real one.
    //
    // Two populations, mirroring the two things the Order actually puts on the field:
    //   SETTLING - specks appearing over the knight and sinking through him, going out
    //              before they land. This is the chill FrostFx.AttachEnemyChill draws.
    //   RING     - specks struck off a circle around his middle and sliding off it,
    //              which is FrostFx.AttachWardRing exactly: same centre, same reason
    //              (a ring hung at his feet sags below him), edge only, never filled.
    // The ring's share grows with the Order, so one Frigid pick reads as a knight who
    // is merely cold and six reads as a knight standing inside a ward.
    private class FrostEffect : OrderEffect
    {
        // FrostFx's own palette. Pale at the core, cyan through the body, cold dark
        // blue to die into - kept in step with FrostFx.PaleIce and FrostFx.Cyan.
        private static readonly Color PaleIce = new Color(0.85f, 0.96f, 1f);
        private static readonly Color Cyan = new Color(0.47f, 0.80f, 0.92f);
        private static readonly Color DeepBlue = new Color(0.24f, 0.45f, 0.62f);

        // The aura layer is 104x84 with the 64px knight standing on its floor, so 52 is
        // his centre line and his middle is about 30px up. The ring is centred there
        // rather than at his feet for the same reason the in-game one is.
        private const float CenterX = 52f;
        private const float MidY = 30f;

        private float _owed;

        public FrostEffect(VisualElement layer) : base(layer) { }

        protected override void OnClear()
        {
            _owed = 0f;
            RemoveOwned(Layer, "order-frost");
        }

        public override void Tick()
        {
            if (Layer == null || Count <= 0) return;

            // Roughly the fire's spawn rate, because the count on screen has to be
            // comparable for the portrait to read at all - what keeps it from looking
            // like blue flame is the DIRECTION and the speed, not a thin scatter. An
            // earlier pass at half this rate came out as three or four specks.
            _owed += Count * 2f;
            while (_owed >= 1f)
            {
                _owed -= 1f;
                if (Rng.NextDouble() < Depth) SpawnRing();
                else SpawnSettling();
            }
        }

        // Colour by size, the exact inverse of the fire's rule: the small specks are the
        // ones still bright, and the big soft ones have already cooled toward cyan.
        // Deliberately stops AT cyan and never reaches DeepBlue here - a fleck that
        // spawns dark has nowhere to go and reads as nothing on this backdrop.
        private static Color ChillFor(float size)
        {
            return Color.Lerp(PaleIce, Cyan, Mathf.InverseLerp(1.5f, 4.5f, size));
        }

        private VisualElement NewFleck(float size, Color color, float left, float bottom)
        {
            var fleck = new VisualElement();
            fleck.AddToClassList("order-frost");
            fleck.style.width = size;
            fleck.style.height = size;
            fleck.style.backgroundColor = color;
            fleck.style.left = left - size / 2f;
            fleck.style.bottom = bottom;
            fleck.style.opacity = 0f;
            Layer.Add(fleck);
            return fleck;
        }

        // The chill. Appears above him, sinks through him on a slight wander, and is out
        // before it reaches the ground - snow that never quite lands.
        private void SpawnSettling()
        {
            float depth = Depth;
            float size = 1.5f + (float)Rng.NextDouble() * 3f;
            float spread = 15f + 14f * depth;
            float left = CenterX + (float)(Rng.NextDouble() - 0.5) * 2f * spread;

            // Started well above his head so the fall has room to happen, and stopped
            // short of the floor so nothing piles up in a line along the bottom edge.
            float start = MidY + 26f + (float)Rng.NextDouble() * 16f;
            float fall = start - 3f - (float)Rng.NextDouble() * 11f;
            float sway = (float)(Rng.NextDouble() - 0.5) * 13f;

            int fadeIn = 90 + Rng.Next(110);
            int sink = 1100 + Rng.Next(900);
            int fadeOut = 200 + Rng.Next(180);

            var fleck = NewFleck(size, ChillFor(size), left, start);
            fleck.style.transitionDuration = Times(fadeIn, fadeIn, fadeIn);

            float peak = (0.58f + 0.40f * depth) * (0.65f + (float)Rng.NextDouble() * 0.35f);
            fleck.schedule.Execute(() => fleck.style.opacity = peak).StartingIn(FrameMs);

            // Linear, and only linear. Cold falling does not accelerate off a source or
            // ease into a stop; an eased sink is what would make this look like smoke.
            fleck.schedule.Execute(() =>
            {
                fleck.style.transitionDuration = Times(sink, sink, sink);
                fleck.style.transitionTimingFunction = Easings(EasingMode.Linear);
                fleck.style.translate = new StyleTranslate(new Translate(sway * 0.6f, fall * 0.72f));
            }).StartingIn(FrameMs + fadeIn);

            fleck.schedule.Execute(() =>
            {
                fleck.style.transitionDuration = Times(fadeOut, fadeOut, fadeOut);
                fleck.style.translate = new StyleTranslate(new Translate(sway, fall));
                fleck.style.backgroundColor = Color.Lerp(Cyan, DeepBlue, 0.6f);
                fleck.style.opacity = 0f;
            }).StartingIn(FrameMs + fadeIn + sink);

            fleck.schedule.Execute(() => fleck.RemoveFromHierarchy())
                 .StartingIn(FrameMs + fadeIn + sink + fadeOut);
        }

        // The ward. A speck struck off the ring, which holds its place, slides a few
        // pixels down and outward, and goes out. Barely any travel on purpose: motes
        // that wander lose the circle, and the circle is the whole point - the ward's
        // job on the field is telling the player where its EDGE is.
        private void SpawnRing()
        {
            float depth = Depth;
            float size = 1.5f + (float)Rng.NextDouble() * 2.4f;
            // Half again as wide as it first shipped: at the old radius the rim sat
            // close enough to the knight to read as an outline on him rather than as
            // a ring of cold standing off him, which is what the ward actually is.
            float radius = 33f + 19.5f * depth;
            float angle = (float)Rng.NextDouble() * Mathf.PI * 2f;

            float left = CenterX + Mathf.Cos(angle) * radius;
            float bottom = MidY + Mathf.Sin(angle) * radius;

            float drop = 4f + (float)Rng.NextDouble() * 7f;
            float outward = Mathf.Cos(angle) * (1f + (float)Rng.NextDouble() * 2.5f);

            int fadeIn = 80 + Rng.Next(90);
            int hold = 620 + Rng.Next(600);
            int fadeOut = 180 + Rng.Next(200);

            // Brighter than the falling chill: the rim is the read, the snow is texture.
            var fleck = NewFleck(size, ChillFor(size), left, bottom);
            fleck.style.transitionDuration = Times(fadeIn, fadeIn, fadeIn);

            float peak = (0.82f + 0.18f * depth) * (0.8f + (float)Rng.NextDouble() * 0.2f);
            fleck.schedule.Execute(() => fleck.style.opacity = peak).StartingIn(FrameMs);

            fleck.schedule.Execute(() =>
            {
                fleck.style.transitionDuration = Times(hold, hold, hold);
                fleck.style.transitionTimingFunction = Easings(EasingMode.Linear);
                fleck.style.translate = new StyleTranslate(new Translate(outward * 0.5f, drop * 0.6f));
            }).StartingIn(FrameMs + fadeIn);

            fleck.schedule.Execute(() =>
            {
                fleck.style.transitionDuration = Times(fadeOut, fadeOut, fadeOut);
                fleck.style.translate = new StyleTranslate(new Translate(outward, drop));
                fleck.style.backgroundColor = Color.Lerp(Cyan, DeepBlue, 0.5f);
                fleck.style.opacity = 0f;
            }).StartingIn(FrameMs + fadeIn + hold);

            fleck.schedule.Execute(() => fleck.RemoveFromHierarchy())
                 .StartingIn(FrameMs + fadeIn + hold + fadeOut);
        }
    }

    // The knight's outline as a flat white shape: his alpha kept, every colour in him
    // thrown away. Tinting the sprite itself does not do this — tint multiplies, so his
    // dark armour stays dark and you get a purple-ish knight rather than a purple one.
    //
    // The source texture is imported non-readable, so the pixels come back off the GPU
    // through a temporary RenderTexture rather than through GetPixels. Cached per sprite;
    // this runs once for each knight, the first time the menu opens.
    private static readonly Dictionary<Sprite, Texture2D> _silhouettes = new Dictionary<Sprite, Texture2D>();

    private static Texture2D Silhouette(Sprite sprite)
    {
        if (sprite == null) return null;
        if (_silhouettes.TryGetValue(sprite, out var cached) && cached != null) return cached;

        Texture2D source = sprite.texture;
        var temporary = RenderTexture.GetTemporary(
            source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Graphics.Blit(source, temporary);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = temporary;

        // Read the whole sheet, then cut this sprite's frame out of it. Reading the full
        // texture keeps the coordinate convention the same at both ends, which a partial
        // read does not guarantee.
        var sheet = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        sheet.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
        sheet.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);

        Rect frame = sprite.textureRect;
        int width = Mathf.RoundToInt(frame.width);
        int height = Mathf.RoundToInt(frame.height);
        Color32[] pixels = sheet.GetPixels32();

        // Bilinear, unlike the knight art itself: this is light, and a point filter would
        // draw the feather as a staircase of hard alpha steps.
        var shape = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        // Cut the frame out as a solid mask first.
        var solid = new bool[width * height];
        int originX = Mathf.RoundToInt(frame.x);
        int originY = Mathf.RoundToInt(frame.y);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                solid[y * width + x] = pixels[(originY + y) * source.width + (originX + x)].a > 8;
            }
        }

        // Then feather it inward: a pixel's alpha is how far it sits from the nearest
        // empty pixel, so the middle of the shape stays full strength and the outline
        // fades out. A flat fill of his silhouette reads as a hard purple cut-out; this
        // makes the same shape sit on him like light instead.
        //
        // Sized off the frame only as a search bound. The knight does not fill his frame,
        // so a feather taken from the frame's size erases him — measured on a 64px frame
        // holding a 215px figure, not one pixel survived at full strength. The actual
        // scale comes from the shape itself, below.
        int feather = Mathf.Max(2, Mathf.RoundToInt(Mathf.Min(width, height) * 0.11f));
        var distance = new int[width * height];
        int thickest = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!solid[y * width + x]) continue;

                // Nearest empty pixel within the feather radius. Off the edge of the
                // frame counts as empty, so limbs that run to the border still soften.
                int nearest = feather;
                for (int dy = -feather; dy <= feather && nearest > 0; dy++)
                {
                    for (int dx = -feather; dx <= feather; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        bool empty = nx < 0 || ny < 0 || nx >= width || ny >= height
                                     || !solid[ny * width + nx];
                        if (!empty) continue;

                        int reach = Mathf.RoundToInt(Mathf.Sqrt(dx * dx + dy * dy));
                        if (reach < nearest) nearest = reach;
                        if (nearest == 0) break;
                    }
                }

                distance[y * width + x] = nearest;
                if (nearest > thickest) thickest = nearest;
            }
        }

        // Normalising by the thickest part of the figure rather than by the search radius
        // is what keeps this safe at any sprite size: whatever the knight's build, his
        // core always reaches full strength and only his outline fades.
        float deepest = Mathf.Max(1, thickest);
        var cut = new Color32[width * height];
        for (int i = 0; i < cut.Length; i++)
        {
            float t = solid[i] ? Mathf.Clamp01(distance[i] / deepest) : 0f;
            cut[i] = new Color32(255, 255, 255, (byte)(t * 255f));
        }

        shape.SetPixels32(cut);
        shape.Apply(false, true);
        UnityEngine.Object.Destroy(sheet);

        _silhouettes[sprite] = shape;
        return shape;
    }

    // A white disc that fades to nothing at its rim, built once and tinted per use.
    // UI Toolkit has no blur and no radial gradient, so a flat rounded element is always
    // a hard-edged circle — a spotlight, not a glow. Painting the falloff into a texture
    // is the only way to get light that ends softly.
    private static Texture2D _softDisc;

    private static Texture2D SoftDisc()
    {
        if (_softDisc != null) return _softDisc;

        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        var pixels = new Color32[size * size];
        float centre = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - centre) / centre;
                float dy = (y - centre) / centre;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                // Cubed falloff: a bright, tight core with a long faint tail, which is
                // what reads as light rather than as a disc.
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = alpha * alpha * alpha;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        _softDisc = texture;
        return texture;
    }


    // One frame's grace, so a freshly added element has its start state resolved before
    // the transition to its end state begins — the same reason the card entrance needs
    // its lead-in.
    private const int FrameMs = 16;

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    // One entry per animated property. Sized to the longest transition-property list in
    // play so no caller has to count; entries past a given element's property count are
    // simply unused.
    private static List<EasingFunction> Easings(EasingMode mode)
    {
        var functions = new List<EasingFunction>(4);
        for (int i = 0; i < 4; i++) functions.Add(new EasingFunction(mode));
        return functions;
    }

    private static List<TimeValue> Times(params int[] milliseconds)
    {
        var values = new List<TimeValue>(milliseconds.Length);
        foreach (int ms in milliseconds) values.Add(new TimeValue(ms, TimeUnit.Millisecond));
        return values;
    }

    // The Order palette already used by the badges, chips and detail names.
    private static Color OrderColor(UpgradeOrder order)
    {
        switch (order)
        {
            case UpgradeOrder.Serpent:  return new Color32(126, 179, 91, 255);
            case UpgradeOrder.Shadow:   return new Color32(129, 128, 214, 255);
            case UpgradeOrder.Ember:    return new Color32(226, 120, 63, 255);
            case UpgradeOrder.Guardian: return new Color32(158, 168, 178, 255);
            case UpgradeOrder.Dawn:     return new Color32(240, 200, 170, 255);
            // Cyan rather than a plain blue: blue already reads as the RIGHT
            // knight in this game (arrow_blue, shield_blue, Player_Projectile_Blue),
            // and an Order accent that could be mistaken for a knight's colourway
            // is an accent that tells the player nothing.
            case UpgradeOrder.Frigid:   return new Color32(120, 200, 224, 255);
            default:                    return new Color32(179, 168, 143, 255);
        }
    }

    // Drop every child of a layer carrying the given class.
    private static void RemoveOwned(VisualElement layer, string className)
    {
        if (layer == null) return;
        for (int i = layer.childCount - 1; i >= 0; i--)
        {
            if (layer[i].ClassListContains(className)) layer.RemoveAt(i);
        }
    }

    // Slide the visible cards (and then the confirm button) up into view one at a time.
    // .card-enter parks an element below its slot and invisible; .card-sliding carries the
    // longer transition, so removing .card-enter starts the travel and removing .card-sliding
    // afterwards hands the element back to its normal 0.15s hover/select timing.
    // Each card knocks as it lands; the confirm button settles on a lower knock.
    private void PlayEntranceAnimation()
    {
        var entering = new List<VisualElement>();
        foreach (var item in menuItems)
        {
            if (item.style.display == DisplayStyle.Flex) entering.Add(item);
        }
        // The confirm button comes in behind the cards on a shorter slide. Everything
        // that lands is heard; nothing is heard while travelling. The sequence reads
        // click, click, click, settle.
        int cardCount = entering.Count;
        if (confirmButton != null) entering.Add(confirmButton);

        for (int i = 0; i < entering.Count; i++)
        {
            var element = entering[i];
            bool isCard = i < cardCount;
            element.RemoveFromClassList(PUNCH_CLASS);
            element.RemoveFromClassList(CONFIRM_FLASH_CLASS);
            // .card-enter kills the transition as well as offsetting, so parking a card
            // that is already at rest (re-opening the menu) snaps instead of sliding down first.
            element.RemoveFromClassList(CARD_SLIDING_CLASS);
            element.RemoveFromClassList(CONFIRM_SLIDING_CLASS);
            element.AddToClassList(CARD_ENTER_CLASS);

            string slidingClass = isCard ? CARD_SLIDING_CLASS : CONFIRM_SLIDING_CLASS;
            int travel = isCard ? ENTRANCE_SLIDE_MS : CONFIRM_SLIDE_MS;
            int startDelay = ENTRANCE_LEAD_IN_MS + i * ENTRANCE_STAGGER_MS;

            element.schedule.Execute(() =>
            {
                element.AddToClassList(slidingClass);
                element.RemoveFromClassList(CARD_ENTER_CLASS);
            }).StartingIn(startDelay);

            element.schedule.Execute(() =>
            {
                element.RemoveFromClassList(slidingClass);
            }).StartingIn(startDelay + travel);
        }
    }

    // Attempts to cache references to the player GameObjects and components once
    private void TryCachePlayerRefs()
    {
        if (_leftPlayerGO == null)
        {
            _leftPlayerGO = GameObject.FindWithTag("PlayerLeft");
            if (_leftPlayerGO != null)
            {
                _leftHealth = _leftPlayerGO.GetComponent<PlayerHealth>();
                _leftSpecial = _leftPlayerGO.GetComponent<PlayerSpecial>();
            }
        }
        if (_rightPlayerGO == null)
        {
            _rightPlayerGO = GameObject.FindWithTag("PlayerRight");
            if (_rightPlayerGO != null)
            {
                _rightHealth = _rightPlayerGO.GetComponent<PlayerHealth>();
                _rightSpecial = _rightPlayerGO.GetComponent<PlayerSpecial>();
            }
        }
    }

    // Convenience: read stats from scene components and update status panels
    public void RefreshStatusFromScene()
    {
        TryCachePlayerRefs();
        RefreshAuras();

        // Left knight
        if (_leftHealth != null && _leftSpecial != null)
        {
            var leftUpgrades = UpgradeNamesProvider != null ? UpgradeNamesProvider(KnightTarget.LeftKnight) : _leftUpgradesCache;
            SetLeftKnightStatus(_leftHealth.CurrentHealth, _leftHealth.MaxHealth,
                                _leftSpecial.CurrentSpecial, _leftSpecial.MaxSpecial,
                                leftUpgrades);
        }

        // Right knight
        if (_rightHealth != null && _rightSpecial != null)
        {
            var rightUpgrades = UpgradeNamesProvider != null ? UpgradeNamesProvider(KnightTarget.RightKnight) : _rightUpgradesCache;
            SetRightKnightStatus(_rightHealth.CurrentHealth, _rightHealth.MaxHealth,
                                 _rightSpecial.CurrentSpecial, _rightSpecial.MaxSpecial,
                                 rightUpgrades);
        }
    }
    
    private void PopulateUpgrades()
    {
        if (upgradeManager == null) return;
        
    currentUpgrades = upgradeManager.GetRandomUpgrades() ?? new List<BaseUpgrade>();
        
        // Update menu items with upgrade information
        for (int i = 0; i < menuItems.Count; i++)
        {
            if (i < currentUpgrades.Count)
            {
                BaseUpgrade upgrade = currentUpgrades[i];
                
                // Get the child labels
                var titleLabel = menuItems[i].Q<Label>("upgrade-title");
                var descriptionLabel = menuItems[i].Q<Label>("upgrade-description");
                var rarityLabel = menuItems[i].Q<Label>("upgrade-rarity");
                var orderLabel = menuItems[i].Q<Label>("upgrade-order");

                // Update the text content
                if (titleLabel != null) titleLabel.text = upgrade.UpgradeName;
                if (descriptionLabel != null) descriptionLabel.text = upgrade.Description;
                if (rarityLabel != null) rarityLabel.text = upgrade.Rarity.ToString().ToUpperInvariant();
                if (orderLabel != null) orderLabel.text = upgrade.Order.ToString().ToUpperInvariant();

                PopulateChainRow(menuItems[i], upgrade);
                PopulateStatBadges(menuItems[i], upgrade);

                menuItems[i].style.display = DisplayStyle.Flex;

                // Add rarity styling
                menuItems[i].RemoveFromClassList("common");
                menuItems[i].RemoveFromClassList("rare");
                menuItems[i].RemoveFromClassList("epic");
                menuItems[i].RemoveFromClassList("legendary");
                menuItems[i].AddToClassList(upgrade.Rarity.ToString().ToLower());

                // Order styling (class accent color + badge visibility)
                foreach (UpgradeOrder o in System.Enum.GetValues(typeof(UpgradeOrder)))
                {
                    menuItems[i].RemoveFromClassList($"order--{o.ToString().ToLowerInvariant()}");
                }
                menuItems[i].AddToClassList($"order--{upgrade.Order.ToString().ToLowerInvariant()}");
            }
            else
            {
                menuItems[i].style.display = DisplayStyle.None;
            }
        }
    }

    // Fill a card's chain-progress footer: one pip per step in the upgrade's chain,
    // marking steps the target knight owns, the step on offer, and steps still ahead.
    private void PopulateChainRow(VisualElement card, BaseUpgrade upgrade)
    {
        var chainRow = card.Q<VisualElement>("chain-row");
        if (chainRow == null) return;

        var info = upgradeManager.GetChainInfo(upgrade, selectedKnight);
        if (info.Length <= 1)
        {
            // Standalone upgrades (no chain) don't need a progress footer
            chainRow.style.display = DisplayStyle.None;
            return;
        }
        chainRow.style.display = DisplayStyle.Flex;

        var pips = chainRow.Q<VisualElement>("chain-pips");
        if (pips != null)
        {
            pips.Clear();
            for (int step = 1; step <= info.Length; step++)
            {
                var pip = new VisualElement();
                pip.AddToClassList("pip");
                if (step == info.Position) pip.AddToClassList("pip--offered");
                else if (step <= info.OwnedCount) pip.AddToClassList("pip--owned");
                else pip.AddToClassList("pip--future");
                pips.Add(pip);
            }
        }

        var chainLabel = chainRow.Q<Label>("chain-label");
        if (chainLabel != null)
        {
            chainLabel.text = $"{info.ChainName.ToUpperInvariant()} · {NumberConverter.ToRoman(info.Position)} OF {NumberConverter.ToRoman(info.Length)}";
        }
    }

    // Fill a card's stat-badge column: one arrow badge per structured stat on the
    // upgrade (green = buff, red = downside).
    private void PopulateStatBadges(VisualElement card, BaseUpgrade upgrade)
    {
        var container = card.Q<VisualElement>("stat-badges");
        if (container == null) return;

        container.Clear();
        if (upgrade.Stats.Count == 0)
        {
            container.style.display = DisplayStyle.None;
            return;
        }
        container.style.display = DisplayStyle.Flex;

        foreach (var stat in upgrade.Stats)
        {
            string arrow = stat.isPositive ? "▲" : "▼";
            string text = string.IsNullOrEmpty(stat.labelText)
                ? $"{arrow} {stat.valueText}"
                : $"{arrow} {stat.valueText} {stat.labelText}";
            var badge = new Label(text);
            badge.AddToClassList("stat-badge");
            badge.AddToClassList(stat.isPositive ? "stat-badge--buff" : "stat-badge--bane");
            container.Add(badge);
        }
    }

    // Public APIs to update status panels
    public void SetLeftKnightStatus(float currentHealth, float maxHealth, float currentSpecial, float maxSpecial, IEnumerable<string> upgrades)
    {
    _leftCurHP = currentHealth; _leftMaxHP = maxHealth; _leftCurSP = currentSpecial; _leftMaxSP = maxSpecial;
    _leftUpgradesCache = upgrades != null ? new List<string>(upgrades) : new List<string>();
    if (leftHealthLabel != null) leftHealthLabel.text = $"Health: {Mathf.RoundToInt(_leftCurHP)} / {Mathf.RoundToInt(_leftMaxHP)}";
    if (leftSpecialLabel != null) leftSpecialLabel.text = $"Special: {Mathf.RoundToInt(_leftCurSP)} / {Mathf.RoundToInt(_leftMaxSP)}";
    PopulateColumn(_leftColumn, KnightTarget.LeftKnight);
    }

    public void SetRightKnightStatus(float currentHealth, float maxHealth, float currentSpecial, float maxSpecial, IEnumerable<string> upgrades)
    {
    _rightCurHP = currentHealth; _rightMaxHP = maxHealth; _rightCurSP = currentSpecial; _rightMaxSP = maxSpecial;
    _rightUpgradesCache = upgrades != null ? new List<string>(upgrades) : new List<string>();
    if (rightHealthLabel != null) rightHealthLabel.text = $"Health: {Mathf.RoundToInt(_rightCurHP)} / {Mathf.RoundToInt(_rightMaxHP)}";
    if (rightSpecialLabel != null) rightSpecialLabel.text = $"Special: {Mathf.RoundToInt(_rightCurSP)} / {Mathf.RoundToInt(_rightMaxSP)}";
    PopulateColumn(_rightColumn, KnightTarget.RightKnight);
    }

    private void BindColumn(LoadoutColumn col, string side)
    {
        col.Scroll = root.Q<ScrollView>($"{side}-upgrades-scroll");
        col.List = root.Q<VisualElement>($"{side}-upgrades");
        col.More = root.Q<Label>($"{side}-upgrades-more");
        col.DetailName = root.Q<Label>($"detail-{side}-name");
        col.DetailDesc = root.Q<Label>($"detail-{side}-desc");
        col.DetailStats = root.Q<VisualElement>($"detail-{side}-stats");
    }

    // One chip per owned chain, highest tier only - GetAppliedUpgradeSummary
    // already collapses the chain, so this list never shows a rank and its own
    // predecessor. The upgrade asset comes back with the row, so the detail
    // readout below never has to look anything up again.
    private void PopulateColumn(LoadoutColumn col, KnightTarget target)
    {
        col.Rows.Clear();
        col.Upgrades.Clear();
        col.Index = 0;

        if (col.List == null) return;
        col.List.Clear();

        if (upgradeManager != null)
        {
            foreach (var row in upgradeManager.GetAppliedUpgradeSummary(target))
            {
                var chip = new Label(row.Name);
                chip.AddToClassList("loadout-chip");
                string orderClass = OrderClass(row.Order);
                if (orderClass != null) chip.AddToClassList(orderClass);

                var owner = col;
                int index = col.Rows.Count;
                chip.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    if (PauseMenu.IsPaused || _confirmPending) return;
                    _column = owner == _leftColumn ? Column.Left : Column.Right;
                    SelectRow(owner, index);
                    UpdateSelection();
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

        if (col.Scroll != null) col.Scroll.scrollOffset = Vector2.zero;

        ClearDetail(col);
        RefreshMoreArrow(col);
    }

    private void RefreshMoreArrow(LoadoutColumn col)
    {
        if (col.More == null) return;
        var scroller = col.Scroll?.verticalScroller;
        bool overflows = scroller != null && scroller.highValue > 1f;
        bool atEnd = !overflows || col.Scroll.scrollOffset.y >= scroller.highValue - 1f;
        col.More.style.display = overflows && !atEnd ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void SelectRow(LoadoutColumn col, int index)
    {
        if (index < 0 || index >= col.Rows.Count) return;

        col.Index = index;
        ClearDetail(_leftColumn);
        ClearDetail(_rightColumn);
        ShowDetail(col, index);
        col.Scroll?.ScrollTo(col.Rows[index]);
        RefreshMoreArrow(col);
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
        if (col.DetailDesc != null) col.DetailDesc.text = upgrade.Description;
        if (col.DetailStats == null) return;

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
        if (col.DetailDesc != null) col.DetailDesc.text = string.Empty;
        col.DetailStats?.Clear();
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
            default: return null;
        }
    }

    private static void SetOrderClass(VisualElement element, UpgradeOrder order)
    {
        if (element == null) return;
        foreach (var cls in AllOrderClasses) element.RemoveFromClassList(cls);
        string wanted = OrderClass(order);
        if (wanted != null) element.AddToClassList(wanted);
    }

    private void ClearStatusLists()
    {
        _leftColumn.List?.Clear();
        _rightColumn.List?.Clear();
        _leftColumn.Rows.Clear();
        _rightColumn.Rows.Clear();
        _leftColumn.Upgrades.Clear();
        _rightColumn.Upgrades.Clear();
        ClearDetail(_leftColumn);
        ClearDetail(_rightColumn);
    }

    private void ApplyStatusToUI()
    {
        if (leftHealthLabel != null)
            leftHealthLabel.text = $"Health: {Mathf.RoundToInt(_leftCurHP)} / {Mathf.RoundToInt(_leftMaxHP)}";
        if (leftSpecialLabel != null)
            leftSpecialLabel.text = $"Special: {Mathf.RoundToInt(_leftCurSP)} / {Mathf.RoundToInt(_leftMaxSP)}";
        PopulateColumn(_leftColumn, KnightTarget.LeftKnight);

        if (rightHealthLabel != null)
            rightHealthLabel.text = $"Health: {Mathf.RoundToInt(_rightCurHP)} / {Mathf.RoundToInt(_rightMaxHP)}";
        if (rightSpecialLabel != null)
            rightSpecialLabel.text = $"Special: {Mathf.RoundToInt(_rightCurSP)} / {Mathf.RoundToInt(_rightMaxSP)}";
        PopulateColumn(_rightColumn, KnightTarget.RightKnight);
    }
    
    // Public method to set which item is selected
    public void SetSelectedIndex(int index)
    {
        if (index >= 0 && index < menuItems.Count)
        {
            currentSelectedIndex = index;
            isConfirmButtonSelected = false;
            UpdateSelection();
        }
    }
    
    // Get the currently selected upgrade
    public BaseUpgrade GetSelectedUpgrade()
    {
        if (currentUpgrades != null && currentSelectedIndex >= 0 && currentSelectedIndex < currentUpgrades.Count)
        {
            return currentUpgrades[currentSelectedIndex];
        }
        return null;
    }
    
    // Get the chosen upgrade for confirmation
    public BaseUpgrade GetChosenUpgrade()
    {
        if (currentUpgrades != null && chosenUpgradeIndex >= 0 && chosenUpgradeIndex < currentUpgrades.Count)
        {
            return currentUpgrades[chosenUpgradeIndex];
        }
        return null;
    }

    // Set which knight is receiving the upgrade for this menu instance
    public void SetKnightTargetForThisUpgrade(KnightTarget knight)
    {
        selectedKnight = knight;
        // If nothing is chosen yet, reflect the prompt
        UpdateSelectionStatusPrompt();
    }

    // Convenience: set target knight by wave number (1-based): odd -> Left, even -> Right
    public void SetWaveNumber(int waveNumber)
    {
        if (waveNumber <= 0) waveNumber = 1;
        selectedKnight = (waveNumber % 2 == 1) ? KnightTarget.LeftKnight : KnightTarget.RightKnight;
        UpdateSelectionStatusPrompt();
    }

    // The wave the draft is paying FOR: the NEXT one, not the one just cleared
    // (owner's call, 2026-09-06). The menu is a loadout screen — every card on it
    // is a choice about the fight that has not happened yet — and showing the
    // number of the wave already behind you read as a scoreboard. CompletedWavesCount
    // is the cleared count by the time this opens, so the wave about to start is
    // one past it.
    private void UpdateWaveNumberLine()
    {
        if (waveNumberLabel == null) return;
        int cleared = waveManager != null ? waveManager.CompletedWavesCount : 0;
        waveNumberLabel.text = $"WAVE {Mathf.Max(1, cleared + 1)}";
    }

    private void UpdateSelectionStatusPrompt()
    {
        if (selectionStatusLabel == null) return;
        if (chosenUpgradeIndex >= 0 && currentUpgrades != null && chosenUpgradeIndex < currentUpgrades.Count)
        {
            string chosenKnightName = KnightNames.For(chosenKnight);
            selectionStatusLabel.text = $"{currentUpgrades[chosenUpgradeIndex].UpgradeName} will be applied to {chosenKnightName}";
        }
        else
        {
            string knightName = KnightNames.For(selectedKnight);
            selectionStatusLabel.text = $"Upgrade {knightName}";
        }
    }
}