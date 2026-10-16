using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AsciiArena.Editor
{
    public static class ArenaCrtSetup
    {
        public static void Configure()
        {
            var renderer=AssetDatabase.LoadAssetAtPath<Renderer2DData>("Assets/Settings/Renderer2D.asset");
            if(renderer==null)throw new System.InvalidOperationException("Arena Renderer2D asset missing");
            var shader=Shader.Find("AsciiArena/PhosphorCRT");
            if(shader==null)throw new System.InvalidOperationException("CRT shader missing");
            const string path="Assets/Prototype/Art/PhosphorCRT.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            material.shader=shader;
            FullScreenPassRendererFeature feature=null;
            foreach(var item in renderer.rendererFeatures)
                if(item is FullScreenPassRendererFeature f && item.name=="ASCII phosphor CRT")feature=f;
            if(feature==null)
            {
                feature=ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name="ASCII phosphor CRT";
                AssetDatabase.AddObjectToAsset(feature,renderer);
                renderer.rendererFeatures.Add(feature);
            }
            feature.passMaterial=material;
            feature.injectionPoint=FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            feature.fetchColorBuffer=true;
            // URP 17.3 Renderer2D.GetRenderPassInputs checks Color, not requiresIntermediateTexture.
            // Without this, SDR cameras without Volume effects have no cameraColor attachment.
            feature.requirements=ScriptableRenderPassInput.Color;
            feature.bindDepthStencilAttachment=false;
            feature.SetActive(true);feature.Create();
            EditorUtility.SetDirty(material);EditorUtility.SetDirty(feature);EditorUtility.SetDirty(renderer);
            renderer.SetDirty();
            AssetDatabase.SaveAssets();
        }
    }
}
