using System;
using UnityEngine;

namespace LittleGarage
{
    /// <summary>
    /// JSON persistence for ProgressData using PlayerPrefs. Robust to corrupt data and
    /// versioned so future migrations have a stable key.
    /// </summary>
    public sealed class SaveSystem
    {
        private const string Key = "LittleGarage.Progress.v1";

        public void Save(ProgressData data)
        {
            if (data == null) data = new ProgressData();
            data.version = Mathf.Max(1, data.version);
            try
            {
                string json = JsonUtility.ToJson(data);
                PlayerPrefs.SetString(Key, json ?? string.Empty);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SaveSystem] Save failed: " + ex.Message);
            }
        }

        public ProgressData Load()
        {
            try
            {
                if (!PlayerPrefs.HasKey(Key))
                    return CreateDefault();

                string json = PlayerPrefs.GetString(Key, string.Empty);
                if (string.IsNullOrEmpty(json))
                    return CreateDefault();

                ProgressData data = JsonUtility.FromJson<ProgressData>(json);
                return Sanitize(data);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SaveSystem] Load failed, using defaults: " + ex.Message);
                return CreateDefault();
            }
        }

        public void Clear()
        {
            try
            {
                if (PlayerPrefs.HasKey(Key))
                    PlayerPrefs.DeleteKey(Key);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SaveSystem] Clear failed: " + ex.Message);
            }
        }

        private static ProgressData CreateDefault()
        {
            return Sanitize(new ProgressData());
        }

        private static ProgressData Sanitize(ProgressData data)
        {
            if (data == null) data = new ProgressData();
            if (data.version < 1) data.version = 1;
            if (data.stars < 0) data.stars = 0;
            if (data.completedJobs == null) data.completedJobs = new string[0];
            if (data.appearances == null) data.appearances = new AppearanceSave[0];

            int validJobs = 0;
            for (int i = 0; i < data.completedJobs.Length; i++)
                if (!string.IsNullOrEmpty(data.completedJobs[i])) validJobs++;
            if (validJobs != data.completedJobs.Length)
            {
                string[] cleaned = new string[validJobs];
                int w = 0;
                for (int i = 0; i < data.completedJobs.Length; i++)
                {
                    string id = data.completedJobs[i];
                    if (!string.IsNullOrEmpty(id)) cleaned[w++] = id;
                }
                data.completedJobs = cleaned;
            }

            int validLooks = 0;
            for (int i = 0; i < data.appearances.Length; i++)
            {
                AppearanceSave a = data.appearances[i];
                if (a != null && !string.IsNullOrEmpty(a.vehicleId)) validLooks++;
            }
            if (validLooks != data.appearances.Length)
            {
                AppearanceSave[] cleaned = new AppearanceSave[validLooks];
                int w = 0;
                for (int i = 0; i < data.appearances.Length; i++)
                {
                    AppearanceSave a = data.appearances[i];
                    if (a != null && !string.IsNullOrEmpty(a.vehicleId)) cleaned[w++] = a;
                }
                data.appearances = cleaned;
            }

            data.musicVolume = Mathf.Clamp01(data.musicVolume);
            data.sfxVolume = Mathf.Clamp01(data.sfxVolume);
            return data;
        }
    }
}
