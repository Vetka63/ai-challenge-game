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
    // Opt-in deterministic QA only. These files are never produced in normal play.
    public sealed class ArenaTacticsCapture : MonoBehaviour
    {
        private readonly List<string> errors = new();
        private ArenaSimulation sim; private ArenaSession session; private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tactics-capture") >= 0)
                new GameObject("Tactical crowd verification").AddComponent<ArenaTacticsCapture>();
        }
        private void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
        private IEnumerator Start()
        {
            Application.logMessageReceived += OnLog;
            var args = Environment.GetCommandLineArgs(); output = Path.GetFullPath(args[Array.IndexOf(args, "--tactics-capture") + 1]); Directory.CreateDirectory(output);
            for (int i = 0; i < 12; i++) yield return null;
            var container = FindFirstObjectByType<SceneContext>().Container;
            sim = container.Resolve<ArenaSimulation>(); session = container.Resolve<ArenaSession>();
            var report = new Evidence { width = Screen.width, height = Screen.height, graphics = SystemInfo.graphicsDeviceType.ToString() };
            yield return new WaitForEndOfFrame(); Capture("menu-x1");
            Click("Session action 8"); Click("Session action 8");
            Check(sim.PlaybackSpeed == 4, "Menu failed to select x4");
            yield return new WaitForEndOfFrame(); Capture("menu-x4");
            Click("Session action 1"); yield return null; yield return new WaitForEndOfFrame(); Click("Choice 0");
            yield return new WaitForEndOfFrame();
            float before = sim.Elapsed, realtime = Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(1.2f); yield return new WaitForEndOfFrame();
            report.speedRatio = (sim.Elapsed - before) / (Time.realtimeSinceStartup - realtime);
            Check(math.abs(report.speedRatio - 4) < .25f, "Measured speed did not match x4");
            session.TogglePause(); float paused = sim.Elapsed; yield return new WaitForSecondsRealtime(.3f);
            Check(sim.Elapsed == paused, "Accelerated pause advanced time");
            yield return new WaitForEndOfFrame(); Capture("paused-x4"); Click("Session action 8"); Check(sim.PlaybackSpeed == 1, "Menu failed to return to x1");
            session.NewRun(73); sim.ChooseUpgrade(0);
            var state = sim.Snapshot(); state.elapsed = 360; state.initialEnemiesPending = false; sim.Restore(state);
            sim.Settings.spawnPerSecond = 0; sim.Settings.boltDamage = 0;
            var lancer = Add(3, new float2(8, 0), 1.8f, .4f);
            var bomber = Add(4, new float2(0, 7), 1.2f, .7f);
            var flanker = Add(5, new float2(-5, -2), 2.5f, 1f);
            // Surround the trio with 45 ordinary bodies to inspect real foreground layering.
            for (int i = 0; i < 45; i++)
            {
                float a = i * 2.39996f, r = 7 + i % 5 * .65f;
                var p = sim.HeroPosition + ArenaLayout.ScreenToWorld(new float2(math.cos(a), math.sin(a)) * r);
                if (sim.Layout.Walkable(p)) Add((byte)(i % 2 == 0 ? 0 : 2), p, .65f, 10);
            }
            for (int frame = 0; frame < 32; frame++)
            {
                yield return new WaitForSecondsRealtime(.1f); yield return new WaitForEndOfFrame();
                Capture("crowd-" + frame.ToString("00")); report.times.Add(sim.Elapsed);
            }
            report.foes = sim.EnemyQuery.CalculateEntityCount();
            Check(sim.World.EntityManager.Exists(lancer) && sim.World.EntityManager.Exists(bomber) && sim.World.EntityManager.Exists(flanker), "Specialist disappeared unexpectedly");
            Check(sim.WarningCount >= 3, "Not all specialist warnings ran");
            // Persist active specialist actions, reopen through actual session flow, compare all fields.
            session.TogglePause(); var expected = JsonUtility.ToJson(sim.Snapshot()); session.Save(); session.ToHome();
            yield return null; yield return new WaitForEndOfFrame(); Click("Session action 0");
            report.exactResume = expected == JsonUtility.ToJson(sim.Snapshot()); Check(report.exactResume, "Tactical state changed on Continue");
            session.TogglePause(); yield return new WaitForEndOfFrame(); Capture("restored-menu");
            report.errors = errors.ToArray(); File.WriteAllText(Path.Combine(output, "tactics-check.json"), JsonUtility.ToJson(report, true));
            Application.Quit(errors.Count == 0 ? 0 : 2);
        }
        private Entity Add(byte kind, float2 p, float speed, float timer)
        {
            var m = sim.World.EntityManager; var e = m.CreateEntity(typeof(Position2D), typeof(Enemy), typeof(Health));
            m.SetComponentData(e, new Position2D { Value = p }); m.SetComponentData(e, new Health { Value = 1000 });
            m.SetComponentData(e, new Enemy { Kind = kind, MaxHealth = 1000, Speed = speed, AttackTimer = timer }); return e;
        }
        private void Check(bool condition, string error) { if (!condition) errors.Add(error); }
        private void Capture(string name) => ArenaSmokeCapture.Capture(Path.Combine(output, name + ".png"));
        private void Click(string name)
        {
            var obj = GameObject.Find(name); var button = obj == null ? null : obj.GetComponent<UnityEngine.UI.Button>();
            if (button == null || !button.interactable) { errors.Add("Unavailable UI action: " + name); return; }
            button.onClick.Invoke();
        }
        [Serializable] private sealed class Evidence
        {
            public int width, height, foes; public string graphics; public float speedRatio; public bool exactResume;
            public List<float> times = new(); public string[] errors;
        }
    }
}
#endif
