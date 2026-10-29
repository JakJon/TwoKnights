using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The Mine's true boss: the Overseer, rebuilt to the owner's sketch on
// 2026-10-19.
//
// THE BOARD. The knights stand inside three loops of track, and the loops are
// full of crimson carts that no weapon can break. The Overseer rides a straight
// track of his own across the top, outside all three. Every arrow at him has to
// find a gap in three moving walls at once: the inner and outer loops run
// clockwise and the middle one runs the other way, so the gaps line up and
// close again on a rhythm the player can learn.
//
// THE OPENING (owner, 2026-10-20). The track lays, the inner walls fall, the
// first ambush rolls out at once, and only when all of it is on the track does he
// roll in — his name plate fading up as he comes into view.
//
// THE KEY. Ambushes of gnomes roll onto the outer loop from off-screen, and one
// of each is blue. Kill him and every crimson cart turns into an ordinary grey
// cart for fifteen seconds (EnemyCrimsonCart) — the one chance to break the walls
// down — and the next ambush comes 25 seconds after the carts go hard again.
// Each ambush also brings crimson carts with it, more of them the fewer are
// left, and they only ever come in on the OUTER loop:
// the inner two have no way in from off-screen, so their carts fall from above
// at the start and never come back once they are broken. Clearing an inner loop
// is progress that sticks.
//
// THE PATTERN. His axes are not on a timer. Each stage has a fixed list of
// attacks — rock volleys, axe throws, and the two at once on different knights —
// and one clock runs it, spacing every entry by when it LANDS. The gnomes throw on their
// own clocks, so they hold each throw until its landing slot is free
// (IncomingLedger, the fire ogre's rule), and the list books its next entry's
// landings in advance so they can see it coming. He rides at a known speed and
// turns by a known rule, so an axe entry is planned for when he will actually be
// within throwing range of the knight each axe is for — the ordinary pickaxe
// gnome's rule (EnemyDarkGnomeCart.ReadyAt). If a dart, a freeze or the roar
// makes him late anyway, the list waits for him and everything after it moves
// back with it — nothing gets squeezed together.
//
// THE LADDER, all on his health — stages of 350, 350, 350 and 450 (owner,
// 2026-10-20):
//   1500  wraps round the screen, six axes at a time
//   1150  faster, eight at a time; a health orb rises just outside the inner loop
//    800  faster again, ten at a time, and he starts turning round at the ends;
//         the two inner walls are filled back up; two mana orbs, one either side
//         of the middle loop
//    450  he roars, comes back faster, twelve at a time, and wraps twice for every
//         time he turns round; a health orb runs just under his track
// Every stage sends green ogres from the bottom as it starts: one from each
// corner, and from the second stage one, then two, from the bottom centre a
// little later. 15 seconds after each ambush's blue gnome dies he makes a bomb
// run: three bombs in one pass, left of the left knight, between the knights,
// right of the right knight — and in the first three stages a second blue gnome
// rides out alone just before it, so the walls can be opened again. He never
// moves the next ambush, which comes 25 seconds after the window its own blue
// gnome opened shuts; and as it begins, every empty grey cart shatters.
// The numbers live on the asset (stages, roarAtHealth, orbDrops); this is the
// shape OverseerWiring writes.
//
// Everything is fixed, no dice (the no-randomness pillar): the attack lists, the
// cart groups, the gnome rotation and the refill rule are all counting, and the
// stages are health, not time. Two players who play it the same way meet the
// same fight.
[CreateAssetMenu(fileName = "DarkGnome", menuName = "Waves/The Overseer (Boss)")]
public class DarkGnome : BaseWave
{
    [Header("Track")]
    [Tooltip("His straight track, the three loops, and the two ways onto the outer loop from off-screen.")]
    [SerializeField] private RailLayout railLayout;

    [Tooltip("Run index of his track, east-bound. He enters here.")]
    [SerializeField] private int bossLine = 0;

    [Tooltip("Run index of the same track west-bound — lays no pieces; he rides it after a U-turn.")]
    [SerializeField] private int bossReturnLine = 1;

    [Tooltip("Any run of the inner loop")]
    [SerializeField] private int innerLine = 2;

    [Tooltip("Any run of the middle loop")]
    [SerializeField] private int middleLine = 6;

    [Tooltip("Any run of the outer loop")]
    [SerializeField] private int outerLine = 10;

    [Tooltip("The off-screen run feeding the outer loop at its top-left corner")]
    [SerializeField] private int leftEntranceLine = 14;

    [Tooltip("The off-screen run feeding the outer loop at its top-right corner")]
    [SerializeField] private int rightEntranceLine = 15;

    [Header("The Overseer")]
    [Tooltip("The black gnome in the gold cart. Needs an EnemyDarkGnomeCart.")]
    [SerializeField] private GameObject bossPrefab;

    [Tooltip("Seconds after the first ambush is all out on the track before he rolls in")]
    [SerializeField] private float entryDelay = 0.5f;

    [Tooltip("Seconds after he rolls in before the first attack, so the wave name and his health bar have the screen to themselves")]
    [SerializeField] private float firstAttackDelay = 3.5f;

    [Tooltip("He stops and roars the first frame he is at or below this.")]
    [SerializeField] private float roarAtHealth = 1000f;

    [Tooltip("One stage per thousand of his health, highest first.")]
    [SerializeField] private List<OverseerStage> stages = new List<OverseerStage>();

    [Tooltip("Rock speed for the attack lists. 0 = the rock prefab's own.")]
    [SerializeField] private float rockSpeed = 0f;

    [Header("Crimson carts")]
    [Tooltip("Needs an EnemyCrimsonCart.")]
    [SerializeField] private GameObject crimsonCartPrefab;

    [Tooltip("The inner loop's carts, as groups: each number is one group, nose to tail, and the groups are spread evenly round the loop.")]
    [SerializeField] private List<int> innerGroups = new List<int> { 2, 2, 2 };

    [Tooltip("The middle loop's carts, as groups.")]
    [SerializeField] private List<int> middleGroups = new List<int> { 2, 3, 2, 3, 2 };

    [Tooltip("Single carts spread evenly round the outer loop, rolled in from off-screen at the start.")]
    [SerializeField] private int outerSingles = 10;

    [Tooltip("How far above the track an inner-loop cart starts its fall")]
    [SerializeField] private float dropHeight = 7f;

    [Tooltip("Seconds one cart takes to fall")]
    [SerializeField] private float dropSeconds = 0.55f;

    [Tooltip("Seconds between one cart starting to fall and the next")]
    [SerializeField] private float dropStagger = 0.12f;

    [Tooltip("Kicked up where a falling cart lands. Optional.")]
    [SerializeField] private GameObject landingDust;

