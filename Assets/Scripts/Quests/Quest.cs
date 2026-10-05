using System;
using UnityEngine;

/// <summary>
/// One thing a quest asks for. A quest holds several because the Order
/// initiation quests are deliberately "simple but thorough" — proving you
/// practise an Order means meeting two or three conditions together, not
/// running one counter up. Ordinary quests carry exactly one.
/// </summary>
[Serializable]
public class QuestObjective
{
    public string StatKey;
    public int Target;

    // TWO TEXTS, ONE PER SCREEN (owner, 2026-10-05). An objective used to carry a
    // single label that both screens dressed up differently: the quest menu put a
    // counter in front of it (and "Best attempt:" in front of that, for records),
    // and the NPC's card put the goal number in front of it. That made every line
    // a fragment written to survive two prefixes - "slain by venom", "at once,
    // carrying venom" - instead of a sentence. Now each screen has its own text
    // and prints it as written. Nothing is added automatically.
    //
    // Both are templates. The tokens:
    //   {count}    progress, printed "12/100" (on the NPC card: just the goal)
    //   {current}  the progress number alone
    //   {target}   the goal number alone - use it instead of typing the number,
    //              so the words cannot drift from the Target beside them
    // A text with no {count} shows no counter, which is how an objective keeps its
    // number to itself ("Venture further into the forest").

    /// <summary>
    /// The line the quest MENU prints for this objective, as a template (see the
    /// tokens above). Empty falls back to "{count} " plus the stat's short label.
    /// </summary>
    public string MenuText;

    /// <summary>
    /// The line the NPC's CARD prints, when the quest is offered and again when it
    /// is finished. No counter belongs here: it is the ask, said once. Empty means
    /// the menu's words with the counter replaced by the goal.
    /// </summary>
    public string NpcText;

    /// <summary>
    /// Leaves this objective off the NPC's card. For a quest whose first line
    /// already says the whole ask ("Obtain then activate the Last Light upgrade"),
    /// so a second line would repeat it. The menu still lists it.
    /// </summary>
    public bool HideOnNpcCard;

    /// <summary>
    /// Measure this against the player's whole history rather than from the moment
    /// the quest opened.
    ///
    /// The exception, not the rule. It exists for the explorer quests, whose ask is
    /// literally "have you seen every wave type on this map" — a lifetime question
    /// about a set that never resets. Measuring those from the quest's own opening
    /// would make "all of them" unreachable for anyone who had already met most of
    /// them. Everything else starts at zero when the quest appears, which is what
    /// stops a quest arriving half done.
    /// </summary>
    public bool Lifetime;

    /// <summary>
    /// Which quest this objective belongs to, stamped by the Quest constructor so
    /// the objective can find its own private counter. Not authored.
    /// </summary>
    public string OwnerQuestId = "";

    public QuestObjective(string statKey, int target, string menu = null, string npc = null,
                          bool lifetime = false, bool hideOnNpcCard = false)
    {
        StatKey = statKey;
        Target = target;
        MenuText = menu;
        NpcText = npc;
        Lifetime = lifetime;
        HideOnNpcCard = hideOnNpcCard;
    }

    /// <summary>
    /// Where the number comes from: this quest's own copy of the counter, which
    /// began at zero when the quest opened. See QuestMeters for why.
    /// </summary>
    public string MeterKey =>
        Lifetime || string.IsNullOrEmpty(OwnerQuestId)
            ? StatKey
            : QuestMeters.Key(OwnerQuestId, StatKey);

    /// <summary>
    /// This objective asks for a best attempt — the biggest wave, the longest
    /// streak, the deepest run — rather than for a total that only ever climbs.
    ///
    /// It changes nothing about how the objective is judged. The menu used to put
    /// "Best attempt:" in front of these on its own; since 2026-10-05 the menu
    /// prints MenuText as written, so a line that wants those words carries them.
    /// </summary>
    public bool IsRecord => StatsDatabase.IsRecord(StatKey);

    public int Current => Mathf.Min(PlayerStats.Get(MeterKey), Target);
    public bool IsMet => PlayerStats.Get(MeterKey) >= Target;

    private string MenuTemplate =>
        !string.IsNullOrEmpty(MenuText) ? MenuText : "{count} " + StatsDatabase.GetShortLabel(StatKey);

