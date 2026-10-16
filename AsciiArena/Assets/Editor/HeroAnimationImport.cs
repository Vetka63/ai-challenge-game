using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace AsciiArena.Editor
{
    // Registration is prepared from reviewed source art, then applied through Sprite Editor.
    public static class HeroAnimationImport
    {
        [Serializable] public sealed class Frame { public string path; public float rootX, rootY, pixelsPerUnit; }
        [Serializable] public sealed class Manifest { public Frame[] frames; }
        public static void Run()
        {
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText("../art/sprites/hero-animations/registration.json"));
            foreach(var frame in manifest.frames)
            {
                AssetDatabase.ImportAsset(frame.path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(frame.path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
                importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=512;
                importer.spritePixelsPerUnit=frame.pixelsPerUnit;
                var textureSettings=new TextureImporterSettings();importer.ReadTextureSettings(textureSettings);
                textureSettings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(textureSettings);
                importer.SaveAndReimport();
                var factory=new SpriteDataProviderFactories();factory.Init();
                var provider=factory.GetSpriteEditorDataProviderFromObject(importer);
                if(provider==null)throw new Exception("No sprite provider: "+frame.path);
                provider.InitSpriteEditorDataProvider();
                var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();
                if(capability==null)throw new Exception("No edit capability: "+frame.path);
                foreach(var flag in new[]{EEditCapability.CreateAndDeleteSprite,EEditCapability.EditSpriteRect,EEditCapability.EditPivot,EEditCapability.EditSpriteName})
                    if(!capability.GetEditCapability().HasCapability(flag))throw new Exception("Unsupported sprite edit: "+flag);
                var source=new Texture2D(2,2,TextureFormat.RGBA32,false);source.LoadImage(File.ReadAllBytes(frame.path));
                var old=provider.GetSpriteRects();var id=old.Length>0?old[0].spriteID:GUID.Generate();
                string name="anim-"+Path.GetFileNameWithoutExtension(frame.path);
                var rect=new SpriteRect{name=name,spriteID=id,rect=new Rect(0,0,source.width,source.height),
                    pivot=new Vector2(frame.rootX/source.width,1-frame.rootY/source.height),alignment=SpriteAlignment.Custom};
                var names=provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
                if(names==null)throw new Exception("No name mapping: "+frame.path);
                provider.SetSpriteRects(new[]{rect});names.SetNameFileIdPairs(new[]{new SpriteNameFileIdPair(name,id)});
                provider.Apply();importer.SaveAndReimport();UnityEngine.Object.DestroyImmediate(source);
                if(AssetDatabase.LoadAllAssetsAtPath(frame.path).OfType<Sprite>().Count()!=1)throw new Exception("Frame not imported: "+frame.path);
            }
            AssetDatabase.SaveAssets();Debug.Log("HERO_ANIMATIONS_IMPORTED: "+manifest.frames.Length);
        }
    }
}
