using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace AsciiArena.Tests
{
    public sealed class ArenaSimulationTests
    {
        private ArenaSettings settings;
        private ArenaSimulation simulation;
        [SetUp] public void Setup()
        {
            settings = ScriptableObject.CreateInstance<ArenaSettings>();
            settings.initialEnemies = 0;
            settings.spawnPerSecond = 0;
            simulation = new ArenaSimulation(settings, new ArenaLayout());
            simulation.ChooseUpgrade(0);
        }
        [TearDown] public void Cleanup() { simulation.Dispose(); Object.DestroyImmediate(settings); }

        [Test] public void BothAutoAttacksDamageRealEntities()
        {
            var manager = simulation.World.EntityManager;
            var xp=manager.CreateEntity(typeof(Position2D),typeof(Experience));
            manager.SetComponentData(xp,new Position2D{Value=simulation.HeroPosition});manager.SetComponentData(xp,new Experience{Value=simulation.NextLevel});
            simulation.Step(1f/60);simulation.Offers[0]=UpgradeKind.LearnWard;simulation.ChooseUpgrade(0);
            var enemy = manager.CreateEntity(typeof(Position2D), typeof(Enemy), typeof(Health));
            manager.SetComponentData(enemy, new Position2D { Value = simulation.HeroPosition + new float2(2, 0) });
            manager.SetComponentData(enemy, new Enemy { Kind = 2, Speed = 0 });
            manager.SetComponentData(enemy, new Health { Value = 1000 });
            for (int i = 0; i < 90; i++) simulation.Step(1f / 60);
            Assert.That(simulation.BoltHits, Is.GreaterThan(0));
            Assert.That(simulation.PulseHits, Is.GreaterThan(0));
            Assert.That(manager.GetComponentData<Health>(enemy).Value, Is.LessThan(1000));
        }
        [Test] public void CrowdNeverExceedsConfiguredLimit()
        {
            settings.crowdLimit = 45; settings.spawnPerSecond = 100;
            for (int i = 0; i < 600; i++) simulation.Step(1f / 60);
            Assert.That(simulation.EnemyQuery.CalculateEntityCount(), Is.InRange(1, 45));
        }
        [Test] public void PauseAndResetDoNotLeakThePreviousRun()
        {
            settings.spawnPerSecond = 10;
            for (int i = 0; i < 120; i++) simulation.Step(1f / 60);
            float time = simulation.Elapsed;
            simulation.Paused = true; simulation.Step(1);
            Assert.That(simulation.Elapsed, Is.EqualTo(time));
            simulation.Reset();
            Assert.That(simulation.Elapsed, Is.Zero);
            Assert.That(simulation.EnemyQuery.CalculateEntityCount(), Is.Zero);
            Assert.That(simulation.BoltQuery.CalculateEntityCount(), Is.Zero);
            Assert.That(simulation.HeroHealth, Is.EqualTo(settings.heroHealth));
        }
        [Test] public void HeroCannotWalkThroughThePond()
        {
            var manager = simulation.World.EntityManager;
            manager.SetComponentData(simulation.HeroEntity, new Position2D { Value = new float2(-9, -5.3f) });
            simulation.Input = new float2(0, -1);
            for (int i = 0; i < 120; i++) simulation.Step(1f / 60);
            Assert.That(ArenaLayout.Water(simulation.HeroPosition), Is.False);
            Assert.That(simulation.HeroPosition.y, Is.GreaterThanOrEqualTo(-5.5f));
        }
        [Test] public void MovementHasEqualScreenSpeedAndNoDiagonalBoost()
        {
            foreach(var direction in new[]{new float2(1,0),new float2(0,1),new float2(1,1)})
            {
                simulation.Reset();
                simulation.ChooseUpgrade(0);
                var start=simulation.HeroPosition;
                simulation.Input=direction;
                for(int i=0;i<30;i++)simulation.Step(1f/60);
                var displacement=ArenaLayout.Project(simulation.HeroPosition-start);
                Assert.That(math.length(displacement),Is.EqualTo(settings.heroSpeed*.5f).Within(.001f));
                Assert.That(math.distance(math.normalize(displacement),math.normalize(direction)),Is.LessThan(.001f));
                simulation.Input=0;
                var stopped=simulation.HeroPosition;
                simulation.Step(1f/60);
                Assert.That(math.distance(stopped,simulation.HeroPosition),Is.Zero,"No gameplay inertia after releasing input");
            }
        }
        [Test] public void DemoDoesNotOvershootItsMovingTarget()
        {
            settings.heroSpeed=1000;
            simulation.DemoMode=true;
            for(int i=0;i<30;i++)
            {
                simulation.Step(1f/60);
                var target=new float2(7*math.sin(simulation.Elapsed*.24f),4*math.cos(simulation.Elapsed*.32f));
                Assert.That(math.distance(simulation.HeroPosition,target),Is.LessThan(.001f));
            }
        }
    }
}
