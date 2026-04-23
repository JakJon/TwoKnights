using UnityEngine;
using TMPro;

public class KnightRankUI : MonoBehaviour
{
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text honorText;
    [SerializeField] private string rankFormat = "Knight Rank {0}";
    [SerializeField] private string honorFormat = "Honor: {0}";

    private void OnEnable()
    {
        KnightRankManager.OnRankChanged += HandleRankChanged;
        KnightRankManager.OnHonorChanged += HandleHonorChanged;
        if (KnightRankManager.Instance != null)
        {
            HandleRankChanged(KnightRankManager.Instance.KnightRank);
            HandleHonorChanged(KnightRankManager.Instance.HonorPoints);
        }
    }

    private void OnDisable()
    {
        KnightRankManager.OnRankChanged -= HandleRankChanged;
        KnightRankManager.OnHonorChanged -= HandleHonorChanged;
    }

    private void HandleRankChanged(int newRank)
    {
        if (rankText != null)
        {
            rankText.text = string.Format(rankFormat, newRank);
        }
    }

    private void HandleHonorChanged(int newHonor)
    {
        if (honorText != null)
        {
            honorText.text = string.Format(honorFormat, newHonor);
        }
    }
}
