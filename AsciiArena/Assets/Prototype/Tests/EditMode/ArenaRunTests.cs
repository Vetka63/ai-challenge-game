using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
namespace AsciiArena.Tests
{
    public sealed class ArenaRunTests
    {
        private ArenaSettings settings; private ArenaSimulation sim;
        [SetUp] public void Setup()
        { settings=ScriptableObject.CreateInstance<ArenaSettings>();settings.initialEnemies=0;settings.spawnPerSecond=0;sim=new ArenaSimulation(settings,new ArenaLayout());sim.ChooseUpgrade(0); }
        [TearDown] public void Cleanup(){sim.Dispose();Object.DestroyImmediate(settings);}
        private Entity EnemyAt(float2 p, byte kind=0,float hp=1000)
        {
            var m=sim.World.EntityManager;var e=m.CreateEntity(typeof(Enemy),typeof(Position2D),typeof(Health));
            m.SetComponentData(e,new Position2D{Value=p});m.SetComponentData(e,new Enemy{Kind=kind,MaxHealth=hp});m.SetComponentData(e,new Health{Value=hp});return e;
        }
        private void Advance(int frames){for(int i=0;i<frames;i++)sim.Step(1f/60);}
        [Test] public void ExperienceOpensChoiceAndFreezesWorldUntilSelection()
        {
            var m=sim.World.EntityManager;var xp=m.CreateEntity(typeof(Position2D),typeof(Experience));
            m.SetComponentData(xp,new Position2D{Value=sim.HeroPosition});m.SetComponentData(xp,new Experience{Value=settings.firstLevelExperience});
            Advance(1);Assert.That(sim.Phase,Is.EqualTo(RunPhase.ChoosingUpgrade));Assert.That(sim.Level,Is.EqualTo(2));
            float time=sim.Elapsed;sim.Input=new float2(1,0);var p=sim.HeroPosition;Advance(60);
            Assert.That(sim.Elapsed,Is.EqualTo(time));Assert.That(sim.HeroPosition,Is.EqualTo(p));
            sim.ChooseUpgrade(-1);Assert.That(sim.Phase,Is.EqualTo(RunPhase.ChoosingUpgrade));
            var offer=sim.Offers[1];sim.ChooseUpgrade(1);
            if((int)offer<11)Assert.That(sim.UpgradeRanks[(int)offer],Is.EqualTo(1));
            else Assert.That(sim.HasWeapon((WeaponKind)((int)offer-11)),Is.True);
            Advance(1);Assert.That(sim.Elapsed,Is.GreaterThan(time));
        }
        [Test] public void FourDirectionsPersistWhenStopped()
        {
            var dirs=new[]{new float2(0,-1),new float2(0,1),new float2(-1,0),new float2(1,0)};
            for(byte i=0;i<4;i++)
            {
                sim.Input=dirs[i];Advance(1);var pose=sim.World.EntityManager.GetComponentData<Hero>(sim.HeroEntity);
                Assert.That(pose.Facing,Is.EqualTo(i));Assert.That(pose.Moving,Is.True);
                sim.Input=0;Advance(1);pose=sim.World.EntityManager.GetComponentData<Hero>(sim.HeroEntity);
                Assert.That(pose.Facing,Is.EqualTo(i));Assert.That(pose.Moving,Is.False);
            }
        }
        [Test] public void ShooterTelegraphsBeforeProducingHostileProjectile()
        {
            var e=EnemyAt(sim.HeroPosition+new float2(6,0),1);Advance(1);
            Assert.That(sim.World.EntityManager.GetComponentData<Enemy>(e).Windup,Is.GreaterThan(.5f));
            Advance(50);using var bolts=sim.BoltQuery.ToComponentDataArray<Bolt>(Allocator.Temp);
            bool hostile=false;foreach(var b in bolts)hostile|=b.Hostile;Assert.That(hostile,Is.True);
        }
        [Test] public void FastHostileProjectileUsesSweptCollisionAndInvulnerability()
        {
            var m=sim.World.EntityManager;
            for(int i=0;i<2;i++)
            {
                var b=m.CreateEntity(typeof(Position2D),typeof(Bolt));
                m.SetComponentData(b,new Position2D{Value=sim.HeroPosition-new float2(1,0)});
                m.SetComponentData(b,new Bolt{Velocity=new float2(120,0),Lifetime=1,Damage=8,Hostile=true});
            }
            Advance(1);Assert.That(sim.HeroHealth,Is.EqualTo(settings.heroHealth-8));
        }
        [Test] public void BossSpawnsOnceAndVictoryStopsSimulation()
        {
            settings.bossAt=1;settings.runDuration=20;settings.boltDamage=settings.pulseDamage=0;settings.crowdLimit=1;
            Advance(90);Assert.That(sim.BossSpawned,Is.True);Assert.That(sim.EnemyQuery.CalculateEntityCount(),Is.EqualTo(1));
            var m=sim.World.EntityManager;var e=sim.BossEntity;
            m.SetComponentData(e,new Position2D{Value=sim.HeroPosition+new float2(2,0)});
            m.SetComponentData(e,new Health{Value=1});settings.boltDamage=100;Advance(120);
            Assert.That(sim.Phase,Is.EqualTo(RunPhase.Victory));float time=sim.Elapsed;Advance(120);Assert.That(sim.Elapsed,Is.EqualTo(time));
            sim.Reset();Assert.That(sim.BossSpawned,Is.False);Assert.That(sim.Phase,Is.EqualTo(RunPhase.ChoosingWeapon));
            Assert.That(sim.PickupQuery.CalculateEntityCount(),Is.Zero);Assert.That(sim.Level,Is.EqualTo(1));
        }
        [Test] public void DeadlineAndDeathEndTheRun()
        {
            settings.runDuration=1;settings.bossAt=99;Advance(70);Assert.That(sim.Phase,Is.EqualTo(RunPhase.Defeat));
            sim.Reset();sim.ChooseUpgrade(0);sim.World.EntityManager.SetComponentData(sim.HeroEntity,new Health{Value=0});Advance(1);
            Assert.That(sim.Phase,Is.EqualTo(RunPhase.Defeat));Assert.That(sim.Elapsed,Is.Zero);
        }
        [Test] public void GridMatchesBruteForceAtCellBoundaries()
        {
            var grid=new SeparationGrid();var points=new[]{new float2(-.01f,.99f),new float2(.01f,1.01f),new float2(.5f,.8f),new float2(3,4)};
            foreach(var p in points)grid.Add(p);
            foreach(var p in points)
            {
                float2 expected=0;foreach(var q in points){var d=p-q;float sq=math.lengthsq(d);if(sq>.001f&&sq<.65f)expected+=d*(.65f-sq)*2;}
                Assert.That(math.distance(grid.At(p),expected),Is.LessThan(.00001f));
            }
            grid.Clear();Assert.That(math.lengthsq(grid.At(points[0])),Is.Zero);
        }
        [Test] public void ActorArtUsesPrintableAsciiAndFourDifferentFacings()
        {
            Assert.That(AsciiActorArt.Idle.Length,Is.EqualTo(4));
            for(int d=0;d<4;d++)
            {
                Assert.That(AsciiActorArt.Hover[d].Length,Is.EqualTo(4));
                int staffColumn=d==1||d==2?1:9;
                foreach(var frame in AsciiActorArt.Hover[d])Assert.That(frame[7][staffColumn],Is.EqualTo('|'),"Hovering must preserve the staff's length");
                foreach(var row in AsciiActorArt.Idle[d])foreach(char c in row)Assert.That((int)c,Is.InRange(32,126));
                Assert.That(string.Join("\n",AsciiActorArt.Hover[d][0]),Is.Not.EqualTo(string.Join("\n",AsciiActorArt.Hover[d][2])));
            }
            Assert.That(string.Join("",AsciiActorArt.Idle[0]),Is.Not.EqualTo(string.Join("",AsciiActorArt.Idle[1])));
        }
    }
}
