using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A lot of ogres, and rock to keep the guard busy while they walk.
//
// Every other mine wave sends one to five ogres alongside something else. This
// one sends nothing else: plain ogres, in gangs, from the side edges, the roof
// and the floor, far more of them than any other wave, until the whole board is
// walking at the knights. Each ogre is eighty health, so the wave is an arrow
// budget: a base knight clears one about every twelve seconds, and the gangs
// are spaced so a knight who keeps shooting stays ahead of them. One who stops
// shooting falls behind, and every ogre that arrives is forty damage.
//
// No fire ogres, so the only projectiles are the shafts'. They used to be
// single rocks with long rests; the owner asked for more (2026-09-25). Tier I
// sends five rocks a second apart on every volley, II sends walls of five and
// six straight down the shafts, and from III the rock stops coming straight
// down the shaft: fans, spirals and weaves bring it in at angles, with more of
// each at every tier.
//
// THE ROCK ANSWERS THE GANG THAT JUST CAME IN (rule 7). When a gang walks in off
// the side edges the rock comes down the shafts; when one comes over the roof
// the rock comes up from underneath, and the other way round. Either way the
// shield has to turn away from the queue for a moment, which is where the
// arrows are lost. Each gang carries its own volleys, and the shafts switch to
// them the moment that gang starts walking in.
//
// Gangs are released on the wave's clock, not the players': they overlap on
// purpose. Ogres are too slow to pace by kills. Waiting for a gang to die
// before sending the next would never build the crowd this wave is about.
[CreateAssetMenu(fileName = "RushHour", menuName = "Waves/Rush Hour")]
public class RushHour : BaseWave
{
    [System.Serializable]
    public class Gang
    {
        [Tooltip("Authoring only")]
        public string label;

        [Tooltip("This gang's ogres. firstAt counts from the start of the WAVE, and gangs overlap. fireEveryNth 0: plain ogres only.")]
        public OgreBand ogres = new OgreBand();

        [Tooltip("The shafts switch to this list when this gang starts walking in, and cycle it until the next gang does. Rock comes from the side the gang is NOT on.")]
        public List<RockVolley> volleys = new List<RockVolley>();
    }

    [Header("Gangs")]
    [SerializeField] private List<Gang> gangs = new List<Gang>();

    [Header("Pressure")]
    [Tooltip("Seconds from the start of the wave before the first volley")]
    [SerializeField] private float firstVolleyAt = 5f;

    [Tooltip("World units per second for THIS wave's rocks. A shaft is 7 units up, so 1.75 is about four seconds in the air.")]
    [SerializeField] private float rockSpeed = 1.75f;

    [Header("Orbs")]
    [Tooltip("At most one health orb (rule 8). Crosses low, behind the floor gangs, so an arrow for it has to find a gap in the queue.")]
    [SerializeField] private OrbRun healthOrbs = new OrbRun { healthOrb = true, count = 1, firstAt = 20f, from = new Vector2(-12f, -3.5f), to = new Vector2(12f, -3.5f) };

    [Tooltip("Mana orbs. Health and mana together stay within three (WaveOrbBudget).")]
    [SerializeField] private OrbRun manaOrbs = new OrbRun { healthOrb = false, count = 0, firstAt = 35f, interval = 20f, from = new Vector2(12f, 3.5f), to = new Vector2(-12f, 3.5f) };

    // Every ogre this wave put out. Cleared at the top of the wave — the asset
    // is a ScriptableObject and outlives the run.
    private readonly List<GameObject> _ogres = new List<GameObject>();

    // How many gangs have finished walking in. The wave is not over while one
    // is still to come, however empty the board is.
    private int _gangsOut;

    private float _startedAt;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        _ogres.Clear();
        _gangsOut = 0;
        _startedAt = Time.time;

        Coroutine health = spawner.StartCoroutine(healthOrbs.Release(spawner));
        Coroutine mana = spawner.StartCoroutine(manaOrbs.Release(spawner));

        int live = 0;
        if (gangs != null)
        {
            for (int i = 0; i < gangs.Count; i++)
            {
                if (!IsLive(gangs[i])) continue;
                spawner.StartCoroutine(ReleaseGang(spawner, gangs[i]));
                live++;
            }
        }

        Coroutine shafts = spawner.StartCoroutine(WorkTheShafts(spawner, live));

        while (_gangsOut < live) yield return null;

        // The last gang is out: if the orb timer has not paid yet, it pays now,
        // while there is still something on the board.
        healthOrbs.SendFirstIfWaiting(spawner);

        while (!OgreBand.AllDead(_ogres)) yield return null;

        if (shafts != null) spawner.StopCoroutine(shafts);
        spawner.StopCoroutine(health);
        spawner.StopCoroutine(mana);

        MarkSpawningComplete();
        yield return null;
    }

    private IEnumerator ReleaseGang(Spawner spawner, Gang gang)
    {
        yield return gang.ogres.Release(spawner, _ogres);
        _gangsOut++;
    }

    // Cycles the volleys of whichever gang started walking in most recently,
    // from the top of its list each time the gang changes. Stops itself once
    // every gang is out and down, as well as being stopped by the wave.
    private IEnumerator WorkTheShafts(Spawner spawner, int live)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, firstVolleyAt));

        Gang answering = null;
        int shot = 0;

        while (_gangsOut < live || !OgreBand.AllDead(_ogres))
        {
            Gang gang = LatestGangIn();
            if (gang == null)
            {
                yield return null;
                continue;
            }

            if (gang != answering)
            {
                answering = gang;
                shot = 0;
            }

            RockVolley volley = gang.volleys[shot++ % gang.volleys.Count];
            if (volley == null)
            {
                yield return null;
                continue;
            }

            float busy = RockVolley.Fire(spawner, volley, rockSpeed);
            yield return new WaitForSeconds(busy + Mathf.Max(0f, volley.restAfter));
        }
    }

    // The gang with rock authored whose first ogre has most recently walked in.
    private Gang LatestGangIn()
    {
        float elapsed = Time.time - _startedAt;
        Gang latest = null;
        float latestAt = float.MinValue;

        for (int i = 0; i < gangs.Count; i++)
        {
            Gang gang = gangs[i];
            if (!IsLive(gang) || gang.volleys == null || gang.volleys.Count == 0) continue;
            if (gang.ogres.firstAt > elapsed || gang.ogres.firstAt < latestAt) continue;

            latest = gang;
            latestAt = gang.ogres.firstAt;
        }

        return latest;
    }

    private static bool IsLive(Gang gang)
    {
        return gang != null && gang.ogres != null && gang.ogres.Total > 0;
    }
}
