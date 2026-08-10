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
    /// <summary>
    /// Prose shown in place of the stat's own label. This is where a quest hides
    /// its mechanism: "Venture further into the forest" rather than
    /// "1/1 camp fields gate cleared".
    /// </summary>
    public string Label;
    /// <summary>Suppresses the "0/1" counter, for objectives where the number would give the answer away.</summary>
    public bool HideProgress;

    public QuestObjective(string statKey, int target, string label = null, bool hideProgress = false)
    {
        StatKey = statKey;
        Target = target;
        Label = label;
        HideProgress = hideProgress;
    }

    public int Current => Mathf.Min(PlayerStats.Get(StatKey), Target);
    public bool IsMet => PlayerStats.Get(StatKey) >= Target;

    /// <summary>The stat's short label unless the quest overrode it.</summary>
    public string DisplayLabel =>
        !string.IsNullOrEmpty(Label) ? Label : StatsDatabase.GetShortLabel(StatKey);
}

/// <summary>A stat threshold that has to be true before a quest is even visible.</summary>
[Serializable]
public class UnlockCondition
{
    public string StatKey;
    public int AtLeast;

    public UnlockCondition(string statKey, int atLeast = 1)
    {
        StatKey = statKey;
        AtLeast = atLeast;
    }

    public bool IsMet => PlayerStats.Get(StatKey) >= AtLeast;
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

    public bool GrantsEquipment => !string.IsNullOrEmpty(EquipmentId);

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
        if (GrantsEquipment)
        {
            var catalog = EquipmentCatalog.Instance;
            var def = catalog != null ? catalog.Find(EquipmentId) : null;
            parts.Add(def != null ? def.DisplayName : EquipmentId);
        }
        if (ExtraEquipmentSlot) parts.Add("Another equipment slot");
        if (ExtraSpecialSlot) parts.Add("Another special slot");
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

    public Quest(string id, string name, string description,
                 QuestObjective[] objectives, QuestReward reward,
                 string mapId = "",
                 UnlockCondition[] unlocks = null,
                 UnlockMode unlockMode = UnlockMode.All)
    {
        Id = id;
        Name = name;
        Description = description;
        MapId = mapId ?? "";
        Objectives = objectives ?? new QuestObjective[0];
        Reward = reward ?? new QuestReward();
        Unlocks = unlocks ?? new UnlockCondition[0];
        UnlockMode = unlockMode;
    }

    public bool HasObjectives => Objectives != null && Objectives.Length > 0;

    /// <summary>True once every objective is met. A quest with no objectives is never satisfied.</summary>
    public bool IsSatisfied
    {
        get
        {
            if (!HasObjectives) return false;
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

    public bool WatchesStat(string statKey)
    {
        if (Objectives == null) return false;
        for (int i = 0; i < Objectives.Length; i++)
        {
            if (Objectives[i].StatKey == statKey) return true;
        }
        return false;
    }
}
