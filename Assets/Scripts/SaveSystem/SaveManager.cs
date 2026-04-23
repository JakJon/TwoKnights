using System.IO;
using UnityEngine;

public static class SaveManager
{
    private const string FileName = "save.json";
    private const string TempFileName = "save.json.tmp";

    private static SaveData _data;
    private static bool _loaded;

    public static SaveData Data
    {
        get
        {
            if (!_loaded) Load();
            return _data;
        }
    }

    public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);
    private static string TempPath => Path.Combine(Application.persistentDataPath, TempFileName);

    public static void Load()
    {
        _loaded = true;

        if (!File.Exists(SavePath))
        {
            _data = new SaveData();
            return;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            _data = JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
            Migrate(_data);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SaveManager] Failed to load {SavePath}: {e.Message}. Using defaults.");
            _data = new SaveData();
        }
    }

    public static void Save()
    {
        if (!_loaded) Load();

        try
        {
            string json = JsonUtility.ToJson(_data, true);
            File.WriteAllText(TempPath, json);

            if (File.Exists(SavePath))
            {
                File.Replace(TempPath, SavePath, null);
            }
            else
            {
                File.Move(TempPath, SavePath);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveManager] Failed to save {SavePath}: {e.Message}");
        }
    }

    public static void DeleteSave()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
        if (File.Exists(TempPath)) File.Delete(TempPath);
        _data = new SaveData();
        _loaded = true;
    }

    private static void Migrate(SaveData data)
    {
        // No-op at version 1. Add per-version upgrades here when SaveData changes shape.
    }
}
