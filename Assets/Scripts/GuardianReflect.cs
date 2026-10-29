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

        /// <summary>True when the guard turned it, false when a sword did. Only the
        /// guard's turns count toward the reflect quest: every knight can swing at a
        /// fireball, and a Guardian trial anyone could clear without the Order would
        /// not be testing the Order.</summary>
        public bool ByShield;
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
        turn.ByShield = true;

        PlayerStats.Increment("guardian.reflected");
        GuardianAwakening.NoteUse(turn.OwnerTag);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.guardianReflect);
        }

        return turn;
    }

    /// <summary>
    /// The sword's turn. Not a roll and not an upgrade: any knight whose blade meets a
    /// fireball, a pickaxe or a confusion ray sends it off the way the swing faces
    /// (owner, 2026-10-15). Happened is false only when <paramref name="other"/> is not
    /// a blade. Bombs ask for this too, for the owner and the multipliers, but roll
    /// back the way they came rather than taking the heading.
    ///
    /// A knight who owns Reflector gets its damage and pace on what the sword sends
    /// back as well, and Guided Reflections steers it. Without Reflector it leaves at
    /// its own pace and hits for what it would have done to the knight.
    ///
    /// <paramref name="at"/> is where the ammunition is, for the one case the swing
    /// has no facing yet: it then goes straight away from the knight.
    /// </summary>
    public static Turn TryBat(Collider2D other, Vector2 at)
    {
        Turn turn = default;
        SwordDamageDetector blade = other != null ? other.GetComponent<SwordDamageDetector>() : null;
        if (blade == null) return turn;

        GameObject knight = blade.OwningKnight;
        Vector2 heading = blade.SwingFacing;
        if (heading.sqrMagnitude < 1e-6f && knight != null) heading = at - (Vector2)knight.transform.position;
        if (heading.sqrMagnitude < 1e-6f) heading = Vector2.up;

        return SentBack(knight, knight != null ? knight.tag : null, heading);
    }

    /// <summary>
    /// The arrow's turn — asked by a BOMB and nothing else (owner, 2026-10-15). An
    /// arrow that meets a bomb sends it rolling back the way it came, as a sword does,
    /// and flies on; every other kind of ammunition is simply popped by an arrow.
    /// Happened is false when <paramref name="other"/> is not a knight's projectile.
    /// Shadow arrows, shurikens and the knight's own fireballs wear the same tag, so
    /// they count too. Heading is the arrow's, for completeness; a bomb ignores it.
    /// </summary>
    public static Turn TryArrowBat(Collider2D other)
    {
        if (other == null) return default;

        string ownerTag;
        if (other.CompareTag("PlayerLeftProjectile")) ownerTag = "PlayerLeft";
        else if (other.CompareTag("PlayerRightProjectile")) ownerTag = "PlayerRight";
        else return default;

        return SentBack(GameObject.FindWithTag(ownerTag), ownerTag, other.transform.right);
    }

    // Everything a knight sends back without the guard: their Reflector rank's damage
    // and pace if they own it, Guided Reflections' steering, and credit to them
    private static Turn SentBack(GameObject knight, string ownerTag, Vector2 heading)
    {
        GuardianBoost boost = UpgradeHold.Of<GuardianBoost>(knight);
        bool reflector = boost != null && boost.CanReflect;

        Turn turn = default;
        turn.Happened = true;
        turn.Heading = heading.sqrMagnitude > 1e-6f ? heading.normalized : Vector2.up;
        turn.DamageMultiplier = reflector ? boost.ReflectDamageMultiplier : 1f;
        turn.SpeedMultiplier = reflector ? boost.ReflectSpeedMultiplier : 1f;
        turn.GuideRadius = boost != null ? boost.GuidedReflectionRadius : 0f;
        turn.OwnerTag = ownerTag;
        turn.ByShield = false;

        // The guard's clang: steel met it, and the player should hear that it did
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.projectileShield);
        }

        return turn;
    }
}
