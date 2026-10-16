#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Zenject;

namespace AsciiArena
{
    // Explicit opt-in QA; all save files are isolated by --act-save-dir.
    [DefaultExecutionOrder(-10000)]
    public sealed class ArenaActCapture : MonoBehaviour
    {
        private readonly List<string> errors = new();
        private ArenaSimulation sim; private ArenaSession session;
        private Keyboard keyboard; private bool walking;
        private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--act-capture") >= 0)
                new GameObject("Act one verification").AddComponent<ArenaActCapture>();
        }
        private void Update()
        {
            if (keyboard == null) return;
            keyboard.MakeCurrent(); InputState.Change(keyboard, walking ? new KeyboardState(Key.D) : new KeyboardState());
        }
        private void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
        private IEnumerator Start()
        {
            Application.logMessageReceived += OnLog;
            var args = Environment.GetCommandLineArgs();
            output = Path.GetFullPath(args[Array.IndexOf(args, "--act-capture") + 1]); Directory.CreateDirectory(output);
            for (int i = 0; i < 12; i++) yield return null;
            var container = FindFirstObjectByType<SceneContext>().Container;
            sim = container.Resolve<ArenaSimulation>(); session = container.Resolve<ArenaSession>();
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>();
            var report = new Evidence { width = Screen.width, height = Screen.height, graphics = SystemInfo.graphicsDeviceType.ToString(), mode = Array.IndexOf(args, "--act-resume") >= 0 ? "resume" : "save" };
            yield return new WaitForEndOfFrame(); Capture("main-menu");
            if (report.mode == "save")
            {
                Click("Session action 1");
                yield return null; yield return new WaitForEndOfFrame(); Capture("weapon-choice");
                Click("Choice 0");
                float x = sim.HeroPosition.x; walking = true;
                for (int i = 0; i < 20; i++)
                { yield return new WaitForSecondsRealtime(.1f); yield return new WaitForEndOfFrame(); Capture("move-" + i.ToString("00")); }
                walking = false;
                Check(sim.HeroPosition.x > x + 5, "Real keyboard movement failed");
                // Jump only the schedule, retaining production tuning. This is not a balance playthrough.
                var state = sim.Snapshot(); state.elapsed = 1619.9f; state.nextElite = 1800;
                state.nextWaveBoss = 1800; state.waveBossesSpawned = state.waveBossesDefeated = 2;
                state.finalBoss = SpecialBoss.Bellkeeper; sim.Restore(state);
                float deadline = Time.realtimeSinceStartup + 4;
                while (!sim.BossActive && Time.realtimeSinceStartup < deadline) yield return null;
                Check(sim.BossActive, "Final boss did not spawn");
                if (sim.BossActive)
                {
                    sim.World.EntityManager.SetComponentData(sim.BossEntity, new Position2D { Value = sim.HeroPosition + new float2(6, 0) });
                    deadline = Time.realtimeSinceStartup + 5;
                    while (sim.Hazards.Count == 0 && Time.realtimeSinceStartup < deadline) yield return null;
                    yield return new WaitForEndOfFrame(); Capture("bellkeeper-warning");
                }
                session.TogglePause(); yield return null;
                Check(sim.Paused, "Pause failed");
                float elapsed = sim.Elapsed; yield return new WaitForSecondsRealtime(.3f);
                Check(sim.Elapsed == elapsed, "Pause advanced the clock");
                Click("Session action 5");
                File.WriteAllText(Path.Combine(session.Store.DirectoryPath, "expected.json"), JsonUtility.ToJson(sim.Snapshot()));
                Check(session.Unlocked(SpecialBoss.Bellkeeper), "Encounter did not unlock training");
                yield return new WaitForEndOfFrame(); Capture("saved-pause-menu");
            }
            else
            {
                Check(session.HasSavedRun, "Saved run missing after process restart");
                string expected = File.ReadAllText(Path.Combine(session.Store.DirectoryPath, "expected.json"));
                Click("Session action 0");
                report.exactResume = JsonUtility.ToJson(sim.Snapshot()) == expected;
                Check(report.exactResume, "Reloaded combat state differs from save");
                Check(session.Unlocked(SpecialBoss.Bellkeeper), "Training unlock lost after process restart");
                yield return new WaitForEndOfFrame(); Capture("resumed-warning");
                for (int i = 0; i < 24; i++)
                { yield return new WaitForSecondsRealtime(.08f); yield return new WaitForEndOfFrame(); Capture("ring-" + i.ToString("00")); }
                session.TogglePause(); session.Save();
                string before = JsonUtility.ToJson(sim.Snapshot());
                session.Profile.encountered = 3; // QA access to both variants; never a player unlock path.
                session.Practice(SpecialBoss.Archivist);
                float deadline = Time.realtimeSinceStartup + 5;
                while (sim.Hazards.Count == 0 && Time.realtimeSinceStartup < deadline) yield return null;
                yield return new WaitForEndOfFrame(); Capture("archivist-warning");
                for (int i = 0; i < 24; i++)
                { yield return new WaitForSecondsRealtime(.08f); yield return new WaitForEndOfFrame(); Capture("lanes-" + i.ToString("00")); }
                session.Save();
                // Practice saves the expedition once on entry, so compare its payload, not its envelope backup.
                var persisted = session.Store.Read<ArenaSnapshot>("run", ArenaSimulation.ValidSnapshot, out _);
                report.trainingPreserved = persisted != null && JsonUtility.ToJson(persisted) == before;
                Check(report.trainingPreserved, "Training replaced the expedition");
                session.RetryTraining(); Check(sim.Elapsed == 0 && sim.HeroHealth == sim.MaxHealth, "Training retry failed");
                session.ToHome(); yield return null; yield return new WaitForEndOfFrame(); Click("Session action 0");
                Check(!sim.Training && sim.Elapsed > 1620, "Return from training did not restore expedition");
                session.TogglePause(); yield return null; yield return new WaitForEndOfFrame(); Capture("resume-after-training");
            }
            report.errors = errors.ToArray();
            File.WriteAllText(Path.Combine(output, "act-check.json"), JsonUtility.ToJson(report, true));
            InputSystem.RemoveDevice(keyboard); keyboard = null; Application.Quit(errors.Count == 0 ? 0 : 2);
        }
        private void Check(bool condition, string error) { if (!condition) errors.Add(error); }
        private void Click(string name)
        {
            var obj = GameObject.Find(name); var button = obj == null ? null : obj.GetComponent<UnityEngine.UI.Button>();
            if (button == null || !button.interactable) { errors.Add("Unavailable UI action: " + name); return; }
            button.onClick.Invoke();
        }
        private void Capture(string name) => ArenaSmokeCapture.Capture(Path.Combine(output, name + ".png"));
        [Serializable] private sealed class Evidence
        { public string mode, graphics; public int width, height; public bool exactResume, trainingPreserved; public string[] errors; }
    }
}
#endif
