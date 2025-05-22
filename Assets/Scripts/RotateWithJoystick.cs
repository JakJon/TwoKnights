using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;

[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
public class RotateWithJoystick : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 100f;

    // Reference to your Input Actions asset
    [SerializeField] private InputActionAsset inputActions;

    private InputAction rotateAction;

    void Awake()
    {
        // Get the "Player One" Action Map
        InputActionMap playerOneMap = inputActions.FindActionMap("Player One");

        // Get the "Rotate" action from the Action Map
        rotateAction = playerOneMap.FindAction("Rotate");

        // Enable the action
        rotateAction.Enable();
    }

    void Update()
    {
        // Read the joystick input
        Vector2 input = rotateAction.ReadValue<Vector2>();

        // Rotate the object based on horizontal input (left/right on the joystick)
        float rotationAmount = input.x * rotationSpeed * Time.deltaTime;
        transform.Rotate(0, rotationAmount, 0);
    }

    void OnDestroy()
    {
        rotateAction.Disable();
    }

    private string GetDebuggerDisplay()
    {
        return ToString();
    }
}