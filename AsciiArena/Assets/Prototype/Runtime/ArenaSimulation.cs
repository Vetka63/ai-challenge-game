using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Zenject;

namespace AsciiArena
{
    // A scene-owned ECS world. Unity presentation reads it without creating views per entity.
    public sealed partial class ArenaSimulation : ITickable, IDisposable
    {
        public readonly World World;
        public readonly ArenaSettings Settings;
        public readonly ArenaLayout Layout;
        public readonly List<VisualBurst> Bursts = new();
        public Entity HeroEntity { get; private set; }
        public float Elapsed { get; private set; }
        public int Kills { get; private set; }
        public int BoltCasts { get; private set; }
        public int PulseCasts { get; private set; }
        public int BoltHits { get; private set; }
        public int PulseHits { get; private set; }
        public bool Paused;
        public bool DemoMode;
        public bool HighContrast;
        public int PlaybackSpeed { get; private set; } = 1;
        public void SetPlaybackSpeed(int value) => PlaybackSpeed = value == 2 || value == 4 ? value : 1;
        public float2 Input;
        public readonly EntityQuery EnemyQuery;
        public readonly EntityQuery BoltQuery;
        public readonly EntityQuery PickupQuery;
        public float2 HeroPosition => World.EntityManager.GetComponentData<Position2D>(HeroEntity).Value;
        public float HeroHealth => World.EntityManager.GetComponentData<Health>(HeroEntity).Value;
        public bool Dead => HeroHealth <= 0;

        private Unity.Mathematics.Random random;
        private float spawnBudget, boltTimer, pulseTimer, invulnerability, accumulator;
        private readonly EntityArchetype enemyArchetype, boltArchetype, pickupArchetype;
        private readonly SeparationGrid separation = new();

        public ArenaSimulation(ArenaSettings settings, ArenaLayout layout)
        {
            Settings = settings;
            Layout = layout;
            World = new World("ASCII Arena simulation");
            var manager = World.EntityManager;
            enemyArchetype = manager.CreateArchetype(typeof(Position2D), typeof(Enemy), typeof(Health));
            boltArchetype = manager.CreateArchetype(typeof(Position2D), typeof(Bolt));
            pickupArchetype = manager.CreateArchetype(typeof(Position2D), typeof(Experience));
            EnemyQuery = manager.CreateEntityQuery(typeof(Enemy), typeof(Position2D), typeof(Health));
            BoltQuery = manager.CreateEntityQuery(typeof(Bolt), typeof(Position2D));
            PickupQuery = manager.CreateEntityQuery(typeof(Experience), typeof(Position2D));
            Reset();
        }

        public void Reset()
        {
            var manager = World.EntityManager;
            using (var query = manager.CreateEntityQuery(ComponentType.ReadOnly<Position2D>())) manager.DestroyEntity(query);
            HeroEntity = manager.CreateEntity(typeof(Position2D), typeof(Health), typeof(Hero));
            manager.SetComponentData(HeroEntity, new Position2D { Value = new float2(2, 0) });
            manager.SetComponentData(HeroEntity, new Health { Value = Settings.heroHealth });
            random = new Unity.Mathematics.Random((uint)math.max(1, Settings.seed));
            Elapsed = spawnBudget = boltTimer = pulseTimer = invulnerability = accumulator = 0;
            Kills = BoltCasts = PulseCasts = BoltHits = PulseHits = 0;
            Bursts.Clear();
            Paused = false;
            ResetRun();
            initialEnemiesPending = true; RecycledEnemies = 0;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Opt-in capture drives the same fixed steps; ordinary builds never set this.
        public bool DiagnosticManualTicks;
#endif
        public void Tick()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(DiagnosticManualTicks)return;
#endif
            AdvanceFrame(Time.unscaledDeltaTime);
        }

        // Speed changes the number of fixed simulation steps, never their size.
        public void AdvanceFrame(float realDelta)
        {
            if (Paused || Phase != RunPhase.Running) { accumulator = 0; return; }
            accumulator = math.min(accumulator + math.clamp(realDelta, 0, .12f) * PlaybackSpeed, .5f);
            while (accumulator + .000001f >= 1f / 60)
            {
                Step(1f / 60);
                accumulator = math.max(0, accumulator - 1f / 60);
                if (Paused || Phase != RunPhase.Running) { accumulator = 0; break; }
            }
            if (accumulator < .000001f) accumulator = 0;
        }

