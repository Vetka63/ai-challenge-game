using System;
using UnityEngine;

namespace AsciiArena
{
    // Preferences belong to the profile, never to an expedition snapshot or its ECS world.
    public sealed partial class ArenaSession
    {
        private bool preferencesDirty;
        private float preferencesSaveAt;
        public bool Fullscreen { get; private set; }
        public event Action VolumePreviewRequested;

        private ArenaProfile LoadProfile()
        {
            var profile = Store.Read<ArenaProfile>("profile", p => p != null && (p.version == 1 || p.version == 2) && p.encountered >= 0 && p.encountered <= 7, out _) ?? new ArenaProfile();
            if (profile.version == 1)
            {
                // Explicit defaults: old JSON has no audio/display fields. Preserve unlocks and speed.
                profile.masterVolume = 1; profile.muted = false; profile.crtEnabled = true;
                profile.fullscreen = true; profile.bossTestSecondPhase = false;
            }
            profile.version = 2;
            if ((int)profile.selectedHero < 0 || (int)profile.selectedHero > 3) profile.selectedHero = HeroIdentity.Spark;
            profile.masterVolume = float.IsNaN(profile.masterVolume) || float.IsInfinity(profile.masterVolume) ? 1 : Mathf.Clamp01(profile.masterVolume);
            return profile;
        }
        private void ApplyPreferences()
        {
            ApplyVolume(); ArenaDisplay.CrtEnabled = Profile.crtEnabled;
            Fullscreen = Profile.fullscreen;
            if (Application.isPlaying && !Application.isEditor && ArenaScreen.HasLaunchOverride()) Fullscreen = Screen.fullScreen;
            else ArenaScreen.ApplyFullscreen(Fullscreen);
        }
        private void ApplyVolume() => AudioListener.volume = Profile.muted ? 0 : Profile.masterVolume;
        public void SetMasterVolume(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            Profile.masterVolume = Mathf.Clamp01(value); Profile.muted = false; ApplyVolume();
            preferencesDirty = true; preferencesSaveAt = Time.realtimeSinceStartup + .3f;
            Status = "Громкость применена. Настройки сохраняются автоматически.";
            VolumePreviewRequested?.Invoke();
        }
        public void ToggleMute()
        {
            Profile.muted = !Profile.muted; ApplyVolume();
            Status = Profile.muted ? "Звук выключен. M включает звук обратно." : "Звук включён.";
            WriteProfile();
        }
        public void SetCrt(bool value)
        {
            Profile.crtEnabled = value; ArenaDisplay.CrtEnabled = value;
            Status = value ? "ЭЛТ включён." : "ЭЛТ выключен."; WriteProfile();
        }
        public void SetFullscreen(bool value)
        {
            Profile.fullscreen = Fullscreen = value; ArenaScreen.ApplyFullscreen(value);
            Status = value ? "Полноэкранный режим включён." : "Оконный режим включён."; WriteProfile();
        }
        public void ToggleBossTestPhase()
        {
            Profile.bossTestSecondPhase = !Profile.bossTestSecondPhase;
            Status = "Начальная фаза сохранена. Применяется при запуске теста особого босса."; WriteProfile();
        }
        public void ToggleBossTestCrowd()
        {
            Profile.bossTestCrowd = !Profile.bossTestCrowd;
            Status = "Сценарий сохранён. Применяется при запуске следующего теста босса."; WriteProfile();
        }
        private void TickPreferences()
        {
            if (preferencesDirty && Time.realtimeSinceStartup >= preferencesSaveAt) FlushPreferences();
        }
        public bool FlushPreferences()
        {
            if (!preferencesDirty) return true;
            WriteProfile();
            // A failed write retains the pending change for retry/quit and shows the failure status.
            preferencesSaveAt = Time.realtimeSinceStartup + 2;
            return !preferencesDirty;
        }
    }
}
