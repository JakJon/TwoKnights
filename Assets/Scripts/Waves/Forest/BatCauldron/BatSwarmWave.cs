using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using UnityEngine;

[CreateAssetMenu(fileName = "BatSwarmWave", menuName = "Waves/Bat Swarm Wave")]
public class BatSwarmWave : BaseWave
{
    // The first wave ever made. Super simple, just spawns a swarm of bats in a circular formation

    [SerializeField] private int batsPerRing = 8;
    [SerializeField] private int numberOfRings = 3;
    [SerializeField] private float delayBetweenRings = 15f;
    [SerializeField] private float ringRadius = 3f;
    [Tooltip("LEGACY SHAPE, tiers I-IV: rocks in the corner stream. Ignored entirely once `volleys` has anything in it")]
    [SerializeField] private float projectileAmount = 3f;
    [Tooltip("Legacy shape only: seconds between the rocks of that stream")]
    [SerializeField] private float projectileDelay = 4f;

    [Header("Rock")]
    [Tooltip("Cycled in order underneath the whole wave (see RockVolley). Leave EMPTY to keep the old corner streams, which alternate knights ring by ring - so only ever one knight has rock, from one corner, for the whole ring.")]
    [SerializeField] private List<RockVolley> volleys = new List<RockVolley>();

    [Tooltip("Seconds of rock from the start of the wave. Rings are delayBetweenRings apart, so four rings at 11 is about 44.")]
    [SerializeField] private float projectileWindow = 0f;

    [Tooltip("World units per second for this wave's rock. 0 leaves the prefab alone.")]
    [SerializeField] private float rockSpeed = 0f;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        // Orbs come in pairs here, so there is no clean per-ring slot to spread
        // three across — the budget just stops the pairs at three. Local to the
        // run: this is a ScriptableObject and a field would outlive the wave.
        WaveOrbBudget.Budget orbs = new WaveOrbBudget.Budget();

        // The rings are all laid down on this frame with their spacing carried as
        // per-spawn delays, so the rock gets its own clock rather than being
        // pinned to a ring index.
        bool authored = volleys != null && volleys.Count > 0;
        Coroutine shafts = authored
            ? spawner.StartCoroutine(
                RockVolley.WorkTheShafts(spawner, volleys, projectileWindow, rockSpeed))
            : null;

        for (int ring = 1; ring <= numberOfRings; ring++)
        {
            float angleStep = 360f / batsPerRing;
            for (int i = 1; i <= batsPerRing; i++)
            {
                float angle = i * angleStep;
                float x = Mathf.Cos(angle * Mathf.Deg2Rad) * ringRadius;
                float y = Mathf.Sin(angle * Mathf.Deg2Rad) * ringRadius;
                // spawner.SpawnProjectileStraight(spawner.topLeftCorner, spawner.RightPlayer, projectileAmount, projectileDelay, (ring - 1) * delayBetweenRings);

                // Spawn first ring immediately, subsequent rings with increasing delay
                spawner.SpawnBat(new Vector2(x, y), (ring - 1) * delayBetweenRings);
            }

            if (ring % 2 == 0)
            {
                if (!authored)
                {
                    spawner.SpawnProjectileStraight(spawner.topLeftCorner, spawner.LeftPlayer, projectileAmount, projectileDelay, (ring - 1) * delayBetweenRings);
                }
                if (orbs.TrySpend()) spawner.SpawnOrb(new Vector2(8, 8), new Vector2(8, -8), false);
                if (orbs.TrySpend()) spawner.SpawnOrb(new Vector2(-8, 8), new Vector2(-8, -8), false);
            }
            else if (ring % 2 != 0 && !authored)
            {
                spawner.SpawnProjectileStraight(spawner.bottomRightCorner, spawner.RightPlayer, 3, 4, (ring - 1) * delayBetweenRings);
            }
        }

        if (shafts != null) yield return shafts;

        // Mark spawning as complete so the wave knows to start checking for enemy deaths
        MarkSpawningComplete();

        // The wave will now automatically complete when all enemies are killed
        yield return null; // Required for IEnumerator even though we're not waiting
    }
}