using Unity.Mathematics;
using UnityEngine;

namespace AsciiArena
{
    // Presentation only: all identities use the same ECS movement, collision and weapons.
    public static class HeroSprites
    {
        private static readonly Sprite[] sprites = new Sprite[4];
        public static Sprite Get(HeroIdentity hero)
        {
            int index = (int)hero;
            if (index <= 0 || index >= sprites.Length) return null;
            return sprites[index] != null ? sprites[index] : sprites[index] = Resources.Load<Sprite>("DigitalHeroes/" + HeroRoster.Asset(hero));
        }
    }
    public sealed partial class ArenaView
    {
        private SpriteRenderer spriteHero;
        private readonly HeroAnimationPlayer heroAnimation = new();
        private readonly float2[] rewriteTrail = new float2[12];
        private readonly float[] rewriteTimes = new float[12];
        private int rewriteIndex, spriteRun = -1;
        private float nextTrail;
        private void DrawSpriteHero(float2 feet, Hero pose)
        {
            if (spriteHero == null)
            {
                var node = new GameObject("Outsider sprite", typeof(SpriteRenderer));
                node.transform.SetParent(transform, false); spriteHero = node.GetComponent<SpriteRenderer>();
                spriteHero.sortingOrder = 13;
                var material = Resources.Load<Material>("DigitalHeroes/OutsiderSprite");
                if (material != null) spriteHero.sharedMaterial = material;
            }
            var sprite = HeroAnimationSprites.Get(simulation.HeroIdentity, heroAnimation) ?? HeroSprites.Get(simulation.HeroIdentity);
            spriteHero.enabled = sprite != null; spriteHero.sprite = sprite;
            if (sprite == null) return;
            if (spriteRun != simulation.RunSerial)
            {
                spriteRun = simulation.RunSerial; nextTrail = 0;
                for (int i=0;i<rewriteTimes.Length;i++) rewriteTimes[i]=-100;
            }
            float t = simulation.Elapsed;
            float height = HeroAnimationPlayer.Height(simulation.HeroIdentity);
            bool dash = simulation.DashRemaining > 0;
            // Imported frames share a body scale and authored root. Never refit the
            // visible bounds each frame: a crouch must actually lower the head.
            bool animated = sprite.name.StartsWith("anim-");
            float size = animated ? 1 : height / sprite.bounds.size.y;
            float recoil = heroAnimation.Motion == HeroMotion.Hurt ? .045f * (1 - math.saturate(heroAnimation.Age / .22f)) : 0;
            var away = math.normalizesafe(ArenaLayout.Project(simulation.HeroPosition - simulation.LastHurtSource));
            var root = feet + away * recoil;
            bool pacman = simulation.HeroIdentity == HeroIdentity.Pacman;
            spriteHero.transform.position = new Vector3(root.x, root.y + (pacman || !animated ? height * .5f : 0), 0);
            spriteHero.flipX = heroAnimation.Flip;
            spriteHero.flipY = false;
            float rotation = 0;
            if (simulation.HeroIdentity == HeroIdentity.Pacman)
            {
                rotation = HeroAnimationPlayer.PacmanAngle(heroAnimation.Facing);
            }
            spriteHero.transform.rotation = Quaternion.Euler(0,0,rotation);
            spriteHero.transform.localScale = new Vector3(size,size,1);
            spriteHero.color = simulation.HurtFlash>.65f ? new Color(1,.78f,.70f) : dash ? new Color(.85f,1,.90f) : Color.white;
            // The body stays visible; fragments supplement its silhouette, never replace it.
            if ((dash || simulation.HurtFlash > .65f) && t >= nextTrail)
            {
                rewriteTrail[rewriteIndex] = feet; rewriteTimes[rewriteIndex] = t;
                rewriteIndex=(rewriteIndex+1)%rewriteTrail.Length; nextTrail=t+.025f;
            }
            for(int i=0;i<rewriteTimes.Length;i++)
            {
                float age=t-rewriteTimes[i]; if(age<0 || age>.38f)continue;
                float fade=1-age/.38f;
                for(int n=0;n<3;n++) heroLayer.Put("01{}"[(i+n)%4],rewriteTrail[i]+new float2((n-1)*.25f, .25f+n*.35f+age*.3f),
                    new Color(.35f,.9f,.78f,fade*.7f),.16f,.23f);
            }
        }
    }
}