        public void Step(float delta)
        {
            if (Paused || Phase != RunPhase.Running || delta <= 0) return;
            if (Dead) { Phase = RunPhase.Defeat; return; }
            delta = math.min(delta, .1f);
            Elapsed += delta;
            orbitAngle=math.fmod(orbitAngle+delta*OrbitSpeed,math.PI*2);
            invulnerability -= delta;
            DashCooldownRemaining=math.max(0,DashCooldownRemaining-delta);
            var manager = World.EntityManager;
            float2 hero = HeroPosition;
            float2 direction = Input;
            float travel = HeroSpeed * delta;
            if (DemoMode)
            {
                var destination = new float2(7 * math.sin(Elapsed * .24f), 4 * math.cos(Elapsed * .32f));
                var toTarget = ArenaLayout.Project(destination - hero);
                direction = math.normalizesafe(toTarget);
                // The moving target can be closer than a full step. Overshooting it flips
                // facing every tick, which looks like broken animation in the demo.
                travel = math.min(travel, math.length(toTarget));
            }
            if(DashRemaining>0)
            {
                direction=dashDirection;
                travel=HeroSpeed*(delta+math.min(delta,DashRemaining)*(math.max(1,Settings.dashSpeedMultiplier)-1));
            }
            var oldHero = hero;
            // Input is screen-relative: vertical projection must not slow W/S or skew diagonals.
            var movementDelta=ArenaLayout.ScreenToWorld(math.normalizesafe(direction)*travel);
            int movementSteps=math.max(1,(int)math.ceil(math.length(movementDelta)/.12f));
            for(int sub=0;sub<movementSteps;sub++)hero=Layout.Move(hero,movementDelta/movementSteps);
            manager.SetComponentData(HeroEntity, new Position2D { Value = hero });
            var pose = manager.GetComponentData<Hero>(HeroEntity);
            pose.Moving = math.distancesq(hero, oldHero) > .000001f;
            if (pose.Moving)
            {
                pose.Facing = math.abs(direction.x) > math.abs(direction.y) ? (byte)(direction.x > 0 ? 3 : 2) : (byte)(direction.y > 0 ? 1 : 0);
                pose.WalkCycle += math.distance(hero, oldHero) * 1.8f;
            }
            manager.SetComponentData(HeroEntity, pose);

            UpdateHazards(delta);

            MaintainLocalCrowd();
            using var enemies = EnemyQuery.ToEntityArray(Allocator.Temp);
            using var positions = EnemyQuery.ToComponentDataArray<Position2D>(Allocator.Temp);
            separation.Clear(); foreach (var position in positions) separation.Add(position.Value);
            PrepareAttackBudget(enemies);
            float2 closest = hero + new float2(0, 1);
            float closestDistance = float.MaxValue;
            for (int i = 0; i < enemies.Length; i++)
            {
                var enemy = manager.GetComponentData<Enemy>(enemies[i]);
                float2 p = positions[i].Value;
                float distance = math.distancesq(p, hero);
                if (distance < closestDistance) { closestDistance = distance; closest = p; }
                float2 chase = math.normalizesafe(hero - p);
                float move = EnemyAttack(ref enemy, p, hero, delta);
                float2 movement = (math.normalizesafe(chase * move + separation.At(p)) * enemy.Speed + enemy.Knockback) * delta;
                bool bossCharge = enemy.Boss && Settings.actOne && enemy.ChargeLeft > 0;
                float2 moved = enemy.Boss && Settings.actOne ? MoveBoss(ref enemy, p, hero, delta) : enemy.Kind >= 3 && !enemy.Boss ? MoveTacticalEnemy(ref enemy, p, hero, delta) : Layout.Move(p, movement);
                if (!enemy.Boss && enemy.Kind < 3 && math.distancesq(moved, p) < .000001f && move != 0 && enemy.Windup <= 0)
                    moved = Layout.Move(p, new float2(-chase.y, chase.x) * enemy.Speed * delta);
                manager.SetComponentData(enemies[i], new Position2D { Value = moved });
                enemy.HitFlash = math.max(0, enemy.HitFlash - delta);
                enemy.Knockback *= math.max(0, 1 - delta * 9);
                manager.SetComponentData(enemies[i], enemy);
                float radius = enemy.Boss ? 1.1f : enemy.Kind == 2 ? .65f : .48f;
                if (!bossCharge && math.distancesq(moved, hero) < radius * radius) Hurt(Settings.contactDamage * (enemy.Kind == 2 ? 1.5f : 1),moved);
            }
            if (Dead) { Phase = RunPhase.Defeat; return; }

            boltTimer -= delta;
            if (HasWeapon(WeaponKind.Runebolt) && boltTimer <= 0 && enemies.Length > 0)
            {
                boltTimer = BoltCooldown;
                float2 aim = math.normalizesafe(closest - hero, new float2(0, 1));
                // Centered volley; extra projectiles visibly widen the build's coverage.
                for(int shot=0;shot<BoltCount;shot++)
                {
                    float angle=(shot-(BoltCount-1)*.5f)*.24f;
                    FireBolt(hero,new float2(aim.x*math.cos(angle)-aim.y*math.sin(angle),aim.x*math.sin(angle)+aim.y*math.cos(angle)));
                }
                BoltCasts++;
            }
            pulseTimer -= delta;
            if (HasWeapon(WeaponKind.Ward) && pulseTimer <= 0)
            {
                pulseTimer = PulseCooldown;CastWard(hero,enemies);
                if(UpgradeRanks[(int)UpgradeKind.Echo]>0){echoAt=Elapsed+.35f;echoPosition=hero;}
            }
            if(echoAt>=0&&Elapsed>=echoAt){CastWard(echoPosition,enemies,.6f);echoAt=-1;}
            UpdateOrbit(delta,enemies,oldHero);

            using var bolts = BoltQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in bolts)
            {
                var bolt = manager.GetComponentData<Bolt>(entity);
                float2 old = manager.GetComponentData<Position2D>(entity).Value;
                float2 p = old + bolt.Velocity * delta;
                bolt.Lifetime -= delta;
                bool hit = false;
                if (bolt.Hostile && SegmentDistanceSquared(hero, old, p) < .18f) { Hurt(bolt.Damage,old); hit = true; }
                if (!bolt.Hostile)
                {
                    // Resolve intersections in travel order, independent of ECS storage order.
                    // Record hit entities so a piercing bolt cannot damage the same target next tick.
                    for(int pass=0;pass<4;pass++)
                    {
                        Entity nearest=Entity.Null;float nearestT=float.MaxValue;
                        foreach(var target in enemies)
                        {
                            if(!manager.Exists(target)||bolt.HitEntities.Contains(target))continue;
                            float2 enemyPosition=manager.GetComponentData<Position2D>(target).Value;
                            float2 segment=p-old;
                            float t=math.saturate(math.dot(enemyPosition-old,segment)/math.max(.00001f,math.lengthsq(segment)));
                            var targetEnemy=manager.GetComponentData<Enemy>(target);
                            if(math.distancesq(enemyPosition,old+segment*t)>(targetEnemy.Boss?.8f:.3f)||t>=nearestT)continue;
                            nearest=target;nearestT=t;
                        }
                        if(nearest==Entity.Null)break;
                        bolt.HitEntities.Add(nearest);Damage(nearest,bolt.Damage);BoltHits++;
                        if(bolt.Pierces<=0){hit=true;break;}
                        bolt.Pierces--;
                    }
                }
                if (hit || bolt.Lifetime <= 0 || !Layout.Walkable(p)) manager.DestroyEntity(entity);
                else
                {
                    manager.SetComponentData(entity, new Position2D { Value = p });
                    manager.SetComponentData(entity, bolt);
                }
            }
            DashRemaining=math.max(0,DashRemaining-delta);
            if (Dead) { Phase = RunPhase.Defeat; return; }
            if (Phase == RunPhase.Victory) return;
            UpdateRun(delta);
            if (Phase != RunPhase.Running) return;
            spawnBudget += Training && !TrainingCrowd ? 0 : delta * Settings.spawnPerSecond * math.lerp(1, Settings.finalSpawnMultiplier, math.saturate(Elapsed / math.max(1, Settings.bossAt))) * (Settings.actOne && (BossActive || CoreTransition > 0) ? .10f : 1);
            while (spawnBudget >= 1)
            {
                if (EnemyQuery.CalculateEntityCount() < (Settings.actOne && BossActive ? math.min(Settings.crowdLimit, BossCrowdLimit) : Settings.crowdLimit)) SpawnEnemy();
                spawnBudget -= 1;
            }
            Bursts.RemoveAll(burst => Elapsed - burst.Time > .7f);
        }

