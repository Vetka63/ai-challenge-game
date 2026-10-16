using System;
using System.IO;
using UnityEngine;
using Zenject;

namespace AsciiArena
{
    public enum ArenaMenuPage { Root, Settings, Training, BossTests, Heroes }
    [Serializable] public sealed class ArenaProfile
    {
        public int version = 2, encountered, victories;
        public int playbackSpeed = 1;
        public HeroIdentity selectedHero = HeroIdentity.Spark;
        public float masterVolume = 1;
        public bool muted, bossTestSecondPhase, bossTestCrowd;
        public bool crtEnabled = true, fullscreen = true;
    }

    // Zenject owns local persistence. The ECS simulation remains independent of files and menus.
    public sealed partial class ArenaSession : IInitializable, ITickable
    {
        private readonly ArenaSimulation sim;
        private readonly string defaults;
        public ArenaSaveStore Store { get; }
        public ArenaProfile Profile { get; private set; }
        public bool Home { get; private set; } = true;
        public bool ConfirmNew { get; private set; }
        public ArenaMenuPage MenuPage { get; private set; }
        public bool Basics { get; private set; }
        public bool BossTesting { get; private set; }
        public string Status { get; private set; } = "";
        public bool HasSavedRun => saved != null && saved.phase != RunPhase.Victory && saved.phase != RunPhase.Defeat;
        public bool MenuVisible => Home || sim.Paused || sim.Finished;
        private ArenaSnapshot saved;
        private float saveIn = 30;
        private bool outcomeSaved, expeditionStarted;
        private SpecialBoss trainingBoss;

        [Inject] public ArenaSession(ArenaSimulation simulation) : this(simulation, new ArenaSaveStore(DefaultPath())) { }
        public ArenaSession(ArenaSimulation simulation, ArenaSaveStore store)
        {
            sim = simulation; defaults = JsonUtility.ToJson(sim.Settings);
            Store = store;
        }
        private static string DefaultPath()
        {
            string path = Path.Combine(Application.persistentDataPath, "ActOne-v1");
            // Explicit diagnostics use an isolated directory, never the player's own progress.
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "--act-save-dir") path = Path.GetFullPath(args[i + 1]);
            return path;
        }

        public void Initialize()
        {
            Profile = LoadProfile();
            saved = Store.Read<ArenaSnapshot>("run", ArenaSimulation.ValidSnapshot, out bool recovered);
            Status = recovered ? "Восстановлена резервная копия сохранения." : HasSavedRun ? "Сохранённая экспедиция готова к продолжению."
                : saved == null && File.Exists(Path.Combine(Store.DirectoryPath, "run.json")) ? "Не удалось прочитать сохранение. Исходные файлы сохранены."
                : "Легко освоить. Трудно стать мастером.";
            sim.Paused = true;
            sim.SetPlaybackSpeed(Profile.playbackSpeed);
            ApplyPreferences();
        }
        public void CyclePlaybackSpeed()
        {
            SetPlaybackSpeed(sim.PlaybackSpeed == 1 ? 2 : sim.PlaybackSpeed == 2 ? 4 : 1);
        }
        public void SetPlaybackSpeed(int speed)
        {
            sim.SetPlaybackSpeed(speed);
            Profile.playbackSpeed = sim.PlaybackSpeed;
            Status = sim.PlaybackSpeed == 1 ? "Обычная скорость. Полное время на реакцию." : "Ускоренный режим: меньше времени на реакцию. Скорость можно изменить в любой момент.";
            WriteProfile();
        }

        public void OpenMenuPage(ArenaMenuPage page)
        {
            if (!MenuVisible) return;
            MenuPage = page; ConfirmNew = false; sim.Input = 0;
        }
        public void Back()
        {
            if (ConfirmNew) { ConfirmNew = false; Status = "Сохранённая экспедиция оставлена без изменений."; return; }
            if (MenuPage != ArenaMenuPage.Root) { MenuPage = MenuPage == ArenaMenuPage.BossTests ? ArenaMenuPage.Settings : ArenaMenuPage.Root; FlushPreferences(); return; }
            TogglePause();
        }

        public bool Unlocked(SpecialBoss boss) => (Profile.encountered & (1 << (int)boss)) != 0;