    /// <summary>What the quest menu prints for this objective right now.</summary>
    public string MenuLine => Fill(MenuTemplate, withProgress: true);

    /// <summary>What the NPC's card prints for this objective.</summary>
    public string NpcLine => Fill(string.IsNullOrEmpty(NpcText) ? MenuTemplate : NpcText, withProgress: false);

    private string Fill(string template, bool withProgress)
    {
        if (string.IsNullOrEmpty(template)) return "";
        string goal = Target.ToString();
        return template
            .Replace("{count}", withProgress ? Current + "/" + goal : goal)
            .Replace("{current}", Current.ToString())
            .Replace("{target}", goal);
    }
}

/// <summary>A stat threshold that has to be true before a quest is even visible.</summary>
[Serializable]
public class UnlockCondition
{
    public string StatKey;
    public int AtLeast;

    /// <summary>
    /// When set, the threshold is measured from where the stat stood the moment
    /// <see cref="SinceQuestId"/> completed, not from zero.
    ///
    /// This is what makes "three more Rat Kings AFTER the Crimson Twins" mean what
    /// it says. Without it a player who had already killed the Rat King thirty
    /// times would see the quest open the instant its parent finished, because the
    /// lifetime counter was over the line long before the gate existed.
    ///
    /// Empty for an absolute threshold — an Order initiation's "poison 60 things"
    /// has no parent and genuinely counts from the beginning of the file.
    /// </summary>
    public string SinceQuestId;

    public UnlockCondition(string statKey, int atLeast = 1, string sinceQuestId = null)
    {
        StatKey = statKey;
        AtLeast = atLeast;
        SinceQuestId = sinceQuestId ?? "";
    }

    public bool IsRelative => !string.IsNullOrEmpty(SinceQuestId);

    /// <summary>
    /// Where the counter stood when the parent completed. Written once, by
    /// QuestProgress.CompleteQuest; absent until then, which is also why a
    /// relative condition reads as unmet while its parent is still outstanding.
    /// </summary>
    public static string BaselineStatKey(string questId, string statKey)
    {
        return "baseline." + questId + "." + statKey;
    }

    public int Current
    {
        get
        {
            int now = PlayerStats.Get(StatKey);
            if (!IsRelative) return now;
            if (!QuestProgress.IsCompleted(SinceQuestId)) return 0;
            int baseline = PlayerStats.Get(BaselineStatKey(SinceQuestId, StatKey));
            return Mathf.Max(0, now - baseline);
        }
    }

    public bool IsMet
    {
        get
        {
            // A relative condition cannot be met before the quest it counts from
            // has finished — there is no baseline to measure against yet.
            if (IsRelative && !QuestProgress.IsCompleted(SinceQuestId)) return false;
            return Current >= AtLeast;
        }
    }
}

/// <summary>
/// All: every condition must hold (the default, and what every shipped gate uses).
/// Any: one is enough, for a quest reachable by more than one route. Nothing
/// authors it right now — the Shadow initiation did until its trigger became a
/// single counter — but it stays because "either door in" is a shape the Order
/// lines will want again.
/// </summary>
public enum UnlockMode
{
    All = 0,
    Any = 1
}

[Serializable]
public class QuestReward
{
    public int Crystals;
    /// <summary>Equipment id granted on completion; empty for none.</summary>
    public string EquipmentId;
    /// <summary>Gives every knight one more equipment slot. Cumulative — two quests that grant it leave you with three.</summary>
    public bool ExtraEquipmentSlot;
    /// <summary>Gives every knight one more special slot, and a knight with two fires both on one bar. Cumulative, same as the equipment slot.</summary>
    public bool ExtraSpecialSlot;

    /// <summary>
    /// An upgrade this quest REVEALS — the asset filename slug, e.g. "acid_dagger".
    ///
    /// One field doing two jobs deliberately. Capstones and combination upgrades
    /// stay out of the draft entirely until their quest is unlocked, and the same
    /// quest then lists the upgrade as its reward. Those are the same fact said
    /// twice, so they are stored once: UpgradeManager reads this to decide what may
    /// appear, and the log reads it to name the prize.
    ///
    /// The quest is therefore the reveal, and finishing it is the acknowledgement —
    /// which is why the objective is always "acquire it AND do something with it"
    /// rather than merely acquiring it.
    /// </summary>
    public string UpgradeSlug;

