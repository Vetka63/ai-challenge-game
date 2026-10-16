using TMPro;
using UnityEngine;
using Zenject;

namespace AsciiArena
{
    public sealed partial class ArenaHud : MonoBehaviour
    {
        public TMP_FontAsset font;
        public TMP_FontAsset menuFont;
        [Inject] private ArenaSimulation simulation;
        [Inject] private ArenaSession session;
        private TextMeshProUGUI health, counters, timer, state, center;
        private UnityEngine.UI.Image boltFill, pulseFill;
        private GameObject canvasObject;
        private GameObject upgradePanel;
        private GameObject sessionPanel;
        private readonly TextMeshProUGUI[] upgradeLabels = new TextMeshProUGUI[3];
        private UnityEngine.UI.Image bossFill;
        private TextMeshProUGUI progression, objective;
        private TextMeshProUGUI choiceTitle,choiceHint,firstWeapon,secondWeapon,firstStats,secondStats,dashStatus,hurtStatus;
        private readonly UnityEngine.UI.Button[] choiceButtons=new UnityEngine.UI.Button[3];

        private void Start()
        {
            canvasObject = new GameObject("Arena HUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main; canvas.planeDistance = 1; canvas.sortingOrder = 1000;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1; // Same height-based proportional scale as world glyphs.
            var root = canvasObject.GetComponent<RectTransform>();
            var top = Panel(root,"Vitals",new Vector2(0,1),new Vector2(28,-22),new Vector2(500,100),Color.clear);
            health=Text(top,"",Vector2.zero,new Vector2(500,28),20,new Color(.75f,.86f,.46f));
            progression=Text(top,"",new Vector2(0,-28),new Vector2(500,28),20,new Color(.65f,.62f,.86f));
            counters=Text(top,"",new Vector2(0,-56),new Vector2(600,28),20,new Color(.85f,.87f,.76f));
            var right=Panel(root,"Run",new Vector2(1,1),new Vector2(-280,-24),new Vector2(250,80),Color.clear);
            timer=Text(right,"",Vector2.zero,new Vector2(250,38),23,new Color(.85f,.87f,.76f));timer.alignment=TextAlignmentOptions.TopRight;
            state=Text(right,"",new Vector2(-100,-40),new Vector2(350,30),15,new Color(.38f,.59f,.40f));state.alignment=TextAlignmentOptions.TopRight;
            boltFill=Attack(root,new Vector2(-380,112),out firstWeapon,out firstStats);
            pulseFill=Attack(root,new Vector2(20,112),out secondWeapon,out secondStats);
            var dashPanel=Panel(root,"Dash status",new Vector2(.5f,0),new Vector2(-250,150),new Vector2(500,26),Color.clear);
            dashStatus=Text(dashPanel,"",Vector2.zero,new Vector2(500,26),18,new Color(.35f,.88f,.79f));dashStatus.alignment=TextAlignmentOptions.Center;
            hurtStatus=Text(top,"",new Vector2(0,-90),new Vector2(600,28),19,new Color(1,.4f,.3f));
            var controls=Panel(root,"Controls",new Vector2(.5f,0),new Vector2(-550,38),new Vector2(1100,24),Color.clear);
            Text(controls,"WASD MOVE   SHIFT DASH   ESC MENU / SAVE   1/2/3 CHOOSE   F2 CRT   F11 SCREEN   M MUTE",Vector2.zero,new Vector2(1100,24),15,new Color(.43f,.60f,.46f)).alignment=TextAlignmentOptions.Center;
            var session = Panel(root, "Session result", new Vector2(.5f, .5f), new Vector2(-450, 110), new Vector2(900, 220), new Color(.025f, .045f, .065f, 1f));
            sessionPanel = session.gameObject;
            var centerObject = new GameObject("Session message", typeof(RectTransform));
            centerObject.transform.SetParent(session, false);
            var rect = centerObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(900, 220);
            center = centerObject.AddComponent<TextMeshProUGUI>();
            center.font = font; center.fontSize = 36; center.alignment = TextAlignmentOptions.Center;
            center.color = new Color(.98f, .90f, .69f); center.raycastTarget = false;
            var title=Panel(root,"Grove title",new Vector2(.5f,1),new Vector2(-250,-30),new Vector2(500,24),Color.clear);
            Text(title,"CORRUPTED PROGRAM",Vector2.zero,new Vector2(500,24),16,new Color(.19f,.37f,.25f)).alignment=TextAlignmentOptions.Center;
            var boss = Panel(root,"Objective",new Vector2(.5f,1),new Vector2(-280,-58),new Vector2(560,50),Color.clear);
            objective = Text(boss,"",Vector2.zero,new Vector2(560,26),15,new Color(.65f,.48f,.56f));objective.alignment=TextAlignmentOptions.Center;
            bossFill = Fill(Panel(boss,"Boss health",new Vector2(0,1),new Vector2(30,-30),new Vector2(500,4),Color.clear),new Color(.95f,.4f,.6f));
            var upgrades = Panel(root,"Choose upgrade",new Vector2(.5f,.5f),new Vector2(-450,250),new Vector2(900,500),new Color(.012f,.025f,.018f,.99f));
            upgradePanel = upgrades.gameObject;
            choiceTitle=Text(upgrades,"",new Vector2(32,-24),new Vector2(840,50),30,new Color(.75f,.86f,.46f));
            choiceHint=Text(upgrades,"",new Vector2(32,-82),new Vector2(840,34),19,new Color(.65f,.74f,.8f));
            for(int i=0;i<3;i++)
            {
                int choice=i;
                var card=Panel(upgrades,"Choice "+i,new Vector2(0,1),new Vector2(30,-135-i*110),new Vector2(840,94),new Color(.045f,.085f,.058f));
                card.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
                var button=card.gameObject.AddComponent<UnityEngine.UI.Button>();
                choiceButtons[i]=button;
                button.targetGraphic=card.GetComponent<UnityEngine.UI.Image>();
                button.onClick.AddListener(()=>simulation.ChooseUpgrade(choice));
                upgradeLabels[i]=Text(card,"",new Vector2(22,-12),new Vector2(800,75),24,Color.white);
            }
            if(UnityEngine.EventSystems.EventSystem.current==null)
                new GameObject("UI EventSystem",typeof(UnityEngine.EventSystems.EventSystem),typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule)).transform.SetParent(transform,false);
            upgradePanel.SetActive(false);
            BuildSessionMenu(root);
        }

