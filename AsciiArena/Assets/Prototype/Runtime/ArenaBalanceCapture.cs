#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using Zenject;

namespace AsciiArena
{
    // Actual rendered combat driven by the same delayed diagnostic pilot as the batch.
    public sealed class ArenaBalanceCapture : MonoBehaviour
    {
        private readonly List<string> errors=new();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"--balance-capture")>=0 && Array.IndexOf(args,"--act-save-dir")>=0)
                new GameObject("Balance GPU verification").AddComponent<ArenaBalanceCapture>();
        }
        private IEnumerator Start()
        {
            Application.logMessageReceived+=OnLog;
            var args=Environment.GetCommandLineArgs();string output=Path.GetFullPath(args[Array.IndexOf(args,"--balance-capture")+1]);Directory.CreateDirectory(output);
            for(int i=0;i<20;i++)yield return null;
            var container=FindFirstObjectByType<SceneContext>().Container;
            var sim=container.Resolve<ArenaSimulation>();var session=container.Resolve<ArenaSession>();
            var original=UnityEngine.Object.Instantiate(sim.Settings);var results=new List<ArenaBalanceProbe.Result>();
            for(int run=0;run<4;run++)
            {
                int boss=run<3?run:1, rank=run<3?2:0, crowd=run<3?60:24;
                session.NewRun(13);
                var setup=ArenaBalanceProbe.Create(original,boss,run==2?2:0,rank,13,crowd);
                sim.Restore(setup.Snapshot());setup.Dispose();UnityEngine.Object.Destroy(setup.Settings);
                sim.DiagnosticManualTicks=true;var pilot=new ArenaBalancePilot();float start=sim.Elapsed;
                string name=(boss==0?"warden":boss==1?"bell":"archive")+(rank==0?"-basic":"-crowd60");
                int frames=0,patterns=0,peakHazards=0,peakEnemies=0;var seen=new HashSet<int>();
                bool recovery=false,phaseTwo=false;float captureAt=0;
                while(!sim.Finished&&sim.BossActive&&sim.Elapsed-start<150)
                {
                    for(int step=0;step<4 && sim.BossActive&&!sim.Finished;step++)
                    {
                        pilot.Tick(sim,2,1f/60);sim.Step(1f/60);
                    }
                    yield return null;yield return new WaitForEndOfFrame();
                    if(sim.BossActive)
                    {
                        var e=sim.World.EntityManager.GetComponentData<Enemy>(sim.BossEntity);patterns=Math.Max(patterns,e.Pattern);
                        peakHazards=Math.Max(peakHazards,sim.Hazards.Count);peakEnemies=Math.Max(peakEnemies,sim.EnemyQuery.CalculateEntityCount());
                        if(e.Pattern>0&&seen.Add((e.Pattern-1)%3))Capture(Path.Combine(output,name+"-pattern"+((e.Pattern-1)%3)+".png"));
                        if(!recovery&&sim.BossRecovering){recovery=true;Capture(Path.Combine(output,name+"-core.png"));}
                        if(!phaseTwo&&sim.BossHealthRatio<.5f){phaseTwo=true;Capture(Path.Combine(output,name+"-phase2.png"));}
                    }
                    if(sim.Elapsed-start>=captureAt && frames<40)
                    {Capture(Path.Combine(output,name+"-motion-"+(frames++).ToString("00")+".png"));captureAt+=.6f;}
                }
                Capture(Path.Combine(output,name+"-result.png"));
                results.Add(new ArenaBalanceProbe.Result{boss=boss,pair=run==2?2:0,rank=rank,policy=2,seed=13,crowd=crowd,
                    victory=!sim.BossActive||sim.Phase==RunPhase.Victory,seconds=sim.Elapsed-start,health=sim.HeroHealth,
                    bossHealth=sim.BossHealthRatio,damage=sim.BossDamageTaken,hits=sim.HurtCount,dashes=pilot.Dashes,
                    patterns=patterns,peakEnemies=peakEnemies,peakHazards=peakHazards});
            }
            sim.DiagnosticManualTicks=false;UnityEngine.Object.Destroy(original);
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{width=Screen.width,height=Screen.height,graphics=SystemInfo.graphicsDeviceType.ToString(),cases=results.ToArray(),errors=errors.ToArray()},true));
            Application.Quit(errors.Count==0?0:2);
        }
        private void OnLog(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert||message.Contains("was not found in the")&&message.Contains("font"))errors.Add(message);}
        private void Capture(string path)
        {
            foreach(var text in FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))if(text.gameObject.activeInHierarchy)
            {text.ForceMeshUpdate();if(text.isTextOverflowing)errors.Add("HUD overflow: "+text.text);}
            ArenaSmokeCapture.Capture(path);
        }
        [Serializable] private sealed class Report{public int width,height;public string graphics;public ArenaBalanceProbe.Result[] cases;public string[] errors;}
    }
}
#endif
