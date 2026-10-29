using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Behind the Pillars — the castle's first rat wave, and the first wave where the
// pillars and the mirrors are used together.
//
// The board (owner's design, 2026-10-26):
//
//   * Two rows of pillars, one unit square and one unit apart, run the width of
//     the arena one unit above the knights' heads and one unit below their feet
//     (y = +-2). They stop the knights' arrows; enemy rocks fly over them.
//   * Rats come in above the top row and below the bottom row and patrol there,
//     out of reach of a straight shot.
//   * Two mirror pairs carry shots to them. Each knight-level pane, at the far
//     left and far right of the arena, is twinned with a pane at the far end of a
//     rat row, at the rats' own height:
//         far left  (-9.4, 0)  <->  top right    (green)
//         far right ( 9.4, 0)  <->  bottom left  (vermilion)
//     Under the castle's rule a shot keeps its heading through a pane, so a level
//     shot sent LEFT comes out of the top right still travelling left and sweeps
//     the top row end to end, and a level shot sent RIGHT sweeps the bottom row.
//
// The pillars sit on odd x, so there is a gap directly above and below each
// knight. That gap is the second way in: it is where a knight can shoot
// straight up or down, where the rock comes down at him, and where a rat comes
// through when its patrol is up (rats walk to the nearest gap — see
// PillarField.Route). So the rock is what this wave's guard is about: it holds
// a knight's facing on the gap above or below him, while every mirror shot
// needs it turned to face the side.
//
// The rock takes turns (2026-10-27): every volley is on ONE knight, and the
// volleys alternate, so while one knight is held on his gap the other is free
// to turn to the side and make the mirror shot. Straight down (and up) the
// columns, never from the corners.
//
// Tier ladder (five tiers in mind): salvo 1 / 3 / 5 / 8 / 8, flip windows
// 1.2 / 0.8 / 0.6 / 0.3 / 0.1. From tier III a Scissors closes each cycle —
// the one moment both knights are held at once, so both mirror shots wait —
// and tier V has two.
[CreateAssetMenu(fileName = "BehindThePillars", menuName = "Waves/Behind the Pillars")]
public class BehindThePillars : BaseWave
{
    [Header("Board")]
    [Tooltip("Stood up the moment the wave starts; cleared when the next wave begins")]
    [SerializeField] private MirrorLayout mirrorLayout;

    [Tooltip("Castle_Pillar.prefab")]
    [SerializeField] private GameObject pillarPrefab;

    [SerializeField] private List<PillarRow> pillarRows = new List<PillarRow>();

    [Header("Rats")]
    [Tooltip("Groups dropped in above the top row and below the bottom row. Station Height is the rat's own position; its body sits about 0.22 above that, which is the height the rat-row mirrors are hung at.")]
    [SerializeField] private List<RatFlank> flanks = new List<RatFlank>();

    [Header("Rock")]
    [Tooltip("Cycled in order for Projectile Window seconds. Rocks fly over the pillars, so any shape works; the straight-down shapes come down the gap a knight can also shoot up through.")]
    [SerializeField] private List<RockVolley> volleys = new List<RockVolley>();

    [SerializeField] private float projectileWindow = 28f;

    [Tooltip("World units per second for this wave's rock. 0 leaves the prefab alone.")]
    [SerializeField] private float rockSpeed = 0f;

    [Header("Orbs")]
    [SerializeField] private OrbRun manaOrbs = new OrbRun();
    [SerializeField] private OrbRun healthOrbs = new OrbRun { healthOrb = true, count = 0 };

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        spawner.Mirrors.Lay(mirrorLayout);
        spawner.Pillars.Lay(pillarPrefab, pillarRows);

        Coroutine mana = spawner.StartCoroutine(manaOrbs.Release(spawner));
        Coroutine health = spawner.StartCoroutine(healthOrbs.Release(spawner));

        Coroutine shafts = volleys != null && volleys.Count > 0
            ? spawner.StartCoroutine(RockVolley.WorkTheShafts(spawner, volleys, projectileWindow, rockSpeed))
            : null;

        // Every flank runs on its own clock (its firstAt is from the start of the
        // wave), so they are started together and the wave waits for the last.
        var roster = new List<GameObject>();
        int running = 0;
        for (int i = 0; i < flanks.Count; i++)
        {
            if (flanks[i] == null || flanks[i].Total == 0) continue;
            running++;
            spawner.StartCoroutine(Countdown(flanks[i].Release(spawner, roster), () => running--));
        }
        while (running > 0) yield return null;

        // Rock still to be issued is rock the wave owes
        if (shafts != null) yield return shafts;

        // Everything is out. An orb whose timer has not come up yet goes now
        // rather than being stopped unpaid — see OrbRun.SendFirstIfWaiting.
        manaOrbs.SendFirstIfWaiting(spawner);
        healthOrbs.SendFirstIfWaiting(spawner);
        spawner.StopCoroutine(mana);
        spawner.StopCoroutine(health);

        MarkSpawningComplete();
        yield return null;
    }

    private static IEnumerator Countdown(IEnumerator body, System.Action done)
    {
        yield return body;
        done();
    }
}
