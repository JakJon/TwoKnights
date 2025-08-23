using UnityEngine;
using TMPro;
using System.Collections;

public class DamageText : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private Vector3 moveDirection = Vector3.up;
    [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    
    private TextMeshPro textMesh;
    private Color originalColor;
    // Base world position captured at spawn; can be adjusted via PushUp()
    private Vector3 basePosition;
    // Extra Y offset so existing texts can be nudged upward when new ones spawn
    private float externalYOffset = 0f;

    void Awake()
    {
        // Ensure this object is marked as transient (not to be cloned when enemies duplicate)
        var markerType = System.Type.GetType("TransientEffect");
        if (markerType != null && GetComponent(markerType) == null)
        {
            gameObject.AddComponent(markerType);
        }
        // First try to find TextMeshPro on this object
        textMesh = GetComponent<TextMeshPro>();
        
        // If not found, look in children
        if (textMesh == null)
        {
            textMesh = GetComponentInChildren<TextMeshPro>();
        }
        
        if (textMesh != null)
        {
            originalColor = textMesh.color;
        }
    }

    public void Initialize(int damage, Color? color = null)
    {
        if (textMesh != null)
        {
            // minus sign and then the dmage value
            textMesh.text = $"-{damage}";
            // Use provided color or default to red
            textMesh.color = color ?? Color.red;
        }
        
        // Adjust starting position (lower the text)
        transform.position += Vector3.down * 0.0f; // Adjust 0.3f to your preference
        // Capture base world position for animation calculations
        basePosition = transform.position;
        
        StartCoroutine(AnimateText());
    }

    // Public API to nudge the text upward while it animates
    public void PushUp(float amount)
    {
        externalYOffset += amount;
    }

    private IEnumerator AnimateText()
    {
        float elapsed = 0f;
        // basePosition is captured in Initialize and combined with any externalYOffset
        
        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            
            // Move the text upward
            transform.position = basePosition + (Vector3.up * externalYOffset) + (moveDirection * moveSpeed * elapsed);
            
            // Fade out using the animation curve - make fade happen mostly at the end
            if (textMesh != null)
            {
                Color color = textMesh.color;
                // Use a power curve to make fade happen quickly at the end
                float fadeProgress = Mathf.Pow(progress, 3f); // Cubic curve for quick fade at end
                color.a = alphaCurve.Evaluate(fadeProgress);
                textMesh.color = color;
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Destroy the text object when animation is complete
        Destroy(gameObject);
    }
}
