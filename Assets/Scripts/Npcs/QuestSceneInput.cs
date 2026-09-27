using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// "Any button at all", for advancing a quest scene.
///
/// Lifted from WaveSurvivedPanel.AnyAdvancePressed, which had the same problem and
/// solved it the same way: this is an acknowledgement, not a choice, so binding it
/// to one face button would only be a thing to get wrong. Shared rather than copied
/// so the dialogue box, the reward box and the quest cards cannot drift apart on
/// what counts as a press.
/// </summary>
public static class QuestSceneInput
{
    public static bool AdvancePressed()
    {
        var gp = Gamepad.current;
        if (gp != null)
        {
            if (MenuGamepad.SubmitPressed(gp) || MenuGamepad.CancelPressed(gp)) return true;
            if (gp.startButton.wasPressedThisFrame || gp.selectButton.wasPressedThisFrame) return true;
            if (gp.buttonNorth.wasPressedThisFrame || gp.buttonWest.wasPressedThisFrame) return true;
            if (gp.leftShoulder.wasPressedThisFrame || gp.rightShoulder.wasPressedThisFrame) return true;
        }

        var kb = Keyboard.current;
        if (kb != null && kb.anyKey.wasPressedThisFrame) return true;

        var mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
    }
}
