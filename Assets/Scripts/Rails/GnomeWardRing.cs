using System.Collections.Generic;
using UnityEngine;

// A ring of stone spinning around a gnome, and the only thing in the Mine that
// answers an arrow with an arrow's worth of nothing.
//
// Choo Choo was the map's simplest wave: carts come round, you shoot the riders,
// rock falls down the shafts on a timer. Every question it asked was "can you
// hit that", and the answer stopped being interesting the moment a knight had
// two damage upgrades. This is the second question — "can you hit that THROUGH
// this" — and it is asked with the same vocabulary the knights already speak,
// because a gnome behind a spinning guard is a knight behind a spinning shield.
//
// HOW IT IS MEANT TO BE BEATEN: not by grinding. The ring turns at a fixed rate,
// so the gaps between its stones sweep past the rider on a clock the player can
// read, and a shot released into a gap goes through untouched. Chipping the
// stones down works, but it is the slow answer — a stone comes back in its own
// slot a few seconds later, so a player who spends every arrow on the guard
// never gets to the gnome. The ring is a TIMING problem wearing a durability
// problem's clothes.
//
// THE TIER DIAL IS THE STONE COUNT, and it is a dial over the gaps rather than
// over the hit points. Stones are evenly spaced, so on a 0.9-unit ring four of
// them leave gaps well over a unit wide — a shot you can take without thinking —
// and eleven leave a quarter of a unit, which is a shot you have to aim into.
// Nothing else about the ring changes across the tiers.
//
// What it deliberately does NOT do is touch the knights. A ring that dealt
// contact damage would make eating it on the shield the way through, and
// shield-eating an enemy resets the special streak — so the wave would be
// teaching the one thing the whole game is built to punish (see the wave
// authoring rules). It is a barrier and nothing else: it stops arrows, and
// arrows stop it.
public class GnomeWardRing : MonoBehaviour
{
    private const string RingName = "WardRing";

    private float _degreesPerSecond;

    /// <summary>
    /// Hangs a ring on <paramref name="host"/>. Parented, so it rides whatever the
    /// host is riding and dies when the host does — a rider killed on the far
    /// straight does not leave his guard circling an empty seat.
    /// </summary>
    public static GnomeWardRing Attach(GameObject host, GameObject stonePrefab, int stones,
        float radius, float degreesPerSecond, float regrowSeconds)
    {
        if (host == null || stones <= 0) return null;
        if (host.transform.Find(RingName) != null) return null;

        Sprite sprite;
        float stoneRadius;
        ReadStone(stonePrefab, out sprite, out stoneRadius);
        if (sprite == null)
        {
            Debug.LogWarning("[GnomeWardRing] No stone sprite — the rider goes out unguarded.");
            return null;
        }

        var holder = new GameObject(RingName);
        holder.transform.SetParent(host.transform, false);
        holder.transform.localPosition = Vector3.zero;

        var ring = holder.AddComponent<GnomeWardRing>();
        ring._degreesPerSecond = degreesPerSecond;

        float step = 360f / stones;
        for (int i = 0; i < stones; i++)
        {
            WardStone.Build(holder.transform, sprite, stoneRadius,
                            Mathf.Max(0.1f, radius), step * i, Mathf.Max(0f, regrowSeconds));
        }

        return ring;
    }

    // The ring is one object and the stones are its children, so ONE rotation a
    // frame turns the whole guard. Turning each stone on its own orbit angle
    // would be the same picture and N times the arithmetic — and it would let
    // them drift apart, which is exactly what the even spacing is for.
    private void Update()
    {
        transform.Rotate(0f, 0f, _degreesPerSecond * Time.deltaTime);
    }

    // Sprite and hitbox are read off a prefab rather than authored here, so the
    // guard is always made of the same rock the shafts are throwing. Building the
    // stone from scratch instead of instantiating that prefab is deliberate: a
    // rock prefab carries ProjectileMovement and ProjectileSettings, and the
    // second of those registers with wave tracking — eleven of them circling a
    // gnome would hold the wave open forever.
    private static void ReadStone(GameObject prefab, out Sprite sprite, out float radius)
    {
        sprite = null;
        radius = 0.13f;
        if (prefab == null) return;

        var renderer = prefab.GetComponentInChildren<SpriteRenderer>();
        if (renderer != null) sprite = renderer.sprite;

        var circle = prefab.GetComponent<CircleCollider2D>();
        if (circle != null) radius = circle.radius;
    }
}

// One stone of the guard: a rock that sits in its slot, eats one arrow, and is
// back in the same slot a few seconds later.
public class WardStone : MonoBehaviour
{
    private float _regrowSeconds;
    private SpriteRenderer _renderer;
    private Collider2D _collider;
    private float _backAt = -1f;

    public static WardStone Build(Transform ring, Sprite sprite, float colliderRadius,
        float ringRadius, float angleDegrees, float regrowSeconds)
    {
        var go = new GameObject("WardStone");
        go.transform.SetParent(ring, false);

        float rad = angleDegrees * Mathf.Deg2Rad;
        go.transform.localPosition = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * ringRadius;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        // The rider sits on Default like everything else on the rails; the guard
        // draws just in front of him so it reads as being between the knight and
        // the gnome rather than behind him
        renderer.sortingLayerName = ring.GetComponentInParent<SpriteRenderer>() != null
            ? ring.GetComponentInParent<SpriteRenderer>().sortingLayerName
            : "Default";
        renderer.sortingOrder = 20;

        var circle = go.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
        circle.radius = colliderRadius;

        // Kinematic, simulated, moved by its parent's transform. Without a body of
        // its own a collider dragged around by a transform is a STATIC collider
        // being re-baked every frame — it still fires triggers, but it is the one
        // shape Unity asks you not to make, and eleven of them per rider is not
        // the place to find out how much that costs.
        var body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.simulated = true;

        var stone = go.AddComponent<WardStone>();
        stone._regrowSeconds = regrowSeconds;
        stone._renderer = renderer;
        stone._collider = circle;
        return stone;
    }

    // Everything the knights hold takes a stone off the ring, and takes the shot
    // with it. Mirrors EnemyPickaxe, which is the house rule for "one hit point":
    // the arrow is spent on the guard, which is the whole trade the ring offers.
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_backAt > 0f) return;

        if (other.CompareTag("PlayerLeftProjectile") || other.CompareTag("PlayerRightProjectile"))
        {
            Destroy(other.gameObject);
            Break();
            return;
        }

        // The sword object is untagged by design (see SwordSwing); the detector it
        // grows at swing time is the reliable handle. A knight close enough to
        // swing at a passing rider clears the guard by hand.
        if (other.GetComponent<SwordDamageDetector>() != null)
        {
            Break();
        }
    }

    // Hidden rather than destroyed. The slot is the point: a stone that came back
    // somewhere else would move the gaps, and the gaps are the whole puzzle.
    private void Break()
    {
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.projectileShield);
        _backAt = Time.time + _regrowSeconds;
        if (_renderer != null) _renderer.enabled = false;
        if (_collider != null) _collider.enabled = false;
    }

    private void Update()
    {
        if (_backAt < 0f || Time.time < _backAt) return;
        _backAt = -1f;
        if (_renderer != null) _renderer.enabled = true;
        if (_collider != null) _collider.enabled = true;
    }
}
