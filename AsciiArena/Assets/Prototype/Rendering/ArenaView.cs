using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Zenject;

namespace AsciiArena
{
    public sealed partial class ArenaView : MonoBehaviour
    {
        public Material glyphMaterial;
        public TMPro.TMP_FontAsset terminalFont;
        [Inject] private ArenaSimulation simulation;
        private TerminalGlyphBatch environment, characters, actors, heroLayer, danger;
        // Development capture can compare the exact same state with the previous draw order.
        public bool ReadabilityEnabled { get; set; } = true;
        private bool contrast;
        private Camera arenaCamera;
        private Vector3 cameraVelocity;
        private int seenRun = -1;
        private int2 firstChunk, lastChunk;
        private bool environmentReady;
        public int VisibleChunkCount { get; private set; }
        public int EnvironmentRebuilds { get; private set; }
        private float2 cloakTrail, previousHero;
        private readonly System.Collections.Generic.List<int> order = new();
        private static Color Cyan => new(.90f,.96f,1f);
        private static Color Ivory => new(.95f,.96f,.86f);

        private void Start()
        {
            Application.targetFrameRate = 60;
            environment = new TerminalGlyphBatch(transform,glyphMaterial,terminalFont,"Quiet terminal grove",0);
            characters = new TerminalGlyphBatch(transform,glyphMaterial,terminalFont,"Terminal actors",10);
            actors = new TerminalGlyphBatch(transform,glyphMaterial,terminalFont,"Terminal spells and warnings",12);
            heroLayer = new TerminalGlyphBatch(transform,glyphMaterial,terminalFont,"Protected hero silhouette",13);
            danger = new TerminalGlyphBatch(transform,glyphMaterial,terminalFont,"Hostile projectiles and warnings",14);
            arenaCamera = Camera.main;
            contrast = simulation.HighContrast; BuildEnvironment();
        }
        private void BuildEnvironment()
        {
            environment.Clear();
            var floor = contrast ? new Color(.035f,.065f,.055f) : new Color(.06f,.105f,.07f);
            var terrain = contrast ? new Color(.045f,.09f,.055f) : new Color(.095f,.22f,.137f);
            VisibleChunkCount = (lastChunk.x-firstChunk.x+1)*(lastChunk.y-firstChunk.y+1);
            for (int cy=firstChunk.y;cy<=lastChunk.y;cy++) for(int cx=firstChunk.x;cx<=lastChunk.x;cx++)
            {
                var chunk = new int2(cx,cy); var center = ArenaLayout.ChunkCenter(chunk);
                for (int y=-16;y<16;y++) for(int x=-22;x<22;x++)
                {
                    float2 p = center + new float2(x,y);
                    uint detail = math.hash((int2)p);
                    if (ArenaLayout.Water(p)) { if (detail%3==0) environment.Put(':',ArenaLayout.Project(p),new Color(.08f,.15f,.17f),.22f,.3f); }
                    else if (detail%11==0) environment.Put(detail%2==0?'0':'1',ArenaLayout.Project(p),floor,.16f,.25f);
                    if (y%8==0 && x%3==0 && !ArenaLayout.Water(p)) environment.Put('-',ArenaLayout.Project(p),floor,.18f,.22f);
                }
                if (ArenaLayout.HasPond(chunk)) for (int i=0;i<180;i++)
                {
                    float a=i*math.PI*2/180;
                    float2 p=ArenaLayout.PondCenter(chunk)+new float2(math.cos(a)*9.1f,math.sin(a)*4.6f);
                    environment.Put('~',ArenaLayout.Project(p),new Color(.13f,.25f,.24f),.2f,.3f);
                }
                for(int pillar=0;pillar<simulation.Layout.Pillars.Length;pillar++)
                {
                    var p = simulation.Layout.PillarAt(chunk,pillar);
                    environment.Sprite(new[]{" .---. "," | : | "," | # | "," '---' "},ArenaLayout.Project(p),new Color(.24f,.36f,.25f),.24f,.32f);
                    for(int i=0;i<24;i++)
                    {
                        float a=i*math.PI*2/24;
                        environment.Put('.',ArenaLayout.Project(p+new float2(math.cos(a),math.sin(a))),new Color(.17f,.27f,.18f),.15f,.23f);
                    }
                }
                foreach(var local in new[]{new float2(-17,11),new float2(-2,12),new float2(7,11),new float2(19,12),new float2(-18,-3),new float2(17,-8),new float2(5,-12),new float2(1,1)})
                    environment.Sprite(TerminalActorArt.Tree,ArenaLayout.Project(ArenaLayout.DecorationAt(chunk,local)),terrain,.18f,.28f);
            }
            environment.Upload();
            environmentReady = true; EnvironmentRebuilds++;
        }
        // SDF glyphs move continuously. No bitmap-grid quantization of the presentation.
        private static float2 Snap(float2 p) => p;
        private void LateUpdate()
        {
            if(actors==null || simulation==null) return;
            bool contrastChanged = contrast!=simulation.HighContrast;
            contrast=simulation.HighContrast;
            float2 hero=ArenaLayout.Project(simulation.HeroPosition);
            bool reset=seenRun!=simulation.RunSerial;
            if(reset){seenRun=simulation.RunSerial;previousHero=hero;cloakTrail=0;cameraVelocity=Vector3.zero;}
            var displacement=hero-previousHero;
            previousHero=hero;
            float2 trailTarget=-math.clamp(displacement/math.max(Time.deltaTime,.001f)*.012f,-.08f,.08f);
            cloakTrail=math.lerp(cloakTrail,trailTarget,1-math.exp(-Time.deltaTime*12));
            if(arenaCamera!=null)
            {
                var target=new Vector3(hero.x,hero.y+.6f,-20);
                var cp=reset?target:Vector3.SmoothDamp(arenaCamera.transform.position,target,ref cameraVelocity,.10f);
                // Same follow target and damping; SDF text does not require a pixel-grid camera.
                arenaCamera.transform.position=cp;
                var center = ArenaLayout.ScreenToWorld(new float2(cp.x,cp.y));
                var extent = ArenaLayout.ScreenToWorld(new float2(arenaCamera.orthographicSize*arenaCamera.aspect+4,arenaCamera.orthographicSize+4));
                var first = ArenaLayout.ChunkAt(center-extent);
                var last = ArenaLayout.ChunkAt(center+extent);
                if(!environmentReady || contrastChanged || math.any(first!=firstChunk) || math.any(last!=lastChunk))
                { firstChunk=first;lastChunk=last;BuildEnvironment(); }
            }
            characters.Clear(); actors.Clear(); heroLayer.Clear(); danger.Clear();
            var manager=simulation.World.EntityManager;
            using var pickups=simulation.PickupQuery.ToEntityArray(Allocator.Temp);
            foreach(var entity in pickups) actors.Put('+',Snap(ArenaLayout.Project(manager.GetComponentData<Position2D>(entity).Value)),new Color(.51f,.54f,.75f),.17f,.283f);
            using var enemies=simulation.EnemyQuery.ToEntityArray(Allocator.Temp);
            using var positions=simulation.EnemyQuery.ToComponentDataArray<Position2D>(Allocator.Temp);
            order.Clear(); for(int i=0;i<enemies.Length;i++)order.Add(i);
            // Stable enemy painter order; the protected hero uses a separate foreground layer.
            order.Add(-1);
            var heroY=simulation.HeroPosition.y;
            // NativeArray cannot be captured in a lambda declared using: copy only sort keys.
            var sortY=new float[positions.Length]; for(int i=0;i<sortY.Length;i++)sortY[i]=positions[i].Value.y;
            order.Sort((a,b)=>{int y=(b<0?heroY:sortY[b]).CompareTo(a<0?heroY:sortY[a]);return y!=0?y:a.CompareTo(b);});
            foreach(int index in order)
            {
                if(index<0){DrawHero(Snap(hero));continue;}
                var enemy=manager.GetComponentData<Enemy>(enemies[index]);
                float phase=enemies[index].Index*2.39996f;
                float2 ground=ArenaLayout.Project(positions[index].Value);
                float recoil=math.saturate(enemy.HitFlash/.12f);
                float2 p=ground+new float2(0,.22f+math.sin(simulation.Elapsed*2.1f+phase)*.055f)+math.normalizesafe(ArenaLayout.Project(enemy.Knockback))*recoil*.10f;
                var art=enemy.Boss?BossArt(enemy):EnemyArt(enemy.Kind);
                Color color=enemy.Boss?BossColor(enemy):enemy.Kind==1?new Color(.93f,.64f,.36f):enemy.Kind==2?new Color(.73f,.61f,.98f):new Color(.62f,.86f,.41f);
                if (!enemy.Boss && enemy.Kind >= 3) color = TacticalColor(enemy.Kind);
                if (!enemy.Boss && simulation.BossClaimsAttackWindow && enemy.Windup<=0 && enemy.ChargeLeft<=0)
                    color = new Color(color.r*.72f,color.g*.72f,color.b*.72f,color.a);
                // Keep the role colour during hits; only the player's core is ivory.
                if(enemy.HitFlash>0)color=ReadabilityEnabled?Color.Lerp(color,Color.white,.3f):Ivory;
                float size=enemy.Boss?(enemy.FinalBoss && enemy.Special==SpecialBoss.Bellkeeper?.29f:.26f):enemy.Elite?.27f:.23f;
                DrawCloak(art,p,color,size,phase);
                Shadow(ground,size*(enemy.Boss?4:2.8f),color);
                if(enemy.Boss) DrawBossDetails(enemy,p,ground,color,size);
                if(enemy.Elite && !enemy.Boss) Ring(positions[index].Value,enemy.Boss?1.2f:.8f,new Color(.8f,.33f,.7f),32,'+');
                if(enemy.Boss && arenaCamera!=null)
                {
                    var cameraCenter=new float2(arenaCamera.transform.position.x,arenaCamera.transform.position.y);
                    var offset=ground-cameraCenter;
                    var bounds=new float2(arenaCamera.orthographicSize*arenaCamera.aspect-1.5f,arenaCamera.orthographicSize-2.5f);
                    float ratio=math.cmax(math.abs(offset)/math.max(bounds,new float2(1)));
                    if(ratio>1)
                    {
                        var marker=cameraCenter+offset/ratio;
                        danger.Put('>',marker,color,.35f,.5f,math.atan2(offset.y,offset.x));
                        danger.Put('!',marker-math.normalizesafe(offset)*.5f,color,.22f,.35f);
                    }
                }
                if (!enemy.Boss && enemy.Kind >= 3) DrawTacticalWarning(enemy, positions[index].Value, art.Length * size / .63f);
                if(enemy.Windup>0 && (enemy.Kind < 3 || enemy.Boss) && !(simulation.Settings.actOne && enemy.Boss))
                {
                    var warning=ReadabilityEnabled?danger:actors;
                    float charge=1-math.saturate(enemy.Windup/(enemy.Boss?1.1f:.75f));
                    var warningColor=Color.Lerp(new Color(.72f,.31f,.16f),new Color(1,.62f,.30f),charge);
                    warning.Put('!',p+new float2(0,art.Length*size/.63f+.15f),warningColor,.25f+charge*.05f,.4f);
                    int rays=enemy.Boss?12:1;
                    float angle=math.atan2(enemy.Aim.y,enemy.Aim.x);
                    for(int ray=0;ray<rays;ray++)
                    {
                        float a=angle+ray*math.PI*2/rays;
                        var direction=new float2(math.cos(a),math.sin(a));
                        for(int n=1;n<=14;n++)warning.Put(n%4==0?'>':'.',ArenaLayout.Project(positions[index].Value+direction*n*.5f)+new float2(0,.35f),new Color(warningColor.r,warningColor.g,warningColor.b,.35f+charge*.5f),.14f,.20f,math.atan2(direction.y*.68f,direction.x));
                    }
                }
            }
            Shadow(hero,.40f,Ivory);
            if(simulation.HasWeapon(WeaponKind.Orbit))for(int blade=0;blade<simulation.OrbitCount;blade++)
            {
                var position=ArenaLayout.Project(simulation.OrbitPosition(blade,simulation.Elapsed))+new float2(0,.35f);
                actors.Put('*',position,new Color(.35f,.88f,.79f),.32f,.45f,simulation.Elapsed*3);
                for(int n=1;n<=3;n++)actors.Put('.',ArenaLayout.Project(simulation.OrbitPosition(blade,simulation.Elapsed-n*.045f))+new float2(0,.35f),new Color(.35f,.88f,.79f,.4f/n),.16f,.2f);
            }
            using var bolts=simulation.BoltQuery.ToEntityArray(Allocator.Temp);
            foreach(var entity in bolts)
            {
                var bolt=manager.GetComponentData<Bolt>(entity);
                var p=manager.GetComponentData<Position2D>(entity).Value;
                var vector=math.normalizesafe(bolt.Velocity);
                float a=math.atan2(vector.y*.68f,vector.x);
                var color=bolt.Hostile?new Color(1,.35f,.28f):Cyan;
                var layer=bolt.Hostile&&ReadabilityEnabled?danger:actors;
                layer.Put(bolt.Hostile?'o':'>',Snap(ArenaLayout.Project(p)+new float2(0,.35f)),color,.23f,.367f,bolt.Hostile?0:a);
                if(!bolt.Hostile) for(int n=1;n<=2;n++) actors.Put('-',ArenaLayout.Project(p-vector*n*.18f)+new float2(0,.35f),new Color(color.r,color.g,color.b,.5f),.2f,.2f,a);
            }
            foreach(var burst in simulation.Bursts)
            {
                float age=simulation.Elapsed-burst.Time;
                if(burst.Kind==1)
                {
                    float progress=math.saturate(age/.5f);
                    // Actual ward damage is immediate: show its full damage radius immediately.
                    Ring(burst.Position,simulation.PulseRadius,new Color(.87f,.74f,.39f,1-progress),60,age<.14f?'+':'.');
                }
                else if(burst.Kind==3)
                {
                    if(age>.16f)continue;
                    actors.Put('x',ArenaLayout.Project(burst.Position)+new float2(0,.65f+age),new Color(1,.86f,.58f,1-age/.16f),.23f,.32f);
                }
                else for(int n=0;n<6;n++)
                {
                    float a=n*math.PI*2/6;
                    var p=burst.Position+new float2(math.cos(a),math.sin(a))*age*1.5f;
                    actors.Put(burst.Kind==2?'+':'*',ArenaLayout.Project(p)+new float2(0,.3f),new Color(1,burst.Kind==2?.3f:.75f,.45f,1-age/.7f),.15f,.2f);
                }
            }
            DrawBossHazards();
            characters.Upload();actors.Upload();heroLayer.Upload();danger.Upload();
        }
        // Used only by the opt-in visual regression capture while simulation is frozen.
        public void RefreshPresentation() => LateUpdate();
        private void DrawHero(float2 feet)
        {
            var pose=simulation.World.EntityManager.GetComponentData<Hero>(simulation.HeroEntity);
            heroAnimation.Sample(simulation.RunSerial, simulation.Elapsed, pose, simulation.DashRemaining,
                simulation.HurtCount, simulation.HurtFlash);
            var art=TerminalHeroMotion.Get(heroAnimation);
            feet+=new float2(0,.20f);
            const float glyphWidth=.1955f, glyphHeight=.31195f;
            float centerColumn=pose.Facing==1||pose.Facing==2?3:2;
            var layer=ReadabilityEnabled?heroLayer:characters;
            if(ReadabilityEnabled)
            {
                if(simulation.HeroIdentity == HeroIdentity.Ascii) layer.Silhouette(art,feet,glyphWidth,glyphHeight,centerColumn,.085f);
                var marker=new Color(.35f,.88f,.79f);
                layer.Put('v',feet+new float2(0,HeroAnimationPlayer.Height(simulation.HeroIdentity)+.2f),marker,.18f,.28f);
                layer.Put('[',feet+new float2(-.60f,-.20f),marker,.16f,.25f);
                layer.Put(']',feet+new float2(.60f,-.20f),marker,.16f,.25f);
            }
            if(simulation.DashRemaining>0)
            {
                layer.Put('<',feet+new float2(-.9f,.4f),new Color(.35f,.88f,.79f),.25f,.35f);
                layer.Put('>',feet+new float2(.9f,.4f),new Color(.35f,.88f,.79f),.25f,.35f);
            }
            if(simulation.HurtFlash>0)
            {
                var direction=math.normalizesafe(ArenaLayout.Project(simulation.LastHurtSource-simulation.HeroPosition),new float2(0,-1));
                float angle=math.atan2(direction.y,direction.x);
                danger.Put('>',feet+new float2(0,.6f)+direction*1.2f,new Color(1,.35f,.25f,simulation.HurtFlash),.35f,.45f,angle+math.PI);
                if(simulation.HurtFlash>.3f)heroLayer.Sprite(new[]{"-"+simulation.LastDamage.ToString("0")},feet+new float2(0,2.05f+(1-simulation.HurtFlash)*.3f),new Color(1,.45f,.3f),.18f,.28f);
                if((int)(simulation.Elapsed*12)%2==0)
                    Ring(simulation.HeroPosition,.65f,new Color(.95f,.96f,.86f,.5f),16,':',heroLayer);
            }
            if (simulation.HeroIdentity != HeroIdentity.Ascii) { DrawSpriteHero(feet, pose); return; }
            if (spriteHero != null) spriteHero.enabled = false;
            for(int row=0;row<art.Length;row++)for(int col=0;col<art[row].Length;col++)
            {
                char c=art[row][col]; Color color=Ivory;
                bool staff=col>=5||col==0;
                if(simulation.HurtFlash>.8f)color=new Color(1,.5f,.4f);
                else if(simulation.DashRemaining>0)color=new Color(.55f,1,.88f);
                float cloth=staff?0:math.saturate((row-1)/2f);
                float2 flutter=cloakTrail*cloth+new float2(math.sin(simulation.Elapsed*3.1f-row*.6f)*.035f*cloth,0);
                layer.Put(c,Snap(feet+new float2((col-centerColumn)*glyphWidth,(art.Length-1-row)*glyphHeight)+flutter),color,glyphWidth,glyphHeight);
            }
        }
        private void DrawCloak(string[] art,float2 root,Color color,float size,float phase)
        {
            int columns=0;foreach(var line in art)columns=math.max(columns,line.Length);
            if(ReadabilityEnabled)characters.Silhouette(art,root,size,size/.63f,(columns-1)*.5f);
            for(int row=0;row<art.Length;row++)for(int col=0;col<art[row].Length;col++)
            {
                var p=root+new float2((col-(columns-1)*.5f)*size,(art.Length-1-row)*size/.63f);
                characters.Put(art[row][col],Snap(p),color,size,size/.63f);
            }
        }
        private void Shadow(float2 root,float radius,Color color)
        {
            color=new Color(color.r*.13f,color.g*.13f,color.b*.13f,.65f);
            for(int i=0;i<12;i++)
            {
                float a=i*math.PI*2/12;
                characters.Put('.',Snap(root+new float2(math.cos(a)*radius,math.sin(a)*radius*.3f)),color,.15f,.2f);
            }
        }
        private void Ring(float2 p,float radius,Color color,int count,char glyph,TerminalGlyphBatch layer=null)
        {
            layer ??= actors;
            for(int i=0;i<count;i++)
            {
                float a=i*math.PI*2/count;
                layer.Put(glyph,ArenaLayout.Project(p+new float2(math.cos(a),math.sin(a))*radius),color,.15f,.2f);
            }
        }
        private void OnDestroy()
        {
            environment?.Dispose();characters?.Dispose();actors?.Dispose();heroLayer?.Dispose();danger?.Dispose();
        }
    }
}