    public bool RevealsUpgrade => !string.IsNullOrEmpty(UpgradeSlug);

    /// <summary>
    /// Map id opened by finishing this quest; empty for none. The unlock is
    /// normally the MAP's own business — MapProgressStore opens the next map the
    /// moment a gate boss first falls — so this is mostly a way for the quest
    /// that asks for that kill to say out loud what the kill is worth. Completing
    /// the quest applies it as well, which is a no-op when the gate already did.
    /// </summary>
    public string UnlocksMapId;

    public bool GrantsEquipment => !string.IsNullOrEmpty(EquipmentId);
    public bool UnlocksMap => !string.IsNullOrEmpty(UnlocksMapId);

    /// <summary>
    /// What to call the map this quest opens. Named once it is actually open,
    /// and "A new map" while it is still shut: the level select draws a locked
    /// map as "? ? ?", and the quests that open one are the same quests that
    /// hide their objective, so naming it early gives away the thing at the
    /// bottom of the map. By the time such a quest completes the gate kill has
    /// already run, so the reward reads as the real name where it matters.
    /// </summary>
    public string DescribeMapUnlock()
    {
        if (!UnlocksMap) return "";
        var catalog = MapCatalog.Instance;
        var map = catalog != null ? catalog.Find(UnlocksMapId) : null;
        if (map == null || !MapProgressStore.IsUnlocked(map)) return "A new map";
        return map.DisplayName;
    }

    /// <summary>
    /// "The Gnawed Crown, A second equipment slot" — everything the quest gives
    /// EXCEPT crystals. One phrasing shared by the quest log and the in-run
    /// completion panel, so a reward can never read two different ways depending
    /// on where you saw it.
    ///
    /// Crystals are deliberately absent: an amount is always drawn as the count
    /// followed by the crystal icon (see CrystalText), never spelled out as
    /// "2 Crystals". Leaving them out of the string is what stops that form from
    /// being reachable at all.
    /// </summary>
    public string DescribeItems()
    {
        var parts = new System.Collections.Generic.List<string>();
        if (UnlocksMap) parts.Add(DescribeMapUnlock());
        if (GrantsEquipment)
        {
            var catalog = EquipmentCatalog.Instance;
            var def = catalog != null ? catalog.Find(EquipmentId) : null;
            parts.Add(def != null ? def.DisplayName : EquipmentId);
        }
        if (ExtraEquipmentSlot) parts.Add("Another equipment slot");
        if (ExtraSpecialSlot) parts.Add("Another special slot");
        if (RevealsUpgrade) parts.Add(QuestUpgradeReveals.DisplayName(UpgradeSlug));
        return parts.Count == 0 ? "" : string.Join(", ", parts.ToArray());
    }
}

[Serializable]
public class Quest
{
    public string Id;
    public string Name;
    public string Description;
    /// <summary>Groups the quest under a map in the log. Empty means the camp itself.</summary>
    public string MapId;
    public QuestObjective[] Objectives;
    public UnlockCondition[] Unlocks;
    public UnlockMode UnlockMode;
    public QuestReward Reward;

    /// <summary>
    /// What the NPC says when the quest appears. EMPTY MEANS NO OFFER SCENE, not
    /// "fall back to the description" — most second-in-chain quests are offered by
    /// the completion of the quest before them, and playing a second scene for
    /// them would say the same thing twice.
    /// </summary>
    public string OfferText;

    /// <summary>What the NPC says when the quest finishes.</summary>
    public string CompletionText;

    /// <summary>Who turns up, and in which Order's colours.</summary>
    public QuestCast Cast;

    /// <summary>
    /// Whether this quest is awake during the player's very first run. False for
    /// all but one: the tutorial and run one are the same run, and a new player
    /// walking out of the tutorial into six quest scenes learns nothing from any
    /// of them. Only "A Ruckus in the Wood" is live, and killing the Rat King ends
    /// that run, so nothing downstream of it is reachable anyway.
    /// </summary>
    public bool LiveDuringTutorialRun;

