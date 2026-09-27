using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays the scenes a wave earned: what it finished, and what that opened.
///
/// Completions are collected as they fire rather than diffed at wave end, for the
/// reason WaveSurvivedPanel already had to: completion cascades. Finishing one quest
/// publishes a stat that can open the next in the same evaluation pass, so there is
/// no before-and-after to compare.
///
/// Offers are not collected at all. They are read off the save at wave end, because
/// an offer can be owed long before there is anywhere to pay it — see OwedHere.
///
/// Static, because the thing that listens has to outlive any particular scene object
/// and because quests complete during a wave while the player is nowhere near a
/// menu. Subscribed once per process.
/// </summary>
public static class QuestSceneQueue
{
    private static readonly Queue<string> _completed = new Queue<string>();
    private static bool _subscribed;

    public static bool HasPending => _completed.Count > 0 || AnyOwedOffer();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Subscribe()
    {
        if (_subscribed) return;
        _subscribed = true;
        _completed.Clear();
        QuestProgress.OnQuestCompleted += id => _completed.Enqueue(id);
    }

    /// <summary>
    /// Drops the completions queued without playing them. For a run abandoned mid-wave.
    ///
    /// Offers need no dropping and must not be dropped: they live in the save as
    /// "unlocked, never announced", so one this run could not pay is still owed by
    /// the next.
    /// </summary>
    public static void Discard()
    {
        _completed.Clear();
    }

    // ---------- what is owed, and whether it can be paid here ----------

    /// <summary>
    /// An offer the player is standing in the right place to receive: its gate is
    /// open, its NPC has never introduced it, and it either belongs to no map or
    /// belongs to THIS one.
    ///
    /// The map check is why offers are read off the save rather than queued as they
    /// fire. A gate is a stat threshold and knows nothing about where the player is:
    /// breaking the tenth empty cart opens the Cartographer's cart quest wherever
    /// the count ticks over, and when the mine run it opened in ended before the
    /// scene could play, the Cartographer turned up in the middle of a forest one.
    /// Holding the offer back means the debt has to survive a run ending, the queue
    /// being discarded, and the game being closed — which the save already handles,
    /// since "unlocked and never announced" IS that debt written down.
    ///
    /// Safe to hold, because a map-tagged quest's objectives only move on its own
    /// map. An offer waiting for the right ground costs the player no progress.
    /// </summary>
    private static bool OwedHere(Quest quest, string mapId)
    {
        if (quest == null) return false;
        if (!quest.IsUnlocked) return false;
        if (QuestProgress.IsAnnounced(quest.Id) || QuestProgress.IsCompleted(quest.Id)) return false;
        // Camp business — the Order lines — belongs anywhere.
        if (string.IsNullOrEmpty(quest.MapId)) return true;
        // Nowhere identifiable to be. Offer it rather than strand it.
        if (string.IsNullOrEmpty(mapId)) return true;
        return quest.MapId == mapId;
    }

    private static string CurrentMapId()
    {
        var manager = WaveManager.ActiveInstance;
        var map = manager != null ? manager.CurrentMap : null;
        return map != null ? map.MapId : "";
    }

    private static List<Quest> OwedOffers()
    {
        string here = CurrentMapId();
        var list = new List<Quest>();
        foreach (var quest in QuestDatabase.All)
        {
            if (OwedHere(quest, here)) list.Add(quest);
        }
        return list;
    }

    private static bool AnyOwedOffer()
    {
        string here = CurrentMapId();
        foreach (var quest in QuestDatabase.All)
        {
            if (OwedHere(quest, here)) return true;
        }
        return false;
    }

