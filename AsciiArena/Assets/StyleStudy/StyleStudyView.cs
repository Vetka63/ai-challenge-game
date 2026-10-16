using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AsciiArena.StyleStudy
{
    // Isolated visual study. No ArenaSimulation, ECS world, damage, input or balance changes.
    public sealed class StyleStudyView : MonoBehaviour
    {
        public TMP_FontAsset font;
        public UniversalRenderPipelineAsset studyPipeline;
        public Volume volume;
        private RenderPipelineAsset previousPipeline;
        private RectTransform root;
        private readonly List<Figure> figures=new();
        private readonly List<string> errors=new();
        private int variant;
        private float time;
        private bool paused, roster, crt=true, captureMode;
        private TextMeshProUGUI attack;
        private readonly Vector2 hero=new(960,570);
        private static readonly Vector2[] locations={
            new(330,255),new(620,200),new(1170,205),new(1535,245),
            new(430,430),new(655,355),new(1245,370),new(1490,395),
            new(240,610),new(550,620),new(1320,540),new(1590,590),
            new(350,820),new(730,795),new(1170,780),new(1530,815),
            new(1390,680),new(1460,700),new(1510,650),new(1590,715),
            new(1250,850),new(1325,885),new(1440,865),new(1550,925)
        };
        private static readonly string[][] A={
            new[]{" /^\\  *"," (o)  |"," /|\\ / "," ~V~  '"},
            new[]{" /^\\ ","( x )"," ~V~ "},
            new[]{"  ^  *"," /o\\/ "," /|\\  "," ~V~  "},
            new[]{" [o:o] ","/[###]\\"," ~v~v~ "}
        };
        private static readonly string[][] B={
            new[]{"   /^\\   *","  /...\\  |","  | o |  |"," /|:#:|\\ |","/##:#:##\\|","  ~v~v~  '"},
            new[]{"  /^\\  "," /x.x\\ "," /:%:\\ ","/#:%:#\\"," ~v~v~ "},
            new[]{"    ^    *","   /o\\  / ","  /|:|\\/  "," /::#::\\  ","/:::#:::\\ ","  ~v~v~   "},
            new[]{"   [ooo]   "," /[:::::]\\ ","[##:###:##]"," |##:#:##| "," /##:#:##\\ "," ~V~~V~~V~ "}
        };
        private sealed class Figure
        {
            public Vector2 origin;
            public readonly List<TextMeshProUGUI> rows=new();
            public float line, phase, scale;
            public int kind;
        }
        private static Color Hex(string hex){ColorUtility.TryParseHtmlString("#"+hex,out var c);return c;}
        private Color Ink(int kind) => variant==0
            ? Hex(kind==0?"f2f5db":kind==1?"9fdb68":kind==2?"eda45b":"ba9cfa")
            : Hex(kind==0?"e6f3ed":kind==1?"d79774":kind==2?"d6b6e8":"b7a2dc");
        private void Awake()
        {
            previousPipeline=QualitySettings.renderPipeline;QualitySettings.renderPipeline=studyPipeline;
            Application.targetFrameRate=60;
            Application.logMessageReceived+=OnLog;
        }
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();
            int at=Array.IndexOf(args,"--study-capture");captureMode=at>=0;
            Build();yield return null;yield return null;
            if(!captureMode)yield break;
            var output=Path.GetFullPath(args[at+1]);Directory.CreateDirectory(output);
            int startFrame=Time.frameCount;
            for(int v=0;v<2;v++)
            {
                variant=v;roster=false;Build();time=1.2f;Animate();
                yield return null;yield return new WaitForEndOfFrame();
                Capture(Path.Combine(output,v==0?"A-terminal.png":"B-ritual.png"));
                SetCrt(false);yield return null;yield return new WaitForEndOfFrame();
                Capture(Path.Combine(output,v==0?"A-clean.png":"B-clean.png"));SetCrt(true);
                for(int f=0;f<24;f++)
                {
                    time=f/12f;Animate();yield return null;yield return new WaitForEndOfFrame();
                    Capture(Path.Combine(output,$"{(v==0?'A':'B')}-motion-{f:00}.png"));
                }
                roster=true;Build();time=0;Animate();yield return null;yield return new WaitForEndOfFrame();
                Capture(Path.Combine(output,v==0?"A-roster.png":"B-roster.png"));
            }
            File.WriteAllText(Path.Combine(output,"render-check.json"),JsonUtility.ToJson(new Evidence{
                width=Screen.width,height=Screen.height,startFrame=startFrame,endFrame=Time.frameCount,
                figures=25,variants=2,motionFramesPerVariant=24,errors=errors.ToArray(),graphics=SystemInfo.graphicsDeviceType.ToString()
            },true));
            Application.Quit(errors.Count==0?0:2);
        }
        private void Update()
        {
            if(captureMode)return;
            var k=Keyboard.current;
            if(k!=null)
            {
                if(k.digit1Key.wasPressedThisFrame){variant=0;Build();}
                if(k.digit2Key.wasPressedThisFrame){variant=1;Build();}
                if(k.rKey.wasPressedThisFrame){roster=!roster;Build();}
                if(k.spaceKey.wasPressedThisFrame)paused=!paused;
                if(k.f2Key.wasPressedThisFrame)SetCrt(!crt);
                if(k.escapeKey.wasPressedThisFrame)Application.Quit();
            }
            if(!paused)time+=Time.deltaTime;
            Animate();
        }
        private void SetCrt(bool enabled)
        {
            crt=enabled;Shader.SetGlobalFloat("_StudyCrtBypass",enabled?0:1);
            volume.weight=enabled?1:0;
        }
        private void Build()
        {
            if(root!=null){root.gameObject.SetActive(false);Destroy(root.gameObject);}
            figures.Clear();attack=null;
            var obj=new GameObject("Visual study canvas",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));
            root=obj.GetComponent<RectTransform>();var canvas=obj.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=Camera.main;canvas.planeDistance=1;canvas.sortingOrder=2000;
            var scaler=obj.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            Camera.main.backgroundColor=variant==0?Hex("020403"):Hex("050409");
            Shader.SetGlobalFloat("_StudyVariant",variant);
            if(volume.profile.TryGet<Bloom>(out var bloom))bloom.intensity.value=variant==0?.70f:.45f;
            SetCrt(crt);
            if(roster){BuildRoster();return;}
            var terrain=variant==0?Hex("183823"):Hex("292237");
            var tree=variant==0?new[]{"    ^    ","   /A\\   ","  /xAx\\  "," /xAxAx\\ ","/xAxAxAx\\","   {|}   "}
                :new[]{"  \\ | /  ","--\\ | /--","   \\|/   ","  /:#:\\  "," /::#::\\ ","   |#|   "};
            var trees=new[]{new Vector2(150,190),new Vector2(875,190),new Vector2(1710,155),new Vector2(180,470),new Vector2(940,335),new Vector2(1750,470),new Vector2(190,930),new Vector2(850,920),new Vector2(1730,935)};
            foreach(var p in trees)Block(tree,p,17,17,terrain);
            foreach(var p in new[]{new Vector2(580,535),new Vector2(1315,235),new Vector2(1150,990)})
                Block(new[]{" .---. "," | : | "," | # | "," '---' "},p,16,17,terrain*.85f);
            var rng=new System.Random(1597);
            for(int i=0;i<40;i++)Label(i%4==0?"' .":".",new Vector2(90+rng.Next(1740),150+rng.Next(790)),14,terrain*.55f);
            // Same actor locations, roles and phase offsets in both variants.
            for(int i=0;i<locations.Length;i++)AddFigure(i%3+1,locations[i],1,i*.81f);
            AddFigure(0,hero,1,0);
            for(int i=0;i<18;i++)
            {
                float a=i*2.39996f;float r=110+(i%4)*57;
                Label("+",hero+new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r*.6f),17,variant==0?Hex("838abf"):Hex("689ca1"));
            }
            // One illustrative RuneBolt volley. There is no collision or combat logic here.
            attack=Label(". : > > *",new Vector2(1028,494),20,variant==0?Hex("e6f5ff"):Hex("a2f1e4"));
            attack.rectTransform.localRotation=Quaternion.Euler(0,0,23);
            Label("!",locations[6]+new Vector2(0,-120),24,Hex("ff6264"));
            Label("o",new Vector2(1110,448),20,Hex("ff6264"));
            var hud=variant==0?Hex("d8ddc3"):Hex("c8c0d0");
            Label("HP [||||||||||||||||....] 100/120",new Vector2(28,22),20,variant==0?Hex("bedc76"):Hex("b6d4c1"));
            Label("XP [||||||||............]  36/60",new Vector2(28,50),20,variant==0?Hex("a79fdb"):Hex("aaa0c8"));
            Label("Lv.7   Wave 3   Kills 202",new Vector2(28,78),20,hud);
            Label("02:00",new Vector2(1780,24),23,hud);
            Label(variant==0?"THE WAKING GROVE":"THE ASHEN GROVE",new Vector2(770,30),16,terrain*2);
            Label("[ > RUNEBOLT II ]   [ o WARD I ]",new Vector2(710,1015),20,hud);
            Label("1 / 2 STYLE    SPACE PAUSE    F2 CRT    R ROSTER",new Vector2(655,1050),13,terrain*2);
        }
        private void BuildRoster()
        {
            Label(variant==0?"A / PHOSPHOR TERMINAL":"B / RITUAL TERMINAL",new Vector2(100,60),26,Ink(0));
            Label("TOP: GAME SIZE     BOTTOM: 3x INSPECTION",new Vector2(100,110),18,Hex("7f8890"));
            var names=new[]{"RUNESCRIBE","PURSUER","CASTER","WARDEN"};
            for(int k=0;k<4;k++)
            {
                float x=270+k*455;
                Label(names[k],new Vector2(x-90,210),19,Ink(k));
                AddFigure(k,new Vector2(x,365),1,k*.8f);
                AddFigure(k,new Vector2(x,850),3,k*.8f);
            }
        }
        private void AddFigure(int kind,Vector2 p,float scale,float phase)
        {
            var data=(variant==0?A:B)[kind];float size=(variant==0?22:17)*scale;float line=(variant==0?22:17)*scale;
            var figure=new Figure{origin=p,kind=kind,phase=phase,line=line,scale=scale};
            float cell=size*.63f;int columns=0;foreach(var row in data)columns=Math.Max(columns,row.Length);
            for(int r=0;r<data.Length;r++)
            {
                var color=Ink(kind);
                if(variant==1&&r>=3)color=kind==0?Hex("92bdba"):color*.72f;
                var label=Label(data[r],new Vector2(p.x-columns*cell*.5f,p.y-(data.Length-r)*line),size,color);
                figure.rows.Add(label);
            }
            figures.Add(figure);
            if(!roster)Label(". : .",p+new Vector2(-20,10),14,Ink(kind)*.13f);
        }
        private void Animate()
        {
            foreach(var f in figures)
            {
                float bob=Mathf.Sin(time*Mathf.PI+f.phase)*(variant==0?4:3)*f.scale;
                float drift=roster?0:Mathf.Sin(time*Mathf.PI+f.phase)*(f.kind==0?8:10);
                for(int r=0;r<f.rows.Count;r++)
                {
                    var label=f.rows[r];var p=label.rectTransform.anchoredPosition;
                    float cell=label.fontSize*.63f;
                    int columns=0;foreach(var row in (variant==0?A:B)[f.kind])columns=Math.Max(columns,row.Length);
                    float cloth=variant==1&&r>2?Mathf.Sin(time*Mathf.PI+f.phase-r*.4f)*(r-2)*.7f*f.scale:0;
                    label.rectTransform.anchoredPosition=new Vector2(f.origin.x-columns*cell*.5f+drift+cloth,-(f.origin.y-(f.rows.Count-r)*f.line+bob));
                }
            }
            if(attack!=null)attack.rectTransform.anchoredPosition=new Vector2(1030+Mathf.Sin(time*Mathf.PI)*30,-(494-Mathf.Sin(time*Mathf.PI)*12));
        }
        private TextMeshProUGUI Label(string value,Vector2 point,float size,Color color)
        {
            var go=new GameObject("ASCII",typeof(RectTransform),typeof(TextMeshProUGUI));
            var text=go.GetComponent<TextMeshProUGUI>();text.rectTransform.SetParent(root,false);
            text.rectTransform.anchorMin=text.rectTransform.anchorMax=text.rectTransform.pivot=new Vector2(0,1);
            text.rectTransform.anchoredPosition=new Vector2(point.x,-point.y);text.rectTransform.sizeDelta=new Vector2(1900,220);
            text.font=font;text.fontSize=size;text.color=color;text.richText=false;text.textWrappingMode=TextWrappingModes.NoWrap;
            text.raycastTarget=false;text.text=value;text.characterSpacing=.6f;
            return text;
        }
        private void Block(string[] rows,Vector2 bottom,float size,float line,Color color)
        {
            int width=0;foreach(var s in rows)width=Math.Max(width,s.Length);
            for(int r=0;r<rows.Length;r++)Label(rows[r],new Vector2(bottom.x-width*size*.31f,bottom.y-(rows.Length-r)*line),size,color);
        }
        private void Capture(string path)
        {
            Canvas.ForceUpdateCanvases();
            var camera=Camera.main;var target=RenderTexture.GetTemporary(Screen.width,Screen.height,24,RenderTextureFormat.ARGB32);
            var old=RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
                RenderTexture.active=target;var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());Destroy(image);
            }
            finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(target);}
        }
        private void OnLog(string message,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)errors.Add(message);}
        private void OnDestroy(){Application.logMessageReceived-=OnLog;QualitySettings.renderPipeline=previousPipeline;}
        [Serializable]private sealed class Evidence{public int width,height,startFrame,endFrame,figures,variants,motionFramesPerVariant;public string graphics;public string[] errors;}
    }
}
