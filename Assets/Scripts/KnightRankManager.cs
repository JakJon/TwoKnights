using UnityEngine;
using System;

public class KnightRankManager : MonoBehaviour
{
    public static KnightRankManager Instance { get; private set; }

    [SerializeField] private int honorPoints = 0;
    [SerializeField] private int knightRank = 1;

    public const int HonorPerRank = 10;

    public static event Action<int> OnHonorChanged;
    public static event Action<int> OnRankChanged;
    public static event Action<int> OnRankUp;

    public int HonorPoints => honorPoints;
    public int KnightRank => knightRank;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        honorPoints = SaveManager.Data.honorPoints;
        knightRank = Mathf.Max(1, SaveManager.Data.knightRank);
    }

    private void Start()
    {
        OnHonorChanged?.Invoke(honorPoints);
        OnRankChanged?.Invoke(knightRank);
    }

    public void AddHonor(int amount)
    {
        if (amount <= 0) return;
        honorPoints += amount;
        int newRank = CalculateRank(honorPoints);
        bool rankedUp = newRank > knightRank;
        knightRank = newRank;
        Persist();
        OnHonorChanged?.Invoke(honorPoints);
        if (rankedUp)
        {
            OnRankChanged?.Invoke(knightRank);
            OnRankUp?.Invoke(knightRank);
        }
    }

    public static int CalculateRank(int honor)
    {
        return Mathf.Max(1, (honor / HonorPerRank) + 1);
    }

    public int HonorToNextRank()
    {
        int next = knightRank * HonorPerRank;
        return Mathf.Max(0, next - honorPoints);
    }

    private void Persist()
    {
        SaveManager.Data.honorPoints = honorPoints;
        SaveManager.Data.knightRank = knightRank;
        SaveManager.Save();
    }
}
