using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadCircuit
{
    public sealed partial class DemoWorldView
    {
        const int GroundChunksPerFrame = 2;
        const int LocalMapSize = 64;

        sealed class GroundChunkVisual
        {
            public Transform root;
            public readonly Mesh[] meshes = new Mesh[3];
            public readonly MeshRenderer[] renderers = new MeshRenderer[3];
        }

        sealed class GroundMeshData
        {
            public readonly List<Vector3> vertices = new List<Vector3>(2048);
            public readonly List<Vector2> uv = new List<Vector2>(2048);
            public readonly List<int> triangles = new List<int>(3072);
            public readonly List<Color32> colors = new List<Color32>(2048);
            public void Clear() { vertices.Clear(); uv.Clear(); triangles.Clear(); colors.Clear(); }
        }

        readonly Dictionary<long, GroundChunkVisual> groundChunks = new Dictionary<long, GroundChunkVisual>();
        readonly HashSet<long> wantedGroundChunks = new HashSet<long>();
        readonly HashSet<long> dirtyGroundChunks = new HashSet<long>();
        readonly Dictionary<long, ulong> groundSignatures = new Dictionary<long, ulong>();
        readonly List<long> removedGroundChunks = new List<long>();
        readonly GroundMeshData groundMesh = new GroundMeshData();
        readonly GroundMeshData biomeMesh = new GroundMeshData();
        readonly GroundMeshData detailMesh = new GroundMeshData();
        WorldState groundWorld;
        int groundRevision = -1, groundGeneratorVersion, groundCenterX, groundCenterY;
        bool groundInitialized, groundChunked;
        string biomeCache;
        readonly byte[] mapVisibility = new byte[LocalMapSize * LocalMapSize];
        int mapOriginX, mapOriginY;
        Color seasonTint = Color.white;

        // Check availability every frame, but hash tile data only when its source
        // changes. Equal LAN snapshots do not allocate or rebuild any meshes.
        void RebuildGround(WorldState world)
        {
            bool chunked = world.chunks != null && world.chunks.enabled;
            if (!groundInitialized || width != world.width || height != world.height ||
                seed != world.seed || groundChunked != chunked)
            {
                ResetGroundStreaming();
                width = world.width; height = world.height; seed = world.seed; groundChunked = chunked;
                groundInitialized = true;
            }
            int revision = chunked ? world.chunks.revision : 0;
            bool changed = !ReferenceEquals(groundWorld, world) || !ReferenceEquals(groundCache, world.ground) ||
                !ReferenceEquals(biomeCache, world.biomes) || groundRevision != revision || groundGeneratorVersion != world.generatorVersion;
            groundWorld = world; groundCache = world.ground; biomeCache = world.biomes;
            groundRevision = revision; groundGeneratorVersion = world.generatorVersion;
            var local = bridge.LocalPlayer;
            float px = local != null ? local.x : WorldCamera.transform.position.x;
            float py = local != null ? local.y : WorldCamera.transform.position.y;
            groundCenterX = WorldTiles.FloorDiv(Mathf.FloorToInt(px + .5f), ChunkSize);
            groundCenterY = WorldTiles.FloorDiv(Mathf.FloorToInt(py + .5f), ChunkSize);
            wantedGroundChunks.Clear();
            // Presentation follows only this client. Remote players may keep core
            // chunks active, but never allocate terrain meshes on this camera.
            for (int cy = groundCenterY - ChunkStreaming.LoadRadius; cy <= groundCenterY + ChunkStreaming.LoadRadius; cy++)
            for (int cx = groundCenterX - ChunkStreaming.LoadRadius; cx <= groundCenterX + ChunkStreaming.LoadRadius; cx++)
            {
                if (!ChunkStreaming.IsGenerated(world, cx * ChunkSize, cy * ChunkSize)) continue;
                long key = WorldTiles.Key(cx, cy);
                wantedGroundChunks.Add(key);
                bool known = groundSignatures.TryGetValue(key, out ulong previous);
                if (changed || !known)
                {
                    ulong signature = GroundSignature(world, cx, cy);
                    if (!known || signature != previous) dirtyGroundChunks.Add(key);
                    groundSignatures[key] = signature;
                }
                if (!groundChunks.ContainsKey(key)) dirtyGroundChunks.Add(key);
            }
            removedGroundChunks.Clear();
            foreach (long key in groundSignatures.Keys)
                if (!wantedGroundChunks.Contains(key)) removedGroundChunks.Add(key);
            foreach (long key in removedGroundChunks)
            {
                if (groundChunks.TryGetValue(key, out var removed))
                {
                    ReleaseGroundChunk(removed);
                    groundChunks.Remove(key);
                }
                groundSignatures.Remove(key); dirtyGroundChunks.Remove(key);
            }
            // Always spend the bounded budget nearest the local player first.
            for (int built = 0; built < GroundChunksPerFrame; built++)
            {
                long best = 0; bool found = false; float nearest = float.MaxValue;
                foreach (long key in dirtyGroundChunks)
                {
                    float dx = WorldTiles.X(key) * ChunkSize + (ChunkSize - 1) * .5f - px;
                    float dy = WorldTiles.Y(key) * ChunkSize + (ChunkSize - 1) * .5f - py;
                    float distance = dx * dx + dy * dy;
                    if (!found || distance < nearest || (distance == nearest && key < best))
                    { nearest = distance; best = key; found = true; }
                }
                if (!found) break;
                BuildGroundChunk(world, best);
                dirtyGroundChunks.Remove(best);
            }
            EnsureMapTexture();
        }

        ulong GroundSignature(WorldState world, int chunkX, int chunkY)
        {
            // Include a one-cell halo: generating a missing neighbour invalidates
            // shore/road seams even when this chunk's own tiles are unchanged.
            int cx = chunkX * ChunkSize, cy = chunkY * ChunkSize;
            unchecked
            {
                ulong hash = (14695981039346656037UL ^ (uint)world.generatorVersion) * 1099511628211UL;
                for (int y = cy - 1; y <= cy + ChunkSize; y++)
                for (int x = cx - 1; x <= cx + ChunkSize; x++)
                {
                    int kind = Ground(x, y);
                    hash = (hash ^ (uint)(kind + 1)) * 1099511628211UL;
                    if (kind >= 0) hash = (hash ^ (uint)BiomeAt(world, x, y)) * 1099511628211UL;
                }
                return hash;
            }
        }

        void BuildGroundChunk(WorldState world, long key)
        {
            int chunkX = WorldTiles.X(key), chunkY = WorldTiles.Y(key);
            if (!groundChunks.TryGetValue(key, out var chunk))
            {
                chunk = new GroundChunkVisual { root = NewRoot("Chunk " + chunkX + "," + chunkY, terrainRoot) };
                groundChunks.Add(key, chunk);
            }
            groundMesh.Clear(); biomeMesh.Clear(); detailMesh.Clear();
            int cx = chunkX * ChunkSize, cy = chunkY * ChunkSize;
            for (int y = cy; y < cy + ChunkSize; y++)
            for (int x = cx; x < cx + ChunkSize; x++)
            {
                int kind = Ground(x, y);
                if (kind < 0) continue; // '?' is absent geometry, never a grass fallback.
                uint h = Hash(x, y, seed);
                float tint = .96f + h % 9 * .01f;
                Color color = (kind <= 1 ? BiomeTint(world, x, y) : Color.white) * new Color(tint, tint, tint, 1);
                AddQuad(groundMesh.vertices, groundMesh.uv, groundMesh.triangles, groundMesh.colors,
                    x - .5f, y - .5f, 1, 1, TerrainUV(kind * 8 + (int)(h % 8)), color);
                if (kind == 0)
                {
                    AddSpriteQuad(biomeMesh.vertices, biomeMesh.uv, biomeMesh.triangles, biomeMesh.colors,
                        DemoArt.Get("terrain_" + BiomeAt(world, x, y)), new Vector2(x, y), 1, new Color(tint, tint, tint, 1));
                    if (world.generatorVersion > 0 && h % 43 == 0)
                        AddSpriteQuad(biomeMesh.vertices, biomeMesh.uv, biomeMesh.triangles, biomeMesh.colors,
                            DemoArt.Get("biome_" + BiomeAt(world, x, y)), new Vector2(x, y), .68f, Color.white);
                }
                for (int side = 0; side < 4; side++)
                {
                    int nx = x + (side == 0 ? -1 : side == 2 ? 1 : 0);
                    int ny = y + (side == 1 ? 1 : side == 3 ? -1 : 0);
                    int other = Ground(nx, ny);
                    bool edge = other < 0 || (other != kind && (kind == 2 || (other != 2 && kind > other)));
                    if (edge) AddQuad(groundMesh.vertices, groundMesh.uv, groundMesh.triangles, groundMesh.colors,
                        x - .5f, y - .5f, 1, 1, TerrainUV(40 + kind * 4 + side), Color.white);
                }
                string prop = kind == 0 && h % 19 == 0 ? "tuft" : (kind == 1 || kind == 4) && h % 41 == 0 ? "rubble" : null;
                if (prop != null) AddSpriteQuad(detailMesh.vertices, detailMesh.uv, detailMesh.triangles, detailMesh.colors,
                    Art(prop), new Vector2(x + ((h >> 8) % 9 - 4f) * .03f, y), .7f, Color.white);
            }
            UpdateGroundMesh(chunk, 0, "Ground", groundMesh, terrainMaterial, -20000);
            if (biomeMesh.vertices.Count > 0 || chunk.meshes[1] != null)
                UpdateGroundMesh(chunk, 1, "SVG biomes", biomeMesh, ExpansionGroundMaterial(), -19999);
            if (detailMesh.vertices.Count > 0 || chunk.meshes[2] != null)
                UpdateGroundMesh(chunk, 2, "Scattered details", detailMesh, SpriteMeshMaterial(), -19000);
        }

        void UpdateGroundMesh(GroundChunkVisual chunk, int layer, string name, GroundMeshData data, Material material, int order)
        {
            Mesh mesh = chunk.meshes[layer];
            if (mesh == null)
            {
                var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                obj.transform.SetParent(chunk.root, false);
                mesh = new Mesh { name = "DC chunk " + chunk.root.name + " / " + name };
                chunk.meshes[layer] = mesh;
                obj.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = obj.GetComponent<MeshRenderer>();
                chunk.renderers[layer] = renderer;
                renderer.sharedMaterial = material; renderer.sortingOrder = order;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            }
            else mesh.Clear();
            mesh.SetVertices(data.vertices); mesh.SetUVs(0, data.uv);
            mesh.SetTriangles(data.triangles, 0); mesh.SetColors(data.colors); mesh.RecalculateBounds();
            chunk.renderers[layer].enabled = data.vertices.Count > 0;
        }

        void ReleaseGroundChunk(GroundChunkVisual chunk)
        {
            if (chunk.root != null) { chunk.root.gameObject.SetActive(false); Destroy(chunk.root.gameObject); }
            foreach (Mesh mesh in chunk.meshes) if (mesh != null) Destroy(mesh);
        }

        void ResetGroundStreaming()
        {
            foreach (GroundChunkVisual chunk in groundChunks.Values) ReleaseGroundChunk(chunk);
            groundChunks.Clear();
            wantedGroundChunks.Clear(); dirtyGroundChunks.Clear(); groundSignatures.Clear(); removedGroundChunks.Clear();
            groundWorld = null; groundCache = biomeCache = null; groundRevision = -1; groundInitialized = false;
            nextMapAt = 0;
        }

        Color UpdateSeasonTint(WorldState world)
        {
            var sample = SeasonCalendar.Sample(world);
            float temperature = (float)sample.temperatureOffset;
            Color target = temperature < 0 ? Color.Lerp(Color.white, new Color(.83f, .92f, 1.04f), Mathf.Clamp01(-temperature / 18f)) :
                Color.Lerp(Color.white, new Color(1.05f, .98f, .88f), Mathf.Clamp01(temperature / 12f));
            seasonTint = Color.Lerp(seasonTint, target, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 1.5f));
            return seasonTint;
        }

        void EnsureMapTexture()
        {
            if (mapTexture != null) return;
            mapTexture = new Texture2D(LocalMapSize, LocalMapSize, TextureFormat.RGBA32, false)
            { name = "DeadCircuit local fog map", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            mapPixels = new Color32[LocalMapSize * LocalMapSize];
            minimapImage.texture = mapTexture;
            nextMapAt = 0;
        }

        void UpdateMap(WorldState world, PlayerState local)
        {
            if (mapTexture == null || mapPixels == null) return;
            int px = local != null ? Mathf.FloorToInt(local.x + .5f) : width / 2;
            int py = local != null ? Mathf.FloorToInt(local.y + .5f) : height / 2;
            mapOriginX = px - LocalMapSize / 2; mapOriginY = py - LocalMapSize / 2;
            int radius = MapExploration.Radius(world);
            for (int iy = 0; iy < LocalMapSize; iy++)
            for (int ix = 0; ix < LocalMapSize; ix++)
            {
                int index = ix + iy * LocalMapSize, x = mapOriginX + ix, y = mapOriginY + iy;
                mapPixels[index] = new Color32(0, 0, 0, 255); mapVisibility[index] = 0;
                int kind = Ground(x, y);
                if (local == null || kind < 0) continue;
                bool visible = Mathf.Abs(x - local.x) <= radius && Mathf.Abs(y - local.y) <= radius &&
                    MapExploration.IsVisible(world, local, x, y);
                if (!visible && !MapExploration.IsExplored(world, local, x, y)) continue;
                mapVisibility[index] = (byte)(visible ? 2 : 1);
                Color32 color = (Color)MapColors[kind] * (kind <= 1 ? BiomeTint(world, x, y) : Color.white);
                mapPixels[index] = visible ? color : DimMapColor(color);
            }
            if (world.nodes != null) foreach (var n in world.nodes)
                if (n.hp > 0 && n.stock > 0) Plot(n.x, n.y, n.kind == NodeKind.Tree ? new Color32(37, 61, 39, 255) :
                    n.kind == NodeKind.Ore ? new Color32(136, 164, 154, 255) : new Color32(154, 113, 70, 255));
            if (world.buildings != null) foreach (var b in world.buildings)
            {
                if (b.hp <= 0) continue;
                // Clip before walking the footprint; only explored cells may show
                // buildings, and stale areas retain the same dark fog treatment.
                int right = Mathf.Min(b.x + DemoCatalog.Width(b), mapOriginX + LocalMapSize);
                int top = Mathf.Min(b.y + DemoCatalog.Height(b), mapOriginY + LocalMapSize);
                if (right <= mapOriginX || top <= mapOriginY || b.x >= mapOriginX + LocalMapSize || b.y >= mapOriginY + LocalMapSize) continue;
                Color32 color = b.networkId > 0 ? NetworkEnergized(b.networkId) ? NetworkColor(b.networkId) : new Color(.54f, .33f, .22f) : Amber;
                for (int y = Mathf.Max(b.y, mapOriginY); y < top; y++)
                for (int x = Mathf.Max(b.x, mapOriginX); x < right; x++) Plot(x, y, color, true);
            }
            if (world.zombies != null) foreach (var z in world.zombies)
                if (z.hp > 0) Plot(Mathf.FloorToInt(z.x + .5f), Mathf.FloorToInt(z.y + .5f), new Color32(237, 97, 67, 255));
            if (world.survival != null && world.survival.enabled)
            {
                foreach (var c in world.survival.creatures) if (c.hp > 0)
                    Plot(Mathf.FloorToInt(c.x + .5f), Mathf.FloorToInt(c.y + .5f), new Color32(176, 177, 117, 255));
                foreach (var plot in world.survival.plots) Plot(plot.x, plot.y, new Color32(109, 175, 101, 255));
            }
            var cp = WorldCamera.transform.position;
            float hy = WorldCamera.orthographicSize, hx = hy * WorldCamera.aspect;
            int left = Mathf.RoundToInt(cp.x - hx), rightEdge = Mathf.RoundToInt(cp.x + hx);
            int bottom = Mathf.RoundToInt(cp.y - hy), topEdge = Mathf.RoundToInt(cp.y + hy);
            var border = new Color32(174, 175, 136, 255);
            for (int x = Mathf.Max(mapOriginX, left); x <= Mathf.Min(mapOriginX + LocalMapSize - 1, rightEdge); x++)
            { Plot(x, bottom, border); Plot(x, topEdge, border); }
            for (int y = Mathf.Max(mapOriginY, bottom); y <= Mathf.Min(mapOriginY + LocalMapSize - 1, topEdge); y++)
            { Plot(left, y, border); Plot(rightEdge, y, border); }
            // Gate the centre as well as every arm. A hidden teammate cannot leak
            // their position through a marker arm touching a visible tile.
            if (world.players != null) foreach (var p in world.players)
            {
                if (!p.connected) continue;
                int x = Mathf.FloorToInt(p.x + .5f), y = Mathf.FloorToInt(p.y + .5f);
                if (!MapCellVisible(x, y)) continue;
                Color32 color = local != null && p.id == local.id ? new Color32(255, 239, 189, 255) : new Color32(106, 220, 234, 255);
                Plot(x, y, color); Plot(x - 1, y, color); Plot(x + 1, y, color); Plot(x, y - 1, color); Plot(x, y + 1, color);
            }
            mapTexture.SetPixels32(mapPixels); mapTexture.Apply(false, false);
        }

        static Color32 DimMapColor(Color32 color) => new Color32((byte)(color.r * .28f), (byte)(color.g * .28f), (byte)(color.b * .28f), 255);

        bool MapCellVisible(int x, int y)
        {
            int ix = x - mapOriginX, iy = y - mapOriginY;
            return ix >= 0 && iy >= 0 && ix < LocalMapSize && iy < LocalMapSize && mapVisibility[ix + iy * LocalMapSize] == 2;
        }

        void Plot(int x, int y, Color32 color, bool remembered = false)
        {
            int ix = x - mapOriginX, iy = y - mapOriginY;
            if (ix < 0 || iy < 0 || ix >= LocalMapSize || iy >= LocalMapSize) return;
            int index = ix + iy * LocalMapSize;
            if (mapVisibility[index] < (remembered ? 1 : 2)) return;
            mapPixels[index] = mapVisibility[index] == 2 ? color : DimMapColor(color);
        }
    }
}
