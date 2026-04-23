using UnityEngine;
using TMPro;

public class GoldUI : MonoBehaviour
{
    [SerializeField] private TMP_Text goldText;

    private void OnEnable()
    {
        GoldManager.OnGoldChanged += HandleGoldChanged;
        if (GoldManager.Instance != null)
        {
            HandleGoldChanged(GoldManager.Instance.Gold);
        }
    }

    private void OnDisable()
    {
        GoldManager.OnGoldChanged -= HandleGoldChanged;
    }

    private void HandleGoldChanged(int newGold)
    {
        if (goldText != null)
        {
            goldText.text = newGold.ToString();
        }
    }
}
