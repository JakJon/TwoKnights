using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The two minutes a new file opens on: one knight, one line of text at a time,
/// and one control handed over per lesson. It runs in the Main scene before wave
/// one and hands off into it — see <see cref="TutorialRun"/> for how a fresh file
/// gets here instead of to the camp.
///
/// Everything it puts on the board it spawns itself and keeps a reference to,
/// rather than going through the Spawner's fire-and-forget helpers: a lesson has
/// to be able to sweep the sky and start over when the player misses, and it
/// cannot do that with rocks it never held.
///
/// The knight is untouchable throughout. Failing a lesson costs the player the
/// lesson again, never the run.
/// </summary>
public class TutorialDirector : MonoBehaviour
{
    // ---- pacing ---------------------------------------------------------
    // A line with nothing under it is up for this long between its fades. Two
    // values only, by length, so the whole thing reads at one steady tempo.
    private const float ShortLine = 2.2f;
    private const float LongLine = 2.7f;
    // Breath after a lesson is passed, before the praise for it
    private const float BeatAfterLesson = 0.35f;
    // Breath after a lesson is failed, before it comes round again
    private const float BeatBeforeRetry = 0.9f;

    // The arrow lesson's slime walks in along y = 0, straight through where the
    // tutorial's voice stands. Its line alone steps up out of the way; see
    // TutorialText.Raise.
    private const float ArrowLineLift = 1f;

    // The sword lesson's one likely wrong answer is the RIGHT answer to the lesson
    // before it. Three arrow kills is a habit rather than a slip, so that is when
    // the tutorial stops waiting to be understood and says the quiet part.
    private const int SwordNudgeAfterKills = 3;
    private const string SwordNudge = "How about we try out that sword?";

    // ---- rocks ----------------------------------------------------------
    private const float RockSpacing = 2.5f;
    // Every rock is given a speed that makes its flight take this long, whatever
    // corner it comes from. Without that the three arrive out of the order they
    // were sent — the one from the left has half again as far to travel — and the
    // volley stops reading as one rock at a time.
    private const float RockFlightSeconds = 3.5f;
    // ProjectileMovement aims here relative to the knight's origin; the rock
    // speeds have to be worked out against the same point it is flying at.
    private const float RockAimOffsetY = 0.5f;

    // ---- slimes ---------------------------------------------------------
    private const int SlimeSize = 1;                // smallest: 10 health, and it does not split
    private const float SlimeEntryX = 12f;          // off the right edge (the view ends at x = 10)
    // Just clear of the top edge, which the view ends at 5.625. Far enough out to
    // walk on rather than pop in, close enough that the player is not watching it
    // commute: at the stock 1.5 units a second, every extra unit up here is another
    // two-thirds of a second of nothing happening.
    private const float SlimeAboveY = 6.5f;
    private const int SpecialSlimeCount = 4;
    // Spread across the height of the board rather than back in a queue, and sent
    // in faster than a slime normally walks: four of them arriving together is
    // what makes a full bar worth spending. Strung out behind each other at the
    // stock 1.5 units a second, the last one is a twelve-second wait on its own.
    // Closer in than the teaching slimes: the view ends at x = 10, so this is
    // still off the edge, but by the time four of them are walking it is pressure
    // the player is answering rather than a queue they are waiting on.
    private const float SpecialSlimeEntryX = 11f;
    private const float SpecialSlimeTopY = 3.4f;
    private const float SpecialSlimeBottomY = -3.4f;
    private const float SpecialSlimeSpeed = 2.5f;

    // ---- orbs -----------------------------------------------------------
    private const float OrbSpacing = 3f;
    // Measured from the knight's origin, which sits at its feet — this clears the
    // head by about two units, which is the height the player has to aim at.
    private const float OrbHeight = 2.5f;
    private const float OrbEntryX = 12f;
    private const float OrbExitX = -14f;            // past the left edge, so it leaves the view before it expires

    // A lesson can only be failed, never lost. This is not a difficulty valve: it
    // is here so a controller that unplugs itself cannot strand somebody on a
    // screen with no way forward and no menu to escape to.
    private const float LessonTimeout = 150f;

    private Spawner _spawner;
    private Transform _leftKnight;
    private TutorialText _text;
    // A second line that can appear UNDER the first without taking it down, for
    // when a lesson needs to add something rather than replace what it said.
    private TutorialText _hint;
    private TutorialStage _stage;

