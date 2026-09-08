using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "AboutFace", menuName = "Waves/About Face")]
public class AboutFace : BaseWave
{
    [Header("Bursts")]
    [Tooltip("Number of bursts; the flip window shrinks each burst")]
    [SerializeField] private int burstCount = 2;
    [Tooltip("Volley pairs per burst; each pair = one volley, then one from the opposite side")]
    [SerializeField] private int pairsPerBurst = 3;
    [Tooltip("Seconds between a volley and its opposite follow-up. The core difficulty dial")]
    [SerializeField] private float flipWindow = 1.5f;
    [Tooltip("flipWindow is multiplied by this after each burst (1 = no ramp)")]
    [SerializeField] private float flipWindowRampFactor = 1f;
    [Tooltip("Seconds between pairs within a burst")]
    [SerializeField] private float delayBetweenPairs = 1.5f;
    [Tooltip("Seconds between bursts")]
    [SerializeField] private float delayBetweenBursts = 3f;

    [Header("Volleys")]
    [Tooltip("Projectiles per volley direction")]
    [SerializeField] private int projectilesPerVolley = 1;
    [Tooltip("Degrees the opposite volley deviates from a true 180 flip; the deviation alternates direction each volley")]
    [SerializeField] private float angleJitter = 0f;
    [Tooltip("Extra distance beyond the screen edge where projectiles spawn; larger = more reaction time")]
    [SerializeField] private float offscreenPadding = 4f;
    [Tooltip("Volley angles stay at least this many degrees away from horizontal so shots never cross the other knight")]
    [SerializeField] private float minAngleFromHorizontal = 30f;

    [Header("Escorts")]
    [Tooltip("Bats drifting in from the top/bottom edges during each burst")]
    [SerializeField] private int batsPerBurst = 0;
    [Tooltip("Wolves circling in over the course of the wave")]
    [SerializeField] private int wolfEscorts = 0;
    [Tooltip("Spawn a mana orb between bursts")]
    [SerializeField] private bool spawnManaOrb = false;

    private const float FieldX = 12f;
    private const float FieldY = 7f;

    // Volley angles walk the legal top arc in a fixed golden-ratio stride —
    // varied directions, but the exact same sequence every run (design rule 6:
    // wave content never rolls dice). Steps through ~0.50, 0.88, 0.26, 0.65...
    // of the arc; consecutive volleys (left knight, then right) land far apart.
    private const float ArcStride = 0.382f;
    private int _volleyStep;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        // ScriptableObject state persists between runs — reset so every run
        // of this wave plays the identical sequence
        _volleyStep = 0;
        float window = flipWindow;
        // Nothing in this wave may ask for a turn the guard cannot physically
        // make. The flip is at most a half circle, so the shortest honest answer
        // is 180 degrees at the shield's own rotation speed, plus a beat for the
        // player to notice the first shot and start turning. Authored windows
        // below that floor are raised rather than obeyed: an unblockable flip is
        // not a hard wave, it is a wave with no answer.
        window = Mathf.Max(window, MinimumFlipWindow(spawner));

        for (int wolf = 0; wolf < wolfEscorts; wolf++)
        {
            Transform wolfTarget = wolf % 2 == 0 ? spawner.LeftPlayer : spawner.RightPlayer;
            spawner.SpawnWolf(WolfMovementPatterns.CircleLeftThenRight, wolfTarget, (WolfType)(wolf % 3), wolf * 2f);
        }

        for (int burst = 0; burst < burstCount; burst++)
        {
            for (int bat = 0; bat < batsPerBurst; bat++)
            {
                // Even spread across the field, alternating top/bottom edges —
                // the same entry points every run
                float batY = bat % 2 == 0 ? FieldY + 1f : -FieldY - 1f;
                float spreadT = batsPerBurst > 1 ? (float)bat / (batsPerBurst - 1) : 0.5f;
                float batX = Mathf.Lerp(-FieldX, FieldX, spreadT);
                spawner.SpawnBat(new Vector2(batX, batY), bat * 0.5f);
            }

            for (int pair = 0; pair < pairsPerBurst; pair++)
            {
                FireFlipPair(spawner, spawner.LeftPlayer, window);
                FireFlipPair(spawner, spawner.RightPlayer, window);
                yield return new WaitForSeconds(window + delayBetweenPairs);
            }

            window = Mathf.Max(window * flipWindowRampFactor, MinimumFlipWindow(spawner));

            if (burst < burstCount - 1)
            {
                if (spawnManaOrb)
                {
                    spawner.SpawnOrb(new Vector2(0f, FieldY + 1f), Vector2.zero, false);
                }
                yield return new WaitForSeconds(delayBetweenBursts);
            }
        }

