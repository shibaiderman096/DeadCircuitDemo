using System;
using TMPro;
using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class DemoHud
    {
        static readonly Color TitleBrass = new Color32(244, 199, 117, 255);
        static readonly Color TitleShadow = new Color32(10, 19, 18, 255);

        UnityEngine.UI.Button titleSettingsButton, settingsCloseButton;
        UnityEngine.UI.RawImage titlePanorama, titleForeground;
        Vector2 titleViewport;

        void MakeTitlePanorama(RectTransform page)
        {
            titlePanorama = MakeTitleLayer(page, "Title Panorama", "Art/TitlePanorama");
            titleForeground = MakeTitleLayer(page, "Title Foreground", "Art/TitleForeground");
            RectTransform shade = Box("Panorama Shade", page, 0, 0, 0, 0, new Color32(9, 18, 17, 34));
            Stretch(shade);
        }

        UnityEngine.UI.RawImage MakeTitleLayer(RectTransform parent, string name, string resource)
        {
            var texture = Resources.Load<Texture2D>(resource);
            if (texture == null) return null;
            RectTransform rect = NewRect(name, parent);
            Center(rect, 0, 0);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            return image;
        }

        void LateUpdate()
        {
            if (menu == null || !menu.gameObject.activeInHierarchy) return;
            Vector2 viewport = menu.rect.size;
            if (viewport != titleViewport)
            {
                CoverTitleLayer(titlePanorama, viewport);
                CoverTitleLayer(titleForeground, viewport);
                titleViewport = viewport;
            }

            // Move only the scenery. The title and all interactive controls stay fixed.
            float drift = Mathf.Sin(Time.unscaledTime * .075f);
            if (titlePanorama != null) titlePanorama.rectTransform.anchoredPosition = new Vector2(drift * viewport.x * .022f, 0);
            if (titleForeground != null) titleForeground.rectTransform.anchoredPosition = new Vector2(drift * viewport.x * .05f, 0);
        }

        static void CoverTitleLayer(UnityEngine.UI.RawImage layer, Vector2 viewport)
        {
            if (layer == null) return;
            var texture = layer.texture;
            // Overscan horizontally so the foreground still covers both edges at peak drift.
            float scale = Mathf.Max(viewport.x * 1.12f / texture.width, viewport.y / texture.height);
            layer.rectTransform.sizeDelta = new Vector2(texture.width, texture.height) * scale;
        }

        void MakeStartPage(RectTransform page)
        {
            RectTransform content = NewRect("Title and Actions", page);
            Center(content, 680, 532);

            TitleLabel("Title Shadow", "废土回路", -27, 178, 103, TitleShadow, 10);
            TitleLabel("Title", "废土回路", -31, 178, 103, Ink, 10);
            TitleLabel("English Title", "DEAD CIRCUIT", 106, 34, 23, TitleBrass, 18);

            Box("Left Divider", content, 130, 125, 57, 2, TitleBrass);
            Box("Right Divider", content, 493, 125, 57, 2, TitleBrass);

            RectTransform actions = NewRect("Start Actions", content);
            Place(actions, 144, 172, 392, 174);
            TitleButton("Single Player", "单人游戏", 0, 0, 392, Load, true, "ui_load_world");
            TitleButton("Multiplayer", "多人游戏", 0, 60, 392, () => ShowMenuPage(MenuPage.Lan), false, "ui_network");
#if UNITY_WEBGL && !UNITY_EDITOR
            titleSettingsButton = TitleButton("Open Settings", "设置", 0, 120, 392, OpenSettings, false, "ui_settings").button;
#else
            titleSettingsButton = TitleButton("Open Settings", "设置", 0, 120, 190, OpenSettings, false, "ui_settings").button;
            TitleButton("Quit Game", "退出游戏", 202, 120, 190, QuitGame, false, "ui_quit");
#endif
            var version = Label("Game Version", page, "v" + Application.version, 0, 0, 96, 24, 14, Ink);
            version.rectTransform.anchorMin = version.rectTransform.anchorMax = version.rectTransform.pivot = Vector2.zero;
            version.rectTransform.anchoredPosition = new Vector2(24, 18);

            TextMeshProUGUI TitleLabel(string name, string value, float top, float height, float size, Color color, float spacing)
            {
                var label = Label(name, content, value, 0, top, 680, height, size, color, FontStyles.Bold);
                label.alignment = TextAlignmentOptions.Center;
                label.characterSpacing = spacing;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                return label;
            }

            UiButton TitleButton(string name, string text, float x, float y, float width, Action action, bool primary = false, string icon = null)
            {
                const float height = 54;
                Box(name + " Shadow", actions, x + 3, y + 5, width, height, TitleShadow);
                UiButton widget = Button(name, actions, text, x, y, width, height, action, primary);
                if (!string.IsNullOrEmpty(icon)) SetButtonArt(widget, icon, 34);
                Color face = primary ? new Color32(130, 83, 34, 255) : new Color32(30, 46, 41, 255);
                Color rim = primary ? TitleBrass : new Color32(109, 131, 117, 255);
                var colors = widget.button.colors;
                colors.normalColor = face;
                colors.highlightedColor = colors.selectedColor = Color.Lerp(face, rim, .23f);
                colors.pressedColor = Color.Lerp(face, rim, .38f);
                widget.button.colors = colors;
                widget.label.color = Ink;
                widget.label.fontStyle = FontStyles.Bold;
                widget.label.fontSizeMin = 18;
                widget.label.fontSizeMax = 21;
                RectTransform rect = widget.image.rectTransform;
                Box("Top Edge", rect, 0, 0, width, 2, rim);
                Box("Bottom Edge", rect, 0, height - 3, width, 3, TitleShadow);
                return widget;
            }
        }

        static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }
    }
}
