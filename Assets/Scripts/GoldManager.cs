using UnityEngine;
using System;

public class GoldManager : MonoBehaviour
{
    public static GoldManager Instance { get; private set; }

    [SerializeField] private int gold = 0;

    public static event Action<int> OnGoldChanged;

    public int Gold => gold;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        gold = SaveManager.Data.gold;
    }

    private void Start()
    {
        OnGoldChanged?.Invoke(gold);
    }

    public void AddGold(int amount)
    {
        gold += amount;
        Persist();
        OnGoldChanged?.Invoke(gold);
    }

    public void SetGold(int amount)
    {
        gold = amount;
        Persist();
        OnGoldChanged?.Invoke(gold);
    }

    private void Persist()
    {
        SaveManager.Data.gold = gold;
        SaveManager.Save();
    }
}
