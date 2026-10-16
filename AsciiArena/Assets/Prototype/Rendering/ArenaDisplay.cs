using UnityEngine;

namespace AsciiArena
{
    // Display preference, deliberately outside the simulation and its restart state.
    public static class ArenaDisplay
    {
        private static bool crtEnabled = true;
        private static readonly int Bypass = Shader.PropertyToID("_ArenaCrtBypass");
        public static event System.Action<bool> CrtChanged;
        public static bool CrtEnabled
        {
            get => crtEnabled;
            set { crtEnabled = value; Shader.SetGlobalFloat(Bypass, value ? 0 : 1); CrtChanged?.Invoke(value); }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => CrtEnabled = true;
    }
}
