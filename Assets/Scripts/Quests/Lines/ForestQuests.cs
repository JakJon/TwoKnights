using System.Collections.Generic;
using UnityEngine;
using static QuestBuild;

/// <summary>
/// The Camp Fields. The King carries the story of the map — the stolen crown, the
/// push deeper, the thing at the end of it — and the Cartographer carries the work
/// that happens alongside it. Beating the Rat King splits the map into lines that
/// run in parallel.
/// </summary>
public static class ForestQuests
{
    public const string Ruckus = "ruckus_in_the_wood";
    public const string Cleanup1 = "camp_cleanup_1";
    public const string Cleanup2 = "camp_cleanup_2";
    public const string DeepWoods = "into_the_deep_woods";
    public const string CrimsonTwins = "the_crimson_twins";
    public const string WreckingRatKing = "wrecking_rat_king";
    public const string CrushingCrimson = "crushing_crimson";
    public const string Explorer1 = "forest_ranger";
    public const string Explorer2 = "one_with_the_woods";
    public const string TargetPractice = "target_practice";

    // Depth is one measure everywhere: the furthest wave reached on the map.
    public const string DepthStat = "maps.camp_fields.furthest_wave";

    /// <summary>
    /// How deep the TUTORIAL run got, kept apart from the lifetime mark by
    /// WaveManager. Only "A Ruckus in the Wood" reads it, because it is the only
    /// quest awake during that run — every other depth gate must be earned on a
    /// run of the player's own.
    /// </summary>
    public const string TutorialDepthStat = "maps.camp_fields.tutorial_wave";

    // The first explorer quest asks for a flat 40 wave types (owner, 2026-10-05);
    // the second is still a share of however many exist - see ExplorePercent.
    private static string ExploreStat => WaveExploration.DistinctStatKey(Forest);

