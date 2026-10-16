#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Zenject;
namespace AsciiArena
{
    // Opt-in development-build verification. Completely dormant without --ascii-smoke.
    public sealed class ArenaSmokeCapture : MonoBehaviour
    {
        private readonly List<string> errors=new();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--ascii-smoke")>=0)
                new GameObject("Development smoke capture").AddComponent<ArenaSmokeCapture>();
        }
        private IEnumerator Start()
        {
            Application.logMessageReceived+=OnLog;
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--ascii-smoke");
            string output=Path.GetFullPath(args[at+1]);Directory.CreateDirectory(output);
            yield return null;yield return null;
            var context=FindFirstObjectByType<SceneContext>();var sim=context.Container.Resolve<ArenaSimulation>();
            for(int f=0;f<10;f++)yield return null;
            Capture(Path.Combine(output,"weapon-choice.png"));
            if(sim.Phase==RunPhase.ChoosingWeapon)sim.ChooseUpgrade(0);
            WriteDisplay(output,"startup");
            if(Array.IndexOf(args,"--display-check")>=0)
            {
                bool initialFullscreen=Screen.fullScreenMode==FullScreenMode.FullScreenWindow;
                ArenaScreen.ToggleFullscreen();
                for(int f=0;f<20;f++)yield return null;
                WriteDisplay(output,"windowed");bool windowed=!Screen.fullScreen;
                ArenaScreen.ToggleFullscreen();
                for(int f=0;f<20;f++)yield return null;
                WriteDisplay(output,"restored");
                bool restored=Screen.fullScreenMode==FullScreenMode.FullScreenWindow;
                File.WriteAllText(Path.Combine(output,"toggle-check.json"),JsonUtility.ToJson(new DisplayToggleEvidence{initialFullscreen=initialFullscreen,windowed=windowed,restored=restored,errors=errors.ToArray()},true));
                Application.Quit(initialFullscreen&&windowed&&restored&&errors.Count==0?0:2);yield break;
            }
            int startFrame=Time.frameCount;float startTime=sim.Elapsed;
            var dirs=new[]{new float2(0,-1),new float2(0,1),new float2(-1,0),new float2(1,0)};
            // ArenaInput writes zero without keyboard; apply verification input after Update.
            for(int d=0;d<4;d++)
            {
                sim.Input=dirs[d];for(int f=0;f<12;f++)sim.Step(1f/60);
                sim.Paused=true;
                yield return new WaitForEndOfFrame();
                // Hide only HUD for facing inspection: the pause panel otherwise covers the hero.
                var hudCanvas=FindFirstObjectByType<ArenaHud>().GetComponentInChildren<Canvas>();
                hudCanvas.enabled=false;Capture(Path.Combine(output,"hero-"+d+".png"));hudCanvas.enabled=true;
                yield return new WaitForSecondsRealtime(.25f);sim.Paused=false;
            }
            if(Array.IndexOf(args,"--directions-only")>=0)
            {
                File.WriteAllText(Path.Combine(output,"directions-check.json"),JsonUtility.ToJson(new Evidence{width=Screen.width,height=Screen.height,startFrame=startFrame,endFrame=Time.frameCount,errors=errors.ToArray(),graphics=SystemInfo.graphicsDeviceType.ToString()},true));
                Application.Quit(errors.Count==0?0:2);yield break;
            }
            sim.DemoMode=true;yield return new WaitForSecondsRealtime(7);
            yield return new WaitForEndOfFrame();Capture(Path.Combine(output,"battle.png"));
            if(Array.IndexOf(args,"--hover-capture")>=0)
            {
                var crt=Shader.Find("AsciiArena/PhosphorCRT");
                if(crt==null||!crt.isSupported)errors.Add("CRT shader missing or unsupported");
                // Two GPU renders of the same state; no simulation step between A/B shots.
                ArenaDisplay.CrtEnabled=false;Capture(Path.Combine(output,"crt-off.png"));
                ArenaDisplay.CrtEnabled=true;Capture(Path.Combine(output,"crt-on.png"));
                for(int f=0;f<24;f++)
                {
                    yield return new WaitForSecondsRealtime(.08f);
                    yield return new WaitForEndOfFrame();Capture(Path.Combine(output,"motion-"+f.ToString("00")+".png"));
                }
            }
            yield return new WaitForSecondsRealtime(.4f);
            var manager=sim.World.EntityManager;
            var xp=manager.CreateEntity(typeof(Position2D),typeof(Experience));
            manager.SetComponentData(xp,new Position2D{Value=sim.HeroPosition});manager.SetComponentData(xp,new Experience{Value=sim.NextLevel});
            sim.Step(1f/60);sim.Paused=false;
            yield return new WaitForEndOfFrame();Capture(Path.Combine(output,"upgrade.png"));
            float frozen=sim.Elapsed;yield return new WaitForSecondsRealtime(.5f);bool upgradeFrozen=sim.Elapsed==frozen;
            var buttons=FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None);
            if(buttons.Length>0)buttons[0].onClick.Invoke();else errors.Add("No upgrade buttons");
            bool clicked=sim.Phase==RunPhase.Running;
            sim.Paused=true;float pauseTime=sim.Elapsed;yield return new WaitForSecondsRealtime(.3f);bool pauseFrozen=sim.Elapsed==pauseTime;sim.Paused=false;
            // Deterministic accelerated mid-run snapshot, using the real simulation rules.
            sim.DemoMode=true;
            for(int i=0;i<7500&&!sim.Finished&&sim.Elapsed<120;i++)
            {
                while(sim.Phase==RunPhase.ChoosingUpgrade)sim.ChooseUpgrade(0);
                sim.Step(1f/60);
            }
            yield return null;
            yield return new WaitForEndOfFrame();Capture(Path.Combine(output,"crowd.png"));
            if(Array.IndexOf(args,"--readability-capture")>=0)
            {
                // A/B is two renders of this exact simulation state, not two seeded runs.
                var view=FindFirstObjectByType<ArenaView>();
                var cameraPosition=Camera.main.transform.position;
                view.ReadabilityEnabled=false;view.RefreshPresentation();Camera.main.transform.position=cameraPosition;
                Capture(Path.Combine(output,"crowd-before.png"));
                view.ReadabilityEnabled=true;view.RefreshPresentation();Camera.main.transform.position=cameraPosition;
                Capture(Path.Combine(output,"crowd-after.png"));
            }
            if(Array.IndexOf(args,"--style-a-battle")>=0)
            {
                // Real default combat after the accelerated setup. No damage, spawn, speed,
                // collision or attack settings are changed during this crowd recording.
                var samples=new List<BattleSample>();
                for(int f=0;f<48&&!sim.Finished;f++)
                {
                    while(sim.Phase==RunPhase.ChoosingUpgrade)sim.ChooseUpgrade(0);
                    yield return new WaitForSecondsRealtime(.10f);
                    yield return new WaitForEndOfFrame();Capture(Path.Combine(output,"battle-motion-"+f.ToString("00")+".png"));
                    samples.Add(new BattleSample{frame=Time.frameCount,time=sim.Elapsed,enemies=sim.EnemyQuery.CalculateEntityCount(),kills=sim.Kills,boltHits=sim.BoltHits,pulseHits=sim.PulseHits,hp=sim.HeroHealth});
                }
                File.WriteAllText(Path.Combine(output,"battle-check.json"),JsonUtility.ToJson(new BattleEvidence{samples=samples.ToArray()},true));
            }
            if(Array.IndexOf(args,"--readability-capture")>=0)CaptureOverlap(sim,output);
            yield return new WaitForSecondsRealtime(.3f);
            // Controlled boss/result snapshots; explicitly not a natural ten-minute playthrough.
            sim.Reset();sim.ChooseUpgrade(0);sim.Settings.bossAt=1;sim.Settings.runDuration=30;sim.Settings.spawnPerSecond=0;
            sim.Settings.contactDamage=sim.Settings.enemyShotDamage=0;
            sim.DemoMode=false;yield return new WaitForSecondsRealtime(1.5f);
            if(manager.Exists(sim.BossEntity))
            {
                manager.SetComponentData(sim.BossEntity,new Position2D{Value=sim.HeroPosition+new float2(4,2)});
                yield return new WaitForSecondsRealtime(.25f);
                yield return new WaitForEndOfFrame();Capture(Path.Combine(output,"boss.png"));
                yield return new WaitForSecondsRealtime(.3f);
                manager.SetComponentData(sim.BossEntity,new Health{Value=1});sim.Settings.boltDamage=1000;
                yield return new WaitForSecondsRealtime(3);
            }
            yield return new WaitForEndOfFrame();Capture(Path.Combine(output,"result.png"));
            yield return new WaitForSecondsRealtime(.4f);
            File.WriteAllText(Path.Combine(output,"player-check.json"),JsonUtility.ToJson(new Evidence{width=Screen.width,height=Screen.height,startFrame=startFrame,endFrame=Time.frameCount,startTime=startTime,elapsed=sim.Elapsed,upgradeFrozen=upgradeFrozen,upgradeButtonWorked=clicked,pauseFrozen=pauseFrozen,phase=sim.Phase.ToString(),graphics=SystemInfo.graphicsDeviceType.ToString(),errors=errors.ToArray()},true));
            Application.Quit(errors.Count==0&&upgradeFrozen&&clicked&&pauseFrozen&&sim.Phase==RunPhase.Victory?0:2);
        }
        private void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
        private void CaptureOverlap(ArenaSimulation sim,string output)
        {
            // Synthetic occlusion stress test only: temporarily arrange existing enemies.
            // No simulation step or recorded combat occurs while these positions are set.
            var manager=sim.World.EntityManager;
            using var entities=sim.EnemyQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            using var positions=sim.EnemyQuery.ToComponentDataArray<Position2D>(Unity.Collections.Allocator.Temp);
            int count=Math.Min(9,entities.Length);
            var view=FindFirstObjectByType<ArenaView>();var cameraPosition=Camera.main.transform.position;
            try
            {
                for(int i=0;i<count;i++)manager.SetComponentData(entities[i],new Position2D{Value=sim.HeroPosition+new float2((i%3-1)*.7f,(i/3-1)*.6f)});
                view.ReadabilityEnabled=false;view.RefreshPresentation();Camera.main.transform.position=cameraPosition;
                Capture(Path.Combine(output,"synthetic-overlap-before.png"));
                view.ReadabilityEnabled=true;view.RefreshPresentation();Camera.main.transform.position=cameraPosition;
                Capture(Path.Combine(output,"synthetic-overlap-after.png"));
            }
            finally
            {
                for(int i=0;i<count;i++)manager.SetComponentData(entities[i],positions[i]);
                view.ReadabilityEnabled=true;view.RefreshPresentation();Camera.main.transform.position=cameraPosition;
            }
        }
        private void WriteDisplay(string output,string name)
        {
            var camera=Camera.main;
            File.WriteAllText(Path.Combine(output,"display-"+name+".json"),JsonUtility.ToJson(new DisplayEvidence{width=Screen.width,height=Screen.height,systemWidth=Display.main.systemWidth,systemHeight=Display.main.systemHeight,mode=Screen.fullScreenMode.ToString(),orthographicSize=camera.orthographicSize,pixelsPerWorldUnit=camera.GetComponent<ArenaScreen>().PixelsPerWorldUnit,glyphCellHeightPixels=.367f*camera.pixelHeight/(camera.orthographicSize*2),hudScale=FindFirstObjectByType<ArenaHud>().GetComponentInChildren<Canvas>().scaleFactor},true));
        }
        internal static void Capture(string path)
        {
            var camera=Camera.main;
            var target=RenderTexture.GetTemporary(Screen.width,Screen.height,24,RenderTextureFormat.ARGB32);
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            var canvases=FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var changed=new List<Canvas>();
            var originalOrder=new Dictionary<Canvas,int>();
            try
            {
                foreach(var canvas in canvases)if(canvas.renderMode==RenderMode.ScreenSpaceOverlay)
                {changed.Add(canvas);originalOrder[canvas]=canvas.sortingOrder;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.sortingOrder=1000;}
                Canvas.ForceUpdateCanvases();
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=target});
                RenderTexture.active=target;
                var texture=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
                texture.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);texture.Apply();
                File.WriteAllBytes(path,texture.EncodeToPNG());Destroy(texture);
            }
            finally
            {
                foreach(var canvas in changed){canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;canvas.sortingOrder=originalOrder[canvas];}
                camera.targetTexture=oldTarget;RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(target);
            }
        }
        private void OnDestroy(){Application.logMessageReceived-=OnLog;}
        [Serializable]private sealed class Evidence{public int width,height,startFrame,endFrame;public float startTime,elapsed;public bool upgradeFrozen,upgradeButtonWorked,pauseFrozen;public string phase,graphics;public string[] errors;}
        [Serializable]private sealed class BattleSample{public int frame,enemies,kills,boltHits,pulseHits;public float time,hp;}
        [Serializable]private sealed class BattleEvidence{public BattleSample[] samples;}
        [Serializable]private sealed class DisplayEvidence{public int width,height,systemWidth,systemHeight;public string mode;public float orthographicSize,pixelsPerWorldUnit,glyphCellHeightPixels,hudScale;}
        [Serializable]private sealed class DisplayToggleEvidence{public bool initialFullscreen,windowed,restored;public string[] errors;}
    }
}
#endif

