using UnityEngine;

/// <summary>
/// Bites one kinship group harder than the rest — The Gnawed Crown against
/// vermin, the Wolfsbane Pendant against beasts.
///
/// One class covers every bane because the only thing that differs between them
/// is which family and how much; making each a separate class would be four
/// copies of one line.
/// </summary>
[CreateAssetMenu(fileName = "Bane", menuName = "Equipment/Family Bane")]
public class FamilyBaneEquipment : EquipmentDefinition
{
    [SerializeField] private EnemyFamily family = EnemyFamily.Vermin;
    [Tooltip("Outgoing damage multiplier against that family. 1.5 = half again as much.")]
    [SerializeField] private float damageMultiplier = 1.5f;

    public EnemyFamily Family => family;

    public override void Apply(GameObject knight)
    {
        BoostOn(knight).AddFamilyDamage(family, damageMultiplier);
    }
}
