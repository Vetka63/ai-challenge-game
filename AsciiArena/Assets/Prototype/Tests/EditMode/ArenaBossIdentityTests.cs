using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace AsciiArena.Tests
{
    public sealed class ArenaBossIdentityTests
    {
        private ArenaSettings settings;
        private ArenaSimulation sim;
        [SetUp] public void Setup()
        {
            settings = ScriptableObject.CreateInstance<ArenaSettings>(); settings.actOne = true;
            settings.initialEnemies = 0; settings.spawnPerSecond = 0;
            settings.contactDamage = settings.enemyShotDamage = 0;
            settings.boltDamage = settings.pulseDamage = settings.orbitDamage = 0;
            sim = new ArenaSimulation(settings,new ArenaLayout());
        }
        [TearDown] public void Cleanup() { sim.Dispose(); Object.DestroyImmediate(settings); }
        private Enemy Boss => sim.World.EntityManager.GetComponentData<Enemy>(sim.BossEntity);
        private void Start(int kind, int pattern = 0, bool phaseTwo = false)
        {
            sim.StartTraining(kind == 2 ? SpecialBoss.Archivist : SpecialBoss.Bellkeeper);
            var e = Boss; e.FinalBoss = kind != 0; e.Pattern = pattern; e.AttackTimer = 0;
            sim.World.EntityManager.SetComponentData(sim.BossEntity,e);
            if(phaseTwo)sim.World.EntityManager.SetComponentData(sim.BossEntity,new Health{Value=e.MaxHealth*.45f});
            sim.Step(1f/60);
        }
        private void Advance(float seconds)
        {
            for(int i=0;i<Mathf.CeilToInt(seconds*60);i++)sim.Step(1f/60);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void EachBossNaturallyCyclesAllThreePatternsWithRecovery(int kind)
        {
            Start(kind); bool recovery=false; int highest=0;
            for(int i=0;i<60*55;i++)
            {
                sim.Step(1f/60); recovery |= sim.BossRecovering; highest=math.max(highest,Boss.Pattern);
                if(Boss.Windup>0 && sim.Hazards.Count>0)Assert.That(sim.BossRecovering,Is.False);
            }
            Assert.That(highest,Is.GreaterThanOrEqualTo(4),"Boss never completed a full cycle");
            Assert.That(recovery,Is.True,"No punish window"); Assert.That(sim.Phase,Is.EqualTo(RunPhase.Running));
            Assert.That(sim.BossName,Is.EqualTo(kind==0?"FIREWALL GUARD":kind==1?"ALARM DAEMON":"BROKEN ARCHIVE"));
        }
        [TestCase(1,0)] [TestCase(1,1)] [TestCase(1,2)]
        [TestCase(2,0)] [TestCase(2,1)] [TestCase(2,2)]
        public void SecondPhaseAddsTelegraphedCombinationsWithoutMoreHealth(int kind,int pattern)
        {
            Start(kind,pattern); int count=sim.Hazards.Count; float health=Boss.MaxHealth;
            Start(kind,pattern,true);
            Assert.That(sim.Hazards.Count,Is.GreaterThan(count)); Assert.That(Boss.MaxHealth,Is.EqualTo(health));
            Assert.That(sim.Hazards.All(h=>h.Delay>=1.1f && h.Telegraph==h.Delay),Is.True);
        }
        [Test] public void WardenLocksChargeThenWarnsImpactBeforeRecovery()
        {
            Start(0); var e=Boss; Assert.That(e.BossChargePending,Is.True); Assert.That(sim.Hazards,Is.Empty);
            float2 locked=e.AttackTarget, start=sim.World.EntityManager.GetComponentData<Position2D>(sim.BossEntity).Value;
            sim.Input=new float2(0,1); Advance(.4f); Assert.That(Boss.AttackTarget,Is.EqualTo(locked));
            Assert.That(sim.World.EntityManager.GetComponentData<Position2D>(sim.BossEntity).Value,Is.EqualTo(start));
            sim.Input=0;
            for(int i=0;i<120 && sim.Hazards.Count==0;i++)sim.Step(1f/60);
            Assert.That(sim.Hazards.Count,Is.EqualTo(1)); Assert.That(sim.Hazards[0].Disk,Is.True);
            Assert.That(sim.Hazards[0].Delay,Is.GreaterThan(.8f));
            var end=sim.World.EntityManager.GetComponentData<Position2D>(sim.BossEntity).Value;
            Assert.That(sim.Layout.Walkable(end),Is.True); Assert.That(math.distance(end,locked),Is.LessThan(.11f));
            Advance(1.4f); Assert.That(sim.BossRecovering,Is.True);
        }
        [Test] public void ChargeSweepHitsInsideWarningButNotOutsideAndDashProtects()
        {
            foreach(int mode in new[]{0,1,2})
            {
                Start(0); settings.contactDamage=7;
                var m=sim.World.EntityManager; var e=Boss;
                var from=ArenaLayout.Project(e.AttackOrigin); var to=ArenaLayout.Project(e.AttackTarget);
                var dir=math.normalizesafe(to-from);var normal=new float2(-dir.y,dir.x);
                float2 point=from+dir*.5f+normal*(mode==1?ArenaSimulation.WardenChargeWidth+.03f:0);
                m.SetComponentData(sim.HeroEntity,new Position2D{Value=ArenaLayout.ScreenToWorld(point)});
                e.Windup=0;e.BossChargePending=false;e.ChargeLeft=math.distance(from,to);m.SetComponentData(sim.BossEntity,e);
                if(mode==2) { sim.Input=normal; Assert.That(sim.TryDash(),Is.True); }
                sim.Step(.1f);
                Assert.That(sim.HeroHealth,Is.EqualTo(mode==0?sim.MaxHealth-24.5f:sim.MaxHealth));
                settings.contactDamage=0;
            }
        }
        [Test] public void WardenChargeStopsBeforePillarAndImpactUsesActualLanding()
        {
            Start(0); var m=sim.World.EntityManager; var e=Boss;
            e.Pattern=0;e.AttackTimer=0;e.Windup=0;e.BossChargePending=false;e.ChargeLeft=0;e.Recovery=0;
            m.SetComponentData(sim.BossEntity,e);
            m.SetComponentData(sim.BossEntity,new Position2D{Value=new float2(11,6)});
            m.SetComponentData(sim.HeroEntity,new Position2D{Value=new float2(11,12)});
            sim.Step(1f/60);float2 target=Boss.AttackTarget;
            Assert.That(target.y,Is.LessThan(9));Assert.That(sim.Layout.Walkable(target),Is.True);
            for(int i=0;i<120 && sim.Hazards.Count==0;i++)
            {
                sim.Step(1f/60);
                Assert.That(sim.Layout.Walkable(m.GetComponentData<Position2D>(sim.BossEntity).Value),Is.True);
            }
            Assert.That(sim.Hazards.Count,Is.EqualTo(1));
            Assert.That(math.distance(sim.Hazards[0].Origin,ArenaLayout.Project(target)),Is.LessThan(.11f));
        }
        [Test] public void InwardRingSweepsDamageAndPreservesGap()
        {
            sim.ChooseUpgrade(0);settings.enemyShotDamage=8;
            var center=ArenaLayout.Project(sim.HeroPosition)+new float2(5,0);
            sim.Hazards.Add(new BossHazard{Ring=true,Origin=center,Radius=5.5f,Width=.1f,GapAngle=0,RadialSpeed=-10,Remaining=2});
            sim.Step(.1f);Assert.That(sim.HeroHealth,Is.EqualTo(sim.MaxHealth-20));
            Assert.That(sim.Hazards[0].Radius,Is.EqualTo(4.5f).Within(.001f));
            Assert.That(ArenaSimulation.HazardContains(sim.Hazards[0],center+new float2(4.5f,0)),Is.False);
        }
        [Test] public void DiskWarningAndEdgeUseExactDamageGeometry()
        {
            sim.ChooseUpgrade(0);settings.enemyShotDamage=8;
            var h=new BossHazard{Disk=true,Origin=ArenaLayout.Project(sim.HeroPosition),Radius=2,Delay=.25f,Remaining=.5f};
            Assert.That(ArenaSimulation.HazardContains(h,h.Origin+new float2(2.01f,0)),Is.False);
            Assert.That(ArenaSimulation.HazardContains(h,h.Origin+new float2(1.99f,0)),Is.True);
            sim.Hazards.Add(h);Advance(.2f);Assert.That(sim.HeroHealth,Is.EqualTo(sim.MaxHealth));
            Advance(.1f);Assert.That(sim.HeroHealth,Is.EqualTo(sim.MaxHealth-20));
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void SavesReadEarlierSchemasAndPreserveNewAttackStateAndFuture(int version)
        {
            sim.ChooseUpgrade(0);var saved=sim.Snapshot();saved.version=version;
            saved.enemies=new[]{new EnemySave{position=sim.HeroPosition+new float2(6,0),health=1000,data=new Enemy{
                Boss=true,Elite=true,MaxHealth=1000,Windup=.7f,BossChargePending=true,
                AttackOrigin=sim.HeroPosition+new float2(6,0),AttackTarget=sim.HeroPosition+new float2(-1,0),Pattern=1,AttackTimer=4}}};
            // Version 1/2 legacy records lack the new fields. Their rings must still move outward.
            if(version<3){var es=saved.enemies[0];es.data.BossChargePending=false;saved.enemies[0]=es;}
            saved.hazards=new[]{new BossHazard{Ring=true,Radius=9,RadialSpeed=version==3?-3:0,Width=.3f,Delay=.4f,Telegraph=version==3?1.2f:0,Remaining=3,Source=version==3?(byte)2:(byte)0}};
            sim.Restore(JsonUtility.FromJson<ArenaSnapshot>(JsonUtility.ToJson(saved)));
            var otherSettings=Object.Instantiate(settings);var other=new ArenaSimulation(otherSettings,new ArenaLayout());
            try
            {
                other.Restore(sim.Snapshot());
                Assert.That(other.Snapshot().version,Is.EqualTo(4));
                Assert.That(ArenaSimulation.RingSpeed(other.Hazards[0]),Is.EqualTo(version==3?-3:4));
                for(int i=0;i<600;i++){sim.Step(1f/60);other.Step(1f/60);}
                Assert.That(JsonUtility.ToJson(sim.Snapshot()),Is.EqualTo(JsonUtility.ToJson(other.Snapshot())));
            }
            finally{other.Dispose();Object.DestroyImmediate(otherSettings);}
        }
    }
}