    [Tooltip("Played as a falling cart lands. Optional.")]
    [SerializeField] private SoundEffect landingSound;

    [Tooltip("Most carts of any kind the outer loop is allowed to hold. An ambush brings fewer crimson carts rather than pack it solid, so it always has gaps.")]
    [SerializeField] private int outerCap = 24;

    [Tooltip("Track kept clear either side of a cart joining the outer loop, in world units. It waits off-screen until there is that much room.")]
    [SerializeField] private float mergeGap = 1.6f;

    [Header("Ambushes")]
    [Tooltip("Seconds after the inner walls have landed before the first ambush rolls out (owner, 2026-10-20: straight away). He only rolls in once it is all out on the track.")]
    [SerializeField] private float firstAmbushAfter = 0f;

    [Tooltip("Seconds after the crimson carts go hard again — the end of the window the ambush's blue gnome opened — before the next ambush comes (owner, 2026-10-20). The ambush's other gnomes do not hold it back.")]
    [SerializeField] private float nextAmbushAfterWindow = 25f;

    [Tooltip("The gnomes sent, one of each in turn, carried over from one ambush to the next")]
    [SerializeField] private List<GameObject> gnomeRotation = new List<GameObject>();

    [Tooltip("The key. One rides in every ambush, in the middle of the gnomes. Needs an EnemyBlueGnomeCart.")]
    [SerializeField] private GameObject blueGnomePrefab;

    [Header("The bomb run")]
    [Tooltip("Seconds after each ambush's blue gnome dies before he makes his bomb run: three bombs in one pass, left of the left knight, between the knights, right of the right knight (owner, 2026-10-20). 15 lands it as the crimson carts go hard again.")]
    [SerializeField] private float bombRunAfterBlue = 15f;

    [Tooltip("Quiet after the last bomb of a run goes off before the attack list carries on")]
    [SerializeField] private float bombRunRest = 2.5f;

    [Tooltip("In a stage with secondBlueGnome, seconds before the bomb run's first bomb that the second blue gnome rolls onto the loop (owner, 2026-10-20: 5)")]
    [SerializeField] private float secondBlueLead = 5f;

    [Header("Orbs")]
    [Tooltip("Released on his health, highest first. A boss is outside the three-orb cap (WaveOrbBudget).")]
    [SerializeField] private List<OverseerOrbDrop> orbDrops = new List<OverseerOrbDrop>();

    // Everything one fight changes as it goes. A new one per SpawnWave: the wave
    // asset is shared and outlives the run, so nothing that moves may live on it.
    private sealed class Fight
    {
        public int Stage;
        public int Rotation;
        public int StartCount;
        public RailLoopMap Outer;
        public readonly List<RingPlan> Walls = new List<RingPlan>();

        // When the next bomb run is due, or -1 for none. Set by the ambushes,
        // spent by the attack list.
        public float BombRunAt = -1f;
    }

    // Where every cart of an inner wall belongs: its place round the loop at one
    // moment (Origin), which the loop then carries round at track speed. A refill
    // finds the empty places by carrying them forward to now.
    private sealed class RingPlan
    {
        public RailLoopMap Map;
        public float Origin;
        public readonly List<float> Places = new List<float>();
    }

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        RailNetwork rails = spawner.Rails;
        if (rails == null || bossPrefab == null || stages == null || stages.Count == 0)
        {
            Debug.LogWarning("[Overseer] No RailNetwork in the scene, no boss prefab, or no stages — there is no fight.");
            MarkSpawningComplete();
            yield break;
        }

        // A window left open by a fight that ended mid-way would open this one's walls
        EnemyCrimsonCart.CloseWindow();

        float layDuration = rails.Lay(railLayout);

        var fight = new Fight
        {
            StartCount = Total(innerGroups) + Total(middleGroups) + Mathf.Max(0, outerSingles),
            Outer = RailLoopMap.Build(rails, outerLine)
        };
        RailLoopMap inner = RailLoopMap.Build(rails, innerLine);
        RailLoopMap middle = RailLoopMap.Build(rails, middleLine);
        if (inner == null || middle == null || fight.Outer == null)
        {
            Debug.LogWarning("[Overseer] One of the three loops does not close on itself — check the layout's teleports.");
        }

        yield return new WaitForSeconds(layDuration);

        // The walls go up first: the inner two fall from above, the outer one rolls in
        var walls = new List<Coroutine>();
        Coroutine innerDrop = inner != null ? spawner.StartCoroutine(DropRing(rails, inner, innerGroups, 0f, fight)) : null;
        Coroutine middleDrop = middle != null
            ? spawner.StartCoroutine(DropRing(rails, middle, middleGroups, dropStagger * 0.5f, fight))
            : null;
        if (innerDrop != null) walls.Add(innerDrop);
        if (middleDrop != null) walls.Add(middleDrop);
        if (fight.Outer != null) walls.Add(spawner.StartCoroutine(RollInOuter(rails, fight.Outer)));

        // The first ambush rolls out the moment the inner walls are down, before
        // he has shown himself at all (owner, 2026-10-20)
        if (innerDrop != null) yield return innerDrop;
        if (middleDrop != null) yield return middleDrop;
        yield return new WaitForSeconds(dropSeconds + Mathf.Max(0f, firstAmbushAfter));

        var firstAmbush = new List<EnemyBase>();
        yield return ReleaseAmbush(rails, null, fight, firstAmbush, 1);

        // ...and he rolls in once all of it is out on the track
        while (StillComingIn(firstAmbush)) yield return null;
        yield return new WaitForSeconds(Mathf.Max(0f, entryDelay));

        OverseerStage opening = stages[0];
        MineCart bossCart = rails.SpawnCart(bossPrefab, bossLine,
                                            MineCart.TrackSpeed * Mathf.Max(0.1f, opening.speedScale), false);
        EnemyDarkGnomeCart boss = bossCart != null ? bossCart.GetComponent<EnemyDarkGnomeCart>() : null;
        if (boss == null)
        {
            Debug.LogWarning("[Overseer] The boss prefab has no EnemyDarkGnomeCart — the fight has no boss in it.");
            MarkSpawningComplete();
            yield break;
        }

        boss.SetTrack(bossLine, bossReturnLine);
        boss.SetTurns(opening.turns);

        Coroutine attacks = spawner.StartCoroutine(RunAttacks(spawner, boss, fight));
        Coroutine ambushes = spawner.StartCoroutine(RunAmbushes(spawner, rails, boss, fight, firstAmbush));

        // The ladder owns the rest: it watches his health and nothing else
        yield return spawner.StartCoroutine(RunLadder(spawner, boss, fight));

