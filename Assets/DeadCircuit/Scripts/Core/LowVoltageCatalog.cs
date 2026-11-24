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
            T(LowVoltageTech.Generation,"发电配电","解锁燃料发电机、电线杆、蒸汽轮机与绕线台；需陶瓷绝缘件与铜线。",0,
                DemoCatalog.Materials(ItemKind.CopperWire,4,ItemKind.Insulator,2,ItemKind.Steel,2)),
            T(LowVoltageTech.Cabling,"绝缘电缆","解锁绝缘电缆制作，以铜线和陶瓷绝缘件包覆导体，无需聚合物。",0,
                DemoCatalog.Materials(ItemKind.CopperWire,4,ItemKind.Insulator,2)),
            T(LowVoltageTech.Motors,"电动机","解锁电机制作，在工作台以线圈、钢、齿轮和轴承组装电机。",Bit(LowVoltageTech.Generation)|Bit(LowVoltageTech.Cabling),
                DemoCatalog.Materials(ItemKind.Coil,1,ItemKind.Cable,2,ItemKind.IronPlate,2)),
            T(LowVoltageTech.Processing,"电动加工","解锁电动破碎机、采矿机和熔炉，将机械加工升级为电力生产。",Bit(LowVoltageTech.Generation)|Bit(LowVoltageTech.Cabling)|Bit(LowVoltageTech.Motors),
                DemoCatalog.Materials(ItemKind.Motor,1,ItemKind.Cable,2)),
            T(LowVoltageTech.Relays,"继电电路","解锁继电器和电路装配机；先手工制作继电器，再供给装配机生产电路。",Bit(LowVoltageTech.Generation)|Bit(LowVoltageTech.Cabling),
                DemoCatalog.Materials(ItemKind.Coil,1,ItemKind.CopperWire,2,ItemKind.Insulator,1)),
            T(LowVoltageTech.Assembly,"自动装配","完成电力与电路后研究装配机、机枪塔，批量装配部件与弹药。",Bit(LowVoltageTech.Processing)|Bit(LowVoltageTech.Relays),
                DemoCatalog.Materials(ItemKind.Circuit,2,ItemKind.Motor,1)),
            T(LowVoltageTech.Sorting,"分流过滤","解锁过滤分流器；选定物品送前方，其余送左侧。",Bit(LowVoltageTech.Relays),
                DemoCatalog.Materials(ItemKind.Circuit,2,ItemKind.Cable,2)),
            T(LowVoltageTech.Protection,"断路保护","解锁低压断路器；电网过载自动跳闸，降载后手动复位。",Bit(LowVoltageTech.Generation)|Bit(LowVoltageTech.Cabling)|Bit(LowVoltageTech.Relays),
                DemoCatalog.Materials(ItemKind.Relay,2,ItemKind.Cable,2))
        };
        public static LowVoltageTechnologyDefinition Definition(int id) => id>=0 && id<All.Length ? All[id] : null;
        public static bool HasResearch(WorldState s,int id) => s!=null && Definition(id)!=null && (s.lvResearch & (1<<id))!=0;
        public static bool HasResearch(WorldState s,LowVoltageTech id) => HasResearch(s,(int)id);
        public static bool CanResearch(WorldState s,PlayerState p,int id) => ResearchRequirement(s,p,id).Length==0;
        public static string ResearchRequirement(WorldState s,PlayerState p,int id)
        {
            var d=Definition(id);
            if(s==null || d==null) return "无法识别这项低压研究。";
            if(s.era<TechEra.LowVoltage) return "请先完成蒸汽研究和生产目标，进入低压(LV)时代。";
            if(p==null || !s.players.Contains(p) || !p.connected || !(p.hp>0f) || p.items==null || p.items.Length!=DemoCatalog.ItemCount) return "当前角色无法研究，请确认角色已连接且仍存活。";
            if(HasResearch(s,id)) return "全队已完成此项研究。";
            if(p.interactCooldown>0f) return "操作尚未结束，请稍候。";
            if((s.lvResearch & d.prerequisites)!=d.prerequisites)
            {
                string names="";
                foreach(var parent in All) if((d.prerequisites & Bit(parent.id))!=0) names+=(names.Length==0?"":"、")+parent.name;
                return "需要前置研究："+names;
            }
            if(!EquipmentCatalog.HasResearchBench(s,p)) return "请靠近完好的研究台 2.4 格内，并确保中间没有障碍。";
            string evidence=ResearchEvidence(s,d.id);
            if(evidence.Length!=0) return evidence;
            for(int i=0;i<d.cost.Length;i++) if(p.items[i]<d.cost[i]) return "研究材料不足："+DemoCatalog.ItemNames[i];
            return "";
        }
        public static string ResearchEvidence(WorldState s,LowVoltageTech id)
        {
            if(s==null) return "世界尚未就绪。";
            if(SteamCatalog.LowVoltageRequirement(s).Length!=0) return SteamCatalog.LowVoltageRequirement(s);
            if((id==LowVoltageTech.Generation || id==LowVoltageTech.Cabling) && (s.lvWire<4 || s.lvInsulators<2))
                return "研究准备：机床累计产出铜线"+s.lvWire+"/4、陶窑累计产出陶瓷绝缘件"+s.lvInsulators+"/2。";
            if(id==LowVoltageTech.Motors && (s.lvCoils<2 || s.lvCable<4)) return "需累计绕制线圈×2、制作绝缘电缆×4。";
            if(id==LowVoltageTech.Processing && s.lvMotors<2) return "需累计装配电机×2。";
            if(id==LowVoltageTech.Relays && (s.lvCoils<1 || s.lvCable<2)) return "需累计绕制线圈×1、制作绝缘电缆×2。";
            if(id==LowVoltageTech.Assembly && (s.lvElectric<4 || s.lvCircuits<2)) return "需由低压电网供能的设备累计生产物品×4，其中电路至少×2。";
            if((id==LowVoltageTech.Sorting || id==LowVoltageTech.Protection) && s.lvCircuits<2) return "需累计制造电路×2。";
            return "";
        }
        static int BuildResearch(BuildKind kind)
        {
            switch(kind)
            {
                case BuildKind.Generator: case BuildKind.PowerPole: case BuildKind.SteamTurbine: case BuildKind.WindingBench: return (int)LowVoltageTech.Generation;
                case BuildKind.LargeOreProcessor: case BuildKind.Miner: case BuildKind.Smelter: case BuildKind.ElectricCrusher: return (int)LowVoltageTech.Processing;
                case BuildKind.CircuitAssembler: return (int)LowVoltageTech.Relays;
                case BuildKind.LargeAssemblyPlant: case BuildKind.Assembler: case BuildKind.Turret: return (int)LowVoltageTech.Assembly;
                case BuildKind.FilterSplitter: return (int)LowVoltageTech.Sorting;
                case BuildKind.CircuitBreaker: return (int)LowVoltageTech.Protection;
                default: return -1;
            }
        }
        public static string BuildRequirement(WorldState s,BuildKind kind)
        {
            if(s==null || (int)kind<0 || (int)kind>=DemoCatalog.BuildNames.Length) return "无法识别这项建筑。";
            if(s.era<TechCatalog.RequiredEra(kind)) return "需要先进入："+TechCatalog.EraName(TechCatalog.RequiredEra(kind));
            int id=BuildResearch(kind);
            return id<0 || HasResearch(s,id) ? PetrochemicalCatalog.BuildRequirement(s,kind) : "需要低压研究："+Definition(id).name;
        }
        public static bool IsBuildUnlocked(WorldState s,BuildKind kind) => BuildRequirement(s,kind).Length==0;
        public static string RecipeRequirement(WorldState s,RecipeDefinition r)
        {
            if(s==null || r==null) return "无法识别这项配方。";
            if(s.era<r.era) return "需要先进入："+TechCatalog.EraName(r.era);
            // Starter ceramic insulation and mechanically drawn wire predate any electrical equipment.
            int id=r.id==140 || r.id==141 ? -1 : r.id==142 ? (int)LowVoltageTech.Cabling :
                r.id==144 ? (int)LowVoltageTech.Motors : r.id==145 ? (int)LowVoltageTech.Relays :
                r.id==147 ? (int)LowVoltageTech.Processing : BuildResearch(r.building);
            return id<0 || HasResearch(s,id) ? PetrochemicalCatalog.RecipeRequirement(s,r) : "需要低压研究："+Definition(id).name;
        }
        public static bool IsRecipeUnlocked(WorldState s,RecipeDefinition r) => RecipeRequirement(s,r).Length==0;
        public static string MediumVoltageRequirement(WorldState s)
        {
            if(s==null) return "世界尚未就绪。";
            if(s.lvResearch!=255) return "进入中压(MV)时代前，需完成8项低压研究。";
            if(s.lvWire<12 || s.lvInsulators<8 || s.lvCable<8 || s.lvCoils<4 || s.lvMotors<2 || s.lvRelays<4 || s.lvCircuits<6 || s.lvElectric<8 || s.lvAutomated<4 || s.lvSorted<4 || s.lvProtected<4)
                return "低压生产进度：铜线"+s.lvWire+"/12、陶瓷绝缘件"+s.lvInsulators+"/8、绝缘电缆"+s.lvCable+"/8、线圈"+s.lvCoils+"/4、电机"+s.lvMotors+"/2、继电器"+s.lvRelays+"/4、电路"+s.lvCircuits+"/6、低压电力生产"+s.lvElectric+"/8、自动装配"+s.lvAutomated+"/4、过滤分流器送达"+s.lvSorted+"/4、受断路器保护的电网产出"+s.lvProtected+"/4。";
            return "";
        }
    }
}
