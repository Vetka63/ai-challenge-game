using System;
using System.IO;
using AsciiArena;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zenject;

namespace AsciiArena.Editor
{
    public static class ArenaSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Arena.unity";
        [MenuItem("ASCII Arena/Create or refresh arena scene")]
        public static void Create()
        {
            Directory.CreateDirectory("Assets/Prototype/Art");
            Directory.CreateDirectory("Assets/Prototype/Data");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font == null) throw new InvalidOperationException("Import TMP Essentials before creating the scene.");
            var atlas = GlyphAtlas.Create();
            File.WriteAllBytes("Assets/Prototype/Art/GlyphAtlas.png", atlas.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(atlas);
            AssetDatabase.ImportAsset("Assets/Prototype/Art/GlyphAtlas.png", ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Prototype/Art/GlyphAtlas.png");
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            var shader = Shader.Find("ASCII Arena/Glyph");
            if (shader == null) throw new InvalidOperationException("Glyph shader did not compile.");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prototype/Art/Glyph.mat");
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, "Assets/Prototype/Art/Glyph.mat"); }
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Prototype/Art/GlyphAtlas.png");
            var settings = AssetDatabase.LoadAssetAtPath<ArenaSettings>("Assets/Prototype/Data/ArenaSettings.asset");
            if (settings == null) { settings = ScriptableObject.CreateInstance<ArenaSettings>(); AssetDatabase.CreateAsset(settings, "Assets/Prototype/Data/ArenaSettings.asset"); }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, .7f, -20);
            camera.orthographic = true; camera.orthographicSize = 9;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f,.04f,.06f);
            camera.allowHDR = camera.allowMSAA = camera.allowDynamicResolution = false;
            var pixelCamera=cameraObject.AddComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
            pixelCamera.assetsPPU=40;pixelCamera.refResolutionX=1280;pixelCamera.refResolutionY=720;
            pixelCamera.gridSnapping=UnityEngine.Rendering.Universal.PixelPerfectCamera.GridSnapping.PixelSnapping;
            pixelCamera.cropFrame=UnityEngine.Rendering.Universal.PixelPerfectCamera.CropFrame.None;
            cameraObject.AddComponent<AudioListener>();
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.volumeLayerMask = 1;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) throw new InvalidOperationException("URP is required.");
            urp.supportsHDR = true;
            urp.renderScale = 1;
            EditorUtility.SetDirty(urp);
            var lightObject = new GameObject("Global 2D Light", typeof(Light2D));
            lightObject.GetComponent<Light2D>().lightType = Light2D.LightType.Global;
            lightObject.GetComponent<Light2D>().intensity = 1;
            var volumeObject = new GameObject("Atmosphere", typeof(Volume));
            var volume = volumeObject.GetComponent<Volume>(); volume.isGlobal = true;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Prototype/Art/Atmosphere.asset");
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, "Assets/Prototype/Art/Atmosphere.asset"); }
            if (!profile.TryGet<Tonemapping>(out var tone)) { tone = profile.Add<Tonemapping>(true); AssetDatabase.AddObjectToAsset(tone, profile); }
            tone.mode.Override(TonemappingMode.ACES);
            if (!profile.TryGet<Bloom>(out var bloom)) { bloom = profile.Add<Bloom>(true); AssetDatabase.AddObjectToAsset(bloom, profile); }
            bloom.intensity.Override(.28f); bloom.threshold.Override(1.05f); bloom.scatter.Override(.55f);
            if (!profile.TryGet<Vignette>(out var vignette)) { vignette = profile.Add<Vignette>(true); AssetDatabase.AddObjectToAsset(vignette, profile); }
            vignette.intensity.Override(.16f); vignette.smoothness.Override(.65f);
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(profile);

            var contextObject = new GameObject("SceneContext");
            var installer = contextObject.AddComponent<ArenaInstaller>(); installer.settings = settings;
            var context = contextObject.AddComponent<SceneContext>(); context.Installers = new[] { installer };
            var view = new GameObject("ASCII presentation").AddComponent<ArenaView>(); view.glyphMaterial = material;
            new GameObject("Keyboard input").AddComponent<ArenaInput>();
            new GameObject("ASCII sound cues").AddComponent<ArenaAudio>();
            var hud = new GameObject("HUD controller").AddComponent<ArenaHud>(); hud.font = font;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.productName = "The Waking Grove - ASCII prototype";
            PlayerSettings.companyName = "ASCII Arena";
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            ArenaCrtSetup.Configure();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            // Recreating Arena must preserve the approved presentation instead of restoring
            // the legacy bitmap material on the new font-metric renderer.
            ArenaTerminalSetup.Configure();
            Selection.activeObject = UnityEngine.Object.FindFirstObjectByType<ArenaView>();
            Debug.Log("[AsciiArena] Arena scene created with ECS, Zenject and ASCII presentation.");
        }

        [MenuItem("ASCII Arena/Build Windows prototype")]
        public static void BuildWindows()
        {
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = "Builds/Windows/AsciiArena.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Windows build failed: " + report.summary.result);
            CopyNotices("Builds/Windows");
            Debug.Log("[AsciiArena] Windows build succeeded.");
        }

        public static void CopyNotices(string buildDirectory)
        {
            string licenses = Path.Combine(buildDirectory, "Licenses");
            Directory.CreateDirectory(licenses);
            File.Copy("Assets/StyleStudy/Fonts/OFL.txt", Path.Combine(licenses,"JetBrainsMono-OFL.txt"),true);
            File.Copy("Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt", Path.Combine(licenses, "LiberationSans-OFL.txt"), true);
            File.Copy("Assets/TextMesh Pro/LICENSE-Unity-Companion.md", Path.Combine(licenses, "Unity-Companion.md"), true);
            var zenject = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.svermeulen.extenject/LICENSE.txt");
            if (zenject == null) throw new InvalidOperationException("Zenject package license was not found.");
            File.Copy(Path.Combine(zenject.resolvedPath, "LICENSE.txt"), Path.Combine(licenses, "Zenject-MIT.txt"), true);
            File.Copy("THIRD-PARTY-NOTICES.txt", Path.Combine(buildDirectory, "THIRD-PARTY-NOTICES.txt"), true);
        }
    }
}
