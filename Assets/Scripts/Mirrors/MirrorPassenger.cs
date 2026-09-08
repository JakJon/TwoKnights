using UnityEngine;

// A shot's memory of the pane it just came out of.
//
// The moment a projectile is redirected it is standing at the mouth of its twin,
// which is exactly where that twin's trigger is. Without this it would be caught
// again on the next frame, sent back where it came from, caught again, and what
// the player would see is an arrow vibrating between two mirrors instead of
// flying through them.
//
// MirrorPane also pushes the exit point clear of the twin's collider, so most of
// the time this never has to fire. It is here for the shot that comes out at a
// shallow angle and grazes the pane it was born from — geometry alone cannot rule
// that out, and the failure it causes is not subtle.
//
// Added to the projectile on its first pass rather than sitting on every arrow
// prefab: most shots never touch a mirror, and this way nothing outside the
// castle carries the component at all. It dies with the projectile.
public class MirrorPassenger : MonoBehaviour
{
    // Long enough to carry a shot off the pane it left at any sane speed, short
    // enough that a genuine second pass through the same pane still works — a
    // looping arrow re-enters its entry pane, not its exit pane, so it is never
    // the one being held off here.
    public const float Cooldown = 0.05f;

    private MirrorPane _cameOutOf;
    private float _clearAt;

    public static MirrorPassenger For(GameObject shot)
    {
        MirrorPassenger passenger = shot.GetComponent<MirrorPassenger>();
        return passenger != null ? passenger : shot.AddComponent<MirrorPassenger>();
    }

    /// <summary>Is <paramref name="pane"/> allowed to take this shot right now?</summary>
    public bool MayEnter(MirrorPane pane)
    {
        return pane != _cameOutOf || Time.time >= _clearAt;
    }

    /// <summary>Record that the shot has just been put down at <paramref name="exit"/>.</summary>
    public void NoteExit(MirrorPane exit)
    {
        _cameOutOf = exit;
        _clearAt = Time.time + Cooldown;
    }
}
