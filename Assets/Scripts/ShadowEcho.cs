using UnityEngine;

/// <summary>
/// Says a hit came from one of the Shadow Order's echoes rather than from the
/// knight's own arrow or swing, so the number over the body can be printed in
/// purple (owner, 2026-10-13) — see EnemyBase.TakeDamage.
///
/// There are three echoes. A shadow arrow and a shuriken already say so
/// themselves, through PlayerProjectile.IsEcho. A Phantom Blade echo has no
/// projectile: SwordSwing credits a sword hit through a stand-in object, and
/// hangs this on it while an echo is arcing.
/// </summary>
public class ShadowEcho : MonoBehaviour
{
    /// <summary>Whether the thing a hit is credited to is a shadow arrow, a shuriken or a Phantom Blade echo.</summary>
    public static bool Is(GameObject source)
    {
        if (source == null) return false;

        PlayerProjectile shot = source.GetComponent<PlayerProjectile>();

        // A mirror's copy is an echo for the special bar, but it prints the
        // standard red (owner, 2026-10-27) — even a copy of a shadow arrow
        if (shot != null && shot.isMirrorCopy) return false;

        if (shot != null && shot.IsEcho) return true;

        return source.GetComponent<ShadowEcho>() != null;
    }
}
