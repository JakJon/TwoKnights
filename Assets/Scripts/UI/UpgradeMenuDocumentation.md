# Upgrade Menu Integration Documentation

## Overview
The upgrade menu system pauses the game between waves and allows players to select upgrades using controller navigation.

## Files Created/Modified

### New Files:
- `Assets/Scripts/UI/UpgradeMenu.cs` - Main menu controller
- `Assets/UI/UpgradeMenu.uxml` - UI structure
- `Assets/UI/UpgradeMenu.uss` - UI styling
- `Assets/Scripts/UI/UpgradeMenuExample.cs` - Example usage

### Modified Files:
- `Assets/Scripts/Spawner.cs` - Wave management integration

## Setup Instructions

### 1. Create UI Document GameObject
1. In your scene, create a new GameObject
2. Add a `UIDocument` component
3. Assign the `UpgradeMenu.uxml` file to the Source Asset field
4. Add the `UpgradeMenu` component to the same GameObject
5. In the UpgradeMenu component, assign:
   - UI Document reference
   - Style Sheet (`UpgradeMenu.uss`)

### 2. Connect to Spawner
1. Find your Spawner GameObject in the scene
2. In the Spawner component, assign the UpgradeMenu GameObject to the "Upgrade Menu" field

### 3. Canvas Setup (if needed)
- The UI Toolkit system works with Screen Space - Overlay by default
- Ensure your UIDocument is on a GameObject with proper sorting order

## How It Works

### Wave Flow:
1. Wave spawns and runs normally
2. When wave completes, game automatically pauses (`Time.timeScale = 0`)
3. Upgrade menu becomes visible
4. Player navigates with left joystick (up/down)
5. Player selects upgrade with Submit/Fire1 button
6. Game resumes and next wave starts

### Controls:
- **Left Joystick Vertical** or **Arrow Keys**: Navigate menu items
- **Submit** or **Fire1**: Select current upgrade
- **U Key**: Test show/hide menu (in UpgradeMenuExample)
- **P Key**: Test pause toggle (in UpgradeMenuExample)

### Visual Feedback:
- Selected item has blue highlighting and scale effect
- Smooth transitions between selections
- Menu centers on screen

## Code Integration

### Event System:
```csharp
// Subscribe to upgrade selection
upgradeMenu.OnUpgradeSelected += OnUpgradeSelected;

// Handle upgrade selection
private void OnUpgradeSelected(int upgradeIndex)
{
    // Apply upgrade logic here
    Debug.Log($"Upgrade {upgradeIndex} selected!");
}
```

### Manual Control:
```csharp
// Show/hide menu
upgradeMenu.SetMenuVisible(true/false);

// Set selected item
upgradeMenu.SetSelectedIndex(0);

// Update menu text
upgradeMenu.SetMenuItemText(0, "Health Boost");
```

## Extending the System

### Adding Real Upgrades:
1. Create upgrade data structures
2. Replace placeholder menu items with actual upgrade options
3. Implement upgrade effects in the `OnUpgradeSelected` event
4. Update menu text dynamically based on available upgrades

### Customization:
- Modify `UpgradeMenu.uss` for different styling
- Edit `UpgradeMenu.uxml` to add more menu items or structure changes
- Adjust input sensitivity in UpgradeMenu inspector

### Input System Integration:
The current implementation uses Unity's legacy Input Manager for broad compatibility. To use the new Input System:
1. Replace `Input.GetAxis("Vertical")` with Input System actions
2. Follow the pattern in `RotateWithJoystick.cs`
3. Add InputActionAsset reference to UpgradeMenu

## Troubleshooting

### Common Issues:
1. **Menu not showing**: Check UIDocument assignment and ensure UpgradeMenu.uxml is assigned
2. **Navigation not working**: Verify Input Manager has "Vertical" axis configured
3. **Game not pausing**: Check if Time.timeScale is being set elsewhere
4. **Styling issues**: Ensure UpgradeMenu.uss is assigned to StyleSheet field

### Debug Tips:
- Use console logs to track upgrade selection
- Test with UpgradeMenuExample for standalone testing
- Check Unity Input Manager settings for axis configuration

## Future Enhancements

### Suggested Improvements:
1. **Upgrade Categories**: Group upgrades by type (offensive, defensive, utility)
2. **Rarity System**: Different background colors for common/rare upgrades
3. **Preview System**: Show upgrade effects before selection
4. **Sound Integration**: Add audio feedback for navigation and selection
5. **Animation**: Smooth menu transitions and upgrade effect previews
6. **Persistence**: Save selected upgrades across game sessions