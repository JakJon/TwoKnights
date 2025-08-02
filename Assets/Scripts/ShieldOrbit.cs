using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Reload bar implementation using fillAmount approach
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
    
    [Header("Reload Bar Settings")]
    [SerializeField] private float reloadBarOffset = 0.1f; // Angular offset for reload bar

    private Transform playerTransform;
    private InputAction shieldAction;
    
    // Reload bar components
    private GameObject reloadBarObject;
    private Canvas reloadBarCanvas;
    private Image reloadBarImage;

    void Awake()
    {
        playerTransform = transform.parent; // Assuming shield is a child of the player
        InputActionMap actionMap = inputActions.FindActionMap("PlayerControls");
        shieldAction = isLeftShield ?
            actionMap.FindAction("LeftShield") :
            actionMap.FindAction("RightShield");
        shieldAction.Enable();
        
        CreateReloadBar();
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
        
        // Always update reload bar position
        UpdateReloadBarPosition();
    }
    
    private void CreateReloadBar()
    {
        // Create simple reload bar with transform scaling approach
        reloadBarObject = new GameObject("ReloadBar");
        reloadBarObject.transform.SetParent(playerTransform);
        
        // Add Canvas for world space UI
        reloadBarCanvas = reloadBarObject.AddComponent<Canvas>();
        reloadBarCanvas.renderMode = RenderMode.WorldSpace;
        reloadBarCanvas.sortingOrder = 10;
        
        // Set canvas size - small and simple
        RectTransform canvasRect = reloadBarObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1.1f, .05f); // Small bar
        canvasRect.localScale = Vector3.one * .5f; // Small scale
        
        // Create a single red bar that we'll scale down
        GameObject barObj = new GameObject("Bar");
        barObj.transform.SetParent(reloadBarObject.transform);
        reloadBarImage = barObj.AddComponent<Image>();
        // reloadBarImage.color = Color.; // Red bar
        // set the color to a dark red
        reloadBarImage.color = new Color(0.8f, 0.1f, 0.1f, 1f); // Dark red
        
        RectTransform barRect = barObj.GetComponent<RectTransform>();
        barRect.anchorMin = Vector2.zero;
        barRect.anchorMax = Vector2.one;
        barRect.sizeDelta = Vector2.zero;
        barRect.anchoredPosition = Vector2.zero;
        barRect.pivot = new Vector2(0.5f, 0f); // Pivot at bottom center so it shrinks upward
        
        // Start hidden (player can shoot initially)
        reloadBarObject.SetActive(false);        
    }
    
    private void UpdateReloadBarPosition()
    {
        if (reloadBarObject == null) return;
        
        // Calculate reload bar angle (shield angle + offset)
        float reloadBarAngle = _currentAngle + reloadBarOffset;
        float reloadBarAngleRad = reloadBarAngle * Mathf.Deg2Rad;
        
        // Position reload bar closer to the knight
        float reloadBarRadius = orbitRadius * 0.88f; // 88% of shield radius
        Vector3 reloadBarPosition = playerTransform.position + 
            new Vector3(Mathf.Cos(reloadBarAngleRad), Mathf.Sin(reloadBarAngleRad), 0) * reloadBarRadius +
            (Vector3.up * .5f);
        
        reloadBarObject.transform.position = reloadBarPosition;
        
        // Keep the bar parallel to the shield
        reloadBarObject.transform.rotation = transform.rotation * Quaternion.Euler(0, 0, 90f);
    }
    
    // Public method to show/hide the reload bar
    public void SetReloadBarVisible(bool isVisible)
    {
        if (reloadBarObject != null)
        {
            reloadBarObject.SetActive(isVisible);
        }
    }
    
    // Public method to set the bar scale (1.0 = full length, 0.0 = no length)
    public void SetReloadBarFill(float fillAmount)
    {
        if (reloadBarImage != null)
        {
            Vector3 currentScale = reloadBarImage.transform.localScale;
            // Since bar is rotated 90 degrees, scale X-axis to affect visual length
            reloadBarImage.transform.localScale = new Vector3(fillAmount, currentScale.y, currentScale.z);        }
        else
        {
            Debug.LogWarning("Reload bar image is null!");
        }
    }

    void OnDestroy()
    {
        shieldAction.Disable();
        
        // Clean up reload bar
        if (reloadBarObject != null)
        {
            DestroyImmediate(reloadBarObject);
        }
    }
}