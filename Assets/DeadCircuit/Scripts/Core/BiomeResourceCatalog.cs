using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public sealed class BiomeResourceDefinition
    {
        public readonly BiomeKind kind;
        public readonly string hint;
        // Per eligible land tile, before metal vein modulation and access budgets.
        public readonly int stonePermille, clayPermille, ironPermille, copperPermille, tinPermille, uraniumPermille;

        internal BiomeResourceDefinition(BiomeKind kind, string hint, int stone, int clay, int iron, int copper, int tin, int uranium)
        {
            this.kind = kind; this.hint = hint;
            stonePermille = stone; clayPermille = clay; ironPermille = iron;
            copperPermille = copper; tinPermille = tin; uraniumPermille = uranium;
        }
    }

    public static class BiomeResourceCatalog
    {
        public const int ShoreClayBonusPermille = 160;
        public static readonly IReadOnlyList<BiomeResourceDefinition> All = Array.AsReadOnly(new[]
        {
            new BiomeResourceDefinition(BiomeKind.Meadow, "资源较均衡，黏土易找；沿岸更多", 25, 30, 22, 18, 14, 3),
            new BiomeResourceDefinition(BiomeKind.DeciduousForest, "木材、果实丰富；沿岸找黏土", 18, 18, 14, 14, 12, 2),
            new BiomeResourceDefinition(BiomeKind.ConiferousForest, "木材、石材、铁矿与锡矿较多", 50, 10, 65, 16, 40, 6),
            new BiomeResourceDefinition(BiomeKind.Desert, "石材、铜矿与铀矿较多；缺木，沿岸找黏土", 100, 3, 24, 65, 12, 16),
            new BiomeResourceDefinition(BiomeKind.Wetland, "黏土、锡矿与铜矿较多", 12, 95, 12, 45, 60, 3),
            new BiomeResourceDefinition(BiomeKind.Tundra, "石材、铁矿与铀矿较多；木材稀少", 90, 6, 70, 18, 14, 20)
        });

        public static BiomeResourceDefinition Get(BiomeKind kind) =>
            (int)kind >= 0 && (int)kind < All.Count ? All[(int)kind] : null;
    }
}
