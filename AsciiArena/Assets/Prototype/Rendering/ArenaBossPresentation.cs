using Unity.Mathematics;
using UnityEngine;

namespace AsciiArena
{
    public sealed partial class ArenaView
    {
        // Role is legible in monochrome: broad armour, a hollow bell, an open book.
        private static readonly string[] WardenArt = {
            "     /===\\     ",
            " [##]|-v-|[##] ",
            "<|##[==#==]##|>",
            " [##|#####|##] ",
            "  \\=|###|=//  ",
            "    /==V==\\    " };
        private static readonly string[] BellArt = {
            "    O    ",
            "   /|\\   ",
            "  /===\\  ",
            " /|   |\\ ",
            " |:   :| ",
            " |     | ",
            " |     | ",
            "/=======\\",
            "'-------'" };
        private static readonly string[] ArchiveArt = {
            " .----.   .----. ",
            " |==--|\\ /|--==| ",
            " |----| V |----| ",
            " |==--| | |--==| ",
            " '----\\|/----'  ",
            "       v        " };
        private static readonly string[] CoreArt = { "     .-----.", "  .--|  O  |--.", " <==[  ###  ]==>", "  '--|  O  |--'", "     '-----'" };
        private static string[] BossArt(Enemy e) => !e.FinalBoss ? WardenArt : e.Special == SpecialBoss.Core ? CoreArt : e.Special == SpecialBoss.Bellkeeper ? BellArt : ArchiveArt;
        private static Color BossColor(Enemy e) => !e.FinalBoss ? new Color(.95f,.53f,.29f) : e.Special == SpecialBoss.Core ? new Color(.94f,.47f,.87f) : e.Special == SpecialBoss.Bellkeeper ? new Color(.95f,.80f,.39f) : new Color(.53f,.76f,1);

