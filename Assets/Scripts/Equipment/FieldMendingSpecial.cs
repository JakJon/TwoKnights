using UnityEngine;

/// <summary>
/// The right knight's stock special, unchanged: heals the caller and, by less,
/// the other knight. A data front door onto the existing HealSpecial component.
/// </summary>
[CreateAssetMenu(fileName = "Field Mending", menuName = "Equipment/Special/Field Mending")]
public class FieldMendingSpecial : SpecialDefinition
{
    public override void Activate(GameObject knight, string playerTag)
    {
        var heal = knight.GetComponent<HealSpecial>();
        if (heal == null)
        {
            Debug.LogWarning($"[FieldMendingSpecial] {playerTag} has no HealSpecial component.");
            return;
        }
        heal.ActivateHeal(playerTag);
    }
}
