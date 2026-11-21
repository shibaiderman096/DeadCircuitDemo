namespace DeadCircuit
{
    public enum ToolTech { SteelTools = 0, MechanicalService = 1 }

    public static class ToolTechCatalog
    {
        public const int AllResearch = 3;
        static ResearchRef I(TechEra era,int id) => new ResearchRef { domain=ResearchDomain.Industry,era=(int)era,id=id };
        public static readonly EquipmentTechnologyDefinition[] All =
        {
            new EquipmentTechnologyDefinition { domain=ResearchDomain.Tool,id=(int)ToolTech.SteelTools,era=(int)TechEra.Steam,
                name="钢制工具",description="解锁钢斧和钢镐制作。",
                prerequisites=new[] { I(TechEra.Primitive,(int)PrimitiveTech.Tools),I(TechEra.Steam,(int)SteamTech.Refining),I(TechEra.Steam,(int)SteamTech.Machining) },
                cost=DemoCatalog.Materials(ItemKind.Steel,2,ItemKind.Gear,1),production=DemoCatalog.Materials(ItemKind.Steel,2,ItemKind.SteelPlate,2) },
            new EquipmentTechnologyDefinition { domain=ResearchDomain.Tool,id=(int)ToolTech.MechanicalService,era=(int)TechEra.Steam,
                name="机械维护",description="解锁扳手制作，保留既有维护用途。",
                prerequisites=new[] { I(TechEra.Primitive,(int)PrimitiveTech.Construction),I(TechEra.Water,(int)WaterTech.Components),I(TechEra.Steam,(int)SteamTech.Machining) },
                cost=DemoCatalog.Materials(ItemKind.Iron,2,ItemKind.Bronze,1,ItemKind.Gear,1),production=DemoCatalog.Materials(ItemKind.Gear,2,ItemKind.Bearing,2) }
        };

        public static EquipmentTechnologyDefinition Definition(int id) => id>=0 && id<All.Length ? All[id] : null;
        public static bool HasResearch(WorldState world,int id) => world!=null && Definition(id)!=null && (world.toolResearch & (1<<id))!=0;
        public static string ResearchRequirement(WorldState world,PlayerState player,int id) => EquipmentResearchCatalog.Requirement(world,player,Reference(id));
        public static string ResearchEvidence(WorldState world,int id) => EquipmentResearchCatalog.Evidence(world,Reference(id));
        internal static ResearchRef Reference(int id) => Definition(id)==null ? null : new ResearchRef { domain=ResearchDomain.Tool,era=Definition(id).era,id=id };
    }
}
