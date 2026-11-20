using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;

namespace DeadCircuit
{
    public enum WorldPreset { Balanced = 0, Wilderness = 1, Urban = 2 }
    public enum BiomeKind
    {
        Meadow = 0, DeciduousForest = 1, ConiferousForest = 2, Desert = 3,
        Wetland = 4, Tundra = 5, Swamp = 6, SnowMountain = 7,
        SakuraGrove = 8, RiverValley = 9
    }
    public enum DistrictKind { Wilderness = 0, Suburb = 1, Urban = 2, Industrial = 3 }
    public enum SiteKind
    {
        Residence = 0, Shop = 1, Warehouse = 2, Workshop = 3, Clinic = 4, Farmhouse = 5,
        GasStation = 6, PoliceStation = 7, School = 8, Apartment = 9, Greenhouse = 10, Substation = 11,
        Restaurant = 12, Pharmacy = 13, HardwareStore = 14, Supermarket = 15, Library = 16,
        FireStation = 17, Hospital = 18, ResearchLab = 19, PowerPlant = 20, WaterTreatment = 21,
        RadioStation = 22, Bunker = 23, LumberMill = 24, MineOffice = 25, TrainDepot = 26,
        Barn = 27, HuntingCabin = 28, RangerStation = 29, Motel = 30, RecyclingYard = 31
    }

    [Serializable] public sealed class WorldGenerationSettings
    {
        public string worldName = "末日新世界";
        public int seed = 73149;
        // Empty uses seed. A nonempty value overrides seed on a copied settings instance.
        public string seedText = "";
        public WorldPreset preset = WorldPreset.Balanced;
        public int cityDensity = 1;
        public bool chunked;
    }

    [Serializable] public sealed class SiteState
    {
        // IDs share WorldState.nextId with nodes, machines and zombies.
        public int id, x, y, width, height, doorX, doorY;
        public SiteKind kind;
        public DistrictKind district;
        public SettlementKind settlement;
        // Ground sites have parentSiteId=level=0; destruction never removes identity.
        public int parentSiteId, level;
        // Metadata only: saved entities are never reconstructed from this plan.
        public int layoutVersion, rotation;
    }

