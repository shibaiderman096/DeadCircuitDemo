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
            public Mesh mesh, shadow;
            public MeshRenderer renderer, shadowRenderer;
            public NaturalBuildingLayout layout;
            public SiteState site;
            public ulong geometry, topology;
            public int stamp;
            public bool closed;
        }

        readonly Dictionary<int, NaturalExterior> naturalExteriors = new Dictionary<int, NaturalExterior>();
        readonly HashSet<long> hiddenNaturalCells = new HashSet<long>();
        readonly HashSet<long> naturalEntrances = new HashSet<long>();
        readonly List<int> oldNaturalExteriors = new List<int>();
        readonly GroundMeshData exteriorData = new GroundMeshData();
        readonly GroundMeshData exteriorShadowData = new GroundMeshData();
        Material exteriorMaterial;
        ulong exteriorVisibilitySignature;

        bool NaturalInteriorHidden(float x, float y) => hiddenNaturalCells.Contains(
            WorldTiles.Key(Mathf.FloorToInt(x + .5f), Mathf.FloorToInt(y + .5f)));

        bool NaturalEntrance(int x, int y) => naturalEntrances.Contains(WorldTiles.Key(x, y));

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
                    ExteriorPatch(exteriorData, facade, x - .5f, y - .5f + floor * NaturalBuildingLayout.FloorRise,
                        1, NaturalBuildingLayout.FloorRise, new Color(shade, shade, shade, 1));
                }
            }
            for (int y = 0; y < layout.height; y++) for (int x = 0; x < layout.width; x++)
            {
                if (!layout.Covers(x, y)) continue;
                float top = y - .5f + layout.Rise;
                ExteriorPatch(exteriorData, roof, x - .5f, top, 1, 1, Color.white);
            }
            // Thin parapets outline the actual irregular shell, never its bbox.
            for (int y = 0; y < layout.height; y++) for (int x = 0; x < layout.width; x++)
            {
                if (!layout.Covers(x, y)) continue;
                float top = y - .5f + layout.Rise;
                if (!layout.Covers(x, y - 1)) ExteriorPatch(exteriorData, cornice, x - .54f, top - .06f, 1.08f, .125f, new Color(.73f, .78f, .78f));
                if (!layout.Covers(x, y + 1)) ExteriorPatch(exteriorData, cornice, x - .54f, top + .94f, 1.08f, .125f, Color.white);
                if (!layout.Covers(x - 1, y)) ExteriorPatch(exteriorData, cornice, x - .54f, top, .09f, 1, Color.white);
                if (!layout.Covers(x + 1, y)) ExteriorPatch(exteriorData, cornice, x + .45f, top, .09f, 1, new Color(.7f, .75f, .76f));
                // Sparse rooftop equipment belongs to the exterior, not the
                // authored interior's furniture or loot locations.
                if (x % 4 == 2 && y % 4 == 2 && layout.Covers(x - 1, y) && layout.Covers(x + 1, y) &&
                    layout.Covers(x, y - 1) && layout.Covers(x, y + 1) && v.site.kind != SiteKind.Greenhouse)
                    ExteriorPatch(exteriorData, DemoArt.Get(layout.floors >= 3 ? "exterior_roof_vent" : "exterior_roof_hatch"),
                        x - .38f, top + .14f, .76f, .76f, Color.white);
            }
            UploadExteriorMesh(v.mesh, exteriorData); UploadExteriorMesh(v.shadow, exteriorShadowData);
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
