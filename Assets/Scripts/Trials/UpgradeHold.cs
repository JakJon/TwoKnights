using UnityEngine;

/// <summary>
/// Takes the knights' upgrades off them for a while, without un-buying any.
///
/// Nearly every upgrade is a component on the knight — PoisonTipBoost, EmberBoost,
/// LongSwordBoost and the rest — and the bow and the sword ask the knight for it
/// each time they are used. While <see cref="Held"/> is up, <see cref="Of{T}"/>
/// answers "this knight has none", so the bow fires one plain arrow at the reload
/// and speed it had before any upgrade, and the sword makes one plain swing.
/// Nothing is removed and no counter moves, so everything is back exactly as it
/// was the moment the hold comes down.
///
/// Cart sorting raises it (owner, 2026-10-13). The phases are written around a
/// plain knight: one arrow or one swing throws a lever once, and a sword takes a
/// second to come back and a bow a second and a half.
///
/// What it does NOT reach: the shape of the shield (Tower Shield, Curved Aegis),
/// which is rebuilt when the upgrade is bought and does nothing to a lever, and
/// equipment, which is not an upgrade.
/// </summary>
public static class UpgradeHold
{
    public static bool Held { get; set; }

    /// <summary>The knight's <typeparamref name="T"/>, or null while upgrades are held.</summary>
    public static T Of<T>(GameObject knight) where T : Component
    {
        return Held || knight == null ? null : knight.GetComponent<T>();
    }

    /// <summary>The same, asked by a component that sits on the knight.</summary>
    public static T Of<T>(Component onKnight) where T : Component
    {
        return Held || onKnight == null ? null : onKnight.GetComponent<T>();
    }
}
