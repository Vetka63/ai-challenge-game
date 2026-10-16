using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace AsciiArena
{
    public sealed partial class ArenaSimulation
    {
        // Existing save IDs 0..2 remain unchanged: chaser, shooter, heavy.
        public const byte Lancer = 3, Bombardier = 4, Flanker = 5;
        public const float ChargeReach = 7, ChargeSpeed = 14, ChargeWidth = .55f, BlastRadius = 2.2f;
        private int attackSlots;
        public static float EnemyIntroduction(byte kind) => kind switch { Lancer => 90, Bombardier => 180, Flanker => 300, _ => 0 };
        public static float EnemyWindup(byte kind) => kind switch { Lancer => 1.05f, Bombardier => 1.4f, Flanker => .95f, _ => .75f };
        public static string EnemyName(byte kind) => kind switch { Lancer => "MOUSE HUNTER", Bombardier => "MALWARE", Flanker => "NET WORM", _ => "VIRUS" };
        private static float BaseEnemyHealth(byte kind) => kind switch { 1 => 32, 2 => 85, Lancer => 52, Bombardier => 42, Flanker => 36, _ => 22 };
        private static float BaseEnemySpeed(byte kind) => kind switch { 1 => 1.25f, 2 => .95f, Lancer => 1.8f, Bombardier => 1.2f, Flanker => 2.5f, _ => 1.85f };
        public string RosterHint => Elapsed < 90 ? "WASD move / SHIFT dash / ESC menu"
            : Elapsed < 180 ? "MOUSE:  sidestep the marked charge; punish the recovery"
            : Elapsed < 300 ? "MALWARE:  leave the marked circle or interrupt the caster"
            : "NET WORM: circles you; change direction to escape its fan";

        private byte SelectEnemyKind()
        {
            if (Elapsed < 20) return 0;
            byte baseline = (byte)random.NextInt(0, 3);
            if (!Settings.actOne || Elapsed < 90 || random.NextFloat() > .42f) return baseline;
            // Rotate emphasis every 90 seconds; cap specialists instead of flooding the screen.
            byte focus = (byte)(3 + ((int)(Elapsed / 90) % 3));
            byte candidate = random.NextFloat() < .55f ? focus : (byte)random.NextInt(3, 6);
            if (Elapsed < EnemyIntroduction(candidate)) return baseline;
            using var enemies = EnemyQuery.ToComponentDataArray<Enemy>(Allocator.Temp);
            int count = 0; foreach (var e in enemies) if (e.Kind == candidate && !e.Boss) count++;
            return count < (candidate == Flanker ? 4 : 2) ? candidate : baseline;
        }
        private void PrepareAttackBudget(NativeArray<Entity> enemies)
        {
            attackSlots = BossClaimsAttackWindow ? 0 : BossActive ? 1 : 3;
            foreach (var entity in enemies)
            {
                var e = World.EntityManager.GetComponentData<Enemy>(entity);
                if (!e.Boss && (e.Windup > 0 || e.ChargeLeft > 0 || (e.Kind == Bombardier && e.Recovery > 0))) attackSlots--;
            }
        }
        private bool CanBeginAttack(float2 position)
        {
            if (attackSlots <= 0 || math.any(math.abs(ArenaLayout.Project(position - HeroPosition)) > math.max(new float2(1), ViewHalfExtents - 2))) return false;
            attackSlots--; return true;
        }
        private void TacticalAttack(ref Enemy enemy, float2 p, float2 hero, float delta)
        {
            enemy.AttackTimer -= delta;
            enemy.Recovery = math.max(0, enemy.Recovery - delta);
            if (enemy.ChargeLeft > 0 || enemy.Recovery > 0) return;
            if (enemy.Windup > 0)
            {
                enemy.Windup = math.max(0, enemy.Windup - delta);
                if (enemy.Windup > 0) return;
                enemy.AttackTimer = enemy.Kind == Bombardier ? 5.5f : 4.2f;
                if (enemy.Kind == Lancer)
                    enemy.ChargeLeft = math.distance(ArenaLayout.Project(enemy.AttackOrigin), ArenaLayout.Project(enemy.AttackTarget));
                else if (enemy.Kind == Bombardier) enemy.Recovery = .35f;
                else
                {
                    for (int i = -1; i <= 1; i++)
                    {
                        var aim = math.normalizesafe(ArenaLayout.Project(enemy.Aim)); float a = i * .28f;
                        var direction = new float2(aim.x * math.cos(a) - aim.y * math.sin(a), aim.x * math.sin(a) + aim.y * math.cos(a));
                        // Same projected directions are used by its three warning rays.
                        FireBolt(p, ArenaLayout.ScreenToWorld(direction), true, 5);
                    }
                    enemy.Recovery = .5f;
                }
                return;
            }
            float distance = math.length(ArenaLayout.Project(hero - p));
            if (enemy.AttackTimer > 0 || distance > (enemy.Kind == Lancer ? 8 : 11) || distance < 1.5f || !CanBeginAttack(p)) return;
            enemy.AttackOrigin = p;
            enemy.AttackTarget = hero;
            enemy.Aim = math.normalizesafe(hero - p);
            if (enemy.Kind == Lancer)
            {
                var direction = math.normalizesafe(ArenaLayout.Project(hero - p));
                enemy.AttackTarget = p;
                // Clip the shown path against actual terrain. A charge never crosses a wall/pond.
                for (float d = .1f; d <= ChargeReach; d += .1f)
                {
                    var next = p + ArenaLayout.ScreenToWorld(direction * d);
                    if (!Layout.Walkable(next)) break;
                    enemy.AttackTarget = next;
                }
            }
            enemy.Windup = EnemyWindup(enemy.Kind); WarningCount++;
        }
        private float2 MoveTacticalEnemy(ref Enemy enemy, float2 p, float2 hero, float delta)
        {
            if (enemy.Kind == Bombardier && enemy.Recovery > 0)
            {
                if (math.distancesq(ArenaLayout.Project(hero), ArenaLayout.Project(enemy.AttackTarget)) <= BlastRadius * BlastRadius)
                    Hurt(Settings.enemyShotDamage * 1.5f, enemy.AttackTarget);
                return p;
            }
            if (enemy.Kind == Lancer && enemy.ChargeLeft > 0)
            {
                var direction = math.normalizesafe(ArenaLayout.Project(enemy.AttackTarget - enemy.AttackOrigin));
                float travel = math.min(enemy.ChargeLeft, ChargeSpeed * delta);
                int steps = math.max(1, (int)math.ceil(travel / .1f)); var moved = p;
                for (int i = 0; i < steps; i++)
                {
                    var next = moved + ArenaLayout.ScreenToWorld(direction * travel / steps);
                    if (!Layout.Walkable(next)) { enemy.ChargeLeft = 0; break; }
                    moved = next;
                }
                if (SegmentDistanceSquared(ArenaLayout.Project(hero), ArenaLayout.Project(p), ArenaLayout.Project(moved)) <= ChargeWidth * ChargeWidth)
                    Hurt(Settings.contactDamage * 2, p);
                enemy.ChargeLeft = math.max(0, enemy.ChargeLeft - travel);
                if (enemy.ChargeLeft <= 0) enemy.Recovery = 1.1f;
                return moved;
            }
            if (enemy.Windup > 0 || enemy.Recovery > 0) return p;
            var offset = ArenaLayout.Project(hero - p); float distance = math.length(offset);
            var radial = math.normalizesafe(offset);
            var directionMove = radial;
            if (enemy.Kind == Bombardier) directionMove *= distance < 5 ? -1 : distance > 8 ? 1 : 0;
            if (enemy.Kind == Flanker)
            {
                var tangent = new float2(-radial.y, radial.x) * (enemy.Pattern % 2 == 0 ? 1 : -1);
                directionMove = distance > 8 ? radial : tangent + radial * math.clamp((distance - 5.5f) * .65f, -1, 1);
            }
            var velocity = ArenaLayout.ScreenToWorld(math.normalizesafe(directionMove) * enemy.Speed) + separation.At(p) * .35f + enemy.Knockback;
            return Layout.Move(p, velocity * delta);
        }
    }
}
