using System;

namespace DeadCircuit
{
    public static partial class ChunkTerrain
    {
        // A biome spans many storage chunks. Keep local soil detail and river
        // width independent from the much broader climate/landform regions.
        private const int BiomeScale = 6, RiverScale = 4;

        private struct Climate
        {
            public double elevation, moisture, temperature, river, woodland;
        }

        // All fields share absolute world coordinates. The finite starting map
        // and later chunks therefore meet on the same climate and river network.
        private static Climate ClimateAt(int seed, int x, int y)
        {
            // Broad domain warping breaks axis-aligned climate contours without
            // introducing tile or chunk randomness into biome boundaries.
            double wx = x + (Noise(seed, x, y, 96 * BiomeScale, 0x8b01u) - .5) * (18 * BiomeScale);
            double wy = y + (Noise(seed, x, y, 96 * BiomeScale, 0x8b02u) - .5) * (18 * BiomeScale);
            double elevation = Octaves(seed, wx, wy, 40 * BiomeScale, 0x1049u) * .7 +
                Noise(seed, wx, wy, 80 * BiomeScale, 0x82afu) * .3;
            return new Climate
            {
                elevation = elevation,
                moisture = .5 + (Octaves(seed, wx, wy, 40 * BiomeScale, 0x6b21u) - .5) * 1.8,
                temperature = .5 + (Octaves(seed, wx, wy, 48 * BiomeScale, 0xa70du) - .5) * 2.2 - Math.Max(0, elevation - .5) * .8,
                river = Math.Abs(Octaves(seed, x, y, 48 * RiverScale, 0x215cu) - .5),
                woodland = Noise(seed, wx, wy, 64 * BiomeScale, 0x8b03u)
            };
        }

        internal static BiomeKind SampleBiome(int seed, int x, int y) => Classify(ClimateAt(seed, x, y));

        private static BiomeKind Classify(Climate c)
        {
            if (c.elevation > .54 && c.temperature < .43) return BiomeKind.SnowMountain;
            if (c.temperature < .28) return BiomeKind.Tundra;
            if (c.moisture > .69 && c.elevation < .49 && c.temperature > .34) return BiomeKind.Swamp;
            if (c.river < .035 / RiverScale && c.elevation > .365 && c.elevation < .70) return BiomeKind.RiverValley;
            if (c.temperature > .54 && c.moisture < .37) return BiomeKind.Desert;
            if (c.moisture > .63 && c.elevation < .56) return BiomeKind.Wetland;
            if (c.temperature > .42 && c.temperature < .73 && c.moisture > .44 && c.moisture < .73 &&
                c.elevation > .38 && c.elevation < .68 && c.woodland > .58) return BiomeKind.SakuraGrove;
            if (c.temperature < .46 && c.moisture > .38) return BiomeKind.ConiferousForest;
            return c.moisture > .5 ? BiomeKind.DeciduousForest : BiomeKind.Meadow;
        }

        internal static void SampleContinuous(int seed, int x, int y, out char ground, out char biome)
        {
            Climate climate = ClimateAt(seed, x, y);
            BiomeKind kind = Classify(climate);
            biome = (char)('0' + (int)kind);
            // Widen the river network's spacing, not its playable crossing width.
            bool water = climate.elevation < .365 ||
                (climate.river < (.018 + Math.Max(0, climate.moisture - .4) * .018) / RiverScale && climate.elevation < .68);
            double soil = Noise(seed, x, y, 12, 0x5011u);
            ground = water ? '2' : soil < WorldGenerationCatalog.Biomes[(int)kind].dirtPercent / 100.0 ? '1' : '0';
        }
    }
}
