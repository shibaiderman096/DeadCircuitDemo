using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public enum GroundKind { Grass, Dirt, Water, Road, Floor }
    public enum TechEra { Primitive = 0, Water = 1, Steam = 2, LowVoltage = 3, MediumVoltage = 4, HighVoltage = 5, Nuclear = 6 }
    public enum NodeKind { Tree, Ore, Scrap, Crate, Wall, CopperOre = 5, TinOre = 6, UraniumOre = 7, Stone = 8, Clay = 9, Coal = 10, Limestone = 11, FireClay = 12, Oil = 13 }
    public enum BuildKind { Wall, Generator, Miner, Belt, Smelter, Assembler, Turret, Chest, PowerPole, SolarPanel, Workbench = 10, StoneFurnace, Waterwheel, Shaft, MechanicalCrusher, Boiler, SteamPipe, SteamMiner, SteamFurnace, SteamTurbine, CircuitAssembler, DieselGenerator, ChemicalPlant, MediumPole, Transformer, HighGenerator, HighPole, PrecisionAssembler, NuclearProcessor, Reactor, CoolingTower, Door = 31, CombustionChamber, Chimney, FeedwaterPump, SteamEngine, CharcoalKiln = 36, ClayKiln = 37, BlastFurnace = 38, MechanicalPress = 39, MechanicalWorkshop = 40, WaterMill = 41, IrrigationPump = 42, MechanicalHopper = 43, CokeOven = 44, RefractoryKiln = 45, IronBlastFurnace = 46, RefiningForge = 47, MachineTool = 48, WindingBench = 49, FilterSplitter = 50, CircuitBreaker = 51, ElectricCrusher = 52, OilPump = 53, LiquidPipe = 54, LiquidTank = 55, DistillationUnit = 56, ChemicalReactor = 57, ProcessWaterPump = 58 }
    public enum ItemKind { Wood, Ore, Iron, Scrap, Fuel, Ammo, Food, CopperOre = 7, Copper, Bronze, Steel, Circuit, Polymer, AdvancedCircuit, UraniumOre, NuclearFuel, Coolant, NuclearWaste, CrushedOre, TinOre, Tin, RifleAmmo = 21, Shell = 22, PrecisionAmmo = 23, ArmorPiercingAmmo = 24, Stone = 25, Clay = 26, Charcoal = 27, Brick = 28, IronPlate = 29, Gear = 30, Bearing = 31, Coal = 32, Limestone = 33, FireClay = 34, Coke = 35, RefractoryBrick = 36, PigIron = 37, SteelPlate = 38, PipeFitting = 39, CopperWire = 40, Insulator = 41, Cable = 42, Coil = 43, Motor = 44, Relay = 45, CrudeOil = 46, LightFraction = 47, ChemicalFeedstock = 48, HeavyFraction = 49, Diesel = 50, Lubricant = 51, ProcessWater = 52, HotCoolant = 53 }
    public enum GearKind { StoneAxe = 0, StonePickaxe = 1, SteelAxe = 2, SteelPickaxe = 3, Wrench = 4, Knife = 5, Pistol = 6, Shotgun = 7, AssaultRifle = 8, PrecisionRifle = 9, ClothHood = 10, LeatherJacket = 11, WorkPants = 12, WorkBoots = 13, SteelHelmet = 14, ArmorVest = 15, TacticalHelmet = 16, TacticalVest = 17, TacticalBoots = 18 }
    public enum PartKind { RedDot = 0, Scope4x = 1, Suppressor = 2, MuzzleBrake = 3, ExtendedMag = 4, ErgonomicGrip = 5, MatchBarrel = 6 }
    public enum AttachmentSlot { Optic = 0, Muzzle = 1, Magazine = 2, Grip = 3, Barrel = 4 }

    [Serializable] public sealed class GearState
    {
        public int id;
        public GearKind kind;
        public int durability = 100, magazine;
        public ItemKind loadedAmmo = ItemKind.Ammo;
        public int[] attachments = { -1, -1, -1, -1, -1 };
    }

    [Serializable] public sealed class WorldState
    {
        internal WorldState SnapshotCopy() => (WorldState)MemberwiseClone();
        public int version = 16, seed, width = 64, height = 64, nextId = 1, wave;
        // Zero preserves pre-v11 progression; only new worlds opt into primitive research.
        public int primitiveVersion, primitiveResearch;
        // Zero preserves pre-v12 progression; only new worlds opt into water research.
        public int waterVersion, waterResearch;
        // Count only completed operations driven by a real waterwheel-and-shaft network.
        public int waterCrushed, waterSmelted, waterPressed, waterGears, waterBearings, waterTransported, waterMilled;
        public float waterIrrigated;
        public int steamResearch, steamCoked, steamRefractory, steamPigIron, steamSteel, steamPlates, steamFittings, steamDriven;
        public int lvResearch, lvWire, lvInsulators, lvCable, lvCoils, lvMotors, lvRelays, lvCircuits, lvElectric, lvAutomated, lvSorted, lvProtected;
        public int mvResearch, mvSurvey, mvExtracted, mvPiped, mvDistilled, mvDiesel, mvPolymer, mvLubricant, mvCooled, mvPowered;
        public ChunkWorldState chunks = new ChunkWorldState();
        public WorldGenerationSettings generation;
        public int generatorVersion;
        public string biomes = "";
        public List<SiteState> sites = new List<SiteState>();
        public SurvivalWorldState survival = new SurvivalWorldState();
        public TechEra era = TechEra.Primitive;
        public int[] produced = new int[DemoCatalog.ItemCount];
        public long tick;
        public float elapsed, nextWaveAt = 180f, dayLength = 240f;
        public string ground = "", announcement = "";
        public float announcementUntil;
        public int kills, manufactured, gathered;
        public List<PlayerState> players = new List<PlayerState>();
        public List<NodeState> nodes = new List<NodeState>();
        public List<BuildingState> buildings = new List<BuildingState>();
        public List<ZombieState> zombies = new List<ZombieState>();
        public List<TraceState> traces = new List<TraceState>();
        public List<CargoState> cargo = new List<CargoState>();
        public List<PowerNetworkState> powerNetworks = new List<PowerNetworkState>();
        // Derived each power refresh; never trusted when restoring a save.
        public List<SteamClusterState> steamClusters = new List<SteamClusterState>();
    }

    [Serializable] public sealed class PlayerState
    {
        internal PlayerState SnapshotCopy() => (PlayerState)MemberwiseClone();
        public string id = "", name = "幸存者", notice = "";
        public int seat, level = 1, xp, attributePoints = 2, skillPoints = 1, deaths;
        public int appearance;
        public bool connected = true;
        public float x, y, aimX = 1f, aimY, hp = 100f, hunger = 100f, stamina = 100f;
        public float attackCooldown, interactCooldown, noticeUntil, lastDamageAt;
        public int[] items = new int[DemoCatalog.ItemCount], attributes = new int[5], skills = new int[3];
        public List<GearState> gear = new List<GearState>();
        public int[] parts = new int[7];
        public int weaponId, toolId, activeSlot;
        public int[] armorIds = new int[4];
        public ItemKind selectedAmmo = ItemKind.Ammo;
        public float reloadRemaining, recoil;
        public int reloadingGearId;
        public bool aiming, moving;
        public SurvivalPlayerState needs = new SurvivalPlayerState();
        // Four exploration bits per lower-case hex character. Empty on pre-v8 saves.
        public string explored = "";
        public List<ExploredChunkState> exploredChunks = new List<ExploredChunkState>();
    }

    [Serializable] public sealed class NodeState
    {
        public int id, x, y, stock = 10;
        public NodeKind kind;
        public float hp = 30f;
    }

    [Serializable] public sealed class BuildingState
    {
        public int id, x, y, rotation, footprintVersion, networkId;
        public BuildKind kind;
        public int recipe, setting, clusterId;
        // False on pre-v7 saves: their self-contained boilers retain their old behavior.
        public bool modularBoiler;
        public float temperature = 20f;
        public bool tripped;
        public float hp = 120f, progress, fuel;
        public bool powered;
        public float powerRatio, powerDemand, powerGeneration;
        public int[] items = new int[DemoCatalog.ItemCount];
    }

    [Serializable] public sealed class SteamClusterState
    {
        public int id;
        public int[] buildingIds = new int[0];
        public int boilers, chambers, pumps, chimneys;
        public float heatCapacity, waterCapacity, exhaustCapacity, output;
        public string status = "";
    }

    [Serializable] public sealed class PowerNetworkState
    {
        public int id, nodes, consumers, tier;
        public float generation, demand, ratio;
    }

    [Serializable] public sealed class CargoState
    {
        public int id, fromId, toId, item;
        public float fromX, fromY, toX, toY, progress, duration;
    }

    [Serializable] public sealed class ZombieState
    {
        public int id, variant;
        public float x, y, hp = 45f, attackCooldown, alertedUntil;
    }

    [Serializable] public sealed class TraceState
    {
        public float x, y, endX, endY, until;
        public bool hostile;
        // 0 impact, 1 shot, 2 melee, 3 spit, 4 flame, 5 toxin, 6 frost, 7 blast, 8 scream.
        public int effect;
    }

    [Serializable] public sealed class PlayerCommand
    {
        public string playerId = "", action = "";
        public float moveX, moveY, aimX = 1f, aimY;
        public int x, y, arg, rotation;
        public bool sprint, aiming;
    }

    public static class DemoCatalog
    {
        public const int ItemCount = 54;
        public static readonly string[] ItemNames = { "木材", "铁矿", "铁锭", "废料", "燃料", "9mm弹", "食物", "铜矿", "铜锭", "青铜", "钢", "电路", "聚合物", "高级电路", "铀矿", "核燃料", "冷却液", "核废料", "碎铁矿", "锡矿", "锡锭", "步枪弹", "霰弹", "精密弹", "穿甲弹", "石材", "黏土", "木炭", "陶砖", "铁板", "齿轮", "轴承", "煤", "石灰石", "耐火黏土", "焦炭", "耐火砖", "生铁", "钢板", "耐压管件", "铜线", "陶瓷绝缘件", "绝缘电缆", "线圈", "电机", "继电器", "原油", "轻质馏分", "化工原料", "重质馏分", "柴油", "润滑油", "工艺水", "热冷却液" };
        public static readonly string[] BuildNames = { "防护墙", "燃料发电机", "采矿机", "传送带", "熔炉", "装配机", "机枪塔", "储物箱", "电线杆", "太阳能板", "工作台", "石炉", "水车", "传动轴", "机械粉碎机", "锅炉", "蒸汽管", "蒸汽采矿机", "蒸汽高炉", "蒸汽轮机", "电路装配机", "柴油发电机", "化工厂", "中压电杆", "变压器", "高压发电机", "高压电杆", "精密装配机", "核燃料处理机", "核反应堆", "冷却塔", "出入门", "燃烧室", "烟囱", "取水泵", "蒸汽引擎", "炭窑", "陶窑", "鼓风炉", "机械压机", "机械工坊", "水力磨坊", "灌溉泵", "机械装卸器", "焦炉", "耐火窑", "炼铁高炉", "精炼炉", "机床", "机械绕线台", "过滤分流器", "低压断路器", "电动破碎机", "抽油机", "液体管道", "储液罐", "分馏装置", "基础化学反应器", "工艺取水泵" };
        public static readonly string[] AttributeNames = { "力量", "体质", "敏捷", "感知", "智力" };
        public static readonly string[] SkillNames = { "工程专精", "战斗专精", "生存专精" };
        public static readonly string[] SkillDescriptions = { "提升机器产速与回收效率", "提升武器与炮塔伤害", "降低饥饿消耗，提升采集收益" };
        public static readonly string[] BuildDescriptions = { "阻挡并承受丧尸攻击", "LV 120 kW；每份燃料20秒；E补给", "LV 20 kW；邻接任意矿脉，沿箭头输出原矿", "无需动力；沿箭头运输，每带容量2", "LV 35 kW；可选铁/铜/锡/碎矿配方", "LV 45 kW；弹药、电缆、电机与继电器自动装配", "LV 20 kW；弹药自动防守", "E取出；沿箭头向设备/传送带输出", "LV；7格连线、4格供电，不跨电压", "MV最高40 kW；白昼发电、夜间停机", "靠近后研究原始工艺、手工制作青铜/废料；不花技能点", "新世界用木炭，旧世界用燃料；按研究解锁铁/铜/锡及碎矿配方；每份供热20秒", "岸边干地；40机械功率，经接触的传动轴传动", "机械传动；必须接触同类节点", "20机械功率；1铁矿→1碎矿，碎矿可炼2铁", "模块锅炉：钢板、耐火砖与耐压管件构成；与燃烧室、取水泵、烟囱组成接触集群；最高120蒸汽功率", "耐压管件连接；接触传输蒸汽；不与电网裸连", "30蒸汽功率；邻接铁/铜/锡/铀矿、石材、黏土、煤、石灰石或耐火黏土采集", "50蒸汽功率；3生铁+1石灰石→3钢；4秒；也可冶炼碎矿/铜矿/锡矿", "消耗80蒸汽功率，最多输出64 kW LV", "LV 45 kW；继电器+电缆+铁板→2基础电路", "MV 240 kW；每份精制柴油20秒", "MV 80 kW；化工原料与冷却液扩产聚合物，产热冷却液", "MV；7格连线、4格供电", "默认MV→LV；setting=1为HV→MV；200输入上限、90%效率", "HV 600 kW；每份燃料10秒", "HV；7格连线、4格供电", "HV 140 kW；电路+聚合物+铜→高级电路", "HV 180 kW；铀矿+钢→核燃料", "HV最高1200 kW；核燃料+冷却液；900℃停堆、300℃恢复；产核废料", "MV 60 kW且近水；热冷却液+工艺水回收冷却液", "E开关门；关闭挡路，开启可进入；围墙与门组成室内", "集群模块：接受煤、焦炭或燃料；120热功率；与锅炉等边接触", "陶砖与石材建造；集群模块：120排烟容量；与燃烧室/锅炉/泵边接触", "耐压给水模块：取水120容量；可隔一格完整墙体取水，须接触其他模块", "钢板与耐压管件装配；消耗60蒸汽功率→48机械功率；管道接输入、传动轴接输出", "4木材→2木炭；4秒；无需外部动力", "4黏土+1木炭→4陶砖；4秒；无需外部动力", "25机械功率；碎矿+木炭→3铁，也可冶炼铜/锡；木炭由配方消耗", "20机械功率；1铁锭→1铁板（2秒）；蒸汽研究后1钢→1钢板（3秒）", "15机械功率；铁板+青铜制齿轮，青铜+铁锭制轴承", "10机械功率；水力研磨加工，连接水车与传动轴", "10机械功率；岸边干地；水力灌溉周边农田", "5机械功率；储存物品，沿箭头向设备/传送带输出", "4煤+1木炭→3焦炭；4秒；无需外部动力", "3耐火黏土+1石材+1木炭→4耐火砖；4秒；无需外部动力", "30机械功率；2碎矿+2焦炭+1石灰石→3生铁；4秒", "25机械功率；3生铁+1石灰石+1木炭→2钢；4秒", "25机械功率；1钢板+1青铜→2耐压管件；3秒；首批由水车经传动轴驱动", "25机械功率；4铜线+1绝缘件+1铁锭→1线圈；3秒", "LV 5 kW；所选物品送前方，其余送左侧；停电暂停", "LV同网保护；过载跳闸隔离，降载后人工复位", "LV 25 kW；1铁矿→2碎铁矿；2秒", "LV 20 kW；邻接有限油藏，每2秒抽取1单位原油", "离散液量单向输送；只通选定液种；空管无在途包时可改", "储存与输出选定液体；E取出，空罐时E补充；空罐无在途包可改", "LV 40 kW且近水；原油+燃料→轻馏分、化工原料与重馏分；副产满仓停机", "LV 40 kW且近水；燃料精制、裂化、聚合、润滑与基础冷却液", "LV 10 kW且近水；每2秒取2单位工艺水" };
        public const float LinkRange = 7f, SupplyRadius = 4f, CargoSpeed = 2.5f;
        public static string ItemName(ItemKind item) => ItemNames[(int)item];
        public static string BuildName(BuildKind kind) => BuildNames[(int)kind];
        // Costs use the fixed ItemKind indices.
        public static int[] BuildCost(BuildKind kind)
        {
            switch (kind)
            {
                case BuildKind.Wall: return Materials(ItemKind.Wood, 3);
                case BuildKind.Generator: return Materials(ItemKind.SteelPlate,4,ItemKind.Coil,2,ItemKind.Insulator,2,ItemKind.Gear,2,ItemKind.Bearing,2);
                case BuildKind.Miner: return Materials(ItemKind.SteelPlate,4,ItemKind.Motor,1,ItemKind.Cable,2,ItemKind.Gear,2);
                case BuildKind.Belt: return Materials(ItemKind.Scrap,1);
                case BuildKind.Smelter: return Materials(ItemKind.RefractoryBrick,4,ItemKind.SteelPlate,2,ItemKind.Cable,2,ItemKind.Motor,1);
                case BuildKind.Assembler: return Materials(ItemKind.SteelPlate,4,ItemKind.Motor,2,ItemKind.Circuit,2,ItemKind.Cable,2);
                case BuildKind.Turret: return Materials(ItemKind.Steel,4,ItemKind.Motor,1,ItemKind.Circuit,1,ItemKind.Cable,2);
                case BuildKind.PowerPole: return Materials(ItemKind.Wood,2,ItemKind.Cable,1,ItemKind.Insulator,1);
                case BuildKind.SolarPanel: return Materials(ItemKind.Steel,5, ItemKind.Circuit,4, ItemKind.Copper,4);
                case BuildKind.Chest: return Materials(ItemKind.Wood,4, ItemKind.Scrap,1);
                case BuildKind.Workbench: return Materials(ItemKind.Wood,6);
                case BuildKind.CharcoalKiln: return Materials(ItemKind.Wood,4, ItemKind.Stone,6);
                case BuildKind.ClayKiln: return Materials(ItemKind.Stone,6, ItemKind.Clay,4);
                case BuildKind.StoneFurnace: return Materials(ItemKind.Wood,6, ItemKind.Ore,3);
                case BuildKind.Waterwheel: return Materials(ItemKind.Wood,10, ItemKind.Iron,4);
                case BuildKind.Shaft: return Materials(ItemKind.Wood,2);
                case BuildKind.MechanicalCrusher: return Materials(ItemKind.Wood,6, ItemKind.Iron,6);
                case BuildKind.BlastFurnace: return Materials(ItemKind.Wood,6, ItemKind.Stone,8, ItemKind.Brick,8, ItemKind.Iron,4, ItemKind.Bronze,2);
                case BuildKind.MechanicalPress: return Materials(ItemKind.Wood,6, ItemKind.Iron,6, ItemKind.Bronze,2);
                case BuildKind.MechanicalWorkshop: return Materials(ItemKind.Wood,6, ItemKind.Iron,4, ItemKind.Bronze,4, ItemKind.IronPlate,2);
                case BuildKind.WaterMill: return Materials(ItemKind.Wood,6, ItemKind.Stone,6, ItemKind.Bronze,2);
                case BuildKind.IrrigationPump: return Materials(ItemKind.Wood,4, ItemKind.Bronze,4, ItemKind.Iron,4);
                case BuildKind.MechanicalHopper: return Materials(ItemKind.Wood,4, ItemKind.IronPlate,2, ItemKind.Gear,2, ItemKind.Bearing,1);
                case BuildKind.Door: return Materials(ItemKind.Wood,4, ItemKind.Scrap,1);
                case BuildKind.WindingBench: return Materials(ItemKind.IronPlate,4,ItemKind.Gear,2,ItemKind.Bearing,2);
                case BuildKind.FilterSplitter: return Materials(ItemKind.IronPlate,2,ItemKind.Circuit,1,ItemKind.Cable,2,ItemKind.Motor,1);
                case BuildKind.CircuitBreaker: return Materials(ItemKind.IronPlate,2,ItemKind.Relay,2,ItemKind.Cable,2,ItemKind.Insulator,2);
                case BuildKind.ElectricCrusher: return Materials(ItemKind.IronPlate,4,ItemKind.Motor,1,ItemKind.Cable,2,ItemKind.Gear,2);
                case BuildKind.CokeOven: return Materials(ItemKind.Brick,8, ItemKind.Stone,6, ItemKind.IronPlate,2);
                case BuildKind.RefractoryKiln: return Materials(ItemKind.Brick,6, ItemKind.Stone,6, ItemKind.IronPlate,2);
                case BuildKind.IronBlastFurnace: return Materials(ItemKind.RefractoryBrick,8, ItemKind.IronPlate,4, ItemKind.Gear,2, ItemKind.Bronze,2);
                case BuildKind.RefiningForge: return Materials(ItemKind.RefractoryBrick,6, ItemKind.IronPlate,4, ItemKind.Gear,2);
                case BuildKind.MachineTool: return Materials(ItemKind.IronPlate,4, ItemKind.Steel,2, ItemKind.Gear,2, ItemKind.Bearing,2);
                case BuildKind.CombustionChamber: return Materials(ItemKind.Brick,4, ItemKind.RefractoryBrick,4, ItemKind.IronPlate,2);
                case BuildKind.Chimney: return Materials(ItemKind.Brick,4, ItemKind.Stone,2);
                case BuildKind.FeedwaterPump: return Materials(ItemKind.Steel,2, ItemKind.PipeFitting,2, ItemKind.Bronze,2);
                case BuildKind.SteamEngine: return Materials(ItemKind.SteelPlate,4, ItemKind.PipeFitting,4, ItemKind.Gear,3, ItemKind.Bearing,2);
                case BuildKind.Boiler: return Materials(ItemKind.SteelPlate,4, ItemKind.RefractoryBrick,4, ItemKind.PipeFitting,2);
                case BuildKind.SteamPipe: return Materials(ItemKind.PipeFitting,1);
                case BuildKind.SteamMiner: return Materials(ItemKind.Steel,4, ItemKind.PipeFitting,2, ItemKind.Gear,2, ItemKind.Bearing,2);
                case BuildKind.SteamFurnace: return Materials(ItemKind.RefractoryBrick,6, ItemKind.Steel,4, ItemKind.PipeFitting,2);
                case BuildKind.SteamTurbine: return Materials(ItemKind.Steel,4,ItemKind.Coil,2,ItemKind.Insulator,2,ItemKind.PipeFitting,2);
                case BuildKind.CircuitAssembler: return Materials(ItemKind.SteelPlate,2,ItemKind.Relay,1,ItemKind.Cable,4,ItemKind.Gear,2);
                case BuildKind.OilPump: return Materials(ItemKind.SteelPlate,4,ItemKind.Motor,1,ItemKind.PipeFitting,4,ItemKind.Cable,2);
                case BuildKind.LiquidPipe: return Materials(ItemKind.PipeFitting,1);
                case BuildKind.LiquidTank: return Materials(ItemKind.SteelPlate,3,ItemKind.PipeFitting,2);
                case BuildKind.DistillationUnit: return Materials(ItemKind.SteelPlate,4,ItemKind.RefractoryBrick,6,ItemKind.PipeFitting,4,ItemKind.Motor,1,ItemKind.Cable,2);
                case BuildKind.ChemicalReactor: return Materials(ItemKind.SteelPlate,4,ItemKind.PipeFitting,4,ItemKind.Motor,1,ItemKind.Circuit,2,ItemKind.Cable,2);
                case BuildKind.ProcessWaterPump: return Materials(ItemKind.SteelPlate,2,ItemKind.PipeFitting,2,ItemKind.Motor,1,ItemKind.Cable,1);
                case BuildKind.DieselGenerator: return Materials(ItemKind.SteelPlate,6,ItemKind.Motor,2,ItemKind.Circuit,3,ItemKind.Coil,2,ItemKind.Polymer,2,ItemKind.Lubricant,1);
                case BuildKind.ChemicalPlant: return Materials(ItemKind.SteelPlate,6,ItemKind.Circuit,4,ItemKind.PipeFitting,4,ItemKind.Motor,2,ItemKind.Polymer,2,ItemKind.Lubricant,1);
                case BuildKind.MediumPole: return Materials(ItemKind.Steel,1,ItemKind.Cable,2,ItemKind.Polymer,1);
                case BuildKind.Transformer: return Materials(ItemKind.SteelPlate,3,ItemKind.Coil,2,ItemKind.Circuit,2,ItemKind.Polymer,2,ItemKind.Lubricant,1);
                case BuildKind.HighGenerator: return Materials(ItemKind.Steel,10, ItemKind.Polymer,5, ItemKind.Circuit,5);
                case BuildKind.HighPole: return Materials(ItemKind.Steel,2, ItemKind.Copper,3, ItemKind.Polymer,1);
                case BuildKind.PrecisionAssembler: return Materials(ItemKind.Steel,10, ItemKind.Circuit,6, ItemKind.Polymer,6);
                case BuildKind.NuclearProcessor: return Materials(ItemKind.Steel,12, ItemKind.AdvancedCircuit,3, ItemKind.Polymer,6);
                case BuildKind.Reactor: return Materials(ItemKind.Steel,20, ItemKind.AdvancedCircuit,5, ItemKind.Copper,8);
                case BuildKind.CoolingTower: return Materials(ItemKind.SteelPlate,4,ItemKind.Circuit,3,ItemKind.PipeFitting,4,ItemKind.Motor,1,ItemKind.Polymer,2);
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
        public static int[] Materials(params object[] pairs)
        {
            var result = new int[ItemCount];
            for (int i = 0; i < pairs.Length; i += 2) result[(int)(ItemKind)pairs[i]] = (int)pairs[i + 1];
            return result;
        }
        public static string CostText(BuildKind kind)
        {
            var cost = BuildCost(kind);
            var parts = new List<string>();
            for (int i = 0; i < cost.Length; i++) if (cost[i] > 0) parts.Add(ItemNames[i] + " " + cost[i]);
            return string.Join("  ·  ", parts);
        }
        public static int DirectionX(int rotation) => new[] { 1, 0, -1, 0 }[((rotation % 4) + 4) % 4];
        public static int DirectionY(int rotation) => new[] { 0, 1, 0, -1 }[((rotation % 4) + 4) % 4];
        public static int Width(BuildKind kind, int rotation = 0)
        {
            int w = kind == BuildKind.SteamEngine || kind == BuildKind.Assembler || kind == BuildKind.SolarPanel || kind == BuildKind.ChemicalPlant || kind == BuildKind.PrecisionAssembler || kind == BuildKind.NuclearProcessor || kind == BuildKind.Reactor || kind == BuildKind.CoolingTower || kind == BuildKind.HighGenerator ? 3 :
                kind == BuildKind.OilPump || kind == BuildKind.LiquidTank || kind == BuildKind.DistillationUnit || kind == BuildKind.ChemicalReactor || kind == BuildKind.ProcessWaterPump || kind == BuildKind.CokeOven || kind == BuildKind.RefractoryKiln || kind == BuildKind.IronBlastFurnace || kind == BuildKind.RefiningForge || kind == BuildKind.MachineTool || kind == BuildKind.WindingBench || kind == BuildKind.ElectricCrusher ||
                kind == BuildKind.CharcoalKiln || kind == BuildKind.ClayKiln || kind == BuildKind.CombustionChamber || kind == BuildKind.Generator || kind == BuildKind.Miner || kind == BuildKind.Smelter || kind == BuildKind.StoneFurnace || kind == BuildKind.Waterwheel || kind == BuildKind.MechanicalCrusher || kind == BuildKind.BlastFurnace || kind == BuildKind.MechanicalPress || kind == BuildKind.MechanicalWorkshop || kind == BuildKind.WaterMill || kind == BuildKind.IrrigationPump || kind == BuildKind.Boiler || kind == BuildKind.SteamMiner || kind == BuildKind.SteamFurnace || kind == BuildKind.SteamTurbine || kind == BuildKind.CircuitAssembler || kind == BuildKind.DieselGenerator || kind == BuildKind.Transformer ? 2 : 1;
            int h = kind == BuildKind.FeedwaterPump ? 2 : w > 1 ? 2 : 1;
            return (rotation & 1) == 0 ? w : h;
        }
        public static int Height(BuildKind kind, int rotation = 0) => Width(kind, rotation + 1);
        public static int Width(BuildingState b) => b.footprintVersion == 0 ? 1 : Width(b.kind, b.rotation);
        public static int Height(BuildingState b) => b.footprintVersion == 0 ? 1 : Height(b.kind, b.rotation);
        public static float CenterX(BuildingState b) => b.x + (Width(b) - 1) * .5f;
        public static float CenterY(BuildingState b) => b.y + (Height(b) - 1) * .5f;
        public static bool Contains(BuildingState b, int x, int y) => x >= b.x && x < b.x + Width(b) && y >= b.y && y < b.y + Height(b);
        public static bool Walkable(BuildingState b) => b.kind == BuildKind.Belt || (b.kind == BuildKind.Door && b.setting == 1);
        public static int OutputX(BuildingState b) => b.rotation == 0 ? b.x + Width(b) : b.rotation == 2 ? b.x - 1 : b.x + (Width(b) - 1) / 2;
        public static int OutputY(BuildingState b) => b.rotation == 1 ? b.y + Height(b) : b.rotation == 3 ? b.y - 1 : b.y + (Height(b) - 1) / 2;
        public static float Demand(BuildKind kind)
        {
            switch (kind)
            {
                case BuildKind.OilPump: case BuildKind.Miner: case BuildKind.Turret: case BuildKind.MechanicalCrusher: case BuildKind.MechanicalPress: return 20f;
                case BuildKind.BlastFurnace: case BuildKind.RefiningForge: case BuildKind.MachineTool: case BuildKind.WindingBench: case BuildKind.ElectricCrusher: return 25f;
                case BuildKind.FilterSplitter: return 5f;
                case BuildKind.IronBlastFurnace: return 30f;
                case BuildKind.MechanicalWorkshop: return 15f;
                case BuildKind.ProcessWaterPump: case BuildKind.WaterMill: case BuildKind.IrrigationPump: return 10f;
                case BuildKind.MechanicalHopper: return 5f;
                case BuildKind.Smelter: return 35f;
                case BuildKind.Assembler: case BuildKind.CircuitAssembler: return 45f;
                case BuildKind.SteamEngine: return 60f;
                case BuildKind.SteamMiner: return 30f;
                case BuildKind.SteamFurnace: return 50f;
                case BuildKind.SteamTurbine: case BuildKind.ChemicalPlant: return 80f;
                case BuildKind.PrecisionAssembler: return 140f;
                case BuildKind.NuclearProcessor: return 180f;
                case BuildKind.Transformer: return 200f;
                case BuildKind.DistillationUnit: case BuildKind.ChemicalReactor: return 40f;
                case BuildKind.CoolingTower: return 60f;
                default: return 0f;
            }
        }
        public static bool IsPowerNode(BuildKind kind)
        {
            switch (kind)
            {
                case BuildKind.CircuitBreaker: case BuildKind.Generator: case BuildKind.SolarPanel: case BuildKind.PowerPole:
                case BuildKind.SteamEngine: case BuildKind.Waterwheel: case BuildKind.Shaft: case BuildKind.Boiler: case BuildKind.SteamPipe:
                case BuildKind.SteamTurbine: case BuildKind.DieselGenerator: case BuildKind.MediumPole:
                case BuildKind.Transformer: case BuildKind.HighGenerator: case BuildKind.HighPole: case BuildKind.Reactor: return true;
                default: return false;
            }
        }
    }
}
