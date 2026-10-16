using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace AsciiArena
{
    public sealed class GlyphBatch : IDisposable
    {
        private readonly Mesh mesh;
        private readonly GameObject gameObject;
        private readonly List<Vector3> vertices = new(16000);
        private readonly List<Vector2> uvs = new(16000);
        private readonly List<Color> colors = new(16000);
        private readonly List<int> triangles = new(24000);

        public GlyphBatch(Transform parent, Material material, string name, int order)
        {
            gameObject = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            gameObject.transform.SetParent(parent, false);
            mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = gameObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        public void Clear() { vertices.Clear(); uvs.Clear(); colors.Clear(); triangles.Clear(); }
        public void Put(char glyph, float2 center, Color color, float width = .19f, float height = .25f, float angle = 0)
        {
            if (glyph == ' ') return;
            int index = math.clamp(glyph - 32, 0, 95);
            int start = vertices.Count;
            float2 x = new(math.cos(angle) * width * .5f, math.sin(angle) * width * .5f);
            float2 y = new(-math.sin(angle) * height * .5f, math.cos(angle) * height * .5f);
            AddVertex(center - x - y); AddVertex(center + x - y); AddVertex(center + x + y); AddVertex(center - x + y);
            float u = index % 16 / 16f, v = index / 16 / 6f;
            uvs.Add(new Vector2(u, v)); uvs.Add(new Vector2(u + 1f / 16, v));
            uvs.Add(new Vector2(u + 1f / 16, v + 1f / 6)); uvs.Add(new Vector2(u, v + 1f / 6));
            for (int i = 0; i < 4; i++) colors.Add(color);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }
        private void AddVertex(float2 p) => vertices.Add(new Vector3(p.x, p.y, 0));
        public void Sprite(string[] rows, float2 feet, Color color, float width = .19f, float height = .25f)
        {
            for (int row = 0; row < rows.Length; row++)
                for (int column = 0; column < rows[row].Length; column++)
                    Put(rows[row][column], feet + new float2((column - (rows[row].Length - 1) * .5f) * width, (rows.Length - 1 - row) * height), color, width, height);
        }
        public void Upload()
        {
            mesh.Clear();
            mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }
        public void Dispose()
        {
            UnityEngine.Object.Destroy(mesh);
            UnityEngine.Object.Destroy(gameObject);
        }
    }
}
