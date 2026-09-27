using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A shaft opens in the floor BETWEEN the two knights and the mine comes up it in
// one unbroken column of iron.
//
// Every other wave down here arrives from the outside. Carts drive in from
// off-frame, rock comes in off the rectangle, the pack circles the edge — the
// knights stand in the middle of it all and face outward. This one puts the
// track up the centre line and inverts that: the thing you have to watch is
// between you and your partner, and watching it means turning your back on the
// wall.
//
// THE COLUMN WALLS THE TWO KNIGHTS OFF FROM EACH OTHER, and that is not a side
// effect — it is the reason the layout is one run at x=0 rather than two shafts
// either side of the middle. A cart eats arrows (EnemyMineCart), so a knight
// shooting across the board into the other half has iron in the way of the shot.
// Every wave in this game is co-operative by geometry; this is the one that
// takes the geometry away.
//
// And it takes it away COMPLETELY, because the column is packed rather than
// spaced. Carts go up nose to tail — one keg, one empty, one keg, one empty, for
// as long as the fight lasts — so there is never a gap to shoot through and
// never a moment when the two halves of the board can see each other. Each
// player is on their own for the whole wave. (The rail pieces themselves carry
// no collider; see RailSegment. What blocks a shot is the cart, which is why the
// column has to be solid to mean anything.)
//
// ALTERNATING IS WHAT KEEPS IT A STRING OF SEPARATE CHARGES. Packed at one cell
// apart, kegs on every other slot sit two units from each other — comfortably
// outside EnemyKegCart's 1.3 chain radius — so each one goes up alone for
// fifteen and the column never runs away as one long fuse the way the Powder
// Train does. The empty between each pair is the arrow-eater: twenty hit points
// of nothing whose only job is to be the cart your shot stops at.
//
// It also means HALF THE WALL IS LIVE, EVERYWHERE, ALL THE TIME. At two-unit
// spacing there are always about three kegs inside the band that reaches a
// knight (see below), so there is no stretch of column that is safe to shoot at
// and no waiting for a clean segment to come round. Every shot into the iron is
// taken next to powder.
//
// WHICH MAKES THE KEG THE MINE'S SHARPEST VERSION OF ITS OWN RULE. Powder
// anywhere else is a question about ONE knight — pop it over your partner's head
// and only they pay. At x=0 the pair is equidistant from both, so it is either
// free or it costs the two of you thirty apiece. The arithmetic is height and
// nothing else, and it is worth writing out because the whole wave sits on it.
//
// EnemyKegCart reaches 2.8 units, measured collider to collider rather than
// pivot to pivot. From the centre line a knight's box is about 1.6 units away
// across, and reaches 0.49 above and below his middle. So:
//
//   * A PAIR CATCHES BOTH KNIGHTS while it is within about 2.8 units of their
//     level: sqrt(2.8^2 - 1.6^2) is 2.30, plus the half-height of the box. That
//     is a 5.6-unit band of shaft, a little over two seconds at cart speed, and
//     it sits squarely in the middle of the view where the fight is.
//   * A PAIR CATCHES A RAT on its station while it is within about 2.5 units of
//     the rat: the rats stand 1.2 off the centre line, and sqrt(2.8^2 - 1.2^2)
//     is 2.53.
//
// SO THE POWDER IS PURE HAZARD HERE, and nothing else. The rats stand off wide
// — 3.2 either side of the shaft — which is further than a keg reaches from it
// at any height, so no charge on this track can ever be spent on them. There is
// no clever shot. The only thing the powder can do is go off in the band and
// take fifteen off BOTH knights at once, which makes every keg a thing to let
// past rather than a thing to use. The wave asks the players to hold fire on the
// wall they most want to make a hole in — and since kegs run every other slot,
// there is never a moment when holding fire is not what it is asking.
//
// (Two earlier cuts are worth recording, because both were wrong in instructive
// ways. Parking the rats low, at 2.2, put them inside the knights' own band so
// that clearing one with powder always cost something — a real trade, and far
// too close: a grey paces three units either side of its station and a brown
// five, so vermin stationed that low swept back and forth over the knights'
// heads. Pulling them up to the top and bottom of the view fixed that but left
// them close to the centre line, still drifting into the fight. Wide and high is
// where they belong; the price is that the powder stops being a tool.)
//
// What is left for the arrows is everything. A rat has to be shot through iron
// that keeps sliding in front of it, and no knight can help with the far side of
// the board while the shaft is running.
//
// They walk in from the nearest screen edge, settle onto their station, and pace
// there for fifteen seconds, then come down on the knight on their side of the
// shaft, as rats do everywhere else (owner's call, 2026-09-25: a rat pacing up top
// forever held the wave open until somebody hunted it down). The wave does not end
// and the wall does not come down until they are dead.
//
// They alternate corners of the middle — high left, low right, high right, low
// left — so both halves of the board stay loaded. Past four in a group the
// rotation comes round again and the repeats stand a little further out rather
// than on top of the rat already there.
//
// The pack comes out in AMBUSHES: a group is released, and the next one does not
// set off until every rat and every bat of the last is dead. The deeper tiers
// use that to arrive in two shifts with the heavier one second, so the fight
// gets worse rather than merely longer.
//
// A wave on an open route normally has no business doing that — nothing circles
// back, so a group the wave waits on leaves the board empty while the players
// take their time, and an empty board is a rest rather than a fight. What pays
// that debt here is the column. It never stops: it climbs from the first second
// to the last, straight through the handover between groups, so the gap between
// one shift and the next is still a gap spent behind a wall with powder in it.
// This is the one wave in the mine where the standing pressure is the scenery.
//
// A note for anyone adding rolling stock: the column takes PICKAXE riders if it
// takes riders at all, never bomb throwers, and that is a constraint rather than
// a preference. A bomb gnome releases when his cart crosses the knights'
// midpoint (see EnemyGnomeMineCart), and a cart riding exactly ON the midpoint
// never crosses it, so a bomb thrower up this shaft rides the whole way in
// silence.
//
// Nothing rolls dice (see the no-randomness pillar): the column pattern repeats
// slot for slot, the rats work a four-station rotation, and the bats work a
// four-corner one. Every rotation resets at the top of the run, because a wave
// asset's state outlives the run that set it.
[CreateAssetMenu(fileName = "UpTheMiddle", menuName = "Waves/Up the Middle")]
public class UpTheMiddle : BaseWave
{
    // One shift of the pack. Rats and bats are authored together because they
    // come out together and, more to the point, they CLEAR together: everything
    // that registers while a group is open belongs to it, so a bat still flying
    // holds the next shift back exactly as a live rat does.
    [System.Serializable]
    public class Ambush
    {
        [Tooltip("Authoring only — names the shift in the inspector and the dev log")]
        public string label;

