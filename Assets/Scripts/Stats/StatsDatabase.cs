using System.Collections.Generic;

public static class StatsDatabase
{
    public static readonly List<StatDefinition> Definitions = new()
    {
        new StatDefinition("kills.rat",   "Rats Killed",   "rats"),
        new StatDefinition("kills.bat",   "Bats Killed",   "bats"),
        new StatDefinition("kills.wolf",  "Wolves Killed", "wolves"),
        new StatDefinition("kills.slime", "Slimes Killed", "slimes"),
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
