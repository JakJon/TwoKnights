using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Nothing on the board but fire ogres.
//
// A fire ogre walks at one knight and throws fire at the OTHER one
// (EnemyFireOgre). So in this wave each knight has two jobs that belong to two
// different ogres: shoot the ogre walking at them, and block the fire thrown by
// the ogre their partner is shooting. Neither knight can stop their own fire —
// it keeps coming until the OTHER knight's ogre is down. The projectiles are
// the wave, and how many there are is set by how fast the knights shoot.
//
// THE OGRES CROSS. Each one comes in along the top (or bottom) edge and walks
// diagonally at the knight on the far side, so their paths make an X. That puts every throw steeply down (or up) onto the knight nearer
// the entry, and never flat across the board through the partner's shield
// (rule 2). An ogre coming in off a side edge would throw straight through the
// knight it is walking at.
//
// EACH GROUP COMES OUT ALL AT ONCE (owner, 2026-09-25): every ogre of a
// crossing enters on the same frame, from the top and bottom edges together.
// Their ten-second throw clocks would then all start on that frame too, so the
// band staggers each ogre's FIRST throw (OgreBand.throwStagger, set to ten
// seconds over the group size) and the fire comes round one ogre at a time,
// alternating knights, instead of as a volley.
//
// THE ROCK IS SPIRALS (owner, 2026-09-25). Tier I spirals one knight at a
// time; II adds both knights at once, mirrored; III and up hand a spiral from
// one knight straight to the other and send it round and back. See
// VolleyShape.Spiral.
//
// THE FIRE WAITS FOR A GAP IN THE ROCK. The rock is the authored pattern and is
// never held. A fire ogre about to throw checks when its fireball would land and
// holds the throw while anything else is due at that knight within about a
// second either side (EnemyFireOgre, IncomingLedger). This replaced timing each
// crossing's volleys by hand to land between the fireballs: with a whole group
// throwing and spirals lasting several seconds, no fixed schedule could keep
// the two apart, and a fireball landing with a rock from another direction is a
// hit no guard can stop. Each crossing's rock therefore has to leave each knight
// quiet stretches for the fire to land in, or a bigger group adds bodies and no
// fire. The tiers keep the spirals short and fast with long rests after, so
// every knight is quiet for well over half the cycle; simulated against the
// throw clocks, 79-100% of throws go out, held a second or two on average.
//
// Crossings are player-paced: the next one starts once every ogre of the last
// is down, with no timeout. While one is alive, its own fire plus the shafts
// are the standing pressure.
[CreateAssetMenu(fileName = "HotPotato", menuName = "Waves/Hot Potato")]
public class HotPotato : BaseWave
{
    [System.Serializable]
    public class Crossing
    {
        [Tooltip("Authoring only")]
        public string label;

        [Tooltip("This crossing's ogres. firstAt counts from when the crossing STARTS, which is when every ogre of the one before is down. interval 0 sends them together, and throwStagger (10 / group size) then spreads their fire round the clock. fireEveryNth 1: every ogre in this wave is a fire ogre. Enter along the top or bottom edge and name the knight on the FAR side, so the throws come in steep.")]
        public OgreBand ogres = new OgreBand();

        [Tooltip("Seconds after the crossing starts before its first volley.")]
        public float firstVolleyAt = 5.6f;

        [Tooltip("Cycled in order while any ogre of this crossing is alive. Leave each knight quiet stretches: the fire only lands where the rock is not (see the file header).")]
        public List<RockVolley> volleys = new List<RockVolley>();
    }

    [Header("Crossings")]
    [Tooltip("Played in order. The next starts when every ogre of the last is down.")]
    [SerializeField] private List<Crossing> crossings = new List<Crossing>();

    [Header("Pressure")]
    [Tooltip("World units per second for THIS wave's rocks. A shaft is 7 units up, so 1.75 is about four seconds in the air.")]
    [SerializeField] private float rockSpeed = 1.75f;

    [Header("Orbs")]
    [Tooltip("At most one health orb (rule 8). Crosses high, through the band the fire comes down, so taking it means looking up into it.")]
    [SerializeField] private OrbRun healthOrbs = new OrbRun { healthOrb = true, count = 1, firstAt = 12f, from = new Vector2(-12f, 4f), to = new Vector2(12f, 4f) };

    [Tooltip("Mana orbs. Health and mana together stay within three (WaveOrbBudget).")]
    [SerializeField] private OrbRun manaOrbs = new OrbRun { healthOrb = false, count = 0, firstAt = 30f, interval = 20f, from = new Vector2(12f, -4f), to = new Vector2(-12f, -4f) };

    // The current crossing's ogres. Cleared per crossing — the asset is a
    // ScriptableObject and outlives the run.
    private readonly List<GameObject> _ogres = new List<GameObject>();

    // False until every ogre of the current crossing has spawned. Without it an
    // empty roster during the crossing's lead-in reads as "the crossing is dead".
    private bool _crossingOut;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        Coroutine health = spawner.StartCoroutine(healthOrbs.Release(spawner));
        Coroutine mana = spawner.StartCoroutine(manaOrbs.Release(spawner));

        int last = LastLiveCrossing();

        for (int i = 0; i <= last; i++)
        {
            Crossing crossing = crossings[i];
            if (!IsLive(crossing)) continue;

            _ogres.Clear();
            _crossingOut = false;

            spawner.StartCoroutine(ReleaseCrossing(spawner, crossing));
            Coroutine shafts = spawner.StartCoroutine(WorkTheShafts(spawner, crossing));

            while (!_crossingOut) yield return null;

            // The last crossing is out: if the orb timer has not paid yet, it
            // pays now, while there is still something on the board.
            if (i == last) healthOrbs.SendFirstIfWaiting(spawner);

            while (!OgreBand.AllDead(_ogres)) yield return null;

            if (shafts != null) spawner.StopCoroutine(shafts);
        }

        spawner.StopCoroutine(health);
        spawner.StopCoroutine(mana);

        MarkSpawningComplete();
        yield return null;
    }

    private IEnumerator ReleaseCrossing(Spawner spawner, Crossing crossing)
    {
        yield return crossing.ogres.Release(spawner, _ogres);
        _crossingOut = true;
    }

    // Round and round the crossing's own list for as long as it is alive. Stops
    // itself as well as being stopped, so a wave cut short cannot leave it
    // running on the Spawner.
    private IEnumerator WorkTheShafts(Spawner spawner, Crossing crossing)
    {
        if (crossing.volleys == null || crossing.volleys.Count == 0) yield break;

        yield return new WaitForSeconds(Mathf.Max(0f, crossing.firstVolleyAt));

        int shot = 0;
        while (!_crossingOut || !OgreBand.AllDead(_ogres))
        {
            RockVolley volley = crossing.volleys[shot++ % crossing.volleys.Count];
            if (volley == null)
            {
                yield return null;
                continue;
            }

            float busy = RockVolley.Fire(spawner, volley, rockSpeed);
            yield return new WaitForSeconds(busy + Mathf.Max(0f, volley.restAfter));
        }
    }

    private static bool IsLive(Crossing crossing)
    {
        return crossing != null && crossing.ogres != null && crossing.ogres.Total > 0;
    }

    private int LastLiveCrossing()
    {
        if (crossings == null) return -1;
        for (int i = crossings.Count - 1; i >= 0; i--)
        {
            if (IsLive(crossings[i])) return i;
        }
        return -1;
    }
}
