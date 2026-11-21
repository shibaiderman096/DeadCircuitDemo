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
        public static readonly string[] EraNames = { "原始", "水力", "蒸汽", "低压(LV)", "中压(MV)", "高压(HV)", "核工业" };
        public static string EraName(TechEra era) => (int)era >= 0 && (int)era < EraNames.Length ? EraNames[(int)era] : "未知科技";
        public static TechEra RequiredEra(BuildKind kind)
        {
            switch (kind)
            {
                case BuildKind.CharcoalKiln: case BuildKind.ClayKiln:
                case BuildKind.Door: case BuildKind.Wall: case BuildKind.Belt: case BuildKind.Chest: case BuildKind.Workbench: case BuildKind.StoneFurnace: return TechEra.Primitive;
                case BuildKind.Waterwheel: case BuildKind.Shaft: case BuildKind.MechanicalCrusher:
                case BuildKind.BlastFurnace: case BuildKind.MechanicalPress: case BuildKind.MechanicalWorkshop:
                case BuildKind.WaterMill: case BuildKind.IrrigationPump: case BuildKind.MechanicalHopper: return TechEra.Water;
                case BuildKind.CombustionChamber: case BuildKind.Chimney: case BuildKind.FeedwaterPump: case BuildKind.SteamEngine:
                case BuildKind.CokeOven: case BuildKind.RefractoryKiln: case BuildKind.IronBlastFurnace: case BuildKind.RefiningForge: case BuildKind.MachineTool:
                case BuildKind.Boiler: case BuildKind.SteamPipe: case BuildKind.SteamMiner: case BuildKind.SteamFurnace: return TechEra.Steam;
                case BuildKind.WindingBench:
                case BuildKind.FilterSplitter: case BuildKind.CircuitBreaker: case BuildKind.ElectricCrusher:
                case BuildKind.Generator: case BuildKind.Miner: case BuildKind.Smelter: case BuildKind.Assembler: case BuildKind.Turret: case BuildKind.PowerPole: case BuildKind.SteamTurbine: case BuildKind.CircuitAssembler: return TechEra.LowVoltage;
                case BuildKind.OilPump: case BuildKind.LiquidPipe: case BuildKind.LiquidTank: case BuildKind.DistillationUnit: case BuildKind.ChemicalReactor: case BuildKind.ProcessWaterPump:
                case BuildKind.DieselGenerator: case BuildKind.ChemicalPlant: case BuildKind.MediumPole: case BuildKind.Transformer: case BuildKind.CoolingTower: return TechEra.MediumVoltage;
                case BuildKind.SolarPanel: case BuildKind.SiliconRefiner: case BuildKind.ElectronicsBench: case BuildKind.HighGenerator: case BuildKind.HighPole: case BuildKind.PrecisionAssembler: return TechEra.HighVoltage;
                case BuildKind.NuclearProcessor: case BuildKind.Reactor: case BuildKind.ReactorControlConsole: case BuildKind.NuclearWasteVault: return TechEra.Nuclear;
                default: return TechEra.Nuclear;
            }
        }
        public static bool IsUnlocked(WorldState s, BuildKind kind) => s != null && (int)kind >= 0 && (int)kind < DemoCatalog.BuildNames.Length && s.era >= RequiredEra(kind) && PrimitiveCatalog.IsBuildUnlocked(s,kind) && WaterCatalog.IsBuildUnlocked(s,kind) && SteamCatalog.IsBuildUnlocked(s,kind);
        public static int EnergyTier(BuildingState b) => b==null ? -1 : b.kind==BuildKind.Transformer ? 2+b.setting : EnergyTier(b.kind);
        public static float LinkRange(BuildKind kind) => EnergyTier(kind) >= 2 ? DemoCatalog.LinkRange : 0f;
        public static float SupplyRadius(BuildKind kind) => EnergyTier(kind) >= 2 ? DemoCatalog.SupplyRadius : 0f;
        public static bool RequiresWater(BuildKind kind) => kind==BuildKind.Waterwheel || kind==BuildKind.FeedwaterPump || kind==BuildKind.CoolingTower || kind==BuildKind.IrrigationPump || kind==BuildKind.ProcessWaterPump || kind==BuildKind.DistillationUnit || kind==BuildKind.ChemicalReactor;
        public static bool NearWater(WorldState s,BuildingState b)
        {
            if(s==null || b==null || !WorldTiles.Inside(s,b.x,b.y)) return false;
            int w=DemoCatalog.Width(b),h=DemoCatalog.Height(b);
            if(!WorldTiles.Inside(s,b.x+w-1,b.y+h-1)) return false;
            for(int y=b.y;y<b.y+h;y++) for(int x=b.x;x<b.x+w;x++)
            {
                char ground=WorldTiles.Ground(s,x,y);
                if(ground=='2' || ground=='?') return false;
            }
            for(int y=b.y;y<b.y+h;y++)
                if(WorldTiles.Ground(s,b.x-1,y)=='2' || WorldTiles.Ground(s,b.x+w,y)=='2') return true;
            for(int x=b.x;x<b.x+w;x++)
                if(WorldTiles.Ground(s,x,b.y-1)=='2' || WorldTiles.Ground(s,x,b.y+h)=='2') return true;
            // A feedwater intake may penetrate exactly one intact exterior wall.
            // This lets an enclosed dry factory use shoreline water without a hole in its shell.
            if(b.kind==BuildKind.FeedwaterPump)
            {
                for(int y=b.y;y<b.y+h;y++)
                    if(WaterIntake(s,b.x-1,y,-1,0) || WaterIntake(s,b.x+w,y,1,0)) return true;
                for(int x=b.x;x<b.x+w;x++)
                    if(WaterIntake(s,x,b.y-1,0,-1) || WaterIntake(s,x,b.y+h,0,1)) return true;
            }
            return false;
        }
        private static bool WaterIntake(WorldState s,int wallX,int wallY,int dx,int dy)
        {
            int waterX=wallX+dx,waterY=wallY+dy;
            if(!WorldTiles.Inside(s,wallX,wallY) || !WorldTiles.Inside(s,waterX,waterY) ||
                WorldTiles.Ground(s,waterX,waterY)!='2') return false;
            foreach(var wall in s.nodes)
                if(wall.kind==NodeKind.Wall && wall.hp>0 && wall.stock>0 && wall.x==wallX && wall.y==wallY) return true;
            foreach(var wall in s.buildings)
                if(wall.kind==BuildKind.Wall && wall.hp>0 && DemoCatalog.Contains(wall,wallX,wallY)) return true;
            return false;
        }
        // A converter's public tier is its OUTPUT. Its input is a separate port.
        public static int EnergyTier(BuildKind kind)
        {
            switch (kind)
            {
                case BuildKind.SteamEngine: case BuildKind.Waterwheel: case BuildKind.Shaft: case BuildKind.MechanicalCrusher:
                case BuildKind.IronBlastFurnace: case BuildKind.RefiningForge: case BuildKind.MachineTool: case BuildKind.WindingBench:
                case BuildKind.BlastFurnace: case BuildKind.MechanicalPress: case BuildKind.MechanicalWorkshop:
                case BuildKind.WaterMill: case BuildKind.IrrigationPump: case BuildKind.MechanicalHopper: return 0;
                case BuildKind.Boiler: case BuildKind.SteamPipe: case BuildKind.SteamMiner: case BuildKind.SteamFurnace: return 1;
                case BuildKind.OilPump: case BuildKind.ProcessWaterPump: case BuildKind.DistillationUnit: case BuildKind.ChemicalReactor:
                case BuildKind.FilterSplitter: case BuildKind.CircuitBreaker: case BuildKind.ElectricCrusher:
                case BuildKind.Generator: case BuildKind.Miner: case BuildKind.Smelter: case BuildKind.Assembler: case BuildKind.Turret: case BuildKind.PowerPole: case BuildKind.SteamTurbine: case BuildKind.CircuitAssembler: case BuildKind.Transformer: return 2;
                case BuildKind.SiliconRefiner: case BuildKind.ElectronicsBench: case BuildKind.SolarPanel: case BuildKind.DieselGenerator: case BuildKind.ChemicalPlant: case BuildKind.MediumPole: case BuildKind.CoolingTower: return 3;
                case BuildKind.HighGenerator: case BuildKind.HighPole: case BuildKind.PrecisionAssembler: case BuildKind.NuclearProcessor: case BuildKind.Reactor: case BuildKind.ReactorControlConsole: return 4;
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
                case TechEra.Nuclear: return DemoCatalog.Materials(ItemKind.ControlModule,4, ItemKind.HeatAlloy,4, ItemKind.Coolant,8);
                default: return new int[DemoCatalog.ItemCount];
            }
        }
        public static string ResearchRequirement(WorldState s, TechEra target)
        {
            if (s == null || (int)target < 1 || (int)target > 6 || (int)target != (int)s.era + 1) return "请先完成当前时代，再研究下一阶段。";
            if (target == TechEra.Water)
            {
                string primitive = PrimitiveCatalog.WaterRequirement(s);
                if (primitive.Length > 0) return primitive;
            }
            if (target == TechEra.Steam)
            {
                string water = WaterCatalog.SteamRequirement(s);
                if (water.Length > 0) return water;
            }
            if (target == TechEra.LowVoltage)
            {
                string steam = SteamCatalog.LowVoltageRequirement(s);
                if (steam.Length > 0) return steam;
            }
            if(target==TechEra.MediumVoltage)
            {
                string electrical=LowVoltageCatalog.MediumVoltageRequirement(s);
                if(electrical.Length>0) return electrical;
            }
            if(target==TechEra.HighVoltage)
            {
                string petroleum=PetrochemicalCatalog.HighVoltageRequirement(s);
                if(petroleum.Length>0) return petroleum;
            }
            if(target==TechEra.Nuclear)
            {
                string precision=PrecisionCatalog.NuclearRequirement(s);
                if(precision.Length>0) return precision;
            }
            BuildKind required = target == TechEra.Water ? BuildKind.Workbench : target == TechEra.Steam ? BuildKind.MechanicalCrusher : target == TechEra.LowVoltage ? BuildKind.SteamFurnace : target == TechEra.MediumVoltage ? BuildKind.CircuitAssembler : target == TechEra.HighVoltage ? BuildKind.ChemicalPlant : BuildKind.PrecisionAssembler;
            return s.buildings.Exists(b => b != null && b.kind == required && b.hp > 0f) ? "" : "需先建造并确保设备完好：" + DemoCatalog.BuildName(required);
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
                case TechEra.Water: return new[] { BuildKind.Waterwheel, BuildKind.Shaft, BuildKind.MechanicalCrusher, BuildKind.BlastFurnace, BuildKind.MechanicalPress, BuildKind.MechanicalWorkshop, BuildKind.MechanicalHopper, BuildKind.WaterMill, BuildKind.IrrigationPump, BuildKind.Chest };
                case TechEra.Steam: return new[] { BuildKind.CombustionChamber, BuildKind.Boiler, BuildKind.FeedwaterPump, BuildKind.Chimney, BuildKind.SteamPipe, BuildKind.SteamEngine, BuildKind.SteamMiner, BuildKind.SteamFurnace, BuildKind.Belt, BuildKind.Chest };
                case TechEra.LowVoltage: return new[] { BuildKind.Generator, BuildKind.Miner, BuildKind.Smelter, BuildKind.Assembler, BuildKind.Turret, BuildKind.PowerPole, BuildKind.CircuitAssembler, BuildKind.SteamTurbine, BuildKind.Belt, BuildKind.Chest };
                case TechEra.MediumVoltage: return new[] { BuildKind.OilPump, BuildKind.LiquidPipe, BuildKind.LiquidTank, BuildKind.ProcessWaterPump, BuildKind.DistillationUnit, BuildKind.ChemicalReactor, BuildKind.DieselGenerator, BuildKind.MediumPole, BuildKind.ChemicalPlant, BuildKind.CoolingTower };
                case TechEra.HighVoltage: return new[] { BuildKind.SiliconRefiner, BuildKind.ElectronicsBench, BuildKind.HighGenerator, BuildKind.HighPole, BuildKind.PrecisionAssembler, BuildKind.Transformer, BuildKind.SolarPanel, BuildKind.LiquidPipe, BuildKind.Belt, BuildKind.Chest };
                case TechEra.Nuclear: return new[] { BuildKind.Reactor, BuildKind.ReactorControlConsole, BuildKind.NuclearWasteVault, BuildKind.NuclearProcessor, BuildKind.CoolingTower, BuildKind.HighPole, BuildKind.Transformer, BuildKind.PrecisionAssembler, BuildKind.ChemicalPlant, BuildKind.Belt };
                default: return new[] { BuildKind.Workbench, BuildKind.StoneFurnace, BuildKind.CharcoalKiln, BuildKind.ClayKiln, BuildKind.Chest, BuildKind.Wall, BuildKind.Door, BuildKind.Belt, BuildKind.Workbench, BuildKind.StoneFurnace };
            }
        }
        private static RecipeDefinition R(int id, string name, TechEra era, BuildKind building, float duration, int[] inputs, int[] outputs) => new RecipeDefinition { id=id,name=name,era=era,building=building,duration=duration,inputs=inputs,outputs=outputs };
        public static readonly RecipeDefinition[] Recipes =
        {
            R(100,"木炭烧制",TechEra.Primitive,BuildKind.CharcoalKiln,4f,DemoCatalog.Materials(ItemKind.Wood,4),DemoCatalog.Materials(ItemKind.Charcoal,2)),
            R(101,"陶砖烧制",TechEra.Primitive,BuildKind.ClayKiln,4f,DemoCatalog.Materials(ItemKind.Clay,4,ItemKind.Charcoal,1),DemoCatalog.Materials(ItemKind.Brick,4)),
            R(2,"青铜合金",TechEra.Primitive,BuildKind.Workbench,.5f,DemoCatalog.Materials(ItemKind.Copper,3,ItemKind.Tin,1),DemoCatalog.Materials(ItemKind.Bronze,4)),
            R(3,"铁锭加工废料",TechEra.Primitive,BuildKind.Workbench,.5f,DemoCatalog.Materials(ItemKind.Iron,1),DemoCatalog.Materials(ItemKind.Scrap,3)),
            R(10,"石炉炼铁",TechEra.Primitive,BuildKind.StoneFurnace,3f,DemoCatalog.Materials(ItemKind.Ore,1),DemoCatalog.Materials(ItemKind.Iron,1)),
            R(11,"石炉炼铜",TechEra.Primitive,BuildKind.StoneFurnace,3f,DemoCatalog.Materials(ItemKind.CopperOre,1),DemoCatalog.Materials(ItemKind.Copper,1)),
            R(12,"石炉炼锡",TechEra.Primitive,BuildKind.StoneFurnace,3f,DemoCatalog.Materials(ItemKind.TinOre,1),DemoCatalog.Materials(ItemKind.Tin,1)),
            R(13,"富集碎矿炼铁",TechEra.Water,BuildKind.StoneFurnace,3f,DemoCatalog.Materials(ItemKind.CrushedOre,1),DemoCatalog.Materials(ItemKind.Iron,2)),
            R(20,"机械粉碎铁矿",TechEra.Water,BuildKind.MechanicalCrusher,2f,DemoCatalog.Materials(ItemKind.Ore,1),DemoCatalog.Materials(ItemKind.CrushedOre,1)),
            R(30,"蒸汽炼钢",TechEra.Steam,BuildKind.SteamFurnace,4f,DemoCatalog.Materials(ItemKind.PigIron,3,ItemKind.Limestone,1),DemoCatalog.Materials(ItemKind.Steel,3)),
            R(31,"蒸汽碎矿冶炼",TechEra.Steam,BuildKind.SteamFurnace,2f,DemoCatalog.Materials(ItemKind.CrushedOre,1),DemoCatalog.Materials(ItemKind.Iron,2)),
            R(32,"蒸汽炼铜",TechEra.Steam,BuildKind.SteamFurnace,2f,DemoCatalog.Materials(ItemKind.CopperOre,1),DemoCatalog.Materials(ItemKind.Copper,1)),
            R(33,"蒸汽炼锡",TechEra.Steam,BuildKind.SteamFurnace,2f,DemoCatalog.Materials(ItemKind.TinOre,1),DemoCatalog.Materials(ItemKind.Tin,1)),
            R(40,"电炉炼铁",TechEra.LowVoltage,BuildKind.Smelter,2.5f,DemoCatalog.Materials(ItemKind.Ore,1),DemoCatalog.Materials(ItemKind.Iron,1)),
            R(41,"电炉碎矿冶炼",TechEra.LowVoltage,BuildKind.Smelter,2.5f,DemoCatalog.Materials(ItemKind.CrushedOre,1),DemoCatalog.Materials(ItemKind.Iron,2)),
            R(42,"电炉炼铜",TechEra.LowVoltage,BuildKind.Smelter,2.5f,DemoCatalog.Materials(ItemKind.CopperOre,1),DemoCatalog.Materials(ItemKind.Copper,1)),
            R(43,"电炉炼锡",TechEra.LowVoltage,BuildKind.Smelter,2.5f,DemoCatalog.Materials(ItemKind.TinOre,1),DemoCatalog.Materials(ItemKind.Tin,1)),
            R(50,"装配弹药",TechEra.LowVoltage,BuildKind.Assembler,3f,DemoCatalog.Materials(ItemKind.Iron,1),DemoCatalog.Materials(ItemKind.Ammo,4)),
            R(52,"步枪弹装配",TechEra.LowVoltage,BuildKind.Assembler,3f,DemoCatalog.Materials(ItemKind.Iron,2,ItemKind.Copper,1),DemoCatalog.Materials(ItemKind.RifleAmmo,6)),
            R(53,"霰弹装配",TechEra.LowVoltage,BuildKind.Assembler,3f,DemoCatalog.Materials(ItemKind.Iron,2,ItemKind.Scrap,1),DemoCatalog.Materials(ItemKind.Shell,4)),
            R(54,"精密弹装配",TechEra.MediumVoltage,BuildKind.Assembler,4f,DemoCatalog.Materials(ItemKind.Steel,1,ItemKind.Copper,2),DemoCatalog.Materials(ItemKind.PrecisionAmmo,4)),
            R(55,"穿甲弹精密装配",TechEra.HighVoltage,BuildKind.PrecisionAssembler,4f,DemoCatalog.Materials(ItemKind.Steel,2,ItemKind.Copper,2,ItemKind.Polymer,1),DemoCatalog.Materials(ItemKind.ArmorPiercingAmmo,4)),
            R(56,"手制步枪弹",TechEra.LowVoltage,BuildKind.Workbench,.5f,DemoCatalog.Materials(ItemKind.Iron,2,ItemKind.Copper,1),DemoCatalog.Materials(ItemKind.RifleAmmo,3)),
            R(57,"手制霰弹",TechEra.LowVoltage,BuildKind.Workbench,.5f,DemoCatalog.Materials(ItemKind.Iron,2,ItemKind.Scrap,1),DemoCatalog.Materials(ItemKind.Shell,2)),
            R(60,"继电电路装配",TechEra.LowVoltage,BuildKind.CircuitAssembler,3f,DemoCatalog.Materials(ItemKind.Relay,1,ItemKind.Cable,1,ItemKind.IronPlate,1),DemoCatalog.Materials(ItemKind.Circuit,2)),
            R(70,"基础聚合",TechEra.MediumVoltage,BuildKind.ChemicalReactor,4f,DemoCatalog.Materials(ItemKind.ChemicalFeedstock,2),DemoCatalog.Materials(ItemKind.Polymer,2)),
            R(71,"冷却液配制",TechEra.MediumVoltage,BuildKind.ChemicalReactor,3f,DemoCatalog.Materials(ItemKind.ProcessWater,2,ItemKind.Polymer,1),DemoCatalog.Materials(ItemKind.Coolant,2)),
            R(80,"高压控制模块",TechEra.HighVoltage,BuildKind.PrecisionAssembler,4f,DemoCatalog.Materials(ItemKind.AdvancedCircuit,2,ItemKind.PrecisionPart,2,ItemKind.Coolant,1),DemoCatalog.Materials(ItemKind.ControlModule,2,ItemKind.HotCoolant,1)),
            R(90,"核燃料加工",TechEra.Nuclear,BuildKind.NuclearProcessor,4f,DemoCatalog.Materials(ItemKind.UraniumOre,2,ItemKind.Steel,1),DemoCatalog.Materials(ItemKind.NuclearFuel,1)),
            R(91,"冷却液回收（近水）",TechEra.MediumVoltage,BuildKind.CoolingTower,3f,DemoCatalog.Materials(ItemKind.HotCoolant,2,ItemKind.ProcessWater,1),DemoCatalog.Materials(ItemKind.Coolant,2)),
            R(110,"鼓风碎矿炼铁",TechEra.Water,BuildKind.BlastFurnace,3f,DemoCatalog.Materials(ItemKind.CrushedOre,1,ItemKind.Charcoal,1),DemoCatalog.Materials(ItemKind.Iron,3)),
            R(111,"鼓风炼铜",TechEra.Water,BuildKind.BlastFurnace,3f,DemoCatalog.Materials(ItemKind.CopperOre,2,ItemKind.Charcoal,1),DemoCatalog.Materials(ItemKind.Copper,3)),
            R(112,"鼓风炼锡",TechEra.Water,BuildKind.BlastFurnace,3f,DemoCatalog.Materials(ItemKind.TinOre,2,ItemKind.Charcoal,1),DemoCatalog.Materials(ItemKind.Tin,3)),
            R(113,"机械压制铁板",TechEra.Water,BuildKind.MechanicalPress,2f,DemoCatalog.Materials(ItemKind.Iron,1),DemoCatalog.Materials(ItemKind.IronPlate,1)),
            R(114,"机械加工齿轮",TechEra.Water,BuildKind.MechanicalWorkshop,3f,DemoCatalog.Materials(ItemKind.IronPlate,1,ItemKind.Bronze,1),DemoCatalog.Materials(ItemKind.Gear,2)),
            R(115,"机械加工轴承",TechEra.Water,BuildKind.MechanicalWorkshop,3f,DemoCatalog.Materials(ItemKind.Bronze,2,ItemKind.Iron,1),DemoCatalog.Materials(ItemKind.Bearing,2)),
            R(120,"煤炭焦化",TechEra.Steam,BuildKind.CokeOven,4f,DemoCatalog.Materials(ItemKind.Coal,4,ItemKind.Charcoal,1),DemoCatalog.Materials(ItemKind.Coke,3)),
            R(121,"耐火砖烧制",TechEra.Steam,BuildKind.RefractoryKiln,4f,DemoCatalog.Materials(ItemKind.FireClay,3,ItemKind.Stone,1,ItemKind.Charcoal,1),DemoCatalog.Materials(ItemKind.RefractoryBrick,4)),
            R(122,"高炉炼生铁",TechEra.Steam,BuildKind.IronBlastFurnace,4f,DemoCatalog.Materials(ItemKind.CrushedOre,2,ItemKind.Coke,2,ItemKind.Limestone,1),DemoCatalog.Materials(ItemKind.PigIron,3)),
            R(123,"精炼钢材",TechEra.Steam,BuildKind.RefiningForge,4f,DemoCatalog.Materials(ItemKind.PigIron,3,ItemKind.Limestone,1,ItemKind.Charcoal,1),DemoCatalog.Materials(ItemKind.Steel,2)),
            R(124,"机械压制钢板",TechEra.Steam,BuildKind.MechanicalPress,3f,DemoCatalog.Materials(ItemKind.Steel,1),DemoCatalog.Materials(ItemKind.SteelPlate,1)),
            R(125,"耐压管件加工",TechEra.Steam,BuildKind.MachineTool,3f,DemoCatalog.Materials(ItemKind.SteelPlate,1,ItemKind.Bronze,1),DemoCatalog.Materials(ItemKind.PipeFitting,2)),
            R(140,"陶瓷绝缘烧制",TechEra.LowVoltage,BuildKind.ClayKiln,4f,DemoCatalog.Materials(ItemKind.Clay,2,ItemKind.Brick,1,ItemKind.Charcoal,1),DemoCatalog.Materials(ItemKind.Insulator,2)),
            R(141,"机械拉制铜线",TechEra.LowVoltage,BuildKind.MachineTool,3f,DemoCatalog.Materials(ItemKind.Copper,1),DemoCatalog.Materials(ItemKind.CopperWire,3)),
            R(142,"手工绝缘电缆",TechEra.LowVoltage,BuildKind.Workbench,.5f,DemoCatalog.Materials(ItemKind.CopperWire,2,ItemKind.Insulator,1),DemoCatalog.Materials(ItemKind.Cable,2)),
            R(143,"机械绕制线圈",TechEra.LowVoltage,BuildKind.WindingBench,3f,DemoCatalog.Materials(ItemKind.CopperWire,4,ItemKind.Insulator,1,ItemKind.Iron,1),DemoCatalog.Materials(ItemKind.Coil,1)),
            R(144,"手工电机装配",TechEra.LowVoltage,BuildKind.Workbench,.5f,DemoCatalog.Materials(ItemKind.Coil,1,ItemKind.Steel,1,ItemKind.Gear,1,ItemKind.Bearing,1),DemoCatalog.Materials(ItemKind.Motor,1)),
            R(145,"手工继电器",TechEra.LowVoltage,BuildKind.Workbench,.5f,DemoCatalog.Materials(ItemKind.Coil,1,ItemKind.IronPlate,1,ItemKind.Insulator,1),DemoCatalog.Materials(ItemKind.Relay,1)),
            R(146,"电动破碎铁矿",TechEra.LowVoltage,BuildKind.ElectricCrusher,2f,DemoCatalog.Materials(ItemKind.Ore,1),DemoCatalog.Materials(ItemKind.CrushedOre,2)),
            R(147,"电炉铜线加工",TechEra.LowVoltage,BuildKind.Smelter,2f,DemoCatalog.Materials(ItemKind.Copper,1),DemoCatalog.Materials(ItemKind.CopperWire,4)),
            R(148,"电缆自动装配",TechEra.LowVoltage,BuildKind.Assembler,3f,DemoCatalog.Materials(ItemKind.CopperWire,4,ItemKind.Insulator,2),DemoCatalog.Materials(ItemKind.Cable,4)),
            R(149,"电机自动装配",TechEra.LowVoltage,BuildKind.Assembler,3f,DemoCatalog.Materials(ItemKind.Coil,1,ItemKind.Steel,1,ItemKind.Gear,1,ItemKind.Bearing,1),DemoCatalog.Materials(ItemKind.Motor,1)),
            R(150,"继电器自动装配",TechEra.LowVoltage,BuildKind.Assembler,3f,DemoCatalog.Materials(ItemKind.Coil,1,ItemKind.IronPlate,1,ItemKind.Insulator,1),DemoCatalog.Materials(ItemKind.Relay,1)),
            R(160,"轻质馏分转化",TechEra.MediumVoltage,BuildKind.ChemicalReactor,4f,DemoCatalog.Materials(ItemKind.LightFraction,2,ItemKind.ProcessWater,1),DemoCatalog.Materials(ItemKind.ChemicalFeedstock,2)),
            R(161,"原油分馏",TechEra.MediumVoltage,BuildKind.DistillationUnit,4f,DemoCatalog.Materials(ItemKind.CrudeOil,4,ItemKind.Fuel,1),DemoCatalog.Materials(ItemKind.LightFraction,2,ItemKind.ChemicalFeedstock,1,ItemKind.HeavyFraction,1)),
            R(162,"重质馏分制润滑油",TechEra.MediumVoltage,BuildKind.ChemicalReactor,4f,DemoCatalog.Materials(ItemKind.HeavyFraction,2),DemoCatalog.Materials(ItemKind.Lubricant,2)),
            R(163,"重质馏分调制燃料",TechEra.MediumVoltage,BuildKind.ChemicalReactor,3f,DemoCatalog.Materials(ItemKind.HeavyFraction,1,ItemKind.Coal,1),DemoCatalog.Materials(ItemKind.Fuel,2)),
            R(164,"中压冷却聚合",TechEra.MediumVoltage,BuildKind.ChemicalPlant,3f,DemoCatalog.Materials(ItemKind.ChemicalFeedstock,4,ItemKind.Coolant,1),DemoCatalog.Materials(ItemKind.Polymer,4,ItemKind.HotCoolant,1)),
            R(165,"柴油精制",TechEra.MediumVoltage,BuildKind.ChemicalReactor,4f,DemoCatalog.Materials(ItemKind.LightFraction,2),DemoCatalog.Materials(ItemKind.Diesel,2)),
            R(170,"工业硅冶炼",TechEra.HighVoltage,BuildKind.SteamFurnace,4f,DemoCatalog.Materials(ItemKind.Quartz,3,ItemKind.Coke,1),DemoCatalog.Materials(ItemKind.IndustrialSilicon,2)),
            R(171,"硅材提纯",TechEra.HighVoltage,BuildKind.SiliconRefiner,5f,DemoCatalog.Materials(ItemKind.IndustrialSilicon,2,ItemKind.ProcessWater,1),DemoCatalog.Materials(ItemKind.PureSilicon,2)),
            R(172,"晶圆切制",TechEra.HighVoltage,BuildKind.SiliconRefiner,4f,DemoCatalog.Materials(ItemKind.PureSilicon,2,ItemKind.Coolant,1),DemoCatalog.Materials(ItemKind.Wafer,4,ItemKind.HotCoolant,1)),
            R(173,"半导体封装",TechEra.HighVoltage,BuildKind.ElectronicsBench,4f,DemoCatalog.Materials(ItemKind.Wafer,2,ItemKind.Polymer,1,ItemKind.CopperWire,1),DemoCatalog.Materials(ItemKind.Semiconductor,2)),
            R(174,"高级电路叠层",TechEra.HighVoltage,BuildKind.ElectronicsBench,4f,DemoCatalog.Materials(ItemKind.Circuit,2,ItemKind.Semiconductor,1,ItemKind.Polymer,1,ItemKind.CopperWire,1),DemoCatalog.Materials(ItemKind.AdvancedCircuit,2)),
            R(175,"精密部件加工",TechEra.HighVoltage,BuildKind.ElectronicsBench,4f,DemoCatalog.Materials(ItemKind.SteelPlate,2,ItemKind.Bearing,1,ItemKind.Lubricant,1),DemoCatalog.Materials(ItemKind.PrecisionPart,2)),
            R(176,"中压控制模块",TechEra.HighVoltage,BuildKind.ElectronicsBench,5f,DemoCatalog.Materials(ItemKind.AdvancedCircuit,2,ItemKind.PrecisionPart,2,ItemKind.Relay,1),DemoCatalog.Materials(ItemKind.ControlModule,1)),
            R(177,"耐热合金冶炼",TechEra.HighVoltage,BuildKind.SteamFurnace,5f,DemoCatalog.Materials(ItemKind.Steel,2,ItemKind.Copper,1,ItemKind.Coke,1),DemoCatalog.Materials(ItemKind.HeatAlloy,2))
        };
        public static RecipeDefinition Recipe(int id) { foreach(var r in Recipes) if(r.id==id) return r; return null; }
        public static RecipeDefinition DefaultRecipe(BuildKind kind)
        {
            if(kind==BuildKind.PrecisionAssembler) return Recipe(80);
            foreach(var r in Recipes) if(r.building==kind && kind!=BuildKind.Workbench) return r;
            return null;
        }
        public static RecipeDefinition SelectedRecipe(BuildingState b) => b.recipe == 0 ? DefaultRecipe(b.kind) : Recipe(b.recipe);
        public static bool IsFuelBurner(BuildKind kind) => kind==BuildKind.CombustionChamber || kind==BuildKind.Generator || kind==BuildKind.DieselGenerator || kind==BuildKind.HighGenerator || kind==BuildKind.Boiler || kind==BuildKind.StoneFurnace;
        public static bool Stores(BuildKind kind,int item)
        {
            if(item<0 || item>=DemoCatalog.ItemCount) return false;
            if(PetrochemicalCatalog.IsLiquidStorage(kind)) return PetrochemicalCatalog.IsFluid(item);
            if(kind==BuildKind.Chest || kind==BuildKind.Belt || kind==BuildKind.MechanicalHopper || kind==BuildKind.FilterSplitter) return !PetrochemicalCatalog.IsFluid(item);
            if(kind==BuildKind.OilPump) return item==(int)ItemKind.CrudeOil;
            if(kind==BuildKind.ProcessWaterPump) return item==(int)ItemKind.ProcessWater;
            if(kind==BuildKind.DieselGenerator) return item==(int)ItemKind.Diesel;
            if(kind==BuildKind.CombustionChamber && (item==(int)ItemKind.Coal || item==(int)ItemKind.Coke)) return true;
            if(IsFuelBurner(kind) && item==(int)ItemKind.Fuel) return true;
            if(kind==BuildKind.StoneFurnace && item==(int)ItemKind.Charcoal) return true;
            if(kind==BuildKind.Miner) return item==(int)ItemKind.Ore || item==(int)ItemKind.CopperOre || item==(int)ItemKind.TinOre || item==(int)ItemKind.UraniumOre;
            if(kind==BuildKind.SteamMiner) return item==(int)ItemKind.Quartz || item==(int)ItemKind.Ore || item==(int)ItemKind.CopperOre || item==(int)ItemKind.TinOre || item==(int)ItemKind.UraniumOre || item==(int)ItemKind.Stone || item==(int)ItemKind.Clay || item==(int)ItemKind.Coal || item==(int)ItemKind.Limestone || item==(int)ItemKind.FireClay;
            if(kind==BuildKind.Turret) return item==(int)ItemKind.Ammo;
            if(kind==BuildKind.ReactorControlConsole) return false;
            if(kind==BuildKind.NuclearWasteVault) return item==(int)ItemKind.NuclearWaste;
            if(kind==BuildKind.Reactor) return item==(int)ItemKind.NuclearFuel || item==(int)ItemKind.Coolant || item==(int)ItemKind.NuclearWaste;
            foreach(var r in Recipes) if(r.building==kind && kind!=BuildKind.Workbench && (r.inputs[item]>0 || r.outputs[item]>0)) return true;
            return false;
        }
    }
}
