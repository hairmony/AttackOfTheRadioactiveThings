using System.IO;
using UnityEngine;

/// <summary>
/// Persists <see cref="GameSaveData"/> as JSON under persistent data path.
/// Settings are merged into an existing file so future gameplay fields are preserved.
/// </summary>
public static class GameSave
{
    private const string FileName = "gamesave.json";

    private static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

    public static bool TryLoad(out GameSaveData data)
    {
        data = null;
        if (!File.Exists(SavePath))
            return false;
        try
        {
            string json = File.ReadAllText(SavePath);
            data = JsonUtility.FromJson<GameSaveData>(json);
            return data != null;
        }
        catch
        {
            return false;
        }
    }

    public static void Write(GameSaveData data)
    {
        if (data == null)
            data = new GameSaveData();
        if (data.settings == null)
            data.settings = new SettingsSaveData();
        File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
    }

    /// <summary>Writes current in-memory <see cref="GameSettings"/> into the save file.</summary>
    public static void PersistSettingsFromRuntime()
    {
        GameSettings.EnsureLoaded();
        GameSaveData data;
        TryLoad(out data);
        if (data == null)
            data = new GameSaveData();
        if (data.settings == null)
            data.settings = new SettingsSaveData();
        GameSettings.CopyRuntimeStateTo(data.settings);
        Write(data);
    }
}
