using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System;

public class UpgradeMenu : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private StyleSheet styleSheet;
    
    [Header("Input Settings")]
    [SerializeField] private float inputDeadzone = 0.5f;
    [SerializeField] private float inputCooldown = 0.3f;
    
    private VisualElement root;
    private List<Button> menuItems;
    private int currentSelectedIndex = 0;
    private float lastInputTime = 0f;
    
    private const string SELECTED_CLASS = "menu-item--selected";
    
    // Event for when an upgrade is selected
    public event System.Action<int> OnUpgradeSelected;
    
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
        
        // Find menu items
        menuItems = new List<Button>();
        var itemOne = root.Q<Button>("item-one");
        var itemTwo = root.Q<Button>("item-two");
        var itemThree = root.Q<Button>("item-three");
        
        if (itemOne != null) menuItems.Add(itemOne);
        if (itemTwo != null) menuItems.Add(itemTwo);
        if (itemThree != null) menuItems.Add(itemThree);
        
        if (menuItems.Count == 0) return;
        
        // Setup click handlers
        for (int i = 0; i < menuItems.Count; i++)
        {
            int index = i; // Capture for closure
            menuItems[i].clicked += () => SelectUpgrade(index);
        }
        
        // Set initial selection
        UpdateSelection();
    }
    
    void Update()
    {
        // Only handle input when menu is visible
        if (root != null && root.style.display == DisplayStyle.Flex)
        {
            HandleInput();
        }
    }
    
    void HandleInput()
    {
        if (menuItems.Count == 0) return;
        
        // Check if enough time has passed since last input
        if (Time.unscaledTime - lastInputTime < inputCooldown) return;
        
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
        
        // Handle action button (Submit/Fire1)
        if (Input.GetButtonDown("Submit") || Input.GetButtonDown("Fire1"))
        {
            SelectUpgrade(currentSelectedIndex);
        }
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
        
        // Trigger the upgrade selected event
        OnUpgradeSelected?.Invoke(index);
    }
    
    // Public method to show/hide the menu
    public void SetMenuVisible(bool visible)
    {
        if (root != null)
        {
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            
            // Reset selection to first item when showing menu
            if (visible)
            {
                currentSelectedIndex = 0;
                UpdateSelection();
                
                // Force refresh the UI
                root.MarkDirtyRepaint();
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
    
    // Method to update menu item text (for future upgrade content)
    public void SetMenuItemText(int index, string text)
    {
        if (index >= 0 && index < menuItems.Count)
        {
            menuItems[index].text = text;
        }
    }
}