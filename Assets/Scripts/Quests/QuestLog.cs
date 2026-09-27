using System.Collections.Generic;

/// <summary>
/// How the quest log is arranged.
///
/// Two different questions deserve two different shelves. Map quests answer "what
/// is left to do out there", so they group by PLACE — the King's story and the
/// Cartographer's errands sit together under the map they both happen on, because
/// from the player's side they are one list of reasons to go back to the forest.
///
/// Order quests answer "what am I becoming", so they group by WHO — the ninja's two
/// Orders under the ninja, the wizard's two under the wizard — with a subheading per
/// Order inside, since a knight practising Serpent has no business reading the
/// Shadow line as though it were the same chain.
///
/// Built here rather than in QuestPanel so the pause menu's copy of the log and the
/// camp's copy cannot disagree about the shape of it.
/// </summary>
public static class QuestLog
{
    public class Section
    {
        /// <summary>Stable key for collapse state. Never shown.</summary>
        public string Id;
        public string Name;
        public readonly List<Subsection> Subsections = new List<Subsection>();

        public int LiveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Subsections.Count; i++) n += Subsections[i].Quests.Count;
                return n;
            }
        }
    }

    public class Subsection
    {
        /// <summary>Null for a section that needs no subheading — the maps.</summary>
        public string Name;
        public readonly List<Quest> Quests = new List<Quest>();
    }

    /// <summary>The NPC order the log reads in. Maps first: they are where a run happens.</summary>
    private static readonly NpcId[] OrderCarriers =
    {
        NpcId.Ninja, NpcId.Wizard, NpcId.Paladin,
    };

    private static readonly UpgradeOrder[] OrdersFor_Ninja = { UpgradeOrder.Serpent, UpgradeOrder.Shadow };
    private static readonly UpgradeOrder[] OrdersFor_Wizard = { UpgradeOrder.Ember, UpgradeOrder.Frigid };
    private static readonly UpgradeOrder[] OrdersFor_Paladin = { UpgradeOrder.Guardian, UpgradeOrder.Dawn };

    /// <summary>
    /// Every section with at least one UNFINISHED quest visible, in reading order.
    /// Completed quests are collected separately by the caller — they all fold under
    /// one shelf at the foot of the log rather than one per section.
    /// </summary>
    public static List<Section> Build(List<Quest> finishedOut)
    {
        var sections = new List<Section>();

        // ---- the maps, by place ----
        foreach (var mapId in QuestDatabase.MapGroups)
        {
            var section = new Section { Id = "map:" + mapId, Name = MapName(mapId) };
            var only = new Subsection();
            foreach (var quest in QuestProgress.Visible)
            {
                if (quest.MapId != mapId) continue;
                if (QuestProgress.IsCompleted(quest.Id)) { finishedOut?.Add(quest); continue; }
                only.Quests.Add(quest);
            }
            if (only.Quests.Count > 0)
            {
                section.Subsections.Add(only);
                sections.Add(section);
            }
        }

        // ---- the Orders, by who brings them ----
        foreach (var npc in OrderCarriers)
        {
            var section = new Section { Id = "npc:" + npc, Name = NpcName(npc) };

            foreach (var order in OrdersFor(npc))
            {
                var sub = new Subsection { Name = "Order of the " + order };
                foreach (var quest in QuestProgress.Visible)
                {
                    if (!string.IsNullOrEmpty(quest.MapId)) continue;     // a map quest, handled above
                    if (quest.Cast.Primary != npc) continue;
                    if (quest.Cast.PrimaryAccent != order) continue;
                    if (QuestProgress.IsCompleted(quest.Id)) { finishedOut?.Add(quest); continue; }
                    sub.Quests.Add(quest);
                }
                if (sub.Quests.Count > 0) section.Subsections.Add(sub);
            }

            if (section.Subsections.Count > 0) sections.Add(section);
        }

        // ---- anything the rules above did not claim ----
        // The combination quests live here: their cast is a pair, so they belong to
        // no single NPC's shelf. Also the safety net for a quest authored with no
        // cast at all, which would otherwise be invisible rather than merely odd.
        var loose = new Section { Id = "other", Name = "Between Orders" };
        var leftovers = new Subsection();
        foreach (var quest in QuestProgress.Visible)
        {
            if (!string.IsNullOrEmpty(quest.MapId)) continue;
            if (ClaimedByAnOrderShelf(quest)) continue;
            if (QuestProgress.IsCompleted(quest.Id)) { finishedOut?.Add(quest); continue; }
            leftovers.Quests.Add(quest);
        }
        if (leftovers.Quests.Count > 0)
        {
            loose.Subsections.Add(leftovers);
            sections.Add(loose);
        }

        return sections;
    }

    private static bool ClaimedByAnOrderShelf(Quest quest)
    {
        // A pair belongs to neither of its two, by design.
        if (quest.Cast.IsPair) return false;
        for (int i = 0; i < OrderCarriers.Length; i++)
        {
            if (quest.Cast.Primary != OrderCarriers[i]) continue;
            foreach (var order in OrdersFor(OrderCarriers[i]))
            {
                if (quest.Cast.PrimaryAccent == order) return true;
            }
        }
        return false;
    }

    private static UpgradeOrder[] OrdersFor(NpcId npc)
    {
        switch (npc)
        {
            case NpcId.Ninja: return OrdersFor_Ninja;
            case NpcId.Wizard: return OrdersFor_Wizard;
            case NpcId.Paladin: return OrdersFor_Paladin;
            default: return new UpgradeOrder[0];
        }
    }

    public static string NpcName(NpcId npc)
    {
        switch (npc)
        {
            case NpcId.King: return "The King";
            case NpcId.Cartographer: return "The Cartographer";
            case NpcId.Ninja: return "The Assassin";
            case NpcId.Wizard: return "The Wizard";
            case NpcId.Paladin: return "The Paladin";
            default: return "";
        }
    }

    private static string MapName(string mapId)
    {
        var catalog = MapCatalog.Instance;
        var map = catalog != null ? catalog.Find(mapId) : null;
        if (map != null && !string.IsNullOrEmpty(map.DisplayName)) return map.DisplayName;
        return string.IsNullOrEmpty(mapId) ? "The Camp" : mapId;
    }
}