    /// <summary>
    /// Plays everything the wave earned, and returns when the board is clear.
    ///
    /// Completions first, then offers. A completion is the thing that just happened;
    /// an offer is what to do next, and most offers ARE a completion — the chained
    /// quests have no offer text of their own precisely because the previous quest's
    /// last line is their introduction.
    /// </summary>
    public static IEnumerator PlayPending()
    {
        var completed = Drain(_completed);
        // Read after the completions are drained but before any of them plays. A
        // completion opens its successor during the wave, so the successor is
        // already sitting in the save as owed, and nothing here is introduced until
        // its scene has actually run.
        var unlocked = OwedOffers();

        // Everything opened by a completion is shown as that completion's own
        // consequence, so the player reads "you finished this, and it opened these"
        // as one thought. Only what opened WITHOUT a completion behind it needs a
        // scene of its own.
        //
        // Attribution is by GATE, not by "whichever completion happened to be last
        // in the queue". Two Orders can each finish something in the same wave —
        // an initiation completing while a different Order's initiation is also
        // satisfied — and crediting everything newly unlocked to the last one to
        // finish would fold an unrelated Order's own initiation into someone else's
        // reward cards, announcing it without ever giving it its own cutscene. Each
        // completion claims only what is actually gated on IT.
        var claimed = new List<Quest>();

        for (int i = 0; i < completed.Count; i++)
        {
            var quest = completed[i];
            var opened = ClaimedBy(quest.Id, unlocked, claimed);
            claimed.AddRange(opened);
            yield return QuestScene.Play(quest, QuestScene.Kind.Completion, opened);
            Introduce(quest);
            for (int j = 0; j < opened.Count; j++) Introduce(opened[j]);
        }

        // What opened with nothing finishing alongside it — a threshold crossed
        // mid-wave, an Order noticing the player — gets its own scene. That scene
        // always plays and always carries at least its own name/objectives/reward
        // card, whether or not the NPC has a line to go with it: a quest with no
        // offer text (a silent reveal) must not go completely unannounced.
        var leftovers = Unclaimed(unlocked, claimed);
        for (int i = 0; i < leftovers.Count; i++)
        {
            var quest = leftovers[i];
            yield return QuestScene.Play(quest, QuestScene.Kind.Offer, new List<Quest> { quest });
            Introduce(quest);
        }
    }

    /// <summary>
    /// Which newly-unlocked quests are gated on <paramref name="completedQuestId"/>
    /// finishing — the ones this completion is what actually opened, read off the
    /// same completion stat <see cref="QuestBuild.After"/> gates against. A quest
    /// already claimed by an earlier completion in this same pass (an UnlockMode.Any
    /// gate satisfied by more than one of them) is not claimed twice.
    /// </summary>
    private static List<Quest> ClaimedBy(string completedQuestId, List<Quest> unlocked, List<Quest> alreadyClaimed)
    {
        string stat = QuestDatabase.CompletionStatKey(completedQuestId);
        var claim = new List<Quest>();
        for (int i = 0; i < unlocked.Count; i++)
        {
            var quest = unlocked[i];
            if (alreadyClaimed.Contains(quest) || claim.Contains(quest)) continue;
            if (GatedOn(quest, stat)) claim.Add(quest);
        }
        return claim;
    }

    private static bool GatedOn(Quest quest, string statKey)
    {
        if (quest.Unlocks == null) return false;
        for (int i = 0; i < quest.Unlocks.Length; i++)
        {
            if (quest.Unlocks[i] != null && quest.Unlocks[i].StatKey == statKey) return true;
        }
        return false;
    }

    /// <summary>
    /// The quest now exists: it goes into the log, its meters start, and anything it
    /// reveals may be drafted.
    ///
    /// AFTER the scene, never before. That is the whole rule — a quest starts when
    /// its NPC says so. It applies even to a quest with no scene of its own: the
    /// chained ones are introduced by the last line of the quest before them, which
    /// has just played, so they are introduced too.
    /// </summary>
    private static void Introduce(Quest quest)
    {
        if (quest != null) QuestProgress.MarkAnnounced(quest.Id);
    }

    private static List<Quest> Unclaimed(List<Quest> all, List<Quest> claimed)
    {
        var rest = new List<Quest>();
        for (int i = 0; i < all.Count; i++)
        {
            if (!claimed.Contains(all[i])) rest.Add(all[i]);
        }
        return rest;
    }

    private static List<Quest> Drain(Queue<string> source)
    {
        var list = new List<Quest>();
        while (source.Count > 0)
        {
            var quest = QuestDatabase.Get(source.Dequeue());
            if (quest == null || list.Contains(quest)) continue;
            list.Add(quest);
        }
        return list;
    }
}
