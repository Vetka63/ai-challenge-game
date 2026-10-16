using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AsciiArena
{
    public sealed partial class ArenaHud
    {
        private GameObject menu;
        private RectTransform menuArt;
        private RectTransform settingsControls;
        private UnityEngine.UI.Slider volumeSlider;
        private UnityEngine.UI.Button crtButton, fullscreenButton;
        private TextMeshProUGUI volumeLabel, crtLabel, fullscreenLabel;
        private UnityEngine.UI.Image menuBackdrop;
        private TextMeshProUGUI menuTitle, menuInfo, saveStatus, fightHint, menuEyebrow, menuFooter;
        private readonly UnityEngine.UI.Button[] menuButtons = new UnityEngine.UI.Button[7];
        private readonly TextMeshProUGUI[] menuLabels = new TextMeshProUGUI[7];
        private readonly Action[] menuActions = new Action[7];
        private int menuRows;
        private string previousMenuKey = "";
        private static readonly Color MenuInk = new(.89f, .91f, .75f);
        private static readonly Color MenuMint = new(.35f, .88f, .73f);

        private TextMeshProUGUI MenuText(RectTransform parent, string content, Vector2 position, Vector2 size, float scale, Color color)
        {
            var label = Text(parent, content, position, size, scale, color);
            label.font = menuFont != null ? menuFont : font;
            return label;
        }
        private void BuildSessionMenu(RectTransform root)
        {
            var hint = Panel(root, "Boss instruction", new Vector2(.5f, 1), new Vector2(-500, -113), new Vector2(1000, 54), new Color(.012f, .025f, .018f, .96f));
            fightHint = Text(hint, "", Vector2.zero, new Vector2(1000, 54), 17, new Color(.95f, .74f, .48f));
            fightHint.alignment = TextAlignmentOptions.Center;
            var backdrop = Panel(root, "Menu screen", Vector2.zero, Vector2.zero, Vector2.zero, Color.black);
            backdrop.anchorMin = Vector2.zero; backdrop.anchorMax = Vector2.one; backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
            menu = backdrop.gameObject; menuBackdrop = backdrop.GetComponent<UnityEngine.UI.Image>(); menuBackdrop.raycastTarget = true;
            var panel = Panel(backdrop, "Menu content", new Vector2(.5f, .5f), new Vector2(-640, 450), new Vector2(1280, 900), new Color(.012f, .025f, .018f, 1));
            MenuText(panel, "+--------------------------------------------------------------------------+", new Vector2(36, -20), new Vector2(1220, 44), 25, new Color(.18f,.34f,.25f));
            menuEyebrow = MenuText(panel, "", new Vector2(44, -70), new Vector2(1150, 30), 17, MenuMint);
            menuTitle = MenuText(panel, "", new Vector2(44, -120), new Vector2(1180, 136), 48, MenuInk);
            menuInfo = MenuText(panel, "", new Vector2(44, -260), new Vector2(580, 100), 19, new Color(.58f,.70f,.58f));
            menuInfo.textWrappingMode = TextWrappingModes.Normal;
            for (int i = 0; i < menuButtons.Length; i++)
            {
                int index = i;
                var card = Panel(panel, "Menu row " + i, new Vector2(0, 1), new Vector2(44, -382 - i * 56), new Vector2(570, 48), new Color(.038f,.074f,.050f));
                var image = card.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = true;
                var button = card.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
                var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(2, 2.4f, 2);
                colors.selectedColor = colors.highlightedColor; colors.pressedColor = new Color(1.1f, 1.8f, 1.4f); colors.disabledColor = new Color(.65f,.65f,.65f); colors.fadeDuration = .08f; button.colors = colors;
                button.onClick.AddListener(() => menuActions[index]?.Invoke()); menuButtons[i] = button;
                menuLabels[i] = MenuText(card, "", new Vector2(18, -8), new Vector2(536, 34), 23, MenuInk);
            }
            menuArt = Panel(panel, "ASCII grove vignette", new Vector2(0, 1), new Vector2(690, -260), new Vector2(540, 490), Color.clear);
            Decor(menuArt, TerminalActorArt.Tree, new Vector2(30, -25), 25, new Color(.19f,.34f,.23f));
            Decor(menuArt, TerminalActorArt.Tree, new Vector2(280, -60), 30, new Color(.26f,.42f,.27f));
            Decor(menuArt, TerminalActorArt.Tree, new Vector2(160, -5), 18, new Color(.14f,.25f,.18f));
            var hero = Decor(menuArt, TerminalActorArt.Hero[0], new Vector2(140, -245), 39, MenuInk);
            hero.gameObject.name = "Menu ASCII hero";
            MenuText(menuArt, "+       .      *\n      .     +\n   :             .", new Vector2(45, -192), new Vector2(490, 125), 27, MenuMint);
            MenuText(menuArt, ". : . : . : . : . : . : .", new Vector2(30, -420), new Vector2(510, 30), 22, new Color(.22f,.38f,.26f));
            MenuText(menuArt, "[ MOVE / READ / DASH ]", new Vector2(45, -470), new Vector2(500, 30), 20, MenuMint);
            BuildPreferencesControls(panel);
            saveStatus = MenuText(panel, "", new Vector2(44, -800), new Vector2(1180, 42), 16, new Color(.53f,.67f,.55f));
            saveStatus.textWrappingMode = TextWrappingModes.Normal;
            menuFooter = MenuText(panel, "", new Vector2(44, -860), new Vector2(1180, 28), 16, new Color(.36f,.52f,.40f));
        }
        private TextMeshProUGUI Decor(RectTransform parent, string[] lines, Vector2 position, float size, Color color)
        {
            var label = MenuText(parent, string.Join("\n", lines), position, new Vector2(460, 260), size, color);
            label.richText = false; label.lineSpacing = -5; return label;
        }
        private void Row(string name, string title, Action action, bool enabled = true)
        {
            int i = menuRows++;
            menuButtons[i].gameObject.SetActive(true); menuButtons[i].gameObject.name = name;
            menuButtons[i].interactable = enabled; menuActions[i] = action;
            menuLabels[i].text = title;
            menuLabels[i].color = enabled ? MenuInk : new Color(.38f,.47f,.39f);
        }
        private void UpdateSessionMenu()
        {
            fightHint.text = session.Home ? "" : simulation.CoreTransition > 0 ? "GUARDIAN REMOVED / KERNEL LOADING..." : simulation.BossActive ? simulation.BossAction + "\n" + simulation.BossHint : simulation.RosterHint;
            fightHint.transform.parent.gameObject.SetActive(!session.Home && !session.MenuVisible);
            menu.SetActive(session.MenuVisible);
            if (!session.MenuVisible) { previousMenuKey = ""; return; }
            menuBackdrop.color = new Color(.005f, .012f, .008f, session.Home ? 1 : .88f);
            menuRows = 0;
            bool preferences = session.MenuPage == ArenaMenuPage.Settings && !session.ConfirmNew;
            settingsControls.gameObject.SetActive(preferences);
            menuArt.gameObject.SetActive(!preferences);
            if (preferences)
            {
                volumeSlider.SetValueWithoutNotify(Mathf.RoundToInt(session.Profile.masterVolume * 100));
                volumeLabel.text = $"ГРОМКОСТЬ: {session.Profile.masterVolume * 100:0}%" + (session.Profile.muted ? " / M: ВЫКЛ" : "");
                crtLabel.text = $"ЭЛТ: {(session.Profile.crtEnabled ? "[ВКЛ]" : "[ВЫКЛ]")}   / F2";
                fullscreenLabel.text = $"ПОЛНЫЙ ЭКРАН: {(session.Fullscreen ? "ВКЛ" : "ВЫКЛ")} / F11";
            }
            menuEyebrow.text = session.Home ? "НЕИЗВЕСТНЫЙ ПРОЦЕСС   /   ДОСТУП ЗАПРЕЩЁН" : "АКТ I   /   " + (session.BossTesting ? "ТЕСТ БОССА" : simulation.Training ? "ТРЕНИРОВКА" : "ЭКСПЕДИЦИЯ");
            menuTitle.text = session.Home ? "CORRUPTED\nPROGRAM" : simulation.Finished ? simulation.Phase == RunPhase.Victory ? simulation.Training ? "ТЕСТ ПРОЙДЕН" : "ВЫХОД ОТКРЫТ" : "ЗАБЕГ ЗАВЕРШЁН" : "ПАУЗА";
            menuInfo.text = session.Home ? "Ты попал в повреждённую программу.\nСистема считает тебя ошибкой.\nУничтожь Ядро, чтобы выбраться."
                : $"{simulation.Elapsed / 60:00}:{simulation.Elapsed % 60:00}   /   Уровень {simulation.Level}   /   {simulation.Kills} убийств\nСкорость x{simulation.PlaybackSpeed}. Бой остановлен.";
            UpdateHeroPreview();
            if (simulation.Phase == RunPhase.Victory && !session.Home && !simulation.Training && simulation.FinalBossKind == SpecialBoss.Core)
                menuInfo.text = "Ядро уничтожено. Система больше не удерживает тебя.\nТы выбрался.";
            if (session.ConfirmNew)
            {
                menuTitle.text = "НОВАЯ ЭКСПЕДИЦИЯ";
                menuInfo.text = "Текущий сохранённый забег будет заменён.\nНачать заново?";
                Row("Menu confirm", "> НАЧАТЬ ЗАНОВО", () => session.RequestNew());
                Row("Menu back", "< ОТМЕНА", session.Back);
            }
            else if (session.MenuPage == ArenaMenuPage.Heroes)
            {
                menuTitle.text = "ВЫБЕРИ ЧУЖАКА";
                menuInfo.text = HeroRoster.Description(session.Profile.selectedHero) + "\nХарактеристики одинаковы. Выбери свой облик.";
                foreach (var hero in new[] { HeroIdentity.Spark, HeroIdentity.Pacman, HeroIdentity.Doomguy, HeroIdentity.Ascii })
                {
                    var selected = hero;
                    Row("Menu hero " + (int)hero, (session.Profile.selectedHero == hero ? "[x] " : "[ ] ") + HeroRoster.Name(hero), () => session.SelectHero(selected));
                }
                Row("Menu hero start", "> ВОЙТИ В ПРОГРАММУ", session.BeginSelectedRun);
                Row("Menu back", "< НАЗАД", session.Back);
            }
            else if (session.MenuPage == ArenaMenuPage.Settings)
            {
                menuTitle.text = "НАСТРОЙКИ";
                menuInfo.text = "Скорость игры\nУскоряются движение, атаки и таймер боссов.\nВыбор сохраняется автоматически.";
                foreach (int speed in new[] { 1, 2, 4 })
                {
                    int value = speed;
                    Row("Menu speed " + value, $"{(simulation.PlaybackSpeed == value ? "[x]" : "[ ]")}  x{value}  /  {(value == 1 ? "Обычная" : "Ускоренная")}", () => session.SetPlaybackSpeed(value));
                }
                Row("Menu boss tests", "> ТЕСТ БОССОВ / быстрый вход", () => session.OpenMenuPage(ArenaMenuPage.BossTests));
                Row("Menu back", "< НАЗАД", session.Back);
            }
            else if (session.MenuPage == ArenaMenuPage.BossTests)
            {
                menuTitle.text = "ТЕСТ БОССОВ";
                menuInfo.text = $"Любой босс доступен сразу. Готовый билд.\nЭкспедиция сохранится; наград в тесте нет.\nСкорость теста: x{simulation.PlaybackSpeed}. Меняется в настройках.";
                Row("Menu test warden", "> ФАЕРВОЛ / рывок и удар", () => session.TestBoss(SpecialBoss.Bellkeeper, true));
                Row("Menu test bellkeeper", "> ТРЕВОГА / волны и эхо", () => session.TestBoss(SpecialBoss.Bellkeeper));
                Row("Menu test archivist", "> АРХИВ / полосы и удаление", () => session.TestBoss(SpecialBoss.Archivist));
                Row("Menu test core", "> ЯДРО / прогноз и очистка", () => session.TestBoss(SpecialBoss.Core));
                Row("Menu test phase", session.Profile.bossTestSecondPhase ? "ФАЗА ОСОБЫХ БОССОВ: 2 / 45% HP" : "ФАЗА ОСОБЫХ БОССОВ: 1 / 100% HP", session.ToggleBossTestPhase);
                Row("Menu test crowd", session.Profile.bossTestCrowd ? "БОЙ: С ТОЛПОЙ" : "БОЙ: СОЛО", session.ToggleBossTestCrowd);
                Row("Menu back", "< НАЗАД В НАСТРОЙКИ", session.Back);
            }
            else if (session.MenuPage == ArenaMenuPage.Training)
            {
                menuTitle.text = "ТРЕНИРОВКА";
                menuInfo.text = "Движение и рывок доступны сразу.\nБоссы открываются после первой встречи.\nСохранённая экспедиция остаётся с тобой.";
                Row("Menu basics", "> ДВИЖЕНИЕ И РЫВОК", () => session.Practice(SpecialBoss.Bellkeeper, true));
                Row("Menu bellkeeper", session.Unlocked(SpecialBoss.Bellkeeper) ? "> СИСТЕМА ТРЕВОГИ" : "[-] ТРЕВОГА / не встречена", () => session.Practice(SpecialBoss.Bellkeeper), session.Unlocked(SpecialBoss.Bellkeeper));
                Row("Menu archivist", session.Unlocked(SpecialBoss.Archivist) ? "> ПОВРЕЖДЁННЫЙ АРХИВ" : "[-] АРХИВ / не встречен", () => session.Practice(SpecialBoss.Archivist), session.Unlocked(SpecialBoss.Archivist));
                Row("Menu core", session.Unlocked(SpecialBoss.Core) ? "> ЯДРО ПРОГРАММЫ" : "[-] ЯДРО / не встречено", () => session.Practice(SpecialBoss.Core), session.Unlocked(SpecialBoss.Core));
                Row("Menu back", "< НАЗАД", session.Back);
            }
            else if (session.Home)
            {
                Row("Menu play", "> ИГРАТЬ", () => session.RequestNew());
                Row("Menu continue", "> ПРОДОЛЖИТЬ", session.Continue, session.HasSavedRun);
                Row("Menu training", "> ТРЕНИРОВКА", () => session.OpenMenuPage(ArenaMenuPage.Training));
                Row("Menu settings", "> НАСТРОЙКИ", () => session.OpenMenuPage(ArenaMenuPage.Settings));
                Row("Menu quit", "> ВЫХОД", session.Quit);
            }
            else
            {
                if (!simulation.Finished) Row("Menu resume", "> ВЕРНУТЬСЯ В ИГРУ", session.TogglePause);
                if (simulation.Training) Row("Menu retry", "> ПОВТОРИТЬ ТРЕНИРОВКУ", session.RetryTraining);
                if (session.BossTesting && session.HasSavedRun) Row("Menu return expedition", "> ВЕРНУТЬСЯ К ЭКСПЕДИЦИИ", session.Continue);
                Row("Menu settings", "> НАСТРОЙКИ", () => session.OpenMenuPage(ArenaMenuPage.Settings));
                if (!simulation.Training && !simulation.Finished) Row("Menu save", "> СОХРАНИТЬ", () => session.Save());
                Row("Menu home", "> ГЛАВНОЕ МЕНЮ", session.ToHome);
                Row("Menu quit", "> ВЫХОД", session.Quit);
            }
            for (int i = menuRows; i < menuButtons.Length; i++) menuButtons[i].gameObject.SetActive(false);
            saveStatus.text = session.Status;
            menuFooter.text = session.Home ? "WASD / стрелки: выбор   ENTER: открыть   ESC: назад" : "ESC: назад   /   Выход в меню и из игры сохраняет экспедицию";
            string key = $"{session.Home}:{session.MenuPage}:{session.ConfirmNew}:{simulation.Finished}:{simulation.Training}";
            if (key != previousMenuKey)
            {
                previousMenuKey = key;
                // Explicit vertical navigation skips locked entries and never selects hidden HUD buttons.
                var enabled = new System.Collections.Generic.List<UnityEngine.UI.Selectable>();
                for (int i = 0; i < menuRows; i++) if (menuButtons[i].interactable) enabled.Add(menuButtons[i]);
                if (preferences) { enabled.Insert(0, fullscreenButton); enabled.Insert(0, crtButton); enabled.Insert(0, volumeSlider); }
                for (int i = 0; i < enabled.Count; i++)
                {
                    var nav = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit,
                        selectOnUp = enabled[(i + enabled.Count - 1) % enabled.Count], selectOnDown = enabled[(i + 1) % enabled.Count] };
                    enabled[i].navigation = nav;
                }
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(enabled.Count > 0 ? enabled[0].gameObject : null);
            }
            // A small real-time hover gives the title screen motion while the combat stays paused.
            menuArt.anchoredPosition = new Vector2(690, -260 + Mathf.Sin(Time.unscaledTime * .8f) * 4);
        }
    }
}
