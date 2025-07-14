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
    
    [Header("Knight Selection Input System")]
    [SerializeField] private InputActionReference leftKnightSelectAction;
    [SerializeField] private InputActionReference rightKnightSelectAction;
    
    [Header("Input Settings")]
    [SerializeField] private float inputDeadzone = 0.5f;
    [SerializeField] private float inputCooldown = 0.3f;
    
    private VisualElement root;
    private List<VisualElement> menuItems;
    private List<BaseUpgrade> currentUpgrades;
    private int currentSelectedIndex = 0;
    private float lastInputTime = 0f;
    private KnightTarget selectedKnight = KnightTarget.LeftKnight;
    
    private const string SELECTED_CLASS = "menu-item--selected";
    
    // Event for when an upgrade is selected with knight target
    public event System.Action<int, KnightTarget> OnUpgradeSelected;
    
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
        // Enable knight selection input actions
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
    }
    
    private void OnDisable()
    {
        // Disable knight selection input actions
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
    }
    
    private void OnLeftKnightSelect(InputAction.CallbackContext context)
    {
        if (IsMenuVisible() && CanProcessInput())
        {
            selectedKnight = KnightTarget.LeftKnight;
            SelectUpgrade(currentSelectedIndex);
            lastInputTime = Time.unscaledTime;
        }
    }
    
    private void OnRightKnightSelect(InputAction.CallbackContext context)
    {
        if (IsMenuVisible() && CanProcessInput())
        {
            selectedKnight = KnightTarget.RightKnight;
            SelectUpgrade(currentSelectedIndex);
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
        
        // Find menu items (upgrade containers, not the rest button)
        menuItems = new List<VisualElement>();
        var itemOne = root.Q<VisualElement>("item-one");
        var itemTwo = root.Q<VisualElement>("item-two");
        var itemThree = root.Q<VisualElement>("item-three");

        if (itemOne != null) menuItems.Add(itemOne);
        if (itemTwo != null) menuItems.Add(itemTwo);
        if (itemThree != null) menuItems.Add(itemThree);

        if (menuItems.Count == 0) return;
        
        // Setup click handlers for the upgrade items
        for (int i = 0; i < menuItems.Count; i++)
        {
            int index = i; // Capture for closure
            menuItems[i].RegisterCallback<ClickEvent>(_ => SelectUpgrade(index));
        }
        
        // Set initial selection
        UpdateSelection();
    }
    
    void Update()
    {
        // Only handle input when menu is visible
        if (IsMenuVisible())
        {
            HandleInput();
        }
    }
    
    void HandleInput()
    {
        if (menuItems.Count == 0 || !CanProcessInput()) return;
        
        // Get left joystick vertical input (or keyboard fallback)
        // Use unscaled time for input to work during pause
        float verticalInput = Input.GetAxis("Vertical");
        
        // Handle vertical navigation with deadzone
        // Note: Positive vertical input = up, negative = down
        if (Mathf.Abs(verticalInput) > inputDeadzone)
        {
            if (verticalInput > inputDeadzone) // Up - move to previous item
            {
                NavigateUp();
                lastInputTime = Time.unscaledTime;
            }
            else if (verticalInput < -inputDeadzone) // Down - move to next item
            {
                NavigateDown();
                lastInputTime = Time.unscaledTime;
            }
        }
        
        // Handle knight selection with legacy input as fallback (if InputActionReferences not assigned)
        if (leftKnightSelectAction == null && (Input.GetButtonDown("LeftShoot") || Input.GetButtonDown("LeftSpecial")))
        {
            selectedKnight = KnightTarget.LeftKnight;
            SelectUpgrade(currentSelectedIndex);
            lastInputTime = Time.unscaledTime;
        }
        else if (rightKnightSelectAction == null && (Input.GetButtonDown("RightShoot") || Input.GetButtonDown("RightSpecial")))
        {
            selectedKnight = KnightTarget.RightKnight;
            SelectUpgrade(currentSelectedIndex);
            lastInputTime = Time.unscaledTime;
        }
        
        // Keep legacy selection for keyboard/other input
        if (Input.GetButtonDown("Submit") || Input.GetButtonDown("Fire1"))
        {
            SelectUpgrade(currentSelectedIndex);
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
    
    void NavigateUp()
    {
        currentSelectedIndex = (currentSelectedIndex - 1 + menuItems.Count) % menuItems.Count;
        UpdateSelection();
    }
    
    void NavigateDown()
    {
        currentSelectedIndex = (currentSelectedIndex + 1) % menuItems.Count;
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
        
        // Add selected class to current item
        if (currentSelectedIndex >= 0 && currentSelectedIndex < menuItems.Count)
        {
            menuItems[currentSelectedIndex].AddToClassList(SELECTED_CLASS);
        }
    }
    
    void SelectUpgrade(int index)
    {
        if (index < 0 || index >= menuItems.Count) return;
        
        // Trigger the upgrade selected event with knight target
        OnUpgradeSelected?.Invoke(index, selectedKnight);
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
                selectedKnight = KnightTarget.LeftKnight;
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
}