        [Tooltip("Quiet before this shift's first arrival — the beat after the last one was cleared. On the opening shift it is measured from the track finishing its fall.")]
        public float leadIn = 3f;

        [Tooltip("Rats in this shift. They take the four stations in rotation; past four the repeats stand further out rather than on top of each other.")]
        public int rats = 4;

        [Tooltip("Seconds between one rat arriving and the next")]
        public float ratInterval = 6f;

        [Tooltip("Bats in this shift, working the outer corners. They belong to the group, so the next shift waits on them too — keep the count honest or the handover stalls on one bat nobody could reach.")]
        public int bats = 2;

        [Tooltip("Seconds between this shift's bats. The first is held back by this much as well, so the rats are always the thing that arrives first.")]
        public float batInterval = 7f;
    }

    [Header("Track")]
    [Tooltip("A single run straight up the centre line, open at both ends. Mine Winze is the layout this is tuned against — anything with the shaft off x=0 loses the wave's whole point, which is that the column belongs to both knights equally.")]
    [SerializeField] private RailLayout railLayout;

    [Tooltip("Index into the layout's runs: the shaft. Everything rides up this one.")]
    [SerializeField] private int shaftRun;

    [Header("The column")]
    [Tooltip("One slot per cart, cycled as the column climbs. One keg then one empty is the authored pattern: packed at one cell apart that leaves two units between charges, which is outside EnemyKegCart's 1.3 chain radius, so each keg goes up alone for fifteen instead of taking its neighbours with it. Put two kegs adjacent and they WILL chain into one thirty-damage charge — a different wave, and worth knowing before reordering this.")]
    [SerializeField] private List<GameObject> columnPattern = new List<GameObject>();

