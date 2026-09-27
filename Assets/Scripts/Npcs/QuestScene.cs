using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays one quest's moment: a completion is named on its own card first, then the
/// NPC arrives, says their piece, the reward lands, whatever opened is shown, and
/// they leave their own way.
///
/// Built as a TutorialDirector clone, which is the house pattern for anything
/// choreographed — a code-created object, a static entry point, and a linear
/// IEnumerator body. There is no Timeline in this project and this does not add one.
///
/// The arena is frozen by the CALLER (Spawner already holds timeScale at zero
/// through the wave-end beat), so every wait in here and everything it drives runs
/// on unscaled time.
/// </summary>
public class QuestScene : MonoBehaviour
{
    public enum Kind { Offer, Completion }

    /// <summary>True while any quest scene is on screen. Read by the Spawner's guard.</summary>
    public static bool IsPlaying { get; private set; }

    private DialogueBox _box;
    private QuestNotices _notices;
    private readonly List<NpcActor> _cast = new List<NpcActor>();

    /// <summary>
    /// Runs the scene and returns when the board is clear again. Returns immediately
    /// for a quest with nothing to say — a chained quest whose offer is the previous
    /// quest's completion has no offer scene at all, and that is expressed as empty
    /// text rather than as a flag.
    /// </summary>
    public static IEnumerator Play(Quest quest, Kind kind, IList<Quest> unlocked)
    {
        string line = kind == Kind.Offer ? quest.OfferText : quest.CompletionText;
        bool hasLine = !string.IsNullOrEmpty(line) && quest.Cast.HasCast;
        bool hasReward = kind == Kind.Completion;
        bool hasCards = unlocked != null && unlocked.Count > 0;

        if (!hasLine && !hasReward && !hasCards) yield break;

        var go = new GameObject("Quest Scene");
        var scene = go.AddComponent<QuestScene>();
        IsPlaying = true;
        yield return scene.Run(quest, line, kind, hasReward, unlocked);
        IsPlaying = false;
        Destroy(go);
    }

    private IEnumerator Run(Quest quest, string line, Kind kind, bool showReward, IList<Quest> unlocked)
    {
        // The fight's leftovers go before anyone walks in — damage numbers still
        // rising, blood settling, fire drifting off the last thing that burned, and
        // the carts, track and mirrors the wave left standing.
        ArenaSweep.Clear();

        var banner = FindFirstObjectByType<WaveName>(FindObjectsInactive.Include);
        _box = DialogueBox.Create(banner);
        _notices = QuestNotices.Create(banner);

        // What you just finished, before anybody arrives to talk about it. The
        // scene otherwise opens on an NPC congratulating you for something the
        // game never named — the quest was taken several waves ago and the player
        // has been fighting since.
        if (kind == Kind.Completion) yield return _notices.ShowCompleted(quest);

        bool spoke = false;
        if (!string.IsNullOrEmpty(line) && quest.Cast.HasCast)
        {
            yield return BringOnCast(quest.Cast, line);
            yield return Speak(quest, line);
            spoke = true;
        }

        // The box comes down BEFORE the notices go up. Both draw in the same band
        // at the foot of the screen, so leaving the last spoken line standing puts
        // two blocks of gold text through each other.
        if (spoke) yield return _box.Hide();

        // The reward, then what it opened. In that order deliberately: the thing you
        // were given belongs to the quest that just ended, and the things that opened
        // belong to what comes next.
        if (showReward) yield return _notices.ShowReward(quest);
        if (unlocked != null && unlocked.Count > 0) yield return _notices.ShowUnlocked(unlocked);

        if (spoke) yield return TakeOffCast();

        if (_box != null) Destroy(_box.gameObject);
        if (_notices != null) Destroy(_notices.gameObject);
    }

