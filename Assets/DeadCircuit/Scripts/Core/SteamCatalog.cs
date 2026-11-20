using System;

namespace DeadCircuit
{
    public enum SteamTech { Coking, Refractories, PigIron, Refining, Machining, Piping, Boiler, Drive }

    [Serializable] public sealed class SteamTechnologyDefinition
    {
        public SteamTech id;
        public string name, description;
        public int prerequisites;
        public int[] cost;
    }

    public static class SteamCatalog
    {
        static int Bit(SteamTech id) => 1 << (int)id;
        static SteamTechnologyDefinition T(SteamTech id, string name, string description, int prerequisites, int[] cost) =>
            new SteamTechnologyDefinition { id=id, name=name, description=description, prerequisites=prerequisites, cost=cost };
        public static readonly SteamTechnologyDefinition[] All =
        {
            T(SteamTech.Coking,"焦化","采煤后在焦炉制焦；木炭提供首批加热，焦炭用于还原铁矿。",0,
                DemoCatalog.Materials(ItemKind.Coal,4,ItemKind.Brick,2)),
            T(SteamTech.Refractories,"耐火建材","采耐火黏土，烧制区别于普通陶砖的耐火炉衬。",0,
                DemoCatalog.Materials(ItemKind.FireClay,3,ItemKind.Brick,2)),
            T(SteamTech.PigIron,"生铁冶炼","水力轴系驱动高炉鼓风：碎铁矿、焦炭与石灰石熔剂炼生铁。",Bit(SteamTech.Coking)|Bit(SteamTech.Refractories),
                DemoCatalog.Materials(ItemKind.Coke,2,ItemKind.RefractoryBrick,2,ItemKind.Limestone,1)),
            T(SteamTech.Refining,"钢铁精炼","首批精炼炉由水力驱动搅炼，以生铁与熔剂制钢。",Bit(SteamTech.PigIron),
                DemoCatalog.Materials(ItemKind.PigIron,2,ItemKind.Limestone,1)),
            T(SteamTech.Machining,"机床加工","水力制造首批机床；既有压机可将钢压为耐压钢板。",Bit(SteamTech.Refining),
                DemoCatalog.Materials(ItemKind.Steel,2,ItemKind.Gear,1,ItemKind.Bearing,1)),
            T(SteamTech.Piping,"耐压管道","机床用钢板和青铜加工管道、接头与阀件的组合件。",Bit(SteamTech.Machining),
                DemoCatalog.Materials(ItemKind.SteelPlate,2,ItemKind.Bronze,2)),
            T(SteamTech.Boiler,"模块锅炉","用耐火炉衬、钢板和管件构成燃烧、给水、排烟与蒸汽集群。",Bit(SteamTech.Refractories)|Bit(SteamTech.Refining)|Bit(SteamTech.Piping),
                DemoCatalog.Materials(ItemKind.SteelPlate,2,ItemKind.RefractoryBrick,2)),
            T(SteamTech.Drive,"蒸汽驱动","蒸汽引擎把蒸汽转为机械功率，经轴系驱动工厂，替代或补充水车。",Bit(SteamTech.Boiler)|Bit(SteamTech.Piping),
                DemoCatalog.Materials(ItemKind.PipeFitting,2,ItemKind.Gear,1,ItemKind.Bearing,1))
        };
        public static SteamTechnologyDefinition Definition(int id) => id>=0 && id<All.Length ? All[id] : null;
        public static bool HasResearch(WorldState s, int id) => s!=null && Definition(id)!=null && (s.steamResearch & (1<<id))!=0;
        public static bool HasResearch(WorldState s, SteamTech id) => HasResearch(s,(int)id);
        public static bool CanResearch(WorldState s, PlayerState p, int id) => ResearchRequirement(s,p,id).Length==0;
        public static string ResearchRequirement(WorldState s, PlayerState p, int id)
        {
            var d=Definition(id);
            if(s==null || d==null) return "无效蒸汽科技。";
            if(s.era<TechEra.Steam) return "先完成水力阶段验收并进入蒸汽时代。";
            if(p==null || !s.players.Contains(p) || !p.connected || !(p.hp>0f) ||
                p.items==null || p.items.Length!=DemoCatalog.ItemCount) return "研究者无效。";
            if(HasResearch(s,id)) return "全队已完成此项研究。";
            if(p.interactCooldown>0f) return "请等待交互冷却。";
            if((s.steamResearch & d.prerequisites)!=d.prerequisites)
            {
                string names="";
                foreach(var parent in All) if((d.prerequisites & Bit(parent.id))!=0) names+=(names.Length==0?"":"、")+parent.name;
                return "需要前置研究："+names;
            }
            if(!EquipmentCatalog.HasWorkbench(s,p)) return "需要近距可见工作台。";
            string milestone=ResearchMilestone(s,d.id);
            if(milestone.Length!=0) return milestone;
            for(int i=0;i<d.cost.Length;i++) if(p.items[i]<d.cost[i]) return "研究材料不足："+DemoCatalog.ItemNames[i];
            return "";
        }
        static string ResearchMilestone(WorldState s, SteamTech id)
        {
            if(id==SteamTech.Coking && !PrimitiveCatalog.HasResearch(s,PrimitiveTech.Charcoal)) return "需要原始研究：炭化。";
            if(id==SteamTech.Refractories && !PrimitiveCatalog.HasResearch(s,PrimitiveTech.Ceramics)) return "需要原始研究：陶砖烧制。";
            if(id==SteamTech.Coking || id==SteamTech.Refractories) return WaterCatalog.SteamRequirement(s);
            if(id==SteamTech.PigIron)
            {
                if(!WaterCatalog.HasResearch(s,WaterTech.Blast) || !WaterCatalog.HasResearch(s,WaterTech.Crushing)) return "需要水力鼓风冶炼、机械破碎。";
                if(s.steamCoked<3 || s.steamRefractory<4) return "需要实际焦化3焦炭、烧制4耐火砖。";
            }
            if(id==SteamTech.Refining && s.steamPigIron<3) return "需要高炉实际炼出3生铁。";
            if(id==SteamTech.Machining)
            {
                if(!WaterCatalog.HasResearch(s,WaterTech.Pressing) || !WaterCatalog.HasResearch(s,WaterTech.Components)) return "需要水力锻造压板、齿轮轴承。";
                if(s.steamSteel<2) return "需要实际精炼2钢。";
            }
            if(id==SteamTech.Piping && s.steamPlates<2) return "需要机械压机实际压制2钢板。";
            if(id==SteamTech.Boiler && (s.steamPlates<2 || s.steamFittings<2)) return "需要实际压制2钢板、机床加工2耐压管件。";
            return "";
        }
        static int BuildResearch(BuildKind kind)
        {
            switch(kind)
            {
                case BuildKind.CokeOven: return (int)SteamTech.Coking;
                case BuildKind.RefractoryKiln: return (int)SteamTech.Refractories;
                case BuildKind.IronBlastFurnace: return (int)SteamTech.PigIron;
                case BuildKind.RefiningForge: return (int)SteamTech.Refining;
                case BuildKind.MachineTool: return (int)SteamTech.Machining;
                case BuildKind.SteamPipe: return (int)SteamTech.Piping;
                case BuildKind.Boiler: case BuildKind.CombustionChamber: case BuildKind.Chimney: case BuildKind.FeedwaterPump: return (int)SteamTech.Boiler;
                case BuildKind.SteamEngine: case BuildKind.SteamMiner: case BuildKind.SteamFurnace: return (int)SteamTech.Drive;
                default: return -1;
            }
        }
        public static string BuildRequirement(WorldState s, BuildKind kind)
        {
            if(s==null || (int)kind<0 || (int)kind>=DemoCatalog.BuildNames.Length) return "无效建筑。";
            if(s.era<TechCatalog.RequiredEra(kind)) return "需要科技时代："+TechCatalog.EraName(TechCatalog.RequiredEra(kind));
            int id=BuildResearch(kind);
            return id<0 || HasResearch(s,id) ? LowVoltageCatalog.BuildRequirement(s,kind) : "需要蒸汽研究："+Definition(id).name;
        }
        public static bool IsBuildUnlocked(WorldState s, BuildKind kind) => BuildRequirement(s,kind).Length==0;
        public static string RecipeRequirement(WorldState s, RecipeDefinition recipe)
        {
            if(s==null || recipe==null) return "无效配方。";
            if(s.era<recipe.era) return "需要科技时代："+TechCatalog.EraName(recipe.era);
            int id=recipe.id==124 ? (int)SteamTech.Machining : recipe.id==125 ? (int)SteamTech.Piping : BuildResearch(recipe.building);
            return id<0 || HasResearch(s,id) ? LowVoltageCatalog.RecipeRequirement(s,recipe) : "需要蒸汽研究："+Definition(id).name;
        }
        public static bool IsRecipeUnlocked(WorldState s, RecipeDefinition recipe) => RecipeRequirement(s,recipe).Length==0;
        public static string LowVoltageRequirement(WorldState s)
        {
            if(s==null) return "无效世界。";
            if((s.steamResearch & 255)!=255) return "低压入口需要完成8项蒸汽工艺研究。";
            if(s.steamCoked<6 || s.steamRefractory<8 || s.steamPigIron<6 || s.steamSteel<4 || s.steamPlates<4 || s.steamFittings<4 || s.steamDriven<4)
                return "实际工艺验收：焦炭"+s.steamCoked+"/6、耐火砖"+s.steamRefractory+"/8、生铁"+s.steamPigIron+"/6、钢"+s.steamSteel+"/4、钢板"+s.steamPlates+"/4、管件"+s.steamFittings+"/4、蒸汽引擎经轴加工"+s.steamDriven+"/4。";
            return "";
        }
    }
}
