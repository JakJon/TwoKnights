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
// unit high or a unit low catches neither. So the answer to a bomb is to hit it
// early — one point of health — with an arrow or the sword, which sends it rolling
// back the way it came on a short fuse, to go off on whatever enemy it reaches or
// where the fuse runs out (see RollBack). A bomb nobody touches goes off where it
// wanted to.
//
// It is a projectile, not an enemy: no EnemyBase, no special, no gold, no entry
// in the wave's kill list. Hitting one buys time and turns it on the gnomes, and
// the arrow flies on through it, which is the whole transaction. Follows EnemyFireball's shape (the
// giant slimes' ammunition) rather than the enemy shape, for the same reasons.
public class EnemyBomb : MonoBehaviour, IChillable
{
    [Header("Travel")]
    [Tooltip("World units per second towards the knights' level. Slow enough that a knight has about two seconds to shoot it before it enters the danger band.")]
    [SerializeField] private float travelSpeed = 1.26f;

    /// <summary>
    /// World units per second a bomb prefab drifts towards the knights' level, or
    /// 0 if it is not one. Lets a thrower work out when a bomb will go off before
    /// it lets go of it (IncomingLedger).
    /// </summary>
    public static float TravelSpeedOf(GameObject prefab)
    {
        EnemyBomb bomb = prefab != null ? prefab.GetComponent<EnemyBomb>() : null;
        return bomb != null ? bomb.travelSpeed : 0f;
    }

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

    // ---- Rolled back by a sword or an arrow ----
    //
    // A sword or an arrow does not set a bomb off. It sends it rolling back the way it
    // came (owner, 2026-10-15) — up towards the track it was thrown from, or down if
    // it was lobbed up — on a short fuse: it goes off when it touches an enemy or when
    // the fuse runs out, whichever is first, and is simply gone if it rolls off screen
    // before either. Its blast then hurts the enemies AND the knights in reach (owner,
    // 2026-10-15): sending it back is not a free answer, and a bomb hit too close to
    // the knights' level still catches them. Nothing the knights hold sets it off a
    // second time.

    // Consts, not inspector fields, on purpose. These were serialized fields first,
    // and this project recompiles without leaving play mode: a reload keeps the
    // value every loaded object already had, so the bomb went on using the first
    // fuse it was ever given and later changes to the default never reached it.
    // Change them here.

    // Degrees per second the bomb turns while rolling back
    private const float RollSpinDegreesPerSecond = 420f;

    // Seconds a bomb sent back by a sword or an arrow rolls before it goes off on
    // its own, if it has not touched an enemy first (owner, 2026-10-15)
    private const float RollFuseSeconds = 0.5f;

    private bool _rolling;
    private float _rollElapsed;
    private float _rollSpeed;
    private int _rollDamage;
    private string _rollOwnerTag;
    private Vector3 _rollCentreLocal; // the middle of the ball, in the bomb's own space
    private Renderer _renderer;

    // Measured once at the bat and carried in local space, because the ball turns as
    // it rolls and the collider's bounds lag a physics step behind the transform
    private Vector2 RollCentre => transform.TransformPoint(_rollCentreLocal);

    private void RollBack(GuardianReflect.Turn bat)
    {
        _rolling = true;
        _travel = -_travel;

        // A Reflector rank still counts, as it does on everything a sword sends back
        _rollSpeed = travelSpeed * Mathf.Max(1f, bat.SpeedMultiplier);
        _rollDamage = Mathf.Max(1, Mathf.CeilToInt(knightDamage * bat.DamageMultiplier));
        _rollOwnerTag = bat.OwnerTag;
        _rollCentreLocal = transform.InverseTransformPoint(BodyCenter);
        _renderer = GetComponent<Renderer>();
    }

