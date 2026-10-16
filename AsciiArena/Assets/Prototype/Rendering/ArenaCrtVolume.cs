using UnityEngine;
using UnityEngine.Rendering;

namespace AsciiArena
{
    // F2 switches both bloom and the CRT pass immediately, including paired GPU captures.
    public sealed class ArenaCrtVolume : MonoBehaviour
    {
        public Volume volume;
        private void OnEnable(){ArenaDisplay.CrtChanged+=Apply;Apply(ArenaDisplay.CrtEnabled);}
        private void OnDisable(){ArenaDisplay.CrtChanged-=Apply;}
        private void Apply(bool enabled){if(volume!=null)volume.weight=enabled?1:0;}
    }
}
