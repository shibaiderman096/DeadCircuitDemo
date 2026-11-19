using System;

namespace DeadCircuit
{
    public static partial class ChunkTerrain
    {
        // Pure world-space sampling shared by the finite tutorial map and sparse chunks.
        // Callers check local ground/occupancy and supply their own shoreline flag.
        internal static bool SampleResource(int seed, int x, int y, BiomeKind biome,
            WorldGenerationSettings settings, bool shoreline, out NodeKind kind, out int stock, int generatorVersion)
        {
            kind = NodeKind.Tree; stock = 0;
            var rule = BiomeResourceCatalog.Get(biome);
            if (rule == null) return false;
            double roll = Unit(seed, x, y, 0x7e51u) * 1000;
            // A nonzero background prevents biome/vein boundaries from locking
            // technology; richer deposits follow the same world-space field.
            double vein = .15 + Math.Max(0, Noise(seed, x, y, 8, 0x0ae1u) - .4) * 5;
            if ((roll -= rule.clayPermille + (shoreline ? BiomeResourceCatalog.ShoreClayBonusPermille : 0)) < 0) kind = NodeKind.Clay;
            else if ((roll -= rule.stonePermille) < 0) kind = NodeKind.Stone;
            // These exposed deposits need no industrial unlock. Every natural
            // biome has a background supply even outside its favored geology.
            else if (generatorVersion >= 8 && (roll -= rule.coalPermille) < 0) kind = NodeKind.Coal;
            else if (generatorVersion >= 8 && (roll -= rule.limestonePermille) < 0) kind = NodeKind.Limestone;
            else if (generatorVersion >= 8 && (roll -= rule.fireClayPermille) < 0) kind = NodeKind.FireClay;
            else if ((roll -= rule.ironPermille * vein) < 0) kind = NodeKind.Ore;
            else if ((roll -= rule.copperPermille * vein) < 0) kind = NodeKind.CopperOre;
            else if ((roll -= rule.tinPermille * vein) < 0) kind = NodeKind.TinOre;
            else if ((roll -= rule.uraniumPermille * vein) < 0) kind = NodeKind.UraniumOre;
            else
            {
                double forest = Noise(seed, x, y, 9, 0x7ee1u);
                double trees = WorldGenerationCatalog.Biomes[(int)biome].treePermille / 1000.0;
                if (settings.preset == WorldPreset.Wilderness) trees *= 1.2;
                if (settings.preset == WorldPreset.Urban) trees *= .75;
                if (forest > .36 && Unit(seed, x, y, 0x2e51u) < trees * (.35 + forest))
                { stock = 12 + (int)(Hash(seed, x, y, 0x570cu) % 13); return true; }
                // Scrap follows development, never biome mineral weights.
                bool developed = settings.cityDensity > 0 && Urbanity(seed, x, y) >= CityThreshold(settings);
                if (Unit(seed, x, y, 0x5c2au) >= (developed ? .04 : .008)) return false;
                kind = NodeKind.Scrap; stock = 10 + (int)(Hash(seed, x, y, 0x570eu) % 15);
                return true;
            }
            stock = kind == NodeKind.Stone || kind == NodeKind.Clay || kind == NodeKind.Limestone || kind == NodeKind.FireClay
                ? 60 + (int)(Hash(seed, x, y, 0x6a52u) % 101)
                : (kind == NodeKind.UraniumOre ? 40 : 80) + (int)(Hash(seed, x, y, 0x570du) % 81);
            return true;
        }
    }
}
