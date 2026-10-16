using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace AsciiArena.Tests
{
    public sealed class ArenaBossBalanceTests
    {
        private ArenaSettings settings; private ArenaSimulation sim;
        [SetUp] public void Setup()
        {
            settings=ScriptableObject.CreateInstance<ArenaSettings>();settings.actOne=true;
            settings.boltDamage=settings.pulseDamage=settings.orbitDamage=0;settings.contactDamage=0;
            settings.initialEnemies=0;settings.spawnPerSecond=0;
            sim=new ArenaSimulation(settings,new ArenaLayout());sim.StartTraining(SpecialBoss.Archivist);
        }
        [TearDown] public void Cleanup(){sim.Dispose();Object.DestroyImmediate(settings);}
        private Enemy Boss{get=>sim.World.EntityManager.GetComponentData<Enemy>(sim.BossEntity);set=>sim.World.EntityManager.SetComponentData(sim.BossEntity,value);}
        private Entity AddShooter(float windup)
        {
            var m=sim.World.EntityManager;var entity=m.CreateEntity(typeof(Enemy),typeof(Position2D),typeof(Health));
            m.SetComponentData(entity,new Position2D{Value=sim.HeroPosition+new float2(4,0)});
            m.SetComponentData(entity,new Enemy{Kind=1,MaxHealth=100,Windup=windup,Aim=new float2(1,0)});m.SetComponentData(entity,new Health{Value=100});return entity;
        }
        [Test] public void BossWaitsForExistingWarningAndHostileProjectile()
        {
            var e=Boss;e.AttackTimer=0;Boss=e;var shooter=AddShooter(.3f);sim.Step(.01f);
            Assert.That(Boss.Pattern,Is.Zero);Assert.That(sim.Hazards,Is.Empty);
            var m=sim.World.EntityManager;m.DestroyEntity(shooter);
            var projectile=m.CreateEntity(typeof(Position2D),typeof(Bolt));m.SetComponentData(projectile,new Position2D{Value=sim.HeroPosition+new float2(5,0)});
            m.SetComponentData(projectile,new Bolt{Hostile=true,Lifetime=1});sim.Step(.01f);Assert.That(Boss.Pattern,Is.Zero);
            m.DestroyEntity(projectile);sim.Step(.01f);Assert.That(Boss.Pattern,Is.EqualTo(1));Assert.That(sim.Hazards,Is.Not.Empty);
        }
        [Test] public void RegularWarningCannotStartDuringBossPhraseButCanDuringRecovery()
        {
            var e=Boss;e.AttackTimer=0;Boss=e;sim.Step(.01f);
            var shooter=AddShooter(0);sim.Step(.01f);
            Assert.That(sim.World.EntityManager.GetComponentData<Enemy>(shooter).Windup,Is.Zero);
            sim.Hazards.Clear();e=Boss;e.Windup=0;e.Recovery=1.6f;e.AttackTimer=3;Boss=e;sim.Step(.01f);
            Assert.That(sim.World.EntityManager.GetComponentData<Enemy>(shooter).Windup,Is.GreaterThan(0));
        }
        private float Hit(float amount)
        {
            var m=sim.World.EntityManager;float before=m.GetComponentData<Health>(sim.BossEntity).Value;
            var bolt=m.CreateEntity(typeof(Position2D),typeof(Bolt));m.SetComponentData(bolt,m.GetComponentData<Position2D>(sim.BossEntity));m.SetComponentData(bolt,new Bolt{Damage=amount,Lifetime=1});sim.Step(.01f);
            return before-m.GetComponentData<Health>(sim.BossEntity).Value;
        }
        [Test] public void ExposedCoreRewardsDamageTimingAndGuardDoesNotGrantImmunity()
        {
            var e=Boss;e.Windup=1;e.AttackTimer=99;Boss=e;Assert.That(Hit(100),Is.EqualTo(55).Within(.01));
            e=Boss;e.Windup=0;e.Recovery=1.6f;Boss=e;Assert.That(Hit(100),Is.EqualTo(160).Within(.01));
            e=Boss;e.Recovery=0;Boss=e;Assert.That(Hit(100),Is.EqualTo(100).Within(.01));
        }
        [Test] public void FutureHazardRevealsBeforeDamageAndDoesNotChangeGeometry()
        {
            var h=new BossHazard{Disk=true,Origin=ArenaLayout.Project(sim.HeroPosition),Radius=2,Delay=2,Remaining=.5f,Source=3,Telegraph=2};
            Assert.That(ArenaSimulation.BossHazardVisible(h),Is.False);sim.Hazards.Add(h);
            for(int i=0;i<42;i++)sim.Step(1f/60);
            Assert.That(ArenaSimulation.BossHazardVisible(sim.Hazards[0]),Is.True);Assert.That(sim.Hazards[0].Delay,Is.GreaterThan(1.2f));
            Assert.That(sim.HeroHealth,Is.EqualTo(sim.MaxHealth));Assert.That(sim.Hazards[0].Origin,Is.EqualTo(h.Origin));
        }
        [Test] public void TrainingCrowdIsBoundedAndResetReturnsToSolo()
        {
            sim.EnableTrainingCrowd();sim.EnableTrainingCrowd();Assert.That(sim.EnemyQuery.CalculateEntityCount(),Is.EqualTo(25));
            settings.spawnPerSecond=100;settings.enemyShotDamage=0;
            for(int i=0;i<600;i++)sim.Step(1f/60);
            Assert.That(sim.EnemyQuery.CalculateEntityCount(),Is.LessThanOrEqualTo(ArenaSimulation.BossCrowdLimit));
            Assert.That(sim.TrainingCrowd,Is.True);Assert.Throws<System.InvalidOperationException>(()=>sim.Snapshot());
            sim.StartTraining(SpecialBoss.Bellkeeper);Assert.That(sim.TrainingCrowd,Is.False);Assert.That(sim.EnemyQuery.CalculateEntityCount(),Is.EqualTo(1));
        }
    }
}
