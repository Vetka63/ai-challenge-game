using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace AsciiArena
{
    public enum HeroIdentity { Ascii, Spark, Pacman, Doomguy }
    public static class HeroRoster
    {
        public static string Name(HeroIdentity hero) => hero switch {
            HeroIdentity.Spark => "ИСКРА", HeroIdentity.Pacman => "PAC-MAN",
            HeroIdentity.Doomguy => "DOOMGUY", _ => "ASCII-СТРАННИК" };
        public static string Asset(HeroIdentity hero) => hero switch {
            HeroIdentity.Spark => "spark", HeroIdentity.Pacman => "pacman",
            HeroIdentity.Doomguy => "doomguy", _ => "" };
        public static string Description(HeroIdentity hero) => hero switch {
            HeroIdentity.Spark => "Чужая системе. Светлая маска, плащ и шарф.\nНаш собственный герой.",
            HeroIdentity.Pacman => "Аркадный гость из другого мира.\nЛичный прототип.",
            HeroIdentity.Doomguy => "Морпех против повреждённой программы.\nЛичный прототип.",
            _ => "Исходный герой из символов.\nДля сравнения с новым оформлением." };
    }
    public sealed partial class ArenaSession
    {
        public void SelectHero(HeroIdentity hero)
        {
            if (MenuPage != ArenaMenuPage.Heroes || (int)hero < 0 || (int)hero > 3) return;
            Profile.selectedHero = hero;
            Status = "Выбран " + HeroRoster.Name(hero) + ". Характеристики героев одинаковы.";
            WriteProfile();
        }
        public void BeginSelectedRun()
        {
            if (MenuPage != ArenaMenuPage.Heroes) return;
            if (HasSavedRun) { ConfirmNew = true; return; }
            NewRun();
        }
    }
    public sealed partial class ArenaSimulation
    {
        public HeroIdentity HeroIdentity { get; set; }
        public bool CoreRequired { get; set; }
        public float CoreTransition { get; private set; }
        private void QueueCore()
        {
            BossEntity = Entity.Null; FinalBossKind = SpecialBoss.Core;
            CoreTransition = 3; Hazards.Clear();
        }
        private void UpdateCoreTransition(float delta)
        {
            if (CoreTransition <= 0) return;
            CoreTransition = math.max(0, CoreTransition - delta);
            if (CoreTransition > 0) return;
            SpawnEnemy(true, true);
            // Retry if terrain/crowd temporarily prevented a spawn; never leave an unwinnable run.
            if (!BossActive) CoreTransition = .25f;
        }
        private void PlanCore(float2 origin, float2 target, float2 aim, int pattern, bool phaseTwo)
        {
            var normal = new float2(-aim.y, aim.x);
            if (pattern == 0)
            {
                // The prediction locks at announcement. Changing course defeats it.
                var predicted = target + math.normalizesafe(Input) * 2.8f;
                AddDisk(predicted, 2.0f, 1.35f, .5f, 4);
                AddLine(target + normal * 3.2f, aim, 7, .48f, 2.25f, .45f, 4);
                if (phaseTwo) AddLine(target - normal * 3.2f, aim, 7, .48f, 3.15f, .45f, 4);
            }
            else if (pattern == 1)
            {
                float angle = math.atan2(aim.y, aim.x);
                AddRing(origin, 1.5f, angle + .65f, 1.35f, 3.0f, 3.0f);
                if (phaseTwo) AddRing(origin, 10.5f, angle - .65f, 4.0f, 2.8f, -3.0f);
            }
            else
            {
                var axis = new float2(1, 0);
                AddLine(target + new float2(0, 3), axis, 10, .55f, 1.35f, .55f, 4);
                AddLine(target - new float2(0, 3), axis, 10, .55f, 1.95f, .55f, 4);
                AddLine(target, new float2(0, 1), 8, .55f, 2.85f, .5f, 4);
                if (phaseTwo) AddDisk(target, 1.8f, 3.8f, .45f, 4);
            }
        }
    }
}
