using UnityEngine;
using System.Collections;

public class GlowManager : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private MaterialPropertyBlock propBlock;
    private Texture2D originalTexture;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
            originalTexture = spriteRenderer.sprite?.texture;
        }
        propBlock = new MaterialPropertyBlock();
    }

    // Only one StartGlow method, with defaults for waveSpeed and waveAmplitude
    public void StartGlow(Color color, float duration, float waveSpeed = 7.5f, float waveAmplitude = .65f)
    {
        if (spriteRenderer != null)
            StartCoroutine(GlowWaveRoutine(color, duration, waveSpeed, waveAmplitude));
    }

    private IEnumerator GlowWaveRoutine(Color color, float duration, float waveSpeed, float waveAmplitude)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float wave = (Mathf.Sin(Time.time * waveSpeed) * 0.5f + 0.5f) * waveAmplitude;
            
            // Get current property block to preserve existing properties
            spriteRenderer.GetPropertyBlock(propBlock);
            
            // Ensure the main texture is set (critical for sprite rendering)
            if (originalTexture != null)
                propBlock.SetTexture("_MainTex", originalTexture);
            
            // Set glow properties
            propBlock.SetColor("_GlowColor", color * wave);
            propBlock.SetFloat("_GlowStrength", wave);
            
            spriteRenderer.SetPropertyBlock(propBlock);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Reset to original state
        ResetGlow();
    }

    private void ResetGlow()
    {
        // Clear the property block completely to restore default rendering
        propBlock.Clear();
        spriteRenderer.SetPropertyBlock(propBlock);
    }

    void OnDestroy()
    {
        // Ensure we reset when the object is destroyed
        if (spriteRenderer != null)
            ResetGlow();
    }
}
