using UnityEngine;
using TMPro;
using System.Collections;

public class WaveName : MonoBehaviour
{
    private TextMeshProUGUI waveText;
    private float displayDuration = 5f;
    private float fadeOutDuration = 1f;
    private float fadeInDuration = 1f;

    private void Awake()
    {
        waveText = GetComponent<TextMeshProUGUI>();
        waveText.alpha = 0f;
    }

    public void DisplayWaveName(string waveName)
    {
        StopAllCoroutines();
        StartCoroutine(ShowWaveNameCoroutine(waveName));
    }

    private IEnumerator ShowWaveNameCoroutine(string waveName)
    {
        waveText.text = waveName;
        waveText.alpha = 0f;

        // Fade in
        float fadeInElapsed = 0f;
        while (fadeInElapsed < fadeInDuration)
        {
            fadeInElapsed += Time.deltaTime;
            waveText.alpha = Mathf.Lerp(0f, 1f, fadeInElapsed / fadeInDuration);
            yield return null;
        }
        waveText.alpha = 1f;

        // Wait for the visible duration (excluding fade in/out)
        float visibleDuration = displayDuration - fadeInDuration - fadeOutDuration;
        if (visibleDuration > 0f)
            yield return new WaitForSeconds(visibleDuration);

        // Fade out
        float fadeOutElapsed = 0f;
        while (fadeOutElapsed < fadeOutDuration)
        {
            fadeOutElapsed += Time.deltaTime;
            waveText.alpha = Mathf.Lerp(1f, 0f, fadeOutElapsed / fadeOutDuration);
            yield return null;
        }
        waveText.alpha = 0f;
    }
}