using System;

namespace DeadCircuit
{
    public enum PrimitiveTech
    {
        Tools = 0, Charcoal = 1, Ceramics = 2, CopperTin = 3,
        Bronze = 4, Bloomery = 5, Construction = 6, Cooking = 7
    }

    [Serializable] public sealed class PrimitiveTechnologyDefinition
    {
        public PrimitiveTech id;
        public string name, description;
        public int prerequisites;
        public int[] cost;
    }

    // World research is separate from player levels and from the era ladder.
    // Version zero deliberately bypasses this layer for legacy saves and fixtures.
    public static class PrimitiveCatalog
    {
        private static PrimitiveTechnologyDefinition T(PrimitiveTech id, string name, string description,
            int prerequisites, int[] cost) => new PrimitiveTechnologyDefinition
            { id = id, name = name, description = description, prerequisites = prerequisites, cost = cost };

        public static readonly PrimitiveTechnologyDefinition[] All =
        {
            T(PrimitiveTech.Tools, "石制工具", "解锁石斧、石镐，开启原始工艺研究。", 0,
                DemoCatalog.Materials(ItemKind.Wood, 2, ItemKind.Stone, 2)),
            T(PrimitiveTech.Charcoal, "木炭烧制", "解锁炭窑，将木材烧成木炭。", 1 << (int)PrimitiveTech.Tools,
                DemoCatalog.Materials(ItemKind.Wood, 4, ItemKind.Stone, 2)),
            T(PrimitiveTech.Ceramics, "陶土烧结", "解锁陶窑，消耗黏土和木炭烧制陶砖。", 1 << (int)PrimitiveTech.Charcoal,
                DemoCatalog.Materials(ItemKind.Clay, 4, ItemKind.Charcoal, 1)),
            T(PrimitiveTech.CopperTin, "铜锡冶炼", "累计烧制陶砖×4后，可研究石炉炼铜、炼锡。", 1 << (int)PrimitiveTech.Ceramics,
                DemoCatalog.Materials(ItemKind.Brick, 2, ItemKind.Charcoal, 1)),
            T(PrimitiveTech.Bronze, "青铜合金", "累计冶炼铜锭×2、锡锭×1后，可研究在工作台熔制青铜。", 1 << (int)PrimitiveTech.CopperTin,
                DemoCatalog.Materials(ItemKind.Copper, 2, ItemKind.Tin, 1)),
            T(PrimitiveTech.Bloomery, "块炼铁", "累计制作青铜×4后，可研究石炉炼铁和军刀制作。", 1 << (int)PrimitiveTech.Bronze,
                DemoCatalog.Materials(ItemKind.Bronze, 2, ItemKind.Brick, 4)),
            T(PrimitiveTech.Construction, "基础建造", "解锁防护墙、出入门、储物箱和传送带。", 1 << (int)PrimitiveTech.Tools,
                DemoCatalog.Materials(ItemKind.Wood, 4, ItemKind.Stone, 4)),
            T(PrimitiveTech.Cooking, "烹饪净水", "解锁食物烹饪和原水煮沸，改善饮食与饮水安全。", 1 << (int)PrimitiveTech.Ceramics,
                DemoCatalog.Materials(ItemKind.Brick, 2, ItemKind.Clay, 2))
        };

        public static PrimitiveTechnologyDefinition Definition(int id) =>
            id >= 0 && id < All.Length ? All[id] : null;
        public static PrimitiveTechnologyDefinition Definition(PrimitiveTech id) => Definition((int)id);
        public static bool Enabled(WorldState s) => s != null && s.primitiveVersion > 0;
        public static bool HasResearch(WorldState s, int id) => s != null && Definition(id) != null &&
            (!Enabled(s) || (s.primitiveResearch & (1 << id)) != 0);
        public static bool HasResearch(WorldState s, PrimitiveTech id) => HasResearch(s, (int)id);

        public static string ResearchRequirement(WorldState s, PlayerState p, int id)
        {
            var definition = Definition(id);
            if (s == null || definition == null) return "无法识别这项原始研究。";
            if (!Enabled(s)) return "此世界无需进行原始工艺研究。";
            if (p == null || s.players == null || !s.players.Contains(p) || !p.connected || !(p.hp > 0f) ||
                p.items == null || p.items.Length != DemoCatalog.ItemCount) return "当前角色无法研究，请确认角色已连接且仍存活。";
            if (HasResearch(s, id)) return "全队已完成此项研究。";
            if (p.interactCooldown > 0f) return "操作尚未结束，请稍候。";
            if ((s.primitiveResearch & definition.prerequisites) != definition.prerequisites)
                return "需要前置研究：" + PrerequisiteNames(definition.prerequisites);
            if (!EquipmentCatalog.HasWorkbench(s, p)) return "请靠近工作台，并确保中间没有障碍。";
            string milestone = ResearchMilestone(s, definition.id);
            if (milestone.Length != 0) return milestone;
            for (int i = 0; i < definition.cost.Length; i++)
                if (p.items[i] < definition.cost[i]) return "研究材料不足：" + DemoCatalog.ItemNames[i];
            return "";
        }
        public static string ResearchRequirement(WorldState s, PlayerState p, PrimitiveTech id) => ResearchRequirement(s, p, (int)id);
        public static bool CanResearch(WorldState s, PlayerState p, int id) => ResearchRequirement(s, p, id).Length == 0;
        public static bool CanResearch(WorldState s, PlayerState p, PrimitiveTech id) => CanResearch(s, p, (int)id);

