using UnityEngine;
using UnityEngine.InputSystem;

public class ShieldOrbit : MonoBehaviour
{
    public float CurrentAngle => _currentAngle;
    public Vector2 Direction => _direction;

    private float _currentAngle;
    private Vector2 _direction;

    [SerializeField] private float orbitRadius = 1f; // Distance from the player
    [SerializeField] private float rotationSpeed = 180f;
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private bool isLeftShield; // Toggle in Inspector

    private Transform playerTransform;
    private InputAction shieldAction;

    void Awake()
    {
        playerTransform = transform.parent; // Assuming shield is a child of the player
        InputActionMap actionMap = inputActions.FindActionMap("PlayerControls");
        shieldAction = isLeftShield ?
            actionMap.FindAction("LeftShield") :
            actionMap.FindAction("RightShield");
        shieldAction.Enable();
    }

    void Update()
    {
        // Read joystick input
        Vector2 input = shieldAction.ReadValue<Vector2>();

        if (input.magnitude > .5f) // Deadzone check
        {
            // Calculate target angle from joystick input
            float targetAngle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;

            // Rotate the shield around the player
            Quaternion targetRotation = Quaternion.Euler(0, 0, targetAngle);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );

            // Position the shield around the player
            transform.position = playerTransform.position +
                (transform.right * orbitRadius) +
                (Vector3.up * .5f);
        }

        _currentAngle = transform.eulerAngles.z;
        _direction = transform.right;
    }

    void OnDestroy()
    {
        shieldAction.Disable();
    }
}