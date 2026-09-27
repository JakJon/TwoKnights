/// <summary>
/// The five people who bring quests to the knights. One per line of business:
/// the King carries the main story, the Cartographer the rest of the maps, and
/// one NPC per pair of Orders.
///
/// None is the absence of a cast member — a quest with no NPC plays no scene,
/// which is what the combination quests use before their prose is written.
/// </summary>
public enum NpcId
{
    None = 0,
    King = 1,
    Cartographer = 2,
    Ninja = 3,
    Wizard = 4,
    Paladin = 5,
}

/// <summary>
/// Who turns up for a quest, and in what colour.
///
/// Two slots because the combination quests are the meeting of two Orders and
/// both NPCs attend. <see cref="Accent"/> is the Order the scene is dressed as:
/// the Wizard trails fire for an Ember quest and frost for a Frigid one, and the
/// Paladin's glow takes the Order's own tint. It is not derivable from the NPC —
/// the Wizard and the Paladin each carry two Orders — so the line file states it.
/// </summary>
[System.Serializable]
public struct QuestCast
{
    public NpcId Primary;
    public UpgradeOrder PrimaryAccent;
    /// <summary>None for all but the combination quests.</summary>
    public NpcId Secondary;
    public UpgradeOrder SecondaryAccent;

    public bool HasCast => Primary != NpcId.None;
    public bool IsPair => Secondary != NpcId.None;

    public QuestCast(NpcId primary, UpgradeOrder primaryAccent,
                     NpcId secondary = NpcId.None,
                     UpgradeOrder secondaryAccent = UpgradeOrder.Neutral)
    {
        Primary = primary;
        PrimaryAccent = primaryAccent;
        Secondary = secondary;
        SecondaryAccent = secondaryAccent;
    }
}
