using Unity.Mathematics;
using UnityEngine;

namespace AsciiArena
{
    public sealed partial class ArenaView
    {
        private static readonly string[] LancerArt = { "  __   ", " /| |\\ ", "| |_| |", " \\___/ ", "   \\_~ " };
        private static readonly string[] BombardierArt = { " .---. ", " | ! | ", " |0x0| ", " '---' ", "  /|\\  " };
        private static readonly string[] FlankerArt = { "  [>]  ", " (0)== ", "   (0) ", " ==(0) " };
        private static string[] EnemyArt(byte kind) => kind switch
        {
            ArenaSimulation.Lancer => LancerArt, ArenaSimulation.Bombardier => BombardierArt,
            ArenaSimulation.Flanker => FlankerArt, 1 => TerminalActorArt.Shooter, 2 => TerminalActorArt.Heavy, _ => TerminalActorArt.Chaser
        };
        private static Color TacticalColor(byte kind) => kind switch
        {
            ArenaSimulation.Lancer => new Color(.98f, .48f, .38f), ArenaSimulation.Bombardier => new Color(.94f, .80f, .32f),
            _ => new Color(.42f, .70f, 1f)
        };
        private void DrawTacticalWarning(Enemy enemy, float2 position, float height)
        {
            bool preparing = enemy.Windup > 0;
            var color = preparing ? new Color(1, .68f, .27f) : new Color(1, .32f, .27f);
            var ground = ArenaLayout.Project(position);
            if (preparing)
            {
                danger.Put('!', ground + new float2(0, height + .5f), color, .28f, .4f);
                // A short role marker is readable even when the body is occluded by the crowd.
                danger.Put(enemy.Kind == ArenaSimulation.Lancer ? '>' : enemy.Kind == ArenaSimulation.Bombardier ? 'O' : 'W',
                    ground + new float2(.45f, height + .5f), TacticalColor(enemy.Kind), .24f, .35f);
            }
            if (enemy.Kind == ArenaSimulation.Lancer && (preparing || enemy.ChargeLeft > 0))
            {
                var origin = ArenaLayout.Project(enemy.AttackOrigin);
                var end = ArenaLayout.Project(enemy.AttackTarget);
                var direction = math.normalizesafe(end - origin); var normal = new float2(-direction.y, direction.x);
                float angle = math.atan2(direction.y, direction.x);
                for (float d = 0; d <= math.distance(origin, end); d += .4f)
                {
                    danger.Put(preparing ? '>' : '=', origin + direction * d, color, .16f, .22f, angle);
                    for (int side = -1; side <= 1; side += 2)
                        danger.Put('.', origin + direction * d + normal * side * ArenaSimulation.ChargeWidth, color, .12f, .17f);
                }
            }
            if (enemy.Kind == ArenaSimulation.Bombardier && (preparing || enemy.Recovery > 0))
            {
                var target = ArenaLayout.Project(enemy.AttackTarget);
                for (int i = 0; i < 64; i++)
                {
                    float a = i * math.PI * 2 / 64;
                    danger.Put(preparing ? ':' : '#', target + new float2(math.cos(a), math.sin(a)) * ArenaSimulation.BlastRadius, color, .16f, .22f);
                }
                float fill = preparing ? 1 - math.saturate(enemy.Windup / ArenaSimulation.EnemyWindup(enemy.Kind)) : 1;
                for (int i = 0; i < 24; i++)
                {
                    float a = i * math.PI * 2 / 24;
                    danger.Put('.', target + new float2(math.cos(a), math.sin(a)) * ArenaSimulation.BlastRadius * fill, color, .14f, .2f);
                }
                danger.Put(preparing ? 'X' : '*', target, color, .3f, .4f);
            }
            if (enemy.Kind == ArenaSimulation.Flanker && preparing)
            {
                float angle = math.atan2(enemy.Aim.y * .68f, enemy.Aim.x);
                for (int shot = -1; shot <= 1; shot++)
                {
                    float a = angle + shot * .28f;
                    for (float d = .7f; d < 10; d += .5f)
                        danger.Put('.', ground + new float2(math.cos(a), math.sin(a)) * d + new float2(0, .35f), color, .14f, .2f);
                }
            }
            if (enemy.Kind == ArenaSimulation.Lancer && enemy.Recovery > 0)
                danger.Put('v', ground + new float2(0, height + .4f), new Color(.42f, .85f, .70f), .2f, .3f);
        }
    }
}
