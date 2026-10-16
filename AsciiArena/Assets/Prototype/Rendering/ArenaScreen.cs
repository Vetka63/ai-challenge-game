using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AsciiArena
{
    // Display-only policy. World units, speeds and colliders are never scaled.
    [RequireComponent(typeof(Camera))]
    public sealed class ArenaScreen : MonoBehaviour
    {
        public const float ReferenceHeight=1080;
        public const float VerticalWorldSize=18;
        public float PixelsPerWorldUnit => GetComponent<Camera>().pixelHeight/VerticalWorldSize;
        private void Awake()
        {
            GetComponent<Camera>().orthographicSize=VerticalWorldSize*.5f;
        }
        public static bool HasLaunchOverride()
        {
            var args=Environment.GetCommandLineArgs();
            // Explicit resolution flags are reserved for launch overrides and QA.
            return Array.Exists(args,a=>a.Equals("-screen-width",StringComparison.OrdinalIgnoreCase)||a.Equals("-screen-height",StringComparison.OrdinalIgnoreCase)||a.Equals("-screen-fullscreen",StringComparison.OrdinalIgnoreCase));
        }
        public static void ApplyFullscreen(bool enabled)
        {
            if (!Application.isPlaying || Application.isEditor) return;
            if (enabled) EnterFullscreen();
            else Screen.SetResolution(1280,720,FullScreenMode.Windowed);
        }
        public static void ToggleFullscreen()
        {
            ApplyFullscreen(!Screen.fullScreen);
        }
        public static void EnterFullscreen()
        {
            int width=Display.main.systemWidth,height=Display.main.systemHeight;
            if(width<=0||height<=0){width=Screen.currentResolution.width;height=Screen.currentResolution.height;}
            Screen.SetResolution(width,height,FullScreenMode.FullScreenWindow);
        }
    }
}