        // He is down. The rock stops with him; whatever gnomes are still on the
        // track are kills the wave is still waiting on, and they keep fighting.
        // The crimson carts never were — they are scenery, and roll on regardless.
        spawner.StopCoroutine(attacks);
        spawner.StopCoroutine(ambushes);
        for (int i = 0; i < walls.Count; i++)
        {
            if (walls[i] != null) spawner.StopCoroutine(walls[i]);
        }

        MarkSpawningComplete();
    }

    // ---- the ladder ----

    // Watches his health. Thresholds fire in order and never unfire, so a burst
    // that takes him through two of them gets both rather than skipping one.
    private IEnumerator RunLadder(Spawner spawner, EnemyDarkGnomeCart boss, Fight fight)
    {
        int nextOrb = 0;
        bool roared = false;

        // The first stage starts as he rolls in
        SendOgres(spawner, stages[0]);


        while (boss != null && !boss.IsDead)
        {
            float hp = boss.GetHealth();

            while (fight.Stage + 1 < stages.Count && hp <= stages[fight.Stage + 1].fromHealth)
            {
                fight.Stage++;
                OverseerStage stage = stages[fight.Stage];
                boss.SetTurns(stage.turns);
                float speed = MineCart.TrackSpeed * Mathf.Max(0.1f, stage.speedScale);

                if (!roared && stage.fromHealth <= roarAtHealth)
                {
                    // The roar is this stage's speed-up: he stops, and comes out of it faster
                    roared = true;
                    boss.BeginLastStand(speed);
                }
                else
                {
                    boss.SetSpeed(speed);
                    if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(AudioManager.Instance.cartWhistle);
                }

                Debug.Log($"[Overseer] '{stage.label}' at {hp:F0} HP — {speed:F2} u/s, {stage.turns}, " +
                          $"{stage.axesPerThrow} axes a throw, {stage.gnomesPerAmbush} gnomes an ambush.");

                if (stage.refillInnerLoops && spawner.Rails != null)
                {
                    spawner.StartCoroutine(RefillWalls(spawner.Rails, fight));
                }

                SendOgres(spawner, stage);
            }

            // A roar authored at a health no stage starts on still happens
            if (!roared && hp <= roarAtHealth)
            {
                roared = true;
                boss.BeginLastStand(MineCart.TrackSpeed * Mathf.Max(0.1f, stages[fight.Stage].speedScale));
            }

            while (nextOrb < orbDrops.Count && hp <= orbDrops[nextOrb].atHealth)
            {
                OverseerOrbDrop drop = orbDrops[nextOrb];
                spawner.SpawnOrb(drop.from, drop.to, drop.healthOrb);
                Debug.Log($"[Overseer] {(drop.healthOrb ? "Health" : "Mana")} orb '{drop.label}' at {hp:F0} HP.");
                nextOrb++;
            }

            yield return null;
        }
    }

    // ---- the ogres ----

    // A stage's green ogres (owner, 2026-10-20: one set per phase), each on its
    // own delay from the stage's start. They walk in like any wave's ogres and are
    // kills the wave waits on, so one still walking when he dies keeps the wave
    // going.
    private static void SendOgres(Spawner spawner, OverseerStage stage)
    {
        if (stage == null || stage.ogres == null) return;
        for (int i = 0; i < stage.ogres.Count; i++)
        {
            OverseerOgre ogre = stage.ogres[i];
            if (ogre == null) continue;
            spawner.SpawnOgre(ogre.at, Mathf.Max(0f, ogre.after), false, null, KnightFor(spawner, ogre.mark));
        }
        if (stage.ogres.Count > 0)
        {
            Debug.Log($"[Overseer] '{stage.label}': {stage.ogres.Count} green ogre(s) on their way.");
        }
    }

    private static Transform KnightFor(Spawner spawner, OgreMark mark)
    {
        if (mark == OgreMark.LeftKnight) return spawner.LeftPlayer;
        if (mark == OgreMark.RightKnight) return spawner.RightPlayer;
        return null; // the ogre reads the nearer knight off where it lands
    }

    // ---- the attack list ----
    //
    // One clock for the rock and his axes, and it places every entry by when it
    // LANDS: an entry's first landing comes restAfter after the previous entry's
    // last one.
    //
    // Rock is slow — a shaft is seven units at about a unit a second, a weave or a
    // spiral nearly twice that — and his axes fly for 1.1 seconds, so rock meant to
    // land after an entry has to leave long BEFORE it. Two rules make that work:
    //   * Rock is committed up to CommitAhead seconds early: sent on a delay and
    //     booked in the IncomingLedger at once, so the list can reach past it to
    //     whatever comes next.
    //   * An axe entry looks further down the list. Rock that comes straight
    //     after it is committed before the axes go, timed to land restAfter
    //     behind them, and so on for as long as rock follows rock.
    //
    // His axes are the one part that can be late: he can be off-screen, asleep,
    // frozen or roaring when they are due. They wait for him, nothing new is
    // committed while they wait, and so everything after them slides back with
    // them. Rock already committed cannot slide, so the axes also wait until none
    // of their landings would come within AxeClearance of anything else due at the
    // same knight — late axes drop in behind the rock instead of on top of it.

    /// <summary>How far ahead of its release rock is committed. Covers the slowest rock's head start — a spiral or a weave is in the air for ten seconds and more.</summary>
    public const float CommitAhead = 12f;

    /// <summary>The least time between one of his axes landing and anything else landing at the same knight.</summary>
    public const float AxeClearance = 0.6f;

    // How long before it must leave the rock behind an axe entry is committed,
    // after one last look at where he is
    private const float ForecastLead = 0.1f;

    // Walks the stage lists. A new stage starts its own list from the top.
    private sealed class Cursor
    {
        private int _stage = -1;
        private int _index;

        public OverseerStage Stage { get; private set; }

        public OverseerAttack Next(List<OverseerStage> stages, int stage)
        {
            if (stage != _stage)
            {
                _stage = stage;
                _index = 0;
            }

            Stage = stages[stage];
            if (Stage.attacks == null || Stage.attacks.Count == 0) return null;

            OverseerAttack attack = Stage.attacks[_index % Stage.attacks.Count];
            _index++;
            return attack;
        }
    }

    private IEnumerator RunAttacks(Spawner spawner, EnemyDarkGnomeCart boss, Fight fight)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, firstAttackDelay));

        var shots = new List<RockVolley.PlannedShot>();
        var companion = new List<RockVolley.PlannedShot>();
        var axes = new List<OverseerAttack.AxeShot>();
        var cursor = new Cursor();

        OverseerAttack held = null;      // taken off the list by an axe entry's look ahead, not yet played
        OverseerStage heldStage = null;
        float lastLanding = Time.time;
        float rest = 0f;

        while (Alive(boss))
        {
            // ---- the bomb run ----
            // It goes in ahead of whatever the list would send next, placed like
            // everything else: after the last thing already on its way has landed
            if (fight.BombRunAt > 0f && Time.time >= fight.BombRunAt - CommitAhead)
            {
                float earliestDrop = Mathf.Max(fight.BombRunAt, lastLanding + rest - boss.BombFlightTime);
                fight.BombRunAt = -1f;

                var outcome = new RunOutcome();
                yield return BombRun(spawner, boss, fight, earliestDrop, outcome);
                if (!Alive(boss)) yield break;

                if (outcome.LastLanding > lastLanding)
                {
                    lastLanding = outcome.LastLanding;
                    rest = Mathf.Max(0f, bombRunRest);
                }
                continue;
            }

            OverseerAttack attack;
            OverseerStage stage;
            if (held != null)
            {
                attack = held;
                stage = heldStage;
                held = null;
            }
            else
            {
                attack = cursor.Next(stages, fight.Stage);
                stage = cursor.Stage;
            }

            if (attack == null)
            {
                yield return null;
                continue;
            }

            // ---- rock ----
            if (attack.kind == OverseerAttackKind.Rock)
            {
                shots.Clear();
                RockVolley.Plan(spawner, attack.rock, rockSpeed, shots);
                float release = lastLanding + rest - FirstLanding(spawner, shots);

                while (Alive(boss) && Time.time < release - CommitAhead) yield return null;
                if (!Alive(boss)) yield break;

                release = Mathf.Max(release, Time.time);
                CommitRock(spawner, boss, attack.rock, shots, release);
                lastLanding = release + LastLanding(spawner, shots);
                rest = Mathf.Max(0f, attack.restAfter);
                LogEntry(attack, stage, release, lastLanding);
                continue;
            }

            // ---- axes, alone or with rock at the other knight ----
            float flight = boss.AxeFlightTime;
            float busy = attack.PlanAxes(stage.axesPerThrow, axes);

            bool together = attack.kind == OverseerAttackKind.Together && attack.rock != null;
            float offset = together ? attack.rockOffset : 0f;
            float companionFirst = 0f, companionLast = 0f, companionEnd = 0f;
            if (together)
            {
                companion.Clear();
                RockVolley.Plan(spawner, attack.rock, rockSpeed, companion);
                companionFirst = FirstLanding(spawner, companion);
                companionLast = LastLanding(spawner, companion);
            }

            // The first thing of this entry to land comes restAfter after the last
            // thing before it — the rock, if it was authored to land first
            float due = lastLanding + rest - flight - Mathf.Max(0f, -offset);

            while (Alive(boss) && Time.time < due - CommitAhead) yield return null;
            if (!Alive(boss)) yield break;

            // Planned for when he will actually be there to throw them, not just
            // for when the list would like them
            due = boss.ReadyAt(Mathf.Max(due, Time.time), axes, spawner.LeftPlayer, spawner.RightPlayer);

            // The rock that goes with them leaves now or later, never in the past:
            // if it cannot make it, the whole entry moves back until it can
            if (together)
            {
                for (int tries = 0; tries < 3; tries++)
                {
                    float late = Time.time - (due + flight + offset - companionFirst);
                    if (late <= 0f) break;
                    due = boss.ReadyAt(due + late, axes, spawner.LeftPlayer, spawner.RightPlayer);
                }

                float companionRelease = Mathf.Max(Time.time, due + flight + offset - companionFirst);
                CommitRock(spawner, boss, attack.rock, companion, companionRelease);
                companionEnd = companionRelease + companionLast;
                LogEntry(attack, stage, companionRelease, companionEnd);
            }

            // Booked under a token of their own, so a gnome stays out of the way
            // and the axes can still ask whether everything ELSE is clear
            object token = new object();
            BookAxes(spawner, axes, due + flight, token);

            // Rock that follows has to leave before these do. Every rock entry
            // straight after them that would have to be in the air before the last
            // axe leaves is committed now, each timed off the one before it; the
            // first entry that can wait is held for the next time round the loop.
            bool reforecast = false;
            float chainLast = Mathf.Max(due + busy + flight, companionEnd);
            float chainRest = Mathf.Max(0f, attack.restAfter);
            while (true)
            {
                OverseerAttack following = cursor.Next(stages, fight.Stage);
                if (following == null || following.kind != OverseerAttackKind.Rock)
                {
                    held = following;
                    heldStage = cursor.Stage;
                    break;
                }

                shots.Clear();
                RockVolley.Plan(spawner, following.rock, rockSpeed, shots);
                float release = chainLast + chainRest - FirstLanding(spawner, shots);
                if (release >= due + busy)
                {
                    held = following;
                    heldStage = cursor.Stage;
                    break;
                }

                // The first rock behind them is sent as late as it can be, after a
                // fresh look at where he is: the nearer the throw, the better the
                // forecast, and once the rock is in the air it cannot be taken back
                if (!reforecast)
                {
                    reforecast = true;
                    while (Alive(boss) && Time.time < Mathf.Min(release, due) - ForecastLead) yield return null;
                    if (!Alive(boss)) yield break;

                    float again = boss.ReadyAt(Mathf.Max(due, Time.time), axes, spawner.LeftPlayer, spawner.RightPlayer);
                    if (again > due + 0.05f)
                    {
                        due = again;
                        BookAxes(spawner, axes, due + flight, token);
                        chainLast = Mathf.Max(due + busy + flight, companionEnd);
                        release = chainLast + chainRest - FirstLanding(spawner, shots);
                        if (release >= due + busy)
                        {
                            held = following;
                            heldStage = cursor.Stage;
                            break;
                        }
                    }
                }

                release = Mathf.Max(release, Time.time);
                CommitRock(spawner, boss, following.rock, shots, release);
                chainLast = release + LastLanding(spawner, shots);
                chainRest = Mathf.Max(0f, following.restAfter);
                LogEntry(following, cursor.Stage, release, chainLast);
            }

            while (Alive(boss) && Time.time < due) yield return null;

            // Every axe has to leave with its own knight in range, the whole entry
            // has to be able to finish that way from here, and the slot has to be
            // clear. If any is not, the list waits — and everything after waits
            // with it.
            var waits = new Dictionary<string, float>();
            Transform opener = axes.Count > 0 && axes[0].Right ? spawner.RightPlayer : spawner.LeftPlayer;
            while (Alive(boss))
            {
                EnemyDarkGnomeCart.NotReady why = boss.WhyNotReady(opener);
                string reason = why != EnemyDarkGnomeCart.NotReady.Ready ? why.ToString()
                              : boss.ReadyAt(Time.time, axes, spawner.LeftPlayer, spawner.RightPlayer) > Time.time + 0.01f
                                  ? "WontFinishInRange"
                              : (AxesClear(spawner, axes, Time.time + flight, token) ? null : "SlotTaken");
                if (reason == null) break;

                float so;
                waits.TryGetValue(reason, out so);
                waits[reason] = so + Time.deltaTime;
                yield return null;
            }
            if (!Alive(boss)) yield break;

            float start = Time.time;
            float lastThrow = start;
            for (int i = 0; i < axes.Count; i++)
            {
                while (Time.time < start + axes[i].Release) yield return null;

                // Put to sleep or frozen mid-throw, or the next landing taken: the
                // rest wait, keeping their spacing
                Transform knight = axes[i].Right ? spawner.RightPlayer : spawner.LeftPlayer;
                while (Alive(boss))
                {
                    EnemyDarkGnomeCart.NotReady why = boss.WhyNotReady(knight);
                    string reason = why != EnemyDarkGnomeCart.NotReady.Ready ? "mid-throw " + why
                                  : (IncomingLedger.IsClear(knight, Time.time + flight, AxeClearance, token) ? null : "mid-throw SlotTaken");
                    if (reason == null) break;

                    float so;
                    waits.TryGetValue(reason, out so);
                    waits[reason] = so + Time.deltaTime;
                    start += Time.deltaTime;
                    yield return null;
                }
                if (!Alive(boss)) yield break;

                boss.ThrowAxeAt(knight, token);
                lastThrow = Time.time;
            }

            float axesLast = lastThrow + flight;
            LogWait(attack, waits);
            LogEntry(attack, stage, start, axesLast);

            // Whichever landed last sets the next gap: normally the rock behind
            // them; the axes themselves if they were late enough to land after it
            if (chainLast > axesLast)
            {
                lastLanding = chainLast;
                rest = chainRest;
            }
            else
            {
                lastLanding = axesLast;
                rest = Mathf.Max(0f, attack.restAfter);
            }
        }
    }

    // What a run inside the attack list leaves behind for the list
    private sealed class RunOutcome
    {
        public float LastLanding;
    }

    // ---- the bomb run (owner, 2026-10-20) ----
    //
    // Three bombs in one pass: left of the left knight, between the knights, and
    // right of the right knight, each the knights' own gap out from them, so the
    // blast reaches exactly as it does from the midpoint. He drops each one as he
    // crosses its spot. The pass is chosen from the forecast and booked at once,
    // so the gnomes stay out of its way; each bomb is then dropped on his real
    // crossing, so a hit-pause makes it a little late rather than in the wrong place.
    private IEnumerator BombRun(Spawner spawner, EnemyDarkGnomeCart boss, Fight fight, float earliestDrop,
                                RunOutcome outcome)
    {
        Transform left = spawner.LeftPlayer;
        Transform right = spawner.RightPlayer;
        if (left == null || right == null || !boss.HasBombs) yield break;

        float gap = (right.position.x - left.position.x) * 0.5f;
        var spots = new float[]
        {
            left.position.x - gap,
            (left.position.x + right.position.x) * 0.5f,
            right.position.x + gap
        };
        float flight = boss.BombFlightTime;

        var times = new float[spots.Length];
        object token = new object();
        float firstDrop = earliestDrop;
        if (boss.PlanBombRun(Mathf.Max(earliestDrop, Time.time), spots, times))
        {
            firstDrop = float.PositiveInfinity;
            for (int i = 0; i < spots.Length; i++)
            {
                firstDrop = Mathf.Min(firstDrop, times[i]);
                BookBomb(spawner, i, times[i] + flight, token);
            }
        }

        // In the first three phases a second blue gnome rides out on his own ahead
        // of it, so the walls can be opened again while the bombs are coming down
        OverseerStage stage = stages[Mathf.Clamp(fight.Stage, 0, stages.Count - 1)];
        if (stage.secondBlueGnome && blueGnomePrefab != null && spawner.Rails != null)
        {
            spawner.StartCoroutine(SendSecondBlue(spawner.Rails, boss, fight, firstDrop - Mathf.Max(0f, secondBlueLead)));
        }

        while (Alive(boss) && Time.time < firstDrop - 0.25f) yield return null;

        var dropped = new bool[spots.Length];
        int remaining = spots.Length;
        float lastDrop = 0f;
        float lastX = boss.transform.position.x;
        float giveUp = Time.time + 30f;

        while (Alive(boss) && remaining > 0 && Time.time < giveUp)
        {
            yield return null;
            float x = boss.transform.position.x;

            // A wrap or a turn moves him a long way in one frame: not a crossing
            bool jumped = Mathf.Abs(x - lastX) > 2f;
            for (int i = 0; i < spots.Length && !jumped; i++)
            {
                if (dropped[i] || Mathf.Approximately(x, lastX)) continue;
                if ((lastX - spots[i]) * (x - spots[i]) > 0f) continue;

                boss.DropBombAt(spots[i]);
                dropped[i] = true;
                remaining--;
                lastDrop = Time.time;
            }
            lastX = x;
        }

        outcome.LastLanding = lastDrop > 0f ? lastDrop + flight : 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Overseer] Bomb run: {spots.Length - remaining} of {spots.Length} dropped, " +
                  $"the last goes off at t={outcome.LastLanding:F2}.");
