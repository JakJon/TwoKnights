using UnityEngine;

/// <summary>
/// Heavy Swing (Guardian): a body the sword killed, thrown backward, away from the
/// knight.
///
/// It is a COPY of the enemy's sprite, not the enemy. Enemies are destroyed the frame
/// they die (EnemyBase.OnDeath), so by the time this exists everything the death pays
/// out — gold, the kill, the special, a slime's split — has already happened where
/// the body fell. What flies is the picture of the body and the hit it carries.
///
/// It spins as it goes and strikes the first live enemy it touches, then is gone. If
/// that hit kills, the enemy it killed is launched in turn and carries on the same
/// way (owner, 2026-10-15: chain reactions are wanted). If it touches nothing it flies
/// until its sprite is off screen.
///
/// Guided Heavy Swing bends it onto the nearest live enemy within Guided Shot II's
/// range. That is its own steering rather than a GuidedShot component, because
/// GuidedShot counts every lock toward the Guided Shot tallies and the no-miss streak,
/// and a thrown corpse is not an arrow the knight aimed.
///
/// Empty carts and kegs are flown over, the same rule a reflected rock follows (see
/// ProjectileSettings.ReflectedRockPassesOver): they are scenery on the rail, and a
/// keg going off is the player's shot to take.
/// </summary>
public class LaunchedBody : MonoBehaviour
{
    // World units per second. Fast enough to read as a blow rather than a drift: a
    // body thrown from beside a knight clears the field in about a second.
    private const float Speed = 9f;

    // Degrees per second of tumble, signed by the direction of travel like the
    // gnomes' pickaxe, so a body thrown to the left spins anticlockwise.
    private const float SpinDegreesPerSecond = 900f;

    // Backstop only. ScreenEdge ends the flight; this ends it when there is no
    // camera to ask, or a body somehow never leaves the view.
    private const float MaxLifetimeSeconds = 4f;

    // How far from the middle of the body a touch counts, taken from the size of the
    // enemy it was and kept inside these so a rat and an ogre both read fairly
    private const float MinHitRadius = 0.15f;
    private const float MaxHitRadius = 0.5f;

    // Enemy colliders are triggers, so the filter has to ask for them (as the Acid
    // Dagger's does). Shared by every body in the air.
    private static readonly Collider2D[] Hits = new Collider2D[32];
    private static readonly ContactFilter2D HitFilter = new ContactFilter2D { useTriggers = true };

    private Vector2 _heading;
    private int _damage;
    private string _ownerTag;
    private float _guideRadius;
    private float _hitRadius;
    private float _spinSign;
    private Transform _spin;
    private SpriteRenderer _renderer;
    private bool _spent;

    // Guided Heavy Swing. Sticky once chosen, for GuidedShot's reason: a body that
    // re-picked the nearest enemy every frame would weave between two and hit neither.
    private EnemyBase _target;
    private Collider2D _targetBody;
    private float _nextSearch;

    /// <summary>
    /// Throw <paramref name="enemy"/>'s body along <paramref name="direction"/>. Call it
    /// the frame the enemy died, while its sprite still exists. Does nothing for a
    /// body that cannot be launched (a boss, a cart) or has no sprite to copy.
    /// </summary>
    /// <param name="ownerTag">"PlayerLeft"/"PlayerRight" — who is credited for what it hits.</param>
    /// <param name="damage">What it hits for; the knight's Heavy Swing rank sets this.</param>
    /// <param name="guideRadius">Guided Heavy Swing's steering range, or zero.</param>
    public static LaunchedBody Launch(EnemyBase enemy, Vector2 direction, string ownerTag,
                                      int damage, float guideRadius)
    {
        if (enemy == null || !enemy.CanBeLaunched) return null;

        SpriteRenderer source = enemy.GetComponent<SpriteRenderer>();
        if (source == null || source.sprite == null || !source.enabled) return null;

        if (direction.sqrMagnitude < 1e-6f) direction = Vector2.up;
        direction.Normalize();

        // Spun about the middle of the body, not the pivot — enemy pivots sit at the
        // feet, and a body spun about its feet would swing round in a wide circle
        Collider2D collider = enemy.GetComponent<Collider2D>();
        Bounds bodyBounds = collider != null ? collider.bounds : source.bounds;
        Vector2 centre = bodyBounds.center;

        var root = new GameObject("LaunchedBody");
        root.transform.position = centre;
        // ArenaSweep clears these before a scene, so a body still in the air when the
        // wave ends does not hang there through the conversation
        root.AddComponent<TransientEffect>();

        Transform spin = new GameObject("Spin").transform;
        spin.SetParent(root.transform, false);

        // Placed in world space, so the copy appears exactly where the body was drawn
        // and then turns about the root
        var art = new GameObject("Body");
        art.transform.SetParent(spin, false);
        art.transform.position = source.transform.position;
        art.transform.rotation = source.transform.rotation;
        art.transform.localScale = source.transform.lossyScale;

        // Drawn as the enemy was: same frame, same facing, same layer, and the same
        // material so the arena's lights fall on it the same way
        var copy = art.AddComponent<SpriteRenderer>();
        copy.sprite = source.sprite;
        copy.sharedMaterial = source.sharedMaterial;
        copy.color = source.color;
        copy.flipX = source.flipX;
        copy.flipY = source.flipY;
        copy.sortingLayerID = source.sortingLayerID;
        copy.sortingOrder = source.sortingOrder;

        // The enemy is destroyed at the end of this frame. Hidden now, so the copy is
        // the only body on screen for the frame the two share.
        source.enabled = false;

        var launched = root.AddComponent<LaunchedBody>();
        launched._heading = direction;
        launched._damage = Mathf.Max(1, damage);
        launched._ownerTag = ownerTag;
        launched._guideRadius = Mathf.Max(0f, guideRadius);
        launched._hitRadius = Mathf.Clamp(Mathf.Min(bodyBounds.extents.x, bodyBounds.extents.y),
                                          MinHitRadius, MaxHitRadius);
        launched._spinSign = direction.x >= 0f ? -1f : 1f;
        launched._spin = spin;
        launched._renderer = copy;

        // Bulwark's shove puff, pointed the way the body went: the same Order, the
        // same idea of a body being thrown off the knight
        GuardianFx.ShoveBurst(centre, direction);

        Destroy(root, MaxLifetimeSeconds);
        return launched;
    }

