using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace AsciiArena
{
    public sealed class ArenaInput : MonoBehaviour, ITickable
    {
        [Inject] private ArenaSimulation simulation;
        [Inject] private ArenaSession session;
        // Zenject calls this before the simulation, avoiding a one-frame input delay.
        public void Tick()
        {
            var camera = Camera.main;
            if (simulation != null && camera != null)
                simulation.ViewHalfExtents = new float2(camera.orthographicSize * camera.aspect, camera.orthographicSize);
            var keyboard = Keyboard.current;
            if (keyboard == null || simulation == null) return;
            simulation.Input = new float2(
                (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0));
            if (math.lengthsq(simulation.Input) > 0) simulation.DemoMode = false;
            if (keyboard.f2Key.wasPressedThisFrame) session.SetCrt(!session.Profile.crtEnabled);
            if (keyboard.f11Key.wasPressedThisFrame) session.SetFullscreen(!session.Fullscreen);
            if (keyboard.mKey.wasPressedThisFrame) session.ToggleMute();
            if (keyboard.escapeKey.wasPressedThisFrame) { session.Back(); simulation.Input = 0; return; }
            if (session.MenuVisible) { simulation.Input = 0; return; }
            if(keyboard.leftShiftKey.wasPressedThisFrame||keyboard.rightShiftKey.wasPressedThisFrame)simulation.TryDash();
            if (keyboard.rKey.wasPressedThisFrame && simulation.Training) session.RetryTraining();
            if (keyboard.tabKey.wasPressedThisFrame) simulation.HighContrast = !simulation.HighContrast;
            if (keyboard.digit1Key.wasPressedThisFrame) simulation.ChooseUpgrade(0);
            if (keyboard.digit2Key.wasPressedThisFrame) simulation.ChooseUpgrade(1);
            if (keyboard.digit3Key.wasPressedThisFrame) simulation.ChooseUpgrade(2);
        }
        private void OnApplicationQuit() { session?.Save(); }
        private void OnApplicationPause(bool paused) { if (paused) session?.Save(); }
    }
}
