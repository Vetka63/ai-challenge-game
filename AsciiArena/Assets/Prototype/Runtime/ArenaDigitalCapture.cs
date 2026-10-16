#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace AsciiArena
{
    // Opt-in visual/integration QA uses its own profile and actual simulation damage.
    public sealed class ArenaDigitalCapture : MonoBehaviour
    {
        private readonly List<string> errors=new();
        private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"--digital-capture")>=0 && Array.IndexOf(args,"--act-save-dir")>=0)
                new GameObject("Digital world verification").AddComponent<ArenaDigitalCapture>();
        }
        private void Click(string name)
        {
            var obj=GameObject.Find(name);var button=obj==null?null:obj.GetComponent<Button>();
            if(button==null || !button.interactable)throw new InvalidOperationException("Unavailable button: "+name);
            button.onClick.Invoke();
        }
        private IEnumerator UiFrame(){yield return null;yield return new WaitForEndOfFrame();}
        private IEnumerator Start()
        {
            Application.logMessageReceived+=OnLog;
            var args=Environment.GetCommandLineArgs();output=Path.GetFullPath(args[Array.IndexOf(args,"--digital-capture")+1]);Directory.CreateDirectory(output);
            for(int i=0;i<20;i++)yield return null;
            var container=FindFirstObjectByType<SceneContext>().Container;
            var sim=container.Resolve<ArenaSimulation>();var session=container.Resolve<ArenaSession>();
            sim.DiagnosticManualTicks=true;
            var original=UnityEngine.Object.Instantiate(sim.Settings);var results=new List<Fight>();
            yield return new WaitForEndOfFrame();Capture("home");
            foreach(var hero in new[]{HeroIdentity.Spark,HeroIdentity.Pacman,HeroIdentity.Doomguy,HeroIdentity.Ascii})
            {
                if(!session.Home)session.ToHome();
                session.RequestNew();yield return UiFrame();Click("Menu hero "+(int)hero);
                yield return null;yield return new WaitForEndOfFrame();Capture("select-"+hero);
                Click("Menu hero start");yield return UiFrame();
                if(session.ConfirmNew){Click("Menu confirm");yield return UiFrame();}
                Check(sim.HeroIdentity==hero,"Selected hero not applied: "+hero);
                Click("Choice 0");yield return UiFrame();
                if(hero==HeroIdentity.Spark)
                {
                    session.Save();string saved=JsonUtility.ToJson(sim.Snapshot());
                    session.TogglePause();session.OpenMenuPage(ArenaMenuPage.Settings);
                    yield return UiFrame();Click("Menu boss tests");yield return UiFrame();Capture("boss-menu");
                    Click("Menu test core");yield return UiFrame();
                    Check(session.BossTesting && sim.FinalBossKind==SpecialBoss.Core,"Core test button failed");
                    session.Continue();Check(JsonUtility.ToJson(sim.Snapshot())==saved,"Core practice changed expedition");
                }
                var setup=ArenaBalanceProbe.Create(original,3,hero==HeroIdentity.Doomguy?1:0,hero==HeroIdentity.Ascii?0:2,13,hero==HeroIdentity.Spark?60:24);
                var state=setup.Snapshot();state.heroIdentity=hero;sim.Restore(state);
                setup.Dispose();UnityEngine.Object.Destroy(setup.Settings);
                var pilot=new ArenaBalancePilot();float start=sim.Elapsed;int frames=0;float next=0;
                var seen=new HashSet<int>();bool dash=false,hit=false,core=false;
                while(!sim.Finished && sim.BossActive && sim.Elapsed-start<150)
                {
                    for(int step=0;step<4 && !sim.Finished && sim.BossActive;step++)
                    {pilot.Tick(sim,2,1f/60);sim.Step(1f/60);}
                    yield return null;yield return new WaitForEndOfFrame();
                    if(sim.BossActive)
                    {
                        var e=sim.World.EntityManager.GetComponentData<Enemy>(sim.BossEntity);
                        if(e.Pattern>0 && seen.Add((e.Pattern-1)%3))Capture(hero+"-pattern-"+((e.Pattern-1)%3));
                        if(!core && sim.BossRecovering){core=true;Capture(hero+"-exposed");}
                    }
                    if(!dash && sim.DashRemaining>0){dash=true;Capture(hero+"-dash");}
                    if(!hit && sim.HurtFlash>.4f){hit=true;Capture(hero+"-hit");}
                    if(sim.Elapsed-start>=next && frames<60)
                    {Capture(hero+"-motion-"+(frames++).ToString("00"));next+=.4f;}
                }
                Capture(hero+"-result");
                results.Add(new Fight{hero=hero.ToString(),victory=sim.Phase==RunPhase.Victory,seconds=sim.Elapsed-start,health=sim.HeroHealth,dashes=pilot.Dashes,patterns=seen.Count});
                if(hero!=HeroIdentity.Ascii)Check(HeroSprites.Get(hero)!=null,"Missing sprite: "+hero);
            }
            // Continue through the real new-run finale, including saving during its transition.
            session.NewRun(13);
            var finale=ArenaBalanceProbe.Create(original,2,0,2,13,24);
            var finaleState=finale.Snapshot();finaleState.coreRequired=true;finaleState.heroIdentity=HeroIdentity.Spark;
            sim.Restore(finaleState);finale.Dispose();UnityEngine.Object.Destroy(finale.Settings);
            var finalePilot=new ArenaBalancePilot();float finaleStart=sim.Elapsed;
            bool transitionSaved=false,kernelSeen=false;
            while(!sim.Finished && sim.Elapsed-finaleStart<300)
            {
                for(int step=0;step<4 && !sim.Finished;step++){finalePilot.Tick(sim,2,1f/60);sim.Step(1f/60);}
                yield return null;yield return new WaitForEndOfFrame();
                if(!transitionSaved && sim.CoreTransition>0)
                {
                    Capture("finale-transition");string expected=JsonUtility.ToJson(sim.Snapshot());
                    session.Save();session.ToHome();session.Continue();
                    Check(JsonUtility.ToJson(sim.Snapshot())==expected,"Transition changed on save/continue");transitionSaved=true;
                }
                if(!kernelSeen && sim.BossActive && sim.FinalBossKind==SpecialBoss.Core)
                {kernelSeen=true;Capture("finale-kernel");}
            }
            Check(transitionSaved && kernelSeen && sim.Phase==RunPhase.Victory,"Full finale did not complete");
            Capture("finale-exit");
            results.Add(new Fight{hero="Spark / archive + kernel",victory=sim.Phase==RunPhase.Victory,seconds=sim.Elapsed-finaleStart,health=sim.HeroHealth,dashes=finalePilot.Dashes});
            UnityEngine.Object.Destroy(original);
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{width=Screen.width,height=Screen.height,graphics=SystemInfo.graphicsDeviceType.ToString(),fights=results.ToArray(),errors=errors.ToArray()},true));
            Application.Quit(errors.Count==0?0:2);
        }
        private void OnLog(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert||message.Contains("was not found in the")&&message.Contains("font"))errors.Add(message);}
        private void Check(bool ok,string message){if(!ok)errors.Add(message);}
        private void Capture(string name)
        {
            foreach(var label in FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))if(label.gameObject.activeInHierarchy)
            {label.ForceMeshUpdate();Check(!label.isTextOverflowing,"Text overflow: "+label.text);}
            foreach(var button in FindObjectsByType<Button>(FindObjectsSortMode.None))if(button.gameObject.activeInHierarchy)
            {
                var corners=new Vector3[4];button.GetComponent<RectTransform>().GetWorldCorners(corners);
                foreach(var corner in corners){var p=RectTransformUtility.WorldToScreenPoint(Camera.main,corner);Check(p.x>=-1&&p.x<=Screen.width+1&&p.y>=-1&&p.y<=Screen.height+1,"Clipped button: "+button.name);}
            }
            ArenaSmokeCapture.Capture(Path.Combine(output,name+".png"));
        }
        [Serializable]private sealed class Fight{public string hero;public bool victory;public float seconds,health;public int dashes,patterns;}
        [Serializable]private sealed class Report{public int width,height;public string graphics;public Fight[] fights;public string[] errors;}
    }
}
#endif
