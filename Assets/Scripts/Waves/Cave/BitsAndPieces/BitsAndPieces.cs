using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Four shafts down the two edges of the cave, in touching pairs: powder on the
// outer rail, gnomes on the inner one, both descending past the knights and
// wrapping round for another pass.
//
// Carts go out ONE AT A TIME, alternating lines — a keg, a pause, a rider, a
// pause, and on down the shift. Nothing is ever released onto both rails on the
// same beat, so the track fills as a single readable queue rather than as two
// columns arriving together. When one line runs out the other simply carries on
// at the same pause, which is what makes an uneven shift look deliberate instead
// of ragged.
//
// That alternation is also the wave's whole tactical content, because of a
// property of parallel loops that is worth stating outright:
//
//   ON TWO LOOPS OF EQUAL LENGTH AT EQUAL SPEED, RELATIVE POSITION IS FROZEN.
//
// A keg released one pause ahead of a rider stays one pause ahead of him for the
// rest of the wave. There is no drifting into alignment and no moment to wait
// for: whichever rider a keg is beside when it goes out, it is beside him
// forever. So the timing decision every other keg in the mine offers — hold the
// shot until the traffic comes round — does not exist here, and the pause is what
// stands in for it.
//
// The pause therefore decides, at authoring time, whether the powder can touch
// the riders at all. The rails are one unit apart and a keg reaches about 3.5
// units along the neighbouring shaft (2.8 blast radius tested against the cart's
// box, not its centre), while one pause of separation is pause x 2.4 units. So
// under about 1.45s the powder covers the rider beside it and over that it can
// never reach him — there is no in-between, because alternation spaces every keg
// from its neighbour identically. Every shift authored today sits at a 1s pause,
// comfortably inside that, which is where the wave wants to be: the powder is
// supposed to be worth spending.
//
// What keeps that from being all-or-nothing is the COUNT MISMATCH. The lines
// carry different numbers on purpose, so the shorter one runs dry partway through
// and the tail of the longer one comes down with no powder beside it at all. That
// is the shape of the fight: the front of a shift can be cleared with the kegs and
// the back of it has to be shot the hard way.
//
// Standing pressure while a shift is alive is the shift itself — the riders throw
// on their own cooldown for as long as they are up, so dawdling costs, which is
// the debt every ambush wave owes the player.
//
// Nothing rolls dice (see the no-randomness pillar): the cart kinds come off a
// rotation that carries across the wave and resets at the top of every run, and
// the cadence is fixed. Note that a pause is a PERMANENT spacing on a loop —
// consecutive carts on one line end up 4.8 x pause units apart on a 36-unit lap,
// so a line of more than 36 / (4.8 x pause) carts laps itself and starts to bunch
// at the mouth.
[CreateAssetMenu(fileName = "BitsAndPieces", menuName = "Waves/Bits and Pieces")]
public class BitsAndPieces : BaseWave
{
    // One shift's shape. Both lines are authored together because the whole point
    // of the wave is how they sit against each other.
    [System.Serializable]
    public class Ambush
    {
        [Tooltip("Authoring only — names the shift in the inspector and the dev log")]
        public string label;

        [Tooltip("Riders sent up the INNER shaft in this shift. These are the kills that hold the shift open, and the only thing on the track that fights back.")]
        public int gnomesPerAmbush = 3;

        [Tooltip("Powder carts sent up the OUTER shaft in this shift. They gate nothing — a keg is a tool, not a kill. Keep this DIFFERENT from the rider count: equal counts cover every rider evenly and there is no choice left to make.")]
        public int kegsPerAmbush = 2;

        [Tooltip("Quiet before this shift's first cart — the beat after the last one was cleared. On the opening shift it is measured from the track finishing its fall.")]
        public float leadIn = 1f;

        [Tooltip("Seconds between one cart and the next, alternating powder then riders. Over about 1.45s a keg can no longer reach the rider beside it (one unit across, 3.5 units of reach along the shaft, 2.4 units travelled per second) and the powder becomes decoration.")]
        public float releasePause = 1f;
    }

    // Which run of Mine Twin Shafts each rail enters on. Not authored: the layout
    // is fixed for this wave and a run index is not a design decision anyone can
    // read, it is an index into a list. Retune the wave by the counts above.
    private const int KegEntryRun = 0;   // the outer pair of shafts, x = +/-9
    private const int GnomeEntryRun = 2; // the inner pair, x = +/-8

    [Header("Track")]
    [Tooltip("Two touching shafts down each edge. Laid the moment the wave starts; cleared when the next wave begins.")]
    [SerializeField] private RailLayout railLayout;

    [Header("Rolling stock")]
    [Tooltip("Cycled in order as a shift's rider slots are filled. Pickaxe gnomes only — the bomb gnome cannot work here at all, because he releases on a crossing of the knights' midpoint and a vertical shaft never makes one.")]
    [SerializeField] private List<GameObject> gnomeCarts = new List<GameObject>();

    [Tooltip("Fills every keg slot on the red rail. Ten hit points, so one arrow.")]
    [SerializeField] private GameObject kegCart;

    [Header("Shifts")]
    [Tooltip("In order. Each one waits for the last one's riders to be dead before it sets off, so the wave's length is set by the knights' shooting and not by a stopwatch.")]
    [SerializeField] private List<Ambush> ambushes = new List<Ambush>();

    [Header("Orbs")]
    [Tooltip("The high crossing, over the top of the fight. Runs clear of both rails at y=4.3.")]
    [SerializeField] private OrbRun highOrbs = new OrbRun
    {
        count = 1, firstAt = 10f, interval = 14f,
        from = new Vector2(-12f, 4.3f), to = new Vector2(12f, 4.3f)
    };

