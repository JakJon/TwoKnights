using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// Accept/back buttons for menus that poll the gamepad directly. Nintendo pads
// (Switch Pro) carry the Submit usage on A (east) and Cancel on B (south) —
// the mirror of Xbox/PlayStation — so hardcoding buttonSouth flips accept
// between controller brands. The {Submit}/{Cancel} usages resolve to whatever
// the connected pad considers accept/back, matching the Menu action map and
// the UI EventSystem bindings.
public static class MenuGamepad
{
    private static Gamepad _cached;
    private static ButtonControl _submit;
    private static ButtonControl _cancel;

    public static bool SubmitPressed(Gamepad gamepad)
    {
        if (gamepad == null) return false;
        Cache(gamepad);
        return _submit.wasPressedThisFrame;
    }

    public static bool CancelPressed(Gamepad gamepad)
    {
        if (gamepad == null) return false;
        Cache(gamepad);
        return _cancel.wasPressedThisFrame;
    }

    private static void Cache(Gamepad gamepad)
    {
        if (ReferenceEquals(gamepad, _cached)) return;
        _cached = gamepad;
        _submit = gamepad.TryGetChildControl<ButtonControl>("{Submit}") ?? gamepad.buttonSouth;
        _cancel = gamepad.TryGetChildControl<ButtonControl>("{Cancel}") ?? gamepad.buttonEast;
    }
}
