using System;
using System.IO;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace AsciiArena.Tests
{
    public sealed class ArenaEnemyTacticsTests
    {
        private ArenaSettings settings;
        private ArenaSimulation sim;
        [SetUp] public void Setup()
        {
            settings = ScriptableObject.CreateInstance<ArenaSettings>();
            settings.actOne = true; settings.initialEnemies = 0; settings.spawnPerSecond = 0;
            settings.boltDamage = settings.pulseDamage = 0; settings.bossAt = settings.eliteInterval = 99999;
            sim = new ArenaSimulation(settings, new ArenaLayout()); sim.ChooseUpgrade(0);
        }
        [TearDown] public void Cleanup() { sim.Dispose(); UnityEngine.Object.DestroyImmediate(settings); }
        private Entity Add(byte kind, float2 position)
        {
            var m = sim.World.EntityManager; var e = m.CreateEntity(typeof(Position2D), typeof(Enemy), typeof(Health));
            m.SetComponentData(e, new Position2D { Value = position }); m.SetComponentData(e, new Health { Value = 100 });
            m.SetComponentData(e, new Enemy { Kind = kind, Speed = 2, MaxHealth = 100 }); return e;
        }
        private void Advance(int frames) { for (int i = 0; i < frames; i++) sim.Step(1f / 60); }
        [Test] public void LancerLocksItsPathThenRecoversAndCanBeSidestepped()
        {
            var e = Add(ArenaSimulation.Lancer, new float2(8, 0)); sim.Step(.01f);
            var first = sim.World.EntityManager.GetComponentData<Enemy>(e);
            Assert.That(first.Windup, Is.GreaterThan(1));
            sim.World.EntityManager.SetComponentData(sim.HeroEntity, new Position2D { Value = new float2(2, 3) });
            Advance(100); var after = sim.World.EntityManager.GetComponentData<Enemy>(e);
            Assert.That(after.AttackTarget, Is.EqualTo(first.AttackTarget)); Assert.That(after.Recovery, Is.GreaterThan(0));
            Assert.That(sim.HeroHealth, Is.EqualTo(100));
            Assert.That(sim.World.EntityManager.GetComponentData<Position2D>(e).Value.y, Is.EqualTo(0).Within(.001));
        }
        [Test] public void ChargeSweepsThroughHeroAndCannotTunnelThroughPillar()
        {
            Add(ArenaSimulation.Lancer, new float2(8, 0)); sim.Step(.01f);
            for (int i = 0; i < 18; i++) sim.Step(.1f);
            Assert.That(sim.HeroHealth, Is.EqualTo(100 - 2 * settings.contactDamage));
            sim.Reset(); sim.ChooseUpgrade(0);
            sim.World.EntityManager.SetComponentData(sim.HeroEntity, new Position2D { Value = new float2(-7, 9) });
            var e = Add(ArenaSimulation.Lancer, new float2(-7, 3)); sim.Step(.01f);
            var data = sim.World.EntityManager.GetComponentData<Enemy>(e);
            Assert.That(data.AttackTarget.y, Is.LessThan(5.2f));
            for (int i = 0; i < 18; i++) sim.Step(.1f);
            Assert.That(sim.World.EntityManager.GetComponentData<Position2D>(e).Value.y, Is.LessThan(5.2f));
        }
        [Test] public void BombardierMarksFixedPointAndPlayerCanLeaveIt()
        {
            var e = Add(ArenaSimulation.Bombardier, new float2(8, 0)); sim.Step(.01f);
            var target = sim.World.EntityManager.GetComponentData<Enemy>(e).AttackTarget;
            Advance(60); Assert.That(sim.HeroHealth, Is.EqualTo(100));
            sim.World.EntityManager.SetComponentData(sim.HeroEntity, new Position2D { Value = target + new float2(0, 4) });
            Advance(42); Assert.That(sim.HeroHealth, Is.EqualTo(100));
            Assert.That(sim.World.EntityManager.GetComponentData<Enemy>(e).AttackTarget, Is.EqualTo(target));
        }
        [Test] public void BombardierHitsOnceAndKillingCasterCancelsWarning()
        {
            Add(ArenaSimulation.Bombardier, new float2(8, 0)); Advance(108);
            Assert.That(sim.HeroHealth, Is.EqualTo(100 - 1.5f * settings.enemyShotDamage)); Assert.That(sim.HurtCount, Is.EqualTo(1));
            sim.Reset(); sim.ChooseUpgrade(0); var caster = Add(ArenaSimulation.Bombardier, new float2(8, 0)); sim.Step(.01f);
            var m = sim.World.EntityManager; var b = m.CreateEntity(typeof(Position2D), typeof(Bolt));
            m.SetComponentData(b, m.GetComponentData<Position2D>(caster)); m.SetComponentData(b, new Bolt { Damage = 1000, Lifetime = 1 });
            Advance(108); Assert.That(m.Exists(caster), Is.False); Assert.That(sim.HeroHealth, Is.EqualTo(100));
        }
        [Test] public void FlankerOrbitsAndFiresExactlyThreeTelegraphedShots()
        {
            var e = Add(ArenaSimulation.Flanker, new float2(8, 0)); var m = sim.World.EntityManager;
            var data = m.GetComponentData<Enemy>(e); data.AttackTimer = 1; m.SetComponentData(e, data);
            Advance(20); Assert.That(math.abs(m.GetComponentData<Position2D>(e).Value.y), Is.GreaterThan(.1f));
            Advance(45); Assert.That(m.GetComponentData<Enemy>(e).Windup, Is.GreaterThan(0));
            Advance(58); using var bolts = sim.BoltQuery.ToComponentDataArray<Bolt>(Allocator.Temp);
            int hostile = 0; foreach (var b in bolts) if (b.Hostile) hostile++;
            Assert.That(hostile, Is.EqualTo(3));
        }
        [Test] public void CrowdLimitsSimultaneousWarningsAndCannotStartOffscreen()
        {
            for (int i = 0; i < 12; i++) Add((byte)(3 + i % 3), new float2(6 + i % 3, i / 3));
            sim.Step(.01f); using (var enemies = sim.EnemyQuery.ToComponentDataArray<Enemy>(Allocator.Temp))
            { int warnings = 0; foreach (var e in enemies) if (e.Windup > 0) warnings++; Assert.That(warnings, Is.EqualTo(3)); }
            sim.Reset(); sim.ChooseUpgrade(0); sim.ViewHalfExtents = new float2(3, 3);
            var distant = Add(ArenaSimulation.Bombardier, new float2(8, 0)); sim.Step(.01f);
            Assert.That(sim.World.EntityManager.GetComponentData<Enemy>(distant).Windup, Is.Zero);
        }
        [Test] public void RosterIntroductionsAndPerTypeCapsAreRespected()
        {
            settings.spawnPerSecond = 40; settings.crowdLimit = 400;
            foreach (float time in new[] { 89f, 179f, 299f, 450f })
            {
                sim.Reset(); sim.ChooseUpgrade(0); var s = sim.Snapshot(); s.elapsed = time; s.nextWaveBoss = 99999; sim.Restore(s);
                Advance(240); using var enemies = sim.EnemyQuery.ToComponentDataArray<Enemy>(Allocator.Temp);
                var counts = new int[6]; foreach (var e in enemies) counts[e.Kind]++;
                Assert.That(counts[3], Is.LessThanOrEqualTo(2)); Assert.That(counts[4], Is.LessThanOrEqualTo(2)); Assert.That(counts[5], Is.LessThanOrEqualTo(4));
                if (time == 89) { Assert.That(counts[4], Is.Zero); Assert.That(counts[5], Is.Zero); }
                if (time == 179) Assert.That(counts[5], Is.Zero);
                if (time == 450) { Assert.That(counts[3], Is.Positive); Assert.That(counts[4], Is.Positive); Assert.That(counts[5], Is.Positive); }
            }
        }
        [TestCase(1)] [TestCase(2)] [TestCase(4)] public void SpeedUsesSameFixedStepCombatAndPauseHasNoCatchup(int rate)
        {
            sim.SetPlaybackSpeed(rate); Add(ArenaSimulation.Lancer, new float2(8, 0));
            sim.Input = new float2(0, 1); sim.TryDash();
            var start = sim.Snapshot();
            for (int i = 0; i < 60; i++) sim.AdvanceFrame(1f / 60);
            var accelerated = sim.Snapshot();
            sim.Restore(start); sim.Input = new float2(0, 1);
            for (int i = 0; i < 60 * rate; i++) sim.Step(1f / 60);
            Assert.That(JsonUtility.ToJson(sim.Snapshot()), Is.EqualTo(JsonUtility.ToJson(accelerated)));
            float elapsed = sim.Elapsed; sim.Paused = true; sim.AdvanceFrame(60); Assert.That(sim.Elapsed, Is.EqualTo(elapsed));
            sim.Paused = false; sim.AdvanceFrame(1f / 60); Assert.That(sim.Elapsed - elapsed, Is.EqualTo(rate / 60f).Within(.0001f));
        }
        [Test] public void TacticalStateSurvivesSaveAndLegacyVersionOneStillLoads()
        {
            Add(ArenaSimulation.Lancer, new float2(8, 0)); Add(ArenaSimulation.Bombardier, new float2(8, 3)); Advance(75);
            var s = JsonUtility.FromJson<ArenaSnapshot>(JsonUtility.ToJson(sim.Snapshot()));
            sim.Restore(s); Assert.That(JsonUtility.ToJson(sim.Snapshot()), Is.EqualTo(JsonUtility.ToJson(s)));
            s.version = 1; sim.Restore(s); Assert.That(sim.Snapshot().version, Is.EqualTo(4));
        }
        [Test] public void SpeedPreferenceSurvivesRestartTrainingAndContinue()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ascii-speed-" + Guid.NewGuid());
            try
            {
                var session = new ArenaSession(sim, new ArenaSaveStore(dir)); session.Initialize(); session.CyclePlaybackSpeed(); session.CyclePlaybackSpeed();
                session.NewRun(20); Assert.That(sim.PlaybackSpeed, Is.EqualTo(4));
                session.Practice(SpecialBoss.Bellkeeper, true); session.ToHome(); session.Continue(); Assert.That(sim.PlaybackSpeed, Is.EqualTo(4));
                sim.SetPlaybackSpeed(1); var restarted = new ArenaSession(sim, new ArenaSaveStore(dir)); restarted.Initialize(); Assert.That(sim.PlaybackSpeed, Is.EqualTo(4));
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }
    }
}
