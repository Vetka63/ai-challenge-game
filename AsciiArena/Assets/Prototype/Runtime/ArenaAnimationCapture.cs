#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEngine;
using Zenject;

namespace AsciiArena
{
    // Staged animation coverage uses real movement/dash/damage in an isolated save directory.
    // It is explicitly separate from live crowd/boss balance QA.
    public sealed class ArenaAnimationCapture : MonoBehaviour
    {
        private string output;
        private readonly List<string> errors=new();
        private readonly List<Sample> samples=new();
        private ArenaSimulation sim;
        private ArenaView view;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] private static void Bootstrap()
        {
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"--animation-capture")>=0 && Array.IndexOf(args,"--act-save-dir")>=0)
                new GameObject("Hero animation verification").AddComponent<ArenaAnimationCapture>();
        }
        private IEnumerator Start()
        {
            Application.logMessageReceived+=OnLog;
            var args=Environment.GetCommandLineArgs();output=Path.GetFullPath(args[Array.IndexOf(args,"--animation-capture")+1]);Directory.CreateDirectory(output);
            for(int i=0;i<20;i++)yield return null;
            var container=FindFirstObjectByType<SceneContext>().Container;
            sim=container.Resolve<ArenaSimulation>();var session=container.Resolve<ArenaSession>();
            view=FindFirstObjectByType<ArenaView>();sim.DiagnosticManualTicks=true;
            session.NewRun(13);sim.ChooseUpgrade(0);
            sim.Settings.initialEnemies=0;sim.Settings.spawnPerSecond=0;
            var directions=new[]{new float2(0,-1),new float2(0,1),new float2(-1,0),new float2(1,0)};
            foreach(var identity in new[]{HeroIdentity.Doomguy,HeroIdentity.Spark,HeroIdentity.Pacman,HeroIdentity.Ascii})
            for(int direction=0;direction<4;direction++)
            {
                sim.Reset();sim.HeroIdentity=identity;sim.ChooseUpgrade(0);sim.DemoMode=false;
                for(int frame=0;frame<120;frame++)
                {
                    sim.Input=frame<68?directions[direction]:float2.zero;
                    if(frame==48 && !sim.TryDash())errors.Add("Dash rejected: "+identity+direction);
                    if(frame==80)
                    {
                        var m=sim.World.EntityManager;var bolt=m.CreateEntity(typeof(Position2D),typeof(Bolt));
                        m.SetComponentData(bolt,new Position2D{Value=sim.HeroPosition+new float2(.3f,0)});
                        m.SetComponentData(bolt,new Bolt{Hostile=true,Damage=8,Velocity=new float2(-20,0),Lifetime=1});
                    }
                    sim.Step(1f/60);
                    yield return null;yield return new WaitForEndOfFrame();
                    if(frame%3==0)Capture(identity+"-"+direction+"-"+frame.ToString("000"),direction,frame);
                    if(frame==35)ArenaSmokeCapture.Capture(Path.Combine(output,identity+"-"+direction+"-full.png"));
                    if(frame==90)
                    {
                        if(sim.HurtCount!=1)errors.Add("Expected one actual damage event: "+identity+direction);
                        var pose=sim.World.EntityManager.GetComponentData<Hero>(sim.HeroEntity);
                        if(pose.Facing!=direction)errors.Add("Facing mismatch: "+identity+direction);
                    }
                }
            }
            // Changing direction on one actor must clear both mirror and rotation.
            sim.Reset();sim.HeroIdentity=HeroIdentity.Pacman;sim.ChooseUpgrade(0);
            foreach(int direction in new[]{1,3,2,3,0,3})
            {
                for(int i=0;i<12;i++){sim.Input=directions[direction];sim.Step(1f/60);yield return null;}
                yield return new WaitForEndOfFrame();Capture("pacman-turn-"+samples.Count,direction,999);
                var sprite=GameObject.Find("Outsider sprite").GetComponent<SpriteRenderer>();
                if(direction==3 && (sprite.flipX || sprite.flipY || Quaternion.Angle(sprite.transform.rotation,Quaternion.identity)>.01f))
                    errors.Add("Right turn retained previous transform");
            }
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{width=Screen.width,height=Screen.height,samples=samples.ToArray(),errors=errors.ToArray()},true));
            Application.Quit(errors.Count==0?0:2);
        }
        private void Capture(string name,int direction,int frame)
        {
            var root=ArenaLayout.Project(sim.HeroPosition);var p=Camera.main.WorldToScreenPoint(new Vector3(root.x,root.y+.9f,0));
            const int width=200,height=220;
            var tex=new Texture2D(width,height,TextureFormat.RGB24,false);
            var target=RenderTexture.GetTemporary(Screen.width,Screen.height,24,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;
            try
            {
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(Camera.main,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=target});
                RenderTexture.active=target;
                tex.ReadPixels(new Rect(Mathf.Clamp((int)p.x-width/2,0,Screen.width-width),Mathf.Clamp((int)p.y-height/2,0,Screen.height-height),width,height),0,0);
                tex.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),tex.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Destroy(tex);}
            var node=GameObject.Find("Outsider sprite");var sprite=node==null?null:node.GetComponent<SpriteRenderer>();
            if(sim.HeroIdentity!=HeroIdentity.Ascii && (sprite==null || sprite.sprite==null || !sprite.sprite.name.StartsWith("anim-")))errors.Add("Fallback sprite: "+name);
            if(sim.HeroIdentity!=HeroIdentity.Ascii && sprite!=null && sprite.sprite!=null)
            {
                if(frame==48 && !sprite.sprite.name.EndsWith("dash"))errors.Add("Dash pose missing: "+name);
                if(frame==81 && !sprite.sprite.name.EndsWith("hurt"))errors.Add("Hurt pose missing: "+name);
            }
            samples.Add(new Sample{hero=sim.HeroIdentity.ToString(),direction=direction,frame=frame,time=sim.Elapsed,
                sprite=sim.HeroIdentity==HeroIdentity.Ascii?"ASCII":sprite?.sprite?.name,hits=sim.HurtCount,dashing=sim.DashRemaining>0});
        }
        private void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
        [Serializable]private sealed class Sample{public string hero,sprite;public int direction,frame,hits;public float time;public bool dashing;}
        [Serializable]private sealed class Report{public int width,height;public Sample[] samples;public string[] errors;}
    }
}
#endif
