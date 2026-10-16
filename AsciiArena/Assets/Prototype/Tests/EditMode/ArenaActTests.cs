using System;
using System.IO;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace AsciiArena.Tests
{
    public sealed class ArenaActTests
    {
        private ArenaSettings settings;
        private ArenaSimulation sim;
        [SetUp] public void Setup()
        {
            settings = ScriptableObject.CreateInstance<ArenaSettings>();
            settings.actOne = true; settings.runDuration = 1800; settings.bossAt = 1620;
            settings.waveBossInterval = 600; settings.initialEnemies = 0; settings.spawnPerSecond = 0;
            settings.contactDamage = 0; settings.boltDamage = 0;
            sim = new ArenaSimulation(settings, new ArenaLayout()); sim.ChooseUpgrade(0);
        }
        [TearDown] public void Cleanup() { sim.Dispose(); UnityEngine.Object.DestroyImmediate(settings); }
        private void Advance(float seconds)
        {
            for (int f = 0; f < Mathf.CeilToInt(seconds * 60); f++)
            { if (sim.Phase == RunPhase.ChoosingUpgrade) sim.ChooseUpgrade(0); sim.Step(1f / 60); }
        }
        private void KillBoss()
        {
            var m = sim.World.EntityManager; var bolt = m.CreateEntity(typeof(Position2D), typeof(Bolt));
            m.SetComponentData(bolt, m.GetComponentData<Position2D>(sim.BossEntity));
            m.SetComponentData(bolt, new Bolt { Damage = 100000, Lifetime = 1 }); sim.Step(1f / 60);
        }
        [Test] public void FullThirtyMinuteScheduleHasTwoWardensAndFinalVictoryWithoutTimeout()
        {
            settings.enemyShotDamage = 0; settings.eliteInterval = 100000;
            Advance(600.2f); Assert.That(sim.WaveBossesSpawned, Is.EqualTo(1)); KillBoss();
            Advance(600.2f); Assert.That(sim.WaveBossesSpawned, Is.EqualTo(2)); KillBoss();
            Advance(420.2f); Assert.That(sim.BossSpawned, Is.True);
            Assert.That(sim.WaveBossesDefeated, Is.EqualTo(2));
            Advance(181); Assert.That(sim.Elapsed, Is.GreaterThan(1800)); Assert.That(sim.Finished, Is.False);
            KillBoss(); Assert.That(sim.Phase, Is.EqualTo(RunPhase.Victory));
        }
        [TestCase(SpecialBoss.Bellkeeper)] [TestCase(SpecialBoss.Archivist)]
        public void BossSignalsBeforeDamageAndTrainingHasNoCrowd(SpecialBoss boss)
        {
            sim.StartTraining(boss); settings.pulseDamage = settings.orbitDamage = 0;
            Advance(2.1f); Assert.That(sim.Hazards.Count, Is.GreaterThan(0));
            Assert.That(sim.Hazards[0].Delay, Is.GreaterThan(.9f)); Assert.That(sim.HeroHealth, Is.EqualTo(sim.MaxHealth));
            Assert.That(sim.Hazards[0].Ring, Is.EqualTo(boss == SpecialBoss.Bellkeeper));
            Advance(8); Assert.That(sim.EnemyQuery.CalculateEntityCount(), Is.EqualTo(1));
            Assert.That(sim.EncounteredBosses, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => sim.Snapshot());
        }
        [Test] public void RingGapAndLaneEdgesMatchDamageGeometry()
        {
            var ring = new BossHazard { Ring = true, Origin = 0, Radius = 5, GapAngle = 0, Width = .36f };
            Assert.That(ArenaSimulation.HazardContains(ring, new float2(5, 0)), Is.False);
            Assert.That(ArenaSimulation.HazardContains(ring, new float2(-5, 0)), Is.True);
            Assert.That(ArenaSimulation.HazardContains(ring, new float2(-6, 0)), Is.False);
            var lane = new BossHazard { Origin = 0, Direction = new float2(1, 0), Radius = 18, Width = .55f };
            Assert.That(ArenaSimulation.HazardContains(lane, new float2(3, .54f)), Is.True);
            Assert.That(ArenaSimulation.HazardContains(lane, new float2(3, .56f)), Is.False);
        }
        [Test] public void WarningIsSafeActiveLaneHurtsAndDashProtects()
        {
            var h = new BossHazard { Origin = ArenaLayout.Project(sim.HeroPosition), Direction = new float2(1, 0), Delay = .2f, Remaining = .4f, Width = .55f, Radius = 18 };
            sim.Hazards.Add(h); Advance(.1f); Assert.That(sim.HeroHealth, Is.EqualTo(settings.heroHealth));
            Advance(.2f); Assert.That(sim.HeroHealth, Is.EqualTo(settings.heroHealth - settings.enemyShotDamage * 2.5f));
            sim.Reset(); sim.ChooseUpgrade(0); h.Delay = 0; sim.Hazards.Add(h); sim.Input = new float2(1, 0); sim.TryDash();
            Advance(.05f); Assert.That(sim.HeroHealth, Is.EqualTo(settings.heroHealth));
        }
        [Test] public void SnapshotRoundTripRetainsRngTimersChoicesHazardsAndFuture()
        {
            settings.initialEnemies = 8; settings.spawnPerSecond = 2; settings.boltDamage = 18;
            sim.Reset(); sim.ChooseUpgrade(0); sim.Input = new float2(1, 0); Advance(4);
            sim.TryDash(); sim.Step(1f / 60);
            sim.Hazards.Add(new BossHazard { Ring = true, Origin = 0, Delay = 1, Remaining = 4, Radius = 1.5f, Width = .3f });
            var saved = JsonUtility.FromJson<ArenaSnapshot>(JsonUtility.ToJson(sim.Snapshot()));
            var otherSettings = UnityEngine.Object.Instantiate(settings);
            var other = new ArenaSimulation(otherSettings, new ArenaLayout());
            try
            {
                other.Restore(saved);
                Assert.That(JsonUtility.ToJson(other.Snapshot()), Is.EqualTo(JsonUtility.ToJson(saved)));
                for (int i = 0; i < 600; i++)
                {
                    sim.Input = other.Input = new float2(0, 1);
                    if (sim.Phase == RunPhase.ChoosingUpgrade) { sim.ChooseUpgrade(1); other.ChooseUpgrade(1); }
                    sim.Step(1f / 60); other.Step(1f / 60);
                }
                Assert.That(JsonUtility.ToJson(other.Snapshot()), Is.EqualTo(JsonUtility.ToJson(sim.Snapshot())));
            }
            finally { other.Dispose(); UnityEngine.Object.DestroyImmediate(otherSettings); }
        }
        [Test] public void PiercingProjectileRemapsPreviouslyHitEnemyOnRestore()
        {
            var m = sim.World.EntityManager; var e = m.CreateEntity(typeof(Position2D), typeof(Enemy), typeof(Health));
            m.SetComponentData(e, new Position2D { Value = sim.HeroPosition + new float2(4, 0) });
            m.SetComponentData(e, new Enemy { MaxHealth = 100 }); m.SetComponentData(e, new Health { Value = 100 });
            var b = m.CreateEntity(typeof(Position2D), typeof(Bolt)); var bolt = new Bolt { Lifetime = 5, Damage = 50, Pierces = 2 };
            bolt.HitEntities.Add(e); m.SetComponentData(b, bolt); m.SetComponentData(b, m.GetComponentData<Position2D>(e));
            sim.Restore(JsonUtility.FromJson<ArenaSnapshot>(JsonUtility.ToJson(sim.Snapshot())));
            sim.Step(.01f); using var health = sim.EnemyQuery.ToComponentDataArray<Health>(Allocator.Temp);
            Assert.That(health[0].Value, Is.EqualTo(100));
        }
        [Test] public void ChoosingUpgradeRestoresSameOffersAndDoesNotAdvance()
        {
            var m = sim.World.EntityManager; var p = m.CreateEntity(typeof(Position2D), typeof(Experience));
            m.SetComponentData(p, new Position2D { Value = sim.HeroPosition }); m.SetComponentData(p, new Experience { Value = 12 });
            sim.Step(.01f); var s = sim.Snapshot(); sim.Restore(JsonUtility.FromJson<ArenaSnapshot>(JsonUtility.ToJson(s)));
            sim.Step(1); Assert.That(sim.Elapsed, Is.EqualTo(s.elapsed)); Assert.That(sim.Offers, Is.EqualTo(s.offers));
            sim.ChooseUpgrade(0); Assert.That(sim.WeaponCount, Is.EqualTo(2));
        }
        [Test] public void CorruptSaveRecoversBackupAndUnsupportedSchemaIsRejected()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ascii-act-tests-" + Guid.NewGuid());
            try
            {
                var store = new ArenaSaveStore(dir); store.Write("run", sim.Snapshot()); Advance(1); store.Write("run", sim.Snapshot());
                File.WriteAllText(Path.Combine(dir, "run.json"), "truncated");
                var s = store.Read<ArenaSnapshot>("run", ArenaSimulation.ValidSnapshot, out bool recovered);
                Assert.That(recovered, Is.True); Assert.That(s.elapsed, Is.Zero);
                s.version = 999; Assert.That(ArenaSimulation.ValidSnapshot(s), Is.False);
                Assert.Throws<InvalidDataException>(() => sim.Restore(s));
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }
        [Test] public void SeedSelectsBothVariantsAndReloadNeverRerolls()
        {
            int seen = 0;
            for (int seed = 1; seed <= 32; seed++)
            {
                settings.seed = seed; sim.Reset(); seen |= 1 << (int)sim.FinalBossKind;
                var s = sim.Snapshot(); sim.Restore(s); Assert.That(sim.FinalBossKind, Is.EqualTo(s.finalBoss));
            }
            Assert.That(seen, Is.EqualTo(3));
        }
        [Test] public void SessionTrainingPreservesExactExpeditionAndCompletedRunCannotContinue()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ascii-session-tests-" + Guid.NewGuid());
            try
            {
                var session = new ArenaSession(sim, new ArenaSaveStore(dir)); session.Initialize();
                session.NewRun(12); sim.ChooseUpgrade(0); Advance(2); session.Save();
                string saved = JsonUtility.ToJson(sim.Snapshot());
                session.Practice(SpecialBoss.Bellkeeper, true); Advance(3); session.Save();
                session.ToHome(); session.Continue();
                Assert.That(sim.Training, Is.False); Assert.That(JsonUtility.ToJson(sim.Snapshot()), Is.EqualTo(saved));
                sim.World.EntityManager.SetComponentData(sim.HeroEntity, new Health { Value = 0 }); sim.Step(.01f); session.Tick();
                Assert.That(session.HasSavedRun, Is.False);
                var restarted = new ArenaSession(sim, new ArenaSaveStore(dir)); restarted.Initialize();
                Assert.That(restarted.HasSavedRun, Is.False);
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }
        [Test] public void EncounterUnlockPersistsWithoutVictoryAndNewRunNeedsConfirmation()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ascii-profile-tests-" + Guid.NewGuid());
            try
            {
                settings.bossAt = 1;
                var session = new ArenaSession(sim, new ArenaSaveStore(dir)); session.Initialize();
                session.NewRun(14); sim.ChooseUpgrade(0); Advance(1.1f); session.Tick();
                var boss = sim.FinalBossKind; Assert.That(session.Unlocked(boss), Is.True);
                float time = sim.Elapsed; session.ToHome(); session.RequestNew(); session.BeginSelectedRun();
                Assert.That(session.ConfirmNew, Is.True); Assert.That(sim.Elapsed, Is.EqualTo(time));
                var restarted = new ArenaSession(sim, new ArenaSaveStore(dir)); restarted.Initialize();
                Assert.That(restarted.Unlocked(boss), Is.True); Assert.That(restarted.Profile.victories, Is.Zero);
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }
    }
}
