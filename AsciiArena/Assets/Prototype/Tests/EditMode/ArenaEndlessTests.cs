using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace AsciiArena.Tests
{
    public sealed class ArenaEndlessTests
    {
        private ArenaSettings settings;
        private ArenaSimulation sim;
        [SetUp] public void Setup()
        {
            settings=ScriptableObject.CreateInstance<ArenaSettings>();
            settings.initialEnemies=0;settings.spawnPerSecond=0;
            settings.boltDamage=settings.pulseDamage=settings.contactDamage=settings.enemyShotDamage=0;
            sim=new ArenaSimulation(settings,new ArenaLayout());sim.ChooseUpgrade(0);
        }
        [TearDown] public void Cleanup(){sim.Dispose();Object.DestroyImmediate(settings);}
        private void Advance(float seconds){for(int i=0;i<math.ceil(seconds*60);i++)sim.Step(1f/60);}
        private void Kill(Entity enemy)
        {
            var m=sim.World.EntityManager;var bolt=m.CreateEntity(typeof(Position2D),typeof(Bolt));
            m.SetComponentData(bolt,m.GetComponentData<Position2D>(enemy));
            m.SetComponentData(bolt,new Bolt{Damage=100000,Lifetime=1});sim.Step(1f/60);
        }

        [Test] public void HeroCrossesOldBoundariesInAllFourDirections()
        {
            foreach(var direction in new[]{new float2(1,0),new float2(-1,0),new float2(0,1),new float2(0,-1)})
            {
                sim.Reset();sim.ChooseUpgrade(0);
                sim.World.EntityManager.SetComponentData(sim.HeroEntity,new Position2D{Value=0});sim.Input=direction;
                Advance(40);
                Assert.That(math.distance(ArenaLayout.Project(sim.HeroPosition),direction*settings.heroSpeed*40),Is.LessThan(.04f));
            }
        }
        [Test] public void NegativeChunksAndGeneratedObstaclesAgreeWithCollision()
        {
            Assert.That(ArenaLayout.ChunkAt(new float2(-22.01f,-16.01f)),Is.EqualTo(new int2(-1,-1)));
            Assert.That(ArenaLayout.ChunkAt(new float2(-22,-16)),Is.EqualTo(new int2(0)));
            for(int y=-20;y<=20;y++)for(int x=-20;x<=20;x++)
            {
                var chunk=new int2(x,y);var center=ArenaLayout.ChunkCenter(chunk);
                Assert.That(ArenaLayout.ChunkAt(center),Is.EqualTo(chunk));
                Assert.That(sim.Layout.Walkable(center),Is.True,"Safe connected chunk centers");
                for(int i=0;i<4;i++)Assert.That(sim.Layout.Walkable(sim.Layout.PillarAt(chunk,i)),Is.False);
                Assert.That(ArenaLayout.Water(ArenaLayout.PondCenter(chunk)),Is.EqualTo(ArenaLayout.HasPond(chunk)));
                Assert.That(ArenaLayout.Variant(chunk),Is.EqualTo(ArenaLayout.Variant(chunk)));
            }
        }
        [TestCase(16,9)] [TestCase(32,9)] [TestCase(9,16)]
        public void InitialCrowdSpawnsOutsideActualViewport(float width,float height)
        {
            settings.initialEnemies=40;sim.Reset();sim.ViewHalfExtents=new float2(width,height);sim.ChooseUpgrade(0);sim.Step(1f/60);
            using var positions=sim.EnemyQuery.ToComponentDataArray<Position2D>(Allocator.Temp);
            Assert.That(positions.Length,Is.EqualTo(40));
            foreach(var p in positions)
                Assert.That(math.any(math.abs(ArenaLayout.Project(p.Value-sim.HeroPosition))>sim.ViewHalfExtents+2.8f),Is.True);
        }
        [Test] public void DistantBossRetainsIdentityAndHealthWhenRecycled()
        {
            settings.bossAt=1;Advance(1.1f);var boss=sim.BossEntity;var m=sim.World.EntityManager;
            m.SetComponentData(boss,new Health{Value=731});
            m.SetComponentData(sim.HeroEntity,new Position2D{Value=new float2(880,0)});sim.Step(1f/60);
            Assert.That(sim.BossEntity,Is.EqualTo(boss));Assert.That(m.GetComponentData<Health>(boss).Value,Is.EqualTo(731));
            Assert.That(sim.RecycledEnemies,Is.EqualTo(1));Assert.That(sim.Kills,Is.Zero);
            Assert.That(sim.PickupQuery.CalculateEntityCount(),Is.Zero);
            var offset=math.abs(ArenaLayout.Project(m.GetComponentData<Position2D>(boss).Value-sim.HeroPosition));
            Assert.That(math.any(offset>sim.ViewHalfExtents+2.8f),Is.True);
            Assert.That(math.all(offset<sim.ViewHalfExtents+7),Is.True);
        }
        [Test] public void PickupConsolidationBoundsEntitiesWithoutLosingExperience()
        {
            settings.maxExperiencePickups=2;var m=sim.World.EntityManager;
            for(int i=0;i<8;i++)
            {
                var enemy=m.CreateEntity(typeof(Position2D),typeof(Enemy),typeof(Health));
                m.SetComponentData(enemy,new Position2D{Value=new float2(10+i*.1f,0)});
                m.SetComponentData(enemy,new Enemy{Kind=2,MaxHealth=1});m.SetComponentData(enemy,new Health{Value=1});Kill(enemy);
            }
            Assert.That(sim.PickupQuery.CalculateEntityCount(),Is.EqualTo(2));
            using var xp=sim.PickupQuery.ToComponentDataArray<Experience>(Allocator.Temp);int total=0;foreach(var p in xp)total+=p.Value;
            Assert.That(total,Is.EqualTo(24));Assert.That(sim.ExperiencePoints,Is.Zero);Assert.That(sim.Kills,Is.EqualTo(8));
        }
        [Test] public void ActiveWaveBossBlocksFinalUntilDefeatedAndCannotBeReplaced()
        {
            settings.waveBossInterval=1;settings.bossAt=3;settings.runDuration=10;settings.crowdLimit=1;settings.eliteInterval=1;
            sim.Reset();sim.ChooseUpgrade(0);Advance(3.5f);
            Assert.That(sim.WaveBossesSpawned,Is.EqualTo(1));Assert.That(sim.BossSpawned,Is.False);
            var first=sim.BossEntity;Assert.That(sim.BossHealthRatio,Is.EqualTo(1));Kill(first);
            Assert.That(sim.WaveBossesDefeated,Is.EqualTo(1));Assert.That(sim.Phase,Is.EqualTo(RunPhase.Running));
            Assert.That(sim.BossSpawned,Is.True);Assert.That(sim.BossEntity,Is.Not.EqualTo(first));
            Assert.That(sim.EnemyQuery.CalculateEntityCount(),Is.EqualTo(1));Kill(sim.BossEntity);
            Assert.That(sim.Phase,Is.EqualTo(RunPhase.Victory));
            sim.Reset();Assert.That(sim.WaveBossesDefeated,Is.Zero);Assert.That(sim.RecycledEnemies,Is.Zero);Assert.That(sim.BossActive,Is.False);
        }
        [Test] public void DefaultScheduleHasFourGuardsThenFinalVictory()
        {
            for(int i=0;i<600*60&&!sim.Finished;i++)
            {
                sim.Step(1f/60);
                if(sim.BossActive)Kill(sim.BossEntity);
            }
            Assert.That(sim.WaveBossesSpawned,Is.EqualTo(4));Assert.That(sim.WaveBossesDefeated,Is.EqualTo(4));
            Assert.That(sim.Phase,Is.EqualTo(RunPhase.Victory));Assert.That(sim.Elapsed,Is.InRange(540,541));
        }
    }
}
