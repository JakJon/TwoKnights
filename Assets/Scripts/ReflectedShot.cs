using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a piece of enemy ammunition BECOMES once the guard has turned it around.
///
/// It rides on the object it was thrown as, so a returned pickaxe is still the same
/// pickaxe — same sprite, same collider, same size — and the player reads it by its
/// heading and its pace rather than by a new object appearing. The behaviour that
/// flew it in is switched off first, which is also what stops it hurting a knight or
/// being blocked a second time: those handlers live on the component that is now
/// disabled, and the only trigger left listening is this one, which looks for bodies
/// and nothing else.
///
/// Travel is along local +X, deliberately, because that is the idiom
/// ProjectileMovement uses and it is what GuidedShot steers — writing rotation is how
/// Guided Reflections bends a rebound, so anything that wants to be steerable has to
/// keep its heading in its rotation.
///
/// The rock does NOT use this. ProjectileSettings has a payload to carry — rot from a
/// green one, powder from a red one — and its own rebound path stays where it is;
/// what it shares with this is <see cref="GuardianReflect"/>, the roll and the mirror.
/// </summary>
[DisallowMultipleComponent]
public class ReflectedShot : MonoBehaviour
{
    private float _speed;
    private int _damage;
    private string _ownerTag;
    private float _blastRadius;
    private bool _spent;

    /// <summary>
    /// Turn <paramref name="host"/> into the knight's ammunition. The caller is
    /// responsible for switching off whatever was flying it first.
    /// </summary>
    /// <param name="speed">Its incoming pace. The Order's own multiplier is applied
    /// here rather than by the caller, so every rebound in the game leaves at the
    /// same relative pace.</param>
    /// <param name="damage">What it was going to hit a knight for. The blocking
    /// knight's Reflector rank multiplies it.</param>
    /// <param name="blastRadius">Non-zero only for something that went off on
    /// arrival anyway — a gnome's bomb. Its burst hurts bodies now, which is the one
    /// place in the game an enemy's powder does.</param>
    public static ReflectedShot Attach(GameObject host, GuardianReflect.Turn turn,
                                       float speed, int damage, float blastRadius = 0f)
    {
        if (host == null || !turn.Happened) return null;

        ReflectedShot shot = host.GetComponent<ReflectedShot>();
        if (shot == null) shot = host.AddComponent<ReflectedShot>();

        shot._speed = Mathf.Max(0.1f, speed) * GuardianBoost.ReflectedSpeedMultiplier;
        shot._damage = Mathf.Max(1, Mathf.CeilToInt(damage * turn.DamageMultiplier));
        shot._ownerTag = turn.OwnerTag;
        shot._blastRadius = Mathf.Max(0f, blastRadius);

        float degrees = Mathf.Atan2(turn.Heading.y, turn.Heading.x) * Mathf.Rad2Deg;
        host.transform.rotation = Quaternion.AngleAxis(degrees, Vector3.forward);

        // Guided Reflections: the rebound steers too. Bodies only — the same call
        // ProjectileSettings makes, and for the same reason: none of this can collect
        // an orb, so chasing one would send the Order's payout somewhere it cannot land.
        if (turn.GuideRadius > 0f)
        {
            GuidedShot guide = host.GetComponent<GuidedShot>();
            if (guide == null) guide = host.AddComponent<GuidedShot>();
            guide.Configure(turn.GuideRadius, false, turn.OwnerTag);
        }

        // NOT optional. Everything that reaches a guard was, until now, guaranteed to
        // die there or on a knight; a returned one can fly into open sky forever, and
        // a wave that tracks its projectiles would never end.
        Destroy(host, GuardianBoost.ReflectedLifetimeSeconds);
        return shot;
    }

    private void Update()
    {
        transform.Translate(Vector2.right * (_speed * Time.deltaTime));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_spent) return;

        EnemyBase struck = other.GetComponent<EnemyBase>();
        if (struck == null || struck.IsDead) return;

        _spent = true;
        Strike(struck);
    }

    private void Strike(EnemyBase enemy)
    {
        // TakeDamage reads the projectile only for its TAG, to credit a knight, so
        // this borrows SwordSwing's carrier idiom rather than retagging the object.
        // It must STAY untagged: EnemyBase destroys anything wearing a
        // Player*Projectile tag on contact, and its handler racing this one could eat
        // the rebound before the damage landed.
        GameObject carrier = null;
        if (!string.IsNullOrEmpty(_ownerTag))
        {
            carrier = new GameObject("ReflectedShot");
            carrier.tag = _ownerTag + "Projectile";
        }

        enemy.TakeDamage(EquipmentBoost.ScaleHit(_damage, enemy, _ownerTag), carrier);
        if (carrier != null) Destroy(carrier);

        Burst(enemy);
        Destroy(gameObject);
    }

    private void Burst(EnemyBase directHit)
    {
        if (_blastRadius <= 0f) return;

        BombFx.Explode(transform.position, _blastRadius);
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.fireballExplode);

        // Collected first and paid second: an enemy dying inside the loop can recurse
        // through OnDeath and mutate what the query handed back.
        var victims = new List<EnemyBase>();
        foreach (Collider2D col in Physics2D.OverlapCircleAll(transform.position, _blastRadius))
        {
            EnemyBase other = col != null ? col.GetComponent<EnemyBase>() : null;
            if (other == null || other == directHit || other.IsDead) continue;
            if (!victims.Contains(other)) victims.Add(other);
        }

        for (int i = 0; i < victims.Count; i++)
        {
            victims[i].ApplyBlastDamage(EquipmentBoost.ScaleHit(_damage, victims[i], _ownerTag), _ownerTag);
        }

        if (victims.Count + 1 >= 3) Feats.Record(Feats.ReflectThree);
    }
}
