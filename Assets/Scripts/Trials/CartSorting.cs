using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cart sorting (owner, 2026-10-09): in the Mine, in the gap after a wave, a piece
/// of track drops in with flags at its ends, and coloured carts roll onto it. Every
/// cart has to end up at the flag of its own colour.
///
/// Carts and flags come in four colours: red, blue, green and yellow. There are
/// only ever two levers, a red one and a blue one, standing by the knights. The
/// track is ordinary rail except for a few corner pieces drawn in red or blue, and
/// hitting a lever with a sword or an arrow turns every corner piece of its colour
/// to its other branch. So two levers have to sort up to four colours, and each of
/// the five phases asks that in a different way, on a track of its own — see
/// <see cref="Phases"/>.
///
/// The first cart to reach the wrong flag loses it, there and then. Get every cart
/// home and the fanfare plays, and a health orb and a mana orb fly past the
/// knights, the same payout as the target range.
///
/// A LOW-priority event, like the target range: it only gets a gap that no quest
/// scene and no Order trial wanted — see BetweenWaveEvents. The range is the
/// forest's and this is the mine's, so the two never want the same gap.
///
/// After any wave from the fifth on it has one chance in four, and one in two once
/// four waves have gone by without it — see LowEventRoll (owner, 2026-10-13; it was
/// one in three from the third wave). The roll decides only WHETHER it plays; which
/// phase, and what is in it, is never rolled.
///
/// The five phases go in batches the way the range's patterns do — see Next.
/// Clearing a phase for the first time counts toward the Cartographer's "Sorting
/// Carts" quest, which is offered after the first clear.
/// </summary>
public static class CartSorting
{
    /// <summary>The map it lives on. Only the mine has rails.</summary>
    public const string MapId = QuestBuild.Mine;

    /// <summary>The first wave it can follow. From there, any wave.</summary>
    public const int FirstWave = 5;

    /// <summary>What <see cref="TestRunConfig.ForcedTrial"/> is set to for a forced phase.</summary>
    public const string ForcedKey = "carts";

    /// <summary>How many phases the quest asks for: all of them.</summary>
    public const int TotalPhases = 5;

    // ---- progress ----

    /// <summary>
    /// How many DIFFERENT phases have been cleared. Sorting Carts reads it,
    /// measured over the whole file — the first clear is what opens the quest, so
    /// it has to count.
    /// </summary>
    public const string PhasesStat = "carts.phases";

    /// <summary>Every clear, repeats included. For the Stats panel.</summary>
    public const string SortedStat = "carts.sorted";

    /// <summary>
    /// Bookkeeping, not an achievement: which phase was put up last (cleared or
    /// not), and a bit per phase cleared in the current batch. Written with
    /// PlayerStats.Set and left out of StatsDatabase, like the range's.
    /// </summary>
    private const string LastPlayedKey = "carts.last_played";
    private const string BatchKey = "carts.batch";

    /// <summary>1 once phase <paramref name="number"/> has ever been cleared. Bookkeeping, as above.</summary>
    private static string PhaseClearedKey(int number) => "carts.phase." + number;

    // ---- the phases ----

    /// <summary>One cart: its colour, when it is let go, and which run of the layout it rolls in on.</summary>
    public struct Cart
    {
        public CartColor Color;
        /// <summary>Seconds after the first cart of the phase.</summary>
        public float At;
        /// <summary>Index into the layout's runs.</summary>
        public int Line;
    }

    public sealed class Phase
    {
        /// <summary>1-based: the order the phases are played in.</summary>
        public readonly int Number;
        /// <summary>
        /// Which track it is played on: CartSortingKit.LayoutFor(Track), the asset
        /// "Mine Sorting N". The tracks are numbered in the order they were built,
        /// which stopped being the order they are played in when the owner moved
        /// the twin wheels up (2026-10-09).
        /// </summary>
        public readonly int Track;
        /// <summary>For the log and Test Mode. Working names, not shown to the player.</summary>
        public readonly string Name;
        /// <summary>World units per second, for every cart in the phase.</summary>
        public readonly float Speed;
        public readonly Cart[] Carts;

        /// <summary>
        /// How far each lever stands from the point midway between the knights: the
        /// red one that far to the left, the blue one that far to the right. Half a
        /// unit puts them between the knights. More than two puts them outside,
        /// for a track that runs up the middle.
        /// </summary>
        public readonly float LeverSpot;

