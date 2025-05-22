using UnityEngine;
using System.Collections;

public class GlowManager : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private MaterialPropertyBlock propBlock;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;
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
            spriteRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor("_GlowColor", color * wave);
            propBlock.SetFloat("_GlowStrength", wave);
            spriteRenderer.SetPropertyBlock(propBlock);
            elapsed += Time.deltaTime;
            yield return null;
        }
        // Reset
        spriteRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_GlowColor", originalColor);
        propBlock.SetFloat("_GlowStrength", 0f);
        spriteRenderer.SetPropertyBlock(propBlock);
    }
}
