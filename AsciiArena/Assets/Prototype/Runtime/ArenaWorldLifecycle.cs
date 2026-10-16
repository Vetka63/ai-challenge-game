using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace AsciiArena
{
    public sealed partial class ArenaSimulation
    {
        // Screen-space world units, supplied by presentation/input before the first tick.
        public float2 ViewHalfExtents { get; set; } = new(16, 9);
        public int RecycledEnemies { get; private set; }
        private bool initialEnemiesPending;

        private bool TrySpawnPosition(out float2 position)
        {
            var extents = math.max(new float2(1), ViewHalfExtents) + 3;
            for (int attempt = 0; attempt < 64; attempt++)
            {
                float angle = random.NextFloat(0, math.PI * 2);
                float2 direction = new(math.cos(angle), math.sin(angle));
                var edge = direction / math.cmax(math.abs(direction) / extents);
                position = HeroPosition + ArenaLayout.ScreenToWorld(edge + direction * random.NextFloat(0, 3));
                if (Layout.Walkable(position)) return true;
            }
            position = default;
            return false;
        }

        private void MaintainLocalCrowd()
        {
            if (initialEnemiesPending)
            {
                initialEnemiesPending = false;
                for (int i = 0; i < math.min(Settings.initialEnemies, Settings.crowdLimit); i++) SpawnEnemy();
            }
            var manager = World.EntityManager;
            using var entities = EnemyQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var p = manager.GetComponentData<Position2D>(entity).Value;
                if (!math.any(math.abs(ArenaLayout.Project(p - HeroPosition)) > ViewHalfExtents + 18)) continue;
                if (!TrySpawnPosition(out var replacement)) continue;
                // Preserve identity, health and rank, including bosses. No kill or XP credit.
                manager.SetComponentData(entity, new Position2D { Value = replacement });
                var enemy = manager.GetComponentData<Enemy>(entity);
                enemy.Windup = 0; enemy.AttackTimer = 1; enemy.Knockback = 0;
                enemy.ChargeLeft = enemy.Recovery = 0;
                manager.SetComponentData(entity, enemy);
                RecycledEnemies++;
            }
        }

        private void DropExperience(float2 position, int value)
        {
            var manager = World.EntityManager;
            if (PickupQuery.CalculateEntityCount() >= math.max(1, Settings.maxExperiencePickups))
            {
                // Consolidate into the closest existing pickup; retain all earned XP in world.
                using var pickups = PickupQuery.ToEntityArray(Allocator.Temp);
                Entity closest = Entity.Null; float distance = float.MaxValue;
                foreach (var pickup in pickups)
                {
                    float d = math.distancesq(position, manager.GetComponentData<Position2D>(pickup).Value);
                    if (d < distance) { closest = pickup; distance = d; }
                }
                var experience = manager.GetComponentData<Experience>(closest);
                experience.Value += value; manager.SetComponentData(closest, experience);
                return;
            }
            var entity = manager.CreateEntity(pickupArchetype);
            manager.SetComponentData(entity, new Position2D { Value = position });
            manager.SetComponentData(entity, new Experience { Value = value });
        }
    }
}
