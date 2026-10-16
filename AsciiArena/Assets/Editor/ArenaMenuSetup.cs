using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace AsciiArena.Editor
{
    public static class ArenaMenuSetup
    {
        public static void Configure()
        {
            const string path = "Assets/Prototype/Art/Terminal/MenuMono.asset";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/StyleStudy/Fonts/JetBrainsMono-Medium.ttf");
                font = TMP_FontAsset.CreateFontAsset(source, 48, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic);
                font.name = "MenuMono";
                AssetDatabase.CreateAsset(font, path);
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
                string characters = new string(Enumerable.Range(32, 95).Concat(Enumerable.Range(0x400, 256)).Select(c => (char)c).ToArray());
                font.TryAddCharacters(characters, out string missing);
                font.atlasPopulationMode = AtlasPopulationMode.Static;
                EditorUtility.SetDirty(font);
                foreach (var atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
                EditorUtility.SetDirty(font.material);
            }
            var scene = EditorSceneManager.OpenScene(ArenaSceneBuilder.ScenePath);
            var hud = Object.FindFirstObjectByType<ArenaHud>(); hud.menuFont = font; EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            if (!font.HasCharacters("ИГРАТЬ ПРОДОЛЖИТЬ ТРЕНИРОВКА НАСТРОЙКИ ВЫХОД Ёё")) throw new System.Exception("Menu Cyrillic glyphs missing");
            Debug.Log("MAIN_MENU_CONFIGURED");
        }
    }
}
