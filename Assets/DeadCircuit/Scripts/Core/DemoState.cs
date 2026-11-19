using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public enum GroundKind { Grass, Dirt, Water, Road, Floor }
    public enum TechEra { Primitive = 0, Water = 1, Steam = 2, LowVoltage = 3, MediumVoltage = 4, HighVoltage = 5, Nuclear = 6 }
    public enum NodeKind { Tree, Ore, Scrap, Crate, Wall, CopperOre = 5, TinOre = 6, UraniumOre = 7 }
    public enum BuildKind { Wall, Generator, Miner, Belt, Smelter, Assembler, Turret, Chest, PowerPole, SolarPanel, Workbench = 10, StoneFurnace, Waterwheel, Shaft, MechanicalCrusher, Boiler, SteamPipe, SteamMiner, SteamFurnace, SteamTurbine, CircuitAssembler, DieselGenerator, ChemicalPlant, MediumPole, Transformer, HighGenerator, HighPole, PrecisionAssembler, NuclearProcessor, Reactor, CoolingTower, Door = 31, CombustionChamber, Chimney, FeedwaterPump, SteamEngine }
    public enum ItemKind { Wood, Ore, Iron, Scrap, Fuel, Ammo, Food, CopperOre = 7, Copper, Bronze, Steel, Circuit, Polymer, AdvancedCircuit, UraniumOre, NuclearFuel, Coolant, NuclearWaste, CrushedOre, TinOre, Tin, RifleAmmo = 21, Shell = 22, PrecisionAmmo = 23, ArmorPiercingAmmo = 24 }
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
        public int version = 8, seed, width = 64, height = 64, nextId = 1, wave;
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
        public const int ItemCount = 25;
        public static readonly string[] ItemNames = { "木材", "铁矿", "铁锭", "废料", "燃料", "9mm弹", "食物", "铜矿", "铜锭", "青铜", "钢", "电路", "聚合物", "高级电路", "铀矿", "核燃料", "冷却液", "核废料", "碎铁矿", "锡矿", "锡锭", "步枪弹", "霰弹", "精密弹", "穿甲弹" };
        public static readonly string[] BuildNames = { "防护墙", "燃料发电机", "采矿机", "传送带", "熔炉", "装配机", "机枪塔", "储物箱", "电线杆", "太阳能板", "工作台", "石炉", "水车", "传动轴", "机械粉碎机", "锅炉", "蒸汽管", "蒸汽采矿机", "蒸汽高炉", "蒸汽轮机", "电路装配机", "柴油发电机", "化工厂", "中压电杆", "变压器", "高压发电机", "高压电杆", "精密装配机", "核燃料处理机", "核反应堆", "冷却塔", "出入门", "燃烧室", "烟囱", "取水泵", "蒸汽引擎" };
        public static readonly string[] AttributeNames = { "力量", "体质", "敏捷", "感知", "智力" };
        public static readonly string[] SkillNames = { "工程专精", "战斗专精", "生存专精" };
        public static readonly string[] SkillDescriptions = { "提升机器产速与回收效率", "提升武器与炮塔伤害", "降低饥饿消耗，提升采集收益" };
        public static readonly string[] BuildDescriptions = { "阻挡并承受丧尸攻击", "LV 120 kW；每份燃料20秒；E补给", "LV 20 kW；邻接任意矿脉，沿箭头输出原矿", "无需动力；沿箭头运输，每带容量2", "LV 35 kW；可选铁/铜/锡/碎矿配方", "LV 45 kW；铁锭装配弹药", "LV 20 kW；弹药自动防守", "E取出；沿箭头向设备/传送带输出", "LV；7格连线、4格供电，不跨电压", "MV最高40 kW；白昼发电、夜间停机", "靠近后手工制作青铜/废料；不花技能点", "消耗燃料；可选铁/铜/锡及碎矿配方；每份燃料20秒", "岸边干地；40机械功率，经接触的传动轴传动", "机械传动；必须接触同类节点", "20机械功率；1铁矿→1碎矿，碎矿可炼2铁", "模块锅炉：与燃烧室、取水泵、烟囱组成接触集群；最高120蒸汽功率", "接触传输蒸汽；不与电网裸连", "30蒸汽功率；邻接铁矿自动采集", "50蒸汽功率；铁锭+燃料炼钢，也可冶炼原矿", "消耗80蒸汽功率，最多输出64 kW LV", "LV 45 kW；铜锭+铁锭→电路", "MV 240 kW；每份燃料20秒", "MV 80 kW；聚合物/近水罐装冷却液可选", "MV；7格连线、4格供电", "默认MV→LV；setting=1为HV→MV；200输入上限、90%效率", "HV 600 kW；每份燃料10秒", "HV；7格连线、4格供电", "HV 140 kW；电路+聚合物+铜→高级电路", "HV 180 kW；铀矿+钢→核燃料", "HV最高1200 kW；核燃料+冷却液；900℃停堆、300℃恢复；产核废料", "MV 60 kW且近水；每3秒产生4罐冷却液", "E开关门；关闭挡路，开启可进入；围墙与门组成室内", "集群模块：每份燃料20秒、120热功率；与锅炉等边接触", "集群模块：120排烟容量；与燃烧室/锅炉/泵边接触", "集群模块：取水120容量；可隔一格完整墙体取水，须接触其他模块", "消耗60蒸汽功率→48机械功率；管道接输入、传动轴接输出" };
        public const float LinkRange = 7f, SupplyRadius = 4f, CargoSpeed = 2.5f;
        public static string ItemName(ItemKind item) => ItemNames[(int)item];
        public static string BuildName(BuildKind kind) => BuildNames[(int)kind];
        // Costs are ordered as Wood, Ore, Iron, Scrap, Fuel, Ammo, Food.
        public static int[] BuildCost(BuildKind kind)
        {
            int[] cost;
            switch (kind)
            {
                case BuildKind.Wall: return Materials(ItemKind.Wood, 3);
                case BuildKind.Generator: cost = Materials(ItemKind.Wood,5, ItemKind.Iron,3, ItemKind.Scrap,8); break;
                case BuildKind.Miner: cost = Materials(ItemKind.Wood,3, ItemKind.Iron,4, ItemKind.Scrap,5); break;
                case BuildKind.Belt: return Materials(ItemKind.Scrap,1);
                case BuildKind.Smelter: cost = Materials(ItemKind.Wood,6, ItemKind.Iron,2, ItemKind.Scrap,4); break;
                case BuildKind.Assembler: cost = Materials(ItemKind.Wood,3, ItemKind.Iron,5, ItemKind.Scrap,5); break;
                case BuildKind.Turret: cost = Materials(ItemKind.Iron,6, ItemKind.Scrap,8); break;
                case BuildKind.PowerPole: return Materials(ItemKind.Wood,2, ItemKind.Scrap,2);
                case BuildKind.SolarPanel: return Materials(ItemKind.Steel,5, ItemKind.Circuit,4, ItemKind.Copper,4);
                case BuildKind.Chest: return Materials(ItemKind.Wood,4, ItemKind.Scrap,1);
                case BuildKind.Workbench: return Materials(ItemKind.Wood,6);
                case BuildKind.StoneFurnace: return Materials(ItemKind.Wood,6, ItemKind.Ore,3);
                case BuildKind.Waterwheel: return Materials(ItemKind.Wood,10, ItemKind.Iron,4);
                case BuildKind.Shaft: return Materials(ItemKind.Wood,2);
                case BuildKind.MechanicalCrusher: return Materials(ItemKind.Wood,6, ItemKind.Iron,6);
                case BuildKind.Door: return Materials(ItemKind.Wood,4, ItemKind.Scrap,1);
                case BuildKind.CombustionChamber: return Materials(ItemKind.Iron,5, ItemKind.Ore,4);
                case BuildKind.Chimney: return Materials(ItemKind.Iron,2, ItemKind.Ore,2);
                case BuildKind.FeedwaterPump: return Materials(ItemKind.Bronze,3, ItemKind.Iron,2);
                case BuildKind.SteamEngine: return Materials(ItemKind.Bronze,5, ItemKind.Iron,5);
                case BuildKind.Boiler: return Materials(ItemKind.Bronze,5, ItemKind.Iron,6);
                case BuildKind.SteamPipe: return Materials(ItemKind.Bronze,1);
                case BuildKind.SteamMiner: return Materials(ItemKind.Bronze,4, ItemKind.Iron,6);
                case BuildKind.SteamFurnace: return Materials(ItemKind.Bronze,5, ItemKind.Iron,8);
                case BuildKind.SteamTurbine: return Materials(ItemKind.Steel,4, ItemKind.Bronze,4, ItemKind.Copper,3);
                case BuildKind.CircuitAssembler: return Materials(ItemKind.Steel,4, ItemKind.Copper,4, ItemKind.Iron,4);
                case BuildKind.DieselGenerator: return Materials(ItemKind.Steel,6, ItemKind.Circuit,3, ItemKind.Copper,4);
                case BuildKind.ChemicalPlant: return Materials(ItemKind.Steel,8, ItemKind.Circuit,4, ItemKind.Bronze,4);
                case BuildKind.MediumPole: return Materials(ItemKind.Steel,1, ItemKind.Copper,2);
                case BuildKind.Transformer: return Materials(ItemKind.Steel,3, ItemKind.Circuit,2, ItemKind.Copper,4);
                case BuildKind.HighGenerator: return Materials(ItemKind.Steel,10, ItemKind.Polymer,5, ItemKind.Circuit,5);
                case BuildKind.HighPole: return Materials(ItemKind.Steel,2, ItemKind.Copper,3, ItemKind.Polymer,1);
                case BuildKind.PrecisionAssembler: return Materials(ItemKind.Steel,10, ItemKind.Circuit,6, ItemKind.Polymer,6);
                case BuildKind.NuclearProcessor: return Materials(ItemKind.Steel,12, ItemKind.AdvancedCircuit,3, ItemKind.Polymer,6);
                case BuildKind.Reactor: return Materials(ItemKind.Steel,20, ItemKind.AdvancedCircuit,5, ItemKind.Copper,8);
                case BuildKind.CoolingTower: return Materials(ItemKind.Steel,8, ItemKind.Circuit,3, ItemKind.Copper,4);
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
            return cost;
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
                kind == BuildKind.CombustionChamber || kind == BuildKind.Generator || kind == BuildKind.Miner || kind == BuildKind.Smelter || kind == BuildKind.StoneFurnace || kind == BuildKind.Waterwheel || kind == BuildKind.MechanicalCrusher || kind == BuildKind.Boiler || kind == BuildKind.SteamMiner || kind == BuildKind.SteamFurnace || kind == BuildKind.SteamTurbine || kind == BuildKind.CircuitAssembler || kind == BuildKind.DieselGenerator || kind == BuildKind.Transformer ? 2 : 1;
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
                case BuildKind.Miner: case BuildKind.Turret: case BuildKind.MechanicalCrusher: return 20f;
                case BuildKind.Smelter: return 35f;
                case BuildKind.Assembler: case BuildKind.CircuitAssembler: return 45f;
                case BuildKind.SteamEngine: return 60f;
                case BuildKind.SteamMiner: return 30f;
                case BuildKind.SteamFurnace: return 50f;
                case BuildKind.SteamTurbine: case BuildKind.ChemicalPlant: return 80f;
                case BuildKind.PrecisionAssembler: return 140f;
                case BuildKind.NuclearProcessor: return 180f;
                case BuildKind.Transformer: return 200f;
                case BuildKind.CoolingTower: return 60f;
                default: return 0f;
            }
        }
        public static bool IsPowerNode(BuildKind kind)
        {
            switch (kind)
            {
                case BuildKind.Generator: case BuildKind.SolarPanel: case BuildKind.PowerPole:
                case BuildKind.SteamEngine: case BuildKind.Waterwheel: case BuildKind.Shaft: case BuildKind.Boiler: case BuildKind.SteamPipe:
                case BuildKind.SteamTurbine: case BuildKind.DieselGenerator: case BuildKind.MediumPole:
                case BuildKind.Transformer: case BuildKind.HighGenerator: case BuildKind.HighPole: case BuildKind.Reactor: return true;
                default: return false;
            }
        }
    }
}
