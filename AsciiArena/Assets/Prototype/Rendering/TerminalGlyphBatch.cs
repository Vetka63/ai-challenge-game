using System;
using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace AsciiArena
{
    // Fixed-cell text from the approved font's SDF atlas, with one mesh per display layer.
    // Glyph metrics preserve the baseline and spaces rather than stretching ink to each cell.
    public sealed class TerminalGlyphBatch : IDisposable
    {
        private readonly Mesh mesh;
        private readonly GameObject node;
        private readonly TMP_FontAsset font;
        private readonly List<Vector3> vertices=new(16000);
        private readonly List<Vector2> uvs=new(16000);
        private readonly List<Color> colors=new(16000);
        private readonly List<int> indices=new(24000);
        public TerminalGlyphBatch(Transform parent,Material material,TMP_FontAsset font,string name,int order)
        {
            this.font=font;
            node=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));node.transform.SetParent(parent,false);
            mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.MarkDynamic();
            node.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=node.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=order;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        public void Clear(){vertices.Clear();uvs.Clear();colors.Clear();indices.Clear();}
        public void Put(char c,float2 center,Color color,float width=.23f,float height=.367f,float angle=0)
        {
            if(c==' '||!font.characterLookupTable.TryGetValue(c,out var character))return;
            var glyph=character.glyph;var m=glyph.metrics;var rect=glyph.glyphRect;
            float padding=font.atlasPadding;
            float sx=width/font.characterLookupTable['M'].glyph.metrics.horizontalAdvance;
            float sy=height/font.faceInfo.pointSize;
            float left=-width*.5f+(m.horizontalBearingX-padding)*sx;
            float top=(m.horizontalBearingY+padding-(font.faceInfo.ascentLine+font.faceInfo.descentLine)*.5f)*sy;
            float right=left+(m.width+padding*2)*sx,bottom=top-(m.height+padding*2)*sy;
            int start=vertices.Count;float cosine=math.cos(angle),sine=math.sin(angle);
            Add(left,bottom);Add(right,bottom);Add(right,top);Add(left,top);
            void Add(float x,float y){vertices.Add(new Vector3(center.x+x*cosine-y*sine,center.y+x*sine+y*cosine,0));}
            float u=(rect.x-padding)/font.atlasWidth,v=(rect.y-padding)/font.atlasHeight;
            float u2=(rect.x+rect.width+padding)/font.atlasWidth,v2=(rect.y+rect.height+padding)/font.atlasHeight;
            uvs.Add(new Vector2(u,v));uvs.Add(new Vector2(u2,v));uvs.Add(new Vector2(u2,v2));uvs.Add(new Vector2(u,v2));
            for(int n=0;n<4;n++)colors.Add(color);
            indices.Add(start);indices.Add(start+1);indices.Add(start+2);indices.Add(start);indices.Add(start+2);indices.Add(start+3);
        }
        public void Sprite(string[] rows,float2 bottom,Color color,float width=.23f,float height=.367f,float centerColumn=-1)
        {
            if(centerColumn<0){int columns=0;foreach(var row in rows)columns=Math.Max(columns,row.Length);centerColumn=(columns-1)*.5f;}
            for(int r=0;r<rows.Length;r++)for(int c=0;c<rows[r].Length;c++)Put(rows[r][c],bottom+new float2((c-centerColumn)*width,(rows.Length-r-1)*height),color,width,height);
        }
        // A stepped silhouette, not a rectangular label. Each row masks the interior of
        // the figure, so previously drawn letters cannot leak through its empty spaces.
        // Insert the entire silhouette BEFORE inserting any glyphs of this figure.
        public void Silhouette(string[] rows,float2 bottom,float width,float height,float centerColumn,float padding=.065f)
        {
            var ink=new Color(2/255f,4/255f,3/255f,1);
            for(int r=0;r<rows.Length;r++)
            {
                int first=0,last=rows[r].Length-1;
                while(first<=last&&rows[r][first]==' ')first++;
                while(last>=first&&rows[r][last]==' ')last--;
                if(first>last)continue;
                float y=bottom.y+(rows.Length-r-1)*height;
                SolidQuad(new float2(bottom.x+(first-centerColumn-.5f)*width-padding,y-height*.5f-padding),
                    new float2(bottom.x+(last-centerColumn+.5f)*width+padding,y+height*.5f+padding),ink);
            }
        }
        private void SolidQuad(float2 min,float2 max,Color color)
        {
            int start=vertices.Count;
            vertices.Add(new Vector3(min.x,min.y,0));vertices.Add(new Vector3(max.x,min.y,0));
            vertices.Add(new Vector3(max.x,max.y,0));vertices.Add(new Vector3(min.x,max.y,0));
            // Negative UV marks an untextured mask in the same material/draw call.
            for(int i=0;i<4;i++){uvs.Add(new Vector2(-1,-1));colors.Add(color);}
            indices.Add(start);indices.Add(start+1);indices.Add(start+2);indices.Add(start);indices.Add(start+2);indices.Add(start+3);
        }
        public void Upload(){mesh.Clear();mesh.SetVertices(vertices);mesh.SetUVs(0,uvs);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();}
        public void Dispose(){UnityEngine.Object.Destroy(mesh);UnityEngine.Object.Destroy(node);}
    }
}
