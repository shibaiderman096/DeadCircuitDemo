using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadCircuit
{
    public sealed partial class DemoWorldView
    {
        sealed class NaturalExterior
        {
            public Transform root;
            public Mesh mesh, shadow, entrances;
            public MeshRenderer renderer, shadowRenderer, entranceRenderer;
            public NaturalBuildingLayout layout;
            public SiteState site;
            public ulong geometry, topology;
            public ulong entranceSignature;
            public int stamp;
            public bool closed;
        }

        readonly Dictionary<int, NaturalExterior> naturalExteriors = new Dictionary<int, NaturalExterior>();
        readonly HashSet<long> hiddenNaturalCells = new HashSet<long>();
        readonly HashSet<long> naturalEntrances = new HashSet<long>();
        readonly List<int> oldNaturalExteriors = new List<int>();
        readonly GroundMeshData exteriorData = new GroundMeshData();
        readonly GroundMeshData exteriorShadowData = new GroundMeshData();
        readonly GroundMeshData exteriorEntranceData = new GroundMeshData();
        readonly List<Rect> exteriorFragments = new List<Rect>();
        Material exteriorMaterial;
        ulong exteriorVisibilitySignature;

        bool NaturalInteriorHidden(float x, float y) => hiddenNaturalCells.Contains(
            WorldTiles.Key(Mathf.FloorToInt(x + .5f), Mathf.FloorToInt(y + .5f)));

        bool NaturalEntrance(int x, int y) => naturalEntrances.Contains(WorldTiles.Key(x, y));

        int NaturalDoorOrder(int x, int y, int normalOrder)
        {
            foreach (var v in naturalExteriors.Values)
                if (v.closed) normalOrder = v.layout.EntranceOrder(x - v.site.x, y - v.site.y,
                    normalOrder, v.renderer.sortingOrder);
            return normalOrder;
        }

        bool NaturalExteriorOccludes(float x, float y)
        {
            foreach (var v in naturalExteriors.Values)
                if (v.closed && v.layout.Occludes(x - v.site.x, y - v.site.y)) return true;
            return false;
        }

        int NaturalSignOrder(SiteState site) => naturalExteriors.TryGetValue(site.id, out var v) && v.closed ?
            v.renderer.sortingOrder + 8 : SiteSignOrder;

        void DrawNaturalExteriors(WorldState world, PlayerState local)
        {
            EnsureRoomLayout(world);
            hiddenNaturalCells.Clear(); naturalEntrances.Clear();
            exteriorVisibilitySignature = 0;
            Sprite roof = DemoArt.Get("exterior_roof_flat");
            if (roof == null) return; // Retry after the shared art is available.
            if (exteriorMaterial == null)
                exteriorMaterial = new Material(terrainMaterial) { name = "DeadCircuit natural building exteriors", mainTexture = roof.texture };
            exteriorMaterial.SetColor("_Color", terrainMaterial.GetColor("_Color"));
            bool floors = FloorWorld.Enabled(world);
            if (world.sites != null) foreach (var site in world.sites)
            {
                if (site == null || site.parentSiteId != 0 || site.level != 0 || site.x - .6f > viewMax.x ||
                    site.x + site.width + .6f < viewMin.x || site.y - .6f > viewMax.y ||
                    site.y + site.height + NaturalBuildingLayout.MaxRiseCells < viewMin.y || !ExteriorGroundWanted(site)) continue;
                ulong signature = SiteHash(SiteGeometrySignature(site), floors ? 1 : 0);
                if (!naturalExteriors.TryGetValue(site.id, out var v) || v.geometry != signature)
                {
                    var layout = NaturalBuildingLayout.Create(site, floors);
                    if (layout == null) continue;
                    if (v != null) ReleaseNaturalExterior(v);
                    v = new NaturalExterior { site = site, layout = layout, geometry = signature };
                    v.root = NewRoot("Exterior / " + site.kind + " / " + site.id, worldRoot);
                    v.root.localPosition = new Vector3(site.x, site.y, 0);
                    v.mesh = NewExteriorMesh(v.root, "Roof and storeys", out v.renderer);
                    v.shadow = NewExteriorMesh(v.root, "Building shadow", out v.shadowRenderer);
                    v.entrances = NewExteriorMesh(v.root, "Entrance thresholds and approach", out v.entranceRenderer);
                    naturalExteriors[site.id] = v;
                }
                v.site = site; v.stamp = stamp;
                ulong topology = NaturalBoundarySignature(site, v.layout);
                if (v.topology != topology)
                {
                    BuildNaturalExterior(v);
                    v.topology = topology;
                }
                // Only this client's physical position opens a cutaway. Remote
                // actors, aiming and opening a door never expose the whole plan.
                bool inside = local != null && v.layout.Contains(local.x - site.x, local.y - site.y);
                v.closed = !inside;
                v.root.gameObject.SetActive(v.closed);
                v.renderer.sortingOrder = Order(site.y + v.layout.minY - .5f) + 24;
                v.entranceRenderer.sortingOrder = v.renderer.sortingOrder + 2;
                v.shadowRenderer.sortingOrder = SitePropOrder - 1;
                unchecked { exteriorVisibilitySignature += SiteHash(signature, v.closed ? 1 : 0); }
                if (!v.closed) continue;
                for (int y = 0; y < v.layout.height; y++) for (int x = 0; x < v.layout.width; x++)
                {
                    long cell = WorldTiles.Key(site.x + x, site.y + y);
                    if (v.layout.Entrance(x, y)) naturalEntrances.Add(cell);
                    else if (v.layout.Covers(x, y)) hiddenNaturalCells.Add(cell);
                }
            }
            oldNaturalExteriors.Clear();
            foreach (var pair in naturalExteriors) if (pair.Value.stamp != stamp) oldNaturalExteriors.Add(pair.Key);
            foreach (int id in oldNaturalExteriors) { ReleaseNaturalExterior(naturalExteriors[id]); naturalExteriors.Remove(id); }
        }

        // Draw after entity occupancy is refreshed. These are read-only guides
        // anchored to genuine exterior doors, never new paths or colliders.
        void DrawNaturalEntranceGuides(WorldState world)
        {
            Sprite frame = DemoArt.Get("exterior_entry_frame"), paving = DemoArt.Get("exterior_entry_paving");
            if (frame == null || paving == null) return;
            foreach (var v in naturalExteriors.Values)
            {
                if (!v.closed) continue;
                ulong signature = 14695981039346656037UL;
                exteriorEntranceData.Clear();
                var layout = v.layout;
                for (int y = 0; y < layout.height; y++) for (int x = 0; x < layout.width; x++)
                {
                    if (!layout.EntranceOutward(x, y, out int dx, out int dy)) continue;
                    int rotation = dx > 0 ? 1 : dy > 0 ? 2 : dx < 0 ? 3 : 0;
                    // The floor inset keeps an open door from showing a roof
                    // texture through its transparent centre. The real door is
                    // still drawn above it, so its actual state stays legible.
                    EntrancePatch(exteriorEntranceData, paving, x, y, 1, rotation);
                    EntrancePatch(exteriorEntranceData, frame, x, y, 1.25f, dx == 0 ? 0 : 1);
                    for (int step = 1; step <= 2; step++)
                    {
                        int px = x + dx * step, py = y + dy * step;
                        int gx = v.site.x + px, gy = v.site.y + py;
                        char ground = WorldTiles.Ground(world, gx, gy);
                        long key = WorldTiles.Key(gx, gy);
                        bool clear = ground != '?' && ground != '2' && !layout.Covers(px, py) &&
                            !blockedNodes.Contains(key) && !solidBuildings.Contains(key);
                        signature = SiteHash(signature, clear ? 1 : 0);
                        if (!clear) break; // Never paint a path across an obstacle or unknown terrain.
                        EntrancePatch(exteriorEntranceData, paving, px, py, .875f, rotation);
                    }
                }
                if (v.entranceSignature == signature) continue;
                UploadExteriorMesh(v.entrances, exteriorEntranceData);
                v.entranceSignature = signature;
            }
        }

        static void EntrancePatch(GroundMeshData data, Sprite sprite, float x, float y, float extent, int rotation)
        {
            int start = data.vertices.Count;
            ExteriorPatch(data, sprite, x - extent * .5f, y - extent * .5f, extent, extent, Color.white);
            for (int i = start; i < data.vertices.Count; i++)
            {
                Vector3 p = data.vertices[i] - new Vector3(x, y, 0);
                for (int turn = 0; turn < rotation; turn++) p = new Vector3(-p.y, p.x, 0);
                data.vertices[i] = p + new Vector3(x, y, 0);
            }
        }

        bool ExteriorGroundWanted(SiteState site)
        {
            int x0 = WorldTiles.FloorDiv(site.x - 1, ChunkSize), x1 = WorldTiles.FloorDiv(site.x + site.width, ChunkSize);
            int y0 = WorldTiles.FloorDiv(site.y - 1, ChunkSize), y1 = WorldTiles.FloorDiv(site.y + site.height + NaturalBuildingLayout.MaxRiseCells, ChunkSize);
            for (int cy = y0; cy <= y1; cy++) for (int cx = x0; cx <= x1; cx++)
                if (wantedGroundChunks.Contains(WorldTiles.Key(cx, cy))) return true;
            return false;
        }

        ulong NaturalBoundarySignature(SiteState site, NaturalBuildingLayout layout)
        {
            ulong hash = 14695981039346656037UL;
            for (int y = 0; y < layout.height; y++) for (int x = 0; x < layout.width; x++)
            {
                char cell = layout.Cell(x, y);
                if (!layout.Covers(x, y) || (cell != '#' && cell != '+')) continue;
                bool present = HasRoomBoundary(site.x + x, site.y + y);
                hash = SiteHash(hash, present ? 1 : 0);
            }
            return hash;
        }

        Mesh NewExteriorMesh(Transform parent, string name, out MeshRenderer renderer)
        {
            var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(parent, false);
            var mesh = new Mesh { name = "DeadCircuit " + name, indexFormat = IndexFormat.UInt32 };
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            renderer = obj.GetComponent<MeshRenderer>(); renderer.sharedMaterial = exteriorMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return mesh;
        }

        void BuildNaturalExterior(NaturalExterior v)
        {
            exteriorData.Clear(); exteriorShadowData.Clear();
            var layout = v.layout;
            Sprite roof = DemoArt.Get(layout.roofArt), facade = DemoArt.Get(layout.facadeArt);
            Sprite cornice = DemoArt.Get("exterior_cornice");
            if (roof == null || facade == null || cornice == null) return;
            // The facade fills the vertical projection below each exposed south
            // wall. Repeating storeys (rather than stretching a wall) show height.
            for (int y = 0; y < layout.height; y++) for (int x = 0; x < layout.width; x++)
            {
                if (!layout.Covers(x, y)) continue;
                ExteriorPatch(exteriorShadowData, roof, x - .15f, y - .75f, 1.1f, 1.1f, new Color(0, 0, 0, .20f));
            }
            for (int y = 0; y < layout.height; y++) for (int x = 0; x < layout.width; x++)
            {
                if (!layout.SouthFacade(x, y, HasRoomBoundary(v.site.x + x, v.site.y + y))) continue;
                bool opening = layout.Entrance(x, y);
                for (int floor = 0; floor < layout.floors; floor++)
                {
                    if (floor == 0 && opening) continue;
                    float shade = .78f + .055f * floor;
                    ShellPatch(facade, x - .5f, y - .5f + floor * NaturalBuildingLayout.FloorRise,
                        1, NaturalBuildingLayout.FloorRise, new Color(shade, shade, shade, 1));
                }
            }
            for (int y = 0; y < layout.height; y++) for (int x = 0; x < layout.width; x++)
            {
                if (!layout.Covers(x, y)) continue;
                float top = y - .5f + layout.Rise;
                ShellPatch(roof, x - .5f, top, 1, 1, Color.white);
            }
            // Thin parapets outline the actual irregular shell, never its bbox.
            for (int y = 0; y < layout.height; y++) for (int x = 0; x < layout.width; x++)
            {
                if (!layout.Covers(x, y)) continue;
                float top = y - .5f + layout.Rise;
                if (!layout.Covers(x, y - 1)) ShellPatch(cornice, x - .54f, top - .06f, 1.08f, .125f, new Color(.73f, .78f, .78f));
                if (!layout.Covers(x, y + 1)) ShellPatch(cornice, x - .54f, top + .94f, 1.08f, .125f, Color.white);
                if (!layout.Covers(x - 1, y)) ShellPatch(cornice, x - .54f, top, .09f, 1, Color.white);
                if (!layout.Covers(x + 1, y)) ShellPatch(cornice, x + .45f, top, .09f, 1, new Color(.7f, .75f, .76f));
                // Sparse rooftop equipment belongs to the exterior, not the
                // authored interior's furniture or loot locations.
                if (x % 4 == 2 && y % 4 == 2 && layout.Covers(x - 1, y) && layout.Covers(x + 1, y) &&
                    layout.Covers(x, y - 1) && layout.Covers(x, y + 1) && v.site.kind != SiteKind.Greenhouse)
                    ShellPatch(DemoArt.Get(layout.floors >= 3 ? "exterior_roof_vent" : "exterior_roof_hatch"),
                        x - .38f, top + .14f, .76f, .76f, Color.white);
            }
            UploadExteriorMesh(v.mesh, exteriorData); UploadExteriorMesh(v.shadow, exteriorShadowData);

            void ShellPatch(Sprite sprite, float x, float y, float w, float h, Color color)
            {
                if (sprite == null) return;
                // A narrow cutaway through the screen projection exposes the
                // actual ground-level threshold/approach, rather than painting
                // a misleading path on the roof of a north-facing entrance.
                exteriorFragments.Clear(); exteriorFragments.Add(new Rect(x, y, w, h));
                foreach (var opening in layout.EntryOpenings)
                {
                    int count = exteriorFragments.Count;
                    for (int i = count - 1; i >= 0; i--)
                    {
                        Rect part = exteriorFragments[i];
                        float left = Mathf.Max(part.xMin, opening.minX), right = Mathf.Min(part.xMax, opening.maxX);
                        float bottom = Mathf.Max(part.yMin, opening.minY), top = Mathf.Min(part.yMax, opening.maxY);
                        if (left >= right || bottom >= top) continue;
                        exteriorFragments.RemoveAt(i);
                        AddPart(part.xMin, part.yMin, left, part.yMax);
                        AddPart(right, part.yMin, part.xMax, part.yMax);
                        AddPart(left, part.yMin, right, bottom);
                        AddPart(left, top, right, part.yMax);
                    }
                }
                Rect r = sprite.rect;
                foreach (Rect part in exteriorFragments)
                {
                    var uv = new Rect((r.x + (part.x - x) / w * r.width) / sprite.texture.width,
                        (r.y + (part.y - y) / h * r.height) / sprite.texture.height,
                        part.width / w * r.width / sprite.texture.width, part.height / h * r.height / sprite.texture.height);
                    AddQuad(exteriorData.vertices, exteriorData.uv, exteriorData.triangles, exteriorData.colors,
                        part.x, part.y, part.width, part.height, uv, color);
                }
            }
            void AddPart(float left, float bottom, float right, float top)
            {
                if (right > left && top > bottom) exteriorFragments.Add(Rect.MinMaxRect(left, bottom, right, top));
            }
        }

        static void ExteriorPatch(GroundMeshData data, Sprite sprite, float x, float y, float w, float h, Color color)
        {
            if (sprite == null) return;
            Rect r = sprite.rect;
            var uv = new Rect(r.x / sprite.texture.width, r.y / sprite.texture.height, r.width / sprite.texture.width, r.height / sprite.texture.height);
            AddQuad(data.vertices, data.uv, data.triangles, data.colors, x, y, w, h, uv, color);
        }

        static void UploadExteriorMesh(Mesh mesh, GroundMeshData data)
        {
            mesh.Clear(); mesh.SetVertices(data.vertices); mesh.SetUVs(0, data.uv);
            mesh.SetTriangles(data.triangles, 0); mesh.SetColors(data.colors); mesh.RecalculateBounds();
        }

        void ReleaseNaturalExterior(NaturalExterior v)
        {
            if (v.root != null) { v.root.gameObject.SetActive(false); Destroy(v.root.gameObject); }
            if (v.mesh != null) Destroy(v.mesh);
            if (v.shadow != null) Destroy(v.shadow);
            if (v.entrances != null) Destroy(v.entrances);
        }

        void ResetNaturalExteriors()
        {
            foreach (var v in naturalExteriors.Values) ReleaseNaturalExterior(v);
            naturalExteriors.Clear(); hiddenNaturalCells.Clear(); naturalEntrances.Clear();
            exteriorVisibilitySignature = 0;
        }

        void DisposeNaturalExteriors()
        {
            ResetNaturalExteriors();
            if (exteriorMaterial != null) Destroy(exteriorMaterial);
            exteriorMaterial = null;
        }
    }
}
