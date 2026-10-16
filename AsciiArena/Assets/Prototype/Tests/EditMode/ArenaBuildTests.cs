using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
namespace AsciiArena.Tests
{
    public sealed class ArenaBuildTests
    {
        private ArenaSettings settings;private ArenaSimulation sim;
        [SetUp]public void Setup(){settings=ScriptableObject.CreateInstance<ArenaSettings>();settings.initialEnemies=0;settings.spawnPerSecond=0;sim=new ArenaSimulation(settings,new ArenaLayout());}
        [TearDown]public void Cleanup(){sim.Dispose();Object.DestroyImmediate(settings);}
        private void Advance(int frames){for(int i=0;i<frames;i++)sim.Step(1f/60);}
        private Entity EnemyAt(float2 p)
        {
            var m=sim.World.EntityManager;var e=m.CreateEntity(typeof(Position2D),typeof(Enemy),typeof(Health));
            m.SetComponentData(e,new Position2D{Value=p});m.SetComponentData(e,new Enemy{Kind=2,MaxHealth=1000});m.SetComponentData(e,new Health{Value=1000});return e;
        }
        private void LevelUp()
        {
            var m=sim.World.EntityManager;var e=m.CreateEntity(typeof(Position2D),typeof(Experience));
            m.SetComponentData(e,new Position2D{Value=sim.HeroPosition});m.SetComponentData(e,new Experience{Value=sim.NextLevel});Advance(1);
        }
        private Entity BoltAt(float2 p,float2 velocity,int pierces)
        {
            var m=sim.World.EntityManager;var e=m.CreateEntity(typeof(Position2D),typeof(Bolt));
            m.SetComponentData(e,new Position2D{Value=p});m.SetComponentData(e,new Bolt{Velocity=velocity,Pierces=pierces,Damage=10,Lifetime=2});return e;
        }
        [Test]public void StarterChoiceFreezesTimeAndEnablesOnlySelectedWeapon()
        {
            sim.Input=new float2(1,0);var p=sim.HeroPosition;Advance(60);
            Assert.That(sim.Elapsed,Is.Zero);Assert.That(sim.HeroPosition,Is.EqualTo(p));Assert.That(sim.TryDash(),Is.False);
            sim.ChooseUpgrade(-1);Assert.That(sim.Phase,Is.EqualTo(RunPhase.ChoosingWeapon));
            sim.ChooseUpgrade(2);Assert.That(sim.HasWeapon(WeaponKind.Orbit),Is.True);Advance(60);
            Assert.That(sim.BoltCasts+sim.PulseCasts,Is.Zero);Assert.That(sim.Elapsed,Is.GreaterThan(0));
        }
        [Test]public void FirstLevelOffersSecondWeaponAndTwoSlotsExcludeThird()
        {
            sim.ChooseUpgrade(0);LevelUp();
            Assert.That((int)sim.Offers[0],Is.InRange(12,13));sim.ChooseUpgrade(0);
            Assert.That(sim.WeaponCount,Is.EqualTo(2));LevelUp();
            var distinct=new System.Collections.Generic.HashSet<UpgradeKind>();
            for(int i=0;i<sim.OfferCount;i++){Assert.That(sim.Eligible(sim.Offers[i]),Is.True);Assert.That((int)sim.Offers[i],Is.LessThan(11));Assert.That(distinct.Add(sim.Offers[i]),Is.True);}
        }
        [Test]public void UnownedAndMaxedUpgradesAreNotOffered()
        {
            sim.ChooseUpgrade(1);sim.UpgradeRanks[2]=5;LevelUp();
            for(int i=0;i<sim.OfferCount;i++)Assert.That(sim.Offers[i],Is.Not.EqualTo(UpgradeKind.BoltPower).And.Not.EqualTo(UpgradeKind.WideWard).And.Not.EqualTo(UpgradeKind.Pierce).And.Not.EqualTo(UpgradeKind.OrbitCount));
        }
        [Test]public void AllCapsFallBackToSingleHonestHealOffer()
        {
            sim.ChooseUpgrade(0);LevelUp();sim.ChooseUpgrade(0);
            for(int i=0;i<11;i++)sim.UpgradeRanks[i]=ArenaSimulation.RankCap((UpgradeKind)i);
            LevelUp();Assert.That(sim.OfferCount,Is.EqualTo(1));Assert.That(sim.Offers[0],Is.EqualTo(UpgradeKind.Recovery));
            sim.ChooseUpgrade(2);Assert.That(sim.Phase,Is.EqualTo(RunPhase.ChoosingUpgrade));
            sim.ChooseUpgrade(0);Assert.That(sim.Phase,Is.EqualTo(RunPhase.Running));
        }
        [Test]public void PiercingResolvesNearestTargetsAndDoesNotRepeatHits()
        {
            sim.ChooseUpgrade(1);settings.pulseDamage=0;
            var far=EnemyAt(sim.HeroPosition+new float2(3,0));var near=EnemyAt(sim.HeroPosition+new float2(1,0));var middle=EnemyAt(sim.HeroPosition+new float2(2,0));
            BoltAt(sim.HeroPosition,new float2(240,0),1);Advance(1);
            var m=sim.World.EntityManager;
            Assert.That(m.GetComponentData<Health>(near).Value,Is.EqualTo(990));Assert.That(m.GetComponentData<Health>(middle).Value,Is.EqualTo(990));Assert.That(m.GetComponentData<Health>(far).Value,Is.EqualTo(1000));
            m.DestroyEntity(sim.EnemyQuery);var target=EnemyAt(sim.HeroPosition+new float2(3,0));
            BoltAt(sim.HeroPosition+new float2(3,0),new float2(.001f,0),3);Advance(8);
            Assert.That(m.GetComponentData<Health>(target).Value,Is.EqualTo(990));
        }
        [Test]public void FanChangesProjectileCount()
        {
            sim.ChooseUpgrade(0);sim.UpgradeRanks[7]=2;EnemyAt(sim.HeroPosition+new float2(8,0));Advance(1);
            Assert.That(sim.BoltQuery.CalculateEntityCount(),Is.EqualTo(4));
        }
        [Test]public void EchoUsesCastOriginAndWaitsWhilePaused()
        {
            sim.ChooseUpgrade(1);sim.UpgradeRanks[8]=1;EnemyAt(sim.HeroPosition+new float2(2,0));Advance(1);
            Assert.That(sim.PulseCasts,Is.EqualTo(1));sim.Paused=true;Advance(120);Assert.That(sim.PulseCasts,Is.EqualTo(1));
            sim.Paused=false;Advance(22);Assert.That(sim.PulseCasts,Is.EqualTo(2));Assert.That(sim.PulseHits,Is.GreaterThanOrEqualTo(2));
            Assert.That(sim.Bursts.FindLast(b=>b.Kind==1).Position,Is.EqualTo(new float2(2,0)));
        }
        [Test]public void OrbitDamagesEntitiesAndRespectsPerTargetInterval()
        {
            sim.ChooseUpgrade(2);var e=EnemyAt(sim.OrbitPosition(0,1f/60));Advance(1);
            Assert.That(sim.OrbitHits,Is.EqualTo(1));Assert.That(sim.World.EntityManager.GetComponentData<Health>(e).Value,Is.EqualTo(1000-settings.orbitDamage));
            Advance(5);Assert.That(sim.OrbitHits,Is.EqualTo(1));
        }
        [Test]public void DashIsBoundedFreezesWithPauseAndResets()
        {
            sim.ChooseUpgrade(0);sim.Input=new float2(1,1);var p=sim.HeroPosition;
            Assert.That(sim.TryDash(),Is.True);Assert.That(sim.TryDash(),Is.False);Advance(9);
            float expected=settings.heroSpeed*(.15f+settings.dashDuration*(settings.dashSpeedMultiplier-1));
            Assert.That(math.distance(ArenaLayout.Project(p),ArenaLayout.Project(sim.HeroPosition)),Is.EqualTo(expected).Within(.001f));
            sim.Paused=true;float cooldown=sim.DashCooldownRemaining;Advance(60);Assert.That(sim.DashCooldownRemaining,Is.EqualTo(cooldown));
            sim.Reset();Assert.That(sim.DashRemaining+sim.DashCooldownRemaining,Is.Zero);Assert.That(sim.WeaponCount,Is.Zero);Assert.That(sim.UpgradeRanks[7],Is.Zero);
        }
        [Test]public void DashCannotTunnelThroughPondOrPillar()
        {
            sim.ChooseUpgrade(0);var m=sim.World.EntityManager;
            m.SetComponentData(sim.HeroEntity,new Position2D{Value=new float2(-9,-5.3f)});sim.Input=new float2(0,-1);sim.TryDash();Advance(12);
            Assert.That(ArenaLayout.Water(sim.HeroPosition),Is.False);
            sim.Reset();sim.ChooseUpgrade(0);m.SetComponentData(sim.HeroEntity,new Position2D{Value=new float2(-8.2f,6)});sim.Input=new float2(1,0);sim.TryDash();Advance(12);
            Assert.That(sim.HeroPosition.x,Is.LessThan(-7.8f));
        }
        [Test]public void DamageSourceAndProtectionAreReportedOnce()
        {
            sim.ChooseUpgrade(0);EnemyAt(sim.HeroPosition);Advance(1);
            Assert.That(sim.HurtCount,Is.EqualTo(1));Assert.That(sim.LastDamage,Is.GreaterThan(0));
            Advance(5);Assert.That(sim.HurtCount,Is.EqualTo(1));Assert.That(sim.HurtFlash,Is.GreaterThan(0));
        }
        [Test]public void DashProtectionEndsWithDashAndCannotStartInChoice()
        {
            sim.ChooseUpgrade(0);sim.Input=new float2(1,0);sim.TryDash();
            var m=sim.World.EntityManager;
            var target=EnemyAt(sim.HeroPosition+new float2(.2f,0));Advance(1);Assert.That(sim.HurtCount,Is.Zero);
            Advance(9);m.SetComponentData(target,new Position2D{Value=sim.HeroPosition});Advance(1);Assert.That(sim.HurtCount,Is.EqualTo(1));
            LevelUp();Assert.That(sim.TryDash(),Is.False);
        }
        [Test]public void OrbitSpeedUpgradeDoesNotTeleportShards()
        {
            sim.ChooseUpgrade(2);Advance(90);var position=sim.OrbitPosition(0,sim.Elapsed);
            sim.UpgradeRanks[1]++;Assert.That(math.distance(position,sim.OrbitPosition(0,sim.Elapsed)),Is.LessThan(.00001f));
        }
    }
}