    [Tooltip("The low crossing, under it. Runs the other way at y=-5 so the two lines are never the same shot twice.")]
    [SerializeField] private OrbRun lowOrbs = new OrbRun
    {
        count = 1, firstAt = 20f, interval = 14f,
        from = new Vector2(12f, -5f), to = new Vector2(-12f, -5f)
    };

    // Where the gnome rotation stands. Reset at the top of every run — SO state
    // outlives the run that set it, and a rotation that carried over would deal a
    // different shift each time the wave came up.
    private int _gnome;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        _gnome = 0;

        var rails = spawner.Rails;
        if (rails == null)
        {
            if (railLayout != null)
            {
                Debug.LogWarning("[BitsAndPieces] No RailNetwork in the scene — there are no shafts to run.");
            }
            MarkSpawningComplete();
            yield break;
        }

        // Fire-and-forget: the cascade runs on the network's own clock, and only
        // the opening shift waits it out rather than the whole wave blocking on it
        float layDuration = rails.Lay(railLayout);

        // Beside the wave, never inside it: the shifts are player-paced, so an orb
        // run joined to the spawn phase would stretch or truncate with them
        Coroutine highRun = spawner.StartCoroutine(highOrbs.Release(spawner));
        Coroutine lowRun = spawner.StartCoroutine(lowOrbs.Release(spawner));

        for (int i = 0; i < ambushes.Count; i++)
        {
            Ambush ambush = ambushes[i];
            if (ambush == null) continue;

            // Built BEFORE the group is opened, because the count it comes out at
            // is the count the group has to be told to expect. A rider slot with
            // no prefab in it is dropped here, and declaring the authored number
            // instead would leave the group waiting on a gnome that never existed.
            List<GameObject> riders = BuildRiders(ambush);
            List<GameObject> powder = BuildPowder(ambush);

            // Releases are spread over seconds, so the first rider can very
            // plausibly be dead before the last one is out. MarkAmbushReleased
            // already guards that, and this is the second lock on the same door.
            BeginAmbush(riders.Count);

            yield return spawner.StartCoroutine(Release(rails, powder, riders, ambush, layDuration));

            layDuration = 0f; // only the opening shift waits out the track

            // Out here rather than at the end of the release, so it still runs
            // when that coroutine bails early. A shift that is never marked
            // released can never read as clear, and the wave would hang on it.
            MarkAmbushReleased();

            yield return WaitForAmbushClear();
        }

        // Orbs already on the board finish their crossing on their own; what stops
        // here is any that had not been released yet
        spawner.StopCoroutine(highRun);
        spawner.StopCoroutine(lowRun);

        MarkSpawningComplete();
        yield return null;
    }

    // The riders, in running order. A slot whose prefab is missing is dropped
    // rather than left as a hole: the count is the contract, and a silent gap in
    // the cadence reads as a release that misfired.
    private List<GameObject> BuildRiders(Ambush ambush)
    {
        var line = new List<GameObject>();
        for (int i = 0; i < Mathf.Max(0, ambush.gnomesPerAmbush); i++)
        {
            GameObject prefab = NextOf(gnomeCarts, ref _gnome);
            if (prefab != null) line.Add(prefab);
        }
        return line;
    }

    private List<GameObject> BuildPowder(Ambush ambush)
    {
        var line = new List<GameObject>();
        if (kegCart == null) return line;

        for (int i = 0; i < Mathf.Max(0, ambush.kegsPerAmbush); i++) line.Add(kegCart);
        return line;
    }

    // One cart at a time, red first, then green, then red again. Never both at
    // once: the two shafts are a single queue as far as the player is concerned,
    // and releasing onto both on the same beat gives them two things to read where
    // the wave only means to offer one.
    //
    // Carts are NOT dealt across the layout's other runs. The two runs of a loop
    // are the same stretch of track half a lap apart, so spreading them would have
    // carts appearing on both edges of the board at once instead of driving in
    // from above.
    private IEnumerator Release(RailNetwork rails, List<GameObject> red, List<GameObject> green,
                                Ambush ambush, float trackDelay)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, trackDelay) + Mathf.Max(0f, ambush.leadIn));

        float pause = Mathf.Max(0.1f, ambush.releasePause);
        int r = 0;
        int g = 0;
        bool redsTurn = true; // the powder leads, so the riders arrive into a rail that is already busy

        while (r < red.Count || g < green.Count)
        {
            // Re-checked every pass: the track can be torn down under us between
            // releases, and the entry run would go out of range with it
            if (rails.LineCount == 0)
            {
                Debug.LogWarning("[BitsAndPieces] The shafts went out from under the wave — the rest of the shift stays underground.");
                yield break;
            }

            // Whoever's turn it is, unless they have run dry — then the other line
            // simply carries on at the same pause rather than leaving a hole in it
            bool useRed = redsTurn ? r < red.Count : g >= green.Count;

            int run = Mathf.Clamp(useRed ? KegEntryRun : GnomeEntryRun, 0, rails.LineCount - 1);
            rails.SpawnCart(useRed ? red[r++] : green[g++], run, MineCart.TrackSpeed);

            redsTurn = !redsTurn;

            if (r < red.Count || g < green.Count) yield return new WaitForSeconds(pause);
        }
    }

    // Round the authored list, carrying the cursor across the whole wave so that
    // two shifts of the same size are not the same shift twice.
    private static GameObject NextOf(List<GameObject> list, ref int cursor)
    {
        if (list == null || list.Count == 0) return null;
        return list[cursor++ % list.Count];
    }
}
