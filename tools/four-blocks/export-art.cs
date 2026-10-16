using System;
using System.Collections.Generic;
using System.IO;
using AsciiArena;
using UnityEngine;
using UnityEditor;
public static class ExportAsciiActors
{
    public static object Run()
    {
        const string folder="Assets/Art/AsciiActors";Directory.CreateDirectory(folder);
        var atlas=GlyphAtlas.Create(true);var pixels=atlas.GetPixels32();var files=new List<string>();
        var names=new[]{"south","north","west","east"};
        for(int d=0;d<4;d++)
        {
            Write("hero-idle-"+names[d],AsciiActorArt.Idle[d],true,Color.white);
            for(int f=0;f<4;f++)Write("hero-walk-"+names[d]+"-"+f,AsciiActorArt.Walk[d][f],true,Color.white);
        }
        Write("chaser",AsciiActorArt.Chaser,false,new Color(.87f,.61f,.44f));
        Write("shooter",AsciiActorArt.Shooter,false,new Color(1,.68f,.28f));
        Write("heavy",AsciiActorArt.Heavy,false,new Color(.9f,.4f,.45f));
        Write("boss",AsciiActorArt.Boss,false,new Color(.92f,.4f,.82f));
        UnityEngine.Object.DestroyImmediate(atlas);
        Directory.CreateDirectory("../art/sprites/ascii-actors-v3");
        foreach(var file in files)File.Copy(file,"../art/sprites/ascii-actors-v3/"+Path.GetFileName(file),true);
        File.WriteAllText("../art/sprites/ascii-actors-v3/manifest.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{source="AsciiActorArt.cs + GlyphAtlas.cs",artSource="original ASCII glyph composition",files,ppu=40,cell=new[]{6,8},canvas=new[]{128,128},heroRoot=new[]{64,20},walk="four poses per facing; runtime distance-based cadence"},Newtonsoft.Json.Formatting.Indented));
        AssetDatabase.SaveAssets();return new{count=files.Count,files};
        void Write(string name,string[] art,bool hero,Color baseColor)
        {
            var texture=new Texture2D(128,128,TextureFormat.RGBA32,false);var output=new Color32[128*128];
            for(int row=0;row<art.Length;row++)for(int col=0;col<art[row].Length;col++)
            {
                char c=art[row][col];if(c==' ')continue;
                if(c<32||c>126)throw new Exception("Non ASCII");
                Color color=baseColor;
                if(hero)
                {
                    color=new Color(.26f,.88f,.87f);
                    if(c=='.'||c=='('||c==')'||row==8||c=='|'&&(col==9||col==1))color=new Color(.96f,.9f,.71f);
                    else if(c==':'||c=='=')color=new Color(.16f,.62f,.65f);
                }
                int code=c-32,sx=code%16*6,sy=code/16*8;
                int x0=hero?34+col*6:64-(art[row].Length*6)/2+col*6;
                int y0=20+(art.Length-1-row)*8;
                for(int y=0;y<8;y++)for(int x=0;x<6;x++)if(pixels[(sy+y)*96+sx+x].a>0)
                    output[(y0+y)*128+x0+x]=(Color32)color;
            }
            texture.SetPixels32(output);texture.Apply();string path=folder+"/"+name+".png";
            File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=40;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;
            var ts=new TextureImporterSettings();importer.ReadTextureSettings(ts);
            ts.spriteAlignment=(int)SpriteAlignment.Custom;ts.spritePivot=new Vector2(.5f,20/128f);importer.SetTextureSettings(ts);
            importer.SaveAndReimport();files.Add(path);
        }
    }
}
