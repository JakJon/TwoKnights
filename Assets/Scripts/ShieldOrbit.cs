using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Reload bar implementation using fillAmount approach
public class ShieldOrbit : MonoBehaviour
{
    public float CurrentAngle => _currentAngle;
    public Vector2 Direction => _direction;

    // NOT degrees per second, whatever the field is called. It is fed to
    // Quaternion.Slerp as the interpolation FRACTION (see Update), and that
    // argument is clamped to 0..1 — so at 180 and sixty frames a second the
    // fraction works out at 3.0, clamps to 1, and the guard is on the stick's
    // angle the same frame the stick moved. THE SHIELD TURNS INSTANTLY, and
    // anything above about 60 here is indistinguishable from anything else.
    //
    // Worth knowing before designing round it: the cost of crossing the dial is
    // the player's reaction and their thumb, not any travel time the guard has.
    // A flip volley can therefore ask for a tenth of a second and still be a
    // fair ask — the Mine's tier III and IV flips do exactly that.
    //
    // AboutFace.MinimumFlipWindow still reads this as if it were a rate and
    // derives 180/speed + 0.35 = 1.35s from it. That number is far more generous
    // than the geometry requires; it is left alone because those waves are tuned
    // and playtested at it, not because it is right.
    public float RotationSpeed => rotationSpeed;

    // Confusion status (dark bat sonar) flips the joystick while active
    public bool InvertControls { get; set; }

    /// <summary>
    /// Whether the stick moves this guard at all. Only the tutorial lowers it, to
    /// park the right knight's shield while that knight is still off the board.
    /// See PlayerShooter.InputEnabled.
    /// </summary>
    public bool InputEnabled { get; set; } = true;

    private float _currentAngle;
    private Vector2 _direction;

    [SerializeField] private float orbitRadius = 1f; // Distance from the player
    [SerializeField] private float rotationSpeed = 180f;
    [SerializeField] private InputActionReference shieldActionReference;
    [SerializeField] private bool isLeftShield; // Toggle in Inspector
    
    [Header("Sword Attack")]
    [SerializeField] private GameObject swordAttackPrefab; // Assign swordAttackLeft or swordAttackRight
    
    [Header("Reload Bar Settings")]
    [SerializeField] private float reloadBarOffset = 0.1f; // Angular offset for reload bar

    private Transform playerTransform;
    private InputAction shieldInputAction;
    private GameObject swordAttackInstance; // Instance of the sword attack system
    
    // Reload bar components
    private GameObject reloadBarObject;
    private Canvas reloadBarCanvas;
    private Image reloadBarImage;

    // The sword's cooldown, drawn exactly like the bow's but blue, and laid flush
    // against the knight's side of the red bar
    private GameObject swordReloadBarObject;
    private Image swordReloadBarImage;

    // Both bars' canvases are built to these, so the two are always the same size
    private static readonly Vector2 ReloadBarSize = new Vector2(1.1f, .05f);
    private const float ReloadBarScale = .5f;

    void Awake()
    {
        playerTransform = transform.parent; // Assuming shield is a child of the player
        
        if (shieldActionReference != null)
        {
            shieldInputAction = shieldActionReference.action;
            shieldInputAction.Enable();
        }
        
        Debug.Log("ShieldOrbit: About to create reload bar");
        CreateReloadBar();
        Debug.Log("ShieldOrbit: About to create sword attack");
        CreateSwordAttack();
        
        Debug.Log($"ShieldOrbit: Awake completed. swordAttackPrefab assigned: {swordAttackPrefab != null}");
    }

    void Update()
    {
        // Read joystick input
        Vector2 input = InputEnabled
            ? (shieldInputAction?.ReadValue<Vector2>() ?? Vector2.zero)
            : Vector2.zero;
        if (InvertControls) input = -input;

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
        reloadBarObject = CreateBar("ReloadBar", new Color(0.8f, 0.1f, 0.1f, 1f), out reloadBarImage); // Dark red
        reloadBarCanvas = reloadBarObject.GetComponent<Canvas>();
        swordReloadBarObject = CreateBar("SwordReloadBar", new Color(0.1f, 0.35f, 0.9f, 1f), out swordReloadBarImage); // Blue
    }

