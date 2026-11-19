using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    [Serializable] public sealed class WorldChunkState
    {
        // Global chunk coordinates. Each map is a local 16 x 16 string.
        public int x, y;
        public string ground = "", biomes = "";
    }

    [Serializable] public sealed class ChunkWorldState
    {
        public bool enabled;
        public int revision;
        // Field compatibility only: sparse worlds do not use a generated mask.
        public string generated = "";
        public List<WorldChunkState> records = new List<WorldChunkState>();
        [NonSerialized] internal Dictionary<long, WorldChunkState> index;
        [NonSerialized] internal List<WorldChunkState> indexSource;
        [NonSerialized] internal int indexedCount;
        [NonSerialized] internal bool budgetBlocked;
        [NonSerialized] internal int blockedNodes, blockedBuildings, blockedSites, blockedRecords, blockedNextId, blockedRevision;
    }

    // Records and entities remain authoritative after leaving the active radius.
    // Unloading presentation never replenishes the chunk's resources.
    public static class ChunkStreaming
    {
        public const int ChunkSize = 16, LoadRadius = 3, MaxRecords = 65536;
        public const int DynamicIdStart = 10000000;
        public static bool Enabled(WorldState world) => world != null && world.chunks != null && world.chunks.enabled;

        public static bool IsGenerated(WorldState world, int x, int y)
        {
            if (!WorldTiles.Inside(world, x, y)) return false;
            if (!Enabled(world)) return true;
            return WorldTiles.TryChunk(world, WorldTiles.FloorDiv(x, ChunkSize), WorldTiles.FloorDiv(y, ChunkSize), out _);
        }

        public static bool IsActive(WorldState world, int x, int y)
        {
            if (!IsGenerated(world, x, y)) return false;
            if (!Enabled(world)) return true;
            int cx = WorldTiles.FloorDiv(x, ChunkSize), cy = WorldTiles.FloorDiv(y, ChunkSize);
            foreach (var player in world.players)
                if (PlayerChunk(world, player, out int px, out int py) &&
                    Math.Abs(cx - px) <= LoadRadius && Math.Abs(cy - py) <= LoadRadius) return true;
            return false;
        }

        internal static WorldState Create(WorldGenerationSettings options)
        {
            var initialOptions = WorldGeneration.CloneSettings(options);
            initialOptions.chunked = false;
            WorldState world = WorldGeneration.Generate(initialOptions);
            // Preserve the tutorial, all entities and the 4096-cell legacy maps.
            world.chunks = new ChunkWorldState { enabled = true };
            for (int cy = 0; cy < 4; cy++) for (int cx = 0; cx < 4; cx++)
            {
                var ground = new char[ChunkSize * ChunkSize];
                var biomes = new char[ground.Length];
                for (int y = 0; y < ChunkSize; y++) for (int x = 0; x < ChunkSize; x++)
                {
                    int source = cx * ChunkSize + x + (cy * ChunkSize + y) * 64;
                    ground[x + y * ChunkSize] = world.ground[source];
                    biomes[x + y * ChunkSize] = world.biomes[source];
                }
                world.chunks.records.Add(new WorldChunkState { x = cx, y = cy, ground = new string(ground), biomes = new string(biomes) });
            }
            world.generation = WorldGeneration.CloneSettings(options);
            world.generation.chunked = true;
            world.version = WorldSimulator.SaveVersion;
            world.generatorVersion = WorldGeneration.CurrentGeneratorVersion;
            world.nextId = Math.Max(world.nextId, DynamicIdStart);
            world.announcement = "区块世界：出生点(32,35)，工作台(29,35)、石炉(28,37)、水车岸位(25,25)。四向探索会逐块生成地形。";
            return world;
        }

        // Inspect every connected living player's radius before generating at
        // most one nearest missing chunk. Ties do not depend on player order.
        public static bool Step(WorldState world)
        {
            if (!Enabled(world)) return false;
            var state = world.chunks;
            // Reconsider only when a budget changes. A refused staged chunk is
            // not regenerated at 20 Hz while a player waits at the frontier.
            if (state.budgetBlocked && state.blockedNodes == world.nodes.Count &&
                state.blockedBuildings == world.buildings.Count && state.blockedSites == world.sites.Count &&
                state.blockedRecords == state.records.Count && state.blockedNextId == world.nextId &&
                state.blockedRevision == state.revision) return false;
            state.budgetBlocked = false;
            bool found = false;
            int bestX = 0, bestY = 0, distance = int.MaxValue;
            foreach (var player in world.players)
            {
                if (!PlayerChunk(world, player, out int px, out int py)) continue;
                for (int cy = py - LoadRadius; cy <= py + LoadRadius; cy++)
                for (int cx = px - LoadRadius; cx <= px + LoadRadius; cx++)
                {
                    if (!InsideChunk(cx, cy) || WorldTiles.TryChunk(world, cx, cy, out _)) continue;
                    int dx = cx - px, dy = cy - py, d = dx * dx + dy * dy;
                    if (!found || d < distance || (d == distance && (cy < bestY || (cy == bestY && cx < bestX))))
                    { found = true; bestX = cx; bestY = cy; distance = d; }
                }
            }
            if (!found) return false;
            if (GenerateChunk(world, bestX, bestY)) return true;
            state.budgetBlocked = true;
            state.blockedNodes = world.nodes.Count; state.blockedBuildings = world.buildings.Count;
            state.blockedSites = world.sites.Count; state.blockedRecords = state.records.Count;
            state.blockedNextId = world.nextId; state.blockedRevision = state.revision;
            world.announcement = "区块生成已达当前存档或实体预算，已生成区域与进度会保留。";
            world.announcementUntil = world.elapsed + 12f;
            return false;
        }

        public static bool GenerateChunk(WorldState world, int chunkX, int chunkY) => ChunkTerrain.Generate(world, chunkX, chunkY);

        internal static bool InsideChunk(int x, int y) =>
            x >= -WorldTiles.CoordinateLimit / ChunkSize && x < WorldTiles.CoordinateLimit / ChunkSize &&
            y >= -WorldTiles.CoordinateLimit / ChunkSize && y < WorldTiles.CoordinateLimit / ChunkSize;

        private static bool PlayerChunk(WorldState world, PlayerState player, out int x, out int y)
        {
            x = y = 0;
            if (player == null || !player.connected || !(player.hp > 0) ||
                float.IsNaN(player.x) || float.IsNaN(player.y) || float.IsInfinity(player.x) || float.IsInfinity(player.y)) return false;
            double cellX = Math.Floor((double)player.x + .5), cellY = Math.Floor((double)player.y + .5);
            if (cellX < -WorldTiles.CoordinateLimit || cellX >= WorldTiles.CoordinateLimit ||
                cellY < -WorldTiles.CoordinateLimit || cellY >= WorldTiles.CoordinateLimit ||
                !WorldTiles.Inside(world, (int)cellX, (int)cellY)) return false;
            x = WorldTiles.FloorDiv((int)cellX, ChunkSize);
            y = WorldTiles.FloorDiv((int)cellY, ChunkSize);
            return true;
        }
    }
}
