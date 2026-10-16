UnityEditor.EditorApplication.delayCall += () => {
    try {
        System.IO.Directory.CreateDirectory("Builds/CrtHover");
        var report=UnityEditor.BuildPipeline.BuildPlayer(new UnityEditor.BuildPlayerOptions {
            scenes=new[]{"Assets/Scenes/Arena.unity"},locationPathName="Builds/CrtHover/AsciiArena.exe",
            target=UnityEditor.BuildTarget.StandaloneWindows64,options=UnityEditor.BuildOptions.Development
        });
        AsciiArena.Editor.ArenaSceneBuilder.CopyNotices("Builds/CrtHover");
        System.IO.File.WriteAllText("../art/playtests/crt-hover/build.json",UnityEngine.JsonUtility.ToJson(new AsciiArena.Editor.FourBlocksBuildRecord {result=report.summary.result.ToString(),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,bytes=report.summary.totalSize},true));
    } catch(System.Exception e) { System.IO.File.WriteAllText("../art/playtests/crt-hover/build-error.txt",e.ToString());UnityEngine.Debug.LogException(e); }
};
return "Build queued; inspect build.json and Editor log for completion.";
