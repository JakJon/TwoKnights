using UnityEngine;
using TMPro;

public class WaveReachedUI : MonoBehaviour
{
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private GameObject rootToHide;

    private void OnEnable()
    {
        int wave = SaveManager.Data.furthestWave;
        GameObject target = rootToHide != null ? rootToHide : gameObject;

        if (wave > 1)
        {
            target.SetActive(true);
            if (waveText != null)
            {
                waveText.text = $"Furthest Wave: {wave}";
            }
        }
        else
        {
            target.SetActive(false);
        }
    }
}
