# Player Death Scene Transition Setup

## What's Been Implemented

I've successfully implemented a player death scene transition system for your Unity game. Here's what has been created:

### Files Created/Modified:

1. **`Assets/Scripts/GameSceneManager.cs`** - New scene management system
2. **`Assets/Scripts/PlayerHealth.cs`** - Modified to use scene transitions on death
3. **`Assets/Scenes/Camp.unity`** - New camp scene (basic template)

## Required Setup Steps in Unity Editor

### Step 1: Add Scenes to Build Settings
1. Open Unity Editor
2. Open Build Settings using one of these methods:
   - **Method 1**: Go to **File > Build Settings...**
   - **Method 2**: Use keyboard shortcut **Ctrl+Shift+B** (Windows) or **Cmd+Shift+B** (Mac)
   - **Method 3**: Go to **Edit > Project Settings > Player** then click "Build Settings..."
3. In the Build Settings window:
   - Click **Add Open Scenes** to add your current scene (if not already added)
   - Open the Camp scene (`Assets/Scenes/Camp.unity`) in the editor, then click **Add Open Scenes** again
   - Or manually drag `Assets/Scenes/Camp.unity` from the Project window into the "Scenes In Build" list
4. Make sure both scenes have index numbers (0, 1, etc.) and are checked/enabled
5. The scene at index 0 will be your starting scene

### Step 2: Add GameSceneManager to Your Main Game Scene
1. Open your main game scene (`SampleScene.unity`)
2. Create an empty GameObject and name it "GameSceneManager"
3. Add the `GameSceneManager` component to this GameObject
4. In the inspector, verify these settings:
   - **Camp Scene Name**: "Camp"
   - **Game Scene Name**: "SampleScene" 
   - **Transition Delay**: 2 (seconds before transitioning)
   - **Show Death Message**: true

### Step 3: Set Up the Camp Scene
1. Open `Assets/Scenes/Camp.unity`
2. Add any visual elements you want (background, UI, decorations)
3. Add a way for players to return to the game (button, automatic timer, etc.)
4. Example: Add a UI Button that calls `GameSceneManager.Instance.LoadGameScene()` when clicked

## How It Works

When a player's health reaches 0:
1. The `PlayerHealth.TakeDamage()` method detects death
2. It calls `GameSceneManager.Instance.OnPlayerDeath()`
3. After a configurable delay (default: 2 seconds), the game loads the "Camp" scene
4. Players can then return to the game from the camp

## Customization Options

### GameSceneManager Settings:
- **Transition Delay**: How long to wait before changing scenes
- **Show Death Message**: Whether to log death messages (you can extend this for UI)
- **Scene Names**: Configure which scenes to transition between

### Adding Death UI (Optional):
You can extend the system by adding UI elements during the death transition:
- Modify `HandlePlayerDeath()` coroutine in `GameSceneManager.cs`
- Add fade effects, death screens, or other visual feedback

## Testing

1. Play your game
2. Let the player take damage until health reaches 0
3. Verify the scene transitions to "Camp" after the delay
4. From the camp scene, implement a way to return to the game

## Next Steps

- Customize the Camp scene with your desired visuals and gameplay
- Add a return mechanism from Camp to the main game
- Consider adding save/load functionality to preserve progress
- Add UI animations or effects during the death transition

The basic framework is now in place and ready for your customization!