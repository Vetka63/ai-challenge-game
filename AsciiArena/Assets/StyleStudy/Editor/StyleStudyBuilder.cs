using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;

namespace AsciiArena.StyleStudy.Editor
{
    public static class StyleStudyBuilder
    {
        const string Root="Assets/StyleStudy/";
        public static void Prepare()
        {
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"Fonts/StudyMono.asset");
            if(font==null)
            {
                font=TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(Root+"Fonts/JetBrainsMono-Medium.ttf"),64,8,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,false);
                font.name="Study Mono ASCII";
                if(!font.TryAddCharacters(new string(Enumerable.Range(32,95).Select(i=>(char)i).ToArray()),out string missing))throw new Exception("Missing ASCII: "+missing);
                font.atlasPopulationMode=AtlasPopulationMode.Static;
                AssetDatabase.CreateAsset(font,Root+"Fonts/StudyMono.asset");
                AssetDatabase.AddObjectToAsset(font.material,font);
                foreach(var texture in font.atlasTextures)AssetDatabase.AddObjectToAsset(texture,font);
                EditorUtility.SetDirty(font);
            }
            var renderer=AssetDatabase.LoadAssetAtPath<Renderer2DData>(Root+"StudyRenderer.asset");
            if(renderer==null)
            {
                renderer=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<Renderer2DData>("Assets/Settings/Renderer2D.asset"));
                renderer.name="Isolated study renderer";renderer.rendererFeatures.Clear();
                AssetDatabase.CreateAsset(renderer,Root+"StudyRenderer.asset");
                var material=new Material(Shader.Find("AsciiArena/StudyCRT"));material.name="Study CRT";
                AssetDatabase.CreateAsset(material,Root+"StudyCRT.mat");
                var feature=ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();feature.name="Study CRT";
                feature.passMaterial=material;feature.fetchColorBuffer=true;feature.requirements=ScriptableRenderPassInput.Color;
                feature.injectionPoint=FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
                AssetDatabase.AddObjectToAsset(feature,renderer);renderer.rendererFeatures.Add(feature);feature.Create();
                EditorUtility.SetDirty(renderer);
            }
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root+"StudyPipeline.asset");
            if(pipeline==null)
            {
                pipeline=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/UniversalRP.asset"));
                pipeline.name="Isolated study pipeline";
                AssetDatabase.CreateAsset(pipeline,Root+"StudyPipeline.asset");
                var so=new SerializedObject(pipeline);var list=so.FindProperty("m_RendererDataList");list.arraySize=1;list.GetArrayElementAtIndex(0).objectReferenceValue=renderer;
                so.FindProperty("m_DefaultRendererIndex").intValue=0;so.ApplyModifiedPropertiesWithoutUndo();
            }
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Root+"StudyVolume.asset");
            if(profile==null)
            {
                profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Root+"StudyVolume.asset");
                var bloom=profile.Add<Bloom>(true);bloom.threshold.value=.12f;bloom.intensity.value=.7f;bloom.scatter.value=.7f;bloom.highQualityFiltering.value=true;
                AssetDatabase.AddObjectToAsset(bloom,profile);EditorUtility.SetDirty(profile);
            }
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Study Camera",typeof(Camera)).GetComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=5;camera.clearFlags=CameraClearFlags.SolidColor;camera.transform.position=new Vector3(0,0,-10);camera.allowHDR=false;camera.allowMSAA=false;
            var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.None;data.volumeLayerMask=1;
            var volume=new GameObject("Study bloom",typeof(Volume)).GetComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=profile;
            var view=new GameObject("Isolated visual study",typeof(StyleStudyView)).GetComponent<StyleStudyView>();view.font=font;view.studyPipeline=pipeline;view.volume=volume;
            EditorSceneManager.SaveScene(scene,Root+"StyleStudy.unity");AssetDatabase.SaveAssets();
            Debug.Log("STYLE_STUDY_PREPARED");
        }
        public static void Build()
        {
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Root+"StyleStudy.unity"},locationPathName="Builds/StyleStudy/StyleStudy.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            var output=Path.GetFullPath("../art/style-study-2026-10-15/evidence/build.json");
            File.WriteAllText(output,JsonUtility.ToJson(new Result{result=report.summary.result.ToString(),errors=(int)report.summary.totalErrors,warnings=(int)report.summary.totalWarnings,seconds=report.summary.totalTime.TotalSeconds},true));
            File.WriteAllLines(Path.ChangeExtension(output,"messages.txt"),report.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Warning||m.type==LogType.Error).Select(m=>m.type+": "+m.content));
            Directory.CreateDirectory("Builds/StyleStudy/Licenses");File.Copy(Root+"Fonts/OFL.txt","Builds/StyleStudy/Licenses/JetBrainsMono-OFL.txt",true);
            Debug.Log("STYLE_STUDY_BUILD "+report.summary.result);
        }
        [Serializable]class Result{public string result;public int errors,warnings;public double seconds;}
    }
}
