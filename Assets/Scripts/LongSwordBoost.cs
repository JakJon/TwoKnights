using UnityEngine;

// Long Sword: more reach and more damage, paid for in swing speed. Lives on the
// knight (like every other boost) rather than on the sword object, because the
// sword attack is spawned by ShieldOrbit and would lose the upgrade every time
// it was rebuilt.
//
// Length and slowness COMPOUND across ranks - each rank multiplies what the one
// before it left - while base damage is set outright. That is what makes the
// second rank read as the same trade taken twice rather than as a different
// upgrade.
//
// The per-rank length figures are NOT round numbers, and that is deliberate. Only
// the blade scales: SwordSwing pins the hilt at a fixed arc radius of 1 and grows
// the sprite outward from there, so tip distance is 1 + 0.75 * LengthMultiplier.
// The multipliers are solved backwards from that so each rank puts the tip 30%
// further out than the rank before would have - see SwordSwing.AnimateSwingArc.
public class LongSwordBoost : MonoBehaviour
{
    private float _lengthMultiplier = 1f;
    private float _slowMultiplier = 1f;
    private int _baseDamage;

    /// <summary>How much longer the blade is than stock. 1 = unchanged.</summary>
    public float LengthMultiplier => _lengthMultiplier;

    /// <summary>Swing arc duration is multiplied by this. Above 1 is slower.</summary>
    public float SlowMultiplier => _slowMultiplier;

    /// <summary>Replacement base damage per swing, or 0 to keep the sword's own.</summary>
    public int BaseDamage => _baseDamage;

    public void AddRank(float lengthMultiplier, float slowMultiplier, int baseDamage)
    {
        _lengthMultiplier *= Mathf.Max(0.01f, lengthMultiplier);
        _slowMultiplier *= Mathf.Max(0.01f, slowMultiplier);
        if (baseDamage > 0) _baseDamage = baseDamage;
    }
}