        private void DrawBossDetails(Enemy e, float2 root, float2 ground, Color color, float size)
        {
            bool recovery = simulation.BossRecovering;
            float t = simulation.Elapsed;
            if (e.FinalBoss && e.Special == SpecialBoss.Core)
            {
                for (int i=0;i<8;i++)
                {
                    float angle=t*.35f+i*math.PI/4;
                    characters.Put(i%2==0?'0':'1',root+new float2(math.cos(angle)*2.6f,1.1f+math.sin(angle)*1.6f),color,.20f,.30f);
                }
                characters.Put(recovery?'+':'#',root+new float2(0,.83f),recovery?new Color(.55f,1,.76f):color,.42f,.54f);
            }
            else if (e.FinalBoss && e.Special == SpecialBoss.Bellkeeper)
            {
                float swing = math.sin(t * (e.Windup > 0 ? 12 : 3)) * .42f;
                for (int i = 0; i < 4; i++) characters.Put('|', root + new float2(swing * i / 4, 2.5f - i * .37f), color, .17f,.29f);
                characters.Put('O', root + new float2(swing, .83f), color, .35f,.44f);
            }
            else if (e.FinalBoss)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float2 page = root + new float2(side * (2.65f + math.sin(t * 1.7f) * .12f), 1.3f + math.sin(t * 2 + side) * .24f);
                    characters.Sprite(new[] { ".-.", "|=|", "'-'" }, page, color, .17f,.25f);
                }
                if (simulation.BossHealthRatio >= .5f)
                    characters.Put('*', root + new float2(0, 2.7f), color, .25f,.34f, t * .25f);
            }
            else
            {
                // The exposed visor pulses during its punish window.
                characters.Put('v', root + new float2(0, 4 * size / .63f), recovery ? new Color(.55f,1,.76f) : new Color(1,.76f,.47f),size,size/.63f);
                if (e.BossChargePending || e.ChargeLeft > 0) DrawWardenCharge(e);
            }
            float halfWidth = e.FinalBoss && e.Special == SpecialBoss.Bellkeeper ? 1.5f : 2.2f;
            Color marker = recovery ? new Color(.45f,.95f,.65f) : color;
            actors.Put(recovery ? '>' : '[', ground + new float2(-halfWidth,-.3f), marker,.25f,.32f);
            actors.Put(recovery ? '<' : ']', ground + new float2(halfWidth,-.3f), marker,.25f,.32f);
            if (e.FinalBoss && simulation.BossHealthRatio < .5f)
                actors.Sprite(new[] { "II" }, root + new float2(0, BossArt(e).Length * size / .63f + .2f), color,.18f,.26f);
        }

        private void DrawWardenCharge(Enemy e)
        {
            var start = ArenaLayout.Project(e.AttackOrigin);
            var offset = ArenaLayout.Project(e.AttackTarget) - start;
            float length = math.length(offset);
            var direction = math.normalizesafe(offset);
            var normal = new float2(-direction.y,direction.x);
            Color color = e.BossChargePending ? new Color(1,.67f,.27f,.85f) : new Color(1,.32f,.27f);
            for (float d = 0; d <= length; d += .32f)
            {
                for (int side = -1; side <= 1; side += 2)
                    danger.Put(':',start + direction*d + normal*side*ArenaSimulation.WardenChargeWidth,color,.15f,.22f);
                if ((int)(d/.32f)%3==0) danger.Put('>',start+direction*d,color,.24f,.28f,math.atan2(direction.y,direction.x));
            }
            danger.Put('X',start+offset,color,.32f,.4f);
        }

        private void DrawBossHazards()
        {
            foreach (var h in simulation.Hazards)
            {
                bool warning = h.Delay > 0;
                // Reveal the next part of the phrase with a consistent reaction window;
                // later geometry must not obscure the currently active attack.
                if (!ArenaSimulation.BossHazardVisible(h)) continue;
                float urgency = warning ? 1-math.saturate(h.Delay / math.max(.01f,math.min(h.Telegraph,ArenaSimulation.BossTelegraphWindow))) : 1;
                Color color = warning ? new Color(1,.68f,.28f,.35f+urgency*.5f) : new Color(1,.32f,.27f,.95f);
                if (h.Disk)
                {
                    for (int i = 0; i < 64; i++)
                    {
                        float a = i*math.PI*2/64;
                        var dir = new float2(math.cos(a),math.sin(a));
                        danger.Put(warning ? ':' : '#',h.Origin+dir*h.Radius,color,.17f,.23f);
                        if (warning && i%2==0) danger.Put('.',h.Origin+dir*h.Radius*urgency,color,.12f,.17f);
                    }
                    danger.Put(warning ? '!' : '*',h.Origin,color,.28f,.38f);
                    if (!warning) for (float x=-h.Radius+.3f;x<h.Radius;x+=.55f)
                        for (float y=-h.Radius+.3f;y<h.Radius;y+=.55f)
                            if(x*x+y*y<h.Radius*h.Radius) danger.Put('+',h.Origin+new float2(x,y),new Color(1,.32f,.27f,.45f),.14f,.2f);
                }
                else if (h.Ring)
                {
                    int count = math.max(48,(int)(h.Radius*22));
                    for (int i=0;i<count;i++)
                    {
                        float a=h.GapAngle+i*math.PI*2/count;
                        if(math.abs(math.atan2(math.sin(a-h.GapAngle),math.cos(a-h.GapAngle)))<=.48f)continue;
                        var dir=new float2(math.cos(a),math.sin(a));
                        for(int edge=-1;edge<=1;edge++)
                            danger.Put(warning?'.':edge==0?'#':':',h.Origin+dir*(h.Radius+edge*h.Width),color,.16f,.22f);
                        if(i%12==0) danger.Put('>',h.Origin+dir*(h.Radius+.65f),color,.22f,.26f,a+(ArenaSimulation.RingSpeed(h)<0?math.PI:0));
                    }
                    for(int side=-1;side<=1;side+=2)
                    {
                        float a=h.GapAngle+side*.48f;
                        var dir=new float2(math.cos(a),math.sin(a));
                        if(warning)for(float r=2;r<11;r+=.65f)
                            actors.Put('.',h.Origin+dir*r,new Color(.35f,.70f,.56f,.35f),.12f,.16f);
                        actors.Put('+',h.Origin+dir*h.Radius,new Color(.45f,.95f,.65f),.18f,.25f);
                    }
                }
                else
                {
                    var normal=new float2(-h.Direction.y,h.Direction.x);
                    for(float n=-h.Radius;n<=h.Radius;n+=.4f)
                        for(int side=-1;side<=1;side++)
                            danger.Put(warning?(side==0?'.':':'):'#',h.Origin+h.Direction*n+normal*side*h.Width,color,.16f,.22f);
                }
                if(warning && !h.Ring)
                {
                    // Put counters away from the hero and from crossing-line centres.
                    var label = h.Disk ? h.Origin+new float2(0,-h.Radius-.4f)
                        : h.Origin+h.Direction*2+new float2(-h.Direction.y,h.Direction.x)*(h.Width+.35f);
                    danger.Sprite(new[]{h.Delay.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)},label,new Color(1,.83f,.53f),.18f,.26f);
                }
            }
        }
    }
}