    [Tooltip("Cap on how many carts climb over the whole wave. 0 = keep the shaft full for as long as a rat is alive, which is what a solid column means. Set a number only to make the wave stop feeding itself.")]
    [SerializeField] private int columnCarts;

    [Tooltip("Seconds between carts. Leave at 0 to derive the PACKED spacing from the layout's cell size and the cart speed, which is the only way the column reliably closes up — hand-tuning it means re-tuning it every time either of those moves, and a column with a seam in it is a column the players can shoot through.")]
    [SerializeField] private float columnInterval;

    [Tooltip("Quiet between the shaft finishing its fall and the first cart rising")]
    [SerializeField] private float firstCartAt = 1f;

    [Header("The pack")]
    [Tooltip("In order — one entry per shift. Each waits for the last to be dead before it sets off, so a two-entry list with the heavier shift second is a fight that gets worse rather than one that merely runs longer.")]
    [SerializeField] private List<Ambush> ambushes = new List<Ambush>();

    [Tooltip("How far above and below the knights a rat's station is — the height of the line it paces along for fifteen seconds before it comes down. Mind that a rat does NOT sit still on its station: EnemyRat paces its prefab's moveDistance either side of it, which is 3 units for a grey and 5 for a brown, so the station is the centre of a six- or ten-unit sweep. 4.3 keeps that whole sweep near the top and bottom of the view and never closer than four units to a knight.")]
    [SerializeField] private float stationHeight = 4.3f;

    [Tooltip("How far to each side of the shaft the pacing line is centred. Anything past 2.8 puts the rats outside a keg's reach for good — at 3.2 the powder on the shaft cannot touch them at any height, which is deliberate: the rats are arrow work and the column is a hazard, and the two do not solve each other.")]
    [SerializeField] private float stationLane = 3.2f;

    [Tooltip("How much further out each repeat of a station stands. There are only four stations, so a shift of more than four puts two rats in the same corner; this is what stops them arriving inside one another. Mind the pacing sweep when raising it — a brown rat covers five units either side of wherever it is put, so lane plus spread plus five wants to stay inside the ten-unit half-width of the view.")]
    [SerializeField] private float stationSpread = 0.8f;

    [Header("Rock")]
    [Tooltip("Volleys down the shafts, cycled in order for the window below. The mine had only three waves that threw a rock at all, which left the guard — half of what a knight is — idle through the rest. This is the second question a wave asks while the first one is still standing.")]
    [SerializeField] private List<RockVolley> volleys = new List<RockVolley>();

    [Tooltip("Length of the firing window, from the start of the wave. New volleys stop being issued once it elapses; whatever is already in the air still falls.")]
    [SerializeField] private float projectileWindow = 24f;

    [Tooltip("World units per second for THIS wave's rock, overriding the prefab's 1. A shaft is seven units up, so 1.75 puts a rock on a knight in four seconds instead of seven. 0 leaves the prefab alone.")]
    [SerializeField] private float rockSpeed = 1.75f;

