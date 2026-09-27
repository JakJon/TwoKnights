using UnityEngine;

/// <summary>
/// The one door every reflect comes through, so the Guardian Order spends the same
/// rules on every kind of ammunition the mine and the forest throw.
///
/// It used to live inside ProjectileSettings, which meant the guard could only turn
/// around the ONE thing that happened to be a rock. A knight who bought the chain
/// then watched a pickaxe or a fireball land on the same guard and simply vanish,
/// which is not a rule anyone wrote down — it was where the code happened to be.
/// Now the roll, the mirror geometry and the credit all live here, and each kind of
/// ammunition only has to say how fast it was going and what it hits for.
/// </summary>
public static class GuardianReflect
{
    /// <summary>What a successful turn hands back. <see cref="Happened"/> false means
    /// the guard ate it as normal and the caller should do whatever it always did.</summary>
    public struct Turn
    {
        public bool Happened;

        /// <summary>Unit heading the ammunition leaves on.</summary>
        public Vector2 Heading;

        /// <summary>What the blocking knight's Reflector rank multiplies its damage by.</summary>
        public float DamageMultiplier;

        /// <summary>"PlayerLeft"/"PlayerRight" — who gets paid for what it does now.</summary>
        public string OwnerTag;

        /// <summary>Guided Reflections' steering radius, or zero.</summary>
        public float GuideRadius;

        /// <summary>What the blocking knight's Reflector rank multiplies its PACE by.
        /// Carried on the turn rather than read from a const, because the rank is
        /// what sets it and only the blocking knight knows their rank.</summary>
        public float SpeedMultiplier;
    }

    /// <summary>
    /// Ask the guard whether it sends this one back.
    ///
    /// <paramref name="incoming"/> is the direction of travel, taken from the
    /// ammunition's own movement rather than from its rotation: a rock's heading IS
    /// its rotation, but a pickaxe tumbles end over end on the way in and a fireball
    /// on the arc-then-home path is pointed wherever its last turn left it.
    /// </summary>
    public static Turn TryTurn(Collider2D shield, Vector2 incoming)
    {
        Turn turn = default;
        if (shield == null) return turn;

        GuardianBoost boost = shield.GetComponentInParent<GuardianBoost>();
        if (boost == null || !boost.CanReflect || !boost.ShouldReflect()) return turn;

        // The shield is positioned at knight + transform.right * orbitRadius, so its
        // local +X IS the outward face normal — no separate bookkeeping needed.
        Vector2 normal = ((Vector2)shield.transform.right).normalized;
        Vector2 outgoing = normal;

        if (incoming.sqrMagnitude > 1e-6f)
        {
            outgoing = Vector2.Reflect(incoming.normalized, normal);

            // A true mirror is the right read — angle the guard and you choose the
            // return line — but a graze can mirror to a heading that still points
            // inward, and something that set off back through its own knight would be
            // the Order paying out in damage. Anything not clearly heading away goes
            // straight out instead.
            if (Vector2.Dot(outgoing.normalized, normal) < 0.1f) outgoing = normal;
        }

        turn.Happened = true;
        turn.Heading = outgoing.normalized;
        turn.DamageMultiplier = boost.ReflectDamageMultiplier;
        turn.OwnerTag = boost.gameObject.tag;
        turn.GuideRadius = boost.GuidedReflectionRadius;
        turn.SpeedMultiplier = boost.ReflectSpeedMultiplier;

        PlayerStats.Increment("guardian.reflected");
        GuardianAwakening.NoteUse(turn.OwnerTag);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.guardianReflect);
        }

        return turn;
    }
}
