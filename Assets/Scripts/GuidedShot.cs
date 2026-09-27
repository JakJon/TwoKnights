using UnityEngine;

// Guided Shot (Guardian): a shot that bends onto whatever it passes close to.
//
// Added at spawn ONLY when the firing knight owns the chain, so a run with no
// Guardian picks pays nothing per frame for this existing — the same early-out
// FrigidBoost.AnyKnightIsFrigid buys for the ward.
//
// It steers both families of projectile in the game, because Guided Reflections puts
// it on a rebounding rock as well as on an arrow, and the two are moved by different
// machinery (see MirrorPane, which had to learn the same distinction):
//
//   * the knight's arrow rides a Rigidbody2D velocity set by PlayerShooter;
//   * a reflected rock carries ProjectileMovement, which translates along its own
//     local +X, so its heading lives entirely in its rotation.
//
// Rotation is written for both — the arrow sprite is drawn pointing along +X, and the
// rock reads its rotation as its heading — and the velocity only when there is one.
//
// THE TARGET IS STICKY. Once a shot commits it does not shop around: it holds that
// body until it hits it or until the body stops existing. A shot that re-picked the
// nearest thing every frame would weave between two rats and hit neither, and worse,
// it would stop being something the player can predict. The one case that DOES
// re-acquire is the useful one: an arrow that pops a health orb is not destroyed by
// the orb (CollectibleOrb kills itself and lets the arrow fly on), so as soon as that
// orb is gone the arrow is free to find the next thing in front of it.
[DisallowMultipleComponent]
public class GuidedShot : MonoBehaviour
{
    // Whether this shot ever committed to a target, and whether it then arrived.
    // Only a shot that locked is a guided shot at all, so only those resolve into
    // the no-miss streak — an arrow that found nothing to chase was never aimed.
    private bool _locked;
    private bool _connected;

    /// <summary>Called by PlayerProjectile the once, when this shot lands.</summary>
    public void NoteConnected()
    {
        _connected = true;
    }

    private void OnDestroy()
    {
        // The end of the flight is the only moment that knows the answer. A lock
        // says the shot chose; this says whether the choice paid.
        if (_locked) GuidedAim.Resolve(_ownerTag, _connected);
    }

    private float _radius;
    private bool _chaseOrbs = true;
    private string _ownerTag;
    private Transform _target;
    private Collider2D _targetBody;
    private float _nextSearch;
    private bool _tellShown;

    // Reused across every search by every guided shot on the field, rather than
    // allocated per sweep. A knight with a fast bow has several of these in the air
    // at once and each one searches ten times a second.
    private static readonly Collider2D[] Hits = new Collider2D[64];

    // The lock chime is a per-shot event, and a fast bow lands several a second. One
    // global floor between plays keeps it a texture under the fight instead of a
    // rattle — the same treatment EnemyBase gives the burn tick.
    private const float TellCooldownSeconds = 0.12f;
    private static float _nextTellTime;

    private Rigidbody2D _body;

    /// <summary>Turn this shot into a guided one. Call once, at spawn.
    ///
    /// <paramref name="chaseOrbs"/> is false for a reflected rock: only an arrow can
    /// collect an orb, so a rock that chased one would be spending the Order's payout
    /// on something it physically cannot pick up.</summary>
    public void Configure(float radius, bool chaseOrbs = true, string ownerTag = null)
    {
        _radius = Mathf.Max(0f, radius);
        _chaseOrbs = chaseOrbs;
        _ownerTag = ownerTag;
    }

