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
            T(SteamTech.Coking,"焦化","解锁焦炉，消耗煤和木炭炼制焦炭，用于还原铁矿。",0,
                DemoCatalog.Materials(ItemKind.Coal,4,ItemKind.Brick,2)),
            T(SteamTech.Refractories,"耐火建材","解锁耐火窑，将耐火黏土烧成耐火砖，用作高温炉衬。",0,
                DemoCatalog.Materials(ItemKind.FireClay,3,ItemKind.Brick,2)),
            T(SteamTech.PigIron,"生铁冶炼","解锁炼铁高炉；水车经传动轴鼓风，碎铁矿＋焦炭＋石灰石炼生铁。",Bit(SteamTech.Coking)|Bit(SteamTech.Refractories),
                DemoCatalog.Materials(ItemKind.Coke,2,ItemKind.RefractoryBrick,2,ItemKind.Limestone,1)),
            T(SteamTech.Refining,"钢铁精炼","解锁精炼炉，由水车驱动搅炼，消耗生铁、石灰石和木炭生产钢。",Bit(SteamTech.PigIron),
                DemoCatalog.Materials(ItemKind.PigIron,2,ItemKind.Limestone,1)),
            T(SteamTech.Machining,"机床加工","解锁机床；机械压机可将钢压成钢板，为耐压设备生产材料。",Bit(SteamTech.Refining),
                DemoCatalog.Materials(ItemKind.Steel,2,ItemKind.Gear,1,ItemKind.Bearing,1)),
            T(SteamTech.Piping,"耐压管道","解锁耐压管件加工，使用机床将钢板和青铜制成蒸汽管道所需的连接件。",Bit(SteamTech.Machining),
                DemoCatalog.Materials(ItemKind.SteelPlate,2,ItemKind.Bronze,2)),
            T(SteamTech.Boiler,"模块锅炉","解锁锅炉、燃烧室、取水泵与烟囱；用耐火砖、钢板和耐压管件建造。",Bit(SteamTech.Refractories)|Bit(SteamTech.Refining)|Bit(SteamTech.Piping),
                DemoCatalog.Materials(ItemKind.SteelPlate,2,ItemKind.RefractoryBrick,2)),
            T(SteamTech.Drive,"蒸汽驱动","解锁蒸汽引擎，把蒸汽转为机械动力，可替代或补充水车。",Bit(SteamTech.Boiler)|Bit(SteamTech.Piping),
                DemoCatalog.Materials(ItemKind.PipeFitting,2,ItemKind.Gear,1,ItemKind.Bearing,1))
        };
        public static SteamTechnologyDefinition Definition(int id) => id>=0 && id<All.Length ? All[id] : null;
        public static bool HasResearch(WorldState s, int id) => s!=null && Definition(id)!=null && (s.steamResearch & (1<<id))!=0;
        public static bool HasResearch(WorldState s, SteamTech id) => HasResearch(s,(int)id);
        public static bool CanResearch(WorldState s, PlayerState p, int id) => ResearchRequirement(s,p,id).Length==0;
        public static string ResearchRequirement(WorldState s, PlayerState p, int id)
        {
            var d=Definition(id);
            if(s==null || d==null) return "无法识别这项蒸汽研究。";
            if(s.era<TechEra.Steam) return "请先完成水力研究和生产目标，进入蒸汽时代。";
            if(p==null || !s.players.Contains(p) || !p.connected || !(p.hp>0f) ||
                p.items==null || p.items.Length!=DemoCatalog.ItemCount) return "当前角色无法研究，请确认角色已连接且仍存活。";
            if(HasResearch(s,id)) return "全队已完成此项研究。";
            if(p.interactCooldown>0f) return "操作尚未结束，请稍候。";
            if((s.steamResearch & d.prerequisites)!=d.prerequisites)
            {
                string names="";
                foreach(var parent in All) if((d.prerequisites & Bit(parent.id))!=0) names+=(names.Length==0?"":"、")+parent.name;
                return "需要前置研究："+names;
            }
            if(!EquipmentCatalog.HasResearchBench(s,p)) return "请靠近完好的研究台 2.4 格内，并确保中间没有障碍。";
            string milestone=ResearchMilestone(s,d.id);
            if(milestone.Length!=0) return milestone;
            for(int i=0;i<d.cost.Length;i++) if(p.items[i]<d.cost[i]) return "研究材料不足："+DemoCatalog.ItemNames[i];
            return "";
        }
        public static string ResearchEvidence(WorldState s,int id) => s==null || Definition(id)==null ? "无法识别这项蒸汽研究。" : ResearchMilestone(s,(SteamTech)id);
        static string ResearchMilestone(WorldState s, SteamTech id)
        {
            if(id==SteamTech.Coking && !PrimitiveCatalog.HasResearch(s,PrimitiveTech.Charcoal)) return "需要原始研究：木炭烧制。";
            if(id==SteamTech.Refractories && !PrimitiveCatalog.HasResearch(s,PrimitiveTech.Ceramics)) return "需要原始研究：陶土烧结。";
            if(id==SteamTech.Coking || id==SteamTech.Refractories) return WaterCatalog.SteamRequirement(s);
            if(id==SteamTech.PigIron)
            {
                if(!WaterCatalog.HasResearch(s,WaterTech.Blast) || !WaterCatalog.HasResearch(s,WaterTech.Crushing)) return "需完成水力研究：鼓风冶炼、机械破碎。";
                if(s.steamCoked<3 || s.steamRefractory<4) return "需用焦炉累计生产焦炭×3，并用耐火窑烧制耐火砖×4。";
            }
            if(id==SteamTech.Refining && s.steamPigIron<3) return "需用炼铁高炉累计冶炼生铁×3。";
            if(id==SteamTech.Machining)
            {
                if(!WaterCatalog.HasResearch(s,WaterTech.Pressing) || !WaterCatalog.HasResearch(s,WaterTech.Components)) return "需完成水力研究：锻造压板、齿轮轴承。";
                if(s.steamSteel<2) return "需累计生产钢×2。";
            }
            if(id==SteamTech.Piping && s.steamPlates<2) return "需用机械压机累计压制钢板×2。";
            if(id==SteamTech.Boiler && (s.steamPlates<2 || s.steamFittings<2)) return "需用机械压机累计压制钢板×2，并用机床加工耐压管件×2。";
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
                case BuildKind.LargeSteamFurnace: case BuildKind.SteamEngine: case BuildKind.SteamMiner: case BuildKind.SteamFurnace: return (int)SteamTech.Drive;
                default: return -1;
            }
        }
        public static string BuildRequirement(WorldState s, BuildKind kind)
        {
            if(s==null || (int)kind<0 || (int)kind>=DemoCatalog.BuildNames.Length) return "无法识别这项建筑。";
            if(s.era<TechCatalog.RequiredEra(kind)) return "需要先进入："+TechCatalog.EraName(TechCatalog.RequiredEra(kind));
            int id=BuildResearch(kind);
            return id<0 || HasResearch(s,id) ? LowVoltageCatalog.BuildRequirement(s,kind) : "需要蒸汽研究："+Definition(id).name;
        }
        public static bool IsBuildUnlocked(WorldState s, BuildKind kind) => BuildRequirement(s,kind).Length==0;
        public static string RecipeRequirement(WorldState s, RecipeDefinition recipe)
        {
            if(s==null || recipe==null) return "无法识别这项配方。";
            if(s.era<recipe.era) return "需要先进入："+TechCatalog.EraName(recipe.era);
            int id=recipe.id==124 ? (int)SteamTech.Machining : recipe.id==125 ? (int)SteamTech.Piping : BuildResearch(recipe.building);
            return id<0 || HasResearch(s,id) ? LowVoltageCatalog.RecipeRequirement(s,recipe) : "需要蒸汽研究："+Definition(id).name;
        }
        public static bool IsRecipeUnlocked(WorldState s, RecipeDefinition recipe) => RecipeRequirement(s,recipe).Length==0;
        public static string LowVoltageRequirement(WorldState s)
        {
            if(s==null) return "世界尚未就绪。";
            if((s.steamResearch & 255)!=255) return "进入低压(LV)时代前，需完成8项蒸汽工艺研究。";
            if(s.steamCoked<6 || s.steamRefractory<8 || s.steamPigIron<6 || s.steamSteel<4 || s.steamPlates<4 || s.steamFittings<4 || s.steamDriven<4)
                return "蒸汽生产进度：焦炭"+s.steamCoked+"/6、耐火砖"+s.steamRefractory+"/8、生铁"+s.steamPigIron+"/6、钢"+s.steamSteel+"/4、钢板"+s.steamPlates+"/4、耐压管件"+s.steamFittings+"/4、蒸汽引擎经传动轴驱动的产出"+s.steamDriven+"/4。";
            return "";
        }
    }
}
