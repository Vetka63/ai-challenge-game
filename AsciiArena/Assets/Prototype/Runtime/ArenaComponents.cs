using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace AsciiArena
{
    public struct Position2D : IComponentData { public float2 Value; }
    [System.Serializable] public struct Hero : IComponentData { public byte Facing; public float WalkCycle; public bool Moving; }
    [System.Serializable] public struct Enemy : IComponentData
    {
        public byte Kind; public float Speed, HitFlash, AttackTimer, Windup, MaxHealth;
        public float2 Aim, Knockback; public bool Elite, Boss, FinalBoss;
        public float OrbitHitUntil;
        public SpecialBoss Special;
        public int Pattern;
        public float ChargeLeft, Recovery;
        public float2 AttackOrigin, AttackTarget;
        public bool BossChargePending;
    }
    public struct Health : IComponentData { public float Value; }
    public struct Bolt : IComponentData
    {
        public float2 Velocity; public float Lifetime, Damage; public bool Hostile;
        public int Pierces;
        public FixedList128Bytes<Entity> HitEntities;
    }
    public struct Experience : IComponentData { public int Value; }
    public enum SpecialBoss { Bellkeeper, Archivist, Core }
    // Both damage and presentation use this exact geometry in projected screen units.
    [System.Serializable]
    public struct BossHazard
    {
        public float2 Origin, Direction;
        public float Delay, Remaining, Radius, GapAngle, Width;
        public bool Ring;
        public bool Disk;
        public float RadialSpeed, Telegraph;
        public byte Source; // 0: legacy, 1: warden, 2: bellkeeper, 3: archivist.
    }
    public enum RunPhase { Running, ChoosingUpgrade, Victory, Defeat, ChoosingWeapon }
    public enum WeaponKind { None=-1, Runebolt, Ward, Orbit }
    public enum UpgradeKind { BoltPower, QuickCast, WideWard, Vitality, FleetFoot, Magnet, Pierce, Fan, Echo, OrbitPower, OrbitCount, LearnBolt, LearnWard, LearnOrbit, Recovery }
    public readonly struct VisualBurst
    {
        public readonly float2 Position;
        public readonly float Time;
        public readonly byte Kind;
        public VisualBurst(float2 position, float time, byte kind) { Position = position; Time = time; Kind = kind; }
    }
}
