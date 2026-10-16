using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using AsciiArena;
using UnityEditor;
using UnityEngine;
public static class FourBlockVerification
{
    public static object Run()
    {
        var output=new List<object>();
        foreach(bool invulnerable in new[]{false,true})
        {
            var settings=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ArenaSettings>("Assets/Prototype/Data/ArenaSettings.asset"));
            if(invulnerable)settings.contactDamage=settings.enemyShotDamage=0;
            var watch=Stopwatch.StartNew();double worstStep=0,totalStep=0;int frames=0,maxEnemies=0,maxPickups=0;
            using(var sim=new ArenaSimulation(settings,new ArenaLayout()))
            {
                sim.DemoMode=true;var samples=new List<object>();int minute=0;
                while(!sim.Finished&&frames<36200)
                {
                    while(sim.Phase==RunPhase.ChoosingUpgrade)
                    {
                        int choice=0;
                        for(int i=0;i<3;i++)if(sim.Offers[i]==UpgradeKind.Vitality&&sim.HeroHealth<sim.MaxHealth*.6f)choice=i;
                        sim.ChooseUpgrade(choice);
                    }
                    long begin=Stopwatch.GetTimestamp();sim.Step(1f/60);
                    double ms=(Stopwatch.GetTimestamp()-begin)*1000.0/Stopwatch.Frequency;
                    worstStep=Math.Max(worstStep,ms);totalStep+=ms;frames++;
                    maxEnemies=Math.Max(maxEnemies,sim.EnemyQuery.CalculateEntityCount());maxPickups=Math.Max(maxPickups,sim.PickupQuery.CalculateEntityCount());
                    if((int)(sim.Elapsed/60)>minute){minute=(int)(sim.Elapsed/60);samples.Add(new{time=sim.Elapsed,sim.Level,sim.Kills,hp=sim.HeroHealth,enemies=sim.EnemyQuery.CalculateEntityCount()});}
                }
                output.Add(new{mode=invulnerable?"controlled_no_damage_pacing":"default_autopilot_balance_probe",frames,seconds=sim.Elapsed,result=sim.Phase.ToString(),sim.Level,sim.Kills,sim.BoltHits,sim.PulseHits,sim.BossSpawned,bossHealth=sim.BossHealthRatio,hp=sim.HeroHealth,maxEnemies,maxPickups,wallSeconds=watch.Elapsed.TotalSeconds,meanStepMs=totalStep/frames,worstStepMs=worstStep,samples});
                if(!sim.Finished)throw new Exception("Run did not terminate");
                if(maxEnemies>settings.crowdLimit)throw new Exception("Crowd limit exceeded");
            }
            UnityEngine.Object.DestroyImmediate(settings);
        }
        var json=Newtonsoft.Json.JsonConvert.SerializeObject(output,Newtonsoft.Json.Formatting.Indented);
        Directory.CreateDirectory("../art/playtests/four-blocks");
        File.WriteAllText("../art/playtests/four-blocks/simulation-probes.json",json);
        return output;
    }
}