    [Header("Ogres")]
    [Tooltip("Brutes walking in from the edges, alternating sides. They ignore everything this wave is about and come straight for whichever knight they entered nearest — see OgreBand. Leave the count at 0 for a tier that should not have any.")]
    [SerializeField] private OgreBand ogres = new OgreBand();

    // Every ogre this wave put out. Cleared as the band is released — the asset
    // is a ScriptableObject and outlives the run.
    private readonly List<GameObject> _ogres = new List<GameObject>();

    [Header("Orbs")]
    [Tooltip("Crosses the board low by default, under the knights. It has to pass THROUGH the column, so each knight only gets a shot at it while it is on their own side — which is the wave's whole geometry stated in one collectible.")]
    [SerializeField] private OrbRun orbs = new OrbRun { from = new Vector2(-12f, -1.5f), to = new Vector2(12f, -1.5f) };

    // Where the rotations stand. Reset at the top of every run — SO state
    // outlives the run that set it.
    private int _cart;
    private int _rat;
    private int _bat;

    // Set once every shift has been cleared. The column watches it rather than
    // IsAmbushClear, and that distinction is the whole reason it exists: a group
    // closes between shifts, so a column asking "is the current group down" would
    // stop dead in the handover and take the wall away exactly when the players
    // were owed it most.
    private bool _fightOver;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        _cart = 0;
        _rat = 0;
        _bat = 0;
        _fightOver = false;

        var rails = spawner.Rails;
        if (rails == null && railLayout != null)
        {
            Debug.LogWarning("[UpTheMiddle] No RailNetwork in the scene — there is no shaft, so nothing comes up the middle and this is rats and bats alone.");
        }

        // Fire-and-forget: the cascade runs on the network's own clock. Only the
        // column and the opening shift wait it out, rather than the whole wave
        // blocking on it.
        float layDuration = rails != null ? rails.Lay(railLayout) : 0f;

        Coroutine orbRun = spawner.StartCoroutine(orbs.Release(spawner));

        // Started before the first shift and never stopped until the last one is
        // down. The wall going up IS the wave opening.
        Coroutine column = spawner.StartCoroutine(RaiseTheColumn(rails, layDuration));
        Coroutine brutes = spawner.StartCoroutine(ReleaseTheOgres(spawner));
        Coroutine shafts = spawner.StartCoroutine(WorkTheShafts(spawner));

        for (int i = 0; i < ambushes.Count; i++)
        {
            Ambush shift = ambushes[i];
            if (shift == null) continue;

            // Only the opening shift waits out the track
            yield return new WaitForSeconds((i == 0 ? layDuration : 0f) + Mathf.Max(0f, shift.leadIn));

            int ratCount = Mathf.Max(0, shift.rats);
            int batCount = Mathf.Max(0, shift.bats);

            // The count is DECLARED, and it has to be: both spawners are late.
            // Spawner.SpawnRat and SpawnBat each yield before instantiating, even
            // at zero delay, so a group whose first rat dies before its second
            // exists would read as clear and the next shift would run straight
            // over the top of it.
            BeginAmbush(ratCount + batCount);

            // Released together and BOTH waited on. A bat still queued behind its
            // interval is a member that does not exist yet, and a group declared
            // released before its last member is on the field is one the handover
            // can trip over.
            Coroutine vermin = spawner.StartCoroutine(PlaceTheRats(spawner, shift, ratCount));
            Coroutine air = spawner.StartCoroutine(WorkTheCorners(spawner, shift, batCount));

            yield return vermin;
            yield return air;

            // Out here rather than at the end of either coroutine, so it still
            // runs when one of them bails early. As load-bearing as
            // MarkSpawningComplete: a shift that is never marked released can
            // never read as clear, and the wait below would hang on it forever.
            MarkAmbushReleased();

            // Last shift out: if a quick clear beat the orb timer, the orb goes
            // now, with this shift still on the board. See OrbRun.SendFirstIfWaiting.
            if (i == ambushes.Count - 1) orbs.SendFirstIfWaiting(spawner);

            yield return WaitForAmbushClear();
        }

