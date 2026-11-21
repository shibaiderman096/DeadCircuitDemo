using System;

namespace DeadCircuit
{
    public enum PrecisionTech { Silicon, Wafers, Devices, Circuits, Assembly, Alloys, Control, Grid }

    public sealed class PrecisionTechnologyDefinition
    {
        public PrecisionTech id;
        public string name, description;
        public int prerequisites;
        public int[] cost;
    }

    public static class PrecisionCatalog
    {
        public const int AllResearch = (1 << 8) - 1;
        public const int ControlledBuffer = 8;
        private static int Bit(PrecisionTech id) => 1 << (int)id;
        private static PrecisionTechnologyDefinition T(PrecisionTech id, string name, string description, int parents, params int[] cost) =>
            new PrecisionTechnologyDefinition { id=id, name=name, description=description, prerequisites=parents, cost=cost };

        public static readonly PrecisionTechnologyDefinition[] All =
        {
            T(PrecisionTech.Silicon,"硅材制备","将石英送入蒸汽高炉冶炼工业硅，再用中压(MV)硅材精炼机消耗工艺水提纯。",0,
                DemoCatalog.Materials(ItemKind.Quartz,4,ItemKind.Circuit,2,ItemKind.RefractoryBrick,2)),
            T(PrecisionTech.Wafers,"晶圆制造","硅材精炼机消耗冷却液切制晶圆，排出热冷却液。",Bit(PrecisionTech.Silicon),
                DemoCatalog.Materials(ItemKind.PureSilicon,2,ItemKind.Coolant,2)),
            T(PrecisionTech.Devices,"半导体器件","解锁半导体器件制造，由中压(MV)电子工台消耗晶圆、聚合物和铜线封装。",Bit(PrecisionTech.Wafers),
                DemoCatalog.Materials(ItemKind.Wafer,2,ItemKind.Polymer,2)),
            T(PrecisionTech.Circuits,"多层电路","将半导体器件与电路叠层，制成高级电路。",Bit(PrecisionTech.Devices),
                DemoCatalog.Materials(ItemKind.Semiconductor,2,ItemKind.Relay,2)),
            T(PrecisionTech.Assembly,"精密装配","解锁中压(MV)电子工台的精密部件加工；高压(HV)精密装配机还需完成高压电网研究。",Bit(PrecisionTech.Circuits),
                DemoCatalog.Materials(ItemKind.AdvancedCircuit,1,ItemKind.Lubricant,2)),
            T(PrecisionTech.Alloys,"耐热合金","蒸汽高炉消耗钢、铜锭和焦炭，冶炼耐热合金，用于高压设备制造。",Bit(PrecisionTech.Silicon),
                DemoCatalog.Materials(ItemKind.SteelPlate,3,ItemKind.RefractoryBrick,2)),
            T(PrecisionTech.Control,"自动控制","解锁中压(MV)电子工台的控制模块装配，以及精密装配机的满料停机控制。",Bit(PrecisionTech.Assembly),
                DemoCatalog.Materials(ItemKind.PrecisionPart,2,ItemKind.AdvancedCircuit,1)),
            T(PrecisionTech.Grid,"高压电网","解锁高压发电机、高压电杆、精密装配机，以及变压器的高压(HV)转中压(MV)模式。",Bit(PrecisionTech.Alloys)|Bit(PrecisionTech.Control),
                DemoCatalog.Materials(ItemKind.ControlModule,2,ItemKind.HeatAlloy,2,ItemKind.Cable,4))
        };

        public static PrecisionTechnologyDefinition Definition(int id) => id>=0 && id<All.Length ? All[id] : null;
        public static bool HasResearch(WorldState s,int id) => s!=null && Definition(id)!=null && (s.hvResearch & (1 << id))!=0;
        public static bool HasResearch(WorldState s,PrecisionTech id) => HasResearch(s,(int)id);

        private static int Produced(WorldState s,ItemKind item) => s.produced!=null && s.produced.Length>(int)item ? s.produced[(int)item] : 0;

