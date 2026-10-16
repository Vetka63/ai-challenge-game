using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace AsciiArena
{
    public sealed partial class ArenaSimulation
    {
        public RunPhase Phase { get; private set; }
        public Entity BossEntity { get; private set; }
        public bool BossSpawned { get; private set; }
        public bool BossActive => World.EntityManager.Exists(BossEntity);
        public int WaveBossesSpawned { get; private set; }
        public int WaveBossesDefeated { get; private set; }
        public float NextBossAt => math.min(Settings.bossAt, nextWaveBoss);
        public int RunSerial { get; private set; }
        public int Level { get; private set; }
        public int ExperiencePoints { get; private set; }
        public int NextLevel => math.max(1, Settings.firstLevelExperience + (Level - 1) * Settings.experiencePerLevel);
        public readonly int[] UpgradeRanks = new int[11];
        public readonly UpgradeKind[] Offers = new UpgradeKind[3];
        public bool Finished => Phase == RunPhase.Victory || Phase == RunPhase.Defeat;
        public float MaxHealth => Settings.heroHealth + UpgradeRanks[3] * 20;
        public float BoltDamage => Settings.boltDamage * (1 + .25f * UpgradeRanks[0]);
        public float BoltCooldown => math.max(.12f, Settings.boltCooldown * math.pow(.88f, UpgradeRanks[1]));
        public float PulseRadius => Settings.pulseRadius * (1 + .12f * UpgradeRanks[2]);
        public float PulseDamage => Settings.pulseDamage * (1 + .10f * UpgradeRanks[2]);
        public float HeroSpeed => Settings.heroSpeed * (1 + .08f * UpgradeRanks[4]);
        public float PickupRadius => Settings.pickupRadius + UpgradeRanks[5] * .8f;
        public float BoltCharge => 1 - math.saturate(boltTimer / BoltCooldown);
        public float PulseCharge => 1 - math.saturate(pulseTimer / PulseCooldown);
        public float HurtFlash => math.saturate(invulnerability / .75f);
        public int Wave => 1 + (int)(Elapsed / 60);
        public float BossHealthRatio => World.EntityManager.Exists(BossEntity)
            ? math.saturate(World.EntityManager.GetComponentData<Health>(BossEntity).Value / math.max(1, World.EntityManager.GetComponentData<Enemy>(BossEntity).MaxHealth)) : 0;
        private float nextElite, nextWaveBoss;
        private void ResetRun()
        {
            Array.Clear(UpgradeRanks, 0, UpgradeRanks.Length); Level = 1; ExperiencePoints = 0; RunSerial++;
            nextElite = math.max(1, Settings.eliteInterval); Phase = RunPhase.ChoosingWeapon;ResetLoadout();
            nextWaveBoss = math.max(1, Settings.waveBossInterval); WaveBossesSpawned = WaveBossesDefeated = 0;
            BossEntity = Entity.Null; BossSpawned = false; DemoMode = false; Input = 0; HighContrast = true;
            FinalBossKind = (SpecialBoss)(math.hash(new int2(Settings.seed, 7349)) % 2);
            CoreRequired = false; CoreTransition = 0; HeroIdentity = HeroIdentity.Ascii;
            Hazards.Clear(); EncounteredBosses = 0; Training = false; TrainingCrowd = false; BossDamageTaken = 0;
        }
        private float EnemyAttack(ref Enemy enemy, float2 p, float2 hero, float delta)
        {
            if (Settings.actOne && enemy.Boss) return SpecialAttack(ref enemy, p, hero, delta);
            if (!enemy.Boss && enemy.Kind >= 3) { TacticalAttack(ref enemy, p, hero, delta); return 0; }
            float distance = math.distancesq(p, hero);
            float move = enemy.Kind == 1 ? (distance < 16 ? -1 : distance < 42 ? 0 : 1) : 1;
            enemy.AttackTimer -= delta;
            if (enemy.Windup > 0)
            {
                enemy.Windup -= delta; move = 0;
                if (enemy.Windup <= 0)
                {
                    if (enemy.Boss) for (int shot = 0; shot < 12; shot++)
                    {
                        float angle = shot * math.PI * 2 / 12 + math.atan2(enemy.Aim.y,enemy.Aim.x);
                        FireBolt(p, new float2(math.cos(angle), math.sin(angle)), true, 4.5f);
                    }
                    else FireBolt(p, enemy.Aim, true);
                    enemy.AttackTimer = enemy.Boss ? 2.4f : 2.8f;
                }
            }
            else if ((enemy.Kind == 1 || enemy.Boss) && enemy.AttackTimer <= 0 && distance < 110 && (enemy.Boss || CanBeginAttack(p)))
            { enemy.Windup = enemy.Boss ? 1.1f : .75f; enemy.Aim = math.normalizesafe(hero - p); move = 0;WarningCount++; }
            return move;
        }
        public static float SegmentDistanceSquared(float2 point, float2 from, float2 to)
        {
            var segment = to - from;
            float t = math.saturate(math.dot(point - from, segment) / math.max(.00001f, math.lengthsq(segment)));
            return math.distancesq(point, from + segment * t);
        }
        private void Hurt(float damage,float2 source)
        {
            if (invulnerability > 0 || DashRemaining>0 || damage <= 0) return;
            var manager = World.EntityManager; var health = manager.GetComponentData<Health>(HeroEntity);
            LastDamage=math.min(health.Value,damage);LastHurtSource=source;HurtCount++;
            if (BossActive) BossDamageTaken += LastDamage;
            health.Value = math.max(0, health.Value - damage); manager.SetComponentData(HeroEntity, health);
            invulnerability = .75f; Bursts.Add(new VisualBurst(HeroPosition, Elapsed, 2));
        }
        private void UpdateRun(float delta)
        {
            UpdateCoreTransition(delta);
            var manager = World.EntityManager;
            using var pickups = PickupQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in pickups)
            {
                var p = manager.GetComponentData<Position2D>(entity).Value;
                if (math.distancesq(HeroPosition, p) > PickupRadius * PickupRadius) continue;
                p = math.lerp(p, HeroPosition, math.min(1, delta * 9));
                if (math.distancesq(HeroPosition, p) < .35f)
                { ExperiencePoints += manager.GetComponentData<Experience>(entity).Value; manager.DestroyEntity(entity); }
                else manager.SetComponentData(entity, new Position2D { Value = p });
            }
            // Only one boss at a time. The final boss has priority once the active guard falls;
            // missed intermediate slots are skipped, never queued as a burst of bosses.
            if (!Training && !BossActive && !BossSpawned)
            {
                if (Elapsed >= math.max(1, Settings.bossAt)) SpawnEnemy(true, true);
                else if (Elapsed >= nextWaveBoss)
                {
                    SpawnEnemy(true, true, false);
                    if (BossActive) nextWaveBoss = (math.floor(Elapsed / math.max(1, Settings.waveBossInterval)) + 1) * math.max(1, Settings.waveBossInterval);
                }
            }
            if (BossActive && nextWaveBoss <= Elapsed)
                nextWaveBoss = (math.floor(Elapsed / math.max(1, Settings.waveBossInterval)) + 1) * math.max(1, Settings.waveBossInterval);
            if (!Training && Elapsed >= nextElite) { if (!BossActive && !BossSpawned) SpawnEnemy(true); nextElite += math.max(1, Settings.eliteInterval); }
            if (!Settings.actOne && Elapsed >= Settings.runDuration) { Phase = RunPhase.Defeat; return; }
            if (Training) return;
            TryLevelUp();
        }
        private void TryLevelUp()
        {
            if (Phase != RunPhase.Running || ExperiencePoints < NextLevel) return;
            ExperiencePoints -= NextLevel; Level++; Phase = RunPhase.ChoosingUpgrade;
            RollOffers();
        }
        public void ChooseUpgrade(int index)
        {
            if(Paused)return;
            if(Phase==RunPhase.ChoosingWeapon)
            {
                if(index<0||index>=3)return;
                weapons[0]=(WeaponKind)index;Phase=RunPhase.Running;Input=0;return;
            }
            if (Phase != RunPhase.ChoosingUpgrade || index < 0 || index >= OfferCount) return;
            int kind = (int)Offers[index];
            if(!Eligible(Offers[index]))return;
            if(kind>=11&&kind<=13)weapons[1]=(WeaponKind)(kind-11);
            else if(kind<UpgradeRanks.Length)UpgradeRanks[kind]++;
            var health = World.EntityManager.GetComponentData<Health>(HeroEntity);
            health.Value = math.min(MaxHealth, health.Value + (kind == 3 || kind==14 ? 35 : 10));
            World.EntityManager.SetComponentData(HeroEntity, health);
            Phase = RunPhase.Running;Input=0; TryLevelUp();
        }
        public static string UpgradeName(UpgradeKind kind) => kind switch
        {
            UpgradeKind.BoltPower => "PACKET POWER", UpgradeKind.QuickCast => "OVERCLOCK",
            UpgradeKind.WideWard => "SHIELD RADIUS", UpgradeKind.Vitality => "REPAIR PATCH",
            UpgradeKind.FleetFoot => "LIGHT STEP", UpgradeKind.Magnet=>"DATA MAGNET",
            UpgradeKind.Pierce=>"PACKET PIERCE",UpgradeKind.Fan=>"MULTICAST",UpgradeKind.Echo=>"SHIELD ECHO",
            UpgradeKind.OrbitPower=>"SHARP BITS",UpgradeKind.OrbitCount=>"EXTRA BIT",
            UpgradeKind.LearnBolt=>"ADD DATA BOLT",UpgradeKind.LearnWard=>"ADD PULSE SHIELD",UpgradeKind.LearnOrbit=>"ADD BIT ORBIT",_=>"RESTORE HEALTH"
        };
        public static string UpgradeDescription(UpgradeKind kind) => kind switch
        {
            UpgradeKind.BoltPower => "+25% base bolt damage", UpgradeKind.QuickCast => "Bolts -12% cooldown; ward -8%; orbit +12% base speed",
            UpgradeKind.WideWard => "+12% radius, +10% base ward damage", UpgradeKind.Vitality => "+20 max health and restore 35 health",
            UpgradeKind.FleetFoot => "+8% base movement speed", UpgradeKind.Magnet=>"+0.8 experience attraction radius",
            UpgradeKind.Pierce=>"Bolts pass through +1 enemy; each target hit once",
            UpgradeKind.Fan=>"+1 bolt per volley with a wider spread",
            UpgradeKind.Echo=>"Second pulse at cast point after 0.35s, 60% damage",
            UpgradeKind.OrbitPower=>"+18% base shard damage",UpgradeKind.OrbitCount=>"+1 orbiting shard",
            UpgradeKind.LearnBolt=>WeaponDescription(WeaponKind.Runebolt),UpgradeKind.LearnWard=>WeaponDescription(WeaponKind.Ward),
            UpgradeKind.LearnOrbit=>WeaponDescription(WeaponKind.Orbit),_=>"Restore 35 health"
        };
    }
}
