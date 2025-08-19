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
    private List<BaseUpgrade> currentUpgrades;
    private int currentSelectedIndex = 0;
    private bool isConfirmButtonSelected = false;
    private float lastInputTime = 0f;
    private KnightTarget selectedKnight = KnightTarget.LeftKnight; // Target knight for this upgrade instance
    private int chosenUpgradeIndex = -1;
    private KnightTarget chosenKnight = KnightTarget.LeftKnight;
    
    private const string SELECTED_CLASS = "menu-item--selected";
    private const string CHOSEN_CLASS = "menu-item--chosen";

    // Status panel UI refs
    private Label leftHealthLabel;
    private Label leftSpecialLabel;
    private ScrollView leftUpgradesList;
    private Label rightHealthLabel;
    private Label rightSpecialLabel;
    private ScrollView rightUpgradesList;

    // Cached status data (applied when UI is ready)
    private float _leftCurHP, _leftMaxHP, _leftCurSP, _leftMaxSP;
    private List<string> _leftUpgradesCache = new List<string>();
    private float _rightCurHP, _rightMaxHP, _rightCurSP, _rightMaxSP;
    private List<string> _rightUpgradesCache = new List<string>();
    
    // Event for when an upgrade is confirmed
    public event System.Action<int, KnightTarget> OnUpgradeConfirmed;
    
    void Awake()
    {
        // Get UI Document component if not assigned
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();
    }
    
    void Start()
    {
        SetupUI();
        
        // Initially hide the menu
        SetMenuVisible(false);
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

    // Status panels
    leftHealthLabel = root.Q<Label>("left-health");
    leftSpecialLabel = root.Q<Label>("left-special");
    leftUpgradesList = root.Q<ScrollView>("left-upgrades");
    rightHealthLabel = root.Q<Label>("right-health");
    rightSpecialLabel = root.Q<Label>("right-special");
    rightUpgradesList = root.Q<ScrollView>("right-upgrades");

        if (menuItems.Count == 0) return;
        
        // Setup click handlers for the upgrade items
        for (int i = 0; i < menuItems.Count; i++)
        {
            int index = i; // Capture for closure
            menuItems[i].RegisterCallback<ClickEvent>(_ => {
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
        if (menuItems.Count == 0 || !CanProcessInput()) return;
        
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
    
    // Column layout navigation: map left/right to up/down
    void NavigateLeft() => NavigateUp();
    void NavigateRight() => NavigateDown();

    // Move selection up within items; if on confirm, jump back to last item
    void NavigateUp()
    {
        if (menuItems == null || menuItems.Count == 0) return;
        if (isConfirmButtonSelected)
        {
            isConfirmButtonSelected = false;
            currentSelectedIndex = Mathf.Clamp(menuItems.Count - 1, 0, menuItems.Count - 1);
        }
        else
        {
            currentSelectedIndex = Mathf.Max(0, currentSelectedIndex - 1);
        }
        UpdateSelection();
    }

    // Move selection down within items; from last item move to confirm; stay on confirm if already there
    void NavigateDown()
    {
        if (menuItems == null || menuItems.Count == 0) return;
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
        UpdateSelection();
    }

    private bool IsValidIndex(int index)
    {
        return menuItems != null && index >= 0 && index < menuItems.Count;
    }

    // Horizontal/toggle helpers removed in favor of explicit Up/Down logic for column layout
    
    void UpdateSelection()
    {
        if (menuItems == null || menuItems.Count == 0) return;
        
        // Remove selected class from all items
        for (int i = 0; i < menuItems.Count; i++) menuItems[i].RemoveFromClassList(SELECTED_CLASS);
        
        // Remove selected class from confirm button
        if (confirmButton != null) confirmButton.RemoveFromClassList(SELECTED_CLASS);
        
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
    
    void SelectUpgrade(int upgradeIndex)
    {
        if (currentUpgrades == null) return;
        if (upgradeIndex < 0 || upgradeIndex >= menuItems.Count || upgradeIndex >= currentUpgrades.Count) return;

        // Clear previous selection - this should remove visual styling from any previously chosen upgrade
        ClearChosenUpgrade();

        // Set new selection
        chosenUpgradeIndex = upgradeIndex;
        chosenKnight = selectedKnight; // Apply to the predetermined knight for this wave

        // Update visual state for the newly chosen upgrade
        menuItems[upgradeIndex].AddToClassList(CHOSEN_CLASS);

        // Update status text
        if (selectionStatusLabel != null && currentUpgrades != null && upgradeIndex < currentUpgrades.Count)
        {
            string knightName = selectedKnight == KnightTarget.LeftKnight ? "Left Knight" : "Right Knight";
            selectionStatusLabel.text = $"{currentUpgrades[upgradeIndex].UpgradeName} will be applied to {knightName}";
        }
    }
    
    void ClearChosenUpgrade()
    {
        // Clear visual styling from all menu items to ensure no lingering chosen state
        if (menuItems != null)
        {
            for (int i = 0; i < menuItems.Count; i++) menuItems[i].RemoveFromClassList(CHOSEN_CLASS);
        }
        
        // Reset the chosen state
        chosenUpgradeIndex = -1;
    // Update the status prompt to reflect current target knight
    UpdateSelectionStatusPrompt();

    }
    
    void ConfirmUpgrade()
    {
        if (chosenUpgradeIndex >= 0 && chosenUpgradeIndex < currentUpgrades.Count)
        {
            // Trigger the upgrade confirmed event
            OnUpgradeConfirmed?.Invoke(chosenUpgradeIndex, chosenKnight);
        }
    }
    
    // Public method to show/hide the menu and populate upgrades
    public void SetMenuVisible(bool visible)
    {
        if (root != null)
        {
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            
            // Reset selection and populate upgrades when showing menu
            if (visible)
            {
                currentSelectedIndex = 0;
                isConfirmButtonSelected = false;
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
                ClearChosenUpgrade();
                PopulateUpgrades();
                UpdateSelection();
                
                // Force refresh the UI
                root.MarkDirtyRepaint();
            }
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
                
                // Update the text content
                if (titleLabel != null) titleLabel.text = upgrade.UpgradeName;
                if (descriptionLabel != null) descriptionLabel.text = upgrade.Description;
                if (rarityLabel != null) rarityLabel.text = upgrade.Rarity.ToString();
                
                menuItems[i].style.display = DisplayStyle.Flex;
                
                // Add rarity styling
                menuItems[i].RemoveFromClassList("common");
                menuItems[i].RemoveFromClassList("rare");
                menuItems[i].RemoveFromClassList("epic");
                menuItems[i].RemoveFromClassList("legendary");
                menuItems[i].AddToClassList(upgrade.Rarity.ToString().ToLower());
            }
            else
            {
                menuItems[i].style.display = DisplayStyle.None;
            }
        }
    }

    // Public APIs to update status panels
    public void SetLeftKnightStatus(float currentHealth, float maxHealth, float currentSpecial, float maxSpecial, IEnumerable<string> upgrades)
    {
    _leftCurHP = currentHealth; _leftMaxHP = maxHealth; _leftCurSP = currentSpecial; _leftMaxSP = maxSpecial;
    _leftUpgradesCache = upgrades != null ? new List<string>(upgrades) : new List<string>();
    if (leftHealthLabel != null) leftHealthLabel.text = $"Health: {Mathf.RoundToInt(_leftCurHP)} / {Mathf.RoundToInt(_leftMaxHP)}";
    if (leftSpecialLabel != null) leftSpecialLabel.text = $"Special: {Mathf.RoundToInt(_leftCurSP)} / {Mathf.RoundToInt(_leftMaxSP)}";
    PopulateUpgradeList(leftUpgradesList, _leftUpgradesCache);
    }

    public void SetRightKnightStatus(float currentHealth, float maxHealth, float currentSpecial, float maxSpecial, IEnumerable<string> upgrades)
    {
    _rightCurHP = currentHealth; _rightMaxHP = maxHealth; _rightCurSP = currentSpecial; _rightMaxSP = maxSpecial;
    _rightUpgradesCache = upgrades != null ? new List<string>(upgrades) : new List<string>();
    if (rightHealthLabel != null) rightHealthLabel.text = $"Health: {Mathf.RoundToInt(_rightCurHP)} / {Mathf.RoundToInt(_rightMaxHP)}";
    if (rightSpecialLabel != null) rightSpecialLabel.text = $"Special: {Mathf.RoundToInt(_rightCurSP)} / {Mathf.RoundToInt(_rightMaxSP)}";
    PopulateUpgradeList(rightUpgradesList, _rightUpgradesCache);
    }

    private void PopulateUpgradeList(ScrollView list, IEnumerable<string> upgrades)
    {
        if (list == null) return;
        list.Clear();
        if (upgrades == null) return;
        foreach (var name in upgrades)
        {
            var item = new Label(name) { name = null };
            item.AddToClassList("stat-line");
            list.Add(item);
        }
    }

    private void ClearStatusLists()
    {
        leftUpgradesList?.Clear();
        rightUpgradesList?.Clear();
    }

    private void ApplyStatusToUI()
    {
        if (leftHealthLabel != null)
            leftHealthLabel.text = $"Health: {Mathf.RoundToInt(_leftCurHP)} / {Mathf.RoundToInt(_leftMaxHP)}";
        if (leftSpecialLabel != null)
            leftSpecialLabel.text = $"Special: {Mathf.RoundToInt(_leftCurSP)} / {Mathf.RoundToInt(_leftMaxSP)}";
        PopulateUpgradeList(leftUpgradesList, _leftUpgradesCache);

        if (rightHealthLabel != null)
            rightHealthLabel.text = $"Health: {Mathf.RoundToInt(_rightCurHP)} / {Mathf.RoundToInt(_rightMaxHP)}";
        if (rightSpecialLabel != null)
            rightSpecialLabel.text = $"Special: {Mathf.RoundToInt(_rightCurSP)} / {Mathf.RoundToInt(_rightMaxSP)}";
        PopulateUpgradeList(rightUpgradesList, _rightUpgradesCache);
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

    private void UpdateSelectionStatusPrompt()
    {
        if (selectionStatusLabel == null) return;
        if (chosenUpgradeIndex >= 0 && currentUpgrades != null && chosenUpgradeIndex < currentUpgrades.Count)
        {
            string chosenKnightName = chosenKnight == KnightTarget.LeftKnight ? "Left Knight" : "Right Knight";
            selectionStatusLabel.text = $"{currentUpgrades[chosenUpgradeIndex].UpgradeName} will be applied to {chosenKnightName}";
        }
        else
        {
            string knightName = selectedKnight == KnightTarget.LeftKnight ? "Left Knight" : "Right Knight";
            selectionStatusLabel.text = $"Upgrade {knightName}";
        }
    }
}