        private UnityEngine.UI.Image Attack(RectTransform root, Vector2 position,out TextMeshProUGUI label,out TextMeshProUGUI stats)
        {
            var panel = Panel(root, "Weapon", new Vector2(.5f, 0), position, new Vector2(360, 75),Color.clear);
            label=Text(panel,"",new Vector2(18,0),new Vector2(340,30),20,Color.white);
            stats=Text(panel,"",new Vector2(18,-30),new Vector2(340,24),15,new Color(.62f,.70f,.60f));
            var bar = Panel(panel, "Recharge", new Vector2(0, 1), new Vector2(18, -56), new Vector2(320, 3), new Color(.07f,.11f,.08f));
            return Fill(bar,Color.white);
        }
        private static RectTransform Panel(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color? color = null)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rect = obj.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = obj.GetComponent<UnityEngine.UI.Image>();
            image.color = color ?? new Color(.025f, .06f, .064f, .91f); image.raycastTarget = false;
            return rect;
        }
        private static UnityEngine.UI.Image Fill(RectTransform parent, Color color)
        {
            var obj = new GameObject("Fill", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rect = obj.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = obj.GetComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false;
            return image;
        }
        private TextMeshProUGUI Text(RectTransform parent, string content, Vector2 position, Vector2 size, float fontSize, Color color)
        {
            var obj = new GameObject(content.Length == 0 ? "Value" : content, typeof(RectTransform));
            var rect = obj.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = obj.AddComponent<TextMeshProUGUI>(); text.font = font; text.text = content;
            text.fontSize = fontSize; text.color = color; text.raycastTarget = false;
            text.characterSpacing=.6f; text.enableAutoSizing=false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }
        private void LateUpdate()
        {
            if (health == null || simulation == null) return;
            health.text = $"HP [{Bar(simulation.HeroHealth/simulation.MaxHealth)}] {simulation.HeroHealth:0}/{simulation.MaxHealth:0}";
            float remaining=simulation.Elapsed;
            timer.text=$"{Mathf.FloorToInt(remaining/60):00}:{Mathf.FloorToInt(remaining%60):00}";
            counters.text=$"Lv.{simulation.Level}  Wave {simulation.Wave}  Kills {simulation.Kills}  Foes {simulation.EnemyQuery.CalculateEntityCount()}";
            state.text = simulation.DemoMode ? "DEMO / move to take control" : simulation.HighContrast ? "CLEAR VIEW" : "";
            if (simulation.PlaybackSpeed > 1) state.text = $"PLAYTEST x{simulation.PlaybackSpeed}";
            UpdateWeapon(0,firstWeapon,firstStats,boltFill);UpdateWeapon(1,secondWeapon,secondStats,pulseFill);
            dashStatus.text=simulation.DashCooldownRemaining<=0?"[ SHIFT : DASH READY ]":$"[ DASH {simulation.DashCooldownRemaining:0.0}s ]";
            health.color=simulation.HurtFlash>.6f?new Color(1,.4f,.3f):new Color(.75f,.86f,.46f);
            hurtStatus.text=simulation.HurtFlash>.2f?$"-{simulation.LastDamage:0} HP  /  BRIEF PROTECTION":simulation.HeroHealth<simulation.MaxHealth*.25f?"LOW HEALTH - KEEP MOVING":"";
            progression.text=$"XP [{Bar((float)simulation.ExperiencePoints/simulation.NextLevel)}] {simulation.ExperiencePoints}/{simulation.NextLevel}";
            objective.text=simulation.CoreTransition > 0 ? "GUARDIAN REMOVED / KERNEL LOADING..." : simulation.Finished ? (simulation.Phase==RunPhase.Victory?"EXIT UNLOCKED":"RUN ENDED") : simulation.BossActive
                ? (session.BossTesting ? "BOSS TEST - " : simulation.Training ? "TRAINING - " : simulation.BossSpawned?"FINAL - ":"WARDEN - ") + (simulation.Training && session.Basics ? "WARDEN" : simulation.BossName)
                : $"{(simulation.NextBossAt>=simulation.Settings.bossAt?ArenaSimulation.NameOfBoss(simulation.FinalBossKind):"WARDEN")} IN {Mathf.Max(0,simulation.NextBossAt-simulation.Elapsed):0}s  /  {simulation.WaveBossesDefeated} DEFEATED";
            SetFill(bossFill,simulation.BossActive?simulation.BossHealthRatio:0);
            bool starter=simulation.Phase==RunPhase.ChoosingWeapon;
            bool choosing=starter||simulation.Phase==RunPhase.ChoosingUpgrade;
            upgradePanel.SetActive(choosing && !session.MenuVisible);
            if(choosing)
            {
                choiceTitle.text=starter?"[ CHOOSE YOUR FIRST WEAPON ]":"[ SHAPE YOUR BUILD ]";
                choiceHint.text=starter?"Auto-attacks. Two weapon slots. Press 1 / 2 / 3 or click.":"Time stopped. Add a weapon or upgrade your build. +10 HP.";
                for(int i=0;i<3;i++)
                {
                    choiceButtons[i].gameObject.SetActive(starter||i<simulation.OfferCount);
                    if(starter)upgradeLabels[i].text=$"[{i+1}] {ArenaSimulation.WeaponName((WeaponKind)i)}\n<size=17>{ArenaSimulation.WeaponDescription((WeaponKind)i)}</size>";
                    else if(i<simulation.OfferCount)
                    {
                        var kind=simulation.Offers[i];int cap=ArenaSimulation.RankCap(kind);
                        string rank=cap>0?$"rank {simulation.UpgradeRanks[(int)kind]} -> {simulation.UpgradeRanks[(int)kind]+1}/{cap}":kind==UpgradeKind.Recovery?"HEAL":"SECOND WEAPON";
                        upgradeLabels[i].text=$"[{i+1}] {ArenaSimulation.UpgradeName(kind)} <size=17>{rank}</size>\n<size=17>{ArenaSimulation.UpgradeDescription(kind)}</size>";
                    }
                }
            }
            string ending=simulation.Phase==RunPhase.Victory?"KERNEL DESTROYED":simulation.Dead?"PROCESS DELETED":"TIME RAN OUT";
            center.text=simulation.Finished?$"{ending}\n<size=24>{simulation.Elapsed/60:0.0} min  /  {simulation.Kills} kills  /  level {simulation.Level}\nR to start a new run</size>":simulation.Paused&&!choosing?"PAUSED\n<size=24>SPACE to continue</size>":"";
            sessionPanel.SetActive(false);
            UpdateSessionMenu();
        }
        private void UpdateWeapon(int slot,TextMeshProUGUI label,TextMeshProUGUI stats,UnityEngine.UI.Image bar)
        {
            var weapon=simulation.WeaponAt(slot);
            Color color=weapon==WeaponKind.Runebolt?new Color(.90f,.96f,1):weapon==WeaponKind.Ward?new Color(.90f,.80f,.51f):weapon==WeaponKind.Orbit?new Color(.35f,.88f,.79f):new Color(.35f,.45f,.38f);
            label.text=$"[ {ArenaSimulation.WeaponName(weapon)} ]";label.color=bar.color=color;
            stats.text=simulation.WeaponSummary(weapon);SetFill(bar,simulation.WeaponCharge(weapon));
        }
        private static void SetFill(UnityEngine.UI.Image image, float value) => image.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1);
        private static string Bar(float value){int n=Mathf.Clamp(Mathf.RoundToInt(value*20),0,20);return new string('|',n)+new string('.',20-n);}
    }
}
