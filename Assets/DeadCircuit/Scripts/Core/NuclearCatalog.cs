namespace DeadCircuit
{
    public enum NuclearTech
    {
        UraniumEnrichment, FuelAssembly, CoreDiagnostics, ShieldedStorage,
        HeatResistantStructures, ClosedCooling, HeatConversion, WasteTransfer,
        UraniumBeneficiation, RadiationMonitoring, RadiationProtection
    }

    public sealed class NuclearTechnologyDefinition
    {
        public NuclearTech id;
        public string name, description;
        public int prerequisites;
        public int[] cost;
    }

    public static class NuclearCatalog
    {
        public const int AllResearch = (1 << 11) - 1;
        static int Bit(NuclearTech id) => 1 << (int)id;
        static NuclearTechnologyDefinition T(NuclearTech id, string name, string description, int parents, int[] cost) =>
            new NuclearTechnologyDefinition { id=id, name=name, description=description, prerequisites=parents, cost=cost };

        public static readonly NuclearTechnologyDefinition[] All =
        {
            T(NuclearTech.UraniumEnrichment, "铀矿富集", "用耐热合金优化燃料组件，解锁高效核燃料配方。", Bit(NuclearTech.UraniumBeneficiation),
                DemoCatalog.Materials(ItemKind.ControlModule,1,ItemKind.HeatAlloy,2)),
            T(NuclearTech.FuelAssembly, "高密度燃料组件", "改进燃料组件封装，使单份核燃料维持反应堆运行30秒。", Bit(NuclearTech.UraniumEnrichment),
                DemoCatalog.Materials(ItemKind.NuclearFuel,2,ItemKind.SteelPlate,2)),
            T(NuclearTech.CoreDiagnostics, "堆芯监测", "完成研究后，在反应堆检查面板查看燃料、冷却、外部联锁与废料仓状态。", Bit(NuclearTech.UraniumEnrichment),
                DemoCatalog.Materials(ItemKind.ControlModule,1,ItemKind.AdvancedCircuit,2)),
            T(NuclearTech.ShieldedStorage, "屏蔽仓体", "加厚专用仓屏蔽层，将单仓核废料容量从200提高到400。", Bit(NuclearTech.CoreDiagnostics),
                DemoCatalog.Materials(ItemKind.RefractoryBrick,4,ItemKind.HeatAlloy,2)),
            T(NuclearTech.HeatResistantStructures, "耐热结构", "增强反应堆结构，使自动停堆温度从900℃提高到1100℃。", Bit(NuclearTech.UraniumEnrichment),
                DemoCatalog.Materials(ItemKind.SteelPlate,4,ItemKind.HeatAlloy,2)),
            T(NuclearTech.ClosedCooling, "闭式冷却", "优化冷却剂循环，使每单位冷却液维持时间从2秒延长到4秒。", Bit(NuclearTech.HeatResistantStructures),
                DemoCatalog.Materials(ItemKind.Coolant,4,ItemKind.ProcessWater,4)),
            T(NuclearTech.HeatConversion, "热电转化", "回收更多堆芯热能，将反应堆输出从1200千瓦提高到1500千瓦。", Bit(NuclearTech.ClosedCooling),
                DemoCatalog.Materials(ItemKind.ControlModule,2,ItemKind.HeatAlloy,2)),
            T(NuclearTech.WasteTransfer, "专用废料转运", "允许核废料在相邻专用仓之间自动转运，仍禁止普通物流和背包收发。", Bit(NuclearTech.ShieldedStorage),
                DemoCatalog.Materials(ItemKind.SteelPlate,6,ItemKind.RefractoryBrick,4,ItemKind.ControlModule,1)),
            T(NuclearTech.UraniumBeneficiation, "铀矿选冶", "先采集至少4份铀矿，再研究化学浸取；在化工厂将铀矿处理为铀精矿，后续核燃料配方须使用精矿。", 0,
                DemoCatalog.Materials(ItemKind.UraniumOre,2,ItemKind.SteelPlate,2)),
            T(NuclearTech.RadiationMonitoring, "辐射监测", "开放核设施附近的环境暴露率、个人累计暴露值与防护读数；可在首次装料前完成。", Bit(NuclearTech.UraniumBeneficiation),
                DemoCatalog.Materials(ItemKind.ControlModule,1,ItemKind.AdvancedCircuit,2,ItemKind.CopperWire,4)),
            T(NuclearTech.RadiationProtection, "核作业防护", "解锁核作业防护服制作；正确穿戴且耐久有效时减少80%辐射暴露，持续作业会磨损。", Bit(NuclearTech.RadiationMonitoring),
                DemoCatalog.Materials(ItemKind.Polymer,4,ItemKind.SteelPlate,2,ItemKind.Insulator,2))
        };

        public static NuclearTechnologyDefinition Definition(int id) => id>=0 && id<All.Length ? All[id] : null;
        public static bool HasResearch(WorldState s,int id) => s!=null && Definition(id)!=null && (s.nuclearResearch & (1<<id))!=0;
        public static bool HasResearch(WorldState s,NuclearTech id) => HasResearch(s,(int)id);
        public static bool CanResearch(WorldState s,PlayerState p,int id) => ResearchRequirement(s,p,id).Length==0;

        static int Produced(WorldState s,ItemKind item) =>
            s.produced!=null && s.produced.Length>(int)item ? s.produced[(int)item] : 0;

        public static string ResearchEvidence(WorldState s,NuclearTech id)
        {
            if(s==null) return "世界尚未就绪。";
            switch(id)
            {
                case NuclearTech.UraniumBeneficiation:
                case NuclearTech.RadiationMonitoring:
                case NuclearTech.RadiationProtection:
                    return s.nuclearUraniumMined<4 ? "需累计实际开采铀矿×4。" : "";
                case NuclearTech.UraniumEnrichment:
                case NuclearTech.CoreDiagnostics:
                    return Produced(s,ItemKind.NuclearFuel)<2 ? "需累计生产核燃料×2。" : "";
                case NuclearTech.FuelAssembly:
                    return Produced(s,ItemKind.NuclearWaste)<2 ? "需累计运行反应堆并产生核废料×2。" : "";
                case NuclearTech.ShieldedStorage:
                    return Produced(s,ItemKind.NuclearWaste)<4 ? "需累计产生核废料×4。" : "";
                case NuclearTech.HeatResistantStructures:
                    return Produced(s,ItemKind.HotCoolant)<4 ? "需累计产生热冷却液×4。" : "";
                case NuclearTech.ClosedCooling:
                    return Produced(s,ItemKind.HotCoolant)<8 || Produced(s,ItemKind.Coolant)<4
                        ? "需累计产生热冷却液×8、冷却液×4。" : "";
                case NuclearTech.HeatConversion:
                    return Produced(s,ItemKind.HotCoolant)<12 ? "需累计产生热冷却液×12。" : "";
                case NuclearTech.WasteTransfer:
                    return Produced(s,ItemKind.NuclearWaste)<8 ? "需累计产生核废料×8。" : "";
                default: return "无法识别这项核工业研究。";
            }
        }

        public static string ResearchRequirement(WorldState s,PlayerState p,int id)
        {
            var d=Definition(id);
            if(s==null || d==null) return "无法识别这项核工业研究。";
            if(s.era<TechEra.Nuclear) return "请先进入核工业时代。";
            if(p==null || s.players==null || !s.players.Contains(p) || !p.connected || p.hp<=0f ||
                p.items==null || p.items.Length!=DemoCatalog.ItemCount) return "当前角色无法研究，请确认角色已连接且仍存活。";
            if(HasResearch(s,id)) return "全队已完成此项研究。";
            if(p.interactCooldown>0f) return "操作尚未结束，请稍候。";
            if((s.nuclearResearch & d.prerequisites)!=d.prerequisites)
            {
                string names="";
                foreach(var parent in All)
                    if((d.prerequisites & Bit(parent.id))!=0 && !HasResearch(s,parent.id)) names+=(names.Length==0?"":"、")+parent.name;
                return "需要前置研究："+names;
            }
            if(!EquipmentCatalog.HasWorkbench(s,p)) return "请靠近工作台，并确保中间没有障碍。";
            string evidence=ResearchEvidence(s,d.id);
            if(evidence.Length>0) return evidence;
            for(int i=0;i<d.cost.Length;i++) if(p.items[i]<d.cost[i]) return "研究材料不足："+DemoCatalog.ItemNames[i];
            return "";
        }

        public static string RecipeRequirement(WorldState s,RecipeDefinition recipe)
        {
            if(recipe==null) return "无法识别这项配方。";
            if(recipe.id==93 && !HasResearch(s,NuclearTech.UraniumBeneficiation))
                return "需要核工业研究：铀矿选冶";
            return recipe.id==92 && !HasResearch(s,NuclearTech.UraniumEnrichment)
                ? "需要核工业研究：铀矿富集" : "";
        }

        public static bool IsRecipeUnlocked(WorldState s,RecipeDefinition recipe) => RecipeRequirement(s,recipe).Length==0;
        public static float FuelSeconds(WorldState s) => HasResearch(s,NuclearTech.FuelAssembly) ? 30f : 20f;
        public static float CoolantSeconds(WorldState s) => HasResearch(s,NuclearTech.ClosedCooling) ? 4f : 2f;
        public static float TripTemperature(WorldState s) => HasResearch(s,NuclearTech.HeatResistantStructures) ? 1100f : 900f;
        public static float PowerOutput(WorldState s) => HasResearch(s,NuclearTech.HeatConversion) ? 1500f : 1200f;
        public static bool CanTransferVaultWaste(WorldState s) => HasResearch(s,NuclearTech.WasteTransfer);
        public static int WasteVaultCapacity(WorldState s) => WorldSimulator.BufferLimit *
            (HasResearch(s,NuclearTech.ShieldedStorage) ? 2 : 1);
        public static int ItemCapacity(WorldState s,BuildKind kind,int item) =>
            kind==BuildKind.NuclearWasteVault && item==(int)ItemKind.NuclearWaste
                ? WasteVaultCapacity(s) : WorldSimulator.BufferLimit;
    }
}