        private void FireBolt(float2 position, float2 direction, bool hostile = false, float speed = 5.5f)
        {
            var manager = World.EntityManager;
            var entity = manager.CreateEntity(boltArchetype);
            manager.SetComponentData(entity, new Position2D { Value = position });
            manager.SetComponentData(entity, new Bolt { Velocity = direction * (hostile ? speed : Settings.boltSpeed), Lifetime = hostile ? 4 : 1.4f, Hostile = hostile, Damage = hostile ? Settings.enemyShotDamage : BoltDamage, Pierces=hostile?0:UpgradeRanks[(int)UpgradeKind.Pierce] });
        }

        private void SpawnEnemy(bool elite = false, bool boss = false, bool finalBoss = true)
        {
            if (!TrySpawnPosition(out var position)) return;
            var manager = World.EntityManager;
            if (EnemyQuery.CalculateEntityCount() >= math.max(1, Settings.crowdLimit))
            {
                if (!elite) return;
                using var existing = EnemyQuery.ToEntityArray(Allocator.Temp);
                Entity replace = Entity.Null; float farthest = -1;
                foreach (var candidate in existing)
                {
                    if (manager.GetComponentData<Enemy>(candidate).Boss) continue;
                    float distance = math.distancesq(HeroPosition, manager.GetComponentData<Position2D>(candidate).Value);
                    if (distance > farthest) { replace = candidate; farthest = distance; }
                }
                if (replace == Entity.Null) return;
                manager.DestroyEntity(replace);
            }
            var entity = manager.CreateEntity(enemyArchetype);
            byte kind = elite ? (byte)2 : SelectEnemyKind();
            float hp = boss ? (finalBoss ? Settings.bossHealth : Settings.waveBossHealth * (1 + .25f * WaveBossesSpawned)) : BaseEnemyHealth(kind) * (elite ? 5 : 1) * (1 + Elapsed / math.max(1, Settings.runDuration) * .65f);
            manager.SetComponentData(entity, new Position2D { Value = position });
            var special = finalBoss ? FinalBossKind : (SpecialBoss)(WaveBossesSpawned % 2);
            manager.SetComponentData(entity, new Enemy { Kind = kind, Speed = boss ? 2.5f : BaseEnemySpeed(kind), Elite = elite, Boss = boss, FinalBoss = boss && finalBoss, MaxHealth = hp, AttackTimer = 2, Special = special, Pattern = kind >= 3 ? random.NextInt(0, 2) : 0 });
            manager.SetComponentData(entity, new Health { Value = hp });
            if (boss) { BossEntity = entity; if (finalBoss) BossSpawned = true; else WaveBossesSpawned++; }
            if (boss && finalBoss) EncounteredBosses |= 1 << (int)special;
        }

