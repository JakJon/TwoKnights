using System.Collections.Generic;
using UnityEngine;

/// <summary>Something in the air on its way to a knight.</summary>
public interface IIncoming
{
    /// <summary>The knight it was aimed at. Null for anything not aimed at one.</summary>
    Transform IncomingTarget { get; }

    /// <summary>Seconds until it reaches that knight, or +infinity when it is no longer heading at him (reflected, gone past, homing).</summary>
    float SecondsToArrival { get; }
}

/// <summary>
/// Which projectiles are on their way to which knight, and when each one lands.
///
/// It exists for one question, asked by anything that throws on its own clock:
/// "if I throw now, will my shot land on a knight at the same moment as
/// something else coming at him from another direction?" A knight has ONE guard
/// facing ONE way, so two shots from two directions landing together cannot
/// both be blocked. No aim and no reaction answers that. This is house rule 3
/// (never two projectiles on one frame), applied to things that are not
/// released by the same script.
///
/// The wave scripts already stagger their own rock. What they cannot see is a
/// fire ogre's throw, which runs on the ogre's clock from the moment it spawned.
/// So the ogre asks here before it throws, and holds the throw until the slot
/// is clear (EnemyFireOgre). Rock is never held: it is the authored pattern.
///
/// A throw is BOOKED the moment it is decided, before its wind-up, because the
/// fireball does not exist until the wind-up ends. Without the booking, two ogres
/// deciding on the same frame would both see a clear slot.
/// </summary>
public static class IncomingLedger
{
    private struct Booking
    {
        public Transform Knight;
        public float ArrivesAt;
    }

    private static readonly List<IIncoming> Live = new List<IIncoming>();
    private static readonly List<Booking> Bookings = new List<Booking>();

    // Static state outlives a play session when domain reload is off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Live.Clear();
        Bookings.Clear();
    }

    public static void Add(IIncoming shot)
    {
        if (shot != null && !Live.Contains(shot)) Live.Add(shot);
    }

    public static void Remove(IIncoming shot)
    {
        Live.Remove(shot);
    }

    /// <summary>Holds a slot for a shot that has been decided but not yet released.</summary>
    public static void Book(Transform knight, float arrivesAt)
    {
        if (knight == null) return;
        Bookings.Add(new Booking { Knight = knight, ArrivesAt = arrivesAt });
    }

    /// <summary>
    /// True when nothing else is due at <paramref name="knight"/> within
    /// <paramref name="clearance"/> seconds either side of <paramref name="arrivesAt"/>.
    /// </summary>
    public static bool IsClear(Transform knight, float arrivesAt, float clearance)
    {
        if (knight == null) return true;

        float now = Time.time;

        for (int i = Bookings.Count - 1; i >= 0; i--)
        {
            Booking booking = Bookings[i];
            if (booking.Knight == null || booking.ArrivesAt < now - clearance)
            {
                Bookings.RemoveAt(i);
                continue;
            }
            if (booking.Knight == knight && Mathf.Abs(booking.ArrivesAt - arrivesAt) < clearance) return false;
        }

        for (int i = Live.Count - 1; i >= 0; i--)
        {
            IIncoming shot = Live[i];
            if (shot == null || (shot is Object unityObject && unityObject == null))
            {
                Live.RemoveAt(i);
                continue;
            }

            if (shot.IncomingTarget != knight) continue;

            float eta = shot.SecondsToArrival;
            if (float.IsInfinity(eta)) continue;
            if (Mathf.Abs(now + eta - arrivesAt) < clearance) return false;
        }

        return true;
    }

    /// <summary>
    /// Seconds until a straight-flying shot at <paramref name="position"/> reaches
    /// <paramref name="aimPoint"/>, or +infinity when its heading no longer points
    /// there. Shared by the rock and the fireball so they answer the same way.
    /// </summary>
    public static float StraightEta(Vector2 position, Vector2 heading, Vector2 aimPoint, float speed)
    {
        if (speed <= 0.0001f) return float.PositiveInfinity;

        Vector2 toAim = aimPoint - position;
        float distance = toAim.magnitude;
        if (distance < 0.0001f) return 0f;

        // Reflected, or already past: not coming.
        if (Vector2.Dot(heading.normalized, toAim / distance) < 0.95f) return float.PositiveInfinity;

        return distance / speed;
    }
}
