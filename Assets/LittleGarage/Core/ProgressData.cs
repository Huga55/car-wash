using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleGarage
{
    /// <summary>Serialisable, versioned save payload. Plain classes keep JsonUtility happy.</summary>
    [Serializable]
    public sealed class ProgressData
    {
        public int version = 1;
        public int stars;
        public string[] completedJobs = new string[0];
        public AppearanceSave[] appearances = new AppearanceSave[0];
        public float musicVolume = 0.6f;
        public float sfxVolume = 0.8f;
        public bool musicEnabled = true;
        public bool sfxEnabled = true;
    }

    [Serializable]
    public sealed class AppearanceSave
    {
        public string vehicleId;
        public Color body;
        public int rim;
        public int decal;

        public AppearanceSave() { }

        public AppearanceSave(string id, VehicleAppearance look)
        {
            vehicleId = id;
            body = look.Body;
            rim = look.Rim;
            decal = look.Decal;
        }

        public VehicleAppearance ToAppearance()
        {
            return new VehicleAppearance { Body = body, Rim = rim, Decal = decal };
        }
    }
}
