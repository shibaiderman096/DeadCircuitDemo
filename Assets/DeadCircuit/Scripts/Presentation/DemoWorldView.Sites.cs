using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadCircuit
{
    public sealed partial class DemoWorldView
    {
        // All authored decorations stay below players, live walls, doors, crates
        // and later construction. They never acquire colliders or interaction.
        const int SiteFloorOrder = -17999;
        const int SitePropOrder = -17000;
        const int SiteSignOrder = -16999;
        const string SitePropCells = "bsthnmpkglruvodfwea";

        sealed class SiteFloorBatch
        {
            public Mesh mesh;
            public Material material;
            public MeshRenderer renderer;
            public readonly GroundMeshData data = new GroundMeshData();
        }

        readonly Dictionary<Texture2D, SiteFloorBatch> siteFloors = new Dictionary<Texture2D, SiteFloorBatch>();
        readonly List<SpriteRenderer> siteSprites = new List<SpriteRenderer>();
        readonly List<SiteState> visibleSites = new List<SiteState>();
        Transform sitesRoot;
        ulong drawnSiteSignature;
        int siteMinX, siteMinY, siteMaxX, siteMaxY, usedSiteSprites;
        bool sitesDrawn, siteArtPending;
        float siteArtRetryAt;

        static bool SitePlan(SiteState site, out SiteBlueprint plan)
        {
            plan = site == null || site.layoutVersion != 1 ? null : SiteBlueprintCatalog.Get(site.kind);
            return plan != null && site.rotation >= 0 && site.rotation < 4 &&
                site.width == SiteBlueprintCatalog.Width(plan, site.rotation) &&
                site.height == SiteBlueprintCatalog.Height(plan, site.rotation);
        }

        internal string SiteNameAt(WorldState world, int x, int y)
        {
            if (world?.sites == null) return "";
            foreach (var site in world.sites)
            {
                if (!SitePlan(site, out var plan) || x < site.x || y < site.y ||
                    x >= site.x + site.width || y >= site.y + site.height ||
                    SiteBlueprintCatalog.At(plan, site.rotation, x - site.x, y - site.y) == ' ') continue;
                int kind = (int)site.kind;
                return kind >= 0 && kind < WorldGenerationCatalog.Sites.Count ? WorldGenerationCatalog.Sites[kind].name : "";
            }
            return "";
        }

        static ulong SiteHash(ulong hash, int value)
        {
            unchecked { return (hash ^ (uint)value) * 1099511628211UL; }
        }

        static ulong SiteGeometrySignature(SiteState site)
        {
            ulong hash = SiteHash(14695981039346656037UL, (int)site.kind);
            hash = SiteHash(hash, site.x); hash = SiteHash(hash, site.y);
            hash = SiteHash(hash, site.width); hash = SiteHash(hash, site.height);
            hash = SiteHash(hash, site.rotation); hash = SiteHash(hash, site.layoutVersion);
            hash = SiteHash(hash, site.doorX); return SiteHash(hash, site.doorY);
        }

        // Hash metadata without per-frame objects or WorldState reference keys.
        // Equal LAN snapshots, including reordered site lists, keep their meshes.
        ulong SiteWindowSignature(WorldState world)
        {
            visibleSites.Clear();
            ulong sites = 0, chunks = 0;
            foreach (long chunk in wantedGroundChunks)
                unchecked { chunks += SiteHash(SiteHash(14695981039346656037UL, WorldTiles.X(chunk)), WorldTiles.Y(chunk)); }
            if (world.sites != null) foreach (var site in world.sites)
            {
                if (!SitePlan(site, out _) || site.x > siteMaxX + 1 || site.y > siteMaxY + 1 ||
                    site.x + site.width < siteMinX - 1 || site.y + site.height < siteMinY - 1) continue;
                int x0 = WorldTiles.FloorDiv(site.x - 1, ChunkSize);
                int y0 = WorldTiles.FloorDiv(site.y - 1, ChunkSize);
                int x1 = WorldTiles.FloorDiv(site.x + site.width, ChunkSize);
                int y1 = WorldTiles.FloorDiv(site.y + site.height, ChunkSize);
                bool wanted = false;
                for (int cy = y0; cy <= y1 && !wanted; cy++)
                for (int cx = x0; cx <= x1; cx++)
                    if (wantedGroundChunks.Contains(WorldTiles.Key(cx, cy))) { wanted = true; break; }
                if (!wanted) continue;
                visibleSites.Add(site);
                unchecked { sites += SiteGeometrySignature(site); }
            }
            ulong hash = SiteHash(sites ^ chunks, visibleSites.Count);
            hash = SiteHash(hash, siteMinX); hash = SiteHash(hash, siteMinY);
            hash = SiteHash(hash, siteMaxX); return SiteHash(hash, siteMaxY);
        }

        bool SiteCellVisible(int x, int y) => x >= siteMinX && x <= siteMaxX && y >= siteMinY && y <= siteMaxY &&
            wantedGroundChunks.Contains(WorldTiles.Key(WorldTiles.FloorDiv(x, ChunkSize), WorldTiles.FloorDiv(y, ChunkSize)));

        void DrawSites(WorldState world)
        {
            siteMinX = Mathf.CeilToInt(viewMin.x - .5f); siteMinY = Mathf.CeilToInt(viewMin.y - .5f);
            siteMaxX = Mathf.FloorToInt(viewMax.x + .5f); siteMaxY = Mathf.FloorToInt(viewMax.y + .5f);
            ulong signature = SiteWindowSignature(world);
            // Update daylight even while the geometry is cached. SpriteRenderers
            // use the view's shared sprite material, which already follows it.
            foreach (var batch in siteFloors.Values) batch.material.SetColor("_Color", terrainMaterial.GetColor("_Color"));
            if (sitesDrawn && signature == drawnSiteSignature && (!siteArtPending || Time.unscaledTime < siteArtRetryAt)) return;
            drawnSiteSignature = signature; sitesDrawn = true; siteArtPending = false;
            usedSiteSprites = 0;
            foreach (var batch in siteFloors.Values) { batch.data.Clear(); batch.renderer.enabled = false; }
            foreach (var site in visibleSites) DrawSite(site);
            foreach (var batch in siteFloors.Values)
            {
                var data = batch.data;
                batch.mesh.Clear();
                batch.mesh.SetVertices(data.vertices); batch.mesh.SetUVs(0, data.uv);
                batch.mesh.SetTriangles(data.triangles, 0); batch.mesh.SetColors(data.colors); batch.mesh.RecalculateBounds();
                batch.renderer.enabled = data.vertices.Count > 0;
            }
            for (int i = usedSiteSprites; i < siteSprites.Count; i++) siteSprites[i].enabled = false;
            siteArtRetryAt = Time.unscaledTime + 1f;
        }

        void DrawSite(SiteState site)
        {
            if (!SitePlan(site, out var plan)) return;
            Sprite floor = DemoArt.Get(plan.floorArt);
            int minX = Mathf.Max(site.x, siteMinX), minY = Mathf.Max(site.y, siteMinY);
            int maxX = Mathf.Min(site.x + site.width - 1, siteMaxX), maxY = Mathf.Min(site.y + site.height - 1, siteMaxY);
            for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++)
            {
                if (!SiteCellVisible(x, y)) continue;
                char cell = SiteBlueprintCatalog.At(plan, site.rotation, x - site.x, y - site.y);
                // Outdoor paving keeps the authoritative terrain beneath it; it
                // must not become an indoor room floor. Spaces have no footprint.
                if (cell == ' ' || cell == ',') continue;
                if (floor != null) AddSiteFloor(floor, x, y);
                else siteArtPending = true;
                if (SitePropCells.IndexOf(cell) >= 0)
                    AddSiteSprite(DemoArt.Get("landmark_prop_" + cell), x, y, .875f, SitePropOrder, site.rotation);
                // #, + and uppercase crate cells receive only a floor beneath
                // their live entities, never a second wall, door or container.
            }
            DrawSiteSign(site, plan);
        }

        void EnsureSitesRoot()
        {
            if (sitesRoot == null) sitesRoot = NewRoot("Local landmark floors and nonblocking decor", worldRoot);
        }

        void AddSiteFloor(Sprite floor, int x, int y)
        {
            if (!siteFloors.TryGetValue(floor.texture, out var batch))
            {
                EnsureSitesRoot();
                var obj = new GameObject("Landmark floors", typeof(MeshFilter), typeof(MeshRenderer));
                obj.transform.SetParent(sitesRoot, false);
                batch = new SiteFloorBatch
                {
                    mesh = new Mesh { name = "DeadCircuit local landmark floors", indexFormat = IndexFormat.UInt32 },
                    material = new Material(terrainMaterial) { name = "DeadCircuit landmark floor atlas", mainTexture = floor.texture },
                    renderer = obj.GetComponent<MeshRenderer>()
                };
                obj.GetComponent<MeshFilter>().sharedMesh = batch.mesh;
                batch.renderer.sharedMaterial = batch.material;
                batch.renderer.sortingOrder = SiteFloorOrder;
                batch.renderer.shadowCastingMode = ShadowCastingMode.Off; batch.renderer.receiveShadows = false;
                siteFloors.Add(floor.texture, batch);
            }
            var r = floor.rect;
            var uv = new Rect(r.x / floor.texture.width, r.y / floor.texture.height, r.width / floor.texture.width, r.height / floor.texture.height);
            var data = batch.data;
            AddQuad(data.vertices, data.uv, data.triangles, data.colors, x - .5f, y - .5f, 1, 1, uv, Color.white);
        }

        void AddSiteSprite(Sprite sprite, float x, float y, float extent, int order, int rotation)
        {
            if (sprite == null) { siteArtPending = true; return; }
            float span = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            if (span <= 0) return;
            EnsureSitesRoot();
            if (usedSiteSprites == siteSprites.Count)
            {
                var added = NewSprite("Nonblocking landmark sprite", sitesRoot, order);
                added.shadowCastingMode = ShadowCastingMode.Off; added.receiveShadows = false;
                siteSprites.Add(added);
            }
            var renderer = siteSprites[usedSiteSprites++];
            renderer.sprite = sprite; renderer.color = Color.white; renderer.sortingOrder = order;
            renderer.transform.localPosition = new Vector3(x, y, 0);
            renderer.transform.localScale = Vector3.one * (extent / span);
            renderer.transform.localRotation = Quaternion.Euler(0, 0, (rotation & 3) * 90f);
            renderer.enabled = true;
        }

        void DrawSiteSign(SiteState site, SiteBlueprint plan)
        {
            int dx = site.doorX - site.x, dy = site.doorY - site.y;
            if (dx < 0 || dy < 0 || dx >= site.width || dy >= site.height ||
                SiteBlueprintCatalog.At(plan, site.rotation, dx, dy) != '+') return;
            // The catalog's primary exterior entrance is already rotated. The
            // small, upright sign sits outside and beside it, clear of the lane.
            Vector2 outward = dx == 0 ? Vector2.left : dx == site.width - 1 ? Vector2.right :
                dy == 0 ? Vector2.down : dy == site.height - 1 ? Vector2.up : Vector2.zero;
            if (outward == Vector2.zero) return;
            float x = site.doorX + (outward.x - outward.y) * .8125f;
            float y = site.doorY + (outward.y + outward.x) * .8125f;
            if (!SiteCellVisible(Mathf.FloorToInt(x + .5f), Mathf.FloorToInt(y + .5f))) return;
            AddSiteSprite(DemoArt.Get("landmark_sign_" + (int)site.kind), x, y, .5625f, SiteSignOrder, 0);
        }

        void ResetSites() => DisposeSites();

        void DisposeSites()
        {
            if (sitesRoot != null) { sitesRoot.gameObject.SetActive(false); Destroy(sitesRoot.gameObject); }
            foreach (var batch in siteFloors.Values)
            {
                if (batch.mesh != null) Destroy(batch.mesh);
                if (batch.material != null) Destroy(batch.material);
            }
            siteFloors.Clear(); siteSprites.Clear(); visibleSites.Clear();
            sitesRoot = null; usedSiteSprites = 0; drawnSiteSignature = 0;
            sitesDrawn = siteArtPending = false; siteArtRetryAt = 0;
            // DemoArt sprites/textures and spriteMaterial are shared, not owned.
        }
    }
}
