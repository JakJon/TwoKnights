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
