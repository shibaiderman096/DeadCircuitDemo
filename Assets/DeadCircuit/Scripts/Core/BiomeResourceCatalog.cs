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
        public readonly int coalPermille, limestonePermille, fireClayPermille;
        public readonly int quartzPermille;
        public readonly int oilPermille, oilMinStock, oilMaxStock;

        internal BiomeResourceDefinition(BiomeKind kind, string hint, int stone, int clay, int iron, int copper, int tin, int uranium,
            int coal, int limestone, int fireClay, int oil, int oilMinStock = 800, int oilMaxStock = 1600, int quartz = 3)
        {
            this.kind = kind;
            this.hint = hint + (quartz > 4 ? "；石英露头较多" : "；偶有石英露头") +
                (oil >= 18 ? "；沉积油藏较多，需勘探后用机器采抽" : "；偶有小型油藏，需用机器采抽");
            stonePermille = stone; clayPermille = clay; ironPermille = iron;
            copperPermille = copper; tinPermille = tin; uraniumPermille = uranium;
            coalPermille = coal; limestonePermille = limestone; fireClayPermille = fireClay;
            quartzPermille = quartz;
            oilPermille = oil; this.oilMinStock = oilMinStock; this.oilMaxStock = oilMaxStock;
        }
    }

    public static class BiomeResourceCatalog
    {
        public const int ShoreClayBonusPermille = 160;
        public static readonly IReadOnlyList<BiomeResourceDefinition> All = Array.AsReadOnly(new[]
        {
            new BiomeResourceDefinition(BiomeKind.Meadow, "资源较均衡，沿岸黏土更多；煤、石灰石与耐火黏土有少量露头", 25, 30, 22, 18, 14, 3, 10, 12, 10, 2),
            new BiomeResourceDefinition(BiomeKind.DeciduousForest, "木材、果实丰富；沿岸找黏土，三种蒸汽原料均有零星露头", 18, 18, 14, 14, 12, 2, 12, 10, 12, 2),
            new BiomeResourceDefinition(BiomeKind.ConiferousForest, "煤层露头多，木材、石材、铁矿与锡矿较多；其余蒸汽原料零星可采", 50, 10, 65, 16, 40, 6, 75, 12, 8, 1),
            new BiomeResourceDefinition(BiomeKind.Desert, "石灰石露头多，石材、铜矿与铀矿较多；煤和耐火黏土零星可采，缺木", 100, 3, 24, 65, 12, 16, 8, 72, 6, 22, 1600, 3200, quartz: 30),
            new BiomeResourceDefinition(BiomeKind.Wetland, "沉积层耐火黏土丰富，黏土、锡矿与铜矿较多；煤和石灰石零星可采", 12, 95, 12, 45, 60, 3, 8, 10, 75, 18, 1600, 3200),
            new BiomeResourceDefinition(BiomeKind.Tundra, "煤层露头多，石材、铁矿与铀矿较多；石灰石和耐火黏土零星可采，木材少", 90, 6, 70, 18, 14, 20, 70, 22, 8, 1),
            new BiomeResourceDefinition(BiomeKind.Swamp, "沉积层耐火黏土丰富，原水、黏土与菌草多；煤和石灰石零星可采", 18, 120, 10, 35, 72, 2, 8, 12, 85, 26, 1600, 3200),
            new BiomeResourceDefinition(BiomeKind.SnowMountain, "裸岩石灰石露头多，铁矿与铀矿较多；煤和耐火黏土零星可采，木材少", 115, 4, 92, 20, 12, 24, 12, 82, 6, 1, quartz: 36),
            new BiomeResourceDefinition(BiomeKind.SakuraGrove, "木材、果实与药草丰富；煤、石灰石与耐火黏土有少量露头", 20, 24, 18, 18, 16, 3, 10, 12, 14, 2),
            new BiomeResourceDefinition(BiomeKind.RiverValley, "河岸石灰石与沉积层耐火黏土较多，原水、黏土丰富；煤零星可采", 24, 70, 20, 25, 20, 4, 10, 80, 60, 20, 1600, 3200, quartz: 26)
        });

        public static BiomeResourceDefinition Get(BiomeKind kind) =>
            (int)kind >= 0 && (int)kind < All.Count ? All[(int)kind] : null;
    }
}
