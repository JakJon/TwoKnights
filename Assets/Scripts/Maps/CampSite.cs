using System.Collections.Generic;
using UnityEngine;

// Where the knights pitch camp: wherever the last run got to. Play the mine and
// the camp is in the mine; reach the deep mine and it is in the deep mine.
//
// The Spawner notes every wave as it starts, and the camp asks for the stage
// that wave belongs to. It is the LAST run that counts, not the best one — a
// file that has been to the deep forest and then dies on wave three of the mine
// camps at the mouth of the mine.
public static class CampSite
{
    /// <summary>
    /// Called as each wave begins. Written to disk only when the answer the camp
    /// would give has changed — a new map, or a new stage of the same one — so a
    /// run does not pay for a save every wave.
    /// </summary>
    public static void NoteWave(MapDefinition map, int waveNumber)
    {
        if (map == null || waveNumber < 1) return;

        var data = SaveManager.Data;
        if (data == null) return;

        bool sameMap = data.lastVentureMapId == map.MapId;
        var before = sameMap ? map.StageForWave(data.lastVentureWave) : null;

        data.lastVentureMapId = map.MapId;
        data.lastVentureWave = waveNumber;

        if (!sameMap || before != map.StageForWave(waveNumber)) SaveManager.Save();
    }

    /// <summary>
    /// The stage the camp should be drawn in. A file that has not started a wave
    /// since this was first recorded camps in the forest — the first map, at its
    /// first stage (owner, 2026-10-13). False only when there is no map with a
    /// backdrop at all.
    /// </summary>
    public static bool Resolve(out MapDefinition map, out MapDefinition.MapStage stage)
    {
        map = null;
        stage = null;

        var catalog = MapCatalog.Instance;
        var data = SaveManager.Data;
        if (catalog == null || data == null) return false;

        int wave = 1;
        map = catalog.Find(data.lastVentureMapId);
        if (map != null) wave = Mathf.Max(1, data.lastVentureWave);
        else map = catalog.FirstUnlocked();
        if (map == null) return false;

        stage = StageWithBackdrop(map, wave);
        return stage != null;
    }

    /// <summary>
    /// Every place a camp could be, in campaign order. The camp's dev key walks
    /// this list so each backdrop can be looked at without playing to it.
    /// </summary>
    public static List<KeyValuePair<MapDefinition, MapDefinition.MapStage>> AllSites()
    {
        var sites = new List<KeyValuePair<MapDefinition, MapDefinition.MapStage>>();
        var catalog = MapCatalog.Instance;
        if (catalog == null) return sites;

        foreach (var candidate in catalog.Maps)
        {
            if (candidate == null) continue;
            Sprite last = null;
            foreach (var candidateStage in candidate.Stages)
            {
                // Two stages that share a backdrop (the deep forest and the
                // moonlit wood) are one place as far as the camp can show
                if (candidateStage == null || candidateStage.backdrop == null || candidateStage.backdrop == last) continue;
                last = candidateStage.backdrop;
                sites.Add(new KeyValuePair<MapDefinition, MapDefinition.MapStage>(candidate, candidateStage));
            }
        }
        return sites;
    }

    // The stage in force at this wave, or the nearest earlier one that names a
    // backdrop. A stage with none means "keep the one already up" in a run, and
    // the camp reads it the same way.
    private static MapDefinition.MapStage StageWithBackdrop(MapDefinition map, int waveNumber)
    {
        MapDefinition.MapStage best = null;
        MapDefinition.MapStage first = null;
        var stages = map.Stages;
        for (int i = 0; i < stages.Count; i++)
        {
            var candidate = stages[i];
            if (candidate == null || candidate.backdrop == null) continue;
            if (first == null || candidate.fromWaveNumber < first.fromWaveNumber) first = candidate;
            if (candidate.fromWaveNumber > waveNumber) continue;
            if (best == null || candidate.fromWaveNumber > best.fromWaveNumber) best = candidate;
        }
        return best ?? first;
    }
}
