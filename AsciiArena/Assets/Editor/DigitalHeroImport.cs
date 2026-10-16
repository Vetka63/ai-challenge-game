using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace AsciiArena.Editor
{
    public static class DigitalHeroImport
    {
        public static void Run()
        {
            const string folder="Assets/Resources/DigitalHeroes/";
            foreach(string name in new[]{"spark","pacman","doomguy"})
            {
                string path=folder+name+".png";
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
                importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=512;
                importer.SaveAndReimport();
                var factory=new SpriteDataProviderFactories();factory.Init();
                var provider=factory.GetSpriteEditorDataProviderFromObject(importer);
                if(provider==null)throw new Exception("Sprite provider unavailable: "+path);
                provider.InitSpriteEditorDataProvider();
                var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();
                if(capability==null)throw new Exception("Sprite edit capability unavailable: "+path);
                var caps=capability.GetEditCapability();
                foreach(var flag in new[]{EEditCapability.CreateAndDeleteSprite,EEditCapability.EditSpriteRect,EEditCapability.EditPivot,EEditCapability.EditSpriteName})
                    if(!caps.HasCapability(flag))throw new Exception("Unsupported sprite edit: "+flag);
                var original=new Texture2D(2,2,TextureFormat.RGBA32,false);
                original.LoadImage(File.ReadAllBytes(path));var pixels=original.GetPixels32();
                int left=original.width,bottom=original.height,right=0,top=0,transparent=0;
                for(int y=0;y<original.height;y++)for(int x=0;x<original.width;x++)
                {
                    if(pixels[y*original.width+x].a<8){transparent++;continue;}
                    left=Math.Min(left,x);right=Math.Max(right,x);bottom=Math.Min(bottom,y);top=Math.Max(top,y);
                }
                if(transparent<original.width*original.height/10 || right<=left)throw new Exception("Missing alpha or empty sprite: "+name);
                left=Math.Max(0,left-4);bottom=Math.Max(0,bottom-4);right=Math.Min(original.width-1,right+4);top=Math.Min(original.height-1,top+4);
                var old=provider.GetSpriteRects();var id=old.Length>0?old[0].spriteID:GUID.Generate();
                var rect=new SpriteRect { name=name,spriteID=id,rect=new Rect(left,bottom,right-left+1,top-bottom+1),pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center };
                var names=provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
                if(names==null)throw new Exception("Sprite name mapping unsupported: "+path);
                provider.SetSpriteRects(new[]{rect});names.SetNameFileIdPairs(new[]{new SpriteNameFileIdPair(name,id)});
                provider.Apply();importer.SaveAndReimport();
                UnityEngine.Object.DestroyImmediate(original);
                if(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count()!=1)throw new Exception("Sprite import did not produce one character");
                Debug.Log("DIGITAL_SPRITE_READY: "+name+" "+rect.rect);
            }
            string matPath=folder+"OutsiderSprite.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(material==null)
            {
                var shader=Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if(shader==null)throw new Exception("URP sprite unlit shader missing");
                material=new Material(shader);AssetDatabase.CreateAsset(material,matPath);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