#endif
    }

    // The second blue gnome, alone, timed to roll onto the outer loop at
    // `comesOutAt`: he is sent down an entrance that long before, less the time
    // it takes to get from there onto the loop, and waits for a gap like any cart
    private IEnumerator SendSecondBlue(RailNetwork rails, EnemyDarkGnomeCart boss, Fight fight, float comesOutAt)
    {
        if (fight.Outer == null) yield break;

        float merge, travel;
        if (!Entrance(rails, fight.Outer, leftEntranceLine, blueGnomePrefab, out merge, out travel)) yield break;

        float sendAt = comesOutAt - travel;
        while (Time.time < sendAt)
        {
            if (Over(boss)) yield break;
            yield return null;
        }

        int entrance;
        while (true)
        {
            if (Over(boss)) yield break;
            if (CanEnter(rails, fight.Outer, leftEntranceLine, blueGnomePrefab)) { entrance = leftEntranceLine; break; }
            if (CanEnter(rails, fight.Outer, rightEntranceLine, blueGnomePrefab)) { entrance = rightEntranceLine; break; }
            yield return null;
        }

        rails.SpawnCart(blueGnomePrefab, entrance, MineCart.TrackSpeed, false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Overseer] Second blue gnome sent at t={Time.time:F2}, onto the loop about t={Time.time + travel:F2}.");
#endif
    }

    // A side bomb's blast reaches only the knight beside it; the middle one's
    // reaches both
    private static void BookBomb(Spawner spawner, int spot, float goesOff, object token)
    {
        if (spot != 2) IncomingLedger.Book(spawner.LeftPlayer, goesOff, token);
        if (spot != 0) IncomingLedger.Book(spawner.RightPlayer, goesOff, token);
    }

    private static bool Alive(EnemyDarkGnomeCart boss) => boss != null && !boss.IsDead;

    // Sent on a delay and booked now. The rock stops with him: a volley still
    // waiting to leave when he dies never does.
    private void CommitRock(Spawner spawner, EnemyDarkGnomeCart boss, RockVolley volley,
                            List<RockVolley.PlannedShot> shots, float release)
    {
        BookRock(spawner, shots, release);
        spawner.StartCoroutine(FireAt(spawner, boss, volley, release));
    }

    private IEnumerator FireAt(Spawner spawner, EnemyDarkGnomeCart boss, RockVolley volley, float release)
    {
        while (Time.time < release)
        {
            if (!Alive(boss)) yield break;
            yield return null;
        }
        if (Alive(boss)) RockVolley.Fire(spawner, volley, rockSpeed);
    }

    private static bool AxesClear(Spawner spawner, List<OverseerAttack.AxeShot> axes, float firstLanding, object token)
    {
        for (int i = 0; i < axes.Count; i++)
        {
            Transform knight = axes[i].Right ? spawner.RightPlayer : spawner.LeftPlayer;
            if (!IncomingLedger.IsClear(knight, firstLanding + axes[i].Release, AxeClearance, token)) return false;
        }
        return true;
    }

    // Why an axe entry went late, when it did. Late axes push everything after
    // them back, so a fight that feels slow will say so here.
    private static void LogWait(OverseerAttack attack, Dictionary<string, float> waits)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        float waited = 0f;
        var parts = new List<string>();
        foreach (var pair in waits)
        {
            waited += pair.Value;
            parts.Add($"{pair.Key} {pair.Value:F1}s");
        }
        if (waited < 0.25f) return;
        Debug.Log($"[Overseer] '{attack.label}' waited {waited:F1}s for him: {string.Join(", ", parts)}.");
