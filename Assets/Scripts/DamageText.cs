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

    void Awake()
    {
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

    public void Initialize(int damage)
    {
        if (textMesh != null)
        {
            // minus sign and then the dmage value
            textMesh.text = $"-{damage}";
            textMesh.color = Color.red;
        }
        
        // Adjust starting position (lower the text)
        transform.position += Vector3.down * 0.0f; // Adjust 0.3f to your preference
        
        StartCoroutine(AnimateText());
    }

    private IEnumerator AnimateText()
    {
        float elapsed = 0f;
        Vector3 startPosition = transform.position;
        
        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            
            // Move the text upward
            transform.position = startPosition + (moveDirection * moveSpeed * elapsed);
            
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
