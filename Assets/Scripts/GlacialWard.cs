using System.Collections.Generic;
using UnityEngine;

// Glacial Ward (Frigid Order): a standing ring of cold around a knight.
//
// This is the most Two Knights upgrade in the Order, and the reason it is one of
// the two Common doors. The knights cannot move, so an aura is a real, readable,
// stationary piece of the board rather than something the player has to carry
// around - and it needs no aim, which makes it the entry for someone who drafted
// Frigid without meaning to.
//
// The ward CHILLS and never freezes. That is pillar 2 of the Order and it is not
// negotiable: an aura that could freeze would lock the board down on a timer with
// nobody deciding anything, and every statue on the field is supposed to be one
// the player chose to make.
[DisallowMultipleComponent]
public class GlacialWard : MonoBehaviour
{
    private FrigidBoost _boost;
    private ParticleSystem _ring;
    private float _nextSweep;
    private float _shownRadius = -1f;

    // Middle of the knight rather than the transform at their feet — see
    // FrostFx.AttachWardRing. Read off the body collider so it stays right if the
    // knight's art is ever redrawn taller or shorter.
    private Vector3 _centerOffset;

    // Reused across sweeps rather than allocated each time. The ward runs on every
    // frame of every wave for the rest of the run, so a fresh array four times a
    // second is a garbage collection the player can feel.
    private static readonly Collider2D[] Hits = new Collider2D[64];

    // Every ward on the board, for the Frost Order quest that counts freezes made
    // inside a ring (see Encloses).
    private static readonly List<GlacialWard> Live = new List<GlacialWard>();

    private void Awake()
    {
        _boost = GetComponent<FrigidBoost>();

        Collider2D body = GetComponent<Collider2D>();
        _centerOffset = body != null ? new Vector3(0f, body.offset.y, 0f) : Vector3.zero;
    }

    private void OnEnable() { Live.Add(this); }
    private void OnDisable() { Live.Remove(this); }

    /// <summary>
    /// Whether this body is standing in either knight's frost ring right now. The
    /// same reach the sweep uses: a body counts once any part of it is over the
    /// ring, not only its middle.
    /// </summary>
    public static bool Encloses(EnemyBase enemy)
    {
        if (enemy == null || UpgradeHold.Held) return false;
        Collider2D body = enemy.GetComponent<Collider2D>();

        for (int i = 0; i < Live.Count; i++)
        {
            GlacialWard ward = Live[i];
            if (ward == null || ward._boost == null) continue;
            float radius = ward._boost.WardRadius;
            if (radius <= 0f) continue;

            Vector2 center = ward.transform.TransformPoint(ward._centerOffset);
            Vector2 nearest = body != null ? body.ClosestPoint(center) : (Vector2)enemy.transform.position;
            if ((nearest - center).sqrMagnitude <= radius * radius) return true;
        }
        return false;
    }

    private void Update()
    {
        if (_boost == null) return;

        float radius = _boost.WardRadius;
        if (radius <= 0f) return;

        // Upgrades held (cart sorting): the ring goes with the rest and comes back
        // when they do.
        bool shown = !UpgradeHold.Held;
        if (_ring != null && _ring.gameObject.activeSelf != shown) _ring.gameObject.SetActive(shown);
        if (!shown) return;

        // The tell, rebuilt only when the ring actually changes size - a knight
        // who takes rank two mid-run widens the ring they already have rather
        // than getting a second one.
        if (_ring == null)
        {
            _ring = FrostFx.AttachWardRing(gameObject, radius, _centerOffset);
            _shownRadius = radius;
        }
        else if (!Mathf.Approximately(_shownRadius, radius))
        {
            FrostFx.SetWardRadius(_ring, radius);
            _shownRadius = radius;
        }

        // One overlap query per knight per beat, not one per frame. The ward is
        // permanent, so its cost is paid for the whole run.
        if (Time.time < _nextSweep) return;
        _nextSweep = Time.time + FrigidBoost.WardTickInterval;

        Sweep(radius);
    }

    private void Sweep(float radius)
    {
        int count = Physics2D.OverlapCircleNonAlloc(transform.TransformPoint(_centerOffset), radius, Hits);
        bool slowsAmmo = _boost.WardSlowsProjectiles;

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = Hits[i];
            if (hit == null) continue;

            EnemyBase enemy = hit.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                // isBlow: false. The ward is not a hit the knight landed, so it
                // physically cannot freeze - the boost signs that promise, not a
                // comment here. Which is why the ring quest counts in
                // EnemyBase.Freeze instead: the knight's blow does the freezing,
                // the ring only decides whether it counts.
                _boost.TouchWithCold(enemy, false);
                continue;
            }

            // Everything else that moves and can be slowed: an orb drifting past,
            // and the ammunition on its way in — except rocks, which no rank slows
            // (owner, 2026-10-15; see ProjectileMovement).
            IChillable chillable = hit.GetComponent<IChillable>();
            if (chillable == null) continue;

            CollectibleOrb orb = hit.GetComponent<CollectibleOrb>();
            if (orb != null)
            {
                // An orb is shared ground: one knight buying the slow widens the
                // window for both, the same reading Sunwell III already has.
                _boost.TouchWithCold(chillable, _boost.ChillSpeedMultiplier);
            }
            else if (slowsAmmo)
            {
                // A flat half rather than the knight's own chill depth, because a
                // bomb at a tenth speed would simply hang in the air and stop
                // being a threat the player has to answer at all.
                _boost.TouchWithCold(chillable, FrigidBoost.WardProjectileSpeedMultiplier);
            }
        }
    }
}
