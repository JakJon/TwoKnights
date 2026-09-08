using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Decides whether a file still owes its owner the tutorial, and routes it there.
///
/// A brand-new file does not pass through the camp at all: the first thing anyone
/// who has never played sees is the arena with one knight standing in it. The camp
/// — shop, quests, equipment, level select — is a hub for a player who has
/// something to spend and somewhere to go, and it is meaningless before the first
/// run. So the file select sends a fresh file straight to Main, the tutorial hands
/// off into wave one when it is done, and camp is what that run ends in.
/// </summary>
public static class TutorialRun
{
    // The tutorial is always the forest, whatever the catalog's first unlocked map
    // happens to be later on
    private const string ForestMapId = "camp_fields";

    private const string GameSceneName = "Main";

    /// <summary>
    /// Set by <see cref="TryBegin"/> and taken by the Spawner on the other side of
    /// the scene load. Statics outlive a scene change, which is exactly why this
    /// works — and why it has to be reset per app run.
    /// </summary>
    public static bool Pending { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetForNewSession()
    {
        Pending = false;
    }

    /// <summary>
    /// Sends the active file into the tutorial if it has never finished one.
    /// Returns true when it has taken over — the caller must do nothing further,
    /// a scene load is already under way.
    /// </summary>
    public static bool TryBegin()
    {
        if (SaveManager.Data.tutorialCompleted)
        {
            // Loud on purpose. "The tutorial did not start" and "the file select
            // did nothing" look identical from the outside, and the difference is
            // one boolean nobody can see.
            Debug.Log($"[Tutorial] File {SaveManager.ActiveSlot} has already been taught " +
                      "(tutorialCompleted = true); going to the camp as usual.");
            return false;
        }

        Debug.Log($"[Tutorial] File {SaveManager.ActiveSlot} is new — starting the tutorial.");

        var catalog = MapCatalog.Instance;
        var forest = catalog != null ? catalog.Find(ForestMapId) : null;
        if (forest != null)
        {
            MapSelection.Select(forest);
        }
        else
        {
            // Not fatal — MapSelection falls back to the first unlocked map, which
            // on a new file is the forest anyway. Worth saying out loud, though,
            // because it means the map id moved.
            Debug.LogWarning($"[Tutorial] No map '{ForestMapId}' in the catalog; " +
                             "the run falls back to whatever MapSelection resolves.");
        }

        Pending = true;
        Time.timeScale = 1f;

        if (GameSceneManager.Instance != null) GameSceneManager.Instance.LoadGameScene();
        else SceneManager.LoadScene(GameSceneName);

        return true;
    }

    /// <summary>Takes the pending flag. True at most once per <see cref="TryBegin"/>.</summary>
    public static bool Consume()
    {
        if (!Pending) return false;
        Pending = false;
        return true;
    }

    /// <summary>
    /// Written only when the tutorial actually reaches the end. Quitting or
    /// closing the game part-way leaves the flag down, so the file gets the
    /// tutorial again next time it is opened rather than being dropped into a
    /// game it was halfway through being taught.
    /// </summary>
    public static void MarkComplete()
    {
        if (SaveManager.Data.tutorialCompleted) return;
        SaveManager.Data.tutorialCompleted = true;
        SaveManager.Save();
    }
}