    // Raised by PlayerHealth for any hit aimed at the left knight, landed or not.
    // Every lesson's failure condition is the same event: something got through.
    private bool _knightWasHit;

    // Instance ids of enemies a sword blow killed, so the sword lesson can tell
    // the swing it asked for from an arrow that happened to get there first.
    private readonly HashSet<int> _swordKills = new HashSet<int>();

    // Everything this lesson has put on the board, so a retry can sweep it
    private readonly List<GameObject> _spawned = new List<GameObject>();

    /// <summary>
    /// Starts the tutorial if this run is one. Returns true when it has taken
    /// over — the caller must not start wave one, the tutorial will.
    /// </summary>
    public static bool TryBegin(Spawner spawner)
    {
        if (spawner == null) return false;
        if (!TutorialRun.Consume()) return false;

        var go = new GameObject("Tutorial Director");
        var director = go.AddComponent<TutorialDirector>();
        director.Begin(spawner);
        return true;
    }

    private void Begin(Spawner spawner)
    {
        _spawner = spawner;
        _leftKnight = spawner.LeftPlayer;

        _stage = new TutorialStage(
            spawner.LeftPlayer != null ? spawner.LeftPlayer.gameObject : null,
            spawner.RightPlayer != null ? spawner.RightPlayer.gameObject : null);
        _stage.OpenOnLeftKnightAlone();

        var banner = FindFirstObjectByType<WaveName>(FindObjectsInactive.Include);
        _text = TutorialText.Create(banner);
        _hint = TutorialText.CreateHint(banner);

        PlayerHealth.OnDamageAttempt += HandleDamageAttempt;
        SwordSwing.OnSwordLanded += HandleSwordLanded;

        StartCoroutine(Run());
    }

    private void Update()
    {
        _stage?.Tick();
    }

    private void OnDestroy()
    {
        PlayerHealth.OnDamageAttempt -= HandleDamageAttempt;
        SwordSwing.OnSwordLanded -= HandleSwordLanded;

        // The banner is its own object out in the arena, not a child of this one,
        // so it does not go when this does unless it is sent
        if (_text != null) Destroy(_text.gameObject);
        if (_hint != null) Destroy(_hint.gameObject);
        ClearSpawned();
    }

    private void HandleDamageAttempt(PlayerHealth knight, string source)
    {
        if (_leftKnight != null && knight != null && knight.gameObject == _leftKnight.gameObject)
        {
            _knightWasHit = true;
        }
    }

    private void HandleSwordLanded(int enemyId, bool killed)
    {
        if (killed) _swordKills.Add(enemyId);
    }

    private IEnumerator Run()
    {
        yield return _text.Say("Welcome to Two Knights.", ShortLine);
        yield return _text.Say("Thanks for testing out the game :)", LongLine);
        yield return _text.Say("You'll need a controller to play.", LongLine);
        yield return _text.Say("Left stick rotates the shield.", LongLine);

        yield return Lesson("Try to block these rocks.", RockLesson(), "rocks");
        yield return _text.Say("Well done.", ShortLine);

        _stage.AllowBow();
        yield return Lesson("To shoot an arrow, press left bumper.",
                            SlimeLesson(new Vector2(SlimeEntryX, 0f), requireSword: false), "arrow",
                            ArrowLineLift);
        yield return _text.Say("Excellent!", ShortLine);

        _stage.AllowSword();
        yield return Lesson("Use left trigger to swing your sword.",
                            SlimeLesson(new Vector2(_leftKnight.position.x, SlimeAboveY), requireSword: true), "sword");
        yield return _text.Say("Great, we're almost done here.", LongLine);

        // The bars arrive with the line that first gives the player a reason to
        // look at them, and only this knight's — the other one is still off the board
        yield return _text.Raise("Collect orbs to fill your special or health.");
        yield return FadeLeftHudIn();
        yield return RunWithTimeout(OrbLesson(), "orbs");
        ClearSpawned();
        yield return _text.Lower();

        _stage.AllowSpecial();
        yield return Lesson("Press the d-pad to use your special.", SpecialLesson(), "special");

        yield return _text.Say("This is all of the basic controls.", LongLine);
        yield return _text.Say("They all apply to your right knight as well.", LongLine);
        yield return _text.Say("Its special is on the face buttons.", LongLine);

        yield return _text.Raise("Good luck!");
        yield return BringEverythingBack();
        // A beat on the finished picture — both knights, both sets of bars — before
        // the last line goes and the wave banner takes the screen
        yield return new WaitForSeconds(LongLine);
        yield return _text.Lower();

        TutorialRun.MarkComplete();
        _spawner.BeginFirstWave();
        Destroy(gameObject);
    }

