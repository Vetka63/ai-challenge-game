// Execute as a standalone Unity Pipeline eval_file statement block, outside Assets.
var repoRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../.."));
var packRoot = System.IO.Path.Combine(repoRoot, "art/sprites/roster-01-07-v1");
var manifest = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(packRoot, "runtime/unity-manifest.json")));
var root = "Assets/Art/Roster0107";
System.IO.Directory.CreateDirectory(root);
UnityEditor.AssetDatabase.Refresh();
var report = new System.Collections.Generic.List<string>();
foreach (var character in manifest["characters"])
{
    var id = (string)character["id"];
    var folder = root + "/" + id;
    System.IO.Directory.CreateDirectory(folder + "/Clips");
    var atlasPath = folder + "/atlas.png";
    System.IO.File.Copy(System.IO.Path.Combine(packRoot, "runtime/" + id + "/atlas.png"), atlasPath, true);
    UnityEditor.AssetDatabase.ImportAsset(atlasPath, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
    var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(atlasPath);
    importer.textureType = UnityEditor.TextureImporterType.Sprite;
    importer.spriteImportMode = UnityEditor.SpriteImportMode.Multiple;
    importer.spritePixelsPerUnit = 64;
    importer.filterMode = UnityEngine.FilterMode.Point;
    importer.mipmapEnabled = false;
    importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
    importer.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
    importer.alphaIsTransparency = true;
    importer.maxTextureSize = 1024;
    importer.npotScale = UnityEditor.TextureImporterNPOTScale.None;
    importer.SaveAndReimport();
    var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
    factory.Init();
    var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
    if (provider == null) throw new System.Exception("No sprite data provider: " + atlasPath);
    provider.InitSpriteEditorDataProvider();
    var capabilityProvider = provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteFrameEditCapability>();
    if (capabilityProvider == null) throw new System.Exception("No edit capability: " + atlasPath);
    var capability = capabilityProvider.GetEditCapability();
    foreach (var required in new[] { UnityEditor.U2D.Sprites.EEditCapability.CreateAndDeleteSprite, UnityEditor.U2D.Sprites.EEditCapability.EditPivot, UnityEditor.U2D.Sprites.EEditCapability.EditSpriteRect, UnityEditor.U2D.Sprites.EEditCapability.EditSpriteName })
        if (!capability.HasCapability(required)) throw new System.Exception("Unsupported sprite capability " + required);
    var rects = new System.Collections.Generic.List<UnityEditor.SpriteRect>();
    var pairs = new System.Collections.Generic.List<UnityEditor.SpriteNameFileIdPair>();
    var names = character["frames"];
    for (var i = 0; i < names.Count(); i++)
    {
        var name = (string)names[i];
        var hash = System.Security.Cryptography.MD5.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(id + "/" + name));
        var guid = new UnityEditor.GUID(System.BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant());
        rects.Add(new UnityEditor.SpriteRect { name = name, spriteID = guid, rect = new UnityEngine.Rect((i % 8) * 128, 1024 - (i / 8 + 1) * 128, 128, 128), alignment = UnityEngine.SpriteAlignment.Custom, pivot = new UnityEngine.Vector2(0.5f, 20f / 128f) });
        pairs.Add(new UnityEditor.SpriteNameFileIdPair(name, guid));
    }
    provider.SetSpriteRects(rects.ToArray());
    var nameProvider = provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>();
    if (nameProvider == null) throw new System.Exception("No name/file-ID provider");
    nameProvider.SetNameFileIdPairs(pairs);
    provider.Apply(); importer.SaveAndReimport();
    var sprites = new System.Collections.Generic.Dictionary<string, UnityEngine.Sprite>();
    foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(atlasPath))
        if (asset is UnityEngine.Sprite sprite) sprites.Add(sprite.name, sprite);
    if (sprites.Count != 64) throw new System.Exception("Expected 64 sprites: " + id);
    var controllerPath = folder + "/" + id + ".controller";
    var controller = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath);
    if (controller == null) controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
    var machine = controller.layers[0].stateMachine;
    foreach (var clipData in character["clips"])
    {
        var clipName = (string)clipData["name"];
        var clipPath = folder + "/Clips/" + clipName + ".anim";
        var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>(clipPath);
        if (clip == null) { clip = new UnityEngine.AnimationClip(); UnityEditor.AssetDatabase.CreateAsset(clip, clipPath); }
        clip.name = clipName; clip.frameRate = 60;
        var keys = new System.Collections.Generic.List<UnityEditor.ObjectReferenceKeyframe>();
        var elapsed = 0f;
        for (var i = 0; i < clipData["indices"].Count(); i++)
        {
            var spriteName = (string)names[(int)clipData["indices"][i]];
            keys.Add(new UnityEditor.ObjectReferenceKeyframe { time = elapsed, value = sprites[spriteName] });
            elapsed += (int)clipData["durationsMs"][i] / 1000f;
        }
        // Unity extends a discrete object curve by one sampling tick after its last key.
        keys.Add(new UnityEditor.ObjectReferenceKeyframe { time = elapsed - 1f / clip.frameRate, value = keys[keys.Count - 1].value });
        var binding = new UnityEditor.EditorCurveBinding { path = "", type = typeof(UnityEngine.SpriteRenderer), propertyName = "m_Sprite" };
        UnityEditor.AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
        var settings = UnityEditor.AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = (bool)clipData["loop"]; settings.startTime = 0; settings.stopTime = elapsed;
        UnityEditor.AnimationUtility.SetAnimationClipSettings(clip, settings);
        // Gameplay events remain in JSON; no required MonoBehaviour callback on the prefab.
        UnityEditor.AnimationUtility.SetAnimationEvents(clip, new UnityEngine.AnimationEvent[0]);
        UnityEditor.Animations.AnimatorState state = null;
        foreach (var existing in machine.states) if (existing.state.name == clipName) state = existing.state;
        if (state == null) state = machine.AddState(clipName);
        state.motion = clip; state.writeDefaultValues = false;
        if (clipName == "idle_south") machine.defaultState = state;
        UnityEditor.EditorUtility.SetDirty(clip);
    }
    var shader = UnityEngine.Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
    if (shader == null) throw new System.Exception("URP 2D unlit sprite shader is unavailable");
    var materialPath = folder + "/SpriteUnlit.mat";
    var material = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(materialPath);
    if (material == null) { material = new UnityEngine.Material(shader); UnityEditor.AssetDatabase.CreateAsset(material, materialPath); }
    var go = new UnityEngine.GameObject(id);
    var renderer = go.AddComponent<UnityEngine.SpriteRenderer>(); renderer.sprite = sprites["idle_south_00"]; renderer.sharedMaterial = material;
    var animator = go.AddComponent<UnityEngine.Animator>(); animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, folder + "/" + id + ".prefab");
    UnityEngine.Object.DestroyImmediate(go);
    report.Add(id + ": " + sprites.Count + " sprites, " + machine.states.Length + " states");
    UnityEditor.EditorUtility.SetDirty(controller);
}
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.AssetDatabase.Refresh();
System.IO.File.WriteAllText(System.IO.Path.Combine(packRoot, "unity-import-report.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new { unity = UnityEngine.Application.unityVersion, graphicsDevice = UnityEngine.SystemInfo.graphicsDeviceType.ToString(), results = report }, Newtonsoft.Json.Formatting.Indented));
UnityEditor.AssetDatabase.ExportPackage(root, System.IO.Path.Combine(packRoot, "Roster0107.unitypackage"), UnityEditor.ExportPackageOptions.Recurse);
return report;
