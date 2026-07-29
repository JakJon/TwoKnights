using UnityEngine;
using System.Collections;

// Conductor for the twin-slime boss. The twins are deliberately dumb about each
// other — this object owns everything that has to be true of the PAIR:
//
//   * volleys leave both mouths on the same frame ("always shoot at the same time"),
//   * the two patterns alternate on one shared clock,
//   * the single BossHealthBar shows their COMBINED health, so the bar empties once
//     for the fight rather than fighting over one static bar,
//   * the trickle of ordinary slimes runs from wave start until both twins are down.
//
// Spawned and configured by GiantSlimeWave, then left to run itself.
public class GiantSlimeDuel : MonoBehaviour
{
    [System.Serializable]
    public class AddsConfig
    {
        [Tooltip("Seconds between each pair of ordinary slimes.")]
        public float interval = 10f;
        [Tooltip("Slimes released per interval — one from the top, one from the bottom.")]
        public int perInterval = 2;
        [Tooltip("First pair arrives this long after the fight starts.")]
        public float firstDelay = 4f;
        public Vector2 topEntry = new Vector2(0f, 7f);
        public Vector2 bottomEntry = new Vector2(0f, -7f);
    }

    [System.Serializable]
    public class OrbConfig
    {
        [Tooltip("Mana orbs that cross before the health orb.")]
        public int manaOrbs = 3;
        [Tooltip("One health orb comes last of all, as the twins close in.")]
        public bool finalHealthOrb = true;
        [Tooltip("Height the orbs cross at. The twins' bodies sit ABOVE their feet, so " +
                 "this low lane stays clear of them for the whole approach.")]
        public float lane = -4.5f;
        [Tooltip("Orbs enter this far out and fly to the mirror of it, crossing the " +
                 "full width. Orbs are collected by SHOOTING them (CollectibleOrb), so " +
                 "the lane only has to be aimable, not reachable.")]
        public float travelX = 12f;
    }

    // Wave banner runs 5s from wave start and the twins spawn 1.5s in, so the bar
    // fades up as the banner finishes — same handoff the Rat King uses
    private const float HealthBarRevealDelay = 3.5f;

    private Spawner _spawner;
    private EnemyGiantSlime _left;
    private EnemyGiantSlime _right;
    private EnemyGiantSlime.Config _config;
    private AddsConfig _adds;
    private OrbConfig _orbs;
    private float _combinedMaxHealth;

    public void Begin(Spawner spawner, EnemyGiantSlime left, EnemyGiantSlime right,
        EnemyGiantSlime.Config config, AddsConfig adds, OrbConfig orbs, string bossTitle)
    {
        _spawner = spawner;
        _left = left;
        _right = right;
        _config = config;
        _adds = adds ?? new AddsConfig();
        _orbs = orbs ?? new OrbConfig();
        _combinedMaxHealth = config.health * 2f;

        BossHealthBar.Show(bossTitle);
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.bossBanner);

