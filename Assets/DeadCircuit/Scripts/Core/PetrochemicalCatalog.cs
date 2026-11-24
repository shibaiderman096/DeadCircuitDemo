using System;

namespace DeadCircuit
{
    public enum PetrochemicalTech { Prospecting, Extraction, Distillation, Refining, Polymerization, Lubrication, Transformation, Cooling }
    [Serializable] public sealed class PetrochemicalTechnologyDefinition
    {
        public PetrochemicalTech id;
        public string name, description;
        public int prerequisites;
        public int[] cost;
    }
    public static class PetrochemicalCatalog
    {
        static int Bit(PetrochemicalTech id) => 1 << (int)id;
        static PetrochemicalTechnologyDefinition T(PetrochemicalTech id,string name,string description,int parents,int[] cost) =>
            new PetrochemicalTechnologyDefinition {id=id,name=name,description=description,prerequisites=parents,cost=cost};
        public static readonly PetrochemicalTechnologyDefinition[] All =
        {
            T(PetrochemicalTech.Prospecting,"石油勘探","勘探48格内油藏，标出坐标与方位；荒漠、湿地、沼泽、河谷较多。",0,
                DemoCatalog.Materials(ItemKind.Circuit,2,ItemKind.Motor,1,ItemKind.PipeFitting,2)),
            T(PetrochemicalTech.Extraction,"抽油输液","解锁抽油机、工艺取水泵、液体管道与储液罐；油藏有限，管道只走选定液体。",Bit(PetrochemicalTech.Prospecting),
                DemoCatalog.Materials(ItemKind.Motor,1,ItemKind.PipeFitting,2)),
            T(PetrochemicalTech.Distillation,"原油分馏","解锁分馏装置；靠水源烧燃料分离原油，产物满仓即停。",Bit(PetrochemicalTech.Extraction),
                DemoCatalog.Materials(ItemKind.CrudeOil,4,ItemKind.RefractoryBrick,2)),
            T(PetrochemicalTech.Refining,"燃料精制","解锁柴油精制：基础化学反应器把轻质馏分转为柴油。",Bit(PetrochemicalTech.Distillation),
                DemoCatalog.Materials(ItemKind.LightFraction,2)),
            T(PetrochemicalTech.Polymerization,"聚合反应","轻质馏分＋工艺水→化工原料，再聚合为聚合物。",Bit(PetrochemicalTech.Distillation),
                DemoCatalog.Materials(ItemKind.ChemicalFeedstock,1,ItemKind.ProcessWater,2)),
            T(PetrochemicalTech.Lubrication,"工业润滑","重质馏分炼润滑油；与煤混合制成分馏用燃料。",Bit(PetrochemicalTech.Distillation)|Bit(PetrochemicalTech.Refining),
                DemoCatalog.Materials(ItemKind.HeavyFraction,1,ItemKind.Diesel,1)),
            T(PetrochemicalTech.Transformation,"中压变电","解锁柴油发电机、中压电杆、化工厂与变压器；中压供电，可降压接低压网。",Bit(PetrochemicalTech.Polymerization)|Bit(PetrochemicalTech.Lubrication),
                DemoCatalog.Materials(ItemKind.Polymer,2,ItemKind.Lubricant,1,ItemKind.Cable,2)),
            T(PetrochemicalTech.Cooling,"化工冷却","基础化学反应器制冷却液供中压化工；热冷却液在冷却塔回收。",Bit(PetrochemicalTech.Extraction)|Bit(PetrochemicalTech.Polymerization),
                DemoCatalog.Materials(ItemKind.Motor,1,ItemKind.Coolant,2))
        };
        public static PetrochemicalTechnologyDefinition Definition(int id) => id>=0 && id<All.Length ? All[id] : null;
        public static bool HasResearch(WorldState s,int id) => s!=null && Definition(id)!=null && (s.mvResearch & (1<<id))!=0;
        public static bool HasResearch(WorldState s,PetrochemicalTech id) => HasResearch(s,(int)id);
        public static bool CanResearch(WorldState s,PlayerState p,int id) => ResearchRequirement(s,p,id).Length==0;
        public static string ResearchRequirement(WorldState s,PlayerState p,int id)
        {
            var d=Definition(id);
            if(s==null || d==null) return "无法识别这项石化研究。";
            if(s.era<TechEra.MediumVoltage) return "请先完成低压研究和生产目标，进入中压(MV)时代。";
            if(p==null || !s.players.Contains(p) || !p.connected || !(p.hp>0f) || p.items==null || p.items.Length!=DemoCatalog.ItemCount) return "当前角色无法研究，请确认角色已连接且仍存活。";
            if(HasResearch(s,id)) return "全队已完成此项研究。";
            if(p.interactCooldown>0f) return "操作尚未结束，请稍候。";
            if((s.mvResearch & d.prerequisites)!=d.prerequisites)
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
        public static string ResearchEvidence(WorldState s,PetrochemicalTech id)
        {
            if(s==null) return "世界尚未就绪。";
            string electrical=LowVoltageCatalog.MediumVoltageRequirement(s);
            if(electrical.Length!=0) return electrical;
            if(id==PetrochemicalTech.Extraction && s.mvSurvey<1) return "请先勘探到一处未耗尽的油藏。";
            if(id==PetrochemicalTech.Distillation && (s.mvExtracted<4 || s.mvPiped<2)) return "需累计抽取原油×4，并通过液体管道或储液罐送达液体×2。";
            if((id==PetrochemicalTech.Refining || id==PetrochemicalTech.Polymerization) && s.mvDistilled<4) return "需累计分馏原油×4。";
            if(id==PetrochemicalTech.Lubrication && s.mvDiesel<2) return "需累计精制柴油×2。";
            if(id==PetrochemicalTech.Transformation && (s.mvPolymer<2 || s.mvLubricant<2 || s.mvDiesel<2)) return "需累计生产聚合物×2、润滑油×2、柴油×2。";
            if(id==PetrochemicalTech.Cooling && s.mvPolymer<2) return "需累计生产聚合物×2；可用基础化学反应器制造冷却液。";
            return "";
        }
        static int BuildResearch(BuildKind kind)
        {
            switch(kind)
            {
                case BuildKind.OilPump: case BuildKind.LiquidPipe: case BuildKind.LiquidTank: case BuildKind.ProcessWaterPump: return (int)PetrochemicalTech.Extraction;
                case BuildKind.DistillationUnit: case BuildKind.ChemicalReactor: return (int)PetrochemicalTech.Distillation;
                case BuildKind.DieselGenerator: case BuildKind.MediumPole: case BuildKind.Transformer: case BuildKind.SolarPanel: case BuildKind.ChemicalPlant: return (int)PetrochemicalTech.Transformation;
                case BuildKind.CoolingTower: return (int)PetrochemicalTech.Cooling;
                default: return -1;
            }
        }
        public static string BuildRequirement(WorldState s,BuildKind kind)
        {
            if(s==null || (int)kind<0 || (int)kind>=DemoCatalog.BuildNames.Length) return "无法识别这项建筑。";
            if(s.era<TechCatalog.RequiredEra(kind)) return "需要先进入："+TechCatalog.EraName(TechCatalog.RequiredEra(kind));
            int id=BuildResearch(kind);
            return id<0 || HasResearch(s,id) ? PrecisionCatalog.BuildRequirement(s,kind) : "需要石化研究："+Definition(id).name;
        }
        public static bool IsBuildUnlocked(WorldState s,BuildKind kind) => BuildRequirement(s,kind).Length==0;
        public static string RecipeRequirement(WorldState s,RecipeDefinition r)
        {
            if(s==null || r==null) return "无法识别这项配方。";
            if(s.era<r.era) return "需要先进入："+TechCatalog.EraName(r.era);
            int id=r.id==70 || r.id==71 || r.id==160 ? (int)PetrochemicalTech.Polymerization :
                r.id==165 ? (int)PetrochemicalTech.Refining : r.id==162 || r.id==163 ? (int)PetrochemicalTech.Lubrication :
                r.id==91 || r.id==164 ? (int)PetrochemicalTech.Cooling : BuildResearch(r.building);
            if(id>=0 && !HasResearch(s,id)) return "需要石化研究："+Definition(id).name;
            string build=BuildRequirement(s,r.building);
            return build.Length>0 ? build : PrecisionCatalog.RecipeRequirement(s,r);
        }
        public static bool IsRecipeUnlocked(WorldState s,RecipeDefinition r) => RecipeRequirement(s,r).Length==0;
        public static bool IsFluid(int item) => item==(int)ItemKind.Coolant || item>=(int)ItemKind.CrudeOil && item<= (int)ItemKind.HotCoolant;
        public static bool IsLiquidStorage(BuildKind kind) => kind==BuildKind.LiquidPipe || kind==BuildKind.LiquidTank;
        public static string HighVoltageRequirement(WorldState s)
        {
            if(s==null) return "世界尚未就绪。";
            if(s.mvResearch!=255) return "进入高压(HV)时代前，需完成8项石化研究。";
            if(s.mvSurvey<1 || s.mvExtracted<12 || s.mvPiped<8 || s.mvDistilled<12 || s.mvDiesel<4 || s.mvPolymer<4 || s.mvLubricant<2 || s.mvCooled<4 || s.mvPowered<4)
                return "石化生产进度：油藏勘探"+s.mvSurvey+"/1、抽油"+s.mvExtracted+"/12、液体输送到货"+s.mvPiped+"/8、分馏原油"+s.mvDistilled+"/12、柴油"+s.mvDiesel+"/4、聚合物"+s.mvPolymer+"/4、润滑油"+s.mvLubricant+"/2、冷却液回收"+s.mvCooled+"/4、中压化工厂产出聚合物"+s.mvPowered+"/4。";
            return "";
        }
    }
}