    // ---- lesson scaffolding ---------------------------------------------

    // A line goes up, its lesson runs under it, the board is swept, the line comes
    // down. Every lesson is shaped the same way, so this is the shape.
    private IEnumerator Lesson(string line, IEnumerator lesson, string label, float lift = 0f)
    {
        yield return _text.Raise(line, lift);
        yield return RunWithTimeout(lesson, label);
        ClearSpawned();
        yield return new WaitForSeconds(BeatAfterLesson);
        // Together rather than one after the other: a hint that outlived the line
        // it was hanging off would read as the start of the next lesson. Lowering
        // an unraised hint costs nothing — see TutorialText.Fade.
        StartCoroutine(_hint.Lower());
        yield return _text.Lower();
    }

    // Drives the lesson by hand so it can be abandoned. The timeout is a safety
    // valve and nothing else — a lesson that hits it has gone wrong, so it says so.
    private IEnumerator RunWithTimeout(IEnumerator lesson, string label)
    {
        float deadline = Time.time + LessonTimeout;

        while (lesson.MoveNext())
        {
            if (Time.time > deadline)
            {
                Debug.LogWarning($"[Tutorial] The '{label}' lesson ran past {LessonTimeout}s " +
                                 "and was let through. Something is stopping it from being passed.");
                yield break;
            }
            yield return lesson.Current;
        }
    }

    // ---- the lessons ----------------------------------------------------

    // Three rocks, one at a time, from three sides. A single hit sweeps the sky and
    // starts the volley over: "blocked in succession" is the lesson, and letting a
    // volley the player has already lost play itself out just adds dead air to it.
    private IEnumerator RockLesson()
    {
        Vector2[] origins =
        {
            _spawner.aboveLeftPlayer,
            _spawner.leftOfLeftPlayer,
            _spawner.belowLeftPlayer,
        };

        while (true)
        {
            _knightWasHit = false;
            var volley = new List<GameObject>();

            for (int i = 0; i < origins.Length; i++)
            {
                volley.Add(SpawnRock(origins[i]));
                yield return WaitUntilHitOrSeconds(RockSpacing);
                if (_knightWasHit) break;
            }

            // The last rock is still in the air when the spacing runs out
            while (!_knightWasHit && AnyAlive(volley)) yield return null;

            DestroyAll(volley);
            if (!_knightWasHit) yield break;

            yield return new WaitForSeconds(BeatBeforeRetry);
        }
    }

    // One slime, walked all the way in. It is over when the slime is dead by the
    // means the line asked for; anything else — it reached the knight, or the
    // player shot the one they were told to swing at — sends another.
    private IEnumerator SlimeLesson(Vector2 origin, bool requireSword)
    {
        int shotInstead = 0;

        while (true)
        {
            _knightWasHit = false;
            GameObject slime = SpawnSlime(origin);
            int slimeId = slime.GetInstanceID();

            while (slime != null && !_knightWasHit) yield return null;

            bool reachedTheKnight = _knightWasHit;
            bool bySword = _swordKills.Contains(slimeId);
            if (slime != null) Destroy(slime);

            if (!reachedTheKnight && (!requireSword || bySword)) yield break;

            // Killed, but with the bow. Nothing else on the board can do it, so
            // this is the player answering the last lesson instead of this one —
            // and the retry alone never tells them that. The nudge arrives UNDER
            // the standing line rather than replacing it, because the instruction
            // is still what they need; they have simply not connected it yet.
            if (requireSword && !reachedTheKnight && !bySword)
            {
                shotInstead++;
                if (shotInstead == SwordNudgeAfterKills) yield return _hint.Raise(SwordNudge);
            }

            yield return new WaitForSeconds(BeatBeforeRetry);
        }
    }