        MarkSpawningComplete();
        yield return null;
    }

    private void FireFlipPair(Spawner spawner, Transform targetPlayer, float window)
    {
        // First volley from the top arc; follow-up from the mirrored bottom arc after the flip window.
        // Angles stay minAngleFromHorizontal degrees away from horizontal so neither volley's
        // path can cross the other knight, and spawn points are pushed out of frame.
        float sweep = Mathf.Repeat(0.5f + _volleyStep * ArcStride, 1f);
        float baseAngle = Mathf.Lerp(minAngleFromHorizontal, 180f - minAngleFromHorizontal, sweep);
        float jitter = angleJitter * (_volleyStep % 2 == 0 ? 1f : -1f);
        float flipAngle = baseAngle + 180f + jitter;
        flipAngle = Mathf.Clamp(flipAngle, 180f + minAngleFromHorizontal, 360f - minAngleFromHorizontal);
        _volleyStep++;

        // Both legs leave from the SAME distance, not from their own screen exit.
        // A shallow angle exits the field roughly twice as far out as a steep one,
        // so spawning each at its own exit made the flip arrive anywhere from the
        // full window to almost none of it - occasionally before the shot it was
        // supposed to follow. One radius for the pair means the gap the player
        // actually experiences is the flip window, which is the number this wave
        // is tuned on.
        Vector2 origin = targetPlayer.position;
        float radius = Mathf.Max(ExitDistance(origin, baseAngle), ExitDistance(origin, flipAngle))
                       + offscreenPadding;

        for (int i = 0; i < projectilesPerVolley; i++)
        {
            spawner.SpawnProjectile(targetPlayer, PointAt(origin, baseAngle, radius), i * 0.15f);
            spawner.SpawnProjectile(targetPlayer, PointAt(origin, flipAngle, radius), window + i * 0.15f);
        }
    }

    // Where a ray from the knight at this angle leaves the playfield.
    private float ExitDistance(Vector2 origin, float angleDegrees)
    {
        Vector2 dir = Direction(angleDegrees);
        float tx = Mathf.Approximately(dir.x, 0f)
            ? float.PositiveInfinity
            : (dir.x > 0f ? (FieldX - origin.x) / dir.x : (-FieldX - origin.x) / dir.x);
        float ty = Mathf.Approximately(dir.y, 0f)
            ? float.PositiveInfinity
            : (dir.y > 0f ? (FieldY - origin.y) / dir.y : (-FieldY - origin.y) / dir.y);
        return Mathf.Min(tx, ty);
    }

    private Vector2 PointAt(Vector2 origin, float angleDegrees, float distance)
    {
        return origin + Direction(angleDegrees) * distance;
    }

    private static Vector2 Direction(float angleDegrees)
    {
        return new Vector2(Mathf.Cos(angleDegrees * Mathf.Deg2Rad),
                           Mathf.Sin(angleDegrees * Mathf.Deg2Rad));
    }

    // Seconds a player needs to see the first shot land its direction and get
    // the stick over. Deliberately generous: the wave's difficulty is meant to
    // come from how OFTEN the flip is asked for, not from whether the turn fits.
    private const float FlipReactionAllowance = 0.35f;
    private const float FallbackShieldRotationSpeed = 180f;

    // Half a circle at the guard's own rotation speed, plus the reaction beat.
    private float MinimumFlipWindow(Spawner spawner)
    {
        float rotationSpeed = FallbackShieldRotationSpeed;
        Transform knight = spawner.LeftPlayer != null ? spawner.LeftPlayer : spawner.RightPlayer;
        if (knight != null)
        {
            ShieldOrbit shield = knight.GetComponentInChildren<ShieldOrbit>();
            if (shield != null && shield.RotationSpeed > 0f) rotationSpeed = shield.RotationSpeed;
        }
        return 180f / rotationSpeed + FlipReactionAllowance;
    }
}
