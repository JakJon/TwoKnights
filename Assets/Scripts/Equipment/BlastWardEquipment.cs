using UnityEngine;

/// <summary>
/// Softens explosions — powder kegs in the mine, bombs in the wood. Both are
/// DamageKind.Blast, so one item answers the whole category rather than naming
/// the sources it happens to know about today.
/// </summary>
[CreateAssetMenu(fileName = "Blast Ward", menuName = "Equipment/Blast Ward")]
public class BlastWardEquipment : EquipmentDefinition
{
    [Tooltip("Share of blast damage that still lands. 0.25 = you take a quarter of it.")]
    [Range(0f, 1f)]
    [SerializeField] private float damageTaken = 0.25f;

    public override void Apply(GameObject knight)
    {
        BoostOn(knight).ReduceBlastDamage(damageTaken);
    }
}