        public void RequestNew()
        {
            if (ConfirmNew) { NewRun(); return; }
            if (!Home) return;
            OpenMenuPage(ArenaMenuPage.Heroes);
        }
        public void NewRun(int seed = 0)
        {
            JsonUtility.FromJsonOverwrite(defaults, sim.Settings);
            sim.Settings.seed = seed == 0 ? (Environment.TickCount & int.MaxValue) + 1 : Math.Max(1, seed);
            sim.Reset(); sim.HeroIdentity = Profile.selectedHero; sim.CoreRequired = true; Home = false; ConfirmNew = false; MenuPage = ArenaMenuPage.Root; Basics = false; outcomeSaved = false; expeditionStarted = true;
            BossTesting = false;
            Save();
        }
        public void Continue()
        {
            if (!HasSavedRun) return;
            sim.Restore(saved); Home = false; ConfirmNew = false; MenuPage = ArenaMenuPage.Root; outcomeSaved = false; expeditionStarted = true; Basics = false;
            BossTesting = false;
            Status = "Экспедиция продолжена с сохранённого момента."; saveIn = 30;
        }
        public void TogglePause()
        {
            if (Home || sim.Finished) return;
            sim.Paused = !sim.Paused; sim.Input = 0; ConfirmNew = false; MenuPage = ArenaMenuPage.Root;
        }
        public void ToHome()
        {
            if (!sim.Training && expeditionStarted && !Save()) return;
            Home = true; sim.Paused = true; sim.Input = 0; ConfirmNew = false; MenuPage = ArenaMenuPage.Root;
        }
        public void Practice(SpecialBoss boss, bool basics = false)
        {
            if (!basics && !Unlocked(boss)) return;
            StartPractice(boss, basics, false);
        }
        public void TestBoss(SpecialBoss boss, bool warden = false) => StartPractice(boss, warden, true);
        private void StartPractice(SpecialBoss boss, bool basics, bool testing)
        {
            if (!Home && !sim.Training && expeditionStarted && !Save()) return;
            JsonUtility.FromJsonOverwrite(defaults, sim.Settings);
            sim.StartTraining(boss); sim.HeroIdentity = Profile.selectedHero; Basics = basics; trainingBoss = boss; Home = false; ConfirmNew = false; MenuPage = ArenaMenuPage.Root;
            BossTesting = testing;
            if (basics)
            {
                var m = sim.World.EntityManager; var e = m.GetComponentData<Enemy>(sim.BossEntity);
                e.FinalBoss = false; e.MaxHealth = sim.Settings.waveBossHealth; m.SetComponentData(sim.BossEntity, e);
                m.SetComponentData(sim.BossEntity, new Health { Value = e.MaxHealth });
            }
            else if (testing && Profile.bossTestSecondPhase)
            {
                var m = sim.World.EntityManager;
                float max = m.GetComponentData<Enemy>(sim.BossEntity).MaxHealth;
                m.SetComponentData(sim.BossEntity, new Health { Value = max * .45f });
            }
            if (testing && Profile.bossTestCrowd) sim.EnableTrainingCrowd();
            Status = testing ? "Тест босса: готовый билд, без наград. Экспедиция сохранена." : "Тренировка: без наград и изменений сохранённой экспедиции.";
        }
        public void RetryTraining() { if (sim.Training) StartPractice(trainingBoss, Basics, BossTesting); }
        public void Tick()
        {
            TickPreferences();
            if (Home || sim.Training || !expeditionStarted) return;
            int mask = Profile.encountered | sim.EncounteredBosses;
            if (mask != Profile.encountered)
            {
                Profile.encountered = mask; WriteProfile(); Save();
            }
            if (sim.Finished && !outcomeSaved)
            {
                if (Save()) { outcomeSaved = true; if (sim.Phase == RunPhase.Victory) { Profile.victories++; WriteProfile(); } }
                return;
            }
            saveIn -= Time.unscaledDeltaTime;
            if (saveIn <= 0 && !sim.Finished) Save();
        }
        public bool Save()
        {
            if (!FlushPreferences()) return false;
            if (sim.Training || !expeditionStarted) return true;
            saveIn = 30;
            try
            {
                var snapshot = sim.Snapshot(); Store.Write("run", snapshot); saved = snapshot;
                Status = $"Сохранено: {sim.Elapsed / 60:00}:{sim.Elapsed % 60:00}."; return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            { Status = "Не удалось сохранить. Не закрывай игру: проверь место на диске и доступ к папке."; Debug.LogWarning(e.Message); return false; }
        }
        private void WriteProfile()
        {
            preferencesDirty = true;
            try { Store.Write("profile", Profile); preferencesDirty = false; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            { Status = "Не удалось сохранить настройки профиля. Проверь место на диске и доступ."; Debug.LogWarning(e.Message); }
        }
        public void Quit()
        {
            if (FlushPreferences() && Save()) Application.Quit();
        }
    }
}

