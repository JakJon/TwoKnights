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

    public static QuestObjective Obj(string statKey, int target, string label = null, bool hideProgress = false)
    {
        return new QuestObjective(statKey, target, label, hideProgress);
    }

    public static QuestObjective[] One(string statKey, int target, string label = null, bool hideProgress = false)
    {
        return new[] { new QuestObjective(statKey, target, label, hideProgress) };
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
                                     string unlocksMapId = null)
    {
        return new QuestReward
        {
            Crystals = crystals,
            EquipmentId = equipmentId,
            ExtraEquipmentSlot = extraSlot,
            ExtraSpecialSlot = extraSpecialSlot,
            UnlocksMapId = unlocksMapId,
        };
    }
}