    /// <summary>True while this quest is being held back by the tutorial run.</summary>
    public bool IsFrozenByTutorialRun => TutorialRun.IsTutorialRun && !LiveDuringTutorialRun;

    public Quest(string id, string name, string description,
                 QuestObjective[] objectives, QuestReward reward,
                 string mapId = "",
                 UnlockCondition[] unlocks = null,
                 UnlockMode unlockMode = UnlockMode.All,
                 string offer = null,
                 string completion = null,
                 QuestCast cast = default,
                 bool liveDuringTutorialRun = false)
    {
        Id = id;
        Name = name;
        Description = description;
        MapId = mapId ?? "";
        Objectives = objectives ?? new QuestObjective[0];
        // Each objective needs to know whose it is before it can find its own
        // counter. Stamped here rather than authored, so a line file cannot get it
        // wrong and a copied-and-pasted objective cannot end up reading another
        // quest's progress.
        for (int i = 0; i < Objectives.Length; i++)
        {
            if (Objectives[i] != null) Objectives[i].OwnerQuestId = Id;
        }
        Reward = reward ?? new QuestReward();
        Unlocks = unlocks ?? new UnlockCondition[0];
        UnlockMode = unlockMode;
        OfferText = offer ?? "";
        CompletionText = completion ?? "";
        Cast = cast;
        LiveDuringTutorialRun = liveDuringTutorialRun;
    }

    /// <summary>An offer scene only plays for a quest that actually has one.</summary>
    public bool HasOfferScene => !string.IsNullOrEmpty(OfferText) && Cast.HasCast;
    public bool HasCompletionScene => !string.IsNullOrEmpty(CompletionText) && Cast.HasCast;

    public bool HasObjectives => Objectives != null && Objectives.Length > 0;

    /// <summary>True once every objective is met. A quest with no objectives is never satisfied.</summary>
    public bool IsSatisfied
    {
        get
        {
            if (!HasObjectives) return false;
            if (IsFrozenByTutorialRun) return false;
            // A quest nobody has offered yet cannot be finished. The meters take
            // care of this on their own — they do not run before the scene — but a
            // lifetime objective reads a counter that was running all along, and
            // without this an explorer quest could complete before the cartographer
            // had said a word about it.
            if (!QuestProgress.IsAnnounced(Id)) return false;
            for (int i = 0; i < Objectives.Length; i++)
            {
                if (!Objectives[i].IsMet) return false;
            }
            return true;
        }
    }

    /// <summary>True when nothing gates this quest, or the gate has opened.</summary>
    public bool IsUnlocked
    {
        get
        {
            if (IsFrozenByTutorialRun) return false;
            if (Unlocks == null || Unlocks.Length == 0) return true;
            if (UnlockMode == UnlockMode.Any)
            {
                for (int i = 0; i < Unlocks.Length; i++)
                {
                    if (Unlocks[i].IsMet) return true;
                }
                return false;
            }
            for (int i = 0; i < Unlocks.Length; i++)
            {
                if (!Unlocks[i].IsMet) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// True when this quest measures <paramref name="statKey"/> over the player's
    /// whole history rather than from its own opening — so the per-quest meters
    /// leave it alone. See QuestObjective.Lifetime.
    /// </summary>
    public bool MeasuresLifetime(string statKey)
    {
        if (Objectives == null) return false;
        for (int i = 0; i < Objectives.Length; i++)
        {
            if (Objectives[i] != null && Objectives[i].StatKey == statKey) return Objectives[i].Lifetime;
        }
        return false;
    }

    /// <summary>
    /// Whether a write to <paramref name="statKey"/> is this quest's business —
    /// which is what drives the log's progress refresh.
    ///
    /// Matches the quest's OWN meter as well as the lifetime counter behind it.
    /// The meter is what the objective actually reads, and a record beaten only in
    /// this quest's copy (a good wave that does not beat the player's best ever)
    /// moves no lifetime stat at all.
    /// </summary>
    public bool WatchesStat(string statKey)
    {
        if (Objectives == null) return false;
        for (int i = 0; i < Objectives.Length; i++)
        {
            if (Objectives[i] == null) continue;
            if (Objectives[i].StatKey == statKey) return true;
            if (Objectives[i].MeterKey == statKey) return true;
        }
        return false;
    }
}
