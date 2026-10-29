using UnityEngine;

// The mirrors' buff (owner, 2026-10-27): "it is too hard to be accurate with the
// mirrors", so whatever a knight sends through a pane comes out as TWO. The copy
// flies beside the shot, Offset units to the side, on whichever side of it faces
// the middle of the screen — the side the arena is on — and hits exactly as hard:
// it is a clone of the shot as it stands, damage, poison, fire, holy and all.
//
// Rules:
//   * Copies split too, with no limit on how many times (owner, 2026-10-27:
//     "chaos is fun") — but a shot does not split again until SplitCooldown (1 s)
//     after its last split, and a copy counts the split that made it. Without
//     that a copy put down by the glass was caught again a few frames later and
//     split, and its copy, and so on: a fan of arrows off one pane (owner's
//     screenshot, 2026-10-27). A knight's own shot splits on its first pass
//     whatever its age.
//   * The copy is an ECHO (PlayerProjectile.isMirrorCopy -> IsEcho): it does the
//     damage but pays no special. The bar counts shots the knight took, and the
//     mirror took this one. Its damage number is the standard red, not the
//     Shadow Order's purple (ShadowEcho.Is).
//   * It dies when the shot it was copied from would have — the knight's
//     projectile lifetime, counted from when that shot was fired.
public class MirrorCopy : MonoBehaviour
{
    /// <summary>How far to the side of the shot the copy flies.</summary>
    public const float Offset = 0.4f;

    /// <summary>A shot does not split again until this long after its last split
    /// (owner, 2026-10-27). The split that MADE a copy counts as the copy's last
    /// split, so a copy also has to be this old before it can split.</summary>
    public const float SplitCooldown = 1f;

    /// <summary>Whether the pane may split this shot now. Without the cooldown a
    /// copy put down by the glass could be caught again within a few frames and
    /// split, and its copy, and so on — a fan of arrows off one pane.</summary>
    public static bool MaySplit(GameObject shot)
    {
        PlayerProjectile projectile = shot.GetComponentInChildren<PlayerProjectile>();
        if (projectile == null) return false;
        return projectile.lastSplitAt < 0f || Time.time - projectile.lastSplitAt >= SplitCooldown;
    }

    private static void MarkSplit(GameObject shot)
    {
        foreach (PlayerProjectile projectile in shot.GetComponentsInChildren<PlayerProjectile>())
        {
            projectile.lastSplitAt = Time.time;
        }
    }

    private float _dieAt;

    /// <summary>Puts a copy of <paramref name="shot"/> beside it. Called by the
    /// pane once the shot itself has been put down at <paramref name="exit"/>
    /// travelling along <paramref name="heading"/>.</summary>
    public static GameObject Make(GameObject shot, Vector2 exit, Vector2 heading, float speed, MirrorPane cameOutOf)
    {
        // Square to the heading, on the side toward the middle of the screen. A
        // shot heading dead at the middle has no nearer side; it takes the side
        // anticlockwise of its heading.
        Vector2 across = new Vector2(-heading.y, heading.x);
        if (Vector2.Dot(across, -exit) < -0.0001f) across = -across;
        Vector2 at = exit + across * Offset;

        // Both the shot and its copy start their cooldown now
        MarkSplit(shot);
        GameObject copy = Instantiate(shot, new Vector3(at.x, at.y, shot.transform.position.z), shot.transform.rotation);
        copy.name = shot.name;

        Rigidbody2D body = copy.GetComponent<Rigidbody2D>();
        if (body != null) body.linearVelocity = heading * speed;

        foreach (PlayerProjectile projectile in copy.GetComponentsInChildren<PlayerProjectile>())
        {
            projectile.isMirrorCopy = true;
            // A shuriken's volley tally is for the fan the knight threw
            projectile.volley = null;
        }

        MirrorPassenger.For(copy).NoteExit(cameOutOf);

        // A copy of a copy arrives with the first copy's clock already on it (its
        // deadline is not serialized, so Instantiate hands it over empty) — wind
        // that one rather than adding a second
        MirrorCopy clock = copy.GetComponent<MirrorCopy>();
        if (clock == null) clock = copy.AddComponent<MirrorCopy>();
        clock._dieAt = Time.time + LifeLeft(shot);
        return copy;
    }

    // What is left of the original's flight: the firing knight's projectile
    // lifetime, less how long ago it was fired
    private static float LifeLeft(GameObject shot)
    {
        PlayerProjectile projectile = shot.GetComponentInChildren<PlayerProjectile>();
        float lifetime = 4f;
        float bornAt = Time.time;

        if (projectile != null)
        {
            if (projectile.bornAt >= 0f) bornAt = projectile.bornAt;

            string owner = projectile.CompareTag("PlayerLeftProjectile") ? "PlayerLeft"
                         : projectile.CompareTag("PlayerRightProjectile") ? "PlayerRight" : null;
            GameObject knight = owner != null ? GameObject.FindWithTag(owner) : null;
            PlayerShooter shooter = knight != null ? knight.GetComponent<PlayerShooter>() : null;
            if (shooter != null) lifetime = shooter.ProjectileLifetime;
        }

        return Mathf.Max(0.1f, lifetime - (Time.time - bornAt));
    }

    private void Update()
    {
        if (Time.time < _dieAt) return;

        // As PlayerShooter ends a shot's flight: a fireball that runs out burns
        // out rather than bursting
        FireballProjectile fireball = GetComponent<FireballProjectile>();
        if (fireball != null) fireball.MarkBurnedOut();
        ShotFade.Begin(gameObject);
        enabled = false;
    }
}