        // Only now, with every shift down. The column reads this rather than the
        // ambush, so the wall stays up across the handovers and comes down once.
        _fightOver = true;
        yield return column;
        yield return brutes;
        yield return shafts;

        // FIRST, before any tidying up. Nothing below gates the wave, and an
        // exception thrown between here and the end would kill the coroutine
        // where it stands, leaving a run hanging on a wave that can never end.
        MarkSpawningComplete();

        // Guarded because StartCoroutine hands back null for an enumerator that
        // finishes without ever yielding — see OrbRun.Release.
        if (orbRun != null) spawner.StopCoroutine(orbRun);
    }

    // The column, packed nose to tail, for as long as there is anything up here
    // worth hiding behind it.
    private IEnumerator RaiseTheColumn(RailNetwork rails, float trackDelay)
    {
        yield return null;

        if (rails == null || columnPattern == null || columnPattern.Count == 0) yield break;

        yield return new WaitForSeconds(Mathf.Max(0f, trackDelay) + Mathf.Max(0f, firstCartAt));

        float interval = ResolveInterval();
        int cap = Mathf.Max(0, columnCarts);
        int sent = 0;

        // An uncapped column runs until every shift is down — NOT until the
        // current group clears. That is the debt this wave owes the player for
        // making them fight through a wall, and it is owed hardest in the
        // handover between shifts: a column that stopped there would hand out a
        // few free seconds of open board at exactly the moment the wave is
        // supposed to be reloading.
        while (cap == 0 ? !_fightOver : sent < cap)
        {
            // Re-checked every pass: the track can be torn down under us between
            // releases, and the run index would go out of range with it
            if (rails.LineCount == 0)
            {
                Debug.LogWarning("[UpTheMiddle] The shaft went out from under the wave — the rest of the column stays underground.");
                yield break;
            }

            int run = Mathf.Clamp(shaftRun, 0, rails.LineCount - 1);

            GameObject prefab = columnPattern[_cart++ % columnPattern.Count];
            if (prefab != null) rails.SpawnCart(prefab, run, MineCart.TrackSpeed);
            sent++;

            yield return new WaitForSeconds(interval);
        }
    }

    // One cell of track per slot is what makes the carts touch, and it is derived
    // rather than authored for the same reason the Millstone's rings are: the
    // spacing is a function of the track and the speed, and a hardcoded figure
    // stops closing the column the moment either changes.
    private float ResolveInterval()
    {
        if (columnInterval > 0f) return columnInterval;

        float cell = railLayout != null ? railLayout.CellSize : 1f;
        return Mathf.Max(0.05f, cell / MineCart.TrackSpeed);
    }

    private IEnumerator PlaceTheRats(Spawner spawner, Ambush shift, int count)
    {
        yield return null;
        if (count == 0) yield break;

        for (int i = 0; i < count; i++)
        {
            Station(spawner);
            if (i < count - 1) yield return new WaitForSeconds(Mathf.Max(0.1f, shift.ratInterval));
        }
    }

    // Four stations round the middle, taken in rotation: high left, low right,
    // high right, low left. Alternating the SIDE every rat rather than every pair
    // is what keeps the two halves of the board loaded evenly; alternating the
    // HEIGHT with it means the same corner never fills twice running.
    //
    // Past the fourth rat the rotation comes round again, and each lap of it
    // stands a little further out. Without that a shift of eight would put two
    // rats on the same point — they would arrive inside one another and spend the
    // fight shoving each other apart through EnemyRat's ground-collision bounce.
    // The rotation counter runs across the WHOLE wave, not per shift, so the
    // second shift fills the outer ring rather than crowding the corners the
    // first one has already vacated.
    //
    // The rat is handed the station as its destination, not as a spawn point.
    // EnemyRat walks itself in from whichever screen edge is nearest — the top
    // one for a high station, the bottom for a low one — and then paces there.
    // That walk-in is the wave's telegraph and it takes a second or two, which is
    // why nothing is authored to arrive off-frame by hand.
    //
    // It is assigned the knight on its own side of the shaft, so it patrols for
    // fifteen seconds and then charges him without crossing the column.
    //
    // Eight stations rather than four, because the shifts are now big enough
    // that four would stack three rats on one corner. The extra four sit inboard
    // of the corners at the same height, so the pack reads as a line across the
    // top and bottom of the view instead of four clumps.
    //
    // Repeats past eight step outward and inward at once: a little further from
    // the shaft, a little further down the screen. Both are held inside hard
    // caps — a brown rat paces five units either side of wherever it is put, so
    // a station past MaxLane walks it off the edge, and a station below
    // MinHeight puts it in reach of a knight it was never meant to reach.
    private const float MaxLane = 5.6f;
    private const float MinHeight = 2.6f;

    private static readonly float[] SlotLane = { -1f, 1f, -1f, 1f, -0.45f, 0.45f, -0.45f, 0.45f };
    private static readonly int[] SlotHigh = { 1, 1, -1, -1, 1, 1, -1, -1 };

    private void Station(Spawner spawner)
    {
        int slot = _rat % SlotLane.Length;
        int ring = _rat / SlotLane.Length;

        float lane = Mathf.Abs(stationLane) * (1f + ring * 0.18f) + ring * Mathf.Max(0f, stationSpread) * 0.25f;
        lane = Mathf.Min(lane, MaxLane);

        float height = Mathf.Max(MinHeight, Mathf.Abs(stationHeight) - ring * 0.5f);

        var at = new Vector2(SlotLane[slot] * lane, SlotHigh[slot] * height);
        Transform knight = at.x < 0f ? spawner.LeftPlayer : spawner.RightPlayer;

        // _rat keeps counting: it is the SLOT and RING index above, not a rat
        // type index. Which rat walks on comes off the map's cadence now.
        _rat++;

        spawner.SpawnRat(at, 0f, knight);
    }

    // The air, held back by one interval on purpose: the rats are what the shift
    // wants read first, and a bat arriving with them just splits the read.
    private IEnumerator WorkTheCorners(Spawner spawner, Ambush shift, int count)
    {
        yield return null;
        if (count == 0) yield break;

        float interval = Mathf.Max(0.1f, shift.batInterval);
        yield return new WaitForSeconds(interval);

        for (int i = 0; i < count; i++)
        {
            spawner.SpawnBat(NextCorner(spawner), 0f);
            if (i < count - 1) yield return new WaitForSeconds(interval);
        }
    }

    // The four outer corners in rotation — never the middle, which belongs to the
    // column. Top pair first: the shaft is most demanding while a charge is
    // overhead, so the air opens where the guard is least likely to be.
    private Vector2 NextCorner(Spawner spawner)
    {
        switch (_bat++ % 4)
        {
            case 0: return spawner.topLeftCorner;
            case 1: return spawner.topRightCorner;
            case 2: return spawner.bottomRightCorner;
            default: return spawner.bottomLeftCorner;
        }
    }

    // Beside the wave, never inside it. Ogres do not belong to any shift (see
    // EnemyOgre.JoinsAmbushes) — they are a clock running underneath whatever
    // else the wave is doing, and the wave is not finished until they are down.
    private IEnumerator ReleaseTheOgres(Spawner spawner)
    {
        _ogres.Clear();
        yield return ogres.Release(spawner, _ogres);
    }

    // Beside the wave, never inside it: the window is a fixed number of seconds
    // from the start, so it neither stretches nor truncates with how fast the
    // players clear whatever else is on the board.
    private IEnumerator WorkTheShafts(Spawner spawner)
    {
        yield return RockVolley.WorkTheShafts(spawner, volleys, projectileWindow, rockSpeed);
    }
}
