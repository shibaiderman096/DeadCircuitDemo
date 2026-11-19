using System;

namespace DeadCircuit
{
    public enum WaterTech { Wheel, Shafts, Crushing, Blast, Pressing, Components, Transport, Agriculture }

    [Serializable] public sealed class WaterTechnologyDefinition
    {
        public WaterTech id;
        public string name, description;
        public int prerequisites;
        public int[] cost;
    }

    public static class WaterCatalog
    {
        static int Bit(WaterTech id) => 1 << (int)id;
        static WaterTechnologyDefinition T(WaterTech id, string name, string description, int prerequisites, int[] cost) =>
            new WaterTechnologyDefinition { id = id, name = name, description = description, prerequisites = prerequisites, cost = cost };
        public static readonly WaterTechnologyDefinition[] All =
        {
            T(WaterTech.Wheel, "水车动力", "岸边水车输出40机械功率；首批设备只用原始材料。", 0,
                DemoCatalog.Materials(ItemKind.Wood,4,ItemKind.Bronze,2)),
            T(WaterTech.Shafts, "轴系传动", "传动轴必须沿边接触；加工验收要求同网存活水车和轴。", Bit(WaterTech.Wheel),
                DemoCatalog.Materials(ItemKind.Wood,4,ItemKind.Iron,2)),
            T(WaterTech.Crushing, "机械破碎", "解锁粉碎机与碎矿炼铁；1铁矿粉碎后可提高冶炼收益。", Bit(WaterTech.Shafts),
                DemoCatalog.Materials(ItemKind.Iron,3,ItemKind.Stone,4)),
            T(WaterTech.Blast, "鼓风冶炼", "机械鼓风炉消耗木炭，将碎铁矿、铜矿或锡矿高效冶炼。", Bit(WaterTech.Shafts),
                DemoCatalog.Materials(ItemKind.Brick,4,ItemKind.Bronze,2)),
            T(WaterTech.Pressing, "锻造压板", "锻压机将1铁锭制成1铁板，开启机械零件生产。", Bit(WaterTech.Shafts),
                DemoCatalog.Materials(ItemKind.Iron,4,ItemKind.Bronze,2)),
            T(WaterTech.Components, "齿轮轴承", "先完成水力锻压，再用铁板和青铜制造齿轮、轴承。", Bit(WaterTech.Pressing),
                DemoCatalog.Materials(ItemKind.IronPlate,2,ItemKind.Bronze,2)),
            T(WaterTech.Transport, "基础运输", "机械装卸器由齿轮、轴承构成，接轴后以双倍基础速度出货。", Bit(WaterTech.Shafts)|Bit(WaterTech.Components),
                DemoCatalog.Materials(ItemKind.Gear,1,ItemKind.Bearing,1,ItemKind.Wood,4)),
            T(WaterTech.Agriculture, "灌溉磨粉", "可选生存支线：近水提灌与动力磨粉；保留原有手工磨粉。", Bit(WaterTech.Wheel)|Bit(WaterTech.Shafts),
                DemoCatalog.Materials(ItemKind.Bronze,2,ItemKind.Brick,2))
        };
        public static WaterTechnologyDefinition Definition(int id) => id >= 0 && id < All.Length ? All[id] : null;
        public static bool Enabled(WorldState s) => s != null && s.waterVersion == 1;
        public static bool HasResearch(WorldState s, int id) => s != null && Definition(id) != null &&
            (!Enabled(s) || (s.waterResearch & (1 << id)) != 0);
        public static bool HasResearch(WorldState s, WaterTech id) => HasResearch(s, (int)id);
        public static string ResearchRequirement(WorldState s, PlayerState p, int id)
        {
            var d = Definition(id);
            if (s == null || d == null) return "无效水力科技。";
            if (!Enabled(s)) return "此世界沿用旧水力规则，无需水力研究。";
            if (s.era < TechEra.Water) return "先完成原始阶段验收并进入水力时代。";
            if (p == null || !s.players.Contains(p) || !p.connected || !(p.hp > 0f) ||
                p.items == null || p.items.Length != DemoCatalog.ItemCount) return "研究者无效。";
            if (HasResearch(s,id)) return "全队已完成此项研究。";
            if (p.interactCooldown > 0f) return "请等待交互冷却。";
            if ((s.waterResearch & d.prerequisites) != d.prerequisites)
            {
                string names = "";
                foreach (var parent in All) if ((d.prerequisites & Bit(parent.id)) != 0) names += (names.Length == 0 ? "" : "、") + parent.name;
                return "需要前置研究：" + names;
            }
            if (!EquipmentCatalog.HasWorkbench(s,p)) return "需要近距可见工作台。";
            string milestone = ResearchMilestone(s,d.id);
            if (milestone.Length != 0) return milestone;
            for (int i=0;i<d.cost.Length;i++) if (p.items[i]<d.cost[i]) return "研究材料不足："+DemoCatalog.ItemNames[i];
            return "";
        }
        public static bool CanResearch(WorldState s, PlayerState p, int id) => ResearchRequirement(s,p,id).Length == 0;
        static string ResearchMilestone(WorldState s, WaterTech id)
        {
            if ((id == WaterTech.Wheel || id == WaterTech.Shafts) && PrimitiveCatalog.WaterRequirement(s).Length != 0) return PrimitiveCatalog.WaterRequirement(s);
            if ((id == WaterTech.Crushing || id == WaterTech.Pressing) && !PrimitiveCatalog.HasResearch(s,PrimitiveTech.Bloomery)) return "需要原始研究：块炼铁。";
            if (id == WaterTech.Blast && (!PrimitiveCatalog.HasResearch(s,PrimitiveTech.Bloomery) || !PrimitiveCatalog.HasResearch(s,PrimitiveTech.CopperTin))) return "需要原始研究：块炼铁、铜锡冶炼。";
            if (id == WaterTech.Components && !PrimitiveCatalog.HasResearch(s,PrimitiveTech.Bronze)) return "需要原始研究：青铜合金。";
            if (id == WaterTech.Components && s.waterPressed < 2) return "需要水车经轴系实际锻压2铁板（当前"+s.waterPressed+"）。";
            if (id == WaterTech.Transport && (s.waterGears < 2 || s.waterBearings < 2)) return "需要水车经轴系实际制造齿轮2、轴承2。";
            if (id == WaterTech.Agriculture && !PrimitiveCatalog.IsCookingUnlocked(s)) return "需要原始研究：烹饪净水。";
            return "";
        }
        static int BuildResearch(BuildKind kind)
        {
            switch(kind)
            {
                case BuildKind.Waterwheel: return (int)WaterTech.Wheel;
                case BuildKind.Shaft: return (int)WaterTech.Shafts;
                case BuildKind.MechanicalCrusher: return (int)WaterTech.Crushing;
                case BuildKind.BlastFurnace: return (int)WaterTech.Blast;
                case BuildKind.MechanicalPress: return (int)WaterTech.Pressing;
                case BuildKind.MechanicalWorkshop: return (int)WaterTech.Components;
                case BuildKind.MechanicalHopper: return (int)WaterTech.Transport;
                case BuildKind.WaterMill: case BuildKind.IrrigationPump: return (int)WaterTech.Agriculture;
                default: return -1;
            }
        }
        public static string BuildRequirement(WorldState s, BuildKind kind)
        {
            if (s == null || (int)kind<0 || (int)kind>=DemoCatalog.BuildNames.Length) return "无效建筑。";
            if (s.era < TechCatalog.RequiredEra(kind)) return "需要科技时代："+TechCatalog.EraName(TechCatalog.RequiredEra(kind));
            int id=BuildResearch(kind);
            return id<0 || HasResearch(s,id) ? SteamCatalog.BuildRequirement(s,kind) : "需要水力研究："+Definition(id).name;
        }
        public static bool IsBuildUnlocked(WorldState s, BuildKind kind) => BuildRequirement(s,kind).Length == 0;
        public static string RecipeRequirement(WorldState s, RecipeDefinition recipe)
        {
            if (s==null || recipe==null) return "无效配方。";
            if (s.era<recipe.era) return "需要科技时代："+TechCatalog.EraName(recipe.era);
            int id = recipe.id==13 || recipe.id==20 ? (int)WaterTech.Crushing : BuildResearch(recipe.building);
            return id<0 || HasResearch(s,id) ? SteamCatalog.RecipeRequirement(s,recipe) : "需要水力研究："+Definition(id).name;
        }
        public static bool IsRecipeUnlocked(WorldState s, RecipeDefinition recipe) => RecipeRequirement(s,recipe).Length == 0;
        public static string SteamRequirement(WorldState s)
        {
            if (!Enabled(s)) return "";
            if ((s.waterResearch & 127) != 127) return "蒸汽入口需要水力前7项研究；灌溉磨粉为可选支线。";
            if (s.waterCrushed<4 || s.waterSmelted<6 || s.waterPressed<4 || s.waterGears<2 || s.waterBearings<2 || s.waterTransported<4)
                return "水车＋轴系实际验收：碎矿"+s.waterCrushed+"/4、鼓风铁"+s.waterSmelted+"/6、铁板"+s.waterPressed+"/4、齿轮"+s.waterGears+"/2、轴承"+s.waterBearings+"/2、装卸到货"+s.waterTransported+"/4。";
            return "";
        }
        // The simulator assigns the consumer to one actual connected component. A distant
        // wheel, isolated shaft or a steam engine alone cannot provide water-era evidence.
        public static bool IsWaterDriven(WorldState s, BuildingState b)
        {
            if (!Enabled(s) || b==null || b.hp<=0f || !b.powered || b.networkId<=0 || b.powerRatio<=0f || !IsBuildUnlocked(s,b.kind)) return false;
            bool wheel=false,shaft=false;
            foreach(var n in s.buildings)
            {
                if(n==null || n.hp<=0f || n.networkId!=b.networkId || !n.powered || !IsBuildUnlocked(s,n.kind)) continue;
                if(n.kind==BuildKind.Waterwheel && n.powerGeneration>0f) wheel=true;
                if(n.kind==BuildKind.Shaft) shaft=true;
            }
            return wheel && shaft;
        }
    }
}
