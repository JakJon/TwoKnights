using System.Collections.Generic;

/// <summary>
/// Tracks which wave types the player has met, per map.
///
/// Every tier counts as its own type — Choo Choo I and Choo Choo II are
/// different nights out — because that is how the setlist is authored and how a
/// player experiences it.
///
/// Recorded when a wave BEGINS, not when it is cleared: dying to something new
/// is still having seen it, and a quest about exploration should not quietly
/// require you to win.
///
/// Counts are per map rather than global. A wave belongs to exactly one map's
/// setlist, and "you have seen everything the forest has" is a claim about the
/// forest — a single pooled number would let mine progress finish a forest quest.
/// </summary>
public static class WaveExploration
{
    public static string DistinctStatKey(string mapId)
    {
        return "waves.distinct." + mapId;
    }

    public static void RecordSeen(BaseWave wave, string mapId)
    {
        if (wave == null || string.IsNullOrEmpty(mapId)) return;

        string key = "waves.seen." + mapId + "." + Slug(wave.name);
        if (PlayerStats.Get(key) > 0) return;

        PlayerStats.Set(key, 1);
        PlayerStats.Increment(DistinctStatKey(mapId));
    }

    /// <summary>
    /// Every wave one map can actually play, bosses included. The explorer
    /// finales target this, so adding content raises the bar for anyone who has
    /// not finished it yet and leaves anyone who already did alone — completion
    /// is recorded permanently.
    /// </summary>
    public static int TotalWaveTypesFor(string mapId)
    {
        var catalog = MapCatalog.Instance;
        if (catalog == null) return 0;

        var map = catalog.Find(mapId);
        if (map == null) return 0;

        var seen = new HashSet<BaseWave>();
        var waves = map.Waves;
        if (waves != null)
        {
            for (int i = 0; i < waves.Count; i++)
            {
                if (waves[i] != null) seen.Add(waves[i]);
            }
        }
        if (map.GateBoss != null) seen.Add(map.GateBoss);
        if (map.TrueBoss != null) seen.Add(map.TrueBoss);
        return seen.Count;
    }

    private static string Slug(string assetName)
    {
        if (string.IsNullOrEmpty(assetName)) return "unknown";
        var sb = new System.Text.StringBuilder(assetName.Length);
        foreach (char c in assetName)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            else if (c == ' ' || c == '-' || c == '_') sb.Append('_');
        }
        return sb.ToString();
    }
}
