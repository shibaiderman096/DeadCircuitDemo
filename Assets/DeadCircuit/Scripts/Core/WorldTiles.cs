using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public static class WorldTiles
    {
        // Half-open bounds align both edges to complete 16-cell chunks.
        public const int CoordinateLimit = 1048576;
        public static long Key(int x, int y) => unchecked(((long)x << 32) | (uint)y);
        public static int X(long key) => unchecked((int)(key >> 32));
        public static int Y(long key) => unchecked((int)key);

        public static int FloorDiv(int value, int divisor)
        {
            if (divisor <= 0) throw new ArgumentOutOfRangeException(nameof(divisor));
            int quotient = value / divisor;
            return value % divisor < 0 ? quotient - 1 : quotient;
        }

        public static bool Inside(WorldState world, int x, int y)
        {
            if (world == null) return false;
            if (ChunkStreaming.Enabled(world))
                return x >= -CoordinateLimit && x < CoordinateLimit && y >= -CoordinateLimit && y < CoordinateLimit;
            return world.width > 0 && world.width <= 64 && world.height > 0 && world.height <= 64 &&
                x >= 0 && x < world.width && y >= 0 && y < world.height;
        }

        public static char Ground(WorldState world, int x, int y) => Tile(world, x, y, false);
        public static char Biome(WorldState world, int x, int y) => Tile(world, x, y, true);

        // Build once after deserialization/list replacement; later appends only
        // index their new records. Per-cell queries never scan the world.
        public static bool TryChunk(WorldState world, int chunkX, int chunkY, out WorldChunkState chunk)
        {
            chunk = null;
            if (!ChunkStreaming.Enabled(world) || !ChunkStreaming.InsideChunk(chunkX, chunkY)) return false;
            var state = world.chunks;
            var records = state.records;
            if (records == null) return false;
            if (state.index == null || !ReferenceEquals(state.indexSource, records) || state.indexedCount > records.Count)
            {
                state.index = new Dictionary<long, WorldChunkState>();
                state.indexSource = records;
                state.indexedCount = 0;
            }
            while (state.indexedCount < records.Count)
            {
                var record = records[state.indexedCount++];
                if (record != null) state.index[Key(record.x, record.y)] = record;
            }
            return state.index.TryGetValue(Key(chunkX, chunkY), out chunk);
        }

        private static char Tile(WorldState world, int x, int y, bool biome)
        {
            if (!Inside(world, x, y)) return '?';
            string tiles; int cell;
            if (ChunkStreaming.Enabled(world))
            {
                int cx = FloorDiv(x, ChunkStreaming.ChunkSize), cy = FloorDiv(y, ChunkStreaming.ChunkSize);
                if (!TryChunk(world, cx, cy, out var chunk)) return '?';
                tiles = biome ? chunk.biomes : chunk.ground;
                cell = x - cx * ChunkStreaming.ChunkSize + (y - cy * ChunkStreaming.ChunkSize) * ChunkStreaming.ChunkSize;
            }
            else { tiles = biome ? world.biomes : world.ground; cell = x + y * world.width; }
            return tiles != null && cell < tiles.Length ? tiles[cell] : '?';
        }
    }
}
