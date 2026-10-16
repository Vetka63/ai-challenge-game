using Unity.Mathematics;
using UnityEngine;

namespace AsciiArena
{
    public enum HeroMotion { Idle, Move, Dash, Recover, Hurt }

    // Presentation clock only. Simulation owns movement, invulnerability and damage.
    // Repeated renders of a paused simulation must produce exactly the same pose.
    public sealed class HeroAnimationPlayer
    {
        private int run = -1, hits;
        private float previousTime, previousCycle, stride, dashEnded = -100, hurtAt = -100;
        private bool wasDashing;
        public HeroMotion Motion { get; private set; }
        public string Pose { get; private set; } = "pass";
        public int Frame { get; private set; }
        public float Age { get; private set; }
        public byte Facing { get; private set; }
        public bool Flip => Facing == 2;
        public string Direction => Facing == 0 ? "south" : Facing == 1 ? "north" : "east";
        public static float Height(HeroIdentity hero) => hero == HeroIdentity.Pacman ? 1.00f : 1.39f;
        // Pac-Man uses a mirror for west so his eye stays above the mouth. East is
        // explicitly identity (not a rotation inherited from the preceding frame).
        public static float PacmanAngle(byte facing) => facing == 0 ? -90 : facing == 1 ? 90 : 0;

        public void Sample(int serial, float time, Hero hero, float dash, int hurtCount, float hurtFlash)
        {
            if (run != serial || time < previousTime)
            {
                run = serial; hits = hurtCount; previousTime = time; previousCycle = hero.WalkCycle;
                stride = 0; dashEnded = -100; wasDashing = false;
                hurtAt = hurtFlash > .7f ? time : -100;
            }
            float delta = math.max(0, time - previousTime);
            if (hurtCount != hits) { hits = hurtCount; hurtAt = time; }
            bool dashing = dash > 0;
            if (wasDashing && !dashing) dashEnded = time;
            if (hero.Moving && !dashing)
            {
                // Distance-driven steps, with a cadence limit for discontinuities after loading.
                float distance = math.max(0, hero.WalkCycle - previousCycle);
                stride += math.min(distance * 1.65f, delta * 14);
            }
            else if (!hero.Moving && !dashing) stride = 0;
            Facing = (byte)math.clamp((int)hero.Facing, 0, 3);
            Motion = dashing ? HeroMotion.Dash : time - hurtAt < .22f ? HeroMotion.Hurt :
                time - dashEnded < .10f ? HeroMotion.Recover : hero.Moving ? HeroMotion.Move : HeroMotion.Idle;
            Age = Motion == HeroMotion.Hurt ? time - hurtAt : Motion == HeroMotion.Recover ? time - dashEnded : 0;
            Frame = Motion == HeroMotion.Move ? (int)stride % 6 : 0;
            Pose = Motion == HeroMotion.Dash ? "dash" : Motion == HeroMotion.Hurt && Age < .14f ? "hurt" :
                Motion == HeroMotion.Move ? Frame < 2 ? "stepA" : Frame < 3 ? "pass" : Frame < 5 ? "stepB" : "pass" : "pass";
            wasDashing = dashing; previousTime = time; previousCycle = hero.WalkCycle;
        }
    }

    public static class HeroAnimationSprites
    {
        private static readonly System.Collections.Generic.Dictionary<string, Sprite> cache = new();
        public static Sprite Get(HeroIdentity identity, HeroAnimationPlayer motion)
        {
            string name;
            if (identity == HeroIdentity.Pacman)
            {
                name = motion.Motion == HeroMotion.Hurt && motion.Age < .14f ? "hurt" :
                    motion.Motion == HeroMotion.Dash ? "dash" : motion.Motion == HeroMotion.Move ?
                    motion.Frame == 0 || motion.Frame == 5 ? "closed" : motion.Frame == 1 || motion.Frame == 4 ? "half" : "open" : "half";
                name = "pacman/" + name;
            }
            else
            {
                // The side passing pose has a raised knee; use the planted-foot
                // master when stopped, rather than freezing mid-step.
                string pose = identity == HeroIdentity.Doomguy && motion.Motion == HeroMotion.Idle && motion.Direction == "east" ? "idle" : motion.Pose;
                name = HeroRoster.Asset(identity) + "/" + motion.Direction + "-" + pose;
            }
            if (!cache.TryGetValue(name, out var sprite))
                cache[name] = sprite = Resources.Load<Sprite>("HeroAnimations/" + name);
            return sprite;
        }
    }
}
