using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    [Serializable] public sealed class WorldChunkState
    {
        // Global chunk coordinates. Each map is a local 16 x 16 string.
        public int x, y;
        public int interiorSiteId;
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
                    (FloorWorld.IsInterior(world, px * ChunkSize, py * ChunkSize)
                        ? cx == px && cy == py
                        : Math.Abs(cx - px) <= LoadRadius && Math.Abs(cy - py) <= LoadRadius)) return true;
            return false;
        }

        internal static WorldState Create(WorldGenerationSettings options)
        {
            var world = new WorldState { seed = options.seed, generation = options,
                generatorVersion = WorldGeneration.CurrentGeneratorVersion, primitiveVersion = 1,
                chunks = new ChunkWorldState { enabled = true } };
            for (int cy = 0; cy < 4; cy++) for (int cx = 0; cx < 4; cx++)
                // Opening pickups are placed after finding the actual spawn across all sixteen chunks.
                if (!ChunkTerrain.Generate(world, cx, cy, addGroundPickups: false))
                    throw new InvalidOperationException("Initial terrain generation failed.");
            // Save metadata still includes the bounded 64 by 64 tile strings.
            var ground = new char[world.width * world.height];
            var biomes = new char[ground.Length];
            foreach (var chunk in world.chunks.records)
                for (int y = 0; y < ChunkSize; y++)
                {
                    int target = chunk.x * ChunkSize + (chunk.y * ChunkSize + y) * world.width;
                    chunk.ground.CopyTo(y * ChunkSize, ground, target, ChunkSize);
                    chunk.biomes.CopyTo(y * ChunkSize, biomes, target, ChunkSize);
                }
            world.ground = new string(ground);
            world.biomes = new string(biomes);
            WorldGeneration.FinishNewWorld(world);
            world.nextId = Math.Max(world.nextId, DynamicIdStart);
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
                if (FloorWorld.IsInterior(world, px * ChunkSize, py * ChunkSize)) continue;
                for (int cy = py - LoadRadius; cy <= py + LoadRadius; cy++)
                for (int cx = px - LoadRadius; cx <= px + LoadRadius; cx++)
                {
                    if (!InsideChunk(cx, cy) || world.generatorVersion >= 5 && FloorWorld.IsReservedChunk(cx, cy) ||
                        WorldTiles.TryChunk(world, cx, cy, out _)) continue;
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
