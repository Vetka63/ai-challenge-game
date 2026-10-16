using System;
using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace AsciiArena.Tests
{
    public sealed class ArenaDigitalWorldTests
    {
        private ArenaSimulation sim;
        private ArenaSettings settings;
        private ArenaSession session;
        private string directory;
        [SetUp] public void Setup()
        {
            settings=ScriptableObject.CreateInstance<ArenaSettings>();settings.initialEnemies=0;settings.spawnPerSecond=0;
            settings.actOne=true;settings.contactDamage=settings.enemyShotDamage=settings.boltDamage=settings.pulseDamage=0;
            sim=new ArenaSimulation(settings,new ArenaLayout());
            directory=Path.Combine(Path.GetTempPath(),"digital-world-"+Guid.NewGuid());
            session=new ArenaSession(sim,new ArenaSaveStore(directory));session.Initialize();
        }
        [TearDown] public void Cleanup()
        {sim.Dispose();UnityEngine.Object.DestroyImmediate(settings);if(Directory.Exists(directory))Directory.Delete(directory,true);}
        private void Step(float seconds){for(int i=0;i<Mathf.CeilToInt(seconds*60);i++)sim.Step(1f/60);}
        private void KillBoss()
        {
            var m=sim.World.EntityManager;var b=m.CreateEntity(typeof(Bolt),typeof(Position2D));
            m.SetComponentData(b,m.GetComponentData<Position2D>(sim.BossEntity));m.SetComponentData(b,new Bolt{Damage=100000,Lifetime=1});sim.Step(1f/60);
        }
        [Test] public void SelectionIsPausedAndCancelNeverReplacesExpedition()
        {
            session.NewRun(7);sim.ChooseUpgrade(0);Step(.1f);session.ToHome();string before=JsonUtility.ToJson(sim.Snapshot());
            session.RequestNew();session.SelectHero(HeroIdentity.Doomguy);sim.AdvanceFrame(10);
            Assert.That(JsonUtility.ToJson(sim.Snapshot()),Is.EqualTo(before));
            session.BeginSelectedRun();Assert.That(session.ConfirmNew,Is.True);session.Back();session.Back();session.Continue();
            Assert.That(JsonUtility.ToJson(sim.Snapshot()),Is.EqualTo(before));
        }
        [TestCase(HeroIdentity.Spark)] [TestCase(HeroIdentity.Pacman)] [TestCase(HeroIdentity.Doomguy)] [TestCase(HeroIdentity.Ascii)]
        public void HeroPersistsInRunAndProfileWithoutChangingStats(HeroIdentity identity)
        {
            session.RequestNew();session.SelectHero(identity);session.BeginSelectedRun();
            Assert.That(sim.HeroIdentity,Is.EqualTo(identity));Assert.That(sim.HeroHealth,Is.EqualTo(settings.heroHealth));
            Assert.That(sim.HeroSpeed,Is.EqualTo(settings.heroSpeed));Assert.That(sim.CoreRequired,Is.True);
            session.ToHome();var next=new ArenaSession(sim,new ArenaSaveStore(directory));next.Initialize();
            Assert.That(next.Profile.selectedHero,Is.EqualTo(identity));next.Continue();Assert.That(sim.HeroIdentity,Is.EqualTo(identity));
        }
        [Test] public void CoreTransitionRestoresAndOnlyCoreDeathCompletesNewRun()
        {
            session.NewRun(7);sim.ChooseUpgrade(0);var s=sim.Snapshot();s.elapsed=settings.bossAt;s.nextElite=s.nextWaveBoss=99999;
            sim.Restore(s);Step(.02f);Assert.That(sim.BossActive,Is.True);KillBoss();
            Assert.That(sim.Finished,Is.False);Assert.That(sim.CoreTransition,Is.GreaterThan(2));
            var pending=sim.Snapshot();sim.Restore(pending);Step(3.1f);
            Assert.That(sim.BossActive,Is.True);Assert.That(sim.FinalBossKind,Is.EqualTo(SpecialBoss.Core));
            Assert.That(sim.CoreTransition,Is.Zero);KillBoss();Assert.That(sim.Phase,Is.EqualTo(RunPhase.Victory));
        }
        [Test] public void EarlierSaveKeepsAsciiHeroAndEarlierFinale()
        {
            sim.ChooseUpgrade(0);var s=sim.Snapshot();s.version=3;s.heroIdentity=HeroIdentity.Doomguy;s.coreRequired=true;
            sim.Restore(s);Assert.That(sim.HeroIdentity,Is.EqualTo(HeroIdentity.Ascii));Assert.That(sim.CoreRequired,Is.False);
        }
        [Test] public void CorePredictionStopsTrackingAfterWarningAndPracticeKeepsExpedition()
        {
            session.NewRun(7);sim.ChooseUpgrade(0);session.Save();string saved=JsonUtility.ToJson(sim.Snapshot());
            session.TestBoss(SpecialBoss.Core);sim.Input=new float2(1,0);Step(2.1f);
            Assert.That(sim.Hazards.Count,Is.GreaterThan(0));var origin=sim.Hazards[0].Origin;
            sim.Input=new float2(-1,0);Step(.2f);Assert.That(sim.Hazards[0].Origin,Is.EqualTo(origin));
            session.Continue();Assert.That(JsonUtility.ToJson(sim.Snapshot()),Is.EqualTo(saved));
        }
    }
}
