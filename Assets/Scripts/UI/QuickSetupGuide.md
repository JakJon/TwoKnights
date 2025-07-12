# Quick Setup Guide for Upgrade Menu

## ?? Goal
Display an upgrade menu and pause the game when each wave ends, navigable with controller.

## ? What's Been Implemented

### Files Created:
- `Assets/Scripts/UI/UpgradeMenu.cs` - Menu controller
- `Assets/UI/UpgradeMenu.uxml` - Menu structure  
- `Assets/UI/UpgradeMenu.uss` - Menu styling
- `Assets/Scripts/UI/UpgradeMenuExample.cs` - Testing script
- `Assets/Scripts/UI/UpgradeMenuDocumentation.md` - Full documentation

### Files Modified:
- `Assets/Scripts/Spawner.cs` - Added upgrade menu integration

## ?? Scene Setup (Required)

### Step 1: Create Upgrade Menu GameObject
1. Create new GameObject in your scene: `UpgradeMenuUI`
2. Add `UIDocument` component
3. Set Source Asset to: `Assets/UI/UpgradeMenu.uxml`
4. Add `UpgradeMenu` component
5. In UpgradeMenu component:
   - Assign UI Document reference
   - Assign Style Sheet: `Assets/UI/UpgradeMenu.uss`

### Step 2: Connect to Spawner
1. Find your Spawner GameObject
2. In Spawner component, assign the UpgradeMenuUI to "Upgrade Menu" field

## ?? How It Works

**Current Flow:**
1. Wave spawns ? Wave completes ? Game pauses ? Menu shows ? Player selects ? Game resumes ? Next wave

**Controls:**
- Left joystick up/down (or arrow keys): Navigate
- Submit/Fire1 button: Select upgrade

## ?? Testing

Add `UpgradeMenuExample` to any GameObject for testing:
- **U key**: Toggle menu visibility 
- **P key**: Toggle pause
- Menu will show upgrade selection events in console

## ?? Next Steps

Replace placeholder upgrades in `OnUpgradeSelected` event with real upgrade logic:

```csharp
private void OnUpgradeSelected(int upgradeIndex)
{
    switch(upgradeIndex)
    {
        case 0: // "one" -> implement first upgrade
            break;
        case 1: // "two" -> implement second upgrade  
            break;
        case 2: // "three" -> implement third upgrade
            break;
    }
}
```

The menu is now ready and will automatically show between waves! ??