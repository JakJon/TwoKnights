using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Where an offered upgrade sits within its chain/family, for UI display
public struct UpgradeChainInfo
{
    public string ChainName;  // e.g. "Shadow Arrow"
    public int Position;      // 1-based step of this upgrade within the chain
    public int Length;        // total steps in the chain
    public int OwnedCount;    // steps the target knight already owns in this chain
}

[CreateAssetMenu(fileName = "UpgradeManager", menuName = "Upgrades/Upgrade Manager")]
public class UpgradeManager : ScriptableObject
{
    [SerializeField] private List<BaseUpgrade> allUpgrades = new List<BaseUpgrade>();
    [SerializeField] private int upgradesPerSelection = 3;
    
    // Track owned upgrades per knight (unique per knight per run)
    private readonly HashSet<BaseUpgrade> _leftOwned = new HashSet<BaseUpgrade>();
    private readonly HashSet<BaseUpgrade> _rightOwned = new HashSet<BaseUpgrade>();
    
    // Alternate target between knights
    private KnightTarget _nextTarget = KnightTarget.LeftKnight;
    
    // Track applied upgrades per knight for UI/status
    private readonly List<string> _leftApplied = new List<string>();
    private readonly List<string> _rightApplied = new List<string>();
    
    // Get a random selection of available upgrades for the next knight in turn
    public List<BaseUpgrade> GetRandomUpgrades()
    {
        var target = _nextTarget;
        var availableUpgrades = GetAvailableUpgradesFor(target).ToList();
        Debug.Log($"Available upgrades for {target}: {string.Join(", ", availableUpgrades.Select(u => u))}");

        return SelectWeightedDistinct(availableUpgrades, upgradesPerSelection);
    }

    // Get a random selection of available upgrades for a specific knight (does not change turn)
    public List<BaseUpgrade> GetRandomUpgradesFor(KnightTarget targetKnight)
    {
        var available = GetAvailableUpgradesFor(targetKnight).ToList();
        return SelectWeightedDistinct(available, upgradesPerSelection);
    }
    
    private BaseUpgrade GetWeightedRandomUpgrade(List<BaseUpgrade> upgrades)
    {
        // Calculate total weight for all available upgrades
        float totalWeight = upgrades.Sum(u => u.Weight);
        
        // Random selection based on weights (similar to WaveManager)
        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        foreach (var upgrade in upgrades)
        {
            currentWeight += upgrade.Weight;
            if (randomValue <= currentWeight)
            {
                return upgrade;
            }
        }
        
        // Fallback to last upgrade if something went wrong with weight calculation
        return upgrades.LastOrDefault();
    }
    
    private List<BaseUpgrade> SelectWeightedDistinct(List<BaseUpgrade> pool, int count)
    {
        // Always enforce per-category uniqueness; if not enough unique categories exist, return fewer than count.
        var selected = new List<BaseUpgrade>();
        var usedTypes = new HashSet<System.Type>();
        var temp = new List<BaseUpgrade>(pool);

        for (int i = 0; i < count && temp.Count > 0; i++)
        {
            // Filter out upgrades whose type is already used in this selection
            var filtered = temp.Where(u => !usedTypes.Contains(u.GetType())).ToList();
            if (filtered.Count == 0)
            {
                // No non-conflicting categories left, break early
                break;
            }

            var pick = GetWeightedRandomUpgrade(filtered);
            selected.Add(pick);
            usedTypes.Add(pick.GetType());
            temp.Remove(pick);
        }
        return selected;
    }
    
    private IEnumerable<BaseUpgrade> GetAvailableUpgradesFor(KnightTarget targetKnight)
    {
        var owned = targetKnight == KnightTarget.LeftKnight ? _leftOwned : _rightOwned;

        foreach (var up in allUpgrades)
        {
            if (owned.Contains(up))
                continue; // unique per knight

            // Conflict rule: if an upgrade exists in both arrays for this evaluation, treat as unlocked and optionally throw
            bool conflict = up.UnlockedBy.Any() && up.LockedBy.Any() && up.UnlockedBy.Intersect(up.LockedBy).Any();
            if (conflict)
            {
                Debug.LogError($"Upgrade '{up.UpgradeName}' has the same dependency in both UnlockedBy and LockedBy. Treating as unlocked for availability.");
            }

            // Starting upgrades: no prerequisites -> in pool
            bool isStarting = up.UnlockedBy == null || up.UnlockedBy.Count == 0;

            // Any-of unlock: available if starting OR owns any prerequisite OR conflict says unlocked
            bool unlocked = isStarting || (up.UnlockedBy != null && up.UnlockedBy.Any(owned.Contains)) || conflict;

            // Any-of lock: unavailable if owns any in lockedBy, unless conflict forces unlock
            bool locked = !conflict && up.LockedBy != null && up.LockedBy.Any(owned.Contains);

            if (unlocked && !locked)
            {
                yield return up;
            }
        }
    }
    