    public static class WorldSeed
    {
        public static int Resolve(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 73149;
            text = text.Trim();
            int numeric;
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric)) return numeric;
            // FNV-1a over UTF-16 code units; independent of runtime string hashes/culture.
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char c in text) { hash ^= c; hash *= 16777619u; }
                return (int)hash;
            }
        }

        public static int Random()
        {
            var bytes = new byte[4];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            return unchecked((int)((uint)bytes[0] | (uint)bytes[1] << 8 | (uint)bytes[2] << 16 | (uint)bytes[3] << 24));
        }
    }

    public sealed class BiomeDefinition
    {
        public readonly BiomeKind kind;
        public readonly string name;
        public readonly int dirtPercent, treePermille;
        internal BiomeDefinition(BiomeKind kind, string name, int dirtPercent, int treePermille)
        { this.kind = kind; this.name = name; this.dirtPercent = dirtPercent; this.treePermille = treePermille; }
    }

    public sealed class SiteDefinition
    {
        public readonly SiteKind kind;
        public readonly string name;
        public readonly int width, height, containers;
        internal SiteDefinition(SiteKind kind, string name, int width, int height, int containers)
        { this.kind = kind; this.name = name; this.width = width; this.height = height; this.containers = containers; }
    }

    public static class WorldGenerationCatalog
    {
        public static readonly IReadOnlyList<BiomeDefinition> Biomes = Array.AsReadOnly(new[]
        {
            new BiomeDefinition(BiomeKind.Meadow, "草甸", 18, 90),
            new BiomeDefinition(BiomeKind.DeciduousForest, "阔叶林", 25, 360),
            new BiomeDefinition(BiomeKind.ConiferousForest, "针叶林", 32, 410),
            new BiomeDefinition(BiomeKind.Desert, "荒漠", 92, 18),
            new BiomeDefinition(BiomeKind.Wetland, "湿地", 40, 160),
            new BiomeDefinition(BiomeKind.Tundra, "冻原", 65, 45),
            new BiomeDefinition(BiomeKind.Swamp, "沼泽", 52, 210),
            new BiomeDefinition(BiomeKind.SnowMountain, "雪山", 78, 105),
            new BiomeDefinition(BiomeKind.SakuraGrove, "樱花林", 28, 440),
            new BiomeDefinition(BiomeKind.RiverValley, "河谷", 24, 300)
        });
        public static readonly IReadOnlyList<SiteDefinition> Sites = Array.AsReadOnly(new[]
        {
            new SiteDefinition(SiteKind.Residence, "住宅", 5, 5, 1),
            new SiteDefinition(SiteKind.Shop, "商店", 6, 5, 2),
            new SiteDefinition(SiteKind.Warehouse, "仓库", 7, 7, 3),
            new SiteDefinition(SiteKind.Workshop, "车间", 6, 6, 2),
            new SiteDefinition(SiteKind.Clinic, "诊所", 5, 6, 2),
            new SiteDefinition(SiteKind.Farmhouse, "农舍", 5, 5, 2),
            new SiteDefinition(SiteKind.GasStation, "加油站", 7, 5, 2),
            new SiteDefinition(SiteKind.PoliceStation, "警局", 6, 6, 2),
            new SiteDefinition(SiteKind.School, "学校", 7, 6, 3),
            new SiteDefinition(SiteKind.Apartment, "公寓", 7, 7, 3),
            new SiteDefinition(SiteKind.Greenhouse, "温室", 5, 7, 1),
            new SiteDefinition(SiteKind.Substation, "变电站", 5, 5, 1),
            new SiteDefinition(SiteKind.Restaurant, "餐馆", 9, 8, 2),
            new SiteDefinition(SiteKind.Pharmacy, "药房", 8, 7, 2),
            new SiteDefinition(SiteKind.HardwareStore, "五金店", 9, 8, 2),
            new SiteDefinition(SiteKind.Supermarket, "超市", 11, 10, 4),
            new SiteDefinition(SiteKind.Library, "图书馆", 10, 9, 2),
            new SiteDefinition(SiteKind.FireStation, "消防站", 10, 9, 3),
            new SiteDefinition(SiteKind.Hospital, "医院", 12, 11, 4),
            new SiteDefinition(SiteKind.ResearchLab, "研究所", 11, 10, 3),
            new SiteDefinition(SiteKind.PowerPlant, "发电厂", 12, 10, 3),
            new SiteDefinition(SiteKind.WaterTreatment, "净水站", 11, 10, 3),
            new SiteDefinition(SiteKind.RadioStation, "广播站", 9, 9, 2),
            new SiteDefinition(SiteKind.Bunker, "地下掩体入口", 10, 9, 3),
            new SiteDefinition(SiteKind.LumberMill, "伐木场", 11, 10, 3),
            new SiteDefinition(SiteKind.MineOffice, "矿场办公室", 9, 8, 2),
            new SiteDefinition(SiteKind.TrainDepot, "铁路货运站", 12, 9, 3),
            new SiteDefinition(SiteKind.Barn, "谷仓", 9, 9, 3),
            new SiteDefinition(SiteKind.HuntingCabin, "猎人小屋", 7, 7, 2),
            new SiteDefinition(SiteKind.RangerStation, "护林站", 9, 8, 2),
            new SiteDefinition(SiteKind.Motel, "汽车旅馆", 12, 9, 4),
            new SiteDefinition(SiteKind.RecyclingYard, "回收站", 11, 10, 3)
        });
    }

    public static class WorldGeneration
    {
        public const int CurrentGeneratorVersion = 9;
        public const int BiomeCount = 10;
        public const int AllBiomeMask = (1 << BiomeCount) - 1;
        public const int LegacySiteCount = 12;
        private const int Size = 64;

        public static WorldState Generate(WorldGenerationSettings settings)
        {
            var options = CloneSettings(settings);
            Validate(options);
            options.worldName = options.worldName.Trim();
            options.seedText = options.seedText.Trim();
            if (options.seedText.Length > 0) options.seed = WorldSeed.Resolve(options.seedText);
            if (options.chunked) return ChunkStreaming.Create(options);
            var state = new WorldState { seed = options.seed, generation = options, generatorVersion = CurrentGeneratorVersion, primitiveVersion = 1 };
            state.biomes = GenerateBiomes(options.seed);
            var ground = new char[Size * Size];
            var reserved = new bool[ground.Length];
            var occupied = new HashSet<int>();
            GenerateTerrain(state, ground);
            AddTutorial(state, ground, reserved, occupied);
            // The original two-cell arterial cross also keeps all six initial enemies safe.
            for (int i = 0; i < Size; i++)
            {
                Road(ground, reserved, i, 32); Road(ground, reserved, i, 33);
                Road(ground, reserved, 32, i); Road(ground, reserved, 33, i);
            }
            GenerateSites(state, ground, reserved, occupied);
            GenerateResources(state, ground, reserved, occupied);
            state.ground = new string(ground);
            int[] initialX = { 14, 50, 32, 32, 12, 54 }, initialY = { 32, 32, 14, 54, 33, 33 };
            for (int i = 0; i < initialX.Length; i++)
                state.zombies.Add(new ZombieState { id = state.nextId++, x = initialX[i], y = initialY[i],
                    variant = i == 5 ? 1 : 0, hp = i == 5 ? 32f : 45f });
            state.announcement = "原始时代：工作台(29,35)、石炉(28,37)，附近有石材与黏土。按T研究：工具→烧炭→制陶→铜锡→青铜→块炼铁；满足实际产量后进入水力。科技全队共享。";
            state.announcementUntil = 28f;
            WorldSimulator.PrepareGeneratedWorld(state);
            return state;
        }

        // Metadata only: no terrain/entity reconstruction, list replacement or version change.
        // Main marks migrated worlds generatorVersion=0 and initializes any missing sites list.
        public static void LegacyMetadata(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.width != Size || state.height != Size) throw new ArgumentException("Map must be 64 x 64.", nameof(state));
            if (state.generation == null) state.generation = new WorldGenerationSettings { seed = state.seed };
            if (string.IsNullOrEmpty(state.biomes)) state.biomes = GenerateBiomes(state.seed);
        }

        // Integer-only local RNG: reproducible across Unity/.NET, including all signed seeds.
        private sealed class StableRandom
        {
            private uint state;
            public StableRandom(int seed, uint stream)
            {
                unchecked
                {
                    state = (uint)seed ^ stream;
                    state ^= state >> 16; state *= 0x7feb352du; state ^= state >> 15;
                    state *= 0x846ca68bu; state ^= state >> 16;
                    if (state == 0) state = 0x9e3779b9u;
                }
            }
            public int Next(int min, int max)
            {
                unchecked { state ^= state << 13; state ^= state >> 17; state ^= state << 5; }
                return min + (int)(state % (uint)(max - min));
            }
        }

        // New worlds sample climate and water in global tile coordinates.
        private static string GenerateBiomes(int seed)
        {
            var result = new char[Size * Size];
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
                result[x + y * Size] = (char)('0' + (int)ChunkTerrain.SampleBiome(seed, x, y));
            return new string(result);
        }

        private static bool Tutorial(int x, int y) => x >= 22 && x <= 42 && y >= 22 && y <= 42;

        private static void GenerateTerrain(WorldState state, char[] ground)
        {
            if (state.generatorVersion >= 8)
            {
                for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
                {
                    ChunkTerrain.SampleContinuous(state.seed, x, y, out ground[x + y * Size], out _);
                    // Keep the finite tutorial map's outer edge traversable;
                    // sparse chunks continue to follow the unmodified world field.
                    if (x == 0 || y == 0 || x == Size - 1 || y == Size - 1) ground[x + y * Size] = '0';
                }
                return;
            }
            var random = new StableRandom(state.seed, 0x7e22a001u);
            for (int c = 0; c < ground.Length; c++)
                ground[c] = random.Next(0, 100) < WorldGenerationCatalog.Biomes[state.biomes[c] - '0'].dirtPercent ? '1' : '0';
            for (int n = 0; n < 16; n++)
            {
                int cx = random.Next(6, 58), cy = random.Next(6, 58);
                int r = state.biomes[cx + cy * Size] == (char)('0' + (int)BiomeKind.Wetland) ||
                    state.biomes[cx + cy * Size] == (char)('0' + (int)BiomeKind.Swamp) ? random.Next(3, 5) : random.Next(1, 4);
                for (int y = cy - r; y <= cy + r; y++) for (int x = cx - r; x <= cx + r; x++)
                {
                    int dx = x - cx, dy = y - cy;
                    if (x >= 3 && y >= 3 && x <= 60 && y <= 60 && !Tutorial(x, y) && dx * dx + dy * dy <= r * r)
                        ground[x + y * Size] = '2';
                }
            }
        }

        private static void Road(char[] ground, bool[] reserved, int x, int y)
        { int c = x + y * Size; ground[c] = '3'; reserved[c] = true; }

        private static void AddNode(WorldState state, HashSet<int> occupied, int x, int y, NodeKind kind, int stock)
        {
            if (!occupied.Add(x + y * Size)) throw new InvalidOperationException("World generation attempted an occupied cell.");
            state.nodes.Add(new NodeState { id = state.nextId++, x = x, y = y, kind = kind, stock = stock, hp = kind == NodeKind.Wall ? 160f : 35f });
        }

        private static void AddTutorial(WorldState state, char[] ground, bool[] reserved, HashSet<int> occupied)
        {
            for (int y = 22; y <= 42; y++) for (int x = 22; x <= 42; x++) reserved[x + y * Size] = true;
            for (int y = 26; y <= 39; y++) for (int x = 23; x <= 40; x++) ground[x + y * Size] = '4';
            for (int y = 24; y <= 29; y++) for (int x = 23; x <= 24; x++) ground[x + y * Size] = '2';
            for (int y = 25; y <= 26; y++) for (int x = 25; x <= 26; x++) ground[x + y * Size] = '0';
            AddNode(state, occupied, 24, 30, NodeKind.Ore, 3000);
            AddNode(state, occupied, 24, 34, NodeKind.CopperOre, 1600);
            AddNode(state, occupied, 24, 37, NodeKind.TinOre, 1000);
            AddNode(state, occupied, 40, 37, NodeKind.UraniumOre, 500);
            AddNode(state, occupied, 25, 34, NodeKind.Tree, 30);
            AddNode(state, occupied, 25, 35, NodeKind.Tree, 30);
            AddNode(state, occupied, 26, 36, NodeKind.Tree, 30);
            if (state.generatorVersion >= 6)
            {
                AddNode(state, occupied, 27, 34, NodeKind.Stone, 600);
                AddNode(state, occupied, 27, 36, NodeKind.Clay, 600);
            }
            if (state.generatorVersion >= 8)
            {
                // Exposed finite deposits on the reserved, dry tutorial floor;
                // no crate loot, research or random biome roll gates steam.
                AddNode(state, occupied, 37, 35, NodeKind.Coal, 600);
                AddNode(state, occupied, 38, 37, NodeKind.Limestone, 600);
                AddNode(state, occupied, 39, 35, NodeKind.FireClay, 600);
            }
            AddNode(state, occupied, 36, 34, NodeKind.Scrap, 60);
            AddNode(state, occupied, 35, 35, NodeKind.Crate, 16);
            AddNode(state, occupied, 36, 37, NodeKind.Crate, 16);
            AddStarter(state, occupied, BuildKind.Workbench, 29, 35);
            AddStarter(state, occupied, BuildKind.StoneFurnace, 28, 37);
            AddStarter(state, occupied, BuildKind.Chest, 30, 37).items[(int)ItemKind.Wood] = 12;
        }

        private static BuildingState AddStarter(WorldState state, HashSet<int> occupied, BuildKind kind, int x, int y)
        {
            var b = new BuildingState { id = state.nextId++, kind = kind, x = x, y = y, rotation = 0,
                footprintVersion = 1, hp = WorldSimulator.BuildingMaxHealth(kind) };
            for (int iy = y; iy < y + DemoCatalog.Height(b); iy++) for (int ix = x; ix < x + DemoCatalog.Width(b); ix++)
                if (!occupied.Add(ix + iy * Size)) throw new InvalidOperationException("Overlapping tutorial machine.");
            state.buildings.Add(b); return b;
        }

        private static void GenerateSites(WorldState state, char[] ground, bool[] reserved, HashSet<int> occupied)
        {
            var random = new StableRandom(state.seed, 0xc17b1001u);
            int[] starts = { 3, 12, 22, 35, 46, 56 }, ends = { 9, 18, 30, 42, 52, 60 };
            int[] streets = { 10, 20, 32, 33, 44, 54 };
            var lots = new List<int>();
            for (int row = 0; row < 6; row++) for (int col = 0; col < 6; col++)
                if (!Tutorial(starts[col], starts[row])) lots.Add(col + row * 6);
            for (int i = lots.Count - 1; i > 0; i--) { int j = random.Next(0, i + 1), v = lots[i]; lots[i] = lots[j]; lots[j] = v; }
            int target = state.generation.preset == WorldPreset.Wilderness ? 6 + state.generation.cityDensity * 4 :
                state.generation.preset == WorldPreset.Urban ? 18 + state.generation.cityDensity * 5 : 12 + state.generation.cityDensity * 6;
            int kindOffset = random.Next(0, LegacySiteCount);
            foreach (int lot in lots)
            {
                if (state.sites.Count >= target) break;
                int col = lot % 6, row = lot / 6;
                var definition = WorldGenerationCatalog.Sites[(state.sites.Count + kindOffset) % LegacySiteCount];
                int width = Math.Min(definition.width, ends[col] - starts[col] + 1);
                int height = Math.Min(definition.height, ends[row] - starts[row] + 1);
                int sx = starts[col] + random.Next(0, ends[col] - starts[col] - width + 2);
                int sy = starts[row] + random.Next(0, ends[row] - starts[row] - height + 2);
                bool clear = true;
                // Only empty lots are developed; existing nodes/machines are never removed.
                for (int y = sy; y < sy + height; y++) for (int x = sx; x < sx + width; x++)
                    if (Tutorial(x, y) || reserved[x + y * Size] || occupied.Contains(x + y * Size)) clear = false;
                if (!clear) continue;
                var site = new SiteState { id = state.nextId++, kind = definition.kind, x = sx, y = sy, width = width, height = height,
                    doorX = sx + width / 2, doorY = streets[row] < sy ? sy : sy + height - 1 };
                site.district = District(state.generation, site); state.sites.Add(site);
                // Reserve an exterior halo before scattering natural resources.
                for (int y = sy - 1; y <= sy + height; y++) for (int x = sx - 1; x <= sx + width; x++) reserved[x + y * Size] = true;
                for (int y = sy; y < sy + height; y++) for (int x = sx; x < sx + width; x++)
                {
                    ground[x + y * Size] = '4';
                    if ((x == sx || y == sy || x == sx + width - 1 || y == sy + height - 1) && !(x == site.doorX && y == site.doorY))
                        AddNode(state, occupied, x, y, NodeKind.Wall, 14);
                }
                AddStarter(state, occupied, BuildKind.Door, site.doorX, site.doorY);
                for (int k = 0; k < definition.containers; k++)
                {
                    int cx = k == 1 ? sx + width - 2 : sx + 1;
                    int cy = k == 2 ? sy + height - 2 : sy + 1;
                    AddNode(state, occupied, cx, cy, NodeKind.Crate, random.Next(8, 17));
                }
                int direction = streets[row] < site.doorY ? -1 : 1;
                for (int y = site.doorY + direction; y != streets[row] + direction; y += direction) Road(ground, reserved, site.doorX, y);
                // Shared streets never cross plots or tutorial water, and join the arterial.
                for (int x = 3; x <= 60; x++) Road(ground, reserved, x, streets[row]);
            }
        }

        private static DistrictKind District(WorldGenerationSettings settings, SiteState site)
        {
            if (settings.cityDensity == 0) return DistrictKind.Wilderness;
            if (site.kind == SiteKind.Warehouse || site.kind == SiteKind.Workshop || site.kind == SiteKind.Substation) return DistrictKind.Industrial;
            if (site.kind == SiteKind.Farmhouse || site.kind == SiteKind.Greenhouse) return DistrictKind.Wilderness;
            bool inner = site.x >= 12 && site.x <= 46 && site.y >= 12 && site.y <= 46;
            if (settings.preset == WorldPreset.Wilderness) return inner && settings.cityDensity == 2 ? DistrictKind.Suburb : DistrictKind.Wilderness;
            if (inner || (settings.preset == WorldPreset.Urban && settings.cityDensity == 2)) return DistrictKind.Urban;
            return DistrictKind.Suburb;
        }

        private static void GenerateResources(WorldState state, char[] ground, bool[] reserved, HashSet<int> occupied)
        {
            if (state.generatorVersion >= 7)
            { GenerateBiomeResources(state, ground, reserved, occupied); return; }
            var random = new StableRandom(state.seed, 0x2e50ac01u);
            for (int y = 3; y <= 60; y++) for (int x = 3; x <= 60; x++)
            {
                int c = x + y * Size;
                if (state.nodes.Count >= WorldSimulator.MaxNodes || reserved[c] || occupied.Contains(c) || ground[c] == '2') continue;
                int roll = random.Next(0, 1000); NodeKind kind; int stock;
                if (roll < 12) { kind = NodeKind.Crate; stock = random.Next(6, 12); }
                else if (roll < 28) { kind = NodeKind.Ore; stock = random.Next(80, 180); }
                else if (roll < 40) { kind = NodeKind.CopperOre; stock = random.Next(80, 160); }
                else if (roll < 50) { kind = NodeKind.TinOre; stock = random.Next(60, 120); }
                else if (roll < 55) { kind = NodeKind.UraniumOre; stock = random.Next(40, 80); }
                else if (roll < 75) { kind = NodeKind.Scrap; stock = random.Next(10, 24); }
                else if (state.generatorVersion >= 6 && roll < 95) { kind = NodeKind.Stone; stock = random.Next(80, 160); }
                else if (state.generatorVersion >= 6 && roll < 110) { kind = NodeKind.Clay; stock = random.Next(60, 120); }
                else
                {
                    int trees = WorldGenerationCatalog.Biomes[state.biomes[c] - '0'].treePermille;
                    if (state.generation.preset == WorldPreset.Wilderness) trees = trees * 6 / 5;
                    else if (state.generation.preset == WorldPreset.Urban) trees = trees * 3 / 4;
                    if (roll >= 75 + trees) continue;
                    kind = NodeKind.Tree; stock = random.Next(12, 24);
                }
                AddNode(state, occupied, x, y, kind, stock);
            }
        }

        private static void GenerateBiomeResources(WorldState state, char[] ground, bool[] reserved, HashSet<int> occupied)
        {
            var blocked = new bool[ground.Length];
            foreach (int cell in occupied) blocked[cell] = true;
            // Real doors remain occupied but can be opened when checking access.
            foreach (var building in state.buildings)
                if (building.kind == BuildKind.Door) blocked[building.x + building.y * Size] = false;
            for (int y = 3; y <= 60; y++) for (int x = 3; x <= 60; x++)
            {
                int cell = x + y * Size;
                if (state.nodes.Count >= WorldSimulator.MaxNodes || reserved[cell] || occupied.Contains(cell) ||
                    ground[cell] == '2' || ground[cell] == '3' || ground[cell] == '4') continue;
                bool shoreline = ground[cell - 1] == '2' || ground[cell + 1] == '2' ||
                    ground[cell - Size] == '2' || ground[cell + Size] == '2';
                if (!ChunkTerrain.SampleResource(state.seed, x, y, (BiomeKind)(state.biomes[cell] - '0'),
                    state.generation, shoreline, out NodeKind kind, out int stock, state.generatorVersion)) continue;
                int[] parents = ResourcePaths(ground, blocked);
                if (parents[cell] < 0) continue;
                AddNode(state, occupied, x, y, kind, stock);
                blocked[cell] = true;
                // Preserve an actual gathering approach, as sparse chunks do.
                for (int path = parents[cell]; path >= 0; path = parents[path]) reserved[path] = true;
            }
        }

        private static int[] ResourcePaths(char[] ground, bool[] blocked)
        {
            var parents = new int[ground.Length]; var queue = new int[ground.Length];
            for (int i = 0; i < parents.Length; i++) parents[i] = -2;
            int head = 0, tail = 0, spawn = 32 + 35 * Size;
            parents[spawn] = -1; queue[tail++] = spawn;
            while (head < tail)
            {
                int cell = queue[head++], x = cell % Size, y = cell / Size;
                for (int direction = 0; direction < 4; direction++)
                {
                    int nx = x + (direction == 0 ? 1 : direction == 1 ? -1 : 0);
                    int ny = y + (direction == 2 ? 1 : direction == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= Size || ny >= Size) continue;
                    int next = nx + ny * Size;
                    if (parents[next] != -2 || blocked[next] || ground[next] == '2') continue;
                    parents[next] = cell; queue[tail++] = next;
                }
            }
            return parents;
        }

        public static WorldGenerationSettings CloneSettings(WorldGenerationSettings settings)
        {
            if (settings == null) return new WorldGenerationSettings();
            return new WorldGenerationSettings { worldName = settings.worldName, seed = settings.seed,
                seedText = settings.seedText, preset = settings.preset, cityDensity = settings.cityDensity, chunked = settings.chunked };
        }

        public static void Validate(WorldGenerationSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrWhiteSpace(settings.worldName) || settings.worldName.Length > 64)
                throw new ArgumentException("World name must contain 1-64 printable characters.", nameof(settings));
            foreach (char c in settings.worldName) if (char.IsControl(c)) throw new ArgumentException("World name contains a control character.", nameof(settings));
            if (settings.seedText == null || settings.seedText.Length > 128)
                throw new ArgumentException("Seed text must contain at most 128 characters.", nameof(settings));
            foreach (char c in settings.seedText) if (char.IsControl(c)) throw new ArgumentException("Seed text contains a control character.", nameof(settings));
            if ((int)settings.preset < 0 || (int)settings.preset > 2 || settings.cityDensity < 0 || settings.cityDensity > 2)
                throw new ArgumentException("Invalid world preset or city density.", nameof(settings));
        }

        // Metadata validation only, suitable after Main's save migration. Do not require
        // generated walls/crates to survive gameplay; their destruction is legal.
        public static void Validate(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if(ChunkStreaming.Enabled(state)) { _ = new WorldSimulator(state); return; }
            if (state.width != Size || state.height != Size || state.generatorVersion < 0 || state.generatorVersion > CurrentGeneratorVersion)
                throw new ArgumentException("Invalid generation version or map dimensions.", nameof(state));
            Validate(state.generation);
            if (state.generation.seed != state.seed ||
                (!string.IsNullOrWhiteSpace(state.generation.seedText) && WorldSeed.Resolve(state.generation.seedText) != state.seed))
                throw new ArgumentException("Generation seed does not match world seed.", nameof(state));
            if (state.biomes == null || state.biomes.Length != Size * Size) throw new ArgumentException("Invalid biome map length.", nameof(state));
            foreach (char c in state.biomes) if (c < '0' || c >= '0' + BiomeCount) throw new ArgumentException("Invalid biome kind.", nameof(state));
            if (state.sites == null || state.sites.Count > 32 || (state.generatorVersion == 0 && state.sites.Count != 0))
                throw new ArgumentException("Invalid site list. Legacy generation has no sites.", nameof(state));
            var ids = new HashSet<int>(); var cells = new HashSet<int>();
            foreach (var site in state.sites)
            {
                if (site == null || site.id <= 0 || site.id >= state.nextId || !ids.Add(site.id) ||
                    (int)site.kind < 0 || (int)site.kind >= 12 || (int)site.district < 0 || (int)site.district >= 4 ||
                    site.x < 3 || site.y < 3 || site.width < 5 || site.width > 9 || site.height < 5 || site.height > 9 ||
                    site.x > 61 - site.width || site.y > 61 - site.height)
                    throw new ArgumentException("Invalid site fields.", nameof(state));
                bool horizontal = (site.doorY == site.y || site.doorY == site.y + site.height - 1) && site.doorX > site.x && site.doorX < site.x + site.width - 1;
                bool vertical = (site.doorX == site.x || site.doorX == site.x + site.width - 1) && site.doorY > site.y && site.doorY < site.y + site.height - 1;
                if (!horizontal && !vertical) throw new ArgumentException("Site door must be a noncorner perimeter cell.", nameof(state));
                for (int y = site.y; y < site.y + site.height; y++) for (int x = site.x; x < site.x + site.width; x++)
                    if (Tutorial(x, y) || !cells.Add(x + y * Size)) throw new ArgumentException("Overlapping or tutorial-area site.", nameof(state));
            }
        }
    }
}