    private void Update()
    {
        if (_spent) return;

        float dt = Time.deltaTime;
        if (_guideRadius > 0f) Steer(dt);

        transform.position += (Vector3)(_heading * (Speed * dt));
        _spin.Rotate(0f, 0f, _spinSign * SpinDegreesPerSecond * dt);

        if (TryStrike()) return;

        if (_renderer != null && ScreenEdge.IsPast(_renderer.bounds))
        {
            Destroy(gameObject);
        }
    }

    private bool TryStrike()
    {
        int count = Physics2D.OverlapCircle(transform.position, _hitRadius, HitFilter, Hits);
        for (int i = 0; i < count; i++)
        {
            EnemyBase enemy = Hits[i] != null ? Hits[i].GetComponent<EnemyBase>() : null;
            if (!CanStrike(enemy)) continue;

            // A carried guard stops a thrown body like any other shot
            if (enemy.TryBlockShot(transform.position, _heading))
            {
                _spent = true;
                Destroy(gameObject);
                return true;
            }

            Strike(enemy);
            return true;
        }
        return false;
    }

    // The body it came from is dead, so this also stops it striking itself on the
    // frame it leaves
    private static bool CanStrike(EnemyBase enemy)
    {
        return enemy != null && !enemy.IsDead && !ProjectileSettings.ReflectedRockPassesOver(enemy);
    }

    private void Strike(EnemyBase enemy)
    {
        _spent = true;

        // TakeDamage reads the projectile only for its TAG, to credit a knight — the
        // same carrier idiom the sword and a reflected rock use
        GameObject carrier = null;
        if (!string.IsNullOrEmpty(_ownerTag))
        {
            carrier = new GameObject("LaunchedBodyHit");
            carrier.tag = _ownerTag + "Projectile";
        }

        enemy.TakeDamage(EquipmentBoost.ScaleHit(_damage, enemy, _ownerTag), carrier);
        if (carrier != null) Destroy(carrier);

        GuardianFx.ShoveBurst(transform.position, _heading);

        // A kill throws that body on in turn, the way this one was going
        if (enemy.IsDead) Launch(enemy, _heading, _ownerTag, _damage, _guideRadius);

        Destroy(gameObject);
    }

    private void Steer(float dt)
    {
        if (_target == null || _target.IsDead || _targetBody == null || !_targetBody.enabled)
        {
            _target = null;
            _targetBody = null;
            Acquire();
            if (_target == null) return;
        }

        // The body's middle, not its transform — enemy pivots sit at the feet
        Vector2 toTarget = (Vector2)_targetBody.bounds.center - (Vector2)transform.position;
        if (toTarget.sqrMagnitude < 1e-6f) return;

        float current = Mathf.Atan2(_heading.y, _heading.x) * Mathf.Rad2Deg;
        float desired = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
        float heading = Mathf.MoveTowardsAngle(current, desired,
                                               GuardianBoost.GuidedTurnDegreesPerSecond * dt) * Mathf.Deg2Rad;
        _heading = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));
    }

    // Searched on GuidedShot's beat rather than every frame; the steering itself runs
    // every frame
    private void Acquire()
    {
        if (Time.time < _nextSearch) return;
        _nextSearch = Time.time + GuardianBoost.GuidedSearchInterval;

        Vector2 here = transform.position;
        int count = Physics2D.OverlapCircle(here, _guideRadius, HitFilter, Hits);

        float bestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = Hits[i];
            EnemyBase enemy = hit != null ? hit.GetComponent<EnemyBase>() : null;
            if (!CanStrike(enemy)) continue;

            float distance = ((Vector2)hit.bounds.center - here).sqrMagnitude;
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            _target = enemy;
            _targetBody = hit;
        }
    }
}
