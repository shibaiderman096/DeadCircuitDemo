using System;

namespace DeadCircuit
{
    public enum LowVoltageTech { Generation, Cabling, Motors, Processing, Relays, Assembly, Sorting, Protection }
    [Serializable] public sealed class LowVoltageTechnologyDefinition
    {
        public LowVoltageTech id;
        public string name, description;
        public int prerequisites;
        public int[] cost;
    }
    public static class LowVoltageCatalog
    {
        static int Bit(LowVoltageTech id) => 1 << (int)id;
        static LowVoltageTechnologyDefinition T(LowVoltageTech id,string name,string description,int prerequisites,int[] cost) =>
            new LowVoltageTechnologyDefinition { id=id,name=name,description=description,prerequisites=prerequisites,cost=cost };
        public static readonly LowVoltageTechnologyDefinition[] All =
        {
            T(LowVoltageTech.Generation,"发电配电","先用陶窑与机床制作绝缘件、铜线，再用机械绕线台制作发电线圈。",0,
                DemoCatalog.Materials(ItemKind.CopperWire,4,ItemKind.Insulator,2,ItemKind.Steel,2)),
            T(LowVoltageTech.Cabling,"绝缘电缆","铜线与陶瓷绝缘构成早期电缆；无需聚合物。",0,
                DemoCatalog.Materials(ItemKind.CopperWire,4,ItemKind.Insulator,2)),
            T(LowVoltageTech.Motors,"电动机","线圈、铁芯、绝缘及齿轮轴承装配首批电机。",Bit(LowVoltageTech.Generation)|Bit(LowVoltageTech.Cabling),
                DemoCatalog.Materials(ItemKind.Coil,1,ItemKind.Cable,2,ItemKind.IronPlate,2)),
            T(LowVoltageTech.Processing,"电动加工","电机与配电支撑电动破碎、采矿及电炉加工。",Bit(LowVoltageTech.Generation)|Bit(LowVoltageTech.Cabling)|Bit(LowVoltageTech.Motors),
                DemoCatalog.Materials(ItemKind.Motor,1,ItemKind.Cable,2)),
            T(LowVoltageTech.Relays,"继电电路","先手工制作继电器，再用电路装配机制造基础电路。",Bit(LowVoltageTech.Generation)|Bit(LowVoltageTech.Cabling),
                DemoCatalog.Materials(ItemKind.Coil,1,ItemKind.CopperWire,2,ItemKind.Insulator,1)),
            T(LowVoltageTech.Assembly,"自动装配","用实际电动加工与基础电路验证，解锁部件批量装配和防御供弹。",Bit(LowVoltageTech.Processing)|Bit(LowVoltageTech.Relays),
                DemoCatalog.Materials(ItemKind.Circuit,2,ItemKind.Motor,1)),
            T(LowVoltageTech.Sorting,"分流过滤","带电分流器将选定物料送前方，其余送左侧；堵塞不混线。",Bit(LowVoltageTech.Relays),
                DemoCatalog.Materials(ItemKind.Circuit,2,ItemKind.Cable,2)),
            T(LowVoltageTech.Protection,"断路保护","同一LV连通网过载跳闸，降载后人工复位；与其他电网分开保护。",Bit(LowVoltageTech.Generation)|Bit(LowVoltageTech.Cabling)|Bit(LowVoltageTech.Relays),
                DemoCatalog.Materials(ItemKind.Relay,2,ItemKind.Cable,2))
        };
        public static LowVoltageTechnologyDefinition Definition(int id) => id>=0 && id<All.Length ? All[id] : null;
        public static bool HasResearch(WorldState s,int id) => s!=null && Definition(id)!=null && (s.lvResearch & (1<<id))!=0;
        public static bool HasResearch(WorldState s,LowVoltageTech id) => HasResearch(s,(int)id);
        public static bool CanResearch(WorldState s,PlayerState p,int id) => ResearchRequirement(s,p,id).Length==0;
        public static string ResearchRequirement(WorldState s,PlayerState p,int id)
        {
            var d=Definition(id);
            if(s==null || d==null) return "无效低压科技。";
            if(s.era<TechEra.LowVoltage) return "先完成蒸汽验收并进入LV低压时代。";
            if(p==null || !s.players.Contains(p) || !p.connected || !(p.hp>0f) || p.items==null || p.items.Length!=DemoCatalog.ItemCount) return "研究者无效。";
            if(HasResearch(s,id)) return "全队已完成此项研究。";
            if(p.interactCooldown>0f) return "请等待交互冷却。";
            if((s.lvResearch & d.prerequisites)!=d.prerequisites)
            {
                string names="";
                foreach(var parent in All) if((d.prerequisites & Bit(parent.id))!=0) names+=(names.Length==0?"":"、")+parent.name;
                return "需要前置研究："+names;
            }
            if(!EquipmentCatalog.HasWorkbench(s,p)) return "需要近距可见工作台。";
            string evidence=ResearchEvidence(s,d.id);
            if(evidence.Length!=0) return evidence;
            for(int i=0;i<d.cost.Length;i++) if(p.items[i]<d.cost[i]) return "研究材料不足："+DemoCatalog.ItemNames[i];
            return "";
        }
        public static string ResearchEvidence(WorldState s,LowVoltageTech id)
        {
            if(s==null) return "无效世界。";
            if(SteamCatalog.LowVoltageRequirement(s).Length!=0) return SteamCatalog.LowVoltageRequirement(s);
            if((id==LowVoltageTech.Generation || id==LowVoltageTech.Cabling) && (s.lvWire<4 || s.lvInsulators<2))
                return "实际启动工艺：机床铜线"+s.lvWire+"/4、陶窑绝缘件"+s.lvInsulators+"/2。";
            if(id==LowVoltageTech.Motors && (s.lvCoils<2 || s.lvCable<4)) return "需要实际绕制2线圈、制作4电缆。";
            if(id==LowVoltageTech.Processing && s.lvMotors<2) return "需要实际装配2电机。";
            if(id==LowVoltageTech.Relays && (s.lvCoils<1 || s.lvCable<2)) return "需要实际绕制1线圈、制作2电缆。";
            if(id==LowVoltageTech.Assembly && (s.lvElectric<4 || s.lvCircuits<2)) return "需要电动设备实际产出4件、其中基础电路至少2件。";
            if((id==LowVoltageTech.Sorting || id==LowVoltageTech.Protection) && s.lvCircuits<2) return "需要实际制造2基础电路。";
            return "";
        }
        static int BuildResearch(BuildKind kind)
        {
            switch(kind)
            {
                case BuildKind.Generator: case BuildKind.PowerPole: case BuildKind.SteamTurbine: case BuildKind.WindingBench: return (int)LowVoltageTech.Generation;
                case BuildKind.Miner: case BuildKind.Smelter: case BuildKind.ElectricCrusher: return (int)LowVoltageTech.Processing;
                case BuildKind.CircuitAssembler: return (int)LowVoltageTech.Relays;
                case BuildKind.Assembler: case BuildKind.Turret: return (int)LowVoltageTech.Assembly;
                case BuildKind.FilterSplitter: return (int)LowVoltageTech.Sorting;
                case BuildKind.CircuitBreaker: return (int)LowVoltageTech.Protection;
                default: return -1;
            }
        }
        public static string BuildRequirement(WorldState s,BuildKind kind)
        {
            if(s==null || (int)kind<0 || (int)kind>=DemoCatalog.BuildNames.Length) return "无效建筑。";
            if(s.era<TechCatalog.RequiredEra(kind)) return "需要科技时代："+TechCatalog.EraName(TechCatalog.RequiredEra(kind));
            int id=BuildResearch(kind);
            return id<0 || HasResearch(s,id) ? "" : "需要低压研究："+Definition(id).name;
        }
        public static bool IsBuildUnlocked(WorldState s,BuildKind kind) => BuildRequirement(s,kind).Length==0;
        public static string RecipeRequirement(WorldState s,RecipeDefinition r)
        {
            if(s==null || r==null) return "无效配方。";
            if(s.era<r.era) return "需要科技时代："+TechCatalog.EraName(r.era);
            // Starter ceramic insulation and mechanically drawn wire predate any electrical equipment.
            int id=r.id==140 || r.id==141 ? -1 : r.id==142 ? (int)LowVoltageTech.Cabling :
                r.id==144 ? (int)LowVoltageTech.Motors : r.id==145 ? (int)LowVoltageTech.Relays :
                r.id==147 ? (int)LowVoltageTech.Processing : BuildResearch(r.building);
            return id<0 || HasResearch(s,id) ? "" : "需要低压研究："+Definition(id).name;
        }
        public static bool IsRecipeUnlocked(WorldState s,RecipeDefinition r) => RecipeRequirement(s,r).Length==0;
        public static string MediumVoltageRequirement(WorldState s)
        {
            if(s==null) return "无效世界。";
            if(s.lvResearch!=255) return "中压入口需要完成8项低压研究。";
            if(s.lvWire<12 || s.lvInsulators<8 || s.lvCable<8 || s.lvCoils<4 || s.lvMotors<2 || s.lvRelays<4 || s.lvCircuits<6 || s.lvElectric<8 || s.lvAutomated<4 || s.lvSorted<4 || s.lvProtected<4)
                return "实际低压验收：铜线"+s.lvWire+"/12、绝缘件"+s.lvInsulators+"/8、电缆"+s.lvCable+"/8、线圈"+s.lvCoils+"/4、电机"+s.lvMotors+"/2、继电器"+s.lvRelays+"/4、电路"+s.lvCircuits+"/6、电加工"+s.lvElectric+"/8、自动装配"+s.lvAutomated+"/4、过滤到货"+s.lvSorted+"/4、保护网加工"+s.lvProtected+"/4。";
            return "";
        }
    }
}
