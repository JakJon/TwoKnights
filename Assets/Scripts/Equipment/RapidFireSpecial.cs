using UnityEngine;

/// <summary>
/// The left knight's stock special, unchanged — this is only a data front door
/// onto the existing RapidFire component so the special can be listed, shown and
/// swapped like any other.
///
/// freezeGainSeconds stays 0 on this one: RapidFire.ActivateRapidFire already
/// calls FreezeSpecialGain itself, and duplicating it here would be one more
/// place to keep in step with its duration.
/// </summary>
[CreateAssetMenu(fileName = "Rapid Fire", menuName = "Equipment/Special/Rapid Fire")]
public class RapidFireSpecial : SpecialDefinition
{
    public override void Activate(GameObject knight, string playerTag)
    {
        var rapidFire = knight.GetComponent<RapidFire>();
        if (rapidFire == null)
        {
            Debug.LogWarning($"[RapidFireSpecial] {playerTag} has no RapidFire component.");
            return;
        }
        rapidFire.ActivateRapidFire(playerTag);
    }
}