        public static string ResearchEvidence(WorldState s,PrecisionTech id)
        {
            if(s==null) return "世界尚未就绪。";
            switch(id)
            {
                case PrecisionTech.Wafers:
                    if(Produced(s,ItemKind.IndustrialSilicon)<4 || Produced(s,ItemKind.PureSilicon)<2) return "需累计生产工业硅×4、高纯硅×2。";
                    break;
                case PrecisionTech.Devices: if(Produced(s,ItemKind.Wafer)<4) return "需累计生产晶圆×4。"; break;
                case PrecisionTech.Circuits: if(Produced(s,ItemKind.Semiconductor)<4) return "需累计生产半导体器件×4。"; break;
                case PrecisionTech.Assembly: if(Produced(s,ItemKind.AdvancedCircuit)<2) return "需累计生产高级电路×2。"; break;
                case PrecisionTech.Control: if(Produced(s,ItemKind.PrecisionPart)<4) return "需累计生产精密部件×4。"; break;
                case PrecisionTech.Grid:
                    if(Produced(s,ItemKind.ControlModule)<2 || Produced(s,ItemKind.HeatAlloy)<4) return "需累计生产控制模块×2、耐热合金×4。";
                    break;
            }
            return "";
        }

        public static string ResearchRequirement(WorldState s,PlayerState p,int id)
        {
            var d=Definition(id);
            if(s==null || d==null) return "无法识别这项高压研究。";
            if(s.era<TechEra.HighVoltage) return "请先完成石化研究和生产目标，进入高压(HV)时代。";
            if(p==null || !s.players.Contains(p) || !p.connected || !(p.hp>0) || p.items==null || p.items.Length!=DemoCatalog.ItemCount) return "当前角色无法研究，请确认角色已连接且仍存活。";
            if(HasResearch(s,id)) return "全队已完成此项研究。";
            if(p.interactCooldown>0f) return "操作尚未结束，请稍候。";
            if((s.hvResearch & d.prerequisites)!=d.prerequisites)
            {
                string names="";
                foreach(var parent in All)
                    if((d.prerequisites & Bit(parent.id))!=0 && !HasResearch(s,parent.id)) names+=(names.Length==0?"":"、")+parent.name;
                return "需要前置研究："+names;
            }
            if(!EquipmentCatalog.HasResearchBench(s,p)) return "请靠近完好的研究台 2.4 格内，并确保中间没有障碍。";
            string evidence=ResearchEvidence(s,d.id);
            if(evidence.Length>0) return evidence;
            for(int i=0;i<d.cost.Length;i++) if(p.items[i]<d.cost[i]) return "研究材料不足："+DemoCatalog.ItemNames[i];
            return "";
        }

        private static string Missing(WorldState s,PrecisionTech id) => HasResearch(s,id) ? "" : "需要高压研究："+Definition((int)id).name;

        public static string BuildRequirement(WorldState s,BuildKind kind)
        {
            switch(kind)
            {
                case BuildKind.SiliconRefiner: return Missing(s,PrecisionTech.Silicon);
                case BuildKind.ElectronicsBench: return Missing(s,PrecisionTech.Devices);
                case BuildKind.SolarPanel: return Missing(s,PrecisionTech.Devices);
                case BuildKind.HighGenerator: case BuildKind.HighPole: case BuildKind.PrecisionAssembler: return Missing(s,PrecisionTech.Grid);
                default: return "";
            }
        }

        public static string RecipeRequirement(WorldState s,RecipeDefinition recipe)
        {
            if(recipe==null) return "无法识别这项配方。";
            switch(recipe.id)
            {
                case 170: case 171: return Missing(s,PrecisionTech.Silicon);
                case 172: return Missing(s,PrecisionTech.Wafers);
                case 173: return Missing(s,PrecisionTech.Devices);
                case 174: return Missing(s,PrecisionTech.Circuits);
                case 175: return Missing(s,PrecisionTech.Assembly);
                case 176: return Missing(s,PrecisionTech.Control);
                case 177: return Missing(s,PrecisionTech.Alloys);
                default: return NuclearCatalog.RecipeRequirement(s,recipe);
            }
        }

        public static string NuclearRequirement(WorldState s)
        {
            if(s==null || s.hvResearch!=AllResearch) return "进入核工业时代前，需完成8项高压研究。";
            if(s.hvPowered<4 || Produced(s,ItemKind.ControlModule)<4 || Produced(s,ItemKind.HeatAlloy)<6)
                return $"高压生产进度：精密装配机产出控制模块 {s.hvPowered}/4、控制模块 {Produced(s,ItemKind.ControlModule)}/4、耐热合金 {Produced(s,ItemKind.HeatAlloy)}/6。";
            return "";
        }
    }
}
