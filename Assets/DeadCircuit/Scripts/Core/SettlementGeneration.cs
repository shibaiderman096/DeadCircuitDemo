using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    // Serialized ordinals. Zero deliberately remains the gen1-gen4 default.
    public enum SettlementKind
    {
        Unspecified = 0, HighDensityCity = 1, LowDensityCity = 2,
        HighDensitySuburb = 3, LowDensitySuburb = 4, Rural = 5
    }

    public sealed class SettlementDefinition
    {
        public readonly SettlementKind kind;
        public readonly string name;
        public readonly int streetSpacing, streetWidth, sitePermille;
        internal SettlementDefinition(SettlementKind kind, string name, int streetSpacing, int streetWidth, int sitePermille)
        {
            this.kind = kind; this.name = name; this.streetSpacing = streetSpacing;
            this.streetWidth = streetWidth; this.sitePermille = sitePermille;
        }
    }

    public static class SettlementCatalog
    {
        public static readonly IReadOnlyList<SettlementDefinition> All = Array.AsReadOnly(new[]
        {
            new SettlementDefinition(SettlementKind.Unspecified, "未指定", 0, 0, 0),
            new SettlementDefinition(SettlementKind.HighDensityCity, "高密度市区", 32, 2, 820),
            new SettlementDefinition(SettlementKind.LowDensityCity, "低密度市区", 64, 2, 660),
            new SettlementDefinition(SettlementKind.HighDensitySuburb, "高密度郊区", 128, 2, 480),
            new SettlementDefinition(SettlementKind.LowDensitySuburb, "低密度郊区", 256, 1, 300),
            new SettlementDefinition(SettlementKind.Rural, "乡村", 512, 1, 140)
        });

        public static SettlementDefinition Get(SettlementKind kind) =>
            (int)kind >= 0 && (int)kind < All.Count ? All[(int)kind] : null;

        // Kept at the catalog boundary used by ChunkTerrain's gen5 street hook.
        public static bool Street(WorldState world, int x, int y) => SettlementGeneration.Street(world, x, y);
    }

    // Gen5 overlay only. Callers retain their gen1-gen4 branches. These functions
    // read seed/settings, never generated chunks, mutable entities or biome data.
    public static class SettlementGeneration
    {
        // Columns: high/low city, high/low suburb, rural. Rows retain SiteKind
        // ordinals. Zero is an intentional use exclusion, not a missing weight.
        private static readonly int[,] Weights =
        {
            { 10, 42, 100, 95, 15 }, // Residence
            { 34, 38, 45, 25, 5 },   // Shop
            { 10, 14, 12, 7, 4 },    // Warehouse
            { 8, 12, 12, 10, 8 },    // Workshop
            { 12, 18, 22, 10, 3 },   // Clinic
            { 0, 0, 8, 24, 85 },     // Farmhouse
            { 8, 13, 16, 12, 9 },    // GasStation
            { 12, 13, 8, 4, 1 },     // PoliceStation
            { 12, 18, 18, 8, 1 },    // School
            { 100, 62, 0, 0, 0 },    // Apartment
            { 0, 0, 5, 15, 65 },     // Greenhouse
            { 8, 12, 12, 6, 3 },     // Substation
            { 38, 32, 18, 9, 2 },    // Restaurant
            { 28, 24, 18, 9, 1 },    // Pharmacy
            { 18, 23, 24, 18, 10 },  // HardwareStore
            { 48, 28, 0, 0, 0 },     // Supermarket
            { 16, 18, 8, 2, 0 },     // Library
            { 14, 15, 12, 6, 2 },    // FireStation
            { 70, 30, 0, 0, 0 },     // Hospital
            { 10, 8, 6, 4, 2 },      // ResearchLab
            { 6, 10, 8, 4, 1 },      // PowerPlant
            { 8, 10, 10, 6, 2 },     // WaterTreatment
            { 6, 6, 6, 5, 4 },       // RadioStation
            { 3, 4, 5, 5, 6 },       // Bunker
            { 0, 2, 8, 16, 35 },     // LumberMill
            { 0, 2, 6, 12, 25 },     // MineOffice
            { 16, 12, 6, 2, 1 },     // TrainDepot
            { 0, 0, 5, 18, 75 },     // Barn
            { 0, 1, 8, 30, 80 },     // HuntingCabin
            { 0, 1, 6, 15, 35 },     // RangerStation
            { 6, 10, 16, 20, 12 },   // Motel
            { 6, 10, 12, 10, 10 }    // RecyclingYard
        };

        public static SettlementKind Sample(WorldState world, int x, int y)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var settings = world.generation;
            int density = settings == null ? 1 : settings.cityDensity;
            if (density <= 0) return SettlementKind.Rural;
            WorldPreset preset = settings == null ? WorldPreset.Balanced : settings.preset;
            // Broad districts with smaller-scale edges create differently sized
            // contiguous town/city belts, rather than an independent chunk roll.
            double field = Noise(world.seed, x, y, 384, 0x5e771u) * .72 +
                Noise(world.seed, x, y, 128, 0x5e772u) * .28 +
                (preset == WorldPreset.Urban ? .12 : preset == WorldPreset.Wilderness ? -.14 : 0) +
                (Math.Min(2, density) - 1) * .09;
            return field >= .66 ? SettlementKind.HighDensityCity :
                field >= .56 ? SettlementKind.LowDensityCity :
                field >= .46 ? SettlementKind.HighDensitySuburb :
                field >= .37 ? SettlementKind.LowDensitySuburb : SettlementKind.Rural;
        }

        public static bool Street(WorldState world, int x, int y)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            // Preserve the tutorial cross. All nested road lattices share the
            // same global origin, including the all-world rural trunk network.
            if (x == 32 || x == 33 || y == 32 || y == 33) return true;
            var densest = SettlementCatalog.Get(SettlementKind.HighDensityCity);
            if (Mod(x, densest.streetSpacing) >= densest.streetWidth && Mod(y, densest.streetSpacing) >= densest.streetWidth)
                return false;
            var rule = SettlementCatalog.Get(Sample(world, x, y));
            return Mod(x, rule.streetSpacing) < rule.streetWidth || Mod(y, rule.streetSpacing) < rule.streetWidth;
        }

        public static double SiteChance(WorldState world, SettlementKind kind)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var rule = SettlementCatalog.Get(kind);
            if (rule == null || kind == SettlementKind.Unspecified) return 0;
            var settings = world.generation;
            int density = settings == null ? 1 : Math.Max(0, Math.Min(2, settings.cityDensity));
            WorldPreset preset = settings == null ? WorldPreset.Balanced : settings.preset;
            return rule.sitePermille / 1000.0 * (.9 + density * .1) *
                (preset == WorldPreset.Urban ? 1.08 : preset == WorldPreset.Wilderness ? .75 : 1);
        }

        // x/y are world tile coordinates (the staged chunk's center), not chunk indices.
        public static bool ShouldPlaceSite(WorldState world, int x, int y)
        {
            var settlement = Sample(world, x, y);
            return Unit(world.seed, x, y, 0x5e773u) < SiteChance(world, settlement);
        }

        public static int BlueprintWeight(SettlementKind settlement, SiteKind kind, BiomeKind biome)
        {
            if ((int)settlement < 1 || (int)settlement > 5 || (int)biome < 0 || (int)biome >= WorldGeneration.BiomeCount ||
                (int)kind < 0 || (int)kind >= Weights.GetLength(0)) return 0;
            var plan = SiteBlueprintCatalog.Get(kind);
            return plan != null && (plan.biomeMask & (1 << (int)biome)) != 0 ? Weights[(int)kind, (int)settlement - 1] : 0;
        }

        public static SiteBlueprint SelectBlueprint(WorldState world, int x, int y, SettlementKind settlement, BiomeKind biome)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            int total = 0;
            foreach (var plan in SiteBlueprintCatalog.All) total += BlueprintWeight(settlement, plan.kind, biome);
            if (total == 0) return null;
            int roll = (int)(Hash(world.seed, x, y, 0x5e774u) % (uint)total);
            foreach (var plan in SiteBlueprintCatalog.All)
            {
                roll -= BlueprintWeight(settlement, plan.kind, biome);
                if (roll < 0) return plan;
            }
            return null;
        }

        private static int Mod(int coordinate, int spacing)
        {
            int remainder = coordinate % spacing;
            return remainder < 0 ? remainder + spacing : remainder;
        }

        private static double Noise(int seed, int x, int y, int scale, uint channel)
        {
            // Double division avoids overflowing a floor-quantized origin at
            // int.MinValue; lattice indices themselves remain comfortably int.
            double px = (double)x / scale, py = (double)y / scale;
            int ix = (int)Math.Floor(px), iy = (int)Math.Floor(py);
            double fx = px - ix, fy = py - iy;
            fx = fx * fx * fx * (fx * (fx * 6 - 15) + 10);
            fy = fy * fy * fy * (fy * (fy * 6 - 15) + 10);
            double a = Unit(seed, ix, iy, channel), b = Unit(seed, ix + 1, iy, channel);
            double c = Unit(seed, ix, iy + 1, channel), d = Unit(seed, ix + 1, iy + 1, channel);
            return (a + (b - a) * fx) * (1 - fy) + (c + (d - c) * fx) * fy;
        }

        private static double Unit(int seed, int x, int y, uint channel) => Hash(seed, x, y, channel) / 4294967296.0;
        private static uint Hash(int seed, int x, int y, uint channel)
        {
            unchecked
            {
                uint value = (uint)seed ^ channel ^ (uint)x * 0x9e3779b9u ^ (uint)y * 0x85ebca6bu;
                value ^= value >> 16; value *= 0x7feb352du;
                value ^= value >> 15; value *= 0x846ca68bu;
                return value ^ (value >> 16);
            }
        }
    }
}
