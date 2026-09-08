using UnityEngine;

/// <summary>
/// The right knight's stock special, unchanged: heals the caller and, by less,
/// the other knight. A data front door onto the existing HealSpecial component.
/// </summary>
[CreateAssetMenu(fileName = "Field Healing", menuName = "Equipment/Special/Field Healing")]
public class FieldHealingSpecial : SpecialDefinition
{
    public override void Activate(GameObject knight, string playerTag)
    {
        var heal = knight.GetComponent<HealSpecial>();
        if (heal == null)
        {
            Debug.LogWarning($"[FieldHealingSpecial] {playerTag} has no HealSpecial component.");
            return;
        }
        heal.ActivateHeal(playerTag);
    }
}
