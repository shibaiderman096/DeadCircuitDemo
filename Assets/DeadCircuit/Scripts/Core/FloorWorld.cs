using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    // Floors are ordinary saved sites/entities in isolated chunks. Connections are
    // derived from (root id, level, authored plan), never serialized teleport links.
    public static class FloorWorld
    {
        private const int ReservedMinChunkX = -4096, ReservedMaxChunkX = -2048, ReservedMinChunkY = -4096, ReservedMaxChunkY = 64;
        private const int SlotStart = -4080, SlotColumns = 127, SlotStride = 16, SlotCount = SlotColumns * 259;
        private static readonly int[] Dx = { 1, 0, -1, 0 }, Dy = { 0, 1, 0, -1 };
        private static readonly int[] Lowest = { -1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-2,-2,-2,-1,-4,-1,-3,-1,-1,-1,-1,-1,-1 };
        private static readonly int[] Highest = { 1,1,1,1,1,1,0,2,2,3,0,0,1,1,1,1,2,1,3,2,1,0,2,0,1,1,1,1,0,1,2,0 };

        // North-to-south ASCII. No spaces inside a floor bbox: all cells are '4'.
        // Perimeters are real walls; '+' denotes an internal, physical door only.
        private static readonly string[][] Plans =
        {
            new[] { "#########", "#b.K#b.F#", "#...+...#", "#t..#t..#", "##+###+##", "#.<...>.#", "#h..f...#", "#########" }, // homes
            new[] { "##########", "#F.s#T..K#", "#...+....#", "#ss.#.ss.#", "##+####+##", "#.<....>.#", "#n...h...#", "##########" }, // retail
            new[] { "##########", "#B.m..m.T#", "#........#", "#ss.##.ss#", "#...+....#", "#.<....>.#", "#w..E..t.#", "##########" }, // workshops
            new[] { "###########", "#d.M#d..f.#", "#...+.....#", "#d..#d..M.#", "##+####+###", "#.<.....>.#", "#n..h..h..#", "###########" }, // wards
            new[] { "###########", "#t.A#b..T.#", "#...+.....#", "#w..#b..b.#", "##+####+###", "#.<.....>.#", "#n..l..h..#", "###########" }, // police
            new[] { "###########", "#t.t#t.t.K#", "#h.h+h.h..#", "#l..#l....#", "##+####+###", "#.<.....>.#", "#h..l..h..#", "###########" }, // classrooms/library
            new[] { "############", "#b.F#b.K#M.#", "#...+...+..#", "#t..#t..#f.#", "##+###+##+##", "#.<......>.#", "#h..o...l..#", "############" }, // apartments/motel
            new[] { "###########", "#e.E#m..M.#", "#...+.....#", "#t..#e..e.#", "##+####+###", "#.<.....>.#", "#l..K..t..#", "###########" }, // laboratory
            new[] { "##########", "#b.A#b..M#", "#...+....#", "#w..#l..K#", "##+####+##", "#.<....>.#", "#F..t..E.#", "##########" }, // bunker
            new[] { "###########", "#T.m#B..E.#", "#...+.....#", "#r..#r..r.#", "##+####+###", "#.<.....>.#", "#w..v..T..#", "###########" }, // mine
            new[] { "##########", "#S.g#F..K#", "#...+....#", "#a..#g..g#", "##+####+##", "#.<....>.#", "#o..t....#", "##########" }, // rural
            new[] { "###########", "#E.e#m..T.#", "#...+.....#", "#p..#v..u.#", "##+####+###", "#.<.....>.#", "#w..r..e..#", "###########" }, // utilities
            new[] { "#########", "#F.K#B.T#", "#...+...#", "#ss.#ss.#", "##+###+##", "#.<...>.#", "#a..w...#", "#########" }, // basement storage
            new[] { "#########", "#...<...#", "#b.K.b.M#", "###++####", "#...#...#", "#t.h+o.F#", "#..>....#", "#########" }, // split apartments
            new[] { "##########", "#.<......#", "#b...h..F#", "#..####..#", "#..+..+..#", "#M.#b.#.K#", "#..#..#.>#", "##########" }, // penthouse
            new[] { "###########", "#.<...#..M#", "#..d..+d..#", "#..M..#...#", "####+######", "#f........#", "#..>...E..#", "###########" }, // surgery
            new[] { "##########", "#.<.M..d.#", "#...##+###", "#...#....#", "#d..+..d.#", "#...#..K.#", "#..>#....#", "##########" }, // isolation ward
            new[] { "##########", "#...<....#", "#E..m..T.#", "#..####..#", "#..+..+..#", "#e.#K.#.M#", "#>.#..#..#", "##########" }, // instrumentation
            new[] { "###########", "#...<..u..#", "#E..####..#", "#...+..+..#", "#...#E.#..#", "#r..#..#T.#", "#...>.....#", "###########" }, // containment
            new[] { "#########", "#.<...>.#", "#b.A.b..#", "###+###+#", "#...#...#", "#F..+..M#", "#...#..l#", "#########" }, // bunker quarters
            new[] { "#########", "#.<..A..#", "#..###+##", "#..#..b.#", "#..+....#", "#T.#..l.#", "#..#..>.#", "#########" }, // bunker control
            new[] { "###########", "#.<..E....#", "#...####+##", "#...#.....#", "#T..+..K..#", "#...#..E..#", "#...#...>.#", "###########" }, // bunker vault
            new[] { "#########", "#.<..r..#", "#..#+####", "#..#....#", "#B.+.T..#", "#..#....#", "#...r.>.#", "#########" }, // mine stores
            new[] { "##########", "#.<..m..E#", "#..####..#", "#..+..+..#", "#T.#B.#..#", "#..#..#.>#", "#...r....#", "##########" } // mine machine room
        };
        private static readonly Dictionary<int, SiteBlueprint> FloorPlans = CreatePlans();

        public readonly struct Stair
        {
            public readonly int rootSiteId, siteId, level, targetLevel, x, y;
            public char Glyph => targetLevel > level ? '<' : '>';
            internal Stair(SiteState root, SiteState site, int x, int y, int delta)
            { rootSiteId = root.id; siteId = site.id; level = site.level; targetLevel = level + delta; this.x = x; this.y = y; }
        }

        public static bool Enabled(WorldState world) => ChunkStreaming.Enabled(world) && world.generatorVersion >= 5;
        public static bool IsReservedChunk(int cx, int cy) => cx >= ReservedMinChunkX && cx < ReservedMaxChunkX &&
            cy >= ReservedMinChunkY && cy < ReservedMaxChunkY;
        public static int MinLevel(SiteKind kind) => (uint)(int)kind < Lowest.Length ? Lowest[(int)kind] : 0;
        public static int MaxLevel(SiteKind kind) => (uint)(int)kind < Highest.Length ? Highest[(int)kind] : 0;
        public static string LevelName(int level) => level == 0 ? "地面" : level > 0 ? (level + 1L) + "楼" : "地下" + -(long)level + "层";

        public static SiteBlueprint GetPlan(SiteState site)
        {
            if (site == null) return null;
            if (site.level == 0) return SiteBlueprintCatalog.Get(site.kind);
            return FloorPlans.TryGetValue((int)site.kind * 16 + site.level + 4, out var plan) &&
                site.level >= MinLevel(site.kind) && site.level <= MaxLevel(site.kind) ? plan : null;
        }

        // A whole tagged chunk is an interior domain, including its impassable void.
        public static bool IsInterior(WorldState world, int x, int y) => Enabled(world) &&
            WorldTiles.TryChunk(world, WorldTiles.FloorDiv(x, 16), WorldTiles.FloorDiv(y, 16), out var chunk) && chunk.interiorSiteId > 0;

        public static char StairGlyph(SiteState site, int x, int y)
        {
            if (!ValidPlan(site) || !Contains(site, x, y)) return '\0';
            if (site.level != 0)
            {
                char cell = Cell(site, x, y);
                return cell == '<' && site.level < MaxLevel(site.kind) || cell == '>' && site.level > MinLevel(site.kind) ? cell : '\0';
            }
            if (Cell(site, x, y) != '.') return '\0';
            GroundStairs(site, out int ux, out int uy, out int dx, out int dy);
            if (MaxLevel(site.kind) > 0 && x == ux && y == uy) return '<';
            if (MinLevel(site.kind) < 0 && x == dx && y == dy) return '>';
            return '\0';
        }

        public static char StairGlyph(WorldState world, int x, int y) =>
            Enabled(world) ? StairGlyph(SiteAt(world, x, y), x, y) : '\0';
        public static bool IsStairCell(WorldState world, int x, int y) => StairGlyph(world, x, y) != '\0';
        // Arrival is exactly on the reciprocal stair. No speculative neighbor ban.
        public static bool IsProtectedCell(WorldState world, int x, int y) => IsStairCell(world, x, y);

        public static string Location(WorldState world, int x, int y)
        {
            if (!Enabled(world)) return "";
            var site = SiteAt(world, x, y);
            if (site == null || !TryRoot(world, site, out var root)) return "";
            if (site.level == 0 && (Cell(site, x, y) == ' ' || Cell(site, x, y) == ',')) return "";
            return WorldGenerationCatalog.Sites[(int)root.kind].name + "/" + LevelName(site.level) + "/(" + (x - site.x) + "," + (y - site.y) + ")";
        }

        public static bool TryStair(WorldState world, int x, int y, out Stair stair)
        {
            stair = default;
            if (!Enabled(world)) return false;
            var site = SiteAt(world, x, y);
            char glyph = StairGlyph(site, x, y);
            if (glyph == '\0' || !TryRoot(world, site, out var root)) return false;
            stair = new Stair(root, site, x, y, glyph == '<' ? 1 : -1);
            return true;
        }

        // The host command owns source distance/LOS/cooldown checks. This method
        // owns destination validation and stages ALL persistent changes off-world.
        internal static bool TryTravel(WorldState world, PlayerState player, int x, int y, out string notice)
        {
            notice = "";
            if (!TryStair(world, x, y, out var stair)) return false;
            notice = "楼梯暂时无法通行。";
            if (player == null || !player.connected || !(player.hp > 0) || !Finite(player.x) || !Finite(player.y) ||
                world.players == null || !world.players.Contains(player) || world.nodes == null || world.buildings == null ||
                !ClearCell(world, x, y)) return false;
            var source = SiteAt(world, x, y);
            if (!TryRoot(world, source, out var root)) return false;
            SiteState target = stair.targetLevel == 0 ? root : null;
            if (stair.targetLevel != 0) foreach (var site in world.sites)
            {
                if (site.parentSiteId != root.id || site.level != stair.targetLevel) continue;
                if (target != null || !TryRoot(world, site, out var owner) || owner != root) return false;
                target = site;
            }
            Staged staged = null;
            if (target == null)
            {
                if (!TryStage(world, root, stair.targetLevel, out staged))
                { notice = "楼层空间、实体或存档预算不足，未生成楼层。"; return false; }
                target = staged.site;
            }
            char reciprocal = stair.Glyph == '<' ? '>' : '<';
            if (!FindStair(target, reciprocal, out int tx, out int ty)) return false;
            if (!ActorsClear(world, player, tx, ty) || (staged == null ? !ClearCell(world, tx, ty) : !ClearStaged(staged, tx, ty)))
            { notice = "楼梯落点被人物、生物或设备占用。"; return false; }
            if (staged != null)
            {
                world.sites.Add(staged.site);
                world.nodes.AddRange(staged.nodes); world.buildings.AddRange(staged.buildings);
                world.chunks.records.Add(staged.chunk);
                world.nextId = staged.nextId; world.chunks.revision++;
            }
            player.x = tx; player.y = ty; player.aiming = player.moving = false;
            player.aimX = 1f; player.aimY = 0f;
            notice = "已到达" + WorldGenerationCatalog.Sites[(int)root.kind].name + " · " + LevelName(target.level);
            return true;
        }

        private sealed class Staged
        {
            internal SiteState site; internal WorldChunkState chunk; internal int nextId;
            internal readonly List<NodeState> nodes = new List<NodeState>();
            internal readonly List<BuildingState> buildings = new List<BuildingState>();
        }

        private static bool TryStage(WorldState world, SiteState root, int level, out Staged staged)
        {
            staged = null;
            if (world.chunks.records == null || world.chunks.records.Count >= ChunkStreaming.MaxRecords ||
                world.sites.Count >= WorldSimulator.MaxSites || world.chunks.revision == int.MaxValue || world.nextId <= 0) return false;
            var occupied = new HashSet<long>();
            int ordinal = 0;
            foreach (var chunk in world.chunks.records)
            {
                if (chunk == null) return false;
                occupied.Add(WorldTiles.Key(chunk.x, chunk.y));
                if (chunk.interiorSiteId > 0 && SlotOrdinal(chunk.x, chunk.y, out int previous)) ordinal = Math.Max(ordinal, previous + 1);
            }
            int cx, cy;
            while (true)
            {
                if (ordinal >= SlotCount) return false;
                cx = SlotStart + ordinal % SlotColumns * SlotStride;
                cy = SlotStart + ordinal / SlotColumns * SlotStride;
                if (!IsReservedChunk(cx, cy) || !ChunkStreaming.InsideChunk(cx, cy)) return false;
                ordinal++;
                if (!occupied.Contains(WorldTiles.Key(cx, cy)) && EmptyChunk(world, cx, cy)) break;
            }
            var site = new SiteState { id = world.nextId, parentSiteId = root.id, level = level, kind = root.kind,
                district = root.district, settlement = root.settlement, rotation = root.rotation, layoutVersion = 1, x = cx * 16 + 2, y = cy * 16 + 2 };
            var plan = GetPlan(site); if (plan == null) return false;
            site.width = SiteBlueprintCatalog.Width(plan, site.rotation); site.height = SiteBlueprintCatalog.Height(plan, site.rotation);
            // The always-present stair toward ground is the metadata doorway.
            if (!FindStair(site, level > 0 ? '>' : '<', out site.doorX, out site.doorY)) return false;
            char biome = WorldTiles.Biome(world, root.doorX, root.doorY);
            if (biome < '0' || biome > '5') return false;
            var ground = new char[256]; var biomes = new char[256];
            for (int i = 0; i < ground.Length; i++) ground[i] = biomes[i] = '?';
            var result = new Staged { site = site };
            int idCount = 1;
            for (int ly = 0; ly < site.height; ly++) for (int lx = 0; lx < site.width; lx++)
            {
                int wx = site.x + lx, wy = site.y + ly, index = wx - cx * 16 + (wy - cy * 16) * 16;
                char cell = Cell(site, wx, wy);
                ground[index] = '4'; biomes[index] = biome;
                if (cell == '#' || SiteBlueprintCatalog.Crates.IndexOf(cell) >= 0)
                    result.nodes.Add(new NodeState { id = idCount++, x = wx, y = wy,
                        kind = cell == '#' ? NodeKind.Wall : NodeKind.Crate, stock = cell == '#' ? 14 : 8, hp = cell == '#' ? 160f : 35f });
                else if (cell == '+') result.buildings.Add(new BuildingState { id = idCount++, x = wx, y = wy,
                    kind = BuildKind.Door, rotation = (root.rotation & 1) ^ 1, footprintVersion = 1, hp = WorldSimulator.BuildingMaxHealth(BuildKind.Door) });
            }
            if ((long)world.nodes.Count + result.nodes.Count > WorldSimulator.MaxNodes ||
                (long)world.buildings.Count + result.buildings.Count > WorldSimulator.MaxBuildings ||
                (long)world.nextId + idCount > int.MaxValue - 2) return false;
            foreach (var node in result.nodes) node.id += world.nextId;
            foreach (var building in result.buildings) building.id += world.nextId;
            result.nextId = world.nextId + idCount;
            result.chunk = new WorldChunkState { x = cx, y = cy, interiorSiteId = site.id, ground = new string(ground), biomes = new string(biomes) };
            staged = result; return true;
        }

        internal static bool SlotOrdinal(int cx, int cy, out int ordinal)
        {
            ordinal = 0; int col = cx - SlotStart, row = cy - SlotStart;
            if (!IsReservedChunk(cx, cy) || col < 0 || col % SlotStride != 0 || col / SlotStride >= SlotColumns || row < 0 || row % SlotStride != 0) return false;
            int candidate = row / SlotStride * SlotColumns + col / SlotStride;
            if (candidate >= SlotCount) return false;
            ordinal = candidate;
            return true;
        }

        private static bool EmptyChunk(WorldState world, int cx, int cy)
        {
            int x = cx * 16, y = cy * 16;
            foreach (var site in world.sites) if ((long)site.x < x + 16 && (long)site.x + site.width > x &&
                (long)site.y < y + 16 && (long)site.y + site.height > y) return false;
            foreach (var node in world.nodes) if (node.x >= x && node.x < x + 16 && node.y >= y && node.y < y + 16) return false;
            foreach (var b in world.buildings) if ((long)b.x < x + 16 && (long)b.x + DemoCatalog.Width(b) > x &&
                (long)b.y < y + 16 && (long)b.y + DemoCatalog.Height(b) > y) return false;
            return true;
        }

        private static bool ClearCell(WorldState world, int x, int y)
        {
            if (!WorldTiles.Inside(world, x, y)) return false;
            char ground = WorldTiles.Ground(world, x, y);
            if (ground == '?' || ground == '2') return false;
            foreach (var n in world.nodes) if (n.x == x && n.y == y) return false;
            foreach (var b in world.buildings) if (DemoCatalog.Contains(b, x, y)) return false;
            return true;
        }
        private static bool ClearStaged(Staged staged, int x, int y)
        {
            char cell = Cell(staged.site, x, y);
            if (cell != '<' && cell != '>') return false;
            foreach (var n in staged.nodes) if (n.x == x && n.y == y) return false;
            foreach (var b in staged.buildings) if (DemoCatalog.Contains(b, x, y)) return false;
            return true;
        }
        private static bool ActorsClear(WorldState world, PlayerState player, int x, int y)
        {
            foreach (var other in world.players) if (other != player && other.hp > 0 && Near(other.x, other.y, x, y)) return false;
            if (world.zombies != null) foreach (var z in world.zombies) if (z.hp > 0 && Near(z.x, z.y, x, y)) return false;
            if (world.survival != null)
            {
                if (world.survival.creatures != null) foreach (var c in world.survival.creatures) if (c.hp > 0 && Near(c.x, c.y, x, y)) return false;
                if (world.survival.plots != null) foreach (var p in world.survival.plots) if (p.x == x && p.y == y) return false;
            }
            return true;
        }
        private static bool Near(float x, float y, int tx, int ty) => (x - tx) * (x - tx) + (y - ty) * (y - ty) < 1f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Contains(SiteState site, int x, int y) => site != null && x >= site.x && y >= site.y && (long)x < (long)site.x + site.width && (long)y < (long)site.y + site.height;
        private static SiteState SiteAt(WorldState world, int x, int y)
        {
            if (world.sites != null) foreach (var site in world.sites) if (Contains(site, x, y)) return site;
            return null;
        }
        private static bool ValidPlan(SiteState site)
        {
            if (site == null || site.layoutVersion != 1 || site.rotation < 0 || site.rotation > 3 ||
                site.level < MinLevel(site.kind) || site.level > MaxLevel(site.kind)) return false;
            var plan = GetPlan(site);
            return plan != null && site.width == SiteBlueprintCatalog.Width(plan, site.rotation) && site.height == SiteBlueprintCatalog.Height(plan, site.rotation);
        }
        private static bool TryRoot(WorldState world, SiteState site, out SiteState root)
        {
            root = null; if (!ValidPlan(site)) return false;
            if (site.level == 0) { if (site.parentSiteId != 0) return false; root = site; }
            else foreach (var candidate in world.sites) if (candidate.id == site.parentSiteId) { root = candidate; break; }
            if (root == null || root.id <= 0 || root.parentSiteId != 0 || root.level != 0 || !ValidPlan(root) ||
                site.kind != root.kind || site.rotation != root.rotation || site.district != root.district || site.settlement != root.settlement) return false;
            if (site.level == 0) return true;
            return WorldTiles.TryChunk(world, WorldTiles.FloorDiv(site.x, 16), WorldTiles.FloorDiv(site.y, 16), out var chunk) &&
                chunk.interiorSiteId == site.id && IsReservedChunk(chunk.x, chunk.y);
        }
        private static char Cell(SiteState site, int x, int y) => SiteBlueprintCatalog.At(GetPlan(site), site.rotation, x - site.x, y - site.y);
        private static bool FindStair(SiteState site, char glyph, out int x, out int y)
        {
            for (int ly = 0; ly < site.height; ly++) for (int lx = 0; lx < site.width; lx++)
                if (StairGlyph(site, site.x + lx, site.y + ly) == glyph) { x = site.x + lx; y = site.y + ly; return true; }
            x = y = 0; return false;
        }
        private static bool HasApproach(SiteState site, int x, int y, int exceptX, int exceptY)
        {
            for (int i = 0; i < 4; i++)
            {
                int nx = x + Dx[i], ny = y + Dy[i];
                if (Contains(site, nx, ny) && (nx != exceptX || ny != exceptY) && Cell(site, nx, ny) == '.') return true;
            }
            return false;
        }
        private static void GroundStairs(SiteState site, out int ux, out int uy, out int dx, out int dy)
        {
            ux = uy = dx = dy = int.MinValue;
            if (MaxLevel(site.kind) > 0)
                for (int cell = 0; cell < site.width * site.height; cell++)
                {
                    int x = site.x + cell % site.width, y = site.y + cell / site.width;
                    if (Cell(site, x, y) != '.' || !HasApproach(site, x, y, int.MinValue, int.MinValue)) continue;
                    ux = x; uy = y; break;
                }
            if (MinLevel(site.kind) >= 0) return;
            for (int cell = site.width * site.height - 1; cell >= 0; cell--)
            {
                int x = site.x + cell % site.width, y = site.y + cell / site.width;
                if (x == ux && y == uy || Cell(site, x, y) != '.' || !HasApproach(site, x, y, ux, uy) ||
                    ux != int.MinValue && !HasApproach(site, ux, uy, x, y)) continue;
                dx = x; dy = y; break;
            }
        }

        private static Dictionary<int, SiteBlueprint> CreatePlans()
        {
            var result = new Dictionary<int, SiteBlueprint>();
            for (int kind = 0; kind < Lowest.Length; kind++)
            {
                var ground = SiteBlueprintCatalog.Get((SiteKind)kind);
                for (int level = Lowest[kind]; level <= Highest[kind]; level++)
                {
                    if (level == 0) continue;
                    int variant = Archetype((SiteKind)kind, level);
                    var rows = (string[])Plans[variant].Clone();
                    for (int i = 0; i < rows.Length; i++)
                    {
                        if (level == Highest[kind]) rows[i] = rows[i].Replace('<', '.');
                        if (level == Lowest[kind]) rows[i] = rows[i].Replace('>', '.');
                    }
                    result.Add(kind * 16 + level + 4, new SiteBlueprint((SiteKind)kind, ground.category,
                        ground.description + " " + LevelName(level), ground.floorArt, ground.biomeMask, rows));
                }
            }
            return result;
        }
        private static int Archetype(SiteKind kind, int level)
        {
            switch (kind)
            {
                case SiteKind.Bunker: return level == -1 ? 8 : level == -2 ? 19 : level == -3 ? 20 : 21;
                case SiteKind.MineOffice: return level == 1 ? 2 : level == -1 ? 9 : level == -2 ? 22 : 23;
                case SiteKind.ResearchLab: return level == -2 ? 18 : level == -1 ? 11 : level == 1 ? 7 : 17;
                case SiteKind.PowerPlant: case SiteKind.WaterTreatment: case SiteKind.Substation:
                    return level == -2 ? 18 : level == -1 ? 11 : 2;
                case SiteKind.Hospital: return level < 0 ? 7 : level == 1 ? 3 : level == 2 ? 15 : 16;
                case SiteKind.Clinic: return level < 0 ? 7 : 3;
                case SiteKind.PoliceStation: return level < 0 ? 8 : level == 1 ? 4 : 20;
                case SiteKind.School: case SiteKind.Library: return level < 0 ? 12 : level == 1 ? 5 : 13;
                case SiteKind.Apartment: case SiteKind.Motel: return level < 0 ? 12 : level == 1 ? 6 : level == 2 ? 13 : 14;
                case SiteKind.Farmhouse: case SiteKind.Barn: return level < 0 ? 12 : 10;
                case SiteKind.Greenhouse: return 10;
                case SiteKind.Warehouse: case SiteKind.Workshop: case SiteKind.LumberMill:
                case SiteKind.TrainDepot: case SiteKind.FireStation: case SiteKind.RecyclingYard: return level < 0 ? 22 : 2;
                case SiteKind.Shop: case SiteKind.Pharmacy: case SiteKind.HardwareStore:
                case SiteKind.Supermarket: case SiteKind.Restaurant: case SiteKind.GasStation: return level < 0 ? 12 : 1;
                case SiteKind.RadioStation: return level < 0 ? 11 : level == 1 ? 2 : 17;
                default: return level < 0 ? 12 : 0;
            }
        }
    }
}
