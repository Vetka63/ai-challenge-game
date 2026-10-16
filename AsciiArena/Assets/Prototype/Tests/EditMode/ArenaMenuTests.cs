using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace AsciiArena.Tests
{
    public sealed class ArenaMenuTests
    {
        private ArenaSettings settings;
        private ArenaSimulation sim;
        private ArenaSession session;
        private string directory;
        [SetUp] public void Setup()
        {
            settings = ScriptableObject.CreateInstance<ArenaSettings>();
            settings.initialEnemies = 0; settings.spawnPerSecond = 0;
            sim = new ArenaSimulation(settings, new ArenaLayout());
            directory = Path.Combine(Path.GetTempPath(), "ascii-menu-" + Guid.NewGuid());
            session = new ArenaSession(sim, new ArenaSaveStore(directory)); session.Initialize();
        }
        [TearDown] public void Cleanup()
        {
            sim.Dispose(); UnityEngine.Object.DestroyImmediate(settings);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        [Test] public void HomeSettingsBackNeverStartsCombat()
        {
            session.OpenMenuPage(ArenaMenuPage.Settings); session.SetPlaybackSpeed(4);
            sim.AdvanceFrame(1); Assert.That(sim.Elapsed, Is.Zero);
            session.Back(); Assert.That(session.MenuPage, Is.EqualTo(ArenaMenuPage.Root));
            session.Back(); Assert.That(session.Home && sim.Paused, Is.True);
            Assert.That(session.HasSavedRun, Is.False);
        }
        [Test] public void SettingsBackKeepsPauseThenResumesAtSelectedSpeed()
        {
            session.NewRun(73); sim.ChooseUpgrade(0); sim.AdvanceFrame(.1f);
            session.Back(); session.OpenMenuPage(ArenaMenuPage.Settings);
            var snapshot = JsonUtility.ToJson(sim.Snapshot());
            session.SetPlaybackSpeed(4); sim.AdvanceFrame(10);
            Assert.That(JsonUtility.ToJson(sim.Snapshot()), Is.EqualTo(snapshot));
            session.Back(); Assert.That(sim.Paused, Is.True); Assert.That(session.MenuPage, Is.EqualTo(ArenaMenuPage.Root));
            session.Back(); float before = sim.Elapsed; sim.AdvanceFrame(.1f);
            Assert.That(sim.Elapsed - before, Is.EqualTo(.4f).Within(.001));
        }
        [Test] public void ChoosingWeaponSurvivesSettingsAndDoesNotAdvance()
        {
            session.NewRun(73); session.Back(); session.OpenMenuPage(ArenaMenuPage.Settings);
            session.SetPlaybackSpeed(2); session.Back(); session.Back(); sim.AdvanceFrame(1);
            Assert.That(sim.Phase, Is.EqualTo(RunPhase.ChoosingWeapon)); Assert.That(sim.Elapsed, Is.Zero);
        }
        [Test] public void CancellingNewRunPreservesSavedExpedition()
        {
            session.NewRun(73); sim.ChooseUpgrade(0); sim.AdvanceFrame(.1f); session.ToHome();
            string expected = JsonUtility.ToJson(sim.Snapshot());
            session.RequestNew(); Assert.That(session.MenuPage, Is.EqualTo(ArenaMenuPage.Heroes));
            session.BeginSelectedRun(); Assert.That(session.ConfirmNew, Is.True);
            session.Back(); Assert.That(session.ConfirmNew, Is.False);
            session.Continue(); Assert.That(JsonUtility.ToJson(sim.Snapshot()), Is.EqualTo(expected));
        }
        [Test] public void TrainingAndContinueResetMenuPageButRetainSpeedAndExpedition()
        {
            session.NewRun(73); sim.ChooseUpgrade(0); sim.AdvanceFrame(.1f); session.ToHome();
            string expected = JsonUtility.ToJson(sim.Snapshot());
            session.OpenMenuPage(ArenaMenuPage.Settings); session.SetPlaybackSpeed(2); session.Back();
            session.OpenMenuPage(ArenaMenuPage.Training); session.Practice(SpecialBoss.Bellkeeper, true);
            Assert.That(session.MenuPage, Is.EqualTo(ArenaMenuPage.Root));
            session.Back(); session.OpenMenuPage(ArenaMenuPage.Settings); session.SetPlaybackSpeed(4); session.Back();
            session.ToHome(); session.Continue();
            Assert.That(sim.PlaybackSpeed, Is.EqualTo(4)); Assert.That(JsonUtility.ToJson(sim.Snapshot()), Is.EqualTo(expected));
            var restarted = new ArenaSession(sim, new ArenaSaveStore(directory)); restarted.Initialize();
            Assert.That(sim.PlaybackSpeed, Is.EqualTo(4));
        }
    }
}