    private IEnumerator BringOnCast(QuestCast cast, string line)
    {
        var catalog = NpcCatalog.Instance;
        if (catalog == null)
        {
            Debug.LogWarning("[QuestScene] No NpcCatalog in Resources; the scene plays with no one in it.");
            yield break;
        }

        // A pair stands either side of the mark. One stands on it.
        var marks = cast.IsPair
            ? new[] { NpcActor.Mark + Vector3.left * NpcActor.PairOffset,
                      NpcActor.Mark + Vector3.right * NpcActor.PairOffset }
            : new[] { NpcActor.Mark };

        var first = NpcActor.Spawn(catalog.Find(cast.Primary), cast.PrimaryAccent, marks[0]);
        if (first != null)
        {
            _cast.Add(first);
            SetInitialCrown(first, cast.Primary, line);
        }

        NpcActor second = null;
        if (cast.IsPair)
        {
            second = NpcActor.Spawn(catalog.Find(cast.Secondary), cast.SecondaryAccent, marks[1]);
            if (second != null) _cast.Add(second);
        }

        // Both arrive at once rather than one after the other — a combination quest
        // is two Orders meeting, and queueing them would read as two scenes.
        if (second != null) StartCoroutine(second.Enter(marks[1]));
        if (first != null) yield return first.Enter(marks[0]);
        // A beat for the second to finish whatever it is doing behind the first.
        if (second != null) yield return WaitUnscaled(0.2f);
    }

    /// <summary>
    /// Whether the King walks on wearing his crown.
    ///
    /// The text decides, not the save. By the time a scene plays, the quest that
    /// won the crown back has ALREADY been recorded as complete — the queue runs
    /// after the fact — so asking the save would spawn him wearing the thing he is
    /// about to be handed. A line that contains the reveal starts him without it;
    /// every other scene shows him as the save actually has him.
    /// </summary>
    private static void SetInitialCrown(NpcActor actor, NpcId npc, string line)
    {
        if (npc != NpcId.King) return;
        bool revealedInThisScene = !string.IsNullOrEmpty(line) && line.Contains("<crown>");
        actor.SetCrownVisible(!revealedInThisScene
                              && QuestProgress.IsCompleted(ForestQuests.Ruckus));
    }

    private IEnumerator TakeOffCast()
    {
        for (int i = 1; i < _cast.Count; i++) StartCoroutine(_cast[i].Leave());
        if (_cast.Count > 0) yield return _cast[0].Leave();
        _cast.Clear();
    }

    private IEnumerator Speak(Quest quest, string line)
    {
        var speaker = _cast.Count > 0 ? _cast[0] : null;
        var entry = NpcCatalog.Instance != null ? NpcCatalog.Instance.Find(quest.Cast.Primary) : null;
        float pitch = entry != null ? entry.voicePitch : 1f;

        // The cartographer reads his map while he talks, on his own loop, rather
        // than standing still through the conversation.
        Coroutine idle = null;
        if (speaker != null && quest.Cast.Primary == NpcId.Cartographer)
        {
            idle = StartCoroutine(MapLoop(speaker));
        }

        var pages = DialogueScript.Parse(line);
        for (int i = 0; i < pages.Count; i++)
        {
            yield return _box.Speak(pages[i], quest.Cast.Primary, pitch,
                                    directive => RunDirective(speaker, directive));
        }

        if (idle != null)
        {
            StopCoroutine(idle);
            // The loop is cut wherever it happened to be, so put him back on the
            // standing pose by hand. Without this he reads the map right through
            // the reward and unlock cards on any run where the last page landed
            // mid-consultation.
            if (speaker != null) speaker.StandStill();
        }
    }

    private IEnumerator MapLoop(NpcActor actor)
    {
        while (true)
        {
            yield return actor.ConsultMap();
            // ConsultMap returns instantly if the map states were never wired, and
            // an inner enumerator that completes without yielding does NOT cost a
            // frame — so without this the loop would spin the editor to a halt
            // rather than merely doing nothing.
            yield return null;
        }
    }

    /// <summary>
    /// The staging beats authored into the prose. Each returns a coroutine because
    /// one of them — the King stepping away mid-sentence — takes real time.
    /// </summary>
    private IEnumerator RunDirective(NpcActor speaker, DialogueScript.Directive directive)
    {
        if (speaker == null) yield break;
        switch (directive)
        {
            case DialogueScript.Directive.ShowCrown: speaker.SetCrownVisible(true); break;
            case DialogueScript.Directive.HideCrown: speaker.SetCrownVisible(false); break;
            case DialogueScript.Directive.Exit: yield return speaker.StepAway(); break;
        }
    }

    private static IEnumerator WaitUnscaled(float seconds)
    {
        float until = Time.unscaledTime + seconds;
        while (Time.unscaledTime < until) yield return null;
    }

    private void OnDestroy()
    {
        IsPlaying = false;
    }
}
