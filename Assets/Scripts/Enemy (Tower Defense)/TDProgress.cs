using UnityEngine;

/// <summary>
/// Persists TD stages 1–4 completion in <see cref="PlayerPrefs"/>.
/// </summary>
public static class TDProgress
{
    const string KeyPrefix = "TD_Done_";

    public static bool AllTDComplete =>
        IsComplete(1) && IsComplete(2) && IsComplete(3) && IsComplete(4);

    public static bool IsComplete(int level)
    {
        if (level < 1 || level > 4)
            return false;
        return PlayerPrefs.GetInt(KeyPrefix + level, 0) != 0;
    }

    public static void MarkComplete(int level)
    {
        if (level < 1 || level > 4)
            return;
        PlayerPrefs.SetInt(KeyPrefix + level, 1);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Maps scene names <c>TDLevel1</c> … <c>TDLevel4</c> to level numbers. Other names return false.
    /// </summary>
    public static bool TryGetTdLevelNumberFromSceneName(string sceneName, out int level)
    {
        level = 0;
        if (string.IsNullOrEmpty(sceneName) || !sceneName.StartsWith("TDLevel"))
            return false;
        string suffix = sceneName.Substring("TDLevel".Length);
        if (!int.TryParse(suffix, out int n) || n < 1 || n > 4)
            return false;
        level = n;
        return true;
    }
}
