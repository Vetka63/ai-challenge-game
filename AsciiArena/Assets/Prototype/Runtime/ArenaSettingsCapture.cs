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
    public sealed class ArenaSettingsCapture : MonoBehaviour
    {
        private readonly List<string> errors = new();
        private ArenaSimulation sim; private ArenaSession session; private string output;
        private Keyboard keyboard; private Key[] held = Array.Empty<Key>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--settings-capture") >= 0)
                new GameObject("Settings and boss test verification").AddComponent<ArenaSettingsCapture>();
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
            var args = Environment.GetCommandLineArgs(); output = Path.GetFullPath(args[Array.IndexOf(args, "--settings-capture") + 1]); Directory.CreateDirectory(output);
            for (int i = 0; i < 20; i++) yield return null;
            var container = FindFirstObjectByType<SceneContext>().Container;
            sim = container.Resolve<ArenaSimulation>(); session = container.Resolve<ArenaSession>();
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>();
            var report = new Evidence { width = Screen.width, height = Screen.height, graphics = SystemInfo.graphicsDeviceType.ToString(), resume = Array.IndexOf(args, "--settings-resume") >= 0 };
            yield return new WaitForEndOfFrame();
            if (report.resume)
            {
                Check(!Screen.fullScreen && !session.Fullscreen && !session.Profile.fullscreen, "Saved window mode not restored without launch override");
                Check(!ArenaDisplay.CrtEnabled && !session.Profile.crtEnabled, "Saved CRT not restored");
                Check(Mathf.Abs(AudioListener.volume - .36f) < .001f && !session.Profile.muted, "Saved volume not restored");
                Check(session.Profile.bossTestSecondPhase, "Boss test phase not restored");
                string expected = File.ReadAllText(Path.Combine(session.Store.DirectoryPath, "settings-expected.json"));
                Click("Menu continue"); report.exactResume = expected == JsonUtility.ToJson(sim.Snapshot()); Check(report.exactResume, "Restart changed expedition");
                yield return Press(Key.Escape); Click("Menu settings"); yield return null; yield return new WaitForEndOfFrame(); Capture("restored-settings");
                yield return Press(Key.Escape); Click("Menu home"); yield return null; yield return new WaitForEndOfFrame();
            }
            else
            {
                Click("Menu settings"); yield return null; yield return new WaitForEndOfFrame(); Capture("settings-crt-on");
                SetSlider(.35f); yield return null;
                Check(Mathf.Abs(AudioListener.volume - .35f) < .001f, "Volume pointer control failed");
                EventSystem.current.SetSelectedGameObject(GameObject.Find("Menu volume")); yield return Press(Key.RightArrow);
                Check(Mathf.Abs(AudioListener.volume - .36f) < .001f, "Volume keyboard control failed");
                yield return Press(Key.M); Check(AudioListener.volume == 0, "Mute shortcut did not mute");
                yield return Press(Key.M); Check(Mathf.Abs(AudioListener.volume - .36f) < .001f, "Mute did not restore volume");
                Click("Menu crt"); yield return null; yield return new WaitForEndOfFrame(); Capture("settings-crt-off");
                Check(!ArenaDisplay.CrtEnabled && Shader.GetGlobalFloat("_ArenaCrtBypass") == 1, "CRT switch failed");
                yield return Press(Key.F2); Check(ArenaDisplay.CrtEnabled && session.Profile.crtEnabled, "CRT shortcut not synchronized");
                Click("Menu crt");
                Click("Menu fullscreen"); for (int i = 0; i < 25; i++) yield return null;
                Check(Screen.fullScreen && session.Fullscreen, "Fullscreen UI did not apply");
                report.fullscreenWidth = Screen.width; report.fullscreenHeight = Screen.height;
                yield return new WaitForEndOfFrame(); Capture("settings-fullscreen");
                yield return Press(Key.F11); for (int i = 0; i < 25; i++) yield return null;
                Check(!Screen.fullScreen && !session.Profile.fullscreen, "Fullscreen shortcut did not persist window mode");
                yield return new WaitForEndOfFrame(); Capture("settings-windowed");
                Click("Menu speed 1"); yield return Press(Key.Escape); Click("Menu play"); yield return null; yield return new WaitForEndOfFrame();
                Click("Menu hero start"); yield return null; yield return new WaitForEndOfFrame(); Click("Choice 0");
                yield return new WaitForSecondsRealtime(.2f); yield return Press(Key.Escape);
                string expected = JsonUtility.ToJson(sim.Snapshot());
                Click("Menu settings"); yield return null; yield return new WaitForEndOfFrame();
                SetSlider(0); yield return null; Check(AudioListener.volume == 0, "Zero volume did not silence audio");
                SetSlider(.36f); Click("Menu crt"); Click("Menu crt");
                yield return new WaitForSecondsRealtime(.4f); Check(expected == JsonUtility.ToJson(sim.Snapshot()), "Preferences changed paused combat");
                Click("Menu boss tests"); yield return null; yield return new WaitForEndOfFrame(); Capture("boss-selection");
                Check(session.Profile.encountered == 0, "Test needs a fresh locked profile");
                Click("Menu test crowd"); Check(session.Profile.bossTestCrowd, "Crowd toggle did not enable");
                Click("Menu test warden"); Check(sim.TrainingCrowd && sim.EnemyQuery.CalculateEntityCount()==25, "Crowd practice did not start");
                yield return Press(Key.Escape); Click("Menu settings"); yield return null; yield return new WaitForEndOfFrame();
                Click("Menu boss tests"); yield return null; yield return new WaitForEndOfFrame();
                Click("Menu test crowd"); Check(!session.Profile.bossTestCrowd, "Crowd toggle did not disable");
                Click("Menu test warden"); yield return null; yield return new WaitForEndOfFrame();
                Check(session.BossTesting && sim.BossActive && session.Basics, "Warden test failed");
                yield return new WaitForSecondsRealtime(2.15f); yield return new WaitForEndOfFrame(); Capture("warden");
                yield return Press(Key.Escape); Click("Menu settings"); yield return null; yield return new WaitForEndOfFrame();
                Click("Menu boss tests"); yield return null; yield return new WaitForEndOfFrame(); Click("Menu test phase");
                Click("Menu test archivist"); Check(sim.BossHealthRatio < .5f, "Phase two not active");
                yield return new WaitForSecondsRealtime(2.15f); yield return new WaitForEndOfFrame(); Capture("archivist-phase-two");
                Check(sim.Hazards.Count > 0 && sim.WarningCount > 0, "Archivist did not execute attacks");
                yield return Press(Key.Escape); Click("Menu retry"); Check(sim.BossHealthRatio > .44f && sim.BossHealthRatio < .46f, "Retry lost phase two");
                yield return Press(Key.Escape); Click("Menu settings"); yield return null; yield return new WaitForEndOfFrame();
                Click("Menu boss tests"); yield return null; yield return new WaitForEndOfFrame(); Click("Menu test bellkeeper");
                yield return new WaitForSecondsRealtime(2.15f); yield return new WaitForEndOfFrame(); Capture("bellkeeper-phase-two");
                Check(sim.Hazards.Count >= 2 && sim.WarningCount > 0, "Bellkeeper did not execute phase two attacks");
                yield return Press(Key.Escape); yield return new WaitForEndOfFrame(); Capture("test-pause");
                Click("Menu return expedition"); report.exactResume = expected == JsonUtility.ToJson(sim.Snapshot()); Check(report.exactResume, "Boss tests replaced expedition");
                Check(session.Profile.encountered == 0 && session.Profile.victories == 0, "Boss test awarded campaign progression");
                yield return Press(Key.Escape); Click("Menu home"); yield return null; yield return new WaitForEndOfFrame();
                File.WriteAllText(Path.Combine(session.Store.DirectoryPath, "settings-expected.json"), JsonUtility.ToJson(sim.Snapshot()));
            }
            report.volume = AudioListener.volume; report.crt = ArenaDisplay.CrtEnabled; report.fullscreen = Screen.fullScreen;
            report.errors = errors.ToArray(); File.WriteAllText(Path.Combine(output, "settings-check.json"), JsonUtility.ToJson(report, true));
            Click("Menu quit");
        }
        private void SetSlider(float value)
        {
            var obj = GameObject.Find("Menu volume"); if (obj == null) { errors.Add("Volume slider missing"); return; }
            var rect = obj.GetComponent<RectTransform>();
            var point = new Vector2(rect.rect.xMin + 12 + (rect.rect.width - 24) * value, rect.rect.center.y);
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(Camera.main, rect.TransformPoint(point)), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            if (hits.Count == 0 || hits[0].gameObject != obj) { errors.Add("Slider raycast blocked"); return; }
            // A camera-space Slider converts screen coordinates using the press raycast's camera.
            pointer.pointerPressRaycast = hits[0]; pointer.pointerCurrentRaycast = hits[0];
            ExecuteEvents.Execute(obj, pointer, ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(obj, pointer, ExecuteEvents.pointerUpHandler);
        }
        private void Capture(string name)
        {
            Canvas.ForceUpdateCanvases(); var menuRoot = GameObject.Find("Menu screen");
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
                if (menuRoot != null && label.gameObject.activeInHierarchy && label.transform.IsChildOf(menuRoot.transform))
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
        [Serializable] private sealed class Evidence { public int width, height, fullscreenWidth, fullscreenHeight; public string graphics; public bool resume, exactResume, crt, fullscreen; public float volume; public string[] errors; }
    }
}
#endif

