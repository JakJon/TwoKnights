using UnityEngine;

// One castle pillar: a solid block on the floor that stops shots — every kind
// EXCEPT the enemies' rocks, which fly over it (owner, 2026-10-26). It deals no
// damage and cannot be broken. Its job is to close off the knights' line of fire
// so that the mirrors become the way round it.
//
// Stopped: all the knights' shots (arrows, shadow arrows, fire arrows,
// shurikens), fireballs, pickaxes, bombs and bat sonar. Passed: rocks
// (ProjectileMovement — Projectile_Rock is the only prefab that carries it).
//
// Walkers are not stopped by the collider (rats and wolves move by transform,
// not physics). Rats instead ask PillarField for a gap and walk through it.
//
// Built with a kinematic body and full kinematic contacts, the same as
// Mirror_Pane, so it hears kinematic shots as well as dynamic ones.
[RequireComponent(typeof(BoxCollider2D))]
public class CastlePillar : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;

        // A shuriken keeps its PlayerProjectile on a child, so the collider that
        // fired is not necessarily the object that flies. The body is.
        GameObject shot = other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject
            : other.gameObject;

        if (shot.GetComponent<ProjectileMovement>() != null) return; // rocks fly over

        if (IsShot(shot)) Destroy(shot);
    }

    private static bool IsShot(GameObject shot)
    {
        return shot.GetComponentInChildren<PlayerProjectile>() != null
               || shot.GetComponentInChildren<EnemyFireball>() != null
               || shot.GetComponentInChildren<EnemyPickaxe>() != null
               || shot.GetComponentInChildren<EnemyBomb>() != null
               || shot.GetComponentInChildren<SonarWave>() != null;
    }
}
