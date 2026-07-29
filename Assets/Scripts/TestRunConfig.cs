#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;

// Dev-only Test Mode: carries a run setup (starting wave + pre-picked upgrades)
// from the Camp scene's Test Mode panel into the game scene, where the Spawner
// consumes it once on run start. Compiled out of release builds entirely.
public static class TestRunConfig
{
    public static bool Pending { get; private set; }
    public static int StartWave { get; private set; } = 1;
    public static List<BaseUpgrade> LeftUpgrades { get; } = new List<BaseUpgrade>();
    public static List<BaseUpgrade> RightUpgrades { get; } = new List<BaseUpgrade>();

    // True while the run that consumed a test config is in progress; used to
    // keep test runs from writing furthest-wave progress into the save.
    public static bool ActiveRun { get; set; }

    // One-shot: the death screen's "Try Wave Again" sets this so the first
    // picker moment of the rebuilt run forces the exact wave that killed you.
    public static BaseWave RetryWave;

    // The ground the test run plays on, honoured by MapSelection.Resolve() while
    // a config is Pending. Kept out of MapSelection/the save so a dev detour
    // never rewrites the level select's remembered pick, and — like AutoPickWave
    // — not cleared by Clear(), so "Try Wave Again" re-fights on the same map.
    // Null = fall back to the normal level-select chain.
    public static MapDefinition Map;

    // Automation hook for the per-wave picker (MCP-driven validation must not
    // stall on UI): null/empty = show the picker; "*" = weighted random with no
    // UI; anything else = the candidate wave asset with that name, falling back
    // to weighted random. Not cleared by Clear() — it steers the whole run.
    public static string AutoPickWave;

    public static void Set(int startWave, IEnumerable<BaseUpgrade> left, IEnumerable<BaseUpgrade> right)
    {
        StartWave = startWave < 1 ? 1 : startWave;
        LeftUpgrades.Clear();
        RightUpgrades.Clear();
        if (left != null) LeftUpgrades.AddRange(left);
        if (right != null) RightUpgrades.AddRange(right);
        Pending = true;
    }

    public static void Clear()
    {
        Pending = false;
        LeftUpgrades.Clear();
        RightUpgrades.Clear();
    }
}
#endif
