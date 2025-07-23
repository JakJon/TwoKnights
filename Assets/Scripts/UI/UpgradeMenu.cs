using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System;

public class UpgradeMenu : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private StyleSheet styleSheet;
    
    [Header("Upgrade System")]
    [SerializeField] private UpgradeManager upgradeManager;
    
    [Header("D-Pad Navigation Input Actions")]
    [SerializeField] private InputActionReference dpadLeftAction;
    [SerializeField] private InputActionReference dpadRightAction;
    [SerializeField] private InputActionReference dpadUpAction;
    [SerializeField] private InputActionReference dpadDownAction;
    
    [Header("Knight Selection Input Actions")]
    [SerializeField] private InputActionReference leftKnightSelectAction;
    [SerializeField] private InputActionReference rightKnightSelectAction;
    [SerializeField] private InputActionReference confirmAction;
    
    [Header("Input Settings")]
    [SerializeField] private float inputCooldown = 0.2f;
    
    private VisualElement root;
    private List<VisualElement> menuItems;
    private Button confirmButton;
    private Label selectionStatusLabel;
    private List<BaseUpgrade> currentUpgrades;
    private int currentSelectedIndex = 0;
    private bool isConfirmButtonSelected = false;
    private float lastInputTime = 0f;
    private KnightTarget selectedKnight = KnightTarget.LeftKnight;
    private int chosenUpgradeIndex = -1;
    private KnightTarget chosenKnight = KnightTarget.LeftKnight;
    
    private const string SELECTED_CLASS = "menu-item--selected";
    private const string CHOSEN_CLASS = "menu-item--chosen";
    
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
        // Enable D-pad navigation actions
        if (dpadLeftAction != null)
        {
            dpadLeftAction.action.performed += OnDPadLeft;
            dpadLeftAction.action.Enable();
        }
        
        if (dpadRightAction != null)
        {
            dpadRightAction.action.performed += OnDPadRight;
            dpadRightAction.action.Enable();
        }
        
        if (dpadUpAction != null)
        {
            dpadUpAction.action.performed += OnDPadUp;
            dpadUpAction.action.Enable();
        }
        
        if (dpadDownAction != null)
        {
            dpadDownAction.action.performed += OnDPadDown;
            dpadDownAction.action.Enable();
        }
        
        // Enable knight selection actions
        if (leftKnightSelectAction != null)
        {
            leftKnightSelectAction.action.performed += OnLeftKnightSelect;
            leftKnightSelectAction.action.Enable();
        }
        
        if (rightKnightSelectAction != null)
        {
            rightKnightSelectAction.action.performed += OnRightKnightSelect;
            rightKnightSelectAction.action.Enable();
        }
        
        if (confirmAction != null)
        {
            confirmAction.action.performed += OnConfirmSelect;
            confirmAction.action.Enable();
        }
    }
    
    private void OnDisable()
    {
        // Disable D-pad navigation actions
        if (dpadLeftAction != null)
        {
            dpadLeftAction.action.performed -= OnDPadLeft;
            dpadLeftAction.action.Disable();
        }
        
        if (dpadRightAction != null)
        {
            dpadRightAction.action.performed -= OnDPadRight;
            dpadRightAction.action.Disable();
        }
        
        if (dpadUpAction != null)
        {
            dpadUpAction.action.performed -= OnDPadUp;
            dpadUpAction.action.Disable();
        }
        
        if (dpadDownAction != null)
        {
            dpadDownAction.action.performed -= OnDPadDown;
            dpadDownAction.action.Disable();
        }
        
        // Disable knight selection actions
        if (leftKnightSelectAction != null)
        {
            leftKnightSelectAction.action.performed -= OnLeftKnightSelect;
            leftKnightSelectAction.action.Disable();
        }
        
        if (rightKnightSelectAction != null)
        {
            rightKnightSelectAction.action.performed -= OnRightKnightSelect;
            rightKnightSelectAction.action.Disable();
        }
        
        if (confirmAction != null)
        {
            confirmAction.action.performed -= OnConfirmSelect;
            confirmAction.action.Disable();
        }
    }
    
    // D-pad navigation input callbacks
    private void OnDPadLeft(InputAction.CallbackContext context)
    {
        if (IsMenuVisible() && CanProcessInput())
        {
            NavigateLeft();
            lastInputTime = Time.unscaledTime;
        }
    }
    
    private void OnDPadRight(InputAction.CallbackContext context)
    {
        if (IsMenuVisible() && CanProcessInput())
        {
            NavigateRight();
            lastInputTime = Time.unscaledTime;
        }
    }
    
    private void OnDPadUp(InputAction.CallbackContext context)
    {
        if (IsMenuVisible() && CanProcessInput())
        {
            NavigateUp();
            lastInputTime = Time.unscaledTime;
        }
    }
    
    private void OnDPadDown(InputAction.CallbackContext context)
    {
        if (IsMenuVisible() && CanProcessInput())
        {
            NavigateDown();
            lastInputTime = Time.unscaledTime;
        }
    }
    
    // Knight selection input callbacks
    private void OnLeftKnightSelect(InputAction.CallbackContext context)
    {
        if (IsMenuVisible() && CanProcessInput() && IsUpgradeSelected())
        {
            SelectUpgradeForKnight(currentSelectedIndex, KnightTarget.LeftKnight);
            lastInputTime = Time.unscaledTime;
        }
    }
    
    private void OnRightKnightSelect(InputAction.CallbackContext context)
    {
        if (IsMenuVisible() && CanProcessInput() && IsUpgradeSelected())
        {
            SelectUpgradeForKnight(currentSelectedIndex, KnightTarget.RightKnight);
            lastInputTime = Time.unscaledTime;
        }
    }
    
    private void OnConfirmSelect(InputAction.CallbackContext context)
    {
        if (IsMenuVisible() && CanProcessInput() && isConfirmButtonSelected && chosenUpgradeIndex >= 0)
        {
            ConfirmUpgrade();
            lastInputTime = Time.unscaledTime;
        }
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

        if (menuItems.Count == 0) return;
        
        // Setup click handlers for the upgrade items
        for (int i = 0; i < menuItems.Count; i++)
        {
            int index = i; // Capture for closure
            menuItems[i].RegisterCallback<ClickEvent>(_ => {
                currentSelectedIndex = index;
                isConfirmButtonSelected = false;
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
        
        // Handle D-pad/keyboard horizontal navigation (left/right through upgrade items)
        if (!inputProcessedThisFrame && dpadLeftAction == null && dpadRightAction == null)
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
        
        // Handle D-pad/keyboard vertical navigation (up/down between upgrade items and confirm button)
        if (!inputProcessedThisFrame && dpadUpAction == null && dpadDownAction == null)
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
        
        // Handle knight selection with legacy input as fallback (if InputActionReferences not assigned)
        if (leftKnightSelectAction == null && (Input.GetButtonDown("LeftShoot") || Input.GetButtonDown("LeftSpecial")))
        {
            if (IsUpgradeSelected())
            {
                SelectUpgradeForKnight(currentSelectedIndex, KnightTarget.LeftKnight);
                lastInputTime = Time.unscaledTime;
            }
        }
        else if (rightKnightSelectAction == null && (Input.GetButtonDown("RightShoot") || Input.GetButtonDown("RightSpecial")))
        {
            if (IsUpgradeSelected())
            {
                SelectUpgradeForKnight(currentSelectedIndex, KnightTarget.RightKnight);
                lastInputTime = Time.unscaledTime;
            }
        }
        
        // Handle confirmation with legacy input - only if confirm button is selected
        if (confirmAction == null && (Input.GetButtonDown("Submit") || Input.GetButtonDown("Fire1")))
        {
            if (isConfirmButtonSelected && chosenUpgradeIndex >= 0)
            {
                ConfirmUpgrade();
                lastInputTime = Time.unscaledTime;
            }
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
    
    void NavigateLeft()
    {
        if (!isConfirmButtonSelected)
        {
            // Navigate through upgrade items: 0 <- 1 <- 2
            currentSelectedIndex = (currentSelectedIndex - 1 + menuItems.Count) % menuItems.Count;
            UpdateSelection();
        }
    }
    
    void NavigateRight()
    {
        if (!isConfirmButtonSelected)
        {
            // Navigate through upgrade items: 0 -> 1 -> 2
            currentSelectedIndex = (currentSelectedIndex + 1) % menuItems.Count;
            UpdateSelection();
        }
    }
    
    void NavigateUp()
    {
        if (isConfirmButtonSelected)
        {
            // Move from confirm button to second upgrade item (index 1)
            isConfirmButtonSelected = false;
            currentSelectedIndex = 1;
        }
        else
        {
            // Move from any upgrade item to confirm button
            isConfirmButtonSelected = true;
        }
        UpdateSelection();
    }
    
    void NavigateDown()
    {
        if (isConfirmButtonSelected)
        {
            // Move from confirm button to second upgrade item (index 1)
            isConfirmButtonSelected = false;
            currentSelectedIndex = 1;
        }
        else
        {
            // Move from any upgrade item to confirm button
            isConfirmButtonSelected = true;
        }
        UpdateSelection();
    }
    
    void UpdateSelection()
    {
        if (menuItems.Count == 0) return;
        
        // Remove selected class from all items
        for (int i = 0; i < menuItems.Count; i++)
        {
            menuItems[i].RemoveFromClassList(SELECTED_CLASS);
        }
        
        // Remove selected class from confirm button
        if (confirmButton != null)
        {
            confirmButton.RemoveFromClassList(SELECTED_CLASS);
        }
        
        if (isConfirmButtonSelected)
        {
            // Highlight confirm button
            if (confirmButton != null)
            {
                confirmButton.AddToClassList(SELECTED_CLASS);
            }
        }
        else
        {
            // Highlight current upgrade item
            if (currentSelectedIndex >= 0 && currentSelectedIndex < menuItems.Count)
            {
                menuItems[currentSelectedIndex].AddToClassList(SELECTED_CLASS);
            }
        }
    }
    
    void SelectUpgradeForKnight(int upgradeIndex, KnightTarget knight)
    {
        if (upgradeIndex < 0 || upgradeIndex >= menuItems.Count || upgradeIndex >= currentUpgrades.Count) return;

        // Debug logging to track selection changes
        Debug.Log($"Selecting upgrade {upgradeIndex} for {knight}. Previous selection: upgrade {chosenUpgradeIndex} for {chosenKnight}");

        // Clear previous selection - this should remove visual styling from any previously chosen upgrade
        ClearChosenUpgrade();

        // Set new selection
        chosenUpgradeIndex = upgradeIndex;
        chosenKnight = knight;

        // Update visual state for the newly chosen upgrade
        menuItems[upgradeIndex].AddToClassList(CHOSEN_CLASS);

        // Update status text
        if (selectionStatusLabel != null && currentUpgrades != null && upgradeIndex < currentUpgrades.Count)
        {
            string knightName = knight == KnightTarget.LeftKnight ? "Left Knight" : "Right Knight";
            selectionStatusLabel.text = $"{currentUpgrades[upgradeIndex].UpgradeName} will be applied to {knightName}";
        }

        Debug.Log($"Selection updated: upgrade {chosenUpgradeIndex} for {chosenKnight}");
    }
    
    void ClearChosenUpgrade()
    {
        // Clear visual styling from all menu items to ensure no lingering chosen state
        for (int i = 0; i < menuItems.Count; i++)
        {
            menuItems[i].RemoveFromClassList(CHOSEN_CLASS);
        }
        
        // Reset the chosen state
        chosenUpgradeIndex = -1;
        

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
                selectedKnight = KnightTarget.LeftKnight;
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
        
        currentUpgrades = upgradeManager.GetRandomUpgrades();
        
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
}