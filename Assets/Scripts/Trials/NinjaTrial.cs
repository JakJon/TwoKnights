using System.Collections;
using UnityEngine;

/// <summary>
/// The Shadow Order's trial: catch the Ninja. He appears at a spot round the edge of
/// the arena, holds it for a moment, and vanishes — land a hit on him before he goes.
/// Missing a single window loses.
///
///   I   six appearances, 2.5 seconds each, alternating sides.
///   II  ten at 2.2, with some back-to-back on one side.
///   III twelve at 2.0, and from the third on a shadow copy stands on the opposite
///       side at the same moment. Hitting the copy loses.
///
/// Windows sized for the arrow (owner's call, 2026-09-23): a shot takes most of a
/// second to cross to the far edge, so a window has to cover the turn, the release
/// and the flight.
///
/// The spots and their order are fixed, so the chase can be learned. The copy always
/// stands at the mirror of the real spot.
/// </summary>
public static class NinjaTrial
{
    // Where he stands, by his FEET — the art stands up from its origin. Five a side
    // round the edge of the frame, all in a clear line from the knight on that side,
    // and two in the middle, above and below. The frame is x ±10, y ±5.625.
    private static readonly Vector2 L1 = new Vector2(-8.2f, 2.4f);
    private static readonly Vector2 L2 = new Vector2(-8.6f, -3.0f);
    private static readonly Vector2 L3 = new Vector2(-5.0f, 3.3f);
    private static readonly Vector2 L4 = new Vector2(-9.0f, -0.3f);
    private static readonly Vector2 L5 = new Vector2(-5.4f, -4.6f);
    private static readonly Vector2 R1 = Mirror(L1);
    private static readonly Vector2 R2 = Mirror(L2);
    private static readonly Vector2 R3 = Mirror(L3);
    private static readonly Vector2 R4 = Mirror(L4);
    private static readonly Vector2 R5 = Mirror(L5);
    private static readonly Vector2 Top = new Vector2(0f, 3.3f);

    private static readonly Vector2[][] Chases =
    {
        new[] { L1, R1, L2, R2, L4, R4 },
        new[] { L3, R3, R2, L2, L1, R4, Top, L5, R5, R1 },
        new[] { L1, R2, L4, R1, R5, L3, L2, R4, L5, R3, R2, L1 },
    };

    private static readonly float[] HoldSeconds = { 2.5f, 2.2f, 2.0f };

    /// <summary>Phase three's copies start on this appearance (zero-based) — two clean ones first.</summary>
    private const int FirstCloneAt = 2;

    private const float LeadInSeconds = 0.6f;
    private const float BetweenSeconds = 0.55f;

    /// <summary>
    /// What he says as he arrives. Placeholder lines — the owner approved "Catch me,
    /// if you can" with the design and has not written the other two.
    /// </summary>
    public static string IntroLine(int phase)
    {
        switch (phase)
        {
            case 0: return "Catch me, if you can.";
            case 1: return "Not bad.\nThis time, I will not wait as long.";
            default: return "One last test. Watch closely...\nNot every shadow you see is mine.";
        }
    }

    public static IEnumerator Run(TrialRunner runner, int phase)
    {
        var catalog = NpcCatalog.Instance;
        var entry = catalog != null ? catalog.Find(NpcId.Ninja) : null;
        if (entry == null)
        {
            Debug.LogWarning("[Trials] No Ninja in the NpcCatalog; the Shadow trial cannot run.");
            yield break;
        }

        int index = Mathf.Clamp(phase, 0, Chases.Length - 1);
        var spots = Chases[index];
        float hold = HoldSeconds[index];
        bool clones = index >= 2;

        yield return new WaitForSeconds(LeadInSeconds);

        for (int i = 0; i < spots.Length && !runner.Stopped; i++)
        {
            var real = NinjaTarget.Appear(entry, spots[i], clone: false);
            if (real == null) yield break;
            runner.Track(real.gameObject);

            NinjaTarget copy = null;
            if (clones && i >= FirstCloneAt)
            {
                copy = NinjaTarget.Appear(entry, Mirror(spots[i]), clone: true);
                if (copy != null) runner.Track(copy.gameObject);
            }

            while (!real.Revealed && !runner.Stopped) yield return null;

            float deadline = Time.time + hold;
            while (!runner.Stopped && !real.WasHit && (copy == null || !copy.WasHit)
                   && Time.time < deadline)
            {
                yield return null;
            }
            if (runner.Stopped) yield break;

            if (copy != null && copy.WasHit)
            {
                // Struck the shadow. He shows himself leaving, so the player sees
                // which one was real.
                copy.Vanish();
                real.Vanish();
                runner.Lose();
                yield break;
            }

            if (!real.WasHit)
            {
                real.Vanish();
                if (copy != null) copy.Vanish();
                runner.Lose();
                yield break;
            }

            ShadowFx.ExecuteFlash(real.Centre);
            var audio = AudioManager.Instance;
            if (audio != null) audio.PlaySFX(audio.executeFlash);
            real.Vanish();
            if (copy != null) copy.Vanish();

            yield return new WaitForSeconds(BetweenSeconds);
        }
    }

    private static Vector2 Mirror(Vector2 spot) => new Vector2(-spot.x, spot.y);
}
