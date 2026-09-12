using System.Collections.Generic;

public static class StatsDatabase
{
    public static readonly List<StatDefinition> Definitions = new()
    {
        new StatDefinition("kills.rat",     "Rats Killed",      "rats"),
        new StatDefinition("kills.bat",     "Bats Killed",      "bats"),
        new StatDefinition("kills.darkbat", "Dark Bats Killed", "dark bats"),
        new StatDefinition("kills.wolf",    "Wolves Killed",    "wolves"),
        new StatDefinition("kills.slime",   "Slimes Killed",    "slimes"),

        // How something died, rather than what it was
        new StatDefinition("kills.poisoned", "Slain by Venom",  "slain by venom"),
        new StatDefinition("kills.burned",   "Slain by Fire",   "slain by fire"),
        new StatDefinition("kills.blasted",  "Slain by Blast",  "slain by blast"),
        new StatDefinition("kills.frostbitten", "Slain by Cold", "slain by cold"),
        // Named for the upgrade that does it rather than for the abstraction:
        // "executed" is a word the player never sees anywhere else
        new StatDefinition("kills.executed", "Finished by Killing Blow", "finished by Killing Blow"),

        // Status applications count each enemy once, not once per tick
        new StatDefinition("applied.poison", "Enemies Poisoned", "poisoned"),
        new StatDefinition("applied.ignite", "Enemies Set Alight", "set alight"),

        // Kinship groups (see EnemyFamily)
        new StatDefinition("kills.family.vermin", "Vermin Slain", "vermin"),
        new StatDefinition("kills.family.beast",  "Beasts Slain", "beasts"),
        new StatDefinition("kills.family.ooze",   "Oozes Slain",  "oozes"),
        new StatDefinition("kills.family.cart",   "Carts Wrecked", "carts"),

        // Per-map progress. Map-scoped keys are generated from the map id, so
        // these two have to be spelled out rather than derived.
        new StatDefinition("kills.map.camp_fields", "Slain in the Camp Fields", "slain in the wood"),
        new StatDefinition("kills.map.mine",        "Slain in The Mine",        "slain in the mine"),
        new StatDefinition("maps.camp_fields.furthest_wave", "Deepest Wave in the Camp Fields", "waves into the forest"),
        new StatDefinition("maps.mine.furthest_wave",        "Deepest Wave in The Mine",        "waves into the mine"),
        new StatDefinition("waves.distinct.camp_fields", "Forest Wave Types Met", "forest wave types met"),
        new StatDefinition("waves.distinct.mine",        "Mine Wave Types Met",   "mine wave types met"),

        // Order practice, cumulative across runs
        new StatDefinition("upgrades.order.serpent",  "Serpent Upgrades Taken",  "serpent upgrades"),
        new StatDefinition("upgrades.order.shadow",   "Shadow Upgrades Taken",   "shadow upgrades"),
        new StatDefinition("upgrades.order.ember",    "Ember Upgrades Taken",    "ember upgrades"),
        new StatDefinition("upgrades.order.guardian", "Guardian Upgrades Taken", "guardian upgrades"),
        new StatDefinition("upgrades.order.frigid",   "Frigid Upgrades Taken",   "frigid upgrades"),

        // Frigid deeds. Chilled counts each body once in its life rather than once
        // per application, the way the poison and ignite tallies already do.
        new StatDefinition("frigid.chilled",   "Enemies Chilled",   "enemies chilled"),
        new StatDefinition("frigid.frozen",    "Enemies Frozen",    "enemies frozen"),
        new StatDefinition("frigid.shattered", "Enemies Shattered", "enemies shattered"),

        // Guardian deeds. Blocked counts every rock the guard stops, reflected only
        // the ones it sends back, so the pair reads as "how often was the guard in
        // the right place" against "how much of that was paid for".
        new StatDefinition("guardian.blocked",   "Rocks Blocked",   "rocks blocked"),
        new StatDefinition("guardian.reflected", "Rocks Sent Back", "rocks sent back"),
        new StatDefinition("guardian.guided",    "Shots Guided",    "shots guided"),
        new StatDefinition("guardian.shoved",    "Bodies Thrown",   "bodies thrown off the guard"),

        // Dawn deeds. These were published by DawnBoost and PlayerHealth from the
        // day the Order shipped; nothing had ever named them.
        new StatDefinition("upgrades.order.dawn", "Dawn Upgrades Taken", "dawn upgrades"),
        new StatDefinition("dawn.shared_light", "Light Passed On",  "heals passed to the other knight"),
        new StatDefinition("dawn.lifebloom",    "Kills That Healed", "kills that healed"),
        new StatDefinition("dawn.second_wind",  "Second Winds",      "second winds"),
        new StatDefinition("dawn.benediction",  "Benedictions Paid", "benedictions paid"),
        new StatDefinition("dawn.last_light",   "Falls Prevented",   "falls prevented"),
    };

    public static string GetDisplayName(string key)
    {
        foreach (var def in Definitions)
        {
            if (def.Key == key) return def.DisplayName;
        }
        return key;
    }

    public static string GetShortLabel(string key)
    {
        foreach (var def in Definitions)
        {
            if (def.Key == key) return def.ShortLabel;
        }
        return key;
    }
}

public class StatDefinition
{
    public string Key { get; }
    public string DisplayName { get; }
    public string ShortLabel { get; }

    public StatDefinition(string key, string displayName, string shortLabel)
    {
        Key = key;
        DisplayName = displayName;
        ShortLabel = shortLabel;
    }
}
