using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SheepsClothing", menuName = "Waves/Sheeps Clothing")]
public class SheepsClothing : BaseWave
{
    // A slow wall of slimes crawls in at one knight, eating that knight's arrows,
    // while a wolf zigzags in the wall's arrow shadow and lunges when the wall is close.
    // Two mirrored rounds: right knight besieged first, then the left.
    // Difficulty comes ONLY from the three knobs below.

    [Tooltip("Slimes forming each wall (one wall per knight, two mirrored rounds)")]
    [SerializeField] private int slimesPerWall = 3;
    [Tooltip("Slime size for the wall (1-3). Bigger = tankier, slower, more splits")]
    [SerializeField] private int slimeSize = 2;
    [Tooltip("Wolf that stalks behind each wall")]
    [SerializeField] private WolfType wolfType = WolfType.Grey;

    private const float WallSpawnX = 12.5f;         // just out of frame
    private const float WallSpreadY = 6f;           // vertical fan the wall covers
    private const float WolfZigzagAmplitude = 1.75f;
    private const float WolfZigzagStepX = 0.5f;     // net inward crawl per zigzag leg
    private const float WolfPeelX = 7f;             // wolf abandons cover here and lunges
    private const float RoundSeconds = 26f;
    private const float BreatherSeconds = 6f;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        yield return SpawnWallRound(spawner, 1f);   // right knight besieged first
        yield return new WaitForSeconds(BreatherSeconds);
        yield return SpawnWallRound(spawner, -1f);  // mirrored onto the left knight

        MarkSpawningComplete();
        yield return null;
    }

    private IEnumerator SpawnWallRound(Spawner spawner, float side)
    {
        Transform wallKnight = side > 0 ? spawner.RightPlayer : spawner.LeftPlayer;
        Transform offKnight = side > 0 ? spawner.LeftPlayer : spawner.RightPlayer;
        Vector2 aboveOff = side > 0 ? spawner.aboveLeftPlayer : spawner.aboveRightPlayer;
        Vector2 belowOff = side > 0 ? spawner.belowLeftPlayer : spawner.belowRightPlayer;

        // The wall: a vertical fan of slimes crawling in from the besieged knight's outer edge
        int size = Mathf.Clamp(slimeSize, 1, 3);
        for (int i = 0; i < slimesPerWall; i++)
        {
            float t = slimesPerWall == 1 ? 0.5f : (float)i / (slimesPerWall - 1);
            float y = Mathf.Lerp(-WallSpreadY / 2f, WallSpreadY / 2f, t);
            Vector2 spawnPos = new Vector2(side * (WallSpawnX + Mathf.Abs(y) * 0.3f), y);
            spawner.SpawnSlime(size, spawnPos, i * 0.4f, wallKnight);
        }

        // The wolf: enters visibly behind the wall, then zigzags inward inside the
        // wall's arrow shadow. When its path crosses WolfPeelX it ends and the wolf lunges.
        var waypoints = new List<Vector2> { new Vector2(side * 16f, 0f) };
        float x = WallSpawnX + 2f;
        int leg = 0;
        while (x > WolfPeelX)
        {
            waypoints.Add(new Vector2(side * x, (leg % 2 == 0 ? 1f : -1f) * WolfZigzagAmplitude));
            x -= WolfZigzagStepX;
            leg++;
        }
        spawner.SpawnWolf(waypoints, wallKnight, wolfType, 2f);

        // Steep pin shots steer the off knight (blocked projectiles are combo-safe)
        spawner.SpawnProjectileStraight(aboveOff, offKnight, 2, 8f, 2f);
        spawner.SpawnProjectileStraight(belowOff, offKnight, 2, 8f, 6f);

        // Mana orb sweeps the off knight's side; health orb threads behind the wall late,
        // so collecting it means shooting through a gap in the (split) slime line
        spawner.SpawnOrb(new Vector2(-side * 8f, -9f), new Vector2(-side * 8f, 9f), false, 8f);
        spawner.SpawnOrb(new Vector2(side * 8f, -9f), new Vector2(side * 8f, 9f), true, 18f);

        yield return new WaitForSeconds(RoundSeconds);
    }
}
