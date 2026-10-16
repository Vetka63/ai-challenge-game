#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using Zenject;

namespace AsciiArena
{
    // Explicit GPU regression scenario, never enabled in ordinary play.
    // Isolated saves, no outgoing damage; the stationary observer is healed for inspection.
    public sealed class ArenaBossCapture : MonoBehaviour
    {
        private readonly List<string> errors = new();
        private ArenaSimulation sim;
        private bool observing;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--boss-capture")>=0)
                new GameObject("Boss visual regression").AddComponent<ArenaBossCapture>();
        }
        private void Update()
        {
            if(observing && sim!=null && sim.World.IsCreated)
                sim.World.EntityManager.SetComponentData(sim.HeroEntity,new Health{Value=sim.MaxHealth});
        }
        private void OnLog(string text,string stack,LogType type)
        {
            if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert||text.Contains("was not found in the")&&text.Contains("font"))errors.Add(text);
        }
        private IEnumerator Start()
        {
            Application.logMessageReceived+=OnLog;
            var args=Environment.GetCommandLineArgs();
            string output=Path.GetFullPath(args[Array.IndexOf(args,"--boss-capture")+1]);Directory.CreateDirectory(output);
            for(int i=0;i<20;i++)yield return null;
            var container=FindFirstObjectByType<SceneContext>().Container;
            sim=container.Resolve<ArenaSimulation>();var session=container.Resolve<ArenaSession>();
            var records=new List<Encounter>();
            for(int run=0;run<5;run++)
            {
                bool warden=run==0, phaseTwo=run>=3;
                var boss=run==2||run==4?SpecialBoss.Archivist:SpecialBoss.Bellkeeper;
                session.Profile.bossTestSecondPhase=phaseTwo;
                session.TestBoss(boss,warden);
                sim.Settings.boltDamage=sim.Settings.pulseDamage=sim.Settings.orbitDamage=0;
                sim.SetPlaybackSpeed(1);observing=true;
                string name=warden?"warden":boss==SpecialBoss.Bellkeeper?"bell":"archive";
                name+=phaseTwo?"-phase2":"-phase1";
                yield return new WaitForSecondsRealtime(.2f);yield return new WaitForEndOfFrame();
                Capture(Path.Combine(output,name+"-identity.png"));
                sim.SetPlaybackSpeed(4);
                float start=Time.realtimeSinceStartup;int frame=0,maxPattern=0;
                bool recovery=false,charge=false,inward=false,disk=false;
                var patterns=new HashSet<int>();
                while(Time.realtimeSinceStartup-start<15)
                {
                    yield return new WaitForSecondsRealtime(.25f);yield return new WaitForEndOfFrame();
                    if(!sim.BossActive||sim.Finished){errors.Add(name+": battle ended during inspection");break;}
                    var e=sim.World.EntityManager.GetComponentData<Enemy>(sim.BossEntity);
                    maxPattern=math.max(maxPattern,e.Pattern);recovery|=sim.BossRecovering;charge|=e.ChargeLeft>0||e.BossChargePending;
                    foreach(var h in sim.Hazards){inward|=h.Ring&&ArenaSimulation.RingSpeed(h)<0;disk|=h.Disk;}
                    if(e.Pattern>0&&patterns.Add((e.Pattern-1)%3))Capture(Path.Combine(output,name+"-pattern"+((e.Pattern-1)%3)+".png"));
                    if(frame<36)Capture(Path.Combine(output,name+"-motion-"+frame.ToString("00")+".png"));
                    frame++;
                }
                if(maxPattern<4||!recovery)errors.Add(name+": incomplete natural cycle or no recovery");
                records.Add(new Encounter{name=name,patterns=maxPattern,recovery=recovery,charge=charge,inwardRing=inward,disk=disk,simulatedSeconds=sim.Elapsed});
            }
            observing=false;session.ToHome();
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{width=Screen.width,height=Screen.height,graphics=SystemInfo.graphicsDeviceType.ToString(),encounters=records.ToArray(),errors=errors.ToArray()},true));
            Application.Quit(errors.Count==0?0:2);
        }
        private void Capture(string path)
        {
            Canvas.ForceUpdateCanvases();
            foreach(var label in FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))
                if(label.gameObject.activeInHierarchy){label.ForceMeshUpdate();if(label.isTextOverflowing)errors.Add("HUD text overflow: "+label.text);}
            ArenaSmokeCapture.Capture(path);
        }
        [Serializable] private sealed class Report { public int width,height;public string graphics;public Encounter[] encounters;public string[] errors; }
        [Serializable] private sealed class Encounter { public string name;public int patterns;public float simulatedSeconds;public bool recovery,charge,inwardRing,disk; }
    }
}
#endif
