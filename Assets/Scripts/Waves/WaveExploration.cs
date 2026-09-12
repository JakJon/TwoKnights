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
///
/// THE DISTINCT COUNT IS COUNTED, NEVER ACCUMULATED. It used to be a running
/// tally that ticked up the first time each wave was seen, which was fine right
/// up until a wave asset was RENAMED: the per-wave key is slugged from the asset
/// name, so the whole tree being swept onto "file name = in-game name" on
/// 2026-09-09 made every wave in an existing save look unseen again, and the
/// tally counted each of them a second time. An explorer quest asking for 32 wave
/// types would then have been handed to a player who had met twenty of them.
///
/// Recounting from the map's own setlist closes that for good. The number is now
/// a fact about the save rather than a memory of how it got there, so it cannot
/// exceed <see cref="TotalWaveTypesFor"/> however the assets are shuffled, and a
/// save that was already inflated corrects itself the next time a wave starts.
/// Orphaned keys from old names simply stop matching anything and are ignored.
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

        PlayerStats.Set("waves.seen." + mapId + "." + Slug(wave.name), 1);
        Recount(mapId);
    }

    /// <summary>Has the player ever started this wave on this map, in any run?</summary>
    public static bool HasSeen(BaseWave wave, string mapId)
    {
        if (wave == null || string.IsNullOrEmpty(mapId)) return false;
        return PlayerStats.Get("waves.seen." + mapId + "." + Slug(wave.name)) > 0;
    }

    /// <summary>
    /// Rewrite the distinct count from what the save actually holds. Cheap enough
    /// to do on every wave start — a map's setlist is a couple of dozen entries —
    /// and PlayerStats.Set already no-ops when the number has not moved, so a
    /// wave the player has met before wakes no quest listener.
    /// </summary>
    private static void Recount(string mapId)
    {
        var waves = WaveTypesFor(mapId);

        // An empty set means the catalog could not be read, not that the player
        // has seen nothing. Writing a zero here would wipe real progress.
        if (waves.Count == 0) return;

        int seen = 0;
        foreach (BaseWave wave in waves)
        {
            if (PlayerStats.Get("waves.seen." + mapId + "." + Slug(wave.name)) > 0) seen++;
        }

        PlayerStats.Set(DistinctStatKey(mapId), seen);
    }

    /// <summary>
    /// Every wave one map can actually play, bosses included. The explorer
    /// finales target this, so adding content raises the bar for anyone who has
    /// not finished it yet and leaves anyone who already did alone — completion
    /// is recorded permanently.
    /// </summary>
    public static int TotalWaveTypesFor(string mapId)
    {
        return WaveTypesFor(mapId).Count;
    }

    private static HashSet<BaseWave> WaveTypesFor(string mapId)
    {
        var seen = new HashSet<BaseWave>();

        var catalog = MapCatalog.Instance;
        if (catalog == null) return seen;

        var map = catalog.Find(mapId);
        if (map == null) return seen;

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
        return seen;
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
