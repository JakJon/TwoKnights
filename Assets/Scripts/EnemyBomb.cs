using System.Collections.Generic;
using UnityEngine;

// What a gnome mine cart throws. It travels straight along the line between the
// two knights — down from the track above them, up from the track below — and
// detonates when it reaches their level, perfectly centred on both axes, which
// is the only place its blast can reach either of them.
//
// That geometry IS the mechanic. The knights stand a fixed distance either side
// of the centre line, and the blast radius is tuned to just barely span that gap:
// a bomb that goes off level with them catches both, and a bomb that goes off a
// unit high or a unit low catches neither. So the answer to a bomb is to shoot it
// early — one point of health, any hit sets it off — and the punishment for a slow
// shot is not "you missed" but "you set it off where it wanted to be anyway".
//
// It is a projectile, not an enemy: no EnemyBase, no special, no gold, no entry
// in the wave's kill list. Shooting one down costs an arrow and buys time, which
// is the whole transaction. Follows EnemyFireball's shape (the giant slimes'
// ammunition) rather than the enemy shape, for the same reasons.
public class EnemyBomb : MonoBehaviour, IChillable
{
    [Header("Travel")]
    [Tooltip("World units per second towards the knights' level. Slow enough that a knight has about two seconds to shoot it before it enters the danger band.")]
    [SerializeField] private float travelSpeed = 1.26f;

    [Tooltip("Despawn below this world y, in case the knights are gone and nothing stops it")]
    [SerializeField] private float floorY = -8f;

    [Tooltip("The same backstop overhead, for one thrown up from the track below the knights")]
    [SerializeField] private float ceilingY = 8f;

    [Header("Blast")]
    [Tooltip("Damage radius. Must just span the gap to a knight — see the class note; widening this removes the timing puzzle.")]
    [SerializeField] private float blastRadius = 1.95f;

    [Tooltip("Dealt to every knight inside the blast. A centred bomb catches both.")]
    [SerializeField] private int knightDamage = 15;

    [Tooltip("Shown on the death screen")]
    [SerializeField] private string sourceName = "a Gnome's Bomb";

    [Header("Poison")]
    [Tooltip("The green bomb. On top of the blast, every knight it catches starts rotting — see KnightPoison. The blast damage is unchanged: the poison is the whole difference, and it is a debt rather than a hit.")]
    [SerializeField] private bool poisons;

    private bool _exploded;
    private Collider2D _body;

    // -1 for one rolled over the side from above, +1 for one lobbed up from the
    // track below. Decided once, at the throw, rather than steered every frame:
    // it is a thrown object, and one that re-aimed itself in flight would arrive
    // at the knights' level no matter what the player did to it on the way.
    private float _travel = -1f;

    // The bomb's pivot is at its base, like every sprite here. Everything about
    // the blast — where it goes off, when it decides it is level with the knights —
    // has to be measured from the middle of the fuse-lit ball instead, or the
    // whole thing sits half a unit low and the tuning below lies.
    private Vector2 BodyCenter
    {
        get
        {
            if (_body == null) _body = GetComponent<Collider2D>();
            return _body != null ? (Vector2)_body.bounds.center : (Vector2)transform.position;
        }
    }

    private void Start()
    {
        // Aimed at the knights' level from wherever the cart let it go. With no
        // knights to aim at it falls, which is what it has always done and what
        // the floorY backstop below is written for.
        float midY;
        _travel = TryKnightMidY(out midY) && BodyCenter.y < midY ? 1f : -1f;

        // A wave is not over while its ordnance is still in the air — the same
        // bookkeeping ProjectileSettings and EnemyFireball do
        BaseWave.RegisterProjectile(gameObject);
    }

    private void OnDestroy()
    {
        BaseWave.UnregisterProjectile(gameObject);
    }


    // Glacial Ward II: enemy ammunition crossing a knight's ring of cold loses
    // half its speed. See IChillable.
    private float _chillMultiplier = 1f;
    private float _chillUntil = -1f;

