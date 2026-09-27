/// <summary>
/// Terse constructors for authoring quest lines. The line files read as data,
/// so the noise of `new QuestObjective(...)` on every row is worth removing —
/// `using static QuestBuild;` at the top of each and the content stays legible.
/// </summary>
public static class QuestBuild
{
    public const string Forest = "camp_fields";
    public const string Mine = "mine";
    public const string Keep = "pallid_keep";
    /// <summary>Quests that belong to no map — Order lines, camp business.</summary>
    public const string Camp = "";

    /// <summary>
    /// <paramref name="lifetime"/> measures against the player's whole history
    /// instead of from the moment the quest opened. Almost nothing wants it — see
    /// QuestObjective.Lifetime.
    /// </summary>
    public static QuestObjective Obj(string statKey, int target, string label = null,
                                     bool hideProgress = false, bool lifetime = false,
                                     bool countAfter = false)
    {
        return new QuestObjective(statKey, target, label, hideProgress, lifetime, countAfter);
    }

    public static QuestObjective[] One(string statKey, int target, string label = null,
                                       bool hideProgress = false, bool lifetime = false,
                                       bool countAfter = false)
    {
        return new[] { new QuestObjective(statKey, target, label, hideProgress, lifetime, countAfter) };
    }

    /// <summary>Gate on another quest being finished — the standard link in a chain.</summary>
    public static UnlockCondition After(string questId)
    {
        return new UnlockCondition(QuestDatabase.CompletionStatKey(questId), 1);
    }

    public static UnlockCondition Stat(string statKey, int atLeast = 1)
    {
        return new UnlockCondition(statKey, atLeast);
    }

    public static UnlockCondition[] Gate(params UnlockCondition[] conditions)
    {
        return conditions;
    }

    public static QuestReward Reward(int crystals = 0, string equipmentId = null,
                                     bool extraSlot = false, bool extraSpecialSlot = false,
                                     string unlocksMapId = null, string upgradeSlug = null)
    {
        return new QuestReward
        {
            Crystals = crystals,
            EquipmentId = equipmentId,
            ExtraEquipmentSlot = extraSlot,
            ExtraSpecialSlot = extraSpecialSlot,
            UnlocksMapId = unlocksMapId,
            UpgradeSlug = upgradeSlug,
        };
    }

    /// <summary>
    /// A threshold counted FROM the moment another quest finished, rather than
    /// from the start of the file. "Three more Rat Kings, after the Crimson Twins."
    /// </summary>
    public static UnlockCondition Since(string questId, string statKey, int atLeast)
    {
        return new UnlockCondition(statKey, atLeast, questId);
    }

    // ---- who turns up ----

    public static QuestCast Cast(NpcId npc, UpgradeOrder accent = UpgradeOrder.Neutral)
    {
        return new QuestCast(npc, accent);
    }

    /// <summary>Both Orders attend — the combination quests, and only those.</summary>
    public static QuestCast Pair(NpcId first, UpgradeOrder firstAccent,
                                 NpcId second, UpgradeOrder secondAccent)
    {
        return new QuestCast(first, firstAccent, second, secondAccent);
    }

    public static readonly QuestCast King = Cast(NpcId.King);
    public static readonly QuestCast Mapmaker = Cast(NpcId.Cartographer);
}
