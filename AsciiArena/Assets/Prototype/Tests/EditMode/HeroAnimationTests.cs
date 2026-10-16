using NUnit.Framework;
using UnityEngine;

namespace AsciiArena.Tests
{
    public sealed class HeroAnimationTests
    {
        [TestCase(0, "south", false, -90)] [TestCase(1, "north", false, 90)]
        [TestCase(2, "east", true, 0)] [TestCase(3, "east", false, 0)]
        public void DirectionMappingResetsBothPacmanRotationAndMirror(int facing, string direction, bool flip, float angle)
        {
            var animation = new HeroAnimationPlayer();
            foreach (byte previous in new byte[]{0,1,2,3})
            {
                animation.Sample(1, 1, new Hero{Facing=previous}, 0, 0, 0);
                animation.Sample(1, 2, new Hero{Facing=(byte)facing}, 0, 0, 0);
                Assert.That(animation.Direction, Is.EqualTo(direction)); Assert.That(animation.Flip, Is.EqualTo(flip));
                Assert.That(HeroAnimationPlayer.PacmanAngle(animation.Facing), Is.EqualTo(angle));
            }
        }
        [Test] public void PausedRenderDoesNotAdvanceOrRestartReaction()
        {
            var a=new HeroAnimationPlayer();var hero=new Hero{Moving=true, Facing=3};
            a.Sample(1,0,hero,0,0,0);a.Sample(1,.1f,hero,0,1,1);
            for(int i=0;i<500;i++)a.Sample(1,.1f,hero,0,1,1);
            Assert.That(a.Motion,Is.EqualTo(HeroMotion.Hurt));Assert.That(a.Age,Is.Zero);
            a.Sample(1,.4f,hero,0,1,.6f);Assert.That(a.Motion,Is.EqualTo(HeroMotion.Move));
        }
        [Test] public void DashOverridesHurtThenReturnsWithoutRestartingDamageClip()
        {
            var a=new HeroAnimationPlayer();var hero=new Hero{Moving=true, Facing=1};
            a.Sample(1,0,hero,0,0,0);a.Sample(1,.1f,hero,0,1,1);
            a.Sample(1,.12f,hero,.14f,1,.9f);Assert.That(a.Pose,Is.EqualTo("dash"));
            a.Sample(1,.27f,hero,0,1,.8f);Assert.That(a.Motion,Is.EqualTo(HeroMotion.Hurt));
            a.Sample(1,.5f,hero,0,1,.4f);Assert.That(a.Motion,Is.EqualTo(HeroMotion.Move));
        }
        [Test] public void LoadingNewRunClearsOldHitAndDashRecovery()
        {
            var a=new HeroAnimationPlayer();var hero=new Hero{Facing=2};
            a.Sample(1,30,hero,1,2,0);a.Sample(1,30.1f,hero,0,3,1);
            a.Sample(2,0,hero,0,0,0);Assert.That(a.Motion,Is.EqualTo(HeroMotion.Idle));
            Assert.That(a.Pose,Is.EqualTo("pass"));Assert.That(a.Facing,Is.EqualTo(2));
        }
        [Test] public void MovementIncludesBothContactPosesAndPassingPose()
        {
            var a=new HeroAnimationPlayer();var seen=new System.Collections.Generic.HashSet<string>();
            for(int i=0;i<120;i++)
            {
                a.Sample(1,i/60f,new Hero{Facing=0,Moving=true,WalkCycle=i*.1f},0,0,0);seen.Add(a.Pose);
            }
            CollectionAssert.AreEquivalent(new[]{"stepA","stepB","pass"},seen);
            a.Sample(1,3,new Hero{Facing=0,Moving=false,WalkCycle=12},0,0,0);
            Assert.That(a.Motion,Is.EqualTo(HeroMotion.Idle));
        }
        [Test] public void AllRequiredRuntimePosesLoadAndAsciiPosesRemainPrintable()
        {
            foreach(var hero in new[]{"spark","doomguy"})foreach(var direction in new[]{"south","north","east"})
                foreach(var pose in new[]{"pass","stepA","stepB","dash","hurt"})
                    Assert.That(Resources.Load<Sprite>("HeroAnimations/"+hero+"/"+direction+"-"+pose),Is.Not.Null,hero+direction+pose);
            foreach(var pose in new[]{"open","half","closed","hurt","dash"})
                Assert.That(Resources.Load<Sprite>("HeroAnimations/pacman/"+pose),Is.Not.Null,pose);
            Assert.That(Resources.Load<Sprite>("HeroAnimations/doomguy/east-idle"),Is.Not.Null);
            var a=new HeroAnimationPlayer();
            for(byte facing=0;facing<4;facing++)for(int frame=0;frame<60;frame++)
            {
                a.Sample(1,frame*.1f,new Hero{Facing=facing,Moving=true,WalkCycle=frame*.5f},frame>45?1:0,frame>30?1:0,0);
                foreach(var row in TerminalHeroMotion.Get(a))foreach(char c in row)Assert.That((int)c,Is.InRange(32,126));
            }
        }
    }
}
