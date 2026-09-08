using UnityEngine;

// A vial of venom thrown clear of the knight, which breaks where it lands and
// leaves a cloud rolling on the way it was thrown.
//
// It is NOT a projectile in the sense the rest of this game means it. It does no
// damage, it hits nothing, it cannot be blocked and it cannot miss — it travels
// its authored distance and breaks, every time. All of that is deliberate: the
// value of a vial is entirely the GROUND it denies, and anything that could stop
// it early would turn "where do I want the cloud" back into "did the throw land",
// which is a question the Serpent already answers with poison chance.
//
// What that buys is the one thing a two-knight loadout is otherwise short of: an
// answer to the space you are not pointing at. Shield facing is also shooting
// direction, so a knight covering the top has nothing at all for the bottom, and
// rank II's backward vial is a cloud sitting exactly there without ever being
// aimed at.
//
// It builds itself from a sprite rather than being spawned from a prefab so the
// upgrade asset carries one reference instead of a prefab that would exist only
// to hold a sprite and a collider it does not use.
public class PoisonVial : MonoBehaviour
{
    private const float Speed = 6f;      // world units/second in flight
    private const float SpinSpeed = 420f; // degrees/second, end over end

    // The cloud it leaves. Small and brief next to a Miasma cloud on purpose:
    // this arrives every four or five shots, so it is a repeated small denial
    // rather than a rare big one.
    private const float CloudRadius = 1.2f;
    private const float CloudSeconds = 5f;

    // The cloud keeps going the way the vial was going. A puff that stopped dead
    // where the glass broke denied one spot; one that rolls on denies a LANE, which
    // is what the throw was for — the whole point of a vial is the ground you are
    // not pointing at, and a lane covers more of it than a dot does. Far under the
    // vial's own 6: at 1 unit a second the cloud drifts about five units over its
    // five, which is a slow roll across the board rather than a second projectile.
    // Halved from 2 — at the old speed the puff outran the ground it was meant to
    // be denying and had left the lane before anything walked into it.
    private const float CloudDriftSpeed = 1f;

    private Vector2 _velocity;
    private float _breakAt;
    private string _ownerTag;
    private bool _broken;

    /// <summary>
    /// Throw one from <paramref name="from"/> along <paramref name="direction"/>,
    /// breaking <paramref name="distance"/> units out. Returns null and throws
    /// nothing if there is no sprite to throw, so a missing reference costs a
    /// silent no-op rather than an exception on every fifth shot.
    /// </summary>
    public static PoisonVial Throw(Sprite sprite, Vector2 from, Vector2 direction,
                                   float distance, string ownerTag)
    {
        if (sprite == null || direction.sqrMagnitude < 0.0001f) return null;

        var go = new GameObject("PoisonVial");
        go.transform.position = from;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 15;

        var vial = go.AddComponent<PoisonVial>();
        Vector2 heading = direction.normalized;
        vial._velocity = heading * Speed;
        vial._ownerTag = ownerTag;

        // A time rather than a target point, so a vial cannot overshoot on a long
        // frame and cannot stall a fraction of a unit short of where it was aimed
        vial._breakAt = Time.time + Mathf.Max(0.05f, distance) / Speed;

        return vial;
    }

    private void Update()
    {
        if (_broken) return;

        transform.position += (Vector3)(_velocity * Time.deltaTime);
        transform.Rotate(0f, 0f, (_velocity.x >= 0f ? -SpinSpeed : SpinSpeed) * Time.deltaTime);

        if (Time.time >= _breakAt) Break();
    }

    private void Break()
    {
        if (_broken) return;
        _broken = true;

        AudioManager.Instance?.PlaySFX(AudioManager.Instance.glassBreak);

        // Heading rather than _velocity: the drift is the vial's DIRECTION at its own
        // speed, not a fraction of the throw, so retuning the flight speed does not
        // silently retune the cloud with it.
        Vector2 heading = _velocity.sqrMagnitude > 0.0001f ? _velocity.normalized : Vector2.zero;
        PoisonCloud.SpawnPuff(transform.position, CloudRadius, CloudSeconds, _ownerTag,
                              heading * CloudDriftSpeed);

        Destroy(gameObject);
    }
}
