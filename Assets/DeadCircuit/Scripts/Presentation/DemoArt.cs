using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadCircuit
{
    /// <summary>SVG-exported runtime atlases. Shared sprites are loaded once, never per UI frame.</summary>
    public static class DemoArt
    {
#pragma warning disable 0649
        [Serializable] sealed class Atlas { public int width, height; public Entry[] sprites; }
        [Serializable] sealed class Entry
        {
            public string name; public int x, y, w, h;
            public float pixelsPerUnit, pivotX, pivotY;
        }
#pragma warning restore 0649
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            foreach (var sprite in sprites.Values) if (sprite != null) UnityEngine.Object.Destroy(sprite);
            sprites.Clear(); loaded = false;
        }

        /// <summary>Inventory art can change independently of worn armor and mounted parts.</summary>
        public static Sprite GetIcon(string key)
        {
            if (!loaded) Load();
            if (key == "gear_19") key = "gear_17";
            Sprite icon;
            if (key != null && (key.StartsWith("gear_", StringComparison.Ordinal) || key.StartsWith("part_", StringComparison.Ordinal)) &&
                sprites.TryGetValue("equipment_icon_" + key, out icon)) return icon;
            return Get(key);
        }

        public static Sprite Get(string key)
        {
            if (!loaded) Load();
            if (key == "gear_19") key = "gear_17";
            Sprite sprite;
            if (key == null) return null;
            // Keep inventory, crafting and world-held art on the same weapon family.
            if ((key == "gear_5" || key == "gear_6" || key == "gear_7" || key == "gear_8" || key == "gear_9") &&
                sprites.TryGetValue("weapon_icon_" + key.Substring(5), out sprite)) return sprite;
            if (sprites.TryGetValue(key, out sprite)) return sprite;
            // Reuse known atlas cells until dedicated primitive and water art is supplied.
            // Names and presentation tints keep resources and kilns distinct.
            string fallback;
            switch (key)
            {
                case "node_8": case "node_9": case "node_10": case "node_11": case "node_12": fallback = "node_1"; break;
                case "node_13": fallback = "node_1"; break;
                case "node_14": fallback = "node_1"; break;
                case "machine_63": fallback = "machine_10"; break;
                case "machine_36": case "machine_37": fallback = "machine_11"; break;
                case "machine_38": fallback = "machine_11"; break;
                case "machine_39": case "machine_40": case "machine_41": fallback = "machine_14"; break;
                case "machine_42": fallback = "machine_34"; break;
                case "machine_43": fallback = "machine_7"; break;
                case "machine_44": case "machine_45": case "machine_46": case "machine_47": fallback = "machine_11"; break;
                case "machine_48": fallback = "machine_14"; break;
                case "machine_49": case "machine_52": fallback = "machine_14"; break;
                case "machine_50": fallback = "machine_5"; break;
                case "machine_51": fallback = "machine_8"; break;
                case "machine_53": case "machine_58": fallback = "machine_34"; break;
                case "machine_54": fallback = "machine_16"; break;
                case "machine_55": fallback = "machine_7"; break;
                case "machine_56": fallback = "machine_18"; break;
                case "machine_57": fallback = "machine_22"; break;
                case "machine_59": fallback = "machine_4"; break;
                case "machine_60": fallback = "machine_20"; break;
                case "item_25": fallback = "item_1"; break;
                case "item_26": fallback = "item_7"; break;
                case "item_27": fallback = "item_4"; break;
                case "item_28": fallback = "item_3"; break;
                case "item_29": fallback = "item_2"; break;
                case "item_30": case "item_31": fallback = "item_3"; break;
                case "item_32": case "item_35": fallback = "item_4"; break;
                case "item_33": case "item_34": fallback = "item_1"; break;
                case "item_36": fallback = "item_3"; break;
                case "item_37": case "item_38": fallback = "item_2"; break;
                case "item_39": fallback = "item_3"; break;
                case "item_40": case "item_42": case "item_43": fallback = "item_8"; break;
                case "item_41": fallback = "item_2"; break;
                case "item_44": case "item_45": fallback = "item_11"; break;
                case "item_46": case "item_47": case "item_49":
                case "item_50": case "item_51": fallback = "item_4"; break;
                case "item_48": fallback = "item_12"; break;
                case "item_52": case "item_53": fallback = "item_16"; break;
                case "item_54": fallback = "item_1"; break;
                case "item_55": fallback = "item_10"; break;
                case "item_56": fallback = "item_20"; break;
                case "item_57": fallback = "item_2"; break;
                case "item_58": fallback = "item_11"; break;
                case "item_59": fallback = "item_3"; break;
                case "item_60": fallback = "item_13"; break;
                case "item_61": fallback = "item_9"; break;
                // Uranium concentrate reuses the existing uranium ore cell until dedicated art is supplied.
                case "item_62": fallback = "item_14"; break;
                case "item_63": fallback = "deco_6"; break;
                case "item_64": fallback = "node_1"; break;
                case "item_65": fallback = "crop_3_1"; break;
                default: return null;
            }
            return sprites.TryGetValue(fallback, out sprite) ? sprite : null;
        }

        static void Load()
        {
            loaded = true;
            foreach (var source in Resources.LoadAll<TextAsset>("Art/Expansion"))
            {
                Atlas atlas;
                try { atlas = JsonUtility.FromJson<Atlas>(source.text); }
                catch (ArgumentException) { continue; }
                if (atlas == null || atlas.sprites == null) continue;
                var texture = Resources.Load<Texture2D>("Art/Expansion/" + source.name);
                if (texture == null || texture.width != atlas.width || texture.height != atlas.height)
                { Debug.LogWarning("DeadCircuit atlas dimensions do not match: " + source.name); continue; }
                texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Clamp;
                foreach (var e in atlas.sprites)
                {
                    if (e == null || string.IsNullOrEmpty(e.name) || sprites.ContainsKey(e.name) || e.w <= 0 || e.h <= 0 ||
                        e.x < 0 || e.y < 0 || e.x + e.w > texture.width || e.y + e.h > texture.height) continue;
                    // Legacy expansion atlases omit PPU/pivot and retain their centred origin.
                    bool registered = e.pixelsPerUnit > 0 && !float.IsNaN(e.pixelsPerUnit) && !float.IsInfinity(e.pixelsPerUnit);
                    var pivot = registered ? new Vector2(Mathf.Clamp01(e.pivotX), Mathf.Clamp01(e.pivotY)) : new Vector2(.5f, .5f);
                    var sprite = Sprite.Create(texture, new Rect(e.x, texture.height - e.y - e.h, e.w, e.h),
                        pivot, registered ? e.pixelsPerUnit : 32, 0, SpriteMeshType.FullRect,
                        e.name.StartsWith("ui_",StringComparison.Ordinal) ? new Vector4(4,4,4,4) : Vector4.zero);
                    sprite.name = e.name; sprite.hideFlags = HideFlags.DontSave;
                    sprites.Add(e.name, sprite);
                }
            }
        }
    }
}
