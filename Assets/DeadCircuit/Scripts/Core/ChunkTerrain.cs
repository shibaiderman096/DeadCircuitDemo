using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public static partial class ChunkTerrain
    {
        private const int Size = ChunkStreaming.ChunkSize;
        public const int MaxNodesPerChunk = 75, MaxResourcesPerChunk = 40;

        private sealed class Region
        {
            public WorldState world;
            public WorldGenerationSettings settings;
            public int x, y, nextId;
            public char[] ground, biomes;
            public readonly List<NodeState> nodes = new List<NodeState>();
            public readonly List<BuildingState> buildings = new List<BuildingState>();
            public readonly List<SiteState> sites = new List<SiteState>();
            public readonly bool[] occupied = new bool[256], blocked = new bool[256], reserved = new bool[256];
        }

        // Stage an entire chunk before checking serialized/entity/ID budgets. A
        // rejected chunk leaves no terrain, entities, ID consumption or revision.
        public static bool Generate(WorldState world, int chunkX, int chunkY)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (!ChunkStreaming.Enabled(world) || world.chunks.records == null)
                throw new ArgumentException("World does not use sparse chunk generation.", nameof(world));
            if (!ChunkStreaming.InsideChunk(chunkX, chunkY))
                throw new ArgumentOutOfRangeException(nameof(chunkX));
            if (world.generatorVersion >= 5 && FloorWorld.IsReservedChunk(chunkX, chunkY)) return false;
            if (WorldTiles.TryChunk(world, chunkX, chunkY, out _)) return false;
            if (world.chunks.records.Count >= ChunkStreaming.MaxRecords || world.chunks.revision == int.MaxValue) return false;
            var ground = new char[Size * Size];
            var biomes = new char[ground.Length];
            int startX = chunkX * Size, startY = chunkY * Size;
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
            {
                if (world.generatorVersion >= 8)
                    SampleContinuous(world.seed, startX + x, startY + y, out ground[x + y * Size], out biomes[x + y * Size]);
                else Sample(world.seed, startX + x, startY + y, out ground[x + y * Size], out biomes[x + y * Size]);
            }
            var region = new Region { world = world, settings = world.generation ?? new WorldGenerationSettings(),
                x = startX, y = startY, ground = ground, biomes = biomes };
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
            {
                int cell = x + y * Size;
                if (Street(region, startX + x, startY + y)) { ground[cell] = '3'; region.reserved[cell] = true; }
            }
            // Existing entities contribute only local occupancy. They are never
            // moved, removed or renumbered to make room for a generated object.
            foreach (var node in world.nodes) MarkExisting(region, node.x, node.y, true);
            foreach (var b in world.buildings)
                for (int y = Math.Max(startY, b.y); y < Math.Min(startY + Size, b.y + DemoCatalog.Height(b)); y++)
                    for (int x = Math.Max(startX, b.x); x < Math.Min(startX + Size, b.x + DemoCatalog.Width(b)); x++)
                        MarkExisting(region, x, y, b.kind != BuildKind.Door);
            if (world.generatorVersion >= 4) AddBlueprintSite(region, chunkX, chunkY);
            else AddSite(region, chunkX, chunkY);
            AddResources(region);
            if (region.nodes.Count > MaxNodesPerChunk ||
                (long)world.nodes.Count + region.nodes.Count > WorldSimulator.MaxNodes ||
                (long)world.buildings.Count + region.buildings.Count > WorldSimulator.MaxBuildings ||
                (long)world.sites.Count + region.sites.Count > WorldSimulator.MaxSites ||
                world.nextId <= 0 || (long)world.nextId + region.nextId > int.MaxValue - 2) return false;
            foreach (var node in region.nodes) node.id += world.nextId;
            foreach (var building in region.buildings) building.id += world.nextId;
            foreach (var site in region.sites) site.id += world.nextId;
            world.nodes.AddRange(region.nodes);
            world.buildings.AddRange(region.buildings);
            world.sites.AddRange(region.sites);
            world.nextId += region.nextId;
            world.chunks.records.Add(new WorldChunkState { x = chunkX, y = chunkY, ground = new string(ground), biomes = new string(biomes) });
            world.chunks.revision++;
            return true;
        }

        private static void MarkExisting(Region r, int x, int y, bool blocked)
        {
            x -= r.x; y -= r.y;
            if (x < 0 || y < 0 || x >= Size || y >= Size) return;
            int cell = x + y * Size;
            r.occupied[cell] = true; r.blocked[cell] = blocked; r.reserved[cell] = true;
        }

        private static double Urbanity(int seed, int x, int y) =>
            Noise(seed, x, y, 96, 0xc171u) * .7 + Noise(seed, x, y, 48, 0xc172u) * .3;

        private static double CityThreshold(WorldGenerationSettings s) =>
            (s.preset == WorldPreset.Urban ? .32 : s.preset == WorldPreset.Wilderness ? .77 : .55) - s.cityDensity * .065;

        private static bool Street(Region r, int x, int y)
        {
            if (r.world.generatorVersion >= 5) return SettlementCatalog.Street(r.world, x, y);
            // Two world-coordinate arterials align with the old tutorial cross.
            if (x == 32 || x == 33 || y == 32 || y == 33) return true;
            int streetX = x - WorldTiles.FloorDiv(x, 32) * 32;
            int streetY = y - WorldTiles.FloorDiv(y, 32) * 32;
            return r.settings.cityDensity > 0 && (streetX < 2 || streetY < 2) &&
                Urbanity(r.world.seed, x, y) >= CityThreshold(r.settings);
        }

        private static void AddSite(Region r, int chunkX, int chunkY)
        {
            int seed = r.world.seed;
            double city = Urbanity(seed, r.x + 8, r.y + 8);
            double chance = (r.settings.preset == WorldPreset.Urban ? .52 : r.settings.preset == WorldPreset.Wilderness ? .06 : .22) +
                r.settings.cityDensity * (r.settings.preset == WorldPreset.Wilderness ? .11 : .2);
            if (Unit(seed, chunkX, chunkY, 0x517eu) >= chance) return;
            bool developed = r.settings.cityDensity > 0 && city >= CityThreshold(r.settings);
            SiteKind kind = developed ? (SiteKind)(Hash(seed, chunkX, chunkY, 0x517fu) % 12) :
                Unit(seed, chunkX, chunkY, 0xfa21u) < .8 ? SiteKind.Farmhouse : SiteKind.Greenhouse;
            var definition = WorldGenerationCatalog.Sites[(int)kind];
            int width = definition.width, height = definition.height;
            var positions = new List<int>();
            for (int y = 2; y <= 14 - height; y++) for (int x = 2; x <= 14 - width; x++) positions.Add(x + y * Size);
            SortCells(positions, r, 0x5170u);
            foreach (int position in positions)
            {
                int sx = position % Size, sy = position / Size;
                bool clear = true;
                for (int y = sy - 1; y <= sy + height; y++) for (int x = sx - 1; x <= sx + width; x++)
                {
                    int cell = x + y * Size;
                    bool interior = x >= sx && x < sx + width && y >= sy && y < sy + height;
                    if (r.ground[cell] == '2' || r.occupied[cell] || (interior && r.reserved[cell])) clear = false;
                }
                if (!clear) continue;
                bool south = (Hash(seed, r.x + sx, r.y + sy, 0xd002u) & 1) == 0;
                int doorX = sx + width / 2, doorY = south ? sy + height - 1 : sy;
                int outside = doorX + (doorY + (south ? 1 : -1)) * Size;
                var proposed = (bool[])r.blocked.Clone();
                for (int y = sy; y < sy + height; y++) for (int x = sx; x < sx + width; x++)
                    if (Perimeter(x, y, sx, sy, width, height) && !(x == doorX && y == doorY)) proposed[x + y * Size] = true;
                for (int i = 0; i < definition.containers; i++) proposed[Container(sx, sy, width, height, i)] = true;
                int[] parents = Paths(r.ground, proposed);
                if (parents[outside] == -2 || parents[doorX + doorY * Size] == -2) continue;
                var site = new SiteState { id = r.nextId++, x = r.x + sx, y = r.y + sy, width = width, height = height,
                    doorX = r.x + doorX, doorY = r.y + doorY, kind = kind,
                    district = !developed || kind == SiteKind.Farmhouse || kind == SiteKind.Greenhouse ? DistrictKind.Wilderness :
                        kind == SiteKind.Warehouse || kind == SiteKind.Workshop || kind == SiteKind.Substation ? DistrictKind.Industrial :
                        city > .6 ? DistrictKind.Urban : DistrictKind.Suburb };
                r.sites.Add(site);
                for (int y = sy - 1; y <= sy + height; y++) for (int x = sx - 1; x <= sx + width; x++) r.reserved[x + y * Size] = true;
                for (int y = sy; y < sy + height; y++) for (int x = sx; x < sx + width; x++)
                {
                    r.ground[x + y * Size] = '4';
                    if (Perimeter(x, y, sx, sy, width, height) && !(x == doorX && y == doorY)) AddNode(r, x + y * Size, NodeKind.Wall, 14);
                }
                r.buildings.Add(new BuildingState { id = r.nextId++, x = site.doorX, y = site.doorY,
                    kind = BuildKind.Door, footprintVersion = 1, hp = WorldSimulator.BuildingMaxHealth(BuildKind.Door) });
                r.occupied[doorX + doorY * Size] = true;
                for (int i = 0; i < definition.containers; i++)
                    AddNode(r, Container(sx, sy, width, height, i), NodeKind.Crate, 8 + (int)(Hash(seed, site.x, site.y, (uint)(0xc2a0 + i)) % 9));
                // A reserved dry approach joins a reachable boundary without
                // clearing any pre-existing object or crossing another chunk.
                for (int cell = outside; cell >= 0; cell = parents[cell])
                { r.ground[cell] = '3'; r.reserved[cell] = true; }
                return;
            }
        }

        private static bool Perimeter(int x, int y, int sx, int sy, int width, int height) =>
            x == sx || y == sy || x == sx + width - 1 || y == sy + height - 1;

        private static int Container(int x, int y, int width, int height, int index) =>
            (index == 1 ? x + width - 2 : x + 1) + (index == 2 ? y + height - 2 : y + 1) * Size;

        private static void AddResources(Region r)
        {
            var candidates = new List<int>();
            for (int y = 2; y < 14; y++) for (int x = 2; x < 14; x++) candidates.Add(x + y * Size);
            SortCells(candidates, r, 0x2e50u);
            int count = 0;
            foreach (int cell in candidates)
            {
                if (count == MaxResourcesPerChunk || r.nodes.Count >= MaxNodesPerChunk) break;
                if (r.reserved[cell] || r.occupied[cell] || r.ground[cell] == '2') continue;
                NodeKind kind; int stock;
                if (!Resource(r, cell, out kind, out stock)) continue;
                int[] parents = Paths(r.ground, r.blocked);
                if (parents[cell] < 0) continue;
                AddNode(r, cell, kind, stock); count++;
                // Keep this resource's actual path empty. Later resources can
                // never seal it off, even in dense forests or narrow coastlines.
                for (int path = parents[cell]; path >= 0; path = parents[path]) r.reserved[path] = true;
            }
        }

        private static bool Resource(Region r, int cell, out NodeKind kind, out int stock)
        {
            if (r.world.generatorVersion >= 7)
            {
                bool shoreline = r.ground[cell - 1] == '2' || r.ground[cell + 1] == '2' ||
                    r.ground[cell - Size] == '2' || r.ground[cell + Size] == '2';
                return SampleResource(r.world.seed, r.x + cell % Size, r.y + cell / Size,
                    (BiomeKind)(r.biomes[cell] - '0'), r.settings, shoreline, out kind, out stock, r.world.generatorVersion);
            }
            int x = r.x + cell % Size, y = r.y + cell / Size, seed = r.world.seed;
            var biome = (BiomeKind)(r.biomes[cell] - '0');
            double roll = Unit(seed, x, y, 0x2e51u), vein = Noise(seed, x, y, 8, 0x0ae1u);
            kind = NodeKind.Tree; stock = 12 + (int)(Hash(seed, x, y, 0x570cu) % 13);
            if (r.world.generatorVersion >= 6)
            {
                double primitive = Unit(seed, x, y, 0x6a51u);
                if (primitive < .05 || (biome == BiomeKind.Wetland && primitive < .09))
                {
                    kind = primitive < .025 ? NodeKind.Stone : NodeKind.Clay;
                    stock = 60 + (int)(Hash(seed, x, y, 0x6a52u) % 101);
                    return true;
                }
            }
            if (vein > .63 && roll < .5)
            {
                uint metal = Hash(seed, WorldTiles.FloorDiv(x, 12), WorldTiles.FloorDiv(y, 12), 0x0ae2u) % 100;
                kind = metal < 40 ? NodeKind.Ore : metal < 67 ? NodeKind.CopperOre : metal < 91 ? NodeKind.TinOre : NodeKind.UraniumOre;
                if (biome == BiomeKind.Wetland) kind = metal < 45 ? NodeKind.TinOre : NodeKind.CopperOre;
                if (biome == BiomeKind.Desert && metal >= 78) kind = NodeKind.UraniumOre;
                stock = (kind == NodeKind.UraniumOre ? 40 : 80) + (int)(Hash(seed, x, y, 0x570du) % 81);
                return true;
            }
            double forest = Noise(seed, x, y, 9, 0x7ee1u);
            double trees = WorldGenerationCatalog.Biomes[(int)biome].treePermille / 1000.0;
            if (r.settings.preset == WorldPreset.Wilderness) trees *= 1.2;
            if (r.settings.preset == WorldPreset.Urban) trees *= .75;
            if (forest > .36 && roll < trees * (.35 + forest)) return true;
            if (Unit(seed, x, y, 0x5c2au) < (Urbanity(seed, x, y) > CityThreshold(r.settings) ? .04 : .008))
            { kind = NodeKind.Scrap; stock = 10 + (int)(Hash(seed, x, y, 0x570eu) % 15); return true; }
            return false;
        }

        private static void AddNode(Region r, int cell, NodeKind kind, int stock)
        {
            r.nodes.Add(new NodeState { id = r.nextId++, x = r.x + cell % Size, y = r.y + cell / Size,
                kind = kind, stock = stock, hp = kind == NodeKind.Wall ? 160f : 35f });
            r.occupied[cell] = true; r.blocked[cell] = true;
        }

        private static void SortCells(List<int> cells, Region r, uint channel)
        {
            cells.Sort((a, b) =>
            {
                int order = Hash(r.world.seed, r.x + a % Size, r.y + a / Size, channel).CompareTo(
                    Hash(r.world.seed, r.x + b % Size, r.y + b / Size, channel));
                return order != 0 ? order : a.CompareTo(b);
            });
        }

        // Parents -2 = blocked/unreachable, -1 = a dry boundary cell. Only
        // local arrays are traversed; ungenerated neighbors are never inspected.
        private static int[] Paths(char[] ground, bool[] blocked)
        {
            var parents = new int[256]; var queue = new int[256];
            for (int i = 0; i < parents.Length; i++) parents[i] = -2;
            int head = 0, tail = 0;
            for (int cell = 0; cell < 256; cell++)
                if ((cell % Size == 0 || cell % Size == 15 || cell / Size == 0 || cell / Size == 15) && ground[cell] != '2' && !blocked[cell])
                { parents[cell] = -1; queue[tail++] = cell; }
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

        // Fields are evaluated in absolute world coordinates, including octave
        // lattice corners. No neighbor map reads, chunk-local RNG or edge blending.
        private static void Sample(int seed, int x, int y, out char ground, out char biome)
        {
            double elevation = Octaves(seed, x, y, 64, 0x1049u) * .7 + Noise(seed, x, y, 128, 0x82afu) * .3;
            double moisture = .5 + (Octaves(seed, x, y, 64, 0x6b21u) - .5) * 1.8;
            double temperature = .5 + (Octaves(seed, x, y, 64, 0xa70du) - .5) * 2.2;
            BiomeKind kind = temperature < .28 ? BiomeKind.Tundra :
                temperature < .46 && moisture > .38 ? BiomeKind.ConiferousForest :
                temperature > .54 && moisture < .37 ? BiomeKind.Desert :
                moisture > .63 && elevation < .56 ? BiomeKind.Wetland :
                moisture > .5 ? BiomeKind.DeciduousForest : BiomeKind.Meadow;
            biome = (char)('0' + (int)kind);
            double river = Math.Abs(Octaves(seed, x, y, 128, 0x215cu) - .5);
            bool water = elevation < .365 || (river < .018 + Math.Max(0, moisture - .4) * .018 && elevation < .68);
            double soil = Noise(seed, x, y, 8, 0x5011u) * .65 + Unit(seed, x, y, 0x5012u) * .35;
            ground = water ? '2' : soil < WorldGenerationCatalog.Biomes[(int)kind].dirtPercent / 100.0 ? '1' : '0';
        }

        private static double Octaves(int seed, int x, int y, int scale, uint channel)
        {
            double sum = 0, weight = 1, total = 0;
            for (int i = 0; i < 4; i++, scale /= 2, weight *= .5)
            { sum += Noise(seed, x, y, scale, channel + (uint)i * 173u) * weight; total += weight; }
            return sum / total;
        }

        private static double Noise(int seed, int x, int y, int scale, uint channel)
        {
            int ix = (int)Math.Floor((double)x / scale), iy = (int)Math.Floor((double)y / scale);
            double fx = (double)x / scale - ix, fy = (double)y / scale - iy;
            fx = fx * fx * fx * (fx * (fx * 6 - 15) + 10);
            fy = fy * fy * fy * (fy * (fy * 6 - 15) + 10);
            double a = Unit(seed, ix, iy, channel), b = Unit(seed, ix + 1, iy, channel);
            double c = Unit(seed, ix, iy + 1, channel), d = Unit(seed, ix + 1, iy + 1, channel);
            return (a + (b - a) * fx) * (1 - fy) + (c + (d - c) * fx) * fy;
        }

        private static double Unit(int seed, int x, int y, uint channel) => Hash(seed, x, y, channel) / 4294967296.0;

        private static uint Hash(int seed, int x, int y, uint channel)
        {
            unchecked
            {
                uint value = (uint)seed ^ channel ^ (uint)x * 0x9e3779b9u ^ (uint)y * 0x85ebca6bu;
                value ^= value >> 16; value *= 0x7feb352du;
                value ^= value >> 15; value *= 0x846ca68bu;
                return value ^ (value >> 16);
            }
        }
    }
}
