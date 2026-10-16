#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Zenject;
namespace AsciiArena
{
    // Opt-in diagnostic only: controlled loadout setup, followed by real default-rule combat.
    public sealed class ArenaCombatCapture:MonoBehaviour
    {
        private ArenaSimulation sim;private string output;private readonly List<string> errors=new();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"--combat-capture")>=0)new GameObject("Combat verification").AddComponent<ArenaCombatCapture>();}
        private void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
        private IEnumerator Start()
        {
            Application.logMessageReceived+=OnLog;
            var args=Environment.GetCommandLineArgs();output=Path.GetFullPath(args[Array.IndexOf(args,"--combat-capture")+1]);Directory.CreateDirectory(output);
            for(int i=0;i<12;i++)yield return null;
            sim=FindFirstObjectByType<SceneContext>().Container.Resolve<ArenaSimulation>();
            yield return new WaitForEndOfFrame();Capture("starter");
            if(sim.Elapsed!=0)errors.Add("Starter choice did not freeze time");
            var runs=new List<RunEvidence>();
            for(int starter=0;starter<3;starter++)
            {
                sim.Reset();yield return null;yield return new WaitForEndOfFrame();
                ClickChoice(starter);
                if(!sim.HasWeapon((WeaponKind)starter))errors.Add("Starter UI failed "+starter);
                Grant(starter==0?UpgradeKind.LearnWard:starter==1?UpgradeKind.LearnOrbit:UpgradeKind.LearnBolt);
                yield return new WaitForEndOfFrame();Capture("second-weapon-"+starter);
                ClickChoice(0);
                foreach(var upgrade in starter==0?new[]{UpgradeKind.Pierce,UpgradeKind.Pierce,UpgradeKind.Fan}:starter==1?new[]{UpgradeKind.Echo,UpgradeKind.OrbitCount}:new[]{UpgradeKind.OrbitCount,UpgradeKind.OrbitCount,UpgradeKind.OrbitPower})
                {Grant(upgrade);sim.ChooseUpgrade(0);}
                sim.DemoMode=true;
                for(int i=0;i<2400&&sim.Elapsed<40&&!sim.Finished;i++)
                {if(sim.Phase==RunPhase.ChoosingUpgrade)sim.ChooseUpgrade(0);sim.Step(1f/60);}
                var run=new RunEvidence{starter=starter,first=sim.WeaponAt(0).ToString(),second=sim.WeaponAt(1).ToString()};
                for(int f=0;f<40&&!sim.Finished;f++)
                {
                    if(sim.Phase==RunPhase.ChoosingUpgrade)sim.ChooseUpgrade(0);
                    yield return new WaitForSecondsRealtime(.10f);yield return new WaitForEndOfFrame();
                    Capture("build-"+starter+"-"+f.ToString("00"));
                    run.samples.Add(new Sample{frame=Time.frameCount,time=sim.Elapsed,hp=sim.HeroHealth,enemies=sim.EnemyQuery.CalculateEntityCount(),boltHits=sim.BoltHits,wardHits=sim.PulseHits,orbitHits=sim.OrbitHits,hurt=sim.HurtCount});
                }
                run.phase=sim.Phase.ToString();runs.Add(run);
                if(run.samples.Count<40)errors.Add("Build ended early "+starter);
            }
            // Controlled damage, dash and warning captures are separate from combat recordings.
            sim.Reset();sim.ChooseUpgrade(0);ClearCombat();
            var manager=sim.World.EntityManager;
            var shot=manager.CreateEntity(typeof(Position2D),typeof(Bolt));
            manager.SetComponentData(shot,new Position2D{Value=sim.HeroPosition+new float2(.5f,0)});
            manager.SetComponentData(shot,new Bolt{Hostile=true,Damage=8,Velocity=new float2(-20,0),Lifetime=1});
            sim.Step(1f/60);yield return new WaitForEndOfFrame();Capture("hurt");
            bool hurt=sim.HurtCount==1&&sim.LastDamage==8;
            var keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
            var before=sim.HeroPosition;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D,Key.LeftShift));
            yield return null;yield return new WaitForEndOfFrame();
            bool dash=sim.DashRemaining>0&&math.distancesq(before,sim.HeroPosition)>0;
            Capture("dash");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            sim.Reset();sim.ChooseUpgrade(0);ClearCombat();
            before=sim.HeroPosition;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));
            yield return null;yield return null;yield return new WaitForEndOfFrame();
            bool move=sim.HeroPosition.x>before.x;
            before=sim.HeroPosition;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            yield return null;yield return null;yield return new WaitForEndOfFrame();
            bool stop=math.distancesq(before,sim.HeroPosition)<.000001f;
            sim.Reset();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Digit3));
            yield return null;yield return new WaitForEndOfFrame();
            bool hotkey=sim.HasWeapon(WeaponKind.Orbit);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            InputSystem.RemoveDevice(keyboard);
            sim.Reset();sim.ChooseUpgrade(1);ClearCombat();
            Enemy(sim.HeroPosition+new float2(6,2),false);Enemy(sim.HeroPosition+new float2(-5,3),true);
            for(int i=0;i<24;i++)sim.Step(1f/60);
            yield return new WaitForEndOfFrame();Capture("danger-windup");
            for(int i=0;i<50;i++)sim.Step(1f/60);
            yield return new WaitForEndOfFrame();Capture("danger-release");
            bool warnings=sim.WarningCount>=2;
            if(!hurt)errors.Add("Damage feedback event missing");if(!dash)errors.Add("Shift input did not dash");if(!warnings)errors.Add("Warning events missing");
            if(!move||!stop)errors.Add("Movement start/release input failed");if(!hotkey)errors.Add("Weapon keyboard choice failed");
            File.WriteAllText(Path.Combine(output,"combat-check.json"),JsonUtility.ToJson(new Evidence{width=Screen.width,height=Screen.height,mode=Screen.fullScreenMode.ToString(),hurt=hurt,dash=dash,warnings=warnings,move=move,stop=stop,hotkey=hotkey,runs=runs,errors=errors.ToArray()},true));
            Application.Quit(errors.Count==0?0:2);
        }
        private void Grant(UpgradeKind kind)
        {
            if(sim.Phase==RunPhase.ChoosingUpgrade)sim.ChooseUpgrade(0);
            var m=sim.World.EntityManager;var xp=m.CreateEntity(typeof(Position2D),typeof(Experience));
            m.SetComponentData(xp,new Position2D{Value=sim.HeroPosition});m.SetComponentData(xp,new Experience{Value=sim.NextLevel});
            sim.Step(1f/60);
            if(!sim.Eligible(kind))errors.Add("Invalid controlled upgrade "+kind);
            sim.Offers[0]=kind;
        }
        private void ClearCombat(){var m=sim.World.EntityManager;m.DestroyEntity(sim.EnemyQuery);m.DestroyEntity(sim.BoltQuery);m.DestroyEntity(sim.PickupQuery);}
        private void Enemy(float2 position,bool boss)
        {
            var m=sim.World.EntityManager;var e=m.CreateEntity(typeof(Position2D),typeof(Enemy),typeof(Health));
            m.SetComponentData(e,new Position2D{Value=position});m.SetComponentData(e,new Enemy{Kind=1,Boss=boss,MaxHealth=1000});m.SetComponentData(e,new Health{Value=1000});
        }
        private void ClickChoice(int index)
        {
            var button=FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).First(b=>b.name=="Choice "+index);
            Canvas.ForceUpdateCanvases();
            var rect=(RectTransform)button.transform;
            var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(Camera.main,rect.TransformPoint(rect.rect.center))};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            if(hits.Count==0||hits[0].gameObject!=button.gameObject)errors.Add("Choice raycast did not reach button "+index);
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
        }
        private void Capture(string name)=>ArenaSmokeCapture.Capture(Path.Combine(output,name+".png"));
        private void OnDestroy(){Application.logMessageReceived-=OnLog;}
        [Serializable]private sealed class Sample{public int frame,enemies,boltHits,wardHits,orbitHits,hurt;public float time,hp;}
        [Serializable]private sealed class RunEvidence{public int starter;public string first,second,phase;public List<Sample> samples=new();}
        [Serializable]private sealed class Evidence{public int width,height;public string mode;public bool hurt,dash,warnings,move,stop,hotkey;public List<RunEvidence> runs;public string[] errors;}
    }
}
#endif
