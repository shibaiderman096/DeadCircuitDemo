using TMPro;
using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class DemoHud
    {
        // Match the title page's pixel-industrial treatment across the rest of the
        // runtime interface while leaving every existing layout and action intact.
        void ApplyIndustrialUiTheme(RectTransform root)
        {
            if (root == null) return;

            Transform[] nodes = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform node in nodes)
            {
                RectTransform rect = node as RectTransform;
                if (rect == null || IsTitleElement(rect)) continue;
                var image = rect.GetComponent<UnityEngine.UI.Image>();
                if (image == null) continue;
                if (rect.name == "Panel Header Rule") image.color = TitleBrass;
                if (rect.GetComponent<UnityEngine.UI.Button>() == null && IsPanelSurface(rect))
                {
                    if (rect.GetComponent<TMP_InputField>() != null || rect.GetComponent<UnityEngine.UI.Slider>() != null)
                        AddControlFrame(rect);
                    else AddPanelFrame(rect);
                }
            }
        }

        bool IsTitleElement(RectTransform rect)
        {
            return menuPages != null && menuPages.Length > 0 && rect.IsChildOf(menuPages[0]);
        }

        bool IsPanelSurface(RectTransform rect)
        {
            if (rect == null) return false;
            string name = rect.name;
            if (name == "Main Menu" || name == "Technology Panel" || name == "Session Modal" ||
                name == "Help Panel" || name == "Growth Modal" || name == "Death Modal" ||
                name == "Inventory Panel" || name == "Survival Panel" || name == "Settings Panel") return false;
            if (name.Contains("Track") || name.Contains("Fill") || name.Contains("Handle") ||
                name.Contains("Scrollbar") || name.Contains("Viewport") || name.Contains("Sliding") ||
                name.Contains("Shadow") || name.Contains("Shade") || name.Contains("Rule") ||
                name.Contains("Edge") || name.Contains("Icon")) return false;

            Rect size = rect.rect;
            if (size.width < 180f || size.height < 38f) return false;
            if (rect.GetComponent<TMP_InputField>() != null || rect.GetComponent<UnityEngine.UI.Slider>() != null) return true;
            return name.Contains("Card") || name.Contains("Frame") || name.Contains("Surface") ||
                name.Contains("HUD") || name.Contains("Hud") || name.Contains("Tray") ||
                name.Contains("Status") || name.Contains("Vitals") || name.Contains("Details") ||
                name.Contains("Summary") || name.Contains("Browser") || name.Contains("Target") ||
                name.Contains("Equipment") || name.Contains("Inspector") || name.Contains("World List") ||
                name.Contains("Connection") || name.Contains("Scroll View") || name.Contains("Input") ||
                name.Contains("Field");
        }

        void AddButtonFrame(UnityEngine.UI.Button button)
        {
            if (button == null || button.transform.Find("__Pixel Button Frame") != null) return;

            Color normal = button.colors.normalColor;
            bool primary = ColorDistance(normal, Selected) < .22f;
            Color rim = primary ? Amber : Edge;
            RectTransform rect = button.transform as RectTransform;
            var outline = rect.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = Edge;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;

            RectTransform frame = NewRect("__Pixel Button Frame", rect);
            Stretch(frame);
            frame.SetAsLastSibling();
            AddEdge("Top", frame, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -2), Vector2.zero, rim);
        }

        void AddPanelFrame(RectTransform rect)
        {
            if (rect.Find("__Pixel Panel Frame") != null) return;

            var outline = rect.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = Edge;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;

            RectTransform frame = NewRect("__Pixel Panel Frame", rect);
            Stretch(frame);
            frame.SetAsLastSibling();
            AddEdge("Top", frame, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -2), Vector2.zero, Edge);
            AddEdge("Accent", frame, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(3, 0), Accent);
            AddEdge("Bottom", frame, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 2), Recessed);
        }

        void AddControlFrame(RectTransform rect)
        {
            if (rect.Find("__Pixel Control Frame") != null) return;

            var outline = rect.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = Edge;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;

            RectTransform frame = NewRect("__Pixel Control Frame", rect);
            Stretch(frame);
            frame.SetAsLastSibling();
            AddEdge("Top", frame, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -1), Vector2.zero, TitleBrass);
        }

        void AddEdge(string name, RectTransform parent, Vector2 min, Vector2 max,
            Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            RectTransform edge = NewRect("__Pixel " + name, parent);
            edge.anchorMin = min;
            edge.anchorMax = max;
            edge.offsetMin = offsetMin;
            edge.offsetMax = offsetMax;
            var image = edge.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        static float ColorDistance(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
        }
    }
}
