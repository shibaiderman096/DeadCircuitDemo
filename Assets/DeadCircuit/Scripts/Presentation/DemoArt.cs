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
        [Serializable] sealed class Entry { public string name; public int x, y, w, h; }
#pragma warning restore 0649
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            foreach (var sprite in sprites.Values) if (sprite != null) UnityEngine.Object.Destroy(sprite);
            sprites.Clear(); loaded = false;
        }

        public static Sprite Get(string key)
        {
            if (!loaded) Load();
            Sprite sprite;
            if (key == null) return null;
            if (sprites.TryGetValue(key, out sprite)) return sprite;
            // Reuse known atlas cells until dedicated primitive art is supplied.
            // Names and presentation tints keep resources and kilns distinct.
            string fallback;
            switch (key)
            {
                case "node_8": case "node_9": fallback = "node_1"; break;
                case "machine_36": case "machine_37": fallback = "machine_11"; break;
                case "item_25": fallback = "item_1"; break;
                case "item_26": fallback = "item_7"; break;
                case "item_27": fallback = "item_4"; break;
                case "item_28": fallback = "item_3"; break;
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
                    var sprite = Sprite.Create(texture, new Rect(e.x, texture.height - e.y - e.h, e.w, e.h),
                        new Vector2(.5f, .5f), 32, 0, SpriteMeshType.FullRect,
                        e.name.StartsWith("ui_",StringComparison.Ordinal) ? new Vector4(4,4,4,4) : Vector4.zero);
                    sprite.name = e.name; sprite.hideFlags = HideFlags.DontSave;
                    sprites.Add(e.name, sprite);
                }
            }
        }
    }
}
