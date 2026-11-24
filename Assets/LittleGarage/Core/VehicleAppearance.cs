using UnityEngine;

namespace LittleGarage
{
    /// <summary>Saved visual configuration for a vehicle.</summary>
    [System.Serializable]
    public struct VehicleAppearance
    {
        public Color Body;
        public int Rim;
        public int Decal;

        public bool IsEmpty
        {
            get { return Body.a <= 0f && Rim == 0 && Decal == 0; }
        }
    }
}
