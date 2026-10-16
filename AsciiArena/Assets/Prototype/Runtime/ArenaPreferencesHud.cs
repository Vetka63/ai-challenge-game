using UnityEngine;

namespace AsciiArena
{
    public sealed partial class ArenaHud
    {
        private void BuildPreferencesControls(RectTransform panel)
        {
            settingsControls = Panel(panel, "Audio and display settings", new Vector2(0, 1), new Vector2(690, -260), new Vector2(540, 490), Color.clear);
            MenuText(settingsControls, "ЗВУК И ЭКРАН", Vector2.zero, new Vector2(530, 42), 25, MenuMint);
            volumeLabel = MenuText(settingsControls, "", new Vector2(0, -64), new Vector2(530, 38), 22, MenuInk);
            var sliderRect = Panel(settingsControls, "Menu volume", new Vector2(0, 1), new Vector2(0, -112), new Vector2(510, 40), new Color(.038f, .074f, .05f));
            sliderRect.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            volumeSlider = sliderRect.gameObject.AddComponent<UnityEngine.UI.Slider>();
            volumeSlider.minValue = 0; volumeSlider.maxValue = 100; volumeSlider.wholeNumbers = true;
            var fillArea = new GameObject("Volume fill area", typeof(RectTransform)).GetComponent<RectTransform>();
            fillArea.SetParent(sliderRect, false); fillArea.anchorMin = Vector2.zero; fillArea.anchorMax = Vector2.one;
            fillArea.offsetMin = new Vector2(12, 14); fillArea.offsetMax = new Vector2(-12, -14);
            volumeSlider.fillRect = Fill(fillArea, MenuMint).rectTransform;
            var handleArea = new GameObject("Volume handle area", typeof(RectTransform)).GetComponent<RectTransform>();
            handleArea.SetParent(sliderRect, false); handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(12, 4); handleArea.offsetMax = new Vector2(-12, -4);
            var handle = Panel(handleArea, "Volume handle", Vector2.zero, Vector2.zero, new Vector2(14, 0), MenuInk);
            handle.pivot = new Vector2(.5f, .5f);
            volumeSlider.handleRect = handle; volumeSlider.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>();
            volumeSlider.onValueChanged.AddListener(value => session.SetMasterVolume(value / 100));
            MenuText(settingsControls, "Мышь / влево-вправо: громкость. M: звук.", new Vector2(0, -162), new Vector2(540, 30), 16, new Color(.53f, .67f, .55f));
            crtButton = PreferenceButton(settingsControls, "Menu crt", -222, out crtLabel);
            crtButton.onClick.AddListener(() => session.SetCrt(!session.Profile.crtEnabled));
            fullscreenButton = PreferenceButton(settingsControls, "Menu fullscreen", -300, out fullscreenLabel);
            fullscreenButton.onClick.AddListener(() => session.SetFullscreen(!session.Fullscreen));
            var note = MenuText(settingsControls, "Применяется сразу.\nСохраняется между запусками.", new Vector2(0, -394), new Vector2(530, 80), 19, new Color(.53f, .67f, .55f));
            note.textWrappingMode = TMPro.TextWrappingModes.Normal;
        }
        private UnityEngine.UI.Button PreferenceButton(RectTransform parent, string name, float y, out TMPro.TextMeshProUGUI label)
        {
            var rect = Panel(parent, name, new Vector2(0, 1), new Vector2(0, y), new Vector2(530, 56), new Color(.038f, .074f, .05f));
            var graphic = rect.GetComponent<UnityEngine.UI.Image>(); graphic.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = graphic;
            button.colors = menuButtons[0].colors;
            label = MenuText(rect, "", new Vector2(16, -12), new Vector2(500, 36), 21, MenuInk);
            return button;
        }
    }
}