    // A reflected rock flies over empty carts and kegs (see
    // ProjectileSettings.ReflectedRockPassesOver), so it must not lock onto one
    // either — a sticky target it can never strike would circle the cart until the
    // rock timed out.
    private bool _isRock;

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _isRock = GetComponent<ProjectileSettings>() != null;
    }

    private void Update()
    {
        if (_radius <= 0f) return;

        // The target going null is the re-acquire door — a dead mob, a collected orb,
        // or anything else that has left the field.
        if (_target == null || (_targetBody != null && !_targetBody.enabled))
        {
            _target = null;
            _targetBody = null;
            Acquire();
        }

        if (_target == null) return;

        Steer();
    }

    private void Acquire()
    {
        if (Time.time < _nextSearch) return;
        _nextSearch = Time.time + GuardianBoost.GuidedSearchInterval;

        Vector2 here = transform.position;
        int count = Physics2D.OverlapCircleNonAlloc(here, _radius, Hits);

        float bestDistance = float.MaxValue;
        Transform best = null;
        Collider2D bestBody = null;

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = Hits[i];
            if (hit == null) continue;

            // Accepted BY COMPONENT, never by tag or layer. Nothing in this game is
            // tagged or layered usefully — mobs, orbs, rocks, bombs, fireballs and
            // mirror panes are all untagged triggers on the Default layer and every
            // one of them turns up in this sweep. Naming the two things a shot is
            // allowed to chase is what keeps an arrow from homing onto the enemy
            // ammunition flying past it.
            if (!IsChaseable(hit, _chaseOrbs, _isRock)) continue;

            float distance = ((Vector2)hit.bounds.center - here).sqrMagnitude;
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            best = hit.transform;
            bestBody = hit;
        }

        if (best == null) return;

        _target = best;
        _targetBody = bestBody;

        if (!_tellShown)
        {
            _tellShown = true;

            // Counted at the LOCK, once per shot, rather than on the hit. What the
            // Order sells is the shot committing to something; whether the body was
            // already dead when it arrived is a different question.
            PlayerStats.Increment("guardian.guided");
            GuardianAwakening.NoteUse(_ownerTag);
            _locked = true;

            // The rock's trail is built at three times the size: the rock prefab draws
            // at a quarter scale and parented art inherits the shrink, the same
            // correction ProjectileSettings.SizeTrail has to make for the powder
            // rock's flame. An arrow is already world-sized and wants the plain one.
            if (GetComponent<ProjectileMovement>() != null)
            {
                GuardianFx.AttachReflectTrail(gameObject);
            }
            else
            {
                GuardianFx.AttachGuidedTrail(gameObject);
            }

            if (AudioManager.Instance != null && Time.time >= _nextTellTime)
            {
                _nextTellTime = Time.time + TellCooldownSeconds;
                AudioManager.Instance.PlaySFX(AudioManager.Instance.guardianGuide);
            }
        }
    }

    private static bool IsChaseable(Collider2D hit, bool chaseOrbs, bool isRock)
    {
        EnemyBase enemy = hit.GetComponent<EnemyBase>();
        if (enemy != null) return !enemy.IsDead && !(isRock && ProjectileSettings.ReflectedRockPassesOver(enemy));

        return chaseOrbs && hit.GetComponent<CollectibleOrb>() != null;
    }

    private void Steer()
    {
        // The body's middle, not its transform — enemy pivots sit at the feet, so
        // aiming at the transform sends a shot into the ground in front of a tall one.
        Vector2 aim = _targetBody != null ? (Vector2)_targetBody.bounds.center : (Vector2)_target.position;
        Vector2 toTarget = aim - (Vector2)transform.position;
        if (toTarget.sqrMagnitude < 1e-6f) return;

        float desired = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
        float current = transform.eulerAngles.z;
        float heading = Mathf.MoveTowardsAngle(
            current, desired, GuardianBoost.GuidedTurnDegreesPerSecond * Time.deltaTime);

        transform.rotation = Quaternion.AngleAxis(heading, Vector3.forward);

        // Speed is never touched, only direction — a guided shot arrives when an
        // unguided one would have. The rock steers on rotation alone, because
        // ProjectileMovement translates along its own local +X.
        if (_body != null)
        {
            float speed = _body.linearVelocity.magnitude;
            if (speed > 0.0001f)
            {
                float radians = heading * Mathf.Deg2Rad;
                _body.linearVelocity = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * speed;
            }
        }
    }
}
