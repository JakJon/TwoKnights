using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The registry of every equipment item in the game, loaded from Resources so
/// both the camp and the game scene can reach it.
///
/// An explicit list rather than a folder scan, matching MapCatalog and
/// UpgradeManager: an item that is not in this list simply does not exist, which
/// fails loudly at authoring time instead of quietly shipping something half
/// wired. Resources.Load is used here only because this is a true singleton —
/// individual items are referenced through this list, never by magic string.
/// </summary>
[CreateAssetMenu(fileName = "EquipmentCatalog", menuName = "Equipment/Equipment Catalog")]
public class EquipmentCatalog : ScriptableObject
{
    public const string ResourcePath = "EquipmentCatalog";

    [SerializeField] private List<EquipmentDefinition> equipment = new List<EquipmentDefinition>();
    [SerializeField] private List<SpecialDefinition> specials = new List<SpecialDefinition>();

    [Header("Shared UI art")]
    [Tooltip("Crystal currency icon.")]
    [SerializeField] private Sprite crystalIcon;
    [Tooltip("Shown for the reward that grants a second equipment slot.")]
    [SerializeField] private Sprite equipmentSlotIcon;

    // These ride on the catalog because it is already the one true singleton the
    // UI can reach; a Resources.Load per sprite would couple every screen to a
    // magic string (see the house rule in prefab-authoring).
    public Sprite CrystalIcon => crystalIcon;
    public Sprite EquipmentSlotIcon => equipmentSlotIcon;

    private static EquipmentCatalog _instance;

    public static EquipmentCatalog Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<EquipmentCatalog>(ResourcePath);
                if (_instance == null)
                {
                    Debug.LogError($"[EquipmentCatalog] No catalog at Resources/{ResourcePath}. No equipment will resolve.");
                }
            }
            return _instance;
        }
    }

    public IReadOnlyList<EquipmentDefinition> Equipment => equipment;

    public EquipmentDefinition Find(string equipmentId)
    {
        if (string.IsNullOrEmpty(equipmentId)) return null;
        for (int i = 0; i < equipment.Count; i++)
        {
            if (equipment[i] != null && equipment[i].Id == equipmentId) return equipment[i];
        }
        return null;
    }

    /// <summary>Items the shop can sell, in catalog order.</summary>
    public IEnumerable<EquipmentDefinition> ShopStock
    {
        get
        {
            for (int i = 0; i < equipment.Count; i++)
            {
                if (equipment[i] != null && equipment[i].SoldInShop) yield return equipment[i];
            }
        }
    }

    public IReadOnlyList<SpecialDefinition> Specials => specials;

    public SpecialDefinition FindSpecial(string specialId)
    {
        if (string.IsNullOrEmpty(specialId)) return null;
        for (int i = 0; i < specials.Count; i++)
        {
            if (specials[i] != null && specials[i].Id == specialId) return specials[i];
        }
        return null;
    }

    public IEnumerable<SpecialDefinition> SpecialStock
    {
        get
        {
            for (int i = 0; i < specials.Count; i++)
            {
                if (specials[i] != null && specials[i].SoldInShop) yield return specials[i];
            }
        }
    }
}