    public void ApplyChill(float speedMultiplier, float seconds)
    {
        if (seconds <= 0f) return;
        _chillMultiplier = Mathf.Clamp(Mathf.Min(_chillMultiplier, speedMultiplier), 0.05f, 1f);
        _chillUntil = Mathf.Max(_chillUntil, Time.time + seconds);
    }

    private float ChillScale { get { return Time.time < _chillUntil ? _chillMultiplier : 1f; } }

    private void Update()
    {
        transform.position += Vector3.up * (_travel * travelSpeed * ChillScale * Time.deltaTime);

        float midY;
        if (TryKnightMidY(out midY))
        {
            // Arrived at their level, approached from whichever side it started
            bool arrived = _travel > 0f ? BodyCenter.y >= midY : BodyCenter.y <= midY;
            if (arrived) Explode();
            return;
        }

        float limit = _travel > 0f ? ceilingY : floorY;
        if (_travel > 0f ? transform.position.y > limit : transform.position.y < limit)
        {
            Destroy(gameObject);
        }
    }

    // Every tool the knights hold sets a bomb off, which is what "one hit point"
    // means here. Mirrors EnemyFireball: the arrow is destroyed on the way in.
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_exploded) return;

        if (other.CompareTag("PlayerLeftProjectile") || other.CompareTag("PlayerRightProjectile"))
        {
            Destroy(other.gameObject);
            Explode();
            return;
        }

        // The sword object is untagged by design (see SwordSwing); the detector it
        // grows at swing time is the reliable handle
        if (other.GetComponent<SwordDamageDetector>() != null)
        {
            Explode();
            return;
        }

        if (other.CompareTag("Shield")
            || other.CompareTag("PlayerLeft")
            || other.CompareTag("PlayerRight"))
        {
            Explode();
        }
    }

    /// <summary>Set it off where it stands. Safe to call twice.</summary>
    public void Explode()
    {
        if (_exploded) return;
        _exploded = true;

        Vector2 center = BodyCenter;

        // Only the knights are hurt — it is the gnome's bomb, and a blast that
        // cleared the gnome's own carts off the track would do the player's work
        var hit = new HashSet<PlayerHealth>();
        foreach (var col in Physics2D.OverlapCircleAll(center, blastRadius))
        {
            if (!col.CompareTag("PlayerLeft") && !col.CompareTag("PlayerRight")) continue;

            PlayerHealth knight = col.GetComponent<PlayerHealth>();
            if (knight != null) hit.Add(knight);
        }

        foreach (var knight in hit)
        {
            knight.TakeDamage(knightDamage, sourceName, DamageKind.Blast);

            // After the damage, not before: TakeDamage is what runs the death
            // path, and poisoning a knight the blast has just killed would leave a
            // timer ticking on a run that is already over.
            if (poisons) KnightPoison.Apply(knight.gameObject, sourceName);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.fireballExplode);
        }

        BombFx.Explode(center, blastRadius);
        Destroy(gameObject);
    }

    // Halfway between the knights' bodies, read off their colliders rather than
    // their transforms — the knight pivot is at the feet, and a bomb that stopped
    // at ankle height would sit below its own blast window.
    private bool TryKnightMidY(out float midY)
    {
        midY = 0f;
        GameObject left = GameObject.FindWithTag("PlayerLeft");
        GameObject right = GameObject.FindWithTag("PlayerRight");
        if (left == null || right == null) return false;

        midY = (BodyY(left) + BodyY(right)) * 0.5f;
        return true;
    }

    private static float BodyY(GameObject knight)
    {
        Collider2D body = knight.GetComponent<Collider2D>();
        return body != null ? body.bounds.center.y : knight.transform.position.y;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.7f);
        Gizmos.DrawWireSphere(BodyCenter, blastRadius);
    }
#endif
}