    private void Roll()
    {
        // The fuse. Scaled time, so a scene that holds the game still holds it too.
        _rollElapsed += Time.deltaTime;
        if (_rollElapsed >= RollFuseSeconds)
        {
            Explode();
            return;
        }

        float step = _travel * _rollSpeed * Time.deltaTime;
        transform.position += Vector3.up * step;

        // Turned about the middle of the ball, not the pivot at its base, so it rolls
        // in place rather than swinging round its own feet. Clockwise going up.
        transform.RotateAround(RollCentre, Vector3.forward,
                               -Mathf.Sign(_travel) * RollSpinDegreesPerSecond * Time.deltaTime);

        if (_renderer != null && ScreenEdge.IsPast(_renderer.bounds))
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (_rolling)
        {
            Roll();
            return;
        }

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

    // Every tool the knights hold stops a bomb, which is what "one hit point" means
    // here. An arrow or a sword sends it rolling back (the arrow flies on through);
    // the guard sets it off, or turns it around with Reflector.
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Unity still sends trigger messages to a DISABLED behaviour, and switching
        // this one off is how the guard's Reflector makes a bomb the knight's. Without
        // this a reflected bomb could still go off on the other knight's arrow.
        if (_exploded || !enabled) return;

        // Rolling back: it is looking for a body and nothing else. Empty carts and
        // kegs are rolled over, the same rule a reflected rock follows.
        if (_rolling)
        {
            EnemyBase struck = other.GetComponent<EnemyBase>();
            if (struck != null && !struck.IsDead && !ProjectileSettings.ReflectedRockPassesOver(struck))
            {
                // A carried guard takes the direct hit; the blast still goes off
                // round it, and passes over the body that blocked it
                _directHitBlocked = struck.TryBlockShot(gameObject);
                Explode(struck);
            }
            return;
        }

        // An arrow rolls it back and flies on (owner, 2026-10-15): the bomb is the one
        // piece of enemy ammunition an arrow sends back rather than pops
        GuardianReflect.Turn shot = GuardianReflect.TryArrowBat(other);
        if (shot.Happened)
        {
            RollBack(shot);
            return;
        }

        // The sword object is untagged by design (see SwordSwing); the detector it
        // grows at swing time is the reliable handle
        GuardianReflect.Turn bat = GuardianReflect.TryBat(other, BodyCenter);
        if (bat.Happened)
        {
            RollBack(bat);
            return;
        }

        if (other.CompareTag("Shield"))
        {
            // Reflector (Guardian): the guard bats it away instead of setting it off
            // in the one place its blast can reach. It travels straight up or straight
            // down, so that is the heading the mirror works from — and once it is the
            // knight's, the powder finally hurts the gnomes who packed it.
            GuardianReflect.Turn turn = GuardianReflect.TryTurn(other, Vector2.up * _travel);
            if (turn.Happened)
            {
                enabled = false;
                ReflectedShot.Attach(gameObject, turn, travelSpeed, knightDamage, blastRadius);
                return;
            }

            Explode();
            return;
        }

        if (other.CompareTag("PlayerLeft") || other.CompareTag("PlayerRight"))
        {
            Explode();
        }
    }

    /// <summary>Set it off where it stands. Safe to call twice.</summary>
    public void Explode()
    {
        Explode(null);
    }

    // Set when the body a rolled-back bomb ran into turned it on a carried guard
    private bool _directHitBlocked;

    // directHit is the body a rolled-back bomb ran into, or null
    private void Explode(EnemyBase directHit)
    {
        if (_exploded) return;
        _exploded = true;

        Vector2 center = _rolling ? RollCentre : BodyCenter;

        // A rolled-back bomb hurts both sides; one that arrived on its own, only the
        // knights. The knights take the bomb's own damage and poison either way —
        // Reflector's multiplier is for what the knight sends at the enemy.
        if (_rolling) BlastEnemies(center, directHit);
        BlastKnights(center);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.fireballExplode);
        }

        BombFx.Explode(center, blastRadius);
        Destroy(gameObject);
    }

    // A rolled-back bomb's blast is turned on the enemies as well as the knights
    // (BlastKnights runs beside this). The body it ran into takes the full hit
    // through TakeDamage — that is the aimed part, and carts answer to it — and
    // everything else in the radius takes the blast. Empty carts caught in it break
    // too (owner, 2026-10-15) even though the bomb rolls over them on its way;
    // kegs still shrug it off. The same split a bomb the guard turned around makes
    // (see ReflectedShot).
    private void BlastEnemies(Vector2 center, EnemyBase directHit)
    {
        if (directHit != null && !directHit.IsDead && !_directHitBlocked)
        {
            // TakeDamage reads the projectile only for its TAG, to credit a knight
            GameObject carrier = null;
            if (!string.IsNullOrEmpty(_rollOwnerTag))
            {
                carrier = new GameObject("RolledBomb");
                carrier.tag = _rollOwnerTag + "Projectile";
            }

            directHit.TakeDamage(EquipmentBoost.ScaleHit(_rollDamage, directHit, _rollOwnerTag), carrier);
            if (carrier != null) Destroy(carrier);
        }

        // Collected first and paid second: an enemy dying inside the loop can recurse
        // through OnDeath and mutate what the query handed back
        var victims = new List<EnemyBase>();
        foreach (Collider2D col in Physics2D.OverlapCircleAll(center, blastRadius))
        {
            EnemyBase other = col != null ? col.GetComponent<EnemyBase>() : null;
            if (other == null || other == directHit || other.IsDead) continue;
            if (!victims.Contains(other)) victims.Add(other);
        }

        for (int i = 0; i < victims.Count; i++)
        {
            victims[i].ApplyBlastDamage(EquipmentBoost.ScaleHit(_rollDamage, victims[i], _rollOwnerTag),
                                        _rollOwnerTag, true);
        }
    }

    private void BlastKnights(Vector2 center)
    {
        // A bomb nobody sent back hurts only the knights — it is the gnome's bomb,
        // and a blast that cleared the gnome's own carts off the track would do the
        // player's work. A rolled-back one also reaches enemies (BlastEnemies).
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