    private GameObject CreateBar(string name, Color color, out Image barImage)
    {
        // Create simple reload bar with transform scaling approach
        GameObject barRoot = new GameObject(name);
        barRoot.transform.SetParent(playerTransform);

        // Add Canvas for world space UI
        Canvas canvas = barRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;

        // Set canvas size - small and simple
        RectTransform canvasRect = barRoot.GetComponent<RectTransform>();
        canvasRect.sizeDelta = ReloadBarSize; // Small bar
        canvasRect.localScale = Vector3.one * ReloadBarScale; // Small scale

        // Create a single bar that we'll scale down
        GameObject barObj = new GameObject("Bar");
        barObj.transform.SetParent(barRoot.transform);
        barImage = barObj.AddComponent<Image>();
        barImage.color = color;

        RectTransform barRect = barObj.GetComponent<RectTransform>();
        barRect.anchorMin = Vector2.zero;
        barRect.anchorMax = Vector2.one;
        barRect.sizeDelta = Vector2.zero;
        barRect.anchoredPosition = Vector2.zero;
        barRect.pivot = new Vector2(0.5f, 0f); // Pivot at bottom center so it shrinks upward

        // Start hidden (player can shoot and swing initially)
        barRoot.SetActive(false);
        return barRoot;
    }
    
    private void CreateSwordAttack()
    {
        Debug.Log($"ShieldOrbit: CreateSwordAttack called. swordAttackPrefab is null: {swordAttackPrefab == null}");
        
        if (swordAttackPrefab != null)
        {
            // Instantiate the sword attack system as a child of the player
            swordAttackInstance = Instantiate(swordAttackPrefab, playerTransform.position, Quaternion.identity, playerTransform);
            Debug.Log($"ShieldOrbit: Sword attack instance created: {swordAttackInstance.name}");
        }
        else
        {
            Debug.LogWarning("ShieldOrbit: swordAttackPrefab is null! Please assign it in the inspector.");
        }
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

        // The sword's bar rides against the red one on the knight's side. The bars'
        // local up points in toward the knight, so stepping one bar-thickness along
        // it lays the two edge to edge with no gap.
        //
        // Measured off the red IMAGE, not its canvas: the image is parented keeping
        // its world scale, so it draws twice as thick as the canvas it sits in,
        // growing inward from the canvas's outer edge. Stepping by the canvas's
        // thickness put the blue bar over the inner half of the red one.
        if (swordReloadBarObject != null && reloadBarImage != null)
        {
            RectTransform redRect = reloadBarImage.rectTransform;
            swordReloadBarObject.transform.rotation = reloadBarObject.transform.rotation;
            swordReloadBarObject.transform.position = reloadBarPosition +
                redRect.TransformVector(0f, redRect.rect.height, 0f);
        }
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
        SetBarFill(reloadBarImage, fillAmount);
    }

    // The sword's twins of the two above, driven by SwordSwing's cooldown
    public void SetSwordReloadBarVisible(bool isVisible)
    {
        if (swordReloadBarObject != null)
        {
            swordReloadBarObject.SetActive(isVisible);
        }
    }

    public void SetSwordReloadBarFill(float fillAmount)
    {
        SetBarFill(swordReloadBarImage, fillAmount);
    }

    private static void SetBarFill(Image barImage, float fillAmount)
    {
        if (barImage != null)
        {
            Vector3 currentScale = barImage.transform.localScale;
            // Since bar is rotated 90 degrees, scale X-axis to affect visual length
            barImage.transform.localScale = new Vector3(fillAmount, currentScale.y, currentScale.z);
        }
        else
        {
            Debug.LogWarning("Reload bar image is null!");
        }
    }

    void OnDestroy()
    {
        if (shieldInputAction != null)
        {
            shieldInputAction.Disable();
        }
        
        // Clean up reload bars
        if (reloadBarObject != null)
        {
            DestroyImmediate(reloadBarObject);
        }
        if (swordReloadBarObject != null)
        {
            DestroyImmediate(swordReloadBarObject);
        }
    }
}