        private void Damage(Entity entity, float damage)
        {
            if(damage<=0)return;
            var manager = World.EntityManager;
            var health = manager.GetComponentData<Health>(entity);
            damage *= BossDamageMultiplier(manager.GetComponentData<Enemy>(entity));
            health.Value -= damage;
            float2 position = manager.GetComponentData<Position2D>(entity).Value;
            Bursts.Add(new VisualBurst(position,Elapsed,3));
            if (health.Value <= 0)
            {
                var enemy = manager.GetComponentData<Enemy>(entity);
                manager.DestroyEntity(entity);
                Kills++;
                Bursts.Add(new VisualBurst(position, Elapsed, 0));
                if (enemy.Boss) Hazards.Clear();
                if (enemy.FinalBoss || (Training && enemy.Boss))
                {
                    if (!Training && CoreRequired && enemy.Special != SpecialBoss.Core) QueueCore();
                    else Phase = RunPhase.Victory;
                    return;
                }
                if (enemy.Boss) { WaveBossesDefeated++; BossEntity = Entity.Null; }
                DropExperience(position, enemy.Boss ? 36 : enemy.Elite ? 24 : enemy.Kind >= 2 ? 3 : 1);
            }
            else
            {
                manager.SetComponentData(entity, health);
                var enemy = manager.GetComponentData<Enemy>(entity);
                enemy.HitFlash = .12f;
                enemy.Knockback += math.normalizesafe(position - HeroPosition) * (enemy.Boss ? .2f : 2);
                manager.SetComponentData(entity, enemy);
            }
        }

        public void Dispose() { if (World.IsCreated) World.Dispose(); }
    }
}
