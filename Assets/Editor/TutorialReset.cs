using System.IO;
using UnityEditor;
using UnityEngine;

// Dev convenience for iterating on the tutorial.
//
// The tutorial writes tutorialCompleted the moment it finishes, which is correct
// for players and a nuisance for whoever is tuning it — every playthrough disarms
// the next one. These menu items flip the flag back on a chosen file so the next
// pick of it opens the tutorial again.
//
// It works on the JSON on disk, by slot, rather than through SaveManager.Data:
// Data always follows SaveManager.ActiveSlot, which outside play mode is whatever
// the last domain reload left it at — usually 1. Writing "the active file" is how
// you clear the flag on somebody's real save by accident.
public static class TutorialReset
{
    private const string CompletedTrue = "\"tutorialCompleted\": true";
    private const string CompletedFalse = "\"tutorialCompleted\": false";

    [MenuItem("Tools/Tutorial/Arm for File 1")]
    private static void ArmFile1() => Arm(1);

    [MenuItem("Tools/Tutorial/Arm for File 2")]
    private static void ArmFile2() => Arm(2);

    [MenuItem("Tools/Tutorial/Arm for File 3")]
    private static void ArmFile3() => Arm(3);

    [MenuItem("Tools/Tutorial/Report")]
    private static void Report()
    {
        for (int slot = 1; slot <= SaveManager.SlotCount; slot++)
        {
            string path = PathForSlot(slot);
            if (!File.Exists(path))
            {
                Debug.Log($"[Tutorial] File {slot}: empty — picking it starts the tutorial.");
                continue;
            }

            string json = File.ReadAllText(path);
            bool armed = json.Contains(CompletedFalse);
            Debug.Log($"[Tutorial] File {slot}: {(armed ? "ARMED — picking it starts the tutorial" : "already taught, goes to camp")}");
        }
    }

    private static void Arm(int slot)
    {
        string path = PathForSlot(slot);
        if (!File.Exists(path))
        {
            Debug.Log($"[Tutorial] File {slot} does not exist yet, so it already counts as new — " +
                      "picking it starts the tutorial.");
            return;
        }

        string json = File.ReadAllText(path);
        if (json.Contains(CompletedFalse))
        {
            Debug.Log($"[Tutorial] File {slot} is already armed.");
            return;
        }

        if (!json.Contains(CompletedTrue))
        {
            Debug.LogWarning($"[Tutorial] File {slot} has no tutorialCompleted field to flip — " +
                             "it predates the tutorial and will be migrated on its next load.");
            return;
        }

        File.WriteAllText(path, json.Replace(CompletedTrue, CompletedFalse));

        // The live copy is only stale if this slot happens to be the loaded one
        if (SaveManager.ActiveSlot == slot && SaveManager.IsLoaded)
        {
            SaveManager.Data.tutorialCompleted = false;
        }

        Debug.Log($"[Tutorial] File {slot} armed — picking it in the file select starts the tutorial.");
    }

    // Mirrors SaveManager.PathForSlot, which is private. Slot 1 keeps the original
    // filename; see the comment on SaveManager.FileName for why.
    private static string PathForSlot(int slot)
    {
        string name = slot <= 1 ? "save.json" : $"save_{slot}.json";
        return Path.Combine(Application.persistentDataPath, name);
    }
}
