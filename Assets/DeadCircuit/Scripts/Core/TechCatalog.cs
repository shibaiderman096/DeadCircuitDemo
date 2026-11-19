using System;

namespace DeadCircuit
{
    [Serializable] public sealed class RecipeDefinition
    {
        public int id;
        public string name;
        public TechEra era;
        public BuildKind building;
        public float duration;
        public int[] inputs, outputs;
    }

    public static class TechCatalog
    {
        public static readonly string[] EraNames = { "原始", "水力", "蒸汽", "LV低压", "MV中压", "HV高压", "核工业" };
        public static string EraName(TechEra era) => (int)era >= 0 && (int)era < EraNames.Length ? EraNames[(int)era] : "未知科技";
        public static TechEra RequiredEra(BuildKind kind)
        {
            switch (kind)
            {
                case BuildKind.Wall: case BuildKind.Belt: case BuildKind.Chest: case BuildKind.Workbench: case BuildKind.StoneFurnace: return TechEra.Primitive;
                case BuildKind.Waterwheel: case BuildKind.Shaft: case BuildKind.MechanicalCrusher: return TechEra.Water;
                case BuildKind.Boiler: case BuildKind.SteamPipe: case BuildKind.SteamMiner: case BuildKind.SteamFurnace: return TechEra.Steam;
                case BuildKind.Generator: case BuildKind.Miner: case BuildKind.Smelter: case BuildKind.Assembler: case BuildKind.Turret: case BuildKind.PowerPole: case BuildKind.SteamTurbine: case BuildKind.CircuitAssembler: return TechEra.LowVoltage;
                case BuildKind.SolarPanel: case BuildKind.DieselGenerator: case BuildKind.ChemicalPlant: case BuildKind.MediumPole: case BuildKind.Transformer: case BuildKind.CoolingTower: return TechEra.MediumVoltage;
                case BuildKind.HighGenerator: case BuildKind.HighPole: case BuildKind.PrecisionAssembler: return TechEra.HighVoltage;
                case BuildKind.NuclearProcessor: case BuildKind.Reactor: return TechEra.Nuclear;
                default: return TechEra.Nuclear;
            }
        }
        public static bool IsUnlocked(WorldState s, BuildKind kind) => s != null && (int)kind >= 0 && (int)kind < DemoCatalog.BuildNames.Length && s.era >= RequiredEra(kind);
        public static int EnergyTier(BuildingState b) => b==null ? -1 : b.kind==BuildKind.Transformer ? 2+b.setting : EnergyTier(b.kind);
        public static float LinkRange(BuildKind kind) => EnergyTier(kind) >= 2 ? DemoCatalog.LinkRange : 0f;
        public static float SupplyRadius(BuildKind kind) => EnergyTier(kind) >= 2 ? DemoCatalog.SupplyRadius : 0f;
        public static bool RequiresWater(BuildKind kind) => kind==BuildKind.Waterwheel || kind==BuildKind.Boiler || kind==BuildKind.CoolingTower;
        public static bool NearWater(WorldState s,BuildingState b)
        {
            if(s==null || b==null || s.ground==null || s.ground.Length!=s.width*s.height) return false;
            int w=DemoCatalog.Width(b),h=DemoCatalog.Height(b);
            for(int y=b.y;y<b.y+h;y++) for(int x=b.x;x<b.x+w;x++)
                if(x<0 || y<0 || x>=s.width || y>=s.height || s.ground[x+y*s.width]=='2') return false;
            for(int y=b.y;y<b.y+h;y++)
                if((b.x>0 && s.ground[b.x-1+y*s.width]=='2') || (b.x+w<s.width && s.ground[b.x+w+y*s.width]=='2')) return true;
            for(int x=b.x;x<b.x+w;x++)
                if((b.y>0 && s.ground[x+(b.y-1)*s.width]=='2') || (b.y+h<s.height && s.ground[x+(b.y+h)*s.width]=='2')) return true;
            return false;
        }
        // A converter's public tier is its OUTPUT. Its input is a separate port.
        public static int EnergyTier(BuildKind kind)
        {
            switch (kind)
            {
                case BuildKind.Waterwheel: case BuildKind.Shaft: case BuildKind.MechanicalCrusher: return 0;
                case BuildKind.Boiler: case BuildKind.SteamPipe: case BuildKind.SteamMiner: case BuildKind.SteamFurnace: return 1;
                case BuildKind.Generator: case BuildKind.Miner: case BuildKind.Smelter: case BuildKind.Assembler: case BuildKind.Turret: case BuildKind.PowerPole: case BuildKind.SteamTurbine: case BuildKind.CircuitAssembler: case BuildKind.Transformer: return 2;
                case BuildKind.SolarPanel: case BuildKind.DieselGenerator: case BuildKind.ChemicalPlant: case BuildKind.MediumPole: case BuildKind.CoolingTower: return 3;
                case BuildKind.HighGenerator: case BuildKind.HighPole: case BuildKind.PrecisionAssembler: case BuildKind.NuclearProcessor: case BuildKind.Reactor: return 4;
                default: return -1;
            }
        }
        public static int[] ResearchCost(TechEra era)
        {
            switch (era)
            {
                case TechEra.Water: return DemoCatalog.Materials(ItemKind.Wood,12, ItemKind.Iron,6);
                case TechEra.Steam: return DemoCatalog.Materials(ItemKind.Iron,8, ItemKind.Bronze,6);
                case TechEra.LowVoltage: return DemoCatalog.Materials(ItemKind.Steel,6, ItemKind.Bronze,4);
                case TechEra.MediumVoltage: return DemoCatalog.Materials(ItemKind.Circuit,6, ItemKind.Steel,8);
                case TechEra.HighVoltage: return DemoCatalog.Materials(ItemKind.Polymer,8, ItemKind.Circuit,8);
                case TechEra.Nuclear: return DemoCatalog.Materials(ItemKind.AdvancedCircuit,6, ItemKind.Coolant,8, ItemKind.Steel,12);
                default: return new int[DemoCatalog.ItemCount];
            }
        }
        public static string ResearchRequirement(WorldState s, TechEra target)
        {
            if (s == null || (int)target < 1 || (int)target > 6 || (int)target != (int)s.era + 1) return "只能研究下一阶段";
            BuildKind required = target == TechEra.Water ? BuildKind.Workbench : target == TechEra.Steam ? BuildKind.MechanicalCrusher : target == TechEra.LowVoltage ? BuildKind.SteamFurnace : target == TechEra.MediumVoltage ? BuildKind.CircuitAssembler : target == TechEra.HighVoltage ? BuildKind.ChemicalPlant : BuildKind.PrecisionAssembler;
            return s.buildings.Exists(b => b != null && b.kind == required && b.hp > 0f) ? "" : "需要已建设备：" + DemoCatalog.BuildName(required);
        }
        public static bool CanResearch(WorldState s, PlayerState p, TechEra target)
        {
            if (p == null || !p.connected || p.hp <= 0f || p.items == null || p.items.Length != DemoCatalog.ItemCount || ResearchRequirement(s,target).Length != 0 || !s.players.Contains(p)) return false;
            int[] cost = ResearchCost(target);
            for (int i=0;i<cost.Length;i++) if (p.items[i] < cost[i]) return false;
            return true;
        }
        public static BuildKind[] Hotbar(TechEra era)
        {
            switch (era)
            {
                case TechEra.Water: return new[] { BuildKind.Waterwheel, BuildKind.Shaft, BuildKind.MechanicalCrusher, BuildKind.StoneFurnace, BuildKind.Workbench, BuildKind.Belt, BuildKind.Chest, BuildKind.Wall, BuildKind.Shaft, BuildKind.StoneFurnace };
                case TechEra.Steam: return new[] { BuildKind.Boiler, BuildKind.SteamPipe, BuildKind.SteamMiner, BuildKind.SteamFurnace, BuildKind.MechanicalCrusher, BuildKind.Workbench, BuildKind.Belt, BuildKind.Chest, BuildKind.Wall, BuildKind.StoneFurnace };
                case TechEra.LowVoltage: return new[] { BuildKind.Generator, BuildKind.Miner, BuildKind.Smelter, BuildKind.Assembler, BuildKind.Turret, BuildKind.PowerPole, BuildKind.CircuitAssembler, BuildKind.SteamTurbine, BuildKind.Belt, BuildKind.Chest };
                case TechEra.MediumVoltage: return new[] { BuildKind.DieselGenerator, BuildKind.SolarPanel, BuildKind.MediumPole, BuildKind.Transformer, BuildKind.ChemicalPlant, BuildKind.CoolingTower, BuildKind.CircuitAssembler, BuildKind.Belt, BuildKind.Chest, BuildKind.Turret };
                case TechEra.HighVoltage: return new[] { BuildKind.HighGenerator, BuildKind.HighPole, BuildKind.PrecisionAssembler, BuildKind.Transformer, BuildKind.ChemicalPlant, BuildKind.CoolingTower, BuildKind.Belt, BuildKind.Chest, BuildKind.MediumPole, BuildKind.Turret };
                case TechEra.Nuclear: return new[] { BuildKind.Reactor, BuildKind.NuclearProcessor, BuildKind.CoolingTower, BuildKind.HighPole, BuildKind.Transformer, BuildKind.PrecisionAssembler, BuildKind.ChemicalPlant, BuildKind.Belt, BuildKind.Chest, BuildKind.MediumPole };
                default: return new[] { BuildKind.Workbench, BuildKind.StoneFurnace, BuildKind.Chest, BuildKind.Wall, BuildKind.Belt, BuildKind.Workbench, BuildKind.StoneFurnace, BuildKind.Chest, BuildKind.Wall, BuildKind.Belt };
            }
        }
        private static RecipeDefinition R(int id, string name, TechEra era, BuildKind building, float duration, int[] inputs, int[] outputs) => new RecipeDefinition { id=id,name=name,era=era,building=building,duration=duration,inputs=inputs,outputs=outputs };
        public static readonly RecipeDefinition[] Recipes =
        {
            R(2,"青铜合金",TechEra.Water,BuildKind.Workbench,.5f,DemoCatalog.Materials(ItemKind.Copper,3,ItemKind.Tin,1),DemoCatalog.Materials(ItemKind.Bronze,4)),
            R(3,"铁锭加工废料",TechEra.Primitive,BuildKind.Workbench,.5f,DemoCatalog.Materials(ItemKind.Iron,1),DemoCatalog.Materials(ItemKind.Scrap,3)),
            R(10,"石炉炼铁",TechEra.Primitive,BuildKind.StoneFurnace,3f,DemoCatalog.Materials(ItemKind.Ore,1),DemoCatalog.Materials(ItemKind.Iron,1)),
            R(11,"石炉炼铜",TechEra.Primitive,BuildKind.StoneFurnace,3f,DemoCatalog.Materials(ItemKind.CopperOre,1),DemoCatalog.Materials(ItemKind.Copper,1)),
            R(12,"石炉炼锡",TechEra.Primitive,BuildKind.StoneFurnace,3f,DemoCatalog.Materials(ItemKind.TinOre,1),DemoCatalog.Materials(ItemKind.Tin,1)),
            R(13,"富集碎矿炼铁",TechEra.Water,BuildKind.StoneFurnace,3f,DemoCatalog.Materials(ItemKind.CrushedOre,1),DemoCatalog.Materials(ItemKind.Iron,2)),
            R(20,"机械粉碎铁矿",TechEra.Water,BuildKind.MechanicalCrusher,2f,DemoCatalog.Materials(ItemKind.Ore,1),DemoCatalog.Materials(ItemKind.CrushedOre,1)),
            R(30,"蒸汽炼钢",TechEra.Steam,BuildKind.SteamFurnace,4f,DemoCatalog.Materials(ItemKind.Iron,2,ItemKind.Fuel,1),DemoCatalog.Materials(ItemKind.Steel,1)),
            R(31,"蒸汽碎矿冶炼",TechEra.Steam,BuildKind.SteamFurnace,2f,DemoCatalog.Materials(ItemKind.CrushedOre,1),DemoCatalog.Materials(ItemKind.Iron,2)),
            R(32,"蒸汽炼铜",TechEra.Steam,BuildKind.SteamFurnace,2f,DemoCatalog.Materials(ItemKind.CopperOre,1),DemoCatalog.Materials(ItemKind.Copper,1)),
            R(33,"蒸汽炼锡",TechEra.Steam,BuildKind.SteamFurnace,2f,DemoCatalog.Materials(ItemKind.TinOre,1),DemoCatalog.Materials(ItemKind.Tin,1)),
            R(40,"电炉炼铁",TechEra.LowVoltage,BuildKind.Smelter,2.5f,DemoCatalog.Materials(ItemKind.Ore,1),DemoCatalog.Materials(ItemKind.Iron,1)),
            R(41,"电炉碎矿冶炼",TechEra.LowVoltage,BuildKind.Smelter,2.5f,DemoCatalog.Materials(ItemKind.CrushedOre,1),DemoCatalog.Materials(ItemKind.Iron,2)),
            R(42,"电炉炼铜",TechEra.LowVoltage,BuildKind.Smelter,2.5f,DemoCatalog.Materials(ItemKind.CopperOre,1),DemoCatalog.Materials(ItemKind.Copper,1)),
            R(43,"电炉炼锡",TechEra.LowVoltage,BuildKind.Smelter,2.5f,DemoCatalog.Materials(ItemKind.TinOre,1),DemoCatalog.Materials(ItemKind.Tin,1)),
            R(50,"装配弹药",TechEra.LowVoltage,BuildKind.Assembler,3f,DemoCatalog.Materials(ItemKind.Iron,1),DemoCatalog.Materials(ItemKind.Ammo,4)),
            R(60,"基础电路",TechEra.LowVoltage,BuildKind.CircuitAssembler,3f,DemoCatalog.Materials(ItemKind.Copper,2,ItemKind.Iron,1),DemoCatalog.Materials(ItemKind.Circuit,2)),
            R(70,"聚合物",TechEra.MediumVoltage,BuildKind.ChemicalPlant,4f,DemoCatalog.Materials(ItemKind.Wood,2,ItemKind.Fuel,1),DemoCatalog.Materials(ItemKind.Polymer,2)),
            R(71,"罐装冷却液（近水）",TechEra.MediumVoltage,BuildKind.ChemicalPlant,3f,DemoCatalog.Materials(ItemKind.Iron,1,ItemKind.Fuel,1),DemoCatalog.Materials(ItemKind.Coolant,4)),
            R(80,"高级电路",TechEra.HighVoltage,BuildKind.PrecisionAssembler,4f,DemoCatalog.Materials(ItemKind.Circuit,2,ItemKind.Polymer,2,ItemKind.Copper,1),DemoCatalog.Materials(ItemKind.AdvancedCircuit,1)),
            R(90,"核燃料加工",TechEra.Nuclear,BuildKind.NuclearProcessor,4f,DemoCatalog.Materials(ItemKind.UraniumOre,2,ItemKind.Steel,1),DemoCatalog.Materials(ItemKind.NuclearFuel,1)),
            R(91,"循环制冷（近水）",TechEra.MediumVoltage,BuildKind.CoolingTower,3f,new int[DemoCatalog.ItemCount],DemoCatalog.Materials(ItemKind.Coolant,4))
        };
        public static RecipeDefinition Recipe(int id) { foreach(var r in Recipes) if(r.id==id) return r; return null; }
        public static RecipeDefinition DefaultRecipe(BuildKind kind) { foreach(var r in Recipes) if(r.building==kind && kind!=BuildKind.Workbench) return r; return null; }
        public static RecipeDefinition SelectedRecipe(BuildingState b) => b.recipe == 0 ? DefaultRecipe(b.kind) : Recipe(b.recipe);
        public static bool IsFuelBurner(BuildKind kind) => kind==BuildKind.Generator || kind==BuildKind.DieselGenerator || kind==BuildKind.HighGenerator || kind==BuildKind.Boiler || kind==BuildKind.StoneFurnace;
        public static bool Stores(BuildKind kind,int item)
        {
            if(item<0 || item>=DemoCatalog.ItemCount) return false;
            if(kind==BuildKind.Chest || kind==BuildKind.Belt) return true;
            if(IsFuelBurner(kind) && item==(int)ItemKind.Fuel) return true;
            if(kind==BuildKind.Miner) return item==(int)ItemKind.Ore || item==(int)ItemKind.CopperOre || item==(int)ItemKind.TinOre || item==(int)ItemKind.UraniumOre;
            if(kind==BuildKind.SteamMiner) return item==(int)ItemKind.Ore;
            if(kind==BuildKind.Turret) return item==(int)ItemKind.Ammo;
            if(kind==BuildKind.Reactor) return item==(int)ItemKind.NuclearFuel || item==(int)ItemKind.Coolant || item==(int)ItemKind.NuclearWaste;
            foreach(var r in Recipes) if(r.building==kind && kind!=BuildKind.Workbench && (r.inputs[item]>0 || r.outputs[item]>0)) return true;
            return false;
        }
    }
}
