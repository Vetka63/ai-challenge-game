using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace AsciiArena
{
    [Serializable] public sealed class ArenaSnapshot
    {
        public int version = 4;
        public HeroIdentity heroIdentity;
        public bool coreRequired;
        public float coreTransition;
        public string settings;
        public uint randomState;
        public float elapsed, spawnBudget, boltTimer, pulseTimer, invulnerability, accumulator;
        public float nextElite, nextWaveBoss, dashRemaining, dashCooldown, echoAt, orbitAngle;
        public float2 position, dashDirection, echoPosition, lastHurtSource, viewHalfExtents;
        public float health, lastDamage, bossDamageTaken;
        public Hero hero;
        public RunPhase phase;
        public SpecialBoss finalBoss;
        public bool bossSpawned, initialEnemiesPending, highContrast;
        public int kills, boltCasts, pulseCasts, boltHits, pulseHits, orbitHits, hurtCount, warningCount;
        public int level, xp, waveBossesSpawned, waveBossesDefeated, recycledEnemies, encountered;
        public int[] ranks;
        public WeaponKind[] weapons;
        public UpgradeKind[] offers;
        public int offerCount;
        public EnemySave[] enemies;
        public BoltSave[] bolts;
        public PickupSave[] pickups;
        public BossHazard[] hazards;
    }
    [Serializable] public struct EnemySave { public float2 position; public float health; public Enemy data; }
    [Serializable] public struct BoltSave
    {
        public float2 position, velocity;
        public float lifetime, damage;
        public bool hostile;
        public int pierces;
        public int[] hitIndices;
    }
    [Serializable] public struct PickupSave { public float2 position; public int value; }

    public sealed partial class ArenaSimulation
    {
        public ArenaSnapshot Snapshot()
        {
            if (Training) throw new InvalidOperationException("Training is never an expedition save.");
            var m = World.EntityManager;
            var s = new ArenaSnapshot {
                heroIdentity = HeroIdentity, coreRequired = CoreRequired, coreTransition = CoreTransition,
                settings = JsonUtility.ToJson(Settings), randomState = random.state,
                elapsed = Elapsed, spawnBudget = spawnBudget, boltTimer = boltTimer, pulseTimer = pulseTimer,
                invulnerability = invulnerability, accumulator = accumulator, nextElite = nextElite, nextWaveBoss = nextWaveBoss,
                dashRemaining = DashRemaining, dashCooldown = DashCooldownRemaining, echoAt = echoAt, orbitAngle = orbitAngle,
                position = HeroPosition, health = HeroHealth, hero = m.GetComponentData<Hero>(HeroEntity),
                dashDirection = dashDirection, echoPosition = echoPosition, lastHurtSource = LastHurtSource,
                lastDamage = LastDamage, bossDamageTaken = BossDamageTaken, phase = Phase, finalBoss = FinalBossKind,
                bossSpawned = BossSpawned, initialEnemiesPending = initialEnemiesPending, highContrast = HighContrast,
                kills = Kills, boltCasts = BoltCasts, pulseCasts = PulseCasts, boltHits = BoltHits, pulseHits = PulseHits,
                orbitHits = OrbitHits, hurtCount = HurtCount, warningCount = WarningCount, level = Level, xp = ExperiencePoints,
                waveBossesSpawned = WaveBossesSpawned, waveBossesDefeated = WaveBossesDefeated,
                recycledEnemies = RecycledEnemies, encountered = EncounteredBosses, viewHalfExtents = ViewHalfExtents,
                ranks = (int[])UpgradeRanks.Clone(), weapons = (WeaponKind[])weapons.Clone(),
                offers = (UpgradeKind[])Offers.Clone(), offerCount = OfferCount, hazards = Hazards.ToArray()
            };
            using var es = EnemyQuery.ToEntityArray(Allocator.Temp);
            var indices = new Dictionary<Entity, int>();
            s.enemies = new EnemySave[es.Length];
            for (int i = 0; i < es.Length; i++)
            {
                indices.Add(es[i], i);
                s.enemies[i] = new EnemySave { position = m.GetComponentData<Position2D>(es[i]).Value,
                    health = m.GetComponentData<Health>(es[i]).Value, data = m.GetComponentData<Enemy>(es[i]) };
            }
            using var bs = BoltQuery.ToEntityArray(Allocator.Temp);
            s.bolts = new BoltSave[bs.Length];
            for (int i = 0; i < bs.Length; i++)
            {
                var b = m.GetComponentData<Bolt>(bs[i]); var hits = new List<int>();
                foreach (var hit in b.HitEntities) if (indices.TryGetValue(hit, out int index)) hits.Add(index);
                s.bolts[i] = new BoltSave { position = m.GetComponentData<Position2D>(bs[i]).Value, velocity = b.Velocity,
                    lifetime = b.Lifetime, damage = b.Damage, hostile = b.Hostile, pierces = b.Pierces, hitIndices = hits.ToArray() };
            }
            using var ps = PickupQuery.ToEntityArray(Allocator.Temp);
            s.pickups = new PickupSave[ps.Length];
            for (int i = 0; i < ps.Length; i++) s.pickups[i] = new PickupSave {
                position = m.GetComponentData<Position2D>(ps[i]).Value, value = m.GetComponentData<Experience>(ps[i]).Value };
            return s;
        }

        public static bool ValidSnapshot(ArenaSnapshot s)
        {
            if (s == null || s.version < 1 || s.version > 4 || string.IsNullOrEmpty(s.settings) || s.randomState == 0 ||
                !math.isfinite(s.elapsed) || s.elapsed < 0 || !math.isfinite(s.health) || s.health < 0 ||
                !math.all(math.isfinite(s.position)) || s.level < 1 || s.level > 10000 || s.xp < 0 ||
                (int)s.phase < 0 || (int)s.phase > 4 || (int)s.finalBoss < 0 || (int)s.finalBoss > 2 ||
                s.ranks?.Length != 11 || s.weapons?.Length != 2 || s.offers?.Length != 3 ||
                s.offerCount < 1 || s.offerCount > 3 || s.enemies == null || s.enemies.Length > 1000 ||
                s.bolts == null || s.bolts.Length > 20000 || s.pickups == null || s.pickups.Length > 10000 ||
                s.hazards == null || s.hazards.Length > 100) return false;
            if ((int)s.heroIdentity < 0 || (int)s.heroIdentity > 3 || !math.isfinite(s.coreTransition) || s.coreTransition < 0 || s.coreTransition > 3) return false;
            for (int i = 0; i < 11; i++) if (s.ranks[i] < 0 || s.ranks[i] > RankCap((UpgradeKind)i)) return false;
            foreach (var w in s.weapons) if ((int)w < -1 || (int)w > 2) return false;
            foreach (var o in s.offers) if ((int)o < 0 || (int)o > 14) return false;
            foreach (var b in s.bolts)
            {
                if (b.hitIndices == null || b.hitIndices.Length > 14) return false;
                foreach (var index in b.hitIndices) if (index < 0 || index >= s.enemies.Length) return false;
            }
            return true;
        }

        public void Restore(ArenaSnapshot s)
        {
            if (!ValidSnapshot(s)) throw new InvalidDataException("Unsupported or damaged run save.");
            JsonUtility.FromJsonOverwrite(s.settings, Settings);
            Reset(); var m = World.EntityManager;
            HeroIdentity = s.version >= 4 ? s.heroIdentity : HeroIdentity.Ascii;
            CoreRequired = s.version >= 4 && s.coreRequired; CoreTransition = s.version >= 4 ? s.coreTransition : 0;
            random.state = s.randomState; Elapsed = s.elapsed; spawnBudget = s.spawnBudget; boltTimer = s.boltTimer;
            pulseTimer = s.pulseTimer; invulnerability = s.invulnerability; accumulator = s.accumulator;
            nextElite = s.nextElite; nextWaveBoss = s.nextWaveBoss; DashRemaining = s.dashRemaining;
            DashCooldownRemaining = s.dashCooldown; echoAt = s.echoAt; orbitAngle = s.orbitAngle;
            dashDirection = s.dashDirection; echoPosition = s.echoPosition; LastHurtSource = s.lastHurtSource;
            LastDamage = s.lastDamage; BossDamageTaken = s.bossDamageTaken; Phase = s.phase; FinalBossKind = s.finalBoss;
            BossSpawned = s.bossSpawned; initialEnemiesPending = s.initialEnemiesPending; HighContrast = s.highContrast;
            Kills = s.kills; BoltCasts = s.boltCasts; PulseCasts = s.pulseCasts; BoltHits = s.boltHits; PulseHits = s.pulseHits;
            OrbitHits = s.orbitHits; HurtCount = s.hurtCount; WarningCount = s.warningCount; Level = s.level; ExperiencePoints = s.xp;
            WaveBossesSpawned = s.waveBossesSpawned; WaveBossesDefeated = s.waveBossesDefeated;
            RecycledEnemies = s.recycledEnemies; EncounteredBosses = s.encountered; ViewHalfExtents = s.viewHalfExtents;
            Array.Copy(s.ranks, UpgradeRanks, 11); Array.Copy(s.weapons, weapons, 2); Array.Copy(s.offers, Offers, 3);
            OfferCount = s.offerCount; Hazards.AddRange(s.hazards);
            m.SetComponentData(HeroEntity, new Position2D { Value = s.position });
            m.SetComponentData(HeroEntity, new Health { Value = s.health }); m.SetComponentData(HeroEntity, s.hero);
            var entities = new Entity[s.enemies.Length];
            for (int i = 0; i < entities.Length; i++)
            {
                var e = s.enemies[i]; var entity = entities[i] = m.CreateEntity(enemyArchetype);
                m.SetComponentData(entity, new Position2D { Value = e.position }); m.SetComponentData(entity, new Health { Value = e.health });
                m.SetComponentData(entity, e.data); if (e.data.Boss) BossEntity = entity;
            }
            foreach (var b in s.bolts)
            {
                var entity = m.CreateEntity(boltArchetype);
                var data = new Bolt { Velocity = b.velocity, Lifetime = b.lifetime, Damage = b.damage, Hostile = b.hostile, Pierces = b.pierces };
                foreach (int index in b.hitIndices) data.HitEntities.Add(entities[index]);
                m.SetComponentData(entity, data); m.SetComponentData(entity, new Position2D { Value = b.position });
            }
            foreach (var p in s.pickups)
            {
                var entity = m.CreateEntity(pickupArchetype);
                m.SetComponentData(entity, new Position2D { Value = p.position }); m.SetComponentData(entity, new Experience { Value = p.value });
            }
            Input = 0; DemoMode = false; Paused = false;
        }
    }

    // Checksum detects torn/corrupt files. Atomic replacement keeps the previous complete copy.
    public sealed class ArenaSaveStore
    {
        [Serializable] private sealed class Envelope { public int version = 1; public string payload, checksum; }
        public readonly string DirectoryPath;
        public ArenaSaveStore(string directory) { DirectoryPath = directory; }
        private static string Hash(string value)
        { using var sha = SHA256.Create(); return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value))); }
        public void Write<T>(string name, T data)
        {
            Directory.CreateDirectory(DirectoryPath);
            var payload = JsonUtility.ToJson(data);
            var text = JsonUtility.ToJson(new Envelope { payload = payload, checksum = Hash(payload) });
            string path = Path.Combine(DirectoryPath, name + ".json"), temporary = path + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            { var bytes = Encoding.UTF8.GetBytes(text); stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak"); else File.Move(temporary, path);
        }
        public T Read<T>(string name, Func<T, bool> valid, out bool recovered) where T : class
        {
            recovered = false;
            for (int copy = 0; copy < 2; copy++)
            {
                string path = Path.Combine(DirectoryPath, name + ".json" + (copy == 0 ? "" : ".bak"));
                try
                {
                    if (!File.Exists(path) || new FileInfo(path).Length > 16000000) continue;
                    var e = JsonUtility.FromJson<Envelope>(File.ReadAllText(path));
                    if (e == null || e.version != 1 || e.payload == null || e.checksum != Hash(e.payload)) continue;
                    var data = JsonUtility.FromJson<T>(e.payload);
                    if (valid(data)) { recovered = copy == 1; return data; }
                }
                catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is FormatException || ex is UnauthorizedAccessException) { }
            }
            return null;
        }
    }
}