    public void ApplyUpgrade(BaseUpgrade upgrade, KnightTarget targetKnight)
    {
        GameObject knight = targetKnight == KnightTarget.LeftKnight 
            ? GameObject.FindWithTag("PlayerLeft") 
            : GameObject.FindWithTag("PlayerRight");
            
        if (knight != null && upgrade != null)
        {
            upgrade.ApplyUpgrade(knight);
            // Record applied upgrade name for status panels
            if (targetKnight == KnightTarget.LeftKnight)
            {
                _leftApplied.Add(upgrade.UpgradeName);
                _leftOwned.Add(upgrade);
            }
            else
            {
                _rightApplied.Add(upgrade.UpgradeName);
                _rightOwned.Add(upgrade);
            }

            // Flip turn to the other knight for next selection
            _nextTarget = targetKnight == KnightTarget.LeftKnight ? KnightTarget.RightKnight : KnightTarget.LeftKnight;
        }
    }

    // Expose applied upgrade names for UI
    public IEnumerable<string> GetAppliedUpgradeNames(KnightTarget targetKnight)
    {
        return targetKnight == KnightTarget.LeftKnight ? _leftApplied : _rightApplied;
    }

    // Optional: allow external systems to query available upgrades for a specific knight
    public IEnumerable<BaseUpgrade> GetAvailableUpgrades(KnightTarget targetKnight)
    {
        return GetAvailableUpgradesFor(targetKnight);
    }

    // ---- Chain info (for the upgrade menu's progress pips) ----

    // Depth of each upgrade in the unlockedBy DAG, memoized; assets don't change at runtime
    [System.NonSerialized] private Dictionary<BaseUpgrade, int> _chainDepthCache;

    public UpgradeChainInfo GetChainInfo(BaseUpgrade upgrade, KnightTarget targetKnight)
    {
        var owned = targetKnight == KnightTarget.LeftKnight ? _leftOwned : _rightOwned;
        var family = upgrade.GetType();

        int length = 0;
        foreach (var up in allUpgrades)
        {
            if (up != null && up.GetType() == family)
                length = Mathf.Max(length, GetChainDepth(up));
        }

        return new UpgradeChainInfo
        {
            ChainName = upgrade.ChainName,
            Position = GetChainDepth(upgrade),
            Length = length,
            OwnedCount = owned.Count(u => u != null && u.GetType() == family)
        };
    }

    private int GetChainDepth(BaseUpgrade upgrade)
    {
        _chainDepthCache ??= new Dictionary<BaseUpgrade, int>();
        return GetChainDepth(upgrade, new HashSet<BaseUpgrade>());
    }

    private int GetChainDepth(BaseUpgrade upgrade, HashSet<BaseUpgrade> visiting)
    {
        if (_chainDepthCache.TryGetValue(upgrade, out int cached)) return cached;
        if (!visiting.Add(upgrade)) return 1; // cycle in asset data; treat as a root

        int parentDepth = 0;
        foreach (var parent in upgrade.UnlockedBy)
        {
            // Skip null/self entries so malformed asset references can't recurse forever
            if (parent == null || parent == upgrade) continue;
            parentDepth = Mathf.Max(parentDepth, GetChainDepth(parent, visiting));
        }

        visiting.Remove(upgrade);
        int depth = parentDepth + 1;
        _chainDepthCache[upgrade] = depth;
        return depth;
    }

    // Reset per-run state
    public void ResetRunState()
    {
        _leftOwned.Clear();
        _rightOwned.Clear();
        _leftApplied.Clear();
        _rightApplied.Clear();
        _nextTarget = KnightTarget.LeftKnight;
    }
}