using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace AsciiArena
{
    public sealed partial class ArenaSimulation
    {
        public SpecialBoss FinalBossKind { get; private set; }
        public int EncounteredBosses { get; private set; }
        public bool Training { get; private set; }
        public float BossDamageTaken { get; private set; }
        public readonly List<BossHazard> Hazards = new();
        public const float WardenChargeWidth = .85f, WardenChargeSpeed = 12;
        public string BossName => BossActive ? BossIdentity(World.EntityManager.GetComponentData<Enemy>(BossEntity)) : NameOfBoss(FinalBossKind);
        public static string BossIdentity(Enemy enemy) => !enemy.FinalBoss ? "FIREWALL GUARD" : NameOfBoss(enemy.Special);
        public static string NameOfBoss(SpecialBoss kind) => kind == SpecialBoss.Core ? "CORRUPTED KERNEL" : kind == SpecialBoss.Bellkeeper ? "ALARM DAEMON" : "BROKEN ARCHIVE";
        public string BossHint => !BossActive ? "" : BossName == "FIREWALL GUARD"
            ? "Sidestep the charge. Leave its landing circle. Strike during recovery."
            : BossName == "ALARM DAEMON" ? "Read the ring arrows. Follow the changing gaps; echoes return inward."
            : BossName == "CORRUPTED KERNEL" ? "Prediction locks at warning. Change course; dash through the pulse or use its gap."
            : "Read the marked pages. Leave marked seals; cross lanes between activations.";
        public bool BossRecovering => BossActive && Hazards.Count == 0 && IsRecovering(World.EntityManager.GetComponentData<Enemy>(BossEntity));
        private static bool IsRecovering(Enemy e) => e.Recovery > 0 && e.Windup <= 0 && e.ChargeLeft <= 0;
        public string BossAction
        {
            get
            {
                if(!BossActive)return "";
                var e=World.EntityManager.GetComponentData<Enemy>(BossEntity);
                return BossRecovering ? "EXPOSED CORE - 160% DAMAGE" : ActionName(e)+(BossDamageMultiplier(e)<1?" / GUARDED":"");
            }
        }
        private static readonly string[] WardenActions = { "BREACH + IMPACT", "CLEAVING FAN", "PINCER SEALS" };
        private static readonly string[] BellActions = { "RESONANCE", "THREE TOLLS", "RETURNING ECHO" };
        private static readonly string[] ArchiveActions = { "CROSS INDEX", "TURNING PAGES", "REDACTION" };
        public static string ActionName(Enemy e)
        {
            int pattern = math.max(0, e.Pattern - 1) % 3;
            return !e.FinalBoss ? WardenActions[pattern]
                : e.Special == SpecialBoss.Core ? new[] { "PREDICT / DELETE", "PURGE PULSE", "MEMORY SWEEP" }[pattern]
                : e.Special == SpecialBoss.Bellkeeper ? BellActions[pattern]
                : ArchiveActions[pattern];
        }
        private float SpecialAttack(ref Enemy enemy, float2 p, float2 hero, float delta)
        {
            enemy.AttackTimer -= delta;
            if (enemy.Windup > 0)
            {
                enemy.Windup = math.max(0, enemy.Windup - delta);
                if (enemy.Windup <= 0 && enemy.BossChargePending)
                {
                    enemy.BossChargePending = false;
                    enemy.ChargeLeft = math.distance(ArenaLayout.Project(enemy.AttackOrigin), ArenaLayout.Project(enemy.AttackTarget));
                    if (enemy.ChargeLeft < .05f) FinishWardenCharge(ref enemy, p);
                }
                return 0;
            }
            if (enemy.ChargeLeft > 0 || Hazards.Count > 0) return 0;
            if (enemy.Recovery > 0) { enemy.Recovery = math.max(0, enemy.Recovery - delta); return 0; }
            if (enemy.AttackTimer > 0 || math.lengthsq(ArenaLayout.Project(hero - p)) > 169) return 1;
            if (RegularDangerPending()) return 0;
            int pattern = enemy.Pattern++ % 3;
            bool phaseTwo = enemy.FinalBoss && BossHealthRatio < .5f;
            float2 origin = ArenaLayout.Project(p), target = ArenaLayout.Project(hero);
            float2 aim = math.normalizesafe(target - origin, new float2(1, 0));
            enemy.Aim = ArenaLayout.ScreenToWorld(aim); enemy.Windup = 1.1f;
            enemy.Recovery = enemy.FinalBoss ? 1.6f : 1.8f; WarningCount++;
            if (!enemy.FinalBoss) PlanWarden(ref enemy, p, origin, target, aim, pattern);
            else if (enemy.Special == SpecialBoss.Core) PlanCore(origin, target, aim, pattern, phaseTwo);
            else if (enemy.Special == SpecialBoss.Bellkeeper) PlanBell(origin, target, aim, pattern, phaseTwo);
            else PlanArchive(origin, target, aim, pattern, phaseTwo, enemy.Pattern);
            float duration = 1.8f;
            foreach (var h in Hazards) duration = math.max(duration, h.Delay + h.Remaining);
            enemy.AttackTimer = duration + enemy.Recovery + 1.0f;
            return 0;
        }
        private void PlanWarden(ref Enemy enemy, float2 p, float2 origin, float2 target, float2 aim, int pattern)
        {
            if (pattern == 0)
            {
                enemy.AttackOrigin = enemy.AttackTarget = p;
                for (float d = .1f; d <= 8; d += .1f)
                {
                    var next = p + ArenaLayout.ScreenToWorld(aim * d);
                    if (!Layout.Walkable(next)) break;
                    enemy.AttackTarget = next;
                }
                enemy.BossChargePending = true; enemy.Recovery = 0;
            }
            else if (pattern == 1)
            {
                for (int i = -1; i <= 1; i++)
                {
                    var direction = Rotate(aim, i * .60f);
                    AddLine(origin + direction * 3.8f, direction, 3.8f, .65f, 1.15f + (i + 1) * .22f, .4f, 1);
                }
            }
            else
            {
                var normal = new float2(-aim.y, aim.x);
                AddDisk(target + normal * 2.8f, 1.8f, 1.15f, .45f, 1);
                AddDisk(target - normal * 2.8f, 1.8f, 1.5f, .45f, 1);
                AddDisk(target, 1.8f, 2.05f, .45f, 1);
            }
        }
        private void PlanBell(float2 origin, float2 target, float2 aim, int pattern, bool phaseTwo)
        {
            float angle = math.atan2(aim.y, aim.x);
            var normal = new float2(-aim.y, aim.x);
            if (pattern == 0)
            {
                AddRing(origin, 1.5f, angle + .55f, 1.2f, 3.4f, 3.2f);
                AddRing(origin, 1.5f, angle - .55f, 2.55f, 3.4f, 3.2f);
                if (phaseTwo) AddRing(origin, 1.5f, angle, 3.9f, 3.4f, 3.2f);
            }
            else if (pattern == 1)
            {
                AddDisk(target, 2.1f, 1.2f, .5f, 2);
                AddDisk(target + normal * 3.5f, 2.1f, 1.85f, .5f, 2);
                AddDisk(target - normal * 3.5f, 2.1f, 2.5f, .5f, 2);
                if (phaseTwo) AddRing(origin, 10, angle, 1.5f, 2.6f, -3.2f);
            }
            else
            {
                AddRing(origin, 11, angle + .5f, 1.2f, 2.7f, -3.5f);
                AddRing(origin, 1.5f, angle - .5f, 3.2f, 3.0f, 3.5f);
                if (phaseTwo) AddLine(target, normal, 5, .48f, 2.2f, .45f, 2);
            }
        }
        private void PlanArchive(float2 origin, float2 target, float2 aim, int pattern, bool phaseTwo, int sequence)
        {
            var axis = sequence % 2 == 0 ? new float2(1, 0) : new float2(0, 1);
            var normal = new float2(-axis.y, axis.x);
            if (pattern == 0)
            {
                for (int lane = -1; lane <= 1; lane++)
                    AddLine(target + normal * lane * 3.5f, axis, 11, .46f, 1.2f + math.abs(lane) * .22f, .55f, 3);
                if (phaseTwo) for (int lane = -1; lane <= 1; lane++)
                    AddLine(target + axis * lane * 3.5f, normal, 11, .46f, 2.65f + math.abs(lane) * .22f, .55f, 3);
            }
            else if (pattern == 1)
            {
                // A free corridor between sequential pages; phase two marks a later seal there.
                int safeLane = sequence % 3 - 1;
                for (int lane = -2; lane <= 2; lane++) if (lane != safeLane)
                    AddLine(target + normal * lane * 3.1f, axis, 10, .62f, 1.2f + (lane + 2) * .35f, .65f, 3);
                if (phaseTwo) AddDisk(target + normal * safeLane * 3.1f, 1.6f, 3.6f, .45f, 3);
            }
            else
            {
                AddDisk(target, 2.2f, 1.2f, .5f, 3);
                for (int side = -1; side <= 1; side += 2)
                    AddLine(target + normal * side * 3.6f, axis, 7, .55f, 1.85f, .65f, 3);
                AddLine(target, normal, 8, .5f, 2.9f, .5f, 3);
                if (phaseTwo) AddDisk(target + aim * 3, 1.8f, 3.7f, .5f, 3);
            }
        }
        private static float2 Rotate(float2 v, float angle) => new(v.x * math.cos(angle) - v.y * math.sin(angle), v.x * math.sin(angle) + v.y * math.cos(angle));
        private void AddLine(float2 p, float2 dir, float radius, float width, float delay, float duration, byte source)
            => Hazards.Add(new BossHazard { Origin = p, Direction = dir, Radius = radius, Width = width, Delay = delay, Telegraph = delay, Remaining = duration, Source = source });
        private void AddDisk(float2 p, float radius, float delay, float duration, byte source)
            => Hazards.Add(new BossHazard { Origin = p, Radius = radius, Disk = true, Delay = delay, Telegraph = delay, Remaining = duration, Source = source });
        private void AddRing(float2 p, float radius, float gap, float delay, float duration, float speed)
            => Hazards.Add(new BossHazard { Origin = p, Radius = radius, GapAngle = gap, Width = .32f, Ring = true, Delay = delay, Telegraph = delay, Remaining = duration, RadialSpeed = speed, Source = 2 });
        private void FinishWardenCharge(ref Enemy enemy, float2 p)
        {
            enemy.ChargeLeft = 0; enemy.Recovery = 1.8f; enemy.AttackTimer = 4.1f;
            AddDisk(ArenaLayout.Project(p), 2.6f, .85f, .5f, 1); WarningCount++;
        }
        private float2 MoveBoss(ref Enemy enemy, float2 p, float2 hero, float delta)
        {
            if (enemy.ChargeLeft > 0)
            {
                var direction = math.normalizesafe(ArenaLayout.Project(enemy.AttackTarget - enemy.AttackOrigin));
                float travel = math.min(enemy.ChargeLeft, WardenChargeSpeed * delta);
                int steps = math.max(1, (int)math.ceil(travel / .1f)); var moved = p;
                for (int i = 0; i < steps; i++)
                {
                    var next = moved + ArenaLayout.ScreenToWorld(direction * travel / steps);
                    if (!Layout.Walkable(next)) { enemy.ChargeLeft = 0; break; }
                    moved = next;
                }
                if (SegmentDistanceSquared(ArenaLayout.Project(hero), ArenaLayout.Project(p), ArenaLayout.Project(moved)) <= WardenChargeWidth * WardenChargeWidth)
                    Hurt(WardenChargeDamage, p);
                enemy.ChargeLeft = math.max(0, enemy.ChargeLeft - travel);
                if (enemy.ChargeLeft <= 0) FinishWardenCharge(ref enemy, moved);
                return moved;
            }
            if (enemy.Windup > 0 || enemy.Recovery > 0 || Hazards.Count > 0) return p;
            var offset = ArenaLayout.Project(hero - p); float distance = math.length(offset);
            float2 radial = math.normalizesafe(offset), directionMove = radial;
            if (!enemy.FinalBoss) directionMove *= distance > 3 ? 1 : 0;
            else if (enemy.Special == SpecialBoss.Core) directionMove *= distance > 6 ? 1 : distance < 3 ? -1 : 0;
            else if (enemy.Special == SpecialBoss.Bellkeeper) directionMove *= distance > 7 ? 1 : distance < 4 ? -1 : 0;
            else directionMove = distance > 9 ? radial : new float2(-radial.y, radial.x) * (enemy.Pattern % 2 == 0 ? 1 : -1) + radial * math.clamp((distance - 7) * .6f, -1, 1);
            var velocity = ArenaLayout.ScreenToWorld(math.normalizesafe(directionMove) * (enemy.FinalBoss ? 2.0f : 2.8f)) * delta;
            var nextPosition = Layout.Move(p, velocity);
            if (math.distancesq(nextPosition, p) < .000001f && distance > 7)
                nextPosition = Layout.Move(p, ArenaLayout.ScreenToWorld(new float2(-radial.y, radial.x)) * delta * 2);
            return nextPosition;
        }
        public static float RingSpeed(BossHazard hazard) => hazard.RadialSpeed == 0 ? 4 : hazard.RadialSpeed;
        public static bool HazardContains(BossHazard hazard, float2 projectedPoint)
        {
            var offset = projectedPoint - hazard.Origin;
            if (hazard.Disk) return math.lengthsq(offset) <= hazard.Radius * hazard.Radius;
            if (!hazard.Ring)
                return math.abs(math.dot(offset, hazard.Direction)) <= hazard.Radius &&
                       math.abs(math.dot(offset, new float2(-hazard.Direction.y, hazard.Direction.x))) <= hazard.Width;
            float angle = math.atan2(offset.y, offset.x) - hazard.GapAngle;
            float distanceFromGap = math.abs(math.atan2(math.sin(angle), math.cos(angle)));
            return distanceFromGap > .48f && math.abs(math.length(offset) - hazard.Radius) <= hazard.Width;
        }
        private void UpdateHazards(float delta)
        {
            for (int i = Hazards.Count - 1; i >= 0; i--)
            {
                var h = Hazards[i];
                float activeDelta = math.min(h.Remaining, h.Delay > 0 ? math.max(0, delta - h.Delay) : delta);
                h.Delay = math.max(0, h.Delay - delta);
                if (activeDelta > 0)
                {
                    h.Remaining -= activeDelta;
                    float speed = h.Ring ? RingSpeed(h) : 0;
                    h.Radius += activeDelta * speed;
                    int steps = math.max(1, (int)math.ceil(activeDelta / .016f));
                    for (int s = 0; s <= steps; s++)
                    {
                        var sample = h; sample.Radius -= activeDelta * speed * s / steps;
                        if (HazardContains(sample, ArenaLayout.Project(HeroPosition)))
                            Hurt(BossHazardDamage(h), ArenaLayout.ScreenToWorld(h.Origin));
                    }
                }
                if (h.Remaining <= .00001f) Hazards.RemoveAt(i); else Hazards[i] = h;
            }
        }
        // Training uses a fixed, reproducible build and never changes the suspended expedition.
        public void StartTraining(SpecialBoss boss, WeaponKind first = WeaponKind.Runebolt)
        {
            Reset(); Training = true; FinalBossKind = boss; initialEnemiesPending = false;
            ChooseUpgrade((int)first);
            weapons[1] = first == WeaponKind.Ward ? WeaponKind.Orbit : WeaponKind.Ward;
            for (int i = 0; i < UpgradeRanks.Length; i++) UpgradeRanks[i] = math.min(2, RankCap((UpgradeKind)i));
            SpawnEnemy(true, true);
            var manager = World.EntityManager;
            manager.SetComponentData(BossEntity, new Position2D { Value = HeroPosition + new float2(6, 0) });
            manager.SetComponentData(HeroEntity, new Health { Value = MaxHealth });
            EncounteredBosses = 0;
        }
    }
}
