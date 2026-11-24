using System;
using System.Collections.Generic;

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
            // Version 12 thins the whole surface evenly, keeping per-biome ratios
            // and vein modulation but leaving room for hand-collectable supplies.
            if (generatorVersion >= WorldGeneration.ResourceThinningVersion &&
                Unit(seed, x, y, 0x0d31u) * 1000 >= WorldGeneration.ResourceDensityPermille) return false;
            // Oil is finite node stock, sampled before surface minerals so a
            // shoreline clay bonus cannot erase its sedimentary preference.
            // No entity IDs, local RNG or chunk visit order enter this field.
            if (generatorVersion >= 9 && Unit(seed, x, y, 0x0111u) * 1000 <
                rule.oilPermille * (.35 + Noise(seed, x, y, 24, 0x0112u) * 1.3))
            {
                kind = NodeKind.Oil;
                stock = SampleOilStock(seed, x, y, biome);
                return true;
            }
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
            else if (generatorVersion >= 9 && (roll -= rule.quartzPermille) < 0)
            {
                kind = NodeKind.Quartz;
                stock = SampleQuartzStock(seed, x, y);
                return true;
            }
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

        internal static int SampleOilStock(int seed, int x, int y, BiomeKind biome)
        {
            var rule = BiomeResourceCatalog.Get(biome);
            return rule.oilMinStock + (int)(Hash(seed, x, y, 0x0113u) % (uint)(rule.oilMaxStock - rule.oilMinStock + 1));
        }

        internal static double OilAccessPriority(int seed, int x, int y, BiomeKind biome) =>
            Unit(seed, x, y, 0x0114u) / BiomeResourceCatalog.Get(biome).oilPermille;

        internal static int SampleQuartzStock(int seed, int x, int y) =>
            80 + (int)(Hash(seed, x, y, 0x0141u) % 81);

        internal static double QuartzAccessPriority(int seed, int x, int y, BiomeKind biome) =>
            Unit(seed, x, y, 0x0142u) / BiomeResourceCatalog.Get(biome).quartzPermille;

        internal static uint GroundPickupRank(int seed, int x, int y) => Hash(seed, x, y, 0x01a0u);

        // Version 12 doubles the piles per chunk and raises their stock so direct
        // gathering, not deposit farming, carries the opening tool recipes.
        private const int MaxGroundPickupsPerChunk = 6;

        private static void AddGroundPickups(Region r, int resourceCount)
        {
            int slots = Math.Min(ResourceCap(r.world) - resourceCount, MaxNodesPerChunk - r.nodes.Count);
            if (slots <= 0) return;
            var oilSpace = new bool[r.ground.Length];
            foreach (var node in r.nodes)
                if (node.kind == NodeKind.Oil) ReserveOilPumpSpace(oilSpace, Size, node.x - r.x, node.y - r.y);
            bool boosted = r.world.generatorVersion >= WorldGeneration.ResourceThinningVersion;
            var paths = Paths(r.ground, r.blocked);
            var candidates = new List<int>();
            for (int y = 2; y < Size - 2; y++) for (int x = 2; x < Size - 2; x++)
            {
                int cell = x + y * Size;
                // Ground piles are walkable, so from version 12 they may also sit on
                // reserved gathering lanes; occupancy, pump space and water still exclude a cell.
                if (paths[cell] < 0 || r.occupied[cell] || (!boosted && r.reserved[cell]) || oilSpace[cell] ||
                    (r.ground[cell] != '0' && r.ground[cell] != '1')) continue;
                candidates.Add(cell);
            }
            SortCells(candidates, r, 0x01a0u);
            NodeKind[] kinds = { NodeKind.Branch, NodeKind.LooseStone, NodeKind.DryGrass };
            // Rotate the order so even a chunk with only one or two open slots can provide any material.
            int offset = (int)(Hash(r.world.seed, r.x, r.y, 0x01a1u) % (uint)kinds.Length);
            int limit = boosted ? MaxGroundPickupsPerChunk : kinds.Length;
            int count = Math.Min(Math.Min(slots, candidates.Count), limit);
            for (int i = 0; i < count; i++)
            {
                int cell = candidates[i];
                int x = r.x + cell % Size, y = r.y + cell / Size;
                int stock = (boosted ? 2 : 1) + (int)(Hash(r.world.seed, x, y, 0x01a2u) % 3);
                AddNode(r, cell, kinds[(offset + i) % kinds.Length], stock);
            }
        }

        // Every possible adjacent pump footprint, including corner adjacency.
        internal static void ReserveOilPumpSpace(bool[] reserved, int size, int oilX, int oilY)
        {
            int w = DemoCatalog.Width(BuildKind.OilPump), h = DemoCatalog.Height(BuildKind.OilPump);
            for (int y = Math.Max(0, oilY - h); y <= Math.Min(size - 1, oilY + h); y++)
                for (int x = Math.Max(0, oilX - w); x <= Math.Min(size - 1, oilX + w); x++) reserved[x + y * size] = true;
        }
    }
}