        /// <summary>
        /// Which way each lever leans before it is hit. A lever should lean toward
        /// wherever its corner pieces are sending carts, and that depends on the track.
        /// </summary>
        public readonly bool RedRestsLeft;
        public readonly bool BlueRestsLeft;

        /// <summary>
        /// Seconds to get every cart home, counted from the first cart. 0 for no
        /// clock. Only a track where a cart can go round for ever needs one. Real
        /// seconds: unlike the cart times, this is not stretched by Pace.
        /// </summary>
        public readonly float Seconds;

        public Phase(int number, string name, int track, float speed, Cart[] carts, float leverSpot = 0.5f,
                     bool redRestsLeft = true, bool blueRestsLeft = false, float seconds = 0f)
        {
            Number = number;
            Name = name;
            Track = track;
            Speed = speed * Pace;
            Carts = carts;
            LeverSpot = leverSpot;
            RedRestsLeft = redRestsLeft;
            BlueRestsLeft = blueRestsLeft;
            Seconds = seconds;
        }
    }

    /// <summary>
    /// How fast the whole challenge runs (owner, 2026-10-09: every cart a third
    /// slower). The phases below are written at full pace. A cart's speed is
    /// multiplied by this, and the time each cart is let go is divided by it,
    /// so the carts keep exactly the spacing on
    /// the track they were written with. Slowing the carts alone would run the
    /// coupled carts of phase 3 into each other.
    /// </summary>
    private const float Pace = 0.67f;

    /// <summary>Between two carts coupled nose to tail: about a cart and a third at track speed.</summary>
    private const float CoupledSeconds = 0.55f;

    /// <summary>The mine's ordinary cart speed. Phase 1 runs a little under it.</summary>
    private const float TrackSpeed = MineCart.TrackSpeed;

    /// <summary>
    /// The five, in the order they are played (owner, 2026-10-09). Each has a
    /// track of its own, built as "Mine Sorting N" by CartSortingAssetBuilder —
    /// the number after the name is that N. The first number in a Stream is the
    /// run of the layout the carts roll in on. Full write-up:
    /// Docs/Design/cart-sorting.md.
    ///
    ///   1  TWO FORKS. A fork each side of the knights; in both, the left branch
    ///      ends at a red flag and the right at a blue. Red and blue carts only,
    ///      eight of them, taking turns between the forks and a little slower than
    ///      the others. Five lever hits.
    ///
    ///   2  TWIN WHEELS. A one-way ring each side that carts roll onto and go
    ///      round. Each wheel has two doors, and its lever opens both at once: red
    ///      and yellow on the left wheel, blue and green on the right. A cart at a
    ///      closed door goes round again; a cart at an open door leaves, right or
    ///      wrong. Both wheels have to be empty inside forty seconds. (The owner
    ///      tried it last and found it easier than expected, so it is second.)
    ///
    ///   3  PASS IT ON. One track and two forks in a row. The red fork keeps a red
    ///      cart for the red flag or passes a cart on, over the knights' heads, to
    ///      the blue fork, which sorts blue from green. Seven carts.
    ///
    ///   4  OVER AND UNDER. Two long lanes cross the whole screen in opposite
    ///      directions, one above the knights and one below, each ending in a small
    ///      fork on the far side. Blue and green ride the top lane, red and yellow
    ///      the bottom. Carts come in threes, nose to tail, and the colour changes
    ///      part way through a train — the lever has to be hit in the gap between
    ///      two carts. No train asks one lever for two hits, because a sword takes
    ///      a second to come back and a bow a second and a half.
    ///
    ///   5  THE FOUNTAIN. One track up the middle, between the knights. A red
    ///      fork at the top picks a side; each side has a blue fork, and the one
    ///      blue lever turns both. Four flags along the bottom — red, yellow,
    ///      green, blue — so every cart needs both levers right and each colour is
    ///      one combination. The levers stand outside the knights.
    /// </summary>
    public static readonly Phase[] Phases =
    {
        new Phase(1, "Two forks", 1, 2f, Join(
            Stream(0, 0.0f, 3f, "RBBR"),
            Stream(1, 1.5f, 3f, "BRBR"))),

        new Phase(2, "Twin wheels", 5, TrackSpeed, Join(
            Stream(0, 0.00f, 2.5f, "RYR"),
            Stream(1, 1.25f, 2.5f, "BGB")),
            redRestsLeft: false, blueRestsLeft: true, seconds: 40f),

        new Phase(3, "Pass it on", 2, TrackSpeed, Stream(0, 0f, 2.4f, "RBGBRGB"),
                  blueRestsLeft: true),

        new Phase(4, "Over and under", 3, TrackSpeed, Join(
            Stream(0, 0f, CoupledSeconds, "BBG"), Stream(1, 2f, CoupledSeconds, "RRY"),
            Stream(0, 4f, CoupledSeconds, "GGB"), Stream(1, 6f, CoupledSeconds, "YYR"),
            Stream(0, 8f, CoupledSeconds, "BGG"), Stream(1, 10f, CoupledSeconds, "RYY"))),

        new Phase(5, "The fountain", 4, TrackSpeed, Stream(0, 0f, 2.2f, "RYBGRBYG"),
                  leverSpot: 3.5f, blueRestsLeft: true),
    };

