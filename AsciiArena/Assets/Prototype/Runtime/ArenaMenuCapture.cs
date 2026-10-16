#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Zenject;

namespace AsciiArena
{
    // Opt-in GPU and input verification; always launch with an isolated --act-save-dir.
    [DefaultExecutionOrder(-10000)]
    public sealed class ArenaMenuCapture : MonoBehaviour
    {
        private readonly List<string> errors = new();
        private ArenaSimulation sim; private ArenaSession session; private string output;
        private Keyboard keyboard; private Key[] held = Array.Empty<Key>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--menu-capture") >= 0)
                new GameObject("Main menu verification").AddComponent<ArenaMenuCapture>();
        }
        private void Update()
        {
            if (keyboard == null) return;
            keyboard.MakeCurrent(); InputState.Change(keyboard, new KeyboardState(held));
        }
        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert || message.Contains("was not found in the") && message.Contains("font")) errors.Add(message);
        }
        private IEnumerator Press(params Key[] keys)
        {
            held = keys; yield return null; yield return null;
            held = Array.Empty<Key>(); yield return null; yield return new WaitForEndOfFrame();
        }
        private IEnumerator Start()
        {
            Application.logMessageReceived += OnLog;
            var args = Environment.GetCommandLineArgs(); output = Path.GetFullPath(args[Array.IndexOf(args, "--menu-capture") + 1]); Directory.CreateDirectory(output);
            for (int i = 0; i < 12; i++) yield return null;
            var container = FindFirstObjectByType<SceneContext>().Container;
            sim = container.Resolve<ArenaSimulation>(); session = container.Resolve<ArenaSession>();
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>();
            var report = new Evidence { width = Screen.width, height = Screen.height, graphics = SystemInfo.graphicsDeviceType.ToString(), resume = Array.IndexOf(args, "--menu-resume") >= 0 };
            yield return new WaitForEndOfFrame(); Capture("main-menu");
            Check(session.Home && sim.Paused, "Startup did not show paused home");
            Check(GameObject.Find("Menu speed 4") == null, "Speed leaked onto root menu");
            if (report.resume)
            {
                Check(sim.PlaybackSpeed == 4 && session.HasSavedRun, "Profile or save missing after process restart");
                string expected = File.ReadAllText(Path.Combine(session.Store.DirectoryPath, "menu-expected.json"));
                Click("Menu continue"); report.exactResume = expected == JsonUtility.ToJson(sim.Snapshot());
                Check(report.exactResume, "Continue changed saved combat state");
                yield return Press(Key.Escape); Click("Menu settings"); yield return null; yield return new WaitForEndOfFrame(); Capture("settings-restored");
            }
            else
            {
                Check(!GameObject.Find("Menu continue").GetComponent<UnityEngine.UI.Button>().interactable, "Continue enabled without save");
                Click("Menu settings"); yield return null; yield return new WaitForEndOfFrame(); Capture("settings-home");
                Click("Menu speed 2"); Check(sim.PlaybackSpeed == 2, "Home settings speed failed");
                yield return Press(Key.Escape); Check(session.Home && session.MenuPage == ArenaMenuPage.Root, "ESC from home settings failed");
                Click("Menu training"); yield return null; yield return new WaitForEndOfFrame(); Capture("training");
                Check(!GameObject.Find("Menu bellkeeper").GetComponent<UnityEngine.UI.Button>().interactable, "Unseen boss training enabled");
                yield return Press(Key.Escape); Click("Menu play"); yield return null; yield return new WaitForEndOfFrame();
                Check(session.MenuPage == ArenaMenuPage.Heroes, "Play did not open hero selection");
                Click("Menu hero start"); yield return null; yield return new WaitForEndOfFrame();
                Check(sim.Phase == RunPhase.ChoosingWeapon, "Play did not open weapon choice");
                yield return Press(Key.Escape); Click("Menu settings"); yield return null; yield return new WaitForEndOfFrame();
                yield return Press(Key.Digit1, Key.LeftShift); Check(sim.Phase == RunPhase.ChoosingWeapon, "Menu input chose a weapon or dashed");
                yield return Press(Key.Escape); Check(sim.Paused, "Settings ESC incorrectly resumed combat");
                yield return Press(Key.Escape); Check(!sim.Paused && sim.Phase == RunPhase.ChoosingWeapon, "Weapon choice lost on resume");
                Click("Choice 0"); yield return new WaitForSecondsRealtime(.4f); yield return Press(Key.Escape);
                string paused = JsonUtility.ToJson(sim.Snapshot());
                Click("Menu settings"); yield return null; yield return new WaitForEndOfFrame(); Click("Menu speed 4");
                yield return Press(Key.D, Key.R, Key.LeftShift, Key.Digit2); yield return new WaitForSecondsRealtime(.3f);
                Check(JsonUtility.ToJson(sim.Snapshot()) == paused, "Settings changed combat state");
                yield return new WaitForEndOfFrame(); Capture("settings-in-game-x4");
                yield return Press(Key.Escape); Check(sim.Paused && session.MenuPage == ArenaMenuPage.Root, "ESC failed to return to pause"); Capture("pause");
                yield return Press(Key.Escape);
                float before = sim.Elapsed, real = Time.realtimeSinceStartup;
                yield return new WaitForSecondsRealtime(.5f);
                report.speedRatio = (sim.Elapsed - before) / (Time.realtimeSinceStartup - real);
                Check(Mathf.Abs(report.speedRatio - 4) < .35f, "Combat did not resume at x4");
                yield return Press(Key.Escape); Click("Menu home"); yield return null; yield return new WaitForEndOfFrame();
                string saved = JsonUtility.ToJson(sim.Snapshot());
                Click("Menu play"); yield return null; yield return new WaitForEndOfFrame();
                Click("Menu hero start"); yield return null; yield return new WaitForEndOfFrame(); Capture("replace-confirmation");
                yield return Press(Key.Escape); Check(session.HasSavedRun && !session.ConfirmNew, "Cancel new run lost save");
                yield return Press(Key.Escape);
                Click("Menu training"); yield return null; yield return new WaitForEndOfFrame(); Click("Menu basics");
                yield return new WaitForSecondsRealtime(.1f); yield return Press(Key.Escape);
                Click("Menu settings"); yield return null; yield return new WaitForEndOfFrame(); Capture("settings-training");
                yield return Press(Key.Escape); Click("Menu home"); yield return null; yield return new WaitForEndOfFrame(); Click("Menu continue");
                report.exactResume = saved == JsonUtility.ToJson(sim.Snapshot()); Check(report.exactResume, "Training replaced expedition");
                yield return Press(Key.Escape); Click("Menu home"); yield return null; yield return new WaitForEndOfFrame();
                File.WriteAllText(Path.Combine(session.Store.DirectoryPath, "menu-expected.json"), JsonUtility.ToJson(sim.Snapshot()));
                Capture("main-with-save");
                // Exercise actual UI input module navigation, not only pointer dispatch.
                EventSystem.current.SetSelectedGameObject(GameObject.Find("Menu training"));
                yield return Press(Key.DownArrow); Check(EventSystem.current.currentSelectedGameObject == GameObject.Find("Menu settings"), "Keyboard navigation failed");
                yield return Press(Key.Enter); Check(session.MenuPage == ArenaMenuPage.Settings, "Keyboard submit failed");
                yield return Press(Key.Escape);
            }
            if (session.MenuPage != ArenaMenuPage.Root) { session.Back(); yield return null; yield return new WaitForEndOfFrame(); }
            report.errors = errors.ToArray(); File.WriteAllText(Path.Combine(output, "menu-check.json"), JsonUtility.ToJson(report, true));
            Click("Menu quit");
        }
        private void Capture(string name)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var button in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
            {
                if (!button.gameObject.activeInHierarchy) continue;
                var corners = new Vector3[4]; button.GetComponent<RectTransform>().GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var p = RectTransformUtility.WorldToScreenPoint(Camera.main, corner);
                    Check(p.x >= -1 && p.x <= Screen.width + 1 && p.y >= -1 && p.y <= Screen.height + 1, "Button clipped: " + button.name);
                }
            }
            foreach (var label in FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))
                if (label.gameObject.activeInHierarchy && label.transform.IsChildOf(GameObject.Find("Menu screen").transform))
                { label.ForceMeshUpdate(); Check(!label.isTextOverflowing, "Menu text overflow: " + label.text); }
            ArenaSmokeCapture.Capture(Path.Combine(output, name + ".png"));
        }
        private void Click(string name)
        {
            var obj = GameObject.Find(name); var button = obj == null ? null : obj.GetComponent<UnityEngine.UI.Button>();
            if (button == null || !button.interactable) { errors.Add("Unavailable UI action: " + name); return; }
            var rect = obj.GetComponent<RectTransform>();
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(Camera.main, rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            if (hits.Count == 0 || hits[0].gameObject != obj) { errors.Add("UI raycast blocked: " + name); return; }
            ExecuteEvents.Execute(obj, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(obj, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(obj, pointer, ExecuteEvents.pointerClickHandler);
        }
        private void Check(bool ok, string message) { if (!ok) errors.Add(message); }
        [Serializable] private sealed class Evidence { public int width, height; public string graphics; public bool resume, exactResume; public float speedRatio; public string[] errors; }
    }
}
#endif
