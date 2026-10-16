using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AsciiArena.Editor
{
    public static class ArenaTerminalSetup
    {
        [MenuItem("ASCII Arena/Apply approved style A")]
        public static void Configure()
        {
            var scene=EditorSceneManager.OpenScene(ArenaSceneBuilder.ScenePath);
            const string dir="Assets/Prototype/Art/Terminal";
            if(!AssetDatabase.IsValidFolder(dir))AssetDatabase.CreateFolder("Assets/Prototype/Art","Terminal");
            if(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(dir+"/TerminalMono.asset")==null)
                if(!AssetDatabase.CopyAsset("Assets/StyleStudy/Fonts/StudyMono.asset",dir+"/TerminalMono.asset"))throw new Exception("Could not copy approved font");
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(dir+"/TerminalMono.asset");
            font.ReadFontAssetDefinition();
            var material=AssetDatabase.LoadAssetAtPath<Material>(dir+"/TerminalGlyph.mat");
            if(material==null){material=new Material(Shader.Find("AsciiArena/TerminalGlyph"));AssetDatabase.CreateAsset(material,dir+"/TerminalGlyph.mat");}
            material.mainTexture=font.atlasTexture;EditorUtility.SetDirty(material);
            var view=UnityEngine.Object.FindFirstObjectByType<ArenaView>();
            view.glyphMaterial=material;view.terminalFont=font;EditorUtility.SetDirty(view);
            var hud=UnityEngine.Object.FindFirstObjectByType<ArenaHud>();hud.font=font;EditorUtility.SetDirty(hud);
            var camera=Camera.main;camera.backgroundColor=new Color(2/255f,4/255f,3/255f);camera.orthographicSize=9;
            if(camera.GetComponent<ArenaScreen>()==null)camera.gameObject.AddComponent<ArenaScreen>();
            PlayerSettings.fullScreenMode=FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution=true;
            PlayerSettings.resizableWindow=true;
            camera.allowHDR=camera.allowMSAA=false;
            var pixel=camera.GetComponent<PixelPerfectCamera>();if(pixel!=null)pixel.enabled=false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            camera.GetUniversalAdditionalCameraData().volumeLayerMask=1;
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(dir+"/TerminalBloom.asset");
            if(profile==null)
            {
                profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,dir+"/TerminalBloom.asset");
                var component=profile.Add<Bloom>(true);AssetDatabase.AddObjectToAsset(component,profile);
            }
            profile.TryGet<Bloom>(out var bloom);bloom.threshold.Override(.12f);bloom.intensity.Override(.70f);bloom.scatter.Override(.70f);bloom.highQualityFiltering.Override(true);EditorUtility.SetDirty(bloom);EditorUtility.SetDirty(profile);
            var volume=UnityEngine.Object.FindFirstObjectByType<Volume>();volume.sharedProfile=profile;volume.weight=1;volume.enabled=true;volume.isGlobal=true;
            var toggle=volume.GetComponent<ArenaCrtVolume>()??volume.gameObject.AddComponent<ArenaCrtVolume>();toggle.volume=volume;
            ArenaCrtSetup.Configure();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("STYLE_A_CONFIGURED: Arena presentation only");
        }
        public static void Build()
        {
            const string output="Builds/HeroAnimations";Directory.CreateDirectory(output);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ArenaSceneBuilder.ScenePath},locationPathName=output+"/AsciiArena.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            ArenaSceneBuilder.CopyNotices(output);File.Copy("Assets/StyleStudy/Fonts/OFL.txt",output+"/Licenses/JetBrainsMono-OFL.txt",true);
            Directory.CreateDirectory("../art/playtests/hero-animations");
            File.WriteAllText("../art/playtests/hero-animations/build.json",JsonUtility.ToJson(new BuildRecord{result=report.summary.result.ToString(),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,seconds=report.summary.totalTime.TotalSeconds},true));
            File.WriteAllLines("../art/playtests/hero-animations/build-messages.txt",report.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Error||m.type==LogType.Warning).Select(m=>m.type+": "+m.content));
        }
        [Serializable]private sealed class BuildRecord{public string result;public int errors,warnings;public double seconds;}
    }
}