    public static Phase Find(int number)
    {
        for (int i = 0; i < Phases.Length; i++)
        {
            if (Phases[i].Number == number) return Phases[i];
        }
        return null;
    }

    /// <summary>
    /// Carts in on one run at an even spacing, written as letters in the order
    /// they arrive: R, B, G, Y.
    /// </summary>
    private static Cart[] Stream(int line, float start, float gapSeconds, string colors)
    {
        var carts = new Cart[colors.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            carts[i] = new Cart { Color = ColorOf(colors[i]), At = (start + i * gapSeconds) / Pace, Line = line };
        }
        return carts;
    }

    private static CartColor ColorOf(char letter)
    {
        switch (letter)
        {
            case 'B': return CartColor.Blue;
            case 'G': return CartColor.Green;
            case 'Y': return CartColor.Yellow;
        }
        return CartColor.Red;
    }

    private static Cart[] Join(params Cart[][] groups)
    {
        var all = new List<Cart>();
        for (int i = 0; i < groups.Length; i++) all.AddRange(groups[i]);
        return all.ToArray();
    }

    // ---- when it happens ----

    public static bool IsDueAfter(int wave)
    {
        return wave >= FirstWave;
    }

    /// <summary>
    /// Decides whether the gap after <paramref name="survivedWave"/> gets the carts.
    /// The caller has already given every higher-priority event its chance and none
    /// of them wanted the gap — see BetweenWaveEvents.
    /// </summary>
    public static bool TryPick(int survivedWave, string mapId, out Phase phase)
    {
        phase = null;

        if (mapId != MapId) return false;
        if (!IsDueAfter(survivedWave)) return false;

        Phase next = Next();
        if (!CanPlay(next))
        {
            Debug.LogWarning("[Carts] CartSortingKit is missing from Resources or has nothing for phase "
                             + next.Number + " — no cart sorting.");
            return false;
        }

        if (!LowEventRoll.Roll(survivedWave, FirstWave, out int percent))
        {
            Debug.Log($"[Carts] after wave {survivedWave}: missed its roll ({percent}%)");
            return false;
        }

        phase = next;
        RecordPlayed(phase);
        LowEventRoll.NotePlayed(survivedWave);
        Debug.Log($"[Carts] after wave {survivedWave}: phase {phase.Number} plays ({percent}% roll)");
        return true;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// Test Mode's forced phase: <see cref="TestRunConfig.ForcedTrial"/> set to
    /// <see cref="ForcedKey"/>, with the phase number in ForcedTrialPhase. Only
    /// inside a Test Mode run — the setting is static and outlives the run.
    /// </summary>
    public static bool TryForced(out Phase phase)
    {
        phase = null;
        if (!TestRunConfig.ActiveRun || TestRunConfig.ForcedTrial != ForcedKey) return false;
        phase = Find(Mathf.Clamp(TestRunConfig.ForcedTrialPhase, 1, Phases.Length));
        return phase != null && CanPlay(phase);
    }

    /// <summary>
    /// For Test Mode's event list: the phase the carts would play in the gap after
    /// <paramref name="survivedWave"/>, or null when they are not due there. The
    /// roll is left out on purpose — the tester is the one choosing.
    /// Nothing is written; <see cref="PickByHand"/> does that.
    /// </summary>
    public static Phase PeekDue(int survivedWave, string mapId)
    {
        if (mapId != MapId || !IsDueAfter(survivedWave)) return null;

        Phase next = Next();
        return CanPlay(next) ? next : null;
    }

    /// <summary>
    /// The tester picked the carts for the gap. Recorded as put up, the way
    /// TryPick records it, without the roll.
    /// </summary>
    public static void PickByHand(Phase phase, int survivedWave)
    {
        RecordPlayed(phase);
        LowEventRoll.NotePlayed(survivedWave);
        Debug.Log($"[Carts] after wave {survivedWave}: phase {phase.Number} plays (picked in Test Mode)");
    }
#endif

    /// <summary>The kit has this phase's track, both levers, and a cart in every colour the phase uses.</summary>
    private static bool CanPlay(Phase phase)
    {
        var kit = CartSortingKit.Instance;
        if (kit == null || phase == null) return false;
        if (kit.LeverFor(RailSwitchColor.Red) == null || kit.LeverFor(RailSwitchColor.Blue) == null) return false;
        if (kit.LayoutFor(phase.Track) == null) return false;

        for (int i = 0; i < phase.Carts.Length; i++)
        {
            if (kit.CartFor(phase.Carts[i].Color) == null) return false;
        }
        return true;
    }

    /// <summary>
    /// Works through the phases in batches, the way the target range does. From the
    /// one just played, the next phase NOT yet cleared in this batch, wrapping
    /// back to 1. Pass or fail, play always moves on, and a failed phase comes
    /// round again later in the batch. Once every phase is cleared the batch
    /// resets and starts from 1.
    /// </summary>
    public static Phase Next()
    {
        int mask = PlayerStats.Get(BatchKey);
        int last = Mathf.Clamp(PlayerStats.Get(LastPlayedKey), 0, Phases.Length);

        for (int step = 1; step <= Phases.Length; step++)
        {
            int index = (last - 1 + step + Phases.Length) % Phases.Length;
            if ((mask & (1 << index)) == 0) return Phases[index];
        }
        return Phases[0];
    }

    /// <summary>A phase was put up, whether or not it gets cleared.</summary>
    private static void RecordPlayed(Phase phase)
    {
        PlayerStats.Set(LastPlayedKey, phase.Number);
    }

    /// <summary>A phase was cleared. Written between waves, so it lands at once.</summary>
    public static void RecordClear(Phase phase)
    {
        if (phase == null) return;

        string key = PhaseClearedKey(phase.Number);
        if (PlayerStats.Get(key) == 0)
        {
            PlayerStats.Set(key, 1);
            // Last, so the quest that reads it sees the phase already marked.
            PlayerStats.Increment(PhasesStat);
        }
        PlayerStats.Increment(SortedStat);

        // The batch: this one is done until all of them are, then it starts over at 1
        int full = (1 << Phases.Length) - 1;
        int mask = PlayerStats.Get(BatchKey) | (1 << (phase.Number - 1));
        if (mask == full)
        {
            mask = 0;
            PlayerStats.Set(LastPlayedKey, 0);
        }
        PlayerStats.Set(BatchKey, mask);
        PlayerStats.Flush();
    }

    // ---- playing it ----

    /// <summary>Between the HUD going down and the first rail falling.</summary>
    private const float LeadInSeconds = 0.6f;

    /// <summary>After the last rail has landed, before the levers appear.</summary>
    private const float SettleSeconds = 0.2f;

    /// <summary>After the levers can be hit, before the first cart is let go.</summary>
    private const float FirstCartSeconds = 0.6f;

    /// <summary>After the last cart is home, so its puff is seen before the board clears.</summary>
    private const float AfterLastSeconds = 0.5f;

    /// <summary>A knight's pivot is at the feet and the art is one unit tall.</summary>
    private const float KnightHalfHeight = 0.5f;

    /// <summary>On a phase with a clock, the carts flash for this long before it runs out — the range's warning.</summary>
    private const float WarningSeconds = 3f;

    private const float HomeSmokeRadius = 0.6f;
    private const float WrongSmokeRadius = 0.9f;

    /// <summary>A cart on the board, and the colour of the flag it has to reach.</summary>
    private struct Rider
    {
        public MineCart Cart;
        public SpriteRenderer Renderer;
        public CartColor Color;
    }

    /// <summary>
    /// The phase itself, inside TrialRunner. Lays the track, stands the levers up,
    /// lets the carts go on their schedule, and runs until every cart is at its own
    /// flag (a pass), one reaches another flag (a loss), or the phase's clock, if
    /// it has one, runs out with carts still on the track (a loss).
    ///
    /// The track is laid through the mine's RailNetwork, so it is the network's to
    /// take up again — TrialRunner does that when this returns.
    /// </summary>
    public static IEnumerator Run(TrialRunner runner, Phase phase)
    {
        var kit = CartSortingKit.Instance;
        var rails = runner.Spawner != null ? runner.Spawner.Rails : null;
        var layout = kit != null && phase != null ? kit.LayoutFor(phase.Track) : null;
        if (rails == null || layout == null)
        {
            runner.Lose();
            yield break;
        }

        yield return Wait(runner, LeadInSeconds);
        if (runner.Stopped) yield break;

        // The flags go up with the track: the layout marks the end of each branch
        // as a drop point, and the network plants the flag for its colour.
        float laid = rails.Lay(layout);
        yield return Wait(runner, laid + SettleSeconds);
        if (runner.Stopped) yield break;

        // A lever for each colour of corner piece the track has: red on the left
        // knight's side of the middle, blue on the right knight's.
        Vector2 middle = Middle(runner);
        var levers = new List<RailSwitchLever>();
        if (!StandLever(runner, kit, rails, RailSwitchColor.Red, middle + Vector2.left * phase.LeverSpot,
                        phase.RedRestsLeft, levers)
            || !StandLever(runner, kit, rails, RailSwitchColor.Blue, middle + Vector2.right * phase.LeverSpot,
                           phase.BlueRestsLeft, levers))
        {
            runner.Lose();
            yield break;
        }

        while (!runner.Stopped && !AllRevealed(levers)) yield return null;
        yield return Wait(runner, FirstCartSeconds);
        if (runner.Stopped) yield break;

        var riding = new List<Rider>();
        for (int i = 0; i < phase.Carts.Length; i++)
        {
            Cart entry = phase.Carts[i];
            runner.After(entry.At, () =>
            {
                // A cart that cannot be put on the track can never get home.
                if (!Release(rails, kit, phase, entry, riding)) runner.Lose();
            });
        }

        float deadline = phase.Seconds > 0f ? Time.time + phase.Seconds : float.PositiveInfinity;

        int home = 0;
        while (!runner.Stopped && home < phase.Carts.Length)
        {
            for (int i = riding.Count - 1; i >= 0; i--)
            {
                Rider rider = riding[i];

                // Gone without reaching a flag: the layout let it roll off the end
                // of a branch that has no drop point. Nothing to sort it against.
                if (rider.Cart == null)
                {
                    Debug.LogWarning("[Carts] A cart left the track without reaching a flag — "
                                     + "every branch of the layout needs a drop point.");
                    runner.Lose();
                    break;
                }

                RailDropPoint flag = FlagReached(rails, rider.Cart);
                if (flag == null) continue;

                riding.RemoveAt(i);
                Vector2 at = CentreOf(rider);
                Object.Destroy(rider.Cart.gameObject);

                if (flag.Flag != rider.Color)
                {
                    NpcFx.SmokeCentredOn(at, WrongSmokeRadius);
                    runner.Lose();
                    break;
                }

                home++;
                NpcFx.SmokeCentredOn(at, HomeSmokeRadius, white: true);
                var audio = AudioManager.Instance;
                if (audio != null && kit.DeliveredSound != null) audio.PlaySFX(kit.DeliveredSound);
            }
            if (runner.Stopped) yield break;

            // The clock, on a phase that has one. Whatever is still rolling goes
            // up in the same dark smoke as a cart at the wrong flag.
            if (Time.time >= deadline)
            {
                for (int i = 0; i < riding.Count; i++)
                {
                    if (riding[i].Cart != null) NpcFx.SmokeCentredOn(CentreOf(riding[i]), WrongSmokeRadius);
                }
                runner.Lose();
                yield break;
            }
            if (Time.time >= deadline - WarningSeconds) Blink(riding);

            yield return null;
        }
        if (runner.Stopped) yield break;

        yield return Wait(runner, AfterLastSeconds);
    }

    /// <summary>
    /// Puts one cart on the track at the mouth of its run. The mouth is laid just
    /// off the frame, so the cart is seen almost as soon as it is let go — the
    /// prefab's own long lead-in would hold it out of sight for seconds.
    /// </summary>
    private static bool Release(RailNetwork rails, CartSortingKit kit, Phase phase, Cart entry,
                                List<Rider> riding)
    {
        MineCart cart = rails.SpawnCart(kit.CartFor(entry.Color), entry.Line, phase.Speed);
        if (cart == null)
        {
            Debug.LogWarning($"[Carts] Could not put a cart on run {entry.Line} of the phase {phase.Number} layout.");
            return false;
        }

        cart.Resume(cart.Line, 0f, cart.Speed);
        riding.Add(new Rider { Cart = cart, Renderer = cart.GetComponent<SpriteRenderer>(), Color = entry.Color });
        return true;
    }

    /// <summary>The flag this cart has reached, or null while it is still on its way.</summary>
    private static RailDropPoint FlagReached(RailNetwork rails, MineCart cart)
    {
        var drops = rails.Drops;
        for (int i = 0; i < drops.Count; i++)
        {
            RailDropPoint drop = drops[i];
            if (drop == null || drop.LineIndex != cart.Line.Index) continue;
            if (cart.Along >= drop.DistanceAlong) return drop;
        }
        return null;
    }

    private static Vector2 CentreOf(Rider rider)
    {
        return rider.Renderer != null
            ? (Vector2)rider.Renderer.bounds.center
            : (Vector2)rider.Cart.transform.position;
    }

    /// <summary>
    /// Time is nearly up: every cart still on the track starts to flash, the way
    /// the range's targets do, and keeps flashing until it is home or the clock
    /// takes it. See CartBlink for why the flash is a component of its own.
    /// </summary>
    private static void Blink(List<Rider> riding)
    {
        for (int i = 0; i < riding.Count; i++)
        {
            MineCart cart = riding[i].Cart;
            if (cart != null && cart.GetComponent<CartBlink>() == null) cart.gameObject.AddComponent<CartBlink>();
        }
    }

    /// <summary>
    /// Stands the lever for one colour, if the track has a fork of that colour.
    /// False only if it has one and the lever could not be made.
    /// </summary>
    private static bool StandLever(TrialRunner runner, CartSortingKit kit, RailNetwork rails,
                                   RailSwitchColor color, Vector2 at, bool restsLeft,
                                   List<RailSwitchLever> levers)
    {
        if (!rails.HasSwitch(color)) return true;

        var lever = RailSwitchLever.Appear(kit.LeverFor(color), at, rails, restsLeft);
        if (lever == null) return false;

        runner.Track(lever.gameObject);
        levers.Add(lever);
        return true;
    }

    private static bool AllRevealed(List<RailSwitchLever> levers)
    {
        for (int i = 0; i < levers.Count; i++)
        {
            if (levers[i] != null && !levers[i].Revealed) return false;
        }
        return true;
    }

    /// <summary>Midway between the two knights, level with their middles.</summary>
    private static Vector2 Middle(TrialRunner runner)
    {
        Vector2 left = runner.LeftKnight != null ? (Vector2)runner.LeftKnight.position : new Vector2(-2f, -0.5f);
        Vector2 right = runner.RightKnight != null ? (Vector2)runner.RightKnight.position : new Vector2(2f, -0.5f);
        return (left + right) * 0.5f + Vector2.up * KnightHalfHeight;
    }

    private static IEnumerator Wait(TrialRunner runner, float seconds)
    {
        float until = Time.time + seconds;
        while (!runner.Stopped && Time.time < until) yield return null;
    }

    // ---- the payout ----

    /// <summary>
    /// A clear. Runs as the fanfare starts: records it, then sends the same two
    /// orbs the target range pays — see TargetRange.SendRewardOrbs.
    /// </summary>
    public static IEnumerator PayOut(TrialRunner runner, Phase phase, bool practice)
    {
        if (!practice) RecordClear(phase);
        yield return TargetRange.SendRewardOrbs(runner);
    }
}