    // Orbs are collected by SHOOTING them, which is why this lesson comes after the
    // bow. They keep coming until the bar is full — the player cannot fail it, only
    // take longer over it.
    private IEnumerator OrbLesson()
    {
        var special = _leftKnight != null ? _leftKnight.GetComponent<PlayerSpecial>() : null;
        if (special == null) yield break;

        float nextOrb = 0f;
        while (!special.specialBarFilled)
        {
            if (Time.time >= nextOrb)
            {
                SpawnManaOrb();
                nextOrb = Time.time + OrbSpacing;
            }
            yield return null;
        }
    }

    // Four slimes at once, which is what the full bar is for. The player is not
    // held to killing them — the lesson is over when the board is clear, however
    // it got that way.
    private IEnumerator SpecialLesson()
    {
        var slimes = new List<GameObject>();
        for (int i = 0; i < SpecialSlimeCount; i++)
        {
            float t = SpecialSlimeCount > 1 ? (float)i / (SpecialSlimeCount - 1) : 0.5f;
            float y = Mathf.Lerp(SpecialSlimeTopY, SpecialSlimeBottomY, t);
            slimes.Add(SpawnSlime(new Vector2(SpecialSlimeEntryX, y), SpecialSlimeSpeed));
        }

        while (AnyAlive(slimes)) yield return null;
    }

    // ---- the handover ----------------------------------------------------

    private IEnumerator FadeLeftHudIn()
    {
        const float seconds = 0.6f;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            _stage.SetLeftHudOpacity(elapsed / seconds);
            yield return null;
        }
        _stage.SetLeftHudOpacity(1f);
    }

    // The second knight arrives under the last line, so the player reads "good
    // luck" while the board fills out around it.
    private IEnumerator BringEverythingBack()
    {
        const float seconds = 1.2f;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / seconds;
            _stage.SetRightKnightAlpha(t);
            _stage.SetHudOpacity(1f, t);
            yield return null;
        }

        // Not just alpha 1: this is also what gives both knights their controls
        // back and takes the untouchable scaffolding down.
        _stage.RestoreEverything();
    }

    // ---- spawning --------------------------------------------------------

    private GameObject SpawnRock(Vector2 origin)
    {
        GameObject rock = Instantiate(_spawner.projectilePrefab);
        _spawned.Add(rock);

        var mover = rock.GetComponent<ProjectileMovement>();
        if (mover != null)
        {
            Vector2 aim = (Vector2)_leftKnight.position + new Vector2(0f, RockAimOffsetY);
            float speed = Vector2.Distance(origin, aim) / RockFlightSeconds;
            mover.Initialize(_leftKnight, origin, speed);
        }

        return rock;
    }

    // moveSpeed of 0 or less keeps the prefab's own walk, which is what the two
    // teaching slimes want — the player is meant to have time with those.
    private GameObject SpawnSlime(Vector2 position, float moveSpeed = 0f)
    {
        GameObject slime = Instantiate(_spawner.slimePrefab);
        _spawned.Add(slime);
        slime.transform.position = position;

        var script = slime.GetComponent<EnemySlime>();
        if (script != null)
        {
            script.size = SlimeSize;
            script.targetPlayer = _leftKnight;
            script.InitializeSlime();
            // After InitializeSlime, which sets the walk for the bigger sizes
            if (moveSpeed > 0f) script.moveSpeed = moveSpeed;
        }

        return slime;
    }

    private void SpawnManaOrb()
    {
        float y = _leftKnight.position.y + OrbHeight;
        // Through the Spawner rather than by hand: an orb needs no minding, it
        // crosses the screen and takes itself off the board at the far side.
        _spawner.SpawnOrb(new Vector2(OrbEntryX, y), new Vector2(OrbExitX, y), false);
    }

    // ---- odds and ends ---------------------------------------------------

    private IEnumerator WaitUntilHitOrSeconds(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds && !_knightWasHit)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private static bool AnyAlive(List<GameObject> objects)
    {
        for (int i = 0; i < objects.Count; i++)
        {
            if (objects[i] != null) return true;
        }
        return false;
    }

    private static void DestroyAll(List<GameObject> objects)
    {
        for (int i = 0; i < objects.Count; i++)
        {
            if (objects[i] != null) Destroy(objects[i]);
        }
        objects.Clear();
    }

    private void ClearSpawned()
    {
        DestroyAll(_spawned);
    }
}
