var repo = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../.."));
var pack = System.IO.Path.Combine(repo, "art/sprites/roster-01-07-v1");
var manifest = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(pack,"runtime/unity-manifest.json")));
foreach(var character in manifest["characters"])
foreach(var data in character["clips"])
{
    var clip=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>("Assets/Art/Roster0107/"+(string)character["id"]+"/Clips/"+(string)data["name"]+".anim");
    var duration=0f; foreach(var value in data["durationsMs"]) duration+=(int)value/1000f;
    var binding=UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(clip)[0];
    var keys=UnityEditor.AnimationUtility.GetObjectReferenceCurve(clip,binding);
    keys[keys.Length-1].time=duration-1f/clip.frameRate;
    UnityEditor.AnimationUtility.SetObjectReferenceCurve(clip,binding,keys);
    var settings=UnityEditor.AnimationUtility.GetAnimationClipSettings(clip); settings.stopTime=duration;
    UnityEditor.AnimationUtility.SetAnimationClipSettings(clip,settings); UnityEditor.EditorUtility.SetDirty(clip);
}
UnityEditor.AssetDatabase.SaveAssets();
return "24 discrete clip endpoints corrected";
