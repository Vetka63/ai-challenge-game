#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Zenject;

namespace AsciiArena
{
    // Opt-in GPU evidence. Real-time keyboard traversal is followed by explicitly controlled
    // distant positions and boss encounters; these are correctness checks, not balance data.
    [DefaultExecutionOrder(-10000)]
    public sealed class ArenaEndlessCapture : MonoBehaviour
    {
        private readonly List<string> errors=new();
        private string output;
        private ArenaSimulation sim;
        private Keyboard keyboard;
        private bool walking;
        private void Update()
        {
            if(!walking || keyboard==null)return;
            // Feed only this Player's virtual device before SceneKernel/ArenaInput. OS focus
            // changes must not steal Keyboard.current during the hidden diagnostic run.
            keyboard.MakeCurrent();InputState.Change(keyboard,new KeyboardState(Key.D));
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--endless-capture")>=0)
                new GameObject("Endless arena verification").AddComponent<ArenaEndlessCapture>();
        }
        private void OnLog(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
        private IEnumerator Start()
        {
            Application.logMessageReceived+=OnLog;
            var args=Environment.GetCommandLineArgs();output=Path.GetFullPath(args[Array.IndexOf(args,"--endless-capture")+1]);Directory.CreateDirectory(output);
            for(int i=0;i<12;i++)yield return null;
            sim=FindFirstObjectByType<SceneContext>().Container.Resolve<ArenaSimulation>();
            sim.ChooseUpgrade(0);
            var view=FindFirstObjectByType<ArenaView>();
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();walking=true;
            var report=new Evidence{width=Screen.width,height=Screen.height,graphics=SystemInfo.graphicsDeviceType.ToString()};
            for(int frame=0;frame<60;frame++)
            {
                yield return new WaitForSecondsRealtime(.1f);yield return new WaitForEndOfFrame();
                Capture("walk-"+frame.ToString("00"));
                report.walk.Add(Sample(view));
            }
            walking=false;InputState.Change(keyboard,new KeyboardState());yield return null;
            report.crossedBoundary=sim.HeroPosition.x>ArenaLayout.HalfWidth+5;
            if(!report.crossedBoundary)errors.Add("Real keyboard traversal did not cross the old arena boundary");
            // Far-away snapshots exercise negative coordinates, view rebuilds and crowd recycling.
            var manager=sim.World.EntityManager;
            foreach(var position in new[]{new float2(4400,0),new float2(-4400,0),new float2(0,3200),new float2(0,-3200),new float2(2,0)})
            {
                manager.SetComponentData(sim.HeroEntity,new Position2D{Value=position});
                yield return new WaitForSecondsRealtime(.9f);yield return new WaitForEndOfFrame();
                Capture("region-"+report.regions.Count);report.regions.Add(Sample(view));
                if(view.VisibleChunkCount>16)errors.Add("Environment window grew beyond the tested viewport budget");
            }
            // Controlled encounters use shortened timings and disabled damage to the hero.
            sim.Settings.waveBossInterval=2;sim.Settings.bossAt=6;sim.Settings.runDuration=15;
            sim.Settings.spawnPerSecond=0;sim.Settings.initialEnemies=0;sim.Settings.contactDamage=sim.Settings.enemyShotDamage=0;
            sim.Settings.boltDamage=sim.Settings.pulseDamage=0;
            sim.Reset();sim.ChooseUpgrade(0);
            while(sim.Elapsed<2.1f)yield return null;
            if(!sim.BossActive)errors.Add("Intermediate boss missing");
            else
            {
                manager.SetComponentData(sim.BossEntity,new Position2D{Value=sim.HeroPosition+new float2(5,2)});
                yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();Capture("wave-boss");
                // Keep this boss alive past final time, proving it is not silently replaced.
                var firstBoss=sim.BossEntity;
                while(sim.Elapsed<6.1f)yield return null;
                report.bossPreserved=sim.BossEntity==firstBoss&&!sim.BossSpawned;
                KillBoss();yield return ResolveKill(firstBoss);
                report.waveContinued=sim.WaveBossesDefeated==1&&sim.Phase==RunPhase.Running&&sim.BossSpawned;
                if(sim.BossActive)
                {
                    yield return new WaitForEndOfFrame();Capture("final-boss-direction");
                    manager.SetComponentData(sim.BossEntity,new Position2D{Value=sim.HeroPosition+new float2(5,2)});
                    yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();Capture("final-boss");
                    var finalBoss=sim.BossEntity;
                    KillBoss();yield return ResolveKill(finalBoss);yield return new WaitForEndOfFrame();Capture("victory");
                }
            }
            report.victory=sim.Phase==RunPhase.Victory;
            if(!report.bossPreserved||!report.waveContinued||!report.victory)errors.Add("Boss sequence failed");
            report.errors=errors.ToArray();File.WriteAllText(Path.Combine(output,"endless-check.json"),JsonUtility.ToJson(report,true));
            InputSystem.RemoveDevice(keyboard);Application.Quit(errors.Count==0?0:2);
        }
        private void KillBoss()
        {
            var manager=sim.World.EntityManager;
            var bolt=manager.CreateEntity(typeof(Position2D),typeof(Bolt));
            manager.SetComponentData(bolt,manager.GetComponentData<Position2D>(sim.BossEntity));
            manager.SetComponentData(bolt,new Bolt{Damage=100000,Lifetime=1});
        }
        private IEnumerator ResolveKill(Entity target)
        {
            float deadline=Time.realtimeSinceStartup+2;
            do
            {
                while(sim.Phase==RunPhase.ChoosingUpgrade)sim.ChooseUpgrade(0);
                yield return null;
            }while((sim.World.EntityManager.Exists(target)||sim.Phase==RunPhase.ChoosingUpgrade)&&Time.realtimeSinceStartup<deadline);
        }
        private Snapshot Sample(ArenaView view)=>new(){x=sim.HeroPosition.x,y=sim.HeroPosition.y,time=sim.Elapsed,frame=Time.frameCount,chunks=view.VisibleChunkCount,rebuilds=view.EnvironmentRebuilds,enemies=sim.EnemyQuery.CalculateEntityCount(),pickups=sim.PickupQuery.CalculateEntityCount(),recycled=sim.RecycledEnemies};
        private void Capture(string name)=>ArenaSmokeCapture.Capture(Path.Combine(output,name+".png"));
        private void OnDestroy(){Application.logMessageReceived-=OnLog;}
        [Serializable]private sealed class Evidence
        {
            public int width,height;public string graphics;public bool crossedBoundary,bossPreserved,waveContinued,victory;
            public List<Snapshot> walk=new(),regions=new();public string[] errors;
        }
        [Serializable]private sealed class Snapshot{public float x,y,time;public int frame,chunks,rebuilds,enemies,pickups,recycled;}
    }
}
#endif