#endif
    }

    private static void LogEntry(OverseerAttack attack, OverseerStage stage, float release, float lastLanding)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Overseer] '{attack.label}' ({(stage != null ? stage.label : "?")}) leaves t={release:F2}, " +
                  $"last landing t={lastLanding:F2}.");
#endif
    }

    private float FirstLanding(Spawner spawner, List<RockVolley.PlannedShot> shots)
    {
        float first = float.PositiveInfinity;
        for (int i = 0; i < shots.Count; i++)
        {
            first = Mathf.Min(first, RockVolley.LandingOffset(spawner, shots[i], rockSpeed));
        }
        return float.IsPositiveInfinity(first) ? 0f : first;
    }

    private float LastLanding(Spawner spawner, List<RockVolley.PlannedShot> shots)
    {
        float last = 0f;
        for (int i = 0; i < shots.Count; i++)
        {
            last = Mathf.Max(last, RockVolley.LandingOffset(spawner, shots[i], rockSpeed));
        }
        return last;
    }

    private void BookRock(Spawner spawner, List<RockVolley.PlannedShot> shots, float releaseAt)
    {
        for (int i = 0; i < shots.Count; i++)
        {
            IncomingLedger.Book(shots[i].Knight, releaseAt + RockVolley.LandingOffset(spawner, shots[i], rockSpeed));
        }
    }

    private static void BookAxes(Spawner spawner, List<OverseerAttack.AxeShot> axes, float firstLanding, object token)
    {
        for (int i = 0; i < axes.Count; i++)
        {
            IncomingLedger.Book(axes[i].Right ? spawner.RightPlayer : spawner.LeftPlayer,
                                firstLanding + axes[i].Release, token);
        }
    }

    // ---- the walls ----

    // Groups spread evenly round a loop, dropped in from above one cart at a
    // time. Each cart is put where its place in the ring will BE when it lands,
    // so the ones that land first and start rolling do not open up gaps the
    // later ones were meant to close.
    private IEnumerator DropRing(RailNetwork rails, RailLoopMap ring, List<int> groups, float delay, Fight fight)
    {
        if (crimsonCartPrefab == null || groups == null || groups.Count == 0) yield break;
        if (delay > 0f) yield return new WaitForSeconds(delay);

        const float pitch = 1f; // one cell: nose to tail
        var plan = new RingPlan { Map = ring, Origin = Time.time + dropSeconds };
        for (int g = 0; g < groups.Count; g++)
        {
            int size = Mathf.Max(0, groups[g]);
            float centre = ring.Length * g / groups.Count;
            for (int c = 0; c < size; c++) plan.Places.Add(centre + (c - (size - 1) * 0.5f) * pitch);
        }
        fight.Walls.Add(plan);

        yield return DropInto(rails, plan, false);
    }

    // Every place in the plan gets a cart, or with onlyEmpty, every place that
    // has none in it when that cart would land
    private IEnumerator DropInto(RailNetwork rails, RingPlan plan, bool onlyEmpty)
    {
        float speed = MineCart.TrackSpeed;
        for (int i = 0; i < plan.Places.Count; i++)
        {
            float lands = Time.time + dropSeconds;
            float place = plan.Places[i] + speed * (lands - plan.Origin);
            if (onlyEmpty && Occupied(plan.Map, place, lands)) continue;

            int line;
            float along;
            plan.Map.Locate(place, out line, out along);

            MineCart cart = rails.SpawnCartAt(crimsonCartPrefab, line, along, speed);
            if (cart != null)
            {
                Vector3 spot = cart.transform.position;
                cart.DropIn(dropHeight, dropSeconds, () => Landed(spot));
            }

            yield return new WaitForSeconds(Mathf.Max(0.02f, dropStagger));
        }
    }

    // Is anything on this loop going to be at `place` at time `when`?
    private static bool Occupied(RailLoopMap map, float place, float when)
    {
        IReadOnlyList<MineCart> all = MineCart.All;
        for (int i = 0; i < all.Count; i++)
        {
            MineCart cart = all[i];
            if (cart == null || !cart.IsRiding) continue;

            float at;
            if (!map.TryPosition(cart.Line.Index, cart.Along, out at)) continue;
            if (!cart.IsHeld) at += cart.Speed * Mathf.Max(0f, when - Time.time);
            if (map.Gap(at, place) < 0.8f) return true;
        }
        return false;
    }

    // The inner two walls back to how they started (owner, 2026-10-20: at the
    // start of phase 3). Only the places that are empty get a cart; whatever is
    // still riding stays where it is.
    private IEnumerator RefillWalls(RailNetwork rails, Fight fight)
    {
        int before = EnemyCrimsonCart.LiveCount;
        for (int i = 0; i < fight.Walls.Count; i++)
        {
            yield return DropInto(rails, fight.Walls[i], true);
        }
        Debug.Log($"[Overseer] Inner walls refilled: {EnemyCrimsonCart.LiveCount - before} crimson cart(s) dropped back in.");
    }

    private void Landed(Vector3 at)
    {
        if (landingDust != null) Instantiate(landingDust, at, Quaternion.identity);
        if (landingSound != null && landingSound.clip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(landingSound);
        }
    }

    // The outer loop's opening singles, evenly spaced. Each one has a place in the
    // ring; it is sent down whichever entrance that place passes first, timed to
    // arrive exactly as its place goes by.
    private IEnumerator RollInOuter(RailNetwork rails, RailLoopMap outer)
    {
        int count = Mathf.Max(0, outerSingles);
        if (crimsonCartPrefab == null || count == 0) yield break;

        float speed = MineCart.TrackSpeed;
        float leftMerge, rightMerge, leftTravel, rightTravel;
        if (!Entrance(rails, outer, leftEntranceLine, crimsonCartPrefab, out leftMerge, out leftTravel)
            || !Entrance(rails, outer, rightEntranceLine, crimsonCartPrefab, out rightMerge, out rightTravel))
        {
            Debug.LogWarning("[Overseer] The outer loop's entrances are not wired to it — no opening carts.");
            yield break;
        }

        // Places measured at `origin`, a moment far enough off that a cart can be
        // sent down either entrance in time for any of them
        float origin = Time.time + Mathf.Max(leftTravel, rightTravel);
        var sends = new List<KeyValuePair<float, int>>();

        for (int k = 0; k < count; k++)
        {
            float place = outer.Length * k / count;
            float atLeft = origin + outer.Wrap(leftMerge - place) / speed;
            float atRight = origin + outer.Wrap(rightMerge - place) / speed;

            bool useLeft = atLeft <= atRight;
            float sendAt = useLeft ? atLeft - leftTravel : atRight - rightTravel;
            sends.Add(new KeyValuePair<float, int>(sendAt, useLeft ? leftEntranceLine : rightEntranceLine));
        }

        sends.Sort((a, b) => a.Key.CompareTo(b.Key));

        for (int i = 0; i < sends.Count; i++)
        {
            while (Time.time < sends[i].Key) yield return null;

            // The first ambush comes in through the same two entrances at the same
            // time, so this waits for a gap like every other cart joining the loop
            while (!CanEnter(rails, outer, sends[i].Value, crimsonCartPrefab)) yield return null;
            rails.SpawnCart(crimsonCartPrefab, sends[i].Value, speed, false);
        }
    }

    // Where an entrance joins the loop, and how long a cart of this kind takes to
    // get there from where it is spawned
    private static bool Entrance(RailNetwork rails, RailLoopMap loop, int entranceLine, GameObject prefab,
                                 out float merge, out float travelSeconds)
    {
        merge = 0f;
        travelSeconds = 0f;

        RailTeleporter pad = rails.ExitPadFor(entranceLine);
        if (pad == null || pad.Destination == null) return false;
        if (!loop.TryPosition(pad.Destination.LineIndex, pad.Destination.DistanceAlong, out merge)) return false;

        MineCart cart = prefab != null ? prefab.GetComponent<MineCart>() : null;
        float leadIn = cart != null ? cart.LeadIn : 6f;
        travelSeconds = (leadIn + pad.DistanceAlong) / MineCart.TrackSpeed;
        return true;
    }

    // ---- the ambushes ----

    // `first` is the ambush already sent before he rolled in; this carries on
    // from it rather than sending its own first one
    private IEnumerator RunAmbushes(Spawner spawner, RailNetwork rails, EnemyDarkGnomeCart boss, Fight fight,
                                    List<EnemyBase> first)
    {
        var gnomes = first ?? new List<EnemyBase>();
        int ambush = first != null ? 1 : 0;
        bool released = first != null;

        while (boss != null && !boss.IsDead)
        {
            if (!released)
            {
                gnomes.Clear();
                ambush++;
                ClearWrecks();
                yield return ReleaseAmbush(rails, boss, fight, gnomes, ambush);
            }
            released = false;

            // The next one is counted from the end of the window this ambush's
            // blue gnome opens: once he is dead and the crimson carts are hard
            // again (owner, 2026-10-20). The rest of the ambush may still be
            // riding; they no longer hold the next one back.
            // The window this ambush's OWN blue gnome opened, and nothing else, sets
            // the clock: the lone second blue gnome may open the walls again, but
            // the next ambush never waits on him (owner, 2026-10-20). So the end is
            // read off the window the moment this blue gnome dies, before the lone
            // one has even been sent.
            float hardAgain;
            EnemyBase blue = gnomes.Find(g => g is EnemyBlueGnomeCart);
            if (blue != null)
            {
                while (Alive(boss) && blue != null && !blue.IsDead) yield return null;
                if (!Alive(boss)) yield break;

                float died = EnemyCrimsonCart.WindowOpenedAt >= 0f ? EnemyCrimsonCart.WindowOpenedAt : Time.time;
                hardAgain = EnemyCrimsonCart.WindowEndsAt > died ? EnemyCrimsonCart.WindowEndsAt : Time.time;

                // His bomb run, once per ambush, timed from the blue gnome's death —
                // which can come before he has even rolled in
                if (bombRunAfterBlue >= 0f) fight.BombRunAt = died + bombRunAfterBlue;
            }
            else
            {
                // No blue gnome, so no window to count from: every gnome gone instead
                while (Alive(boss) && AnyAlive(gnomes)) yield return null;
                hardAgain = Time.time;
            }
            if (!Alive(boss)) yield break;

            float until = hardAgain + Mathf.Max(0f, nextAmbushAfterWindow);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Overseer] Ambush {ambush}'s window shuts at t={hardAgain:F2}; the next at t={until:F2}.");
#endif
            while (Time.time < until)
            {
                if (boss == null || boss.IsDead) yield break;
                yield return null;
            }
        }
    }

    private IEnumerator ReleaseAmbush(RailNetwork rails, EnemyDarkGnomeCart boss, Fight fight,
                                      List<EnemyBase> gnomes, int ambush)
    {
        OverseerStage stage = stages[fight.Stage];
        int size = Mathf.Max(1, stage.gnomesPerAmbush);
        int bluePlace = size / 2;

        var riders = new List<GameObject>();
        for (int i = 0; i < size; i++)
        {
            if (i == bluePlace && blueGnomePrefab != null)
            {
                riders.Add(blueGnomePrefab);
                continue;
            }
            if (gnomeRotation == null || gnomeRotation.Count == 0) continue;
            riders.Add(gnomeRotation[fight.Rotation % gnomeRotation.Count]);
            fight.Rotation++;
        }

        // The fewer crimson carts are left, the more come back: half of what is
        // missing, rounded up — but never so many the outer loop loses its gaps
        int missing = Mathf.Max(0, fight.StartCount - EnemyCrimsonCart.LiveCount);
        int bring = Mathf.CeilToInt(missing * 0.5f);
        int room = Mathf.Max(0, outerCap - CartsOnOuter(fight) - riders.Count);
        bring = Mathf.Min(bring, room);
        if (crimsonCartPrefab == null) bring = 0;

        // A crimson cart between gnomes, and any left over at the back
        var order = new List<GameObject>();
        int c = 0;
        for (int i = 0; i < riders.Count; i++)
        {
            order.Add(riders[i]);
            if (c < bring) { order.Add(crimsonCartPrefab); c++; }
        }
        for (; c < bring; c++) order.Add(crimsonCartPrefab);

        Debug.Log($"[Overseer] Ambush {ambush} ('{stage.label}'): {riders.Count} gnome(s), {bring} crimson cart(s) " +
                  $"({EnemyCrimsonCart.LiveCount} of {fight.StartCount} left).");

        // In through both ways, turn about, so the loop fills twice as fast
        bool useLeft = true;
        for (int i = 0; i < order.Count; i++)
        {
            if (Over(boss)) yield break;

            int entrance = useLeft ? leftEntranceLine : rightEntranceLine;
            int other = useLeft ? rightEntranceLine : leftEntranceLine;

            // Wait for a gap at either one, preferring the one whose turn it is
            while (true)
            {
                if (Over(boss)) yield break;
                if (CanEnter(rails, fight.Outer, entrance, order[i])) break;
                if (CanEnter(rails, fight.Outer, other, order[i])) { entrance = other; break; }
                yield return null;
            }

            MineCart cart = rails.SpawnCart(order[i], entrance, MineCart.TrackSpeed, false);
            if (cart != null)
            {
                var gnome = cart.GetComponent<EnemyGnomeCart>();
                if (gnome != null)
                {
                    gnome.WaitForClearLanding = true;
                    gnomes.Add(gnome);
                }
            }

            useLeft = entrance != leftEntranceLine;
            yield return null;
        }
    }

    // Every empty grey cart on the track comes apart as a new ambush begins
    // (owner, 2026-10-20) — the wrecks killed gnomes leave behind, which would
    // otherwise pile up on the outer loop. Shatter, not a kill: it pays nothing
    // and does not count towards breaking empty carts. Only exactly an empty
    // cart: crimson carts, kegs and riders all subclass it and are left alone.
    private static void ClearWrecks()
    {
        bool first = true;
        int cleared = 0;
        foreach (EnemyMineCart cart in Object.FindObjectsByType<EnemyMineCart>(FindObjectsSortMode.None))
        {
            if (cart == null || cart.IsDead || cart.GetType() != typeof(EnemyMineCart)) continue;
            cart.Shatter(first);
            first = false;
            cleared++;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (cleared > 0) Debug.Log($"[Overseer] {cleared} empty cart(s) shattered as the ambush begins.");
#endif
    }

    // He has been on the track and is dead. Before he rolls in he is null, and
    // the first ambush carries on regardless
    private static bool Over(EnemyDarkGnomeCart boss) => boss != null && boss.IsDead;

    // Any of these gnomes still on an entrance, not yet out on the loop?
    private bool StillComingIn(List<EnemyBase> gnomes)
    {
        for (int i = 0; i < gnomes.Count; i++)
        {
            EnemyBase gnome = gnomes[i];
            if (gnome == null || gnome.IsDead) continue;
            MineCart cart = gnome.GetComponent<MineCart>();
            if (cart == null || !cart.IsRiding) continue;
            int line = cart.Line.Index;
            if (line == leftEntranceLine || line == rightEntranceLine) return true;
        }
        return false;
    }

    private static bool AnyAlive(List<EnemyBase> gnomes)
    {
        for (int i = 0; i < gnomes.Count; i++)
        {
            if (gnomes[i] != null && !gnomes[i].IsDead) return true;
        }
        return false;
    }

    // Every cart on the outer loop or on its way onto it
    private int CartsOnOuter(Fight fight)
    {
        if (fight.Outer == null) return 0;
        int count = 0;
        IReadOnlyList<MineCart> all = MineCart.All;
        for (int i = 0; i < all.Count; i++)
        {
            MineCart cart = all[i];
            if (cart == null || !cart.IsRiding) continue;
            int line = cart.Line.Index;
            if (fight.Outer.Contains(line) || line == leftEntranceLine || line == rightEntranceLine) count++;
        }
        return count;
    }

    // Would a cart sent down this entrance now find room where it joins the loop?
    // Everything already on the loop, and everything on its way in by either
    // entrance, is carried forward to the moment the two would meet and measured
    // there. Carts stopped by a dart or a flinch get it wrong for a moment; the
    // spacing sweep (MineCart) settles whatever is left.
    private bool CanEnter(RailNetwork rails, RailLoopMap outer, int entrance, GameObject prefab)
    {
        if (outer == null) return false;

        float merge, travel;
        if (!Entrance(rails, outer, entrance, prefab, out merge, out travel)) return false;

        MineCart template = prefab != null ? prefab.GetComponent<MineCart>() : null;
        float spawnAlong = -(template != null ? template.LeadIn : 6f);
        float speed = MineCart.TrackSpeed;

        IReadOnlyList<MineCart> all = MineCart.All;
        for (int i = 0; i < all.Count; i++)
        {
            MineCart cart = all[i];
            if (cart == null || !cart.IsRiding) continue;
            int line = cart.Line.Index;

            float theirPlace, theirTime;
            if (line == leftEntranceLine || line == rightEntranceLine)
            {
                // Still on its way in: right behind where this one would start?
                if (line == entrance && Mathf.Abs(cart.Along - spawnAlong) < mergeGap) return false;

                RailTeleporter pad = rails.ExitPadFor(line);
                float theirMerge;
                if (pad == null || pad.Destination == null
                    || !outer.TryPosition(pad.Destination.LineIndex, pad.Destination.DistanceAlong, out theirMerge))
                {
                    continue;
                }
                theirTime = Mathf.Max(0f, (pad.DistanceAlong - cart.Along) / speed);
                theirPlace = theirMerge;
            }
            else if (!outer.TryPosition(line, cart.Along, out theirPlace))
            {
                continue;
            }
            else
            {
                theirTime = 0f;
            }

            // Both carried to whichever of the two is on the loop later
            float when = Mathf.Max(travel, theirTime);
            float ours = merge + speed * (when - travel);
            float theirs = theirPlace + speed * (when - theirTime);
            if (outer.Gap(ours, theirs) < mergeGap) return false;
        }

        return true;
    }

    private static int Total(List<int> groups)
    {
        int total = 0;
        if (groups == null) return 0;
        for (int i = 0; i < groups.Count; i++) total += Mathf.Max(0, groups[i]);
        return total;
    }
}