    public static IEnumerable<Quest> All()
    {
        // ---- the King's line ----

        yield return new Quest(
            id: Ruckus,
            name: "A Ruckus in the Wood",
            description:
                "The forest wildlife has been growing more bold these days. One of the creatures managed "
                + "to steal the king's crown. Venture deeper into the forest, and discover where the crown "
                + "has wound up.",
            mapId: Forest,
            objectives: One("maps.camp_fields.gate_cleared", 1,
                            menu: "Venture further into the forest"),
            reward: Reward(equipmentId: "gnawed_crown", unlocksMapId: Mine),
            // Either counter will do. The tutorial run is where this normally
            // opens, and it keeps its depth separately; the lifetime mark is the
            // fallback for a player who died before wave three and came back.
            unlocks: Gate(Stat(DepthStat, 3), Stat(TutorialDepthStat, 3)),
            unlockMode: UnlockMode.Any,
            cast: King,
            liveDuringTutorialRun: true,
            offer:
                "<slow>Ahh there you are.<slow>\n"
                + "It's worse out here than I had expected.\n"
                + "One of the vermin managed to steal my crown whilst I was being ambushed.\n"
                + "Your first job as knights is to return it back to me at once.\n"
                + "We'll talk more once it is retrieved.",
            completion:
                "<slow>My crown...<slow> <fast>Very good!<fast> <crown>\n"
                + "I must say, you have impressed me by bringing this back.\n"
                + "There were more creatures in this forest than I'd expected.\n"
                + "There is more work to be done in the forest, however I have gotten word that there are "
                + "also disturbances in the mine.\n"
                + "Let's retreat to camp for now, before you choose which trouble to pursue.");

        yield return new Quest(
            id: DeepWoods,
            name: "Into the Deep Woods",
            description:
                "There is more to do in the forest according to the king. We should return, and venture "
                + "deeper than before.",
            mapId: Forest,
            objectives: One(DepthStat, 12,
                            menu: "Best attempt: {count} waves into the forest",
                            npc: "Reach wave {target} in the forest"),
            reward: Reward(crystals: 2),
            unlocks: Gate(After(Ruckus)),
            cast: King,
            offer:
                "",
            completion:
                "Thank you for returning to these woods.\n"
                + "You may have noticed there are more creatures the deeper you venture.\n"
                + "Two of our scouts reported a foreboding monster up ahead.\n"
                + "They said the creatures glowed like magma, and they were barely able to retreat in time.\n"
                + "You will need a way to slow them down, or dispose of them quickly.\n"
                + "Take these crystals as help along your way.");

        yield return new Quest(
            id: CrimsonTwins,
            name: "The Crimson Twins",
            description:
                "The king has told us about a glowing pair of monsters deep in the forest. We must take "
                + "care of the beasts.",
            mapId: Forest,
            objectives: One("maps.camp_fields.true_cleared", 1,
                            menu: "Defeat the glowing duo deep in the forest"),
            reward: Reward(extraSlot: true),
            unlocks: Gate(After(DeepWoods), Stat(DepthStat, 11)),
            cast: King,
            offer:
                "",
            completion:
                "<slow>Truly remarkable!<slow> You should be proud to have made it this far. You are the "
                + "first two to have done so.\n"
                + "Now that the crimson beasts have been slain, we should move our efforts elsewhere.\n"
                + "Unfortunately there is no time to rest.\n"
                + "This should help though, I've given you another equipment slot to help you along your "
                + "way.\n"
                + "Stay safe.");


        // ---- the Cartographer's line ----

        yield return new Quest(
            id: Cleanup1,
            name: "Camp Cleanup",
            description:
                "The cartographer needs help thinning out the creatures of the forest so that he can "
                + "complete his expedition.",
            mapId: Forest,
            objectives: One("kills.map.camp_fields", 500,
                            menu: "{count} slain in the wood",
                            npc: "Slay {target} monsters in the forest"),
            reward: Reward(crystals: 1),
            unlocks: Gate(Stat(DepthStat, 5)),
            cast: Mapmaker,
            offer:
                "Hello! I'm a cartographer, in the process of mapping this forest.\n"
                + "Lately my work has become more difficult with the increase in creatures about. Please, "
                + "continue ridding the forest of more creatures so I can complete my assignment. I'll make "
                + "it worth your time!",
            completion:
                "Your work out here has been helpful. But there are many more still in my way. Take this "
                + "as my thanks. I have more to offer if you continue to help!");

        yield return new Quest(
            id: Cleanup2,
            name: "Camp Cleanup II",
            description:
                "The cartographer needs even more help thinning out the creatures of the forest so that "
                + "he can complete his expedition.",
            mapId: Forest,
            objectives: One("kills.map.camp_fields", 2000,
                            menu: "{count} slain in the wood",
                            npc: "Slay {target} monsters in the forest"),
            reward: Reward(crystals: 3),
            unlocks: Gate(After(Cleanup1)),
            cast: Mapmaker,
            offer:
                "",
            completion:
                "Wow! Your work out here has been a tremendous help!\n"
                + "I've managed to explore the main areas of the forest.\n"
                + "Take these crystals as a sign of my gratitude!");

        yield return new Quest(
            id: WreckingRatKing,
            name: "Wrecking Rat King",
            description:
                "The cartographer wants us to defeat the rat king without any equipment, or specials "
                + "equipped for some reason... NOTE: Make sure your special slots are empty.",
            mapId: Forest,
            objectives: One(Feats.RatKingBare, 1,
                            menu: "Defeat the Rat King with no equipment or special equipped"),
            reward: Reward(crystals: 2),
            // The third Rat King kill ever, with no story gate (owner, 2026-10-05).
            // It used to wait for The Crimson Twins and then three MORE kills.
            unlocks: Gate(Stat("kills.ratking", 3)),
            cast: Mapmaker,
            offer:
                "Wow! You sure have gotten good at getting past him. I've been studying the Rat King's "
                + "movements.\n"
                + "I wonder how the battle would go if you didn't use your special abilities or equipment?\n"
                + "What? Pointless? No! This is extremely valuable research!\n"
                + "<slow>And...<slow> I'll give you more rewards.",
            completion:
                "That was thrilling! I managed to get some valuable notes.\n"
                + "I wonder if the same feat could be managed on other creatures...");

        yield return new Quest(
            id: CrushingCrimson,
            name: "Crushing Crimson",
            description:
                "The cartographer wants us to defeat the crimson twins now without any equipment, or "
                + "specials equipped... NOTE: Make sure your special slots are empty.",
            mapId: Forest,
            objectives: One(Feats.TwinsBare, 1,
                            menu: "Defeat the Crimson Twins with no equipment or special equipped"),
            reward: Reward(crystals: 4),
            unlocks: Gate(After(WreckingRatKing)),
            cast: Mapmaker,
            offer:
                "",
            completion:
                "AMAZING!! WOW! THAT WAS -\n"
                + "<slow>Oh... sorry.<slow> I should compose myself.. That was a splendid display of skill.\n"
                + "Please take these, you have earned them.");

        yield return new Quest(
            id: Explorer1,
            name: "Forest ranger",
            description:
                "Explore more routes within the forest.",
            mapId: Forest,
            objectives: One(ExploreStat, 40,
                            menu: "{count} Forest Waves Discovered",
                            npc: "Discover {target} different forest waves",
                            lifetime: true),
            reward: Reward(crystals: 2),
            unlocks: Gate(Stat(ExploreStat, ExplorePercent(33))),
            cast: Mapmaker,
            offer:
                "Wow! You've managed to find some branches of the forest that even I haven't discovered\n"
                + "If you continue to explore the forest and report your findings to me, I'll be sure to "
                + "reward you.",
            completion:
                "You're making good progress on your exploration of these woods.\n"
                + "Please continue finding new paths!");

        yield return new Quest(
            id: Explorer2,
            name: "One with the woods",
            description:
                "Discover every route within the forest.",
            mapId: Forest,
            objectives: One(ExploreStat, ExplorePercent(75),
                            menu: "{count} Forest Waves Discovered",
                            npc: "Discover {target} different forest waves",
                            lifetime: true),
            reward: Reward(equipmentId: "wolfsbane_pendant"),
            unlocks: Gate(After(Explorer1)),
            cast: Mapmaker,
            offer:
                "",
            completion:
                "Wow! I've done it! <slow>Or..<slow> We've done it! We have documented the entirety of "
                + "the forest.\n"
                + "I've been wearing this as a form of protection, but I want you to have it now.\n"
                + "Thank you for your help in this undertaking!");

        // Offered after the first time any target range pattern is cleared, and
        // measured over the whole file so that clear counts — see TargetRange.
        yield return new Quest(
            id: TargetPractice,
            name: "Target Practice",
            description:
                "There are targets set up all throughout the forest. Let's continue to practice our aim "
                + "with any of them that we see.",
            mapId: Forest,
            objectives: One(TargetRange.PatternsStat, TargetRange.Patterns.Length,
                            menu: "Complete all 5 different target range patterns successfully. {current} / {target}",
                            npc: "Complete all the target ranges successfully",
                            lifetime: true),
            reward: Reward(crystals: 3),
            unlocks: Gate(Stat(TargetRange.PatternsStat, 1)),
            cast: Mapmaker,
            offer:
                "These targets are set up all throughout the woods... No one knows where they came from.\n"
                + "I guess they'll serve as good target practice for you during your excursions!",
            completion:
                "Amazing! Your aim has gotten dialed in!\n"
                + "These crystals fell right off the last target you broke, I think you've earned them.");
    }

    /// <summary>
    /// A share of every playable wave in the forest, rounded up. The explorer
    /// quests are written as proportions rather than counts so that adding waves
    /// moves the goalposts with the content instead of quietly making an old
    /// target easier.
    ///
    /// Falls back to an unreachable number if the catalog cannot be read — a
    /// target of zero would hand the quest out the moment it became visible.
    /// </summary>
    private static int ExplorePercent(int percent)
    {
        int total = WaveExploration.TotalWaveTypesFor(Forest);
        if (total > 0) return Mathf.Max(1, Mathf.CeilToInt(total * (percent / 100f)));
        Debug.LogWarning("[ForestQuests] Wave catalog unreadable; explorer targets left unreachable.");
        return int.MaxValue;
    }
}
