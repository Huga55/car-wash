using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleGarage
{
    /// <summary>
    /// Tracks job completion, sequential unlocks, rewards/stars and per-vehicle appearance.
    /// </summary>
    public sealed class ProgressManager
    {
        private readonly SaveSystem _save;
        private readonly ProgressData _data;
        private readonly HashSet<string> _completed;
        private readonly Dictionary<string, VehicleAppearance> _appearances;
        private readonly List<string> _jobOrder;
        private readonly Dictionary<string, string> _jobToVehicle;

        public int CompletedJobs { get { return _completed.Count; } }
        public int Stars { get { return _data.stars; } }
        public float MusicVolume { get { return _data.musicVolume; } set { _data.musicVolume = Mathf.Clamp01(value); _save.Save(_data); } }
        public float SfxVolume { get { return _data.sfxVolume; } set { _data.sfxVolume = Mathf.Clamp01(value); _save.Save(_data); } }

        public ProgressManager() : this(null, null) { }

        public ProgressManager(GameConfig config, SaveSystem saveSystem = null)
        {
            _save = saveSystem ?? new SaveSystem();
            _data = _save.Load() ?? new ProgressData();
            _completed = new HashSet<string>(StringComparer.Ordinal);
            _appearances = new Dictionary<string, VehicleAppearance>(StringComparer.Ordinal);
            _jobOrder = new List<string>();
            _jobToVehicle = new Dictionary<string, string>(StringComparer.Ordinal);

            if (config != null && config.jobs != null)
            {
                for (int i = 0; i < config.jobs.Length; i++)
                {
                    JobDef job = config.jobs[i];
                    if (job == null || string.IsNullOrEmpty(job.id)) continue;
                    _jobOrder.Add(job.id);
                    if (!string.IsNullOrEmpty(job.vehicleId))
                        _jobToVehicle[job.id] = job.vehicleId;
                }
            }

            if (_data.completedJobs != null)
            {
                for (int i = 0; i < _data.completedJobs.Length; i++)
                {
                    string id = _data.completedJobs[i];
                    if (!string.IsNullOrEmpty(id)) _completed.Add(id);
                }
            }

            if (_data.appearances != null)
            {
                for (int i = 0; i < _data.appearances.Length; i++)
                {
                    AppearanceSave save = _data.appearances[i];
                    if (save != null && !string.IsNullOrEmpty(save.vehicleId))
                        _appearances[save.vehicleId] = save.ToAppearance();
                }
            }
        }

        public bool IsCompleted(string jobId)
        {
            return !string.IsNullOrEmpty(jobId) && _completed.Contains(jobId);
        }

        public bool IsUnlocked(string jobId)
        {
            if (string.IsNullOrEmpty(jobId)) return false;
            int index = _jobOrder.IndexOf(jobId);
            if (index < 0) return false;
            if (index == 0) return true;
            string prev = _jobOrder[index - 1];
            return IsCompleted(prev);
        }

        public VehicleAppearance GetSaved(string vehicleId)
        {
            if (!string.IsNullOrEmpty(vehicleId) &&
                _appearances.TryGetValue(vehicleId, out VehicleAppearance look))
                return look;
            return default(VehicleAppearance);
        }

        public void Complete(string jobId, int reward, VehicleAppearance look)
        {
            string vehicleId;
            _jobToVehicle.TryGetValue(jobId, out vehicleId);
            Complete(jobId, vehicleId, reward, look);
        }

        public void Complete(string jobId, string vehicleId, int reward, VehicleAppearance look)
        {
            if (string.IsNullOrEmpty(jobId)) return;
            bool wasCompleted = _completed.Contains(jobId);
            _completed.Add(jobId);
            if (!wasCompleted)
            {
                _data.stars += Mathf.Max(0, reward);
                if (_data.stars < 0) _data.stars = 0;
            }
            if (!string.IsNullOrEmpty(vehicleId))
                _appearances[vehicleId] = look;
            SyncAndSave();
        }

        public void ResetAll()
        {
            _completed.Clear();
            _appearances.Clear();
            _data.version = 1;
            _data.stars = 0;
            _data.completedJobs = new string[0];
            _data.appearances = new AppearanceSave[0];
            _save.Clear();
            _save.Save(_data);
        }

        private void SyncAndSave()
        {
            _data.completedJobs = new string[_completed.Count];
            int i = 0;
            foreach (string id in _completed)
                _data.completedJobs[i++] = id;

            _data.appearances = new AppearanceSave[_appearances.Count];
            i = 0;
            foreach (KeyValuePair<string, VehicleAppearance> pair in _appearances)
                _data.appearances[i++] = new AppearanceSave(pair.Key, pair.Value);

            _save.Save(_data);
        }
    }
}
