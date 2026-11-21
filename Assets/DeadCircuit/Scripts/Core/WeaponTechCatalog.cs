namespace DeadCircuit
{
    public enum WeaponTech
    {
        BasicAmmunition = 0, Sidearms = 1, Shotguns = 2, BasicFittings = 3, ReflexOptics = 4,
        AutomaticWeapons = 5, TacticalFittings = 6, MagnifiedOptics = 7, PrecisionAmmunition = 8,
        PrecisionWeapons = 9, MatchBarrels = 10, ArmorPiercing = 11
    }

    public static class WeaponTechCatalog
    {
        public const int AllResearch = 4095;
        static ResearchRef I(TechEra era,int id) => new ResearchRef { domain=ResearchDomain.Industry,era=(int)era,id=id };
        static ResearchRef W(WeaponTech id) => new ResearchRef { domain=ResearchDomain.Weapon,era=id<=WeaponTech.ReflexOptics ? 3 : id<=WeaponTech.PrecisionAmmunition ? 4 : 5,id=(int)id };
        static EquipmentTechnologyDefinition T(WeaponTech id,TechEra era,string name,string description,ResearchRef[] parents,int[] cost,int[] production) =>
            new EquipmentTechnologyDefinition { domain=ResearchDomain.Weapon,id=(int)id,era=(int)era,name=name,description=description,prerequisites=parents,cost=cost,production=production };

        public static readonly EquipmentTechnologyDefinition[] All =
        {
            T(WeaponTech.BasicAmmunition,TechEra.LowVoltage,"基础弹药","解锁普通弹、步枪弹和霰弹的手工及工业制作资格。",
                new[] { I(TechEra.Steam,(int)SteamTech.Machining) },
                DemoCatalog.Materials(ItemKind.Steel,2,ItemKind.Copper,2),DemoCatalog.Materials(ItemKind.Steel,4,ItemKind.Copper,4)),
            T(WeaponTech.Sidearms,TechEra.LowVoltage,"手枪制造","解锁GLOCK 17手枪制作。",
                new[] { W(WeaponTech.BasicAmmunition),I(TechEra.Steam,(int)SteamTech.Machining) },
                DemoCatalog.Materials(ItemKind.Steel,2,ItemKind.Copper,1),DemoCatalog.Materials(ItemKind.Steel,6)),
            T(WeaponTech.Shotguns,TechEra.LowVoltage,"霰弹枪制造","解锁Mossberg 590霰弹枪制作。",
                new[] { W(WeaponTech.BasicAmmunition),I(TechEra.Steam,(int)SteamTech.Machining) },
                DemoCatalog.Materials(ItemKind.Steel,3,ItemKind.Wood,2),DemoCatalog.Materials(ItemKind.Steel,6)),
            T(WeaponTech.BasicFittings,TechEra.LowVoltage,"基础安装结构","解锁制退器和扩容弹匣制作。",
                new[] { W(WeaponTech.BasicAmmunition),I(TechEra.Steam,(int)SteamTech.Machining) },
                DemoCatalog.Materials(ItemKind.Steel,2,ItemKind.Copper,1),DemoCatalog.Materials(ItemKind.SteelPlate,2)),
            T(WeaponTech.ReflexOptics,TechEra.LowVoltage,"电子瞄具","解锁红点瞄具制作。",
                new[] { W(WeaponTech.BasicFittings),I(TechEra.LowVoltage,(int)LowVoltageTech.Relays) },
                DemoCatalog.Materials(ItemKind.Circuit,1,ItemKind.Copper,1),DemoCatalog.Materials(ItemKind.Circuit,2)),
            T(WeaponTech.AutomaticWeapons,TechEra.MediumVoltage,"自动武器","解锁M4A1卡宾枪制作。",
                new[] { W(WeaponTech.BasicAmmunition),I(TechEra.MediumVoltage,(int)PetrochemicalTech.Polymerization),I(TechEra.Steam,(int)SteamTech.Machining) },
                DemoCatalog.Materials(ItemKind.Steel,3,ItemKind.Polymer,2,ItemKind.Circuit,1),DemoCatalog.Materials(ItemKind.Polymer,2,ItemKind.Circuit,2)),
            T(WeaponTech.TacticalFittings,TechEra.MediumVoltage,"战术配件","解锁消音器和人体工学握把制作。",
                new[] { W(WeaponTech.BasicFittings),I(TechEra.MediumVoltage,(int)PetrochemicalTech.Polymerization),I(TechEra.MediumVoltage,(int)PetrochemicalTech.Lubrication) },
                DemoCatalog.Materials(ItemKind.Polymer,2,ItemKind.Lubricant,1,ItemKind.Steel,1),DemoCatalog.Materials(ItemKind.Polymer,4,ItemKind.Lubricant,2)),
            T(WeaponTech.MagnifiedOptics,TechEra.MediumVoltage,"倍率瞄具","解锁四倍瞄具制作。",
                new[] { W(WeaponTech.ReflexOptics),I(TechEra.MediumVoltage,(int)PetrochemicalTech.Polymerization) },
                DemoCatalog.Materials(ItemKind.Circuit,2,ItemKind.Polymer,2),DemoCatalog.Materials(ItemKind.Polymer,4,ItemKind.Circuit,2)),
            T(WeaponTech.PrecisionAmmunition,TechEra.MediumVoltage,"精密弹药","解锁精密弹制作，可在高压步枪制造前储备。",
                new[] { W(WeaponTech.BasicAmmunition),I(TechEra.MediumVoltage,(int)PetrochemicalTech.Lubrication) },
                DemoCatalog.Materials(ItemKind.Steel,2,ItemKind.Lubricant,1),DemoCatalog.Materials(ItemKind.Steel,8,ItemKind.Lubricant,2)),
            T(WeaponTech.PrecisionWeapons,TechEra.HighVoltage,"精密武器","解锁M24狙击步枪制作。",
                new[] { W(WeaponTech.PrecisionAmmunition),I(TechEra.HighVoltage,(int)PrecisionTech.Circuits),I(TechEra.HighVoltage,(int)PrecisionTech.Assembly) },
                DemoCatalog.Materials(ItemKind.PrecisionPart,1,ItemKind.AdvancedCircuit,1,ItemKind.Steel,2),DemoCatalog.Materials(ItemKind.PrecisionPart,2,ItemKind.AdvancedCircuit,2)),
            T(WeaponTech.MatchBarrels,TechEra.HighVoltage,"精密枪管","解锁精密枪管配件制作。",
                new[] { W(WeaponTech.BasicFittings),I(TechEra.HighVoltage,(int)PrecisionTech.Assembly) },
                DemoCatalog.Materials(ItemKind.PrecisionPart,1,ItemKind.Lubricant,1),DemoCatalog.Materials(ItemKind.PrecisionPart,2)),
            T(WeaponTech.ArmorPiercing,TechEra.HighVoltage,"穿甲弹药","解锁穿甲弹制作。",
                new[] { W(WeaponTech.PrecisionAmmunition),I(TechEra.HighVoltage,(int)PrecisionTech.Alloys) },
                DemoCatalog.Materials(ItemKind.HeatAlloy,1,ItemKind.Steel,2),DemoCatalog.Materials(ItemKind.HeatAlloy,2))
        };

        public static EquipmentTechnologyDefinition Definition(int id) => id>=0 && id<All.Length ? All[id] : null;
        public static bool HasResearch(WorldState world,int id) => world!=null && Definition(id)!=null && (world.weaponResearch & (1<<id))!=0;
        public static string ResearchRequirement(WorldState world,PlayerState player,int id) => EquipmentResearchCatalog.Requirement(world,player,Reference(id));
        public static string ResearchEvidence(WorldState world,int id) => EquipmentResearchCatalog.Evidence(world,Reference(id));
        internal static ResearchRef Reference(int id) => Definition(id)==null ? null : new ResearchRef { domain=ResearchDomain.Weapon,era=Definition(id).era,id=id };
    }
}
