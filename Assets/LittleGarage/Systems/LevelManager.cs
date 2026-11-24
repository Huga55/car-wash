using System;
using UnityEngine;

namespace LittleGarage
{
    /// <summary>Tracks current job and fires job lifecycle events.</summary>
    public sealed class LevelManager : MonoBehaviour
    {
        private GameConfig _config;
        private string _currentId;

        public JobDef Current { get; private set; }
        public event Action<JobDef> JobStarted;
        public event Action JobFinished;

        public void Init(GameConfig config)
        {
            _config = config;
        }

        public void StartJob(string id)
        {
            if (_config == null || string.IsNullOrEmpty(id)) return;
            JobDef job = _config.Job(id);
            if (job == null)
            {
                Debug.LogWarning("LevelManager: job not found " + id);
                return;
            }
            _currentId = id;
            Current = job;
            if (JobStarted != null) JobStarted(job);
        }

        public void CompleteCurrent()
        {
            if (Current == null) return;
            if (JobFinished != null) JobFinished();
        }

        public string NextJobId()
        {
            if (_config == null || _config.jobs == null) return null;
            int index = -1;
            for (int i = 0; i < _config.jobs.Length; i++)
            {
                JobDef j = _config.jobs[i];
                if (j != null && j.id == _currentId) { index = i; break; }
            }
            if (index < 0) return _config.jobs.Length > 0 && _config.jobs[0] != null ? _config.jobs[0].id : null;
            int next = index + 1;
            if (next >= _config.jobs.Length) return null;
            return _config.jobs[next] != null ? _config.jobs[next].id : null;
        }
    }
}