        private static string PrerequisiteNames(int mask)
        {
            string result = "";
            foreach (var definition in All) if ((mask & (1 << (int)definition.id)) != 0)
                result += (result.Length == 0 ? "" : "、") + definition.name;
            return result;
        }
        private static int Produced(WorldState s, ItemKind item) =>
            s.produced != null && (int)item < s.produced.Length ? s.produced[(int)item] : 0;
        public static string ResearchEvidence(WorldState s,int id) => s==null || Definition(id)==null ? "无法识别这项原始研究。" : ResearchMilestone(s,(PrimitiveTech)id);
        private static string ResearchMilestone(WorldState s, PrimitiveTech id)
        {
            switch (id)
            {
                case PrimitiveTech.CopperTin:
                    if (Produced(s, ItemKind.Brick) < 4) return "需累计烧制陶砖×4。";
                    break;
                case PrimitiveTech.Bronze:
                    if (Produced(s, ItemKind.Copper) < 2 || Produced(s, ItemKind.Tin) < 1) return "需累计冶炼铜锭×2、锡锭×1。";
                    break;
                case PrimitiveTech.Bloomery:
                    if (Produced(s, ItemKind.Bronze) < 4) return "需累计制作青铜×4。";
                    break;
            }
            return "";
        }

        private static int BuildResearch(BuildKind kind)
        {
            switch (kind)
            {
                case BuildKind.CharcoalKiln: return (int)PrimitiveTech.Charcoal;
                case BuildKind.ClayKiln: return (int)PrimitiveTech.Ceramics;
                case BuildKind.Wall: case BuildKind.Door: case BuildKind.Chest: case BuildKind.Belt:
                    return (int)PrimitiveTech.Construction;
                default: return -1;
            }
        }
        private static int RecipeResearch(int id)
        {
            switch (id)
            {
                case 2: return (int)PrimitiveTech.Bronze;
                case 10: return (int)PrimitiveTech.Bloomery;
                case 11: case 12: return (int)PrimitiveTech.CopperTin;
                case 100: return (int)PrimitiveTech.Charcoal;
                case 101: return (int)PrimitiveTech.Ceramics;
                default: return -1;
            }
        }
        public static bool IsBuildUnlocked(WorldState s, BuildKind kind) => BuildRequirement(s, kind).Length == 0;
        public static string BuildRequirement(WorldState s, BuildKind kind)
        {
            if (s == null || (int)kind < 0 || (int)kind >= DemoCatalog.BuildNames.Length) return "无法识别这项建筑。";
            if (s.era < TechCatalog.RequiredEra(kind)) return "需要先进入：" + TechCatalog.EraName(TechCatalog.RequiredEra(kind));
            int id = BuildResearch(kind);
            return id < 0 || HasResearch(s, id) ? WaterCatalog.BuildRequirement(s, kind) : "需要原始研究：" + Definition(id).name;
        }
        public static bool IsRecipeUnlocked(WorldState s, RecipeDefinition recipe) => RecipeRequirement(s, recipe).Length == 0;
        public static string RecipeRequirement(WorldState s, RecipeDefinition recipe)
        {
            if (s == null || recipe == null) return "无法识别这项配方。";
            if (s.era < recipe.era) return "需要先进入：" + TechCatalog.EraName(recipe.era);
            string equipment=EquipmentResearchCatalog.RecipeRequirement(s,recipe);
            if(equipment.Length>0) return equipment;
            int id = RecipeResearch(recipe.id);
            return id < 0 || HasResearch(s, id) ? WaterCatalog.RecipeRequirement(s, recipe) : "需要原始研究：" + Definition(id).name;
        }
        public static bool IsGearUnlocked(WorldState s, GearKind kind)
        {
            if (s == null || (int)kind < 0 || (int)kind >= EquipmentCatalog.GearNames.Length || s.era < EquipmentCatalog.Gear(kind).era) return false;
            if (kind == GearKind.StoneAxe || kind == GearKind.StonePickaxe) return HasResearch(s, PrimitiveTech.Tools);
            return kind != GearKind.Knife || HasResearch(s, PrimitiveTech.Bloomery);
        }
        public static bool IsCookingUnlocked(WorldState s) => HasResearch(s, PrimitiveTech.Cooking);

        public static string WaterRequirement(WorldState s)
        {
            if (!Enabled(s)) return "";
            if (!HasResearch(s, PrimitiveTech.Bronze) || !HasResearch(s, PrimitiveTech.Bloomery) || !HasResearch(s, PrimitiveTech.Construction))
                return "进入水力时代前，需完成青铜合金、块炼铁和基础建造研究。";
            if (Produced(s, ItemKind.Bronze) < 4 || Produced(s, ItemKind.Iron) < 6 ||
                Produced(s, ItemKind.Brick) < 4 || Produced(s, ItemKind.Charcoal) < 2)
                return "进入水力时代前，需累计生产：青铜×4、铁锭×6、陶砖×4、木炭×2。";
            return "";
        }
    }
}
