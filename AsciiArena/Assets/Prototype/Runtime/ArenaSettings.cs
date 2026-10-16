using UnityEngine;

namespace AsciiArena
{
    [CreateAssetMenu(menuName = "ASCII Arena/Settings")]
    public sealed class ArenaSettings : ScriptableObject
    {
        public int seed = 5271;
        [Range(40, 400)] public int crowdLimit = 180;
        public int initialEnemies = 12;
        public float spawnPerSecond = 1.4f;
        public float heroSpeed = 5.8f;
        public float heroHealth = 100;
        public float boltCooldown = .42f;
        public float boltSpeed = 18;
        public float boltDamage = 18;
        public float pulseCooldown = 2.8f;
        public float pulseRadius = 4.8f;
        public float pulseDamage = 44;
        [Header("Run pacing")]
        public bool actOne;
        [Min(10)] public float runDuration = 600;
        [Min(1)] public float bossAt = 540;
        [Min(1)] public float eliteInterval = 180;
        public float finalSpawnMultiplier = 3.5f;
        public float bossHealth = 4800;
        [Min(1)] public float waveBossInterval = 120;
        public float waveBossHealth = 3200;
        [Min(1)] public int maxExperiencePickups = 512;
        public float enemyShotDamage = 8;
        public float contactDamage = 7;
        public int firstLevelExperience = 12;
        public int experiencePerLevel = 8;
        public float pickupRadius = 2.8f;
        [Header("Combat and loadout")]
        public float orbitDamage = 48;
        public float orbitRadius = 1.6f;
        public float orbitAngularSpeed = 3.8f;
        public float orbitHitInterval = .3f;
        public float dashCooldown = 2.4f;
        public float dashDuration = .14f;
        public float dashSpeedMultiplier = 2.8f;
    }
}
