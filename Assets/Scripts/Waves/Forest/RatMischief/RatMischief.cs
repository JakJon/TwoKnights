using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RatMischief", menuName = "Waves/Rat Mischief")]
public class RatMischef : BaseWave
{
    // The rats have always come in two halves - odd indices walk at the left
    // knight, even ones at the right - and until tier 5 the ROCK did not. Every
    // occurrence threw one 180-degree sweep at the right knight and nothing at
    // all at the left, so one player read a stream while the other only ever
    // fought rats. `volleys` is where that is fixed: one volley per occurrence,
    // cycled in order, authored per tier.

    [Tooltip("Tooltip")]
    [SerializeField] private int waveOccurences = 1;
    [SerializeField] private int baseRatCount = 1;
    [Tooltip("LEGACY SHAPE, tiers I-IV: rocks in the single counter-clockwise sweep at the right knight. Ignored entirely once `volleys` has anything in it")]
    [SerializeField] private int projectileCount = 3;
    [Tooltip("Legacy shape only: seconds between the rocks of that sweep")]
    [SerializeField] private float projectileDelay = 1f;

    [Header("Rock")]
    [Tooltip("Cycled in order underneath the whole wave (see RockVolley). Leave EMPTY to keep the old single sweep at the right knight, which is what tiers I-IV want - the sweep is where the shape is learned before the deep tiers start varying it.")]
    [SerializeField] private List<RockVolley> volleys = new List<RockVolley>();

    [Tooltip("Seconds of rock from the start of the wave. The occurrences run 12 + 2N seconds each, so three of them is about 48 - size it to cover them or the back of the wave goes quiet.")]
    [SerializeField] private float projectileWindow = 0f;

    [Tooltip("World units per second for this wave's rock. 0 leaves the prefab alone.")]
    [SerializeField] private float rockSpeed = 0f;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        // Beside the formation, not inside it. A single volley per occurrence was
        // the obvious shape and the wrong one: an occurrence is fourteen seconds
        // and one volley is about two, so the rats would have had eighty per cent
        // of every occurrence to themselves. Cycling underneath means the guard is
        // being worked the whole time the formation is walking in.
        bool authored = volleys != null && volleys.Count > 0;
        Coroutine shafts = authored
            ? spawner.StartCoroutine(
                RockVolley.WorkTheShafts(spawner, volleys, projectileWindow, rockSpeed))
            : null;

        int[] yPattern = { 2, 3, 4, 3, 2, 1 };
        for (int i = 1; i < waveOccurences + 1; i++)
        {
            float yOffset = yPattern[(i - 1) % yPattern.Length];

            int totalRats = baseRatCount * i;
            float spacing = 2.5f;
            float groupWidth = (totalRats - 1) * spacing;
            float startX = 0 + (-groupWidth / 2f); // Center group around x = -6
            float staggerAmount = 0.75f; // Adjust for more/less vertical staggering

            if (i == 2)
            {
                spawner.SpawnOrb(new Vector2(0, 6), new Vector2(-12, -2), false, 12f);
            }
            if (i == 5)
            {
                spawner.SpawnOrb(new Vector2(8, -7), new Vector2(8, 7), false, 20f);
            }

            if (!authored)
            {
                spawner.SpawnProjectileArc(
                    spawner.RightPlayer,
                    Spawner.ArcDirection.CounterClockwise,
                    spawner.belowRightPlayer,
                    180f,
                    projectileCount + i,
                    projectileDelay
                    );
            }

            for (int ratIndex = 1; ratIndex < totalRats + 1; ratIndex++)
            {
                Vector2 spawnPos;
                Transform targetPlayer;

                float xPos = startX + (ratIndex - 1) * spacing;
                float stagger = (ratIndex - 1 - (totalRats - 1) / 2f) * staggerAmount;

                float y = (ratIndex % 2 == 0) ? -yOffset + stagger : yOffset + stagger;
                if (y > -2f && y < 2f)
                    y += 2f * Mathf.Sign(y == 0 ? (ratIndex % 2 == 0 ? -1 : 1) : y);
                y = Mathf.Clamp(y, -yOffset, yOffset);
                if (y < 0 && y > -2)
                {
                    y = y - 2f;
                }
                else if (y > 0 && y < 2)
                {
                    y = y + 2f;
                }

                spawnPos = new Vector2(xPos, y);

                // Which knight the rat walks at, and nothing else. The index used
                // to pick the rat's colour here too; it does not any more — that is
                // the map's cadence now — but the left/right split is positional and
                // stays, because it is what makes the formation read as two halves.
                targetPlayer = (ratIndex % 2 == 0) ? spawner.RightPlayer : spawner.LeftPlayer;

                spawner.SpawnRat(spawnPos, 0, targetPlayer);
            }
            yield return new WaitForSeconds(12f + i * 2);
        }

        // Rock still to be issued is rock the wave owes; ending before it exists
        // would close the wave with stone still to come.
        if (shafts != null) yield return shafts;

        // Mark spawning as complete so the wave knows to start checking for enemy deaths
        MarkSpawningComplete();

        // The wave will now automatically complete when all enemies are killed
        yield return null; // Required for IEnumerator even though we're not waiting
    }
}
