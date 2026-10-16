using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace AsciiArena.Tests
{
    public sealed class ArenaPreferencesTests
    {
        private ArenaSettings settings; private ArenaSimulation sim; private ArenaSession session;
        private string directory; private float oldVolume; private bool oldCrt;
        [Serializable] private sealed class LegacyProfile { public int version = 1, encountered = 2, victories = 3, playbackSpeed = 2; }
        [SetUp] public void Setup()
        {
            oldVolume = AudioListener.volume; oldCrt = ArenaDisplay.CrtEnabled;
            settings = ScriptableObject.CreateInstance<ArenaSettings>(); settings.actOne = true;
            settings.initialEnemies = 0; settings.spawnPerSecond = 0;
            sim = new ArenaSimulation(settings, new ArenaLayout());
            directory = Path.Combine(Path.GetTempPath(), "ascii-preferences-" + Guid.NewGuid());
            session = NewSession(); session.Initialize();
        }
        private ArenaSession NewSession() => new ArenaSession(sim, new ArenaSaveStore(directory));
        [TearDown] public void Cleanup()
        {
            sim.Dispose(); UnityEngine.Object.DestroyImmediate(settings);
            AudioListener.volume = oldVolume; ArenaDisplay.CrtEnabled = oldCrt;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        [Test] public void LegacyProfileGetsAudioDisplayDefaultsAndKeepsProgress()
        {
            session.Store.Write("profile", new LegacyProfile()); session = NewSession(); session.Initialize();
            Assert.That(session.Profile.version, Is.EqualTo(2)); Assert.That(session.Profile.encountered, Is.EqualTo(2));
            Assert.That(session.Profile.victories, Is.EqualTo(3)); Assert.That(sim.PlaybackSpeed, Is.EqualTo(2));
            Assert.That(session.Profile.masterVolume, Is.EqualTo(1)); Assert.That(AudioListener.volume, Is.EqualTo(1));
            Assert.That(session.Profile.crtEnabled && session.Profile.fullscreen, Is.True);
        }
        [Test] public void PreferencesPersistAcrossRestartIncludingZeroAndFalse()
        {
            session.SetMasterVolume(0); session.SetCrt(false); session.SetFullscreen(false); session.ToggleBossTestPhase();
            session = NewSession(); session.Initialize();
            Assert.That(AudioListener.volume, Is.Zero); Assert.That(ArenaDisplay.CrtEnabled, Is.False);
            Assert.That(session.Profile.fullscreen, Is.False); Assert.That(session.Profile.bossTestSecondPhase, Is.True);
            session.SetMasterVolume(.37f); session.ToggleMute(); session = NewSession(); session.Initialize();
            Assert.That(session.Profile.masterVolume, Is.EqualTo(.37f)); Assert.That(AudioListener.volume, Is.Zero);
            session.ToggleMute(); Assert.That(AudioListener.volume, Is.EqualTo(.37f));
        }
        [Test] public void SliderFlushesOnSettingsExitWithoutChangingCombat()
        {
            session.NewRun(73); sim.ChooseUpgrade(0); sim.Step(.1f); session.TogglePause();
            string expected = JsonUtility.ToJson(sim.Snapshot());
            session.OpenMenuPage(ArenaMenuPage.Settings); session.SetMasterVolume(.43f); session.SetCrt(false);
            session.SetMasterVolume(.27f); session.Back();
            Assert.That(JsonUtility.ToJson(sim.Snapshot()), Is.EqualTo(expected)); Assert.That(sim.Paused, Is.True);
            session = NewSession(); session.Initialize(); Assert.That(session.Profile.masterVolume, Is.EqualTo(.27f));
        }
        [TestCase(SpecialBoss.Bellkeeper)] [TestCase(SpecialBoss.Archivist)]
        public void BossTestBypassesUnlockAndNeverReplacesExpedition(SpecialBoss boss)
        {
            session.NewRun(73); sim.ChooseUpgrade(0); sim.Step(.1f); session.TogglePause();
            string expected = JsonUtility.ToJson(sim.Snapshot());
            Assert.That(session.Unlocked(boss), Is.False); session.TestBoss(boss);
            Assert.That(sim.Training && sim.BossActive && session.BossTesting, Is.True);
            Assert.That(sim.World.EntityManager.GetComponentData<Enemy>(sim.BossEntity).Special, Is.EqualTo(boss));
            session.RetryTraining(); Assert.That(sim.BossActive, Is.True);
            var manager = sim.World.EntityManager;
            var bolt = manager.CreateEntity(typeof(Position2D), typeof(Bolt));
            manager.SetComponentData(bolt, manager.GetComponentData<Position2D>(sim.BossEntity));
            manager.SetComponentData(bolt, new Bolt { Damage = 100000, Lifetime = 1 });
            sim.Step(.1f); Assert.That(sim.Phase, Is.EqualTo(RunPhase.Victory)); session.Tick(); session.Save();
            Assert.That(session.Profile.encountered, Is.Zero); Assert.That(session.Profile.victories, Is.Zero);
            session.Continue(); Assert.That(JsonUtility.ToJson(sim.Snapshot()), Is.EqualTo(expected)); Assert.That(session.BossTesting, Is.False);
        }
        [Test] public void PhaseTwoAndWardenUseDifferentHealthRulesAndBackReturnsToSettings()
        {
            session.OpenMenuPage(ArenaMenuPage.Settings); session.OpenMenuPage(ArenaMenuPage.BossTests); session.Back();
            Assert.That(session.MenuPage, Is.EqualTo(ArenaMenuPage.Settings)); Assert.That(session.Home, Is.True);
            session.ToggleBossTestPhase(); session.TestBoss(SpecialBoss.Archivist);
            Assert.That(sim.BossHealthRatio, Is.EqualTo(.45f).Within(.001));
            session.RetryTraining(); Assert.That(sim.BossHealthRatio, Is.EqualTo(.45f).Within(.001));
            session.TestBoss(SpecialBoss.Bellkeeper, true);
            var enemy = sim.World.EntityManager.GetComponentData<Enemy>(sim.BossEntity);
            Assert.That(enemy.FinalBoss, Is.False); Assert.That(enemy.MaxHealth, Is.EqualTo(sim.Settings.waveBossHealth)); Assert.That(sim.BossHealthRatio, Is.EqualTo(1));
        }
        [Test] public void OrdinaryTrainingStaysLockedAndBossTestDoesNotCreateExpedition()
        {
            session.Practice(SpecialBoss.Archivist); Assert.That(session.Home, Is.True);
            session.TestBoss(SpecialBoss.Archivist); session.Save(); session.ToHome();
            Assert.That(session.HasSavedRun, Is.False); Assert.That(File.Exists(Path.Combine(directory, "run.json")), Is.False);
            Assert.That(session.Unlocked(SpecialBoss.Archivist), Is.False);
        }
        [Test] public void CrowdPreferencePersistsAndPracticeNeverReplacesExpedition()
        {
            session.NewRun(77);sim.ChooseUpgrade(0);sim.Step(.1f);session.Save();
            string before=JsonUtility.ToJson(sim.Snapshot());
            session.ToggleBossTestCrowd();session.TestBoss(SpecialBoss.Archivist);
            Assert.That(sim.TrainingCrowd,Is.True);Assert.That(sim.EnemyQuery.CalculateEntityCount(),Is.EqualTo(25));
            session.RetryTraining();Assert.That(sim.TrainingCrowd,Is.True);
            session.ToHome();session=NewSession();session.Initialize();Assert.That(session.Profile.bossTestCrowd,Is.True);
            session.Continue();Assert.That(JsonUtility.ToJson(sim.Snapshot()),Is.EqualTo(before));Assert.That(sim.TrainingCrowd,Is.False);
        }
    }
}
