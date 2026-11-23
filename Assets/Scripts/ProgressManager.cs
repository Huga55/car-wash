using UnityEngine;

/// <summary>Local save data. Only the next unlocked level, completed flags and
/// stars are stored; there is no currency, timer or other child-unfriendly system.</summary>
public static class ProgressManager
{
    public static int UnlockedLevel
    {
        get
        {
            int value = PlayerPrefs.GetInt(GameData.UnlockKey, 0);
            if (value < 0) value = 0;
            if (value >= GameData.LevelCount) value = GameData.LevelCount - 1;
            return value;
        }
    }

    public static bool IsUnlocked(int level)
    {
        return level >= 0 && level < GameData.LevelCount && level <= UnlockedLevel;
    }

    public static bool IsCompleted(int level)
    {
        return level >= 0 && level < GameData.LevelCount &&
               PlayerPrefs.GetInt(GameData.CompletedKey(level), 0) == 1;
    }

    public static int Stars(int level)
    {
        if (level < 0 || level >= GameData.LevelCount) return 0;
        return Mathf.Clamp(PlayerPrefs.GetInt(GameData.StarsKey(level), 0), 0, 3);
    }

    public static int CompletedCount()
    {
        int count = 0;
        for (int i = 0; i < GameData.LevelCount; i++) if (IsCompleted(i)) count++;
        return count;
    }

    /// <summary>Records a win. Stars are always full: finishing is the only goal.</summary>
    public static bool RecordWin(int level, int stars)
    {
        if (level < 0 || level >= GameData.LevelCount) return false;

        bool newLevel = !IsCompleted(level);
        PlayerPrefs.SetInt(GameData.CompletedKey(level), 1);
        int best = Mathf.Max(Stars(level), Mathf.Clamp(stars, 1, 3));
        PlayerPrefs.SetInt(GameData.StarsKey(level), best);

        int next = level + 1;
        if (next < GameData.LevelCount && next > UnlockedLevel)
            PlayerPrefs.SetInt(GameData.UnlockKey, next);

        PlayerPrefs.Save();
        return newLevel;
    }

    public static void ResetProgress()
    {
        for (int i = 0; i < GameData.LevelCount; i++)
        {
            PlayerPrefs.DeleteKey(GameData.CompletedKey(i));
            PlayerPrefs.DeleteKey(GameData.StarsKey(i));
        }
        PlayerPrefs.DeleteKey(GameData.UnlockKey);
        PlayerPrefs.Save();
    }
}