        StartCoroutine(RevealHealthBarAfterBanner());
        StartCoroutine(VolleyClock());
        StartCoroutine(AddTrickle());
        StartCoroutine(OrbDrops());
    }

    private IEnumerator RevealHealthBarAfterBanner()
    {
        yield return new WaitForSeconds(HealthBarRevealDelay);
        if (!BothDown) BossHealthBar.Reveal();
    }

    private void Update()
    {
        // Pushed every frame so poison ticks and mid-volley hits both register
        if (_combinedMaxHealth > 0f)
        {
            BossHealthBar.SetHealth(CombinedHealth, _combinedMaxHealth);
        }

        if (BothDown)
        {
            BossHealthBar.Hide();
            Destroy(gameObject); // stops the adds with it
        }
    }

    private float CombinedHealth
    {
        get
        {
            float total = 0f;
            if (Alive(_left)) total += _left.GetHealth();
            if (Alive(_right)) total += _right.GetHealth();
            return Mathf.Max(0f, total);
        }
    }

    private static bool Alive(EnemyGiantSlime slime) => slime != null && !slime.IsDead;

    private bool BothDown => !Alive(_left) && !Alive(_right);

    // One clock for both twins: telegraph together, fire together, alternate the
    // pattern. Alternating rather than rolling for it — wave content is never random
    // (see the design pillar in Spawner.SpawnBat).
    private IEnumerator VolleyClock()
    {
        int volley = 0;
        while (!BothDown)
        {
            yield return new WaitForSeconds(_config.volleyInterval);
            if (BothDown) break;

            // Once a twin raises its ward it stops shooting; when both have, there's
            // nothing to telegraph and the clock just idles
            bool leftShoots = Alive(_left) && !_left.InWardPhase;
            bool rightShoots = Alive(_right) && !_right.InWardPhase;
            if (!leftShoots && !rightShoots) continue;

            if (leftShoots) _left.Telegraph(_config.telegraphPause);
            if (rightShoots) _right.Telegraph(_config.telegraphPause);
            AudioManager.Instance?.PlaySFX(AudioManager.Instance.bossTelegraph);

            yield return new WaitForSeconds(_config.telegraphPause);
            if (BothDown) break;

            var pattern = (volley % 2 == 0)
                ? EnemyGiantSlime.VolleyPattern.MirroredArc
                : EnemyGiantSlime.VolleyPattern.Straight;

            // Same frame for both — that simultaneity is the pair's whole signature
            if (Alive(_left) && !_left.InWardPhase) _left.FireVolley(pattern);
            if (Alive(_right) && !_right.InWardPhase) _right.FireVolley(pattern);

            volley++;
        }
    }

    // Ordinary slimes ooze in from top and bottom centre for the whole fight, well
    // clear of the twins' horizontal lanes. Sizes and targets alternate rather than
    // rolling, for the same determinism reason as the volley patterns.
    private IEnumerator AddTrickle()
    {
        yield return new WaitForSeconds(_adds.firstDelay);

        int cycle = 0;
        while (!BothDown)
        {
            if (_spawner != null)
            {
                for (int i = 0; i < Mathf.Max(0, _adds.perInterval); i++)
                {
                    bool fromTop = i % 2 == 0;
                    Vector2 entry = fromTop ? _adds.topEntry : _adds.bottomEntry;
                    // Alternate 1 / 2 so each pair brings one of each, swapping ends
                    int size = ((cycle + i) % 2 == 0) ? 1 : 2;
                    Transform target = ((cycle + i) % 2 == 0)
                        ? _spawner.LeftPlayer
                        : _spawner.RightPlayer;
                    _spawner.SpawnSlime(size, entry, 0f, target);
                }
                AudioManager.Instance?.PlaySFX(AudioManager.Instance.slimeSpawn);
            }

            cycle++;
            yield return new WaitForSeconds(_adds.interval);
        }
    }

    // Mana, mana, mana, then one health orb last of all, skimming the low lane.
    // Spacing is fight/(n+1) rather than fight/n: with n intervals the last orb would
    // land exactly as a twin arrives, which is far too late to spend. This way the run
    // of orbs is evenly spaced AND both ends of the fight get breathing room, and the
    // health orb lands deep in the ward phase where it's worth most.
    private IEnumerator OrbDrops()
    {
        int count = Mathf.Max(0, _orbs.manaOrbs) + (_orbs.finalHealthOrb ? 1 : 0);
        if (count == 0 || _spawner == null) yield break;

        float fight = EstimatedFightSeconds;
        if (fight <= 0f) yield break;
        float step = fight / (count + 1);

        for (int i = 0; i < count; i++)
        {
            yield return new WaitForSeconds(step);
            if (BothDown) yield break;

            bool isHealth = _orbs.finalHealthOrb && i == count - 1;
            // Alternate the crossing direction so the run of orbs doesn't all sweep
            // the same way (deterministic, like everything else here)
            bool leftToRight = i % 2 == 0;
            float from = leftToRight ? -_orbs.travelX : _orbs.travelX;
            _spawner.SpawnOrb(new Vector2(from, _orbs.lane),
                new Vector2(-from, _orbs.lane), isHealth, 0f);
        }
    }

    // The twins are symmetric, so either one's crossing time is the fight's length
    private float EstimatedFightSeconds
    {
        get
        {
            if (Alive(_left)) return _left.EstimatedSecondsToContact;
            if (Alive(_right)) return _right.EstimatedSecondsToContact;
            return 0f;
        }
    }

    // Covers death, despawn and scene unload alike
    private void OnDestroy()
    {
        BossHealthBar.Hide();
    }
}
