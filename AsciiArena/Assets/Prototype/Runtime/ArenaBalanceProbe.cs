#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace AsciiArena
{
    // Diagnostic controllers operate normal input and dash. No immunity, healing or damage cheats.
    // The reactive controller reads only already-visible attacks, at 250 ms intervals,
    // and applies decisions after 150 ms. It is a reproducible probe, not a human difficulty model.
    public sealed class ArenaBalancePilot
    {
        private float nextDecision, applyAt;
        private float2 pending, direction;
        private bool dash;
        public int Dashes { get; private set; }
        private readonly List<(Enemy enemy,float2 point)> enemies=new();
        private readonly List<(Bolt bolt,float2 point)> bolts=new();
        public void Tick(ArenaSimulation sim, int policy, float delta)
        {
            if(!sim.BossActive)return;
            var m=sim.World.EntityManager;
            float2 hero=ArenaLayout.Project(sim.HeroPosition), boss=ArenaLayout.Project(m.GetComponentData<Position2D>(sim.BossEntity).Value);
            float ideal=sim.HasWeapon(WeaponKind.Runebolt)?4.5f:2.0f;
            if(policy==0){sim.Input=0;return;}
            if(policy==1)
            {
                var radial=math.normalizesafe(boss-hero);
                sim.Input=math.normalizesafe(new float2(-radial.y,radial.x)+radial*math.clamp(math.distance(hero,boss)-ideal,-2,2));return;
            }
            if(applyAt>0 && sim.Elapsed>=applyAt)
            {direction=pending;sim.Input=direction;if(dash && sim.TryDash())Dashes++;applyAt=0;}
            sim.Input=direction;
            if(sim.Elapsed<nextDecision)return;
            nextDecision=sim.Elapsed+.25f;applyAt=sim.Elapsed+.15f;
            enemies.Clear();bolts.Clear();
            using(var es=sim.EnemyQuery.ToEntityArray(Allocator.Temp))foreach(var entity in es)
            {
                var p=ArenaLayout.Project(m.GetComponentData<Position2D>(entity).Value);
                if(math.distance(p,hero)<12)enemies.Add((m.GetComponentData<Enemy>(entity),p));
            }
            using(var bs=sim.BoltQuery.ToEntityArray(Allocator.Temp))foreach(var entity in bs)
            {
                var b=m.GetComponentData<Bolt>(entity);if(b.Hostile)bolts.Add((b,ArenaLayout.Project(m.GetComponentData<Position2D>(entity).Value)));
            }
            float best=float.MaxValue;pending=0;dash=false;
            for(int mode=0;mode<2;mode++)
            {
                if(mode==1&&(policy==3||sim.DashCooldownRemaining>0))continue;
                for(int n=-1;n<16;n++)
                {
                    if(mode==1&&n<0)continue;
                    float a=n*math.PI/8;float2 dir=n<0?float2.zero:new float2(math.cos(a),math.sin(a));
                    float2 p=hero;float score=mode==1?1.2f:0;
                    for(int step=1;step<=10;step++)
                    {
                        float t=step*.1f;
                        // Reaction delay is part of the forecast; dash invulnerability is finite.
                        var movement=t<=.15f?direction:dir;
                        float travel=sim.HeroSpeed*.1f;
                        bool invulnerable=mode==1&&t>.15f&&t<=.15f+sim.Settings.dashDuration;
                        if(invulnerable)travel*=sim.Settings.dashSpeedMultiplier;
                        float2 candidate=p+movement*travel;
                        if(!sim.Layout.Walkable(ArenaLayout.ScreenToWorld(candidate))){score+=2;candidate=p;}
                        p=candidate;
                        if(!invulnerable)score+=Risk(sim,p,t)*(1.1f-t)*16;
                    }
                    score+=math.abs(math.distance(p,boss)-ideal)*.35f;
                    score+=math.lengthsq(dir-direction)*.025f;
                    if(score<best){best=score;pending=dir;dash=mode==1;}
                }
            }
        }
        private float Risk(ArenaSimulation sim,float2 p,float time)
        {
            float risk=0;
            foreach(var h in sim.Hazards)
            {
                if(!ArenaSimulation.BossHazardVisible(h))continue;
                float active=time-h.Delay;if(active<0||active>h.Remaining)continue;
                var future=h;future.Width+=.18f;
                if(h.Ring)future.Radius+=active*ArenaSimulation.RingSpeed(h);
                if(h.Disk)future.Radius+=.18f;
                if(ArenaSimulation.HazardContains(future,p))risk+=1;
            }
            foreach(var item in enemies)
            {
                var e=item.enemy;var point=item.point;
                if(e.BossChargePending||e.ChargeLeft>0)
                {
                    if(time>=e.Windup && time<=e.Windup+math.distance(ArenaLayout.Project(e.AttackOrigin),ArenaLayout.Project(e.AttackTarget))/ArenaSimulation.WardenChargeSpeed+.1f
                       && ArenaSimulation.SegmentDistanceSquared(p,ArenaLayout.Project(e.AttackOrigin),ArenaLayout.Project(e.AttackTarget))<1.1f*1.1f)risk+=1;
                }
                if(!e.Boss&&e.Kind==ArenaSimulation.Bombardier&&(e.Windup>0||e.Recovery>0)&&time>=e.Windup&&time<=e.Windup+.35f&&math.distance(p,ArenaLayout.Project(e.AttackTarget))<ArenaSimulation.BlastRadius+.2f)risk+=1;
                if(!e.Boss&&e.Kind==ArenaSimulation.Lancer&&(e.Windup>0||e.ChargeLeft>0)&&time>=e.Windup&&time<=e.Windup+.6f&&ArenaSimulation.SegmentDistanceSquared(p,ArenaLayout.Project(e.AttackOrigin),ArenaLayout.Project(e.AttackTarget))<.75f*.75f)risk+=1;
                if(!e.Boss&&e.Kind==0)point+=math.normalizesafe(p-point)*e.Speed*time*.7f;
                float radius=e.Boss?1.25f:e.Kind==2?.8f:.65f;
                if(math.distance(p,point)<radius)risk+=.8f;
            }
            foreach(var item in bolts)
            {
                var future=item.point+ArenaLayout.Project(item.bolt.Velocity)*time;
                if(math.distance(p,future)<.45f)risk+=.7f;
            }
            return risk;
        }
    }

    public static class ArenaBalanceProbe
    {
        [Serializable] public sealed class Result
        {
            public int boss,pair,rank,policy,seed,crowd,patterns,hits,dashes,peakEnemies,peakHazards;
            public bool victory;public float seconds,health,bossHealth,damage;
        }
        [Serializable] private sealed class Report {public string note;public Result[] cases;}
        public static ArenaSimulation Create(ArenaSettings original,int boss,int pair,int rank,int seed,int crowd)
        {
            var settings=UnityEngine.Object.Instantiate(original);settings.seed=seed;settings.actOne=true;
            settings.initialEnemies=0;settings.spawnPerSecond=crowd>0?original.spawnPerSecond:0;
            settings.firstLevelExperience=1000000; // Freeze the chosen build: no level-up healing mid-comparison.
            var sim=new ArenaSimulation(settings,new ArenaLayout());sim.ChooseUpgrade(0);var s=sim.Snapshot();
            s.elapsed=1620;s.nextWaveBoss=s.nextElite=999999;s.bossSpawned=true;s.initialEnemiesPending=false;
            s.weapons=pair==0?new[]{WeaponKind.Runebolt,WeaponKind.Ward}:pair==1?new[]{WeaponKind.Runebolt,WeaponKind.Orbit}:new[]{WeaponKind.Ward,WeaponKind.Orbit};
            for(int i=0;i<s.ranks.Length;i++)s.ranks[i]=i==3||i==4||i==5?0:math.min(rank,ArenaSimulation.RankCap((UpgradeKind)i));
            var rng=new Unity.Mathematics.Random((uint)(seed+7));
            s.position=seed%3==0?new float2(13,6):seed%3==1?new float2(2,0):new float2(2,-4);
            float2 bossPosition=FindPosition(sim,s.position,6,rng.NextFloat(0,math.PI*2));
            float hp=boss==0?original.waveBossHealth:original.bossHealth;
            s.finalBoss=boss==3?SpecialBoss.Core:boss==2?SpecialBoss.Archivist:SpecialBoss.Bellkeeper;
            var enemies=new List<EnemySave>{new(){position=bossPosition,health=hp,data=new Enemy{Boss=true,FinalBoss=boss!=0,Elite=true,Kind=2,Speed=2.5f,AttackTimer=2,MaxHealth=hp,Special=s.finalBoss}}};
            for(int i=0;i<crowd;i++)
            {
                byte kind=i<6?(byte)(1+i%5):(byte)0;
                enemies.Add(new EnemySave{position=FindPosition(sim,s.position,rng.NextFloat(8,13),rng.NextFloat(0,math.PI*2)),health=kind==2?110:kind>=3?60:35,
                    data=new Enemy{Kind=kind,MaxHealth=kind==2?110:kind>=3?60:35,Speed=kind==0?1.85f:kind==5?2.5f:1.25f,AttackTimer=1+rng.NextFloat(0,2),Pattern=i%2}});
            }
            s.enemies=enemies.ToArray();sim.Restore(s);return sim;
        }
        private static float2 FindPosition(ArenaSimulation sim,float2 center,float radius,float angle)
        {
            for(int i=0;i<32;i++){float a=angle+i*.23f;var p=center+ArenaLayout.ScreenToWorld(new float2(math.cos(a),math.sin(a))*radius);if(sim.Layout.Walkable(p))return p;}
            return center+new float2(6,0);
        }
        public static Result Run(ArenaSettings settings,int boss,int pair,int rank,int policy,int seed,int crowd)
        {
            var sim=Create(settings,boss,pair,rank,seed,crowd);var pilot=new ArenaBalancePilot();
            var r=new Result{boss=boss,pair=pair,rank=rank,policy=policy,seed=seed,crowd=crowd};float start=sim.Elapsed;
            try
            {
                while(!sim.Finished&&sim.BossActive&&sim.Elapsed-start<150)
                {
                    pilot.Tick(sim,policy,1f/60);sim.Step(1f/60);
                    r.peakEnemies=math.max(r.peakEnemies,sim.EnemyQuery.CalculateEntityCount());r.peakHazards=math.max(r.peakHazards,sim.Hazards.Count);
                    if(sim.BossActive)r.patterns=math.max(r.patterns,sim.World.EntityManager.GetComponentData<Enemy>(sim.BossEntity).Pattern);
                }
                r.victory=!sim.BossActive||sim.Phase==RunPhase.Victory;r.seconds=sim.Elapsed-start;r.health=sim.HeroHealth;r.bossHealth=sim.BossHealthRatio;
                r.hits=sim.HurtCount;r.damage=sim.BossDamageTaken;r.dashes=pilot.Dashes;return r;
            }
            finally{sim.Dispose();UnityEngine.Object.DestroyImmediate(sim.Settings);}
        }
        public static void Batch(ArenaSettings settings,string path,bool full=false)
        {
            var results=new List<Result>();var seeds=full?new[]{13,29,42}:new[]{13};
            foreach(int seed in seeds)foreach(int crowd in full?new[]{0,24}:new[]{24})foreach(int rank in full?new[]{0,2}:new[]{2})
            for(int boss=0;boss<3;boss++)for(int pair=0;pair<3;pair++)for(int policy=0;policy<4;policy++)
            {
                results.Add(Run(settings,boss,pair,rank,policy,seed,crowd));
                File.WriteAllText(path,JsonUtility.ToJson(new Report{note="Real ECS combat, 100 HP, base movement, no mid-fight healing. Policies: stationary / circle / reactive+dash / reactive-no-dash. Bots are diagnostics, not human success rates.",cases=results.ToArray()},true));
            }
        }
        public static void Validate(ArenaSettings settings,string path)
        {
            var results=new List<Result>();
            foreach(int seed in new[]{71,89,103})foreach(int rank in new[]{0,2})
                for(int boss=0;boss<3;boss++)for(int pair=0;pair<3;pair++)
                {
                    results.Add(Run(settings,boss,pair,rank,2,seed,24));
                    File.WriteAllText(path,JsonUtility.ToJson(new Report{note="Unseen seeds, reactive+dash, same fixed HP/speed and no healing. Validation, not tuning input.",cases=results.ToArray()},true));
                }
        }
        public static void ValidateCore(ArenaSettings settings,string path)
        {
            var results=new List<Result>();
            foreach(int seed in new[]{13,29,42})foreach(int rank in new[]{0,2})
                foreach(int crowd in new[]{0,24})for(int pair=0;pair<3;pair++)for(int policy=0;policy<4;policy++)
                {
                    results.Add(Run(settings,3,pair,rank,policy,seed,crowd));
                    File.WriteAllText(path,JsonUtility.ToJson(new Report{note="Kernel first balance: same ordinary ECS, 100 HP, base speed, no healing. Diagnostic controller, not human win rates.",cases=results.ToArray()},true));
                }
        }
    }
}
#endif
