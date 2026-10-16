var repo = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../.."));
var pack = System.IO.Path.Combine(repo, "art/sprites/roster-01-07-v1");
var manifest = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(pack,"runtime/unity-manifest.json")));
var verified = new System.Collections.Generic.List<object>();
foreach (var character in manifest["characters"])
{
    var id=(string)character["id"]; var root="Assets/Art/Roster0107/"+id;
    var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(root+"/"+id+".prefab");
    if(prefab==null) throw new System.Exception("Missing prefab "+id);
    var go=(UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
    try
    {
        var renderer=go.GetComponent<UnityEngine.SpriteRenderer>();
        if(renderer.sharedMaterial==null || renderer.sharedMaterial.shader==null) throw new System.Exception("Missing shader");
        var importer=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(root+"/atlas.png");
        if(importer.filterMode!=UnityEngine.FilterMode.Point || importer.mipmapEnabled || importer.textureCompression!=UnityEditor.TextureImporterCompression.Uncompressed) throw new System.Exception("Wrong texture settings");
        var sampled=0;
        foreach(var data in character["clips"])
        {
            var name=(string)data["name"];
            var clip=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>(root+"/Clips/"+name+".anim");
            if(clip==null) throw new System.Exception("Missing clip "+name);
            var elapsed=0f;
            for(var i=0;i<data["indices"].Count();i++)
            {
                var duration=(int)data["durationsMs"][i]/1000f;
                clip.SampleAnimation(go,elapsed+duration*0.5f);
                var expected=(string)character["frames"][(int)data["indices"][i]];
                if(renderer.sprite==null || renderer.sprite.name!=expected) throw new System.Exception("Wrong sample "+id+"/"+name+"/"+i);
                if(renderer.sprite.rect.size!=new UnityEngine.Vector2(128,128) || UnityEngine.Vector2.Distance(renderer.sprite.pivot,new UnityEngine.Vector2(64,20))>0.01f) throw new System.Exception("Wrong rect/pivot");
                if(go.transform.localPosition!=UnityEngine.Vector3.zero || go.transform.localScale!=UnityEngine.Vector3.one) throw new System.Exception("Unexpected root motion");
                sampled++; elapsed+=duration;
            }
            if(UnityEngine.Mathf.Abs(clip.length-elapsed)>0.001f) throw new System.Exception("Wrong duration "+name+" "+clip.length+" "+elapsed);
            if(UnityEditor.AnimationUtility.GetAnimationClipSettings(clip).loopTime!=(bool)data["loop"]) throw new System.Exception("Wrong loop "+name);
        }
        verified.Add(new {id,clips=character["clips"].Count(),sampledFrames=sampled,texture="Point, no mipmaps, uncompressed",pivot=new[]{64,20}});
    }
    finally {UnityEngine.Object.DestroyImmediate(go);}
}
var result=new {status="pass",unity=UnityEngine.Application.unityVersion,graphicsDevice=UnityEngine.SystemInfo.graphicsDeviceType.ToString(),verified,scope="Imported assets and clip sampling in headless Editor; Arena play mode and GPU rendering not tested."};
System.IO.File.WriteAllText(System.IO.Path.Combine(pack,"unity-validation.json"),Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
return result;
