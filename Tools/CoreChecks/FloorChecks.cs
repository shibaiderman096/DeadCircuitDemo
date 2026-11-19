using System;
using System.Collections.Generic;
using System.Linq;
using DeadCircuit;

static partial class Checks
{
    // Public seams agreed for this feature: metadata/tile queries, commands and save/restore.
    static void FloorChecks()
    {
        Run("floors plans cover all kinds and remove stairs beyond level limits", () =>
        {
            foreach (SiteKind kind in Enum.GetValues<SiteKind>())
            {
                int min = FloorWorld.MinLevel(kind), max = FloorWorld.MaxLevel(kind);
                Require(min <= 0 && max >= 0 && max - min + 1 >= 2 && max - min + 1 <= 5, "floor count: " + kind);
                var layouts = new HashSet<string>();
                for (int level = min; level <= max; level++)
                {
                    if (level == 0) continue;
                    var site = new SiteState { kind = kind, level = level, parentSiteId = 1 };
                    var plan = FloorWorld.GetPlan(site);
                    Require(plan != null && plan.Width <= 12 && plan.Height <= 12, "floor dimensions");
                    string cells = string.Concat(plan.Rows);
                    Require(cells.Count(c => c == '<') == (level < max ? 1 : 0) &&
                        cells.Count(c => c == '>') == (level > min ? 1 : 0), "endpoint stair directions");
                    Require(plan.Rows.All(row => row.Length == plan.Width && row.All(c => c == '#' || c == '+' || c == '.' || c == '<' || c == '>' || SiteBlueprintCatalog.Crates.Contains(c) || SiteBlueprintCatalog.Props.Contains(c))), "floor grammar");
                    Require(layouts.Add(string.Join("/", plan.Rows).Replace('<', '.').Replace('>', '.')), "reused floor of " + kind);
                    for (int rotation = 0; rotation < 4; rotation++)
                    {
                        site.layoutVersion = 1;
                        site.rotation = rotation;
                        site.width = SiteBlueprintCatalog.Width(plan, rotation);
                        site.height = SiteBlueprintCatalog.Height(plan, rotation);
                        CheckFloorPlanConnectivity(site);
                    }
                }
                var ground = SiteBlueprintCatalog.Get(kind);
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    var root = new SiteState { kind = kind, layoutVersion = 1, rotation = rotation,
                        width = SiteBlueprintCatalog.Width(ground, rotation), height = SiteBlueprintCatalog.Height(ground, rotation) };
                    var stairs = FloorCells(root).Where(c => FloorWorld.StairGlyph(root, c.x, c.y) != '\0').ToArray();
                    Require(stairs.Length == (max > 0 ? 1 : 0) + (min < 0 ? 1 : 0), "ground stair count: " + kind);
                    Require(stairs.All(c => SiteBlueprintCatalog.At(root, c.x, c.y) == '.' && FloorNeighbors(c).Any(n =>
                        SiteBlueprintCatalog.At(root, n.x, n.y) == '.' && FloorWorld.StairGlyph(root, n.x, n.y) == '\0')), "ground stair lacks plain approach: " + kind);
                }
            }
            Require(!FloorWorld.IsReservedChunk(-4097, 0) && FloorWorld.IsReservedChunk(-4096, -4096) &&
                FloorWorld.IsReservedChunk(-2049, 63) && !FloorWorld.IsReservedChunk(-2048, 0) &&
                !FloorWorld.IsReservedChunk(-4080, -4097) && !FloorWorld.IsReservedChunk(-4080, 64), "reserved half-open pocket");
            Require(FloorWorld.SlotOrdinal(-2064, 48, out int last) && last == 127 * 259 - 1 &&
                !FloorWorld.SlotOrdinal(-4080, 64, out _), "slot ordinal upper bound");
        });
        Run("floors natural generation can go around the reserve and allocation skips a taken slot", () =>
        {
            var sim = FloorFixture(out var root, out var p); var world = sim.State;
            Require(!ChunkStreaming.GenerateChunk(world, -4080, -4080) &&
                ChunkStreaming.GenerateChunk(world, -4080, -4097) && ChunkStreaming.GenerateChunk(world, -4080, 64), "natural generation did not respect the bounded reserve");
            var occupied = new WorldChunkState { x = -4080, y = -4080, ground = new string('1', 256), biomes = new string('0', 256) };
            world.chunks.records.Add(occupied);
            FloorInteract(sim, p, FloorStair(root, '<'));
            var floor = world.sites.Single(s => s.parentSiteId == root.id);
            var slot = world.chunks.records.Single(c => c.interiorSiteId == floor.id);
            Require(slot.x == -4064 && slot.y == -4080 && occupied.interiorSiteId == 0 && occupied.ground[0] == '1', "occupied slot was overwritten or did not advance");
        });
        Run("floors E travels both ways and lazily creates isolated upper and lower chunks", () =>
        {
            var sim = FloorFixture(out var root, out var p, rotation: 1);
            var upstairs = FloorStair(root, '<'); var downstairs = FloorStair(root, '>');
            Require(FloorWorld.Location(sim.State, upstairs.x, upstairs.y).Contains("公寓/地面/") &&
                FloorWorld.Location(sim.State, root.x + root.width, root.y + 1) == "", "ground indoor/outdoor location");
            int records = sim.State.chunks.records.Count, revision = sim.State.chunks.revision, sites = sim.State.sites.Count;
            var spawn = (p.x, p.y);
            sim.Apply(Command(p, "interact", upstairs.x, upstairs.y));
            Require((p.x, p.y) == spawn && sim.State.sites.Count == sites, "remote command created or entered a floor");
            sim.Apply(new PlayerCommand { playerId = p.id, action = "move", moveX = 1, aiming = true, sprint = true });
            FloorInteract(sim, p, upstairs);
            var upper = sim.State.sites.Single(s => s.parentSiteId == root.id && s.level == 1);
            Require((p.x, p.y) == ((float)upper.doorX, (float)upper.doorY) && !p.aiming && !p.moving, "arrival or input reset");
            Require(MapExploration.IsExplored(sim.State, p, (int)p.x, (int)p.y), "arrival was not revealed");
            var arrival = (p.x, p.y);
            var slot = sim.State.chunks.records.Single(c => c.interiorSiteId == upper.id);
            Require(slot.x == -4080 && slot.y == -4080 && slot.ground.Count(c => c == '?') == 256 - upper.width * upper.height &&
                slot.biomes.Count(c => c == '2') == upper.width * upper.height && slot.biomes.Count(c => c == '?') == slot.ground.Count(c => c == '?'), "first slot or interior padding");
            Require(FloorWorld.IsInterior(sim.State, (int)p.x, (int)p.y) && FloorWorld.Location(sim.State, (int)p.x, (int)p.y).Contains("公寓/2楼/") &&
                sim.IsBlocked(upper.x, upper.y) && sim.IsBlocked(upper.x - 1, upper.y), "interior label, real wall or void");
            sim.Step(WorldSimulator.FixedStep);
            Require((p.x, p.y) == arrival && sim.State.chunks.records.Count == records + 1, "held movement or streaming leaked into floor");
            FloorInteract(sim, p, FloorStair(upper, '>'));
            Require((p.x, p.y) == ((float)upstairs.x, (float)upstairs.y), "upstairs return missed its paired stair");
            FloorInteract(sim, p, downstairs);
            var lower = sim.State.sites.Single(s => s.parentSiteId == root.id && s.level == -1);
            Require(lower.x - upper.x == 256 && lower.y == upper.y && lower.kind == root.kind && lower.rotation == root.rotation &&
                lower.settlement == root.settlement && sim.State.chunks.records.Count == records + 2 && sim.State.chunks.revision == revision + 2, "upper/lower instances overlapped or metadata diverged");
            FloorInteract(sim, p, FloorStair(lower, '<'));
            Require((p.x, p.y) == ((float)downstairs.x, (float)downstairs.y) && FloorWorld.Location(sim.State, downstairs.x, downstairs.y).Contains("公寓/地面/"), "basement return missed ground");
        });
        Run("floors E rejects occupied landings and retries after the other player moves", () =>
        {
            var sim = FloorFixture(out var root, out var a); var b = sim.AddPlayer("floor-b", "B");
            var stair = FloorStair(root, '<');
            FloorInteract(sim, a, stair);
            int id = sim.State.nextId, records = sim.State.chunks.records.Count;
            FloorInteract(sim, b, stair);
            Require((b.x, b.y) == ((float)stair.x, (float)stair.y) && b.notice.Contains("占用") &&
                sim.State.nextId == id && sim.State.chunks.records.Count == records, "second player overlapped the first or regenerated the floor");
            var upper = sim.State.sites.Single(s => s.parentSiteId == root.id && s.level == 1);
            var clear = FloorCells(upper).First(c => SiteBlueprintCatalog.At(upper, c.x, c.y) == '.' &&
                Math.Abs(c.x - upper.doorX) + Math.Abs(c.y - upper.doorY) == 1);
            a.x = clear.x; a.y = clear.y;
            FloorInteract(sim, b, stair);
            Require((b.x, b.y) == ((float)upper.doorX, (float)upper.doorY) && (a.x, a.y) != (b.x, b.y), "freed landing could not be reused");
        });
        Run("floors E preserves persistent generation state when creation exceeds a budget", () =>
        {
            foreach (string budget in new[] { "ids", "sites", "records", "revision" })
            {
                var sim = FloorFixture(out var root, out var p); var world = sim.State;
                var stair = FloorStair(root, '<');
                if (budget == "ids") world.nextId = int.MaxValue - 50;
                if (budget == "revision") world.chunks.revision = int.MaxValue;
                if (budget == "sites") while (world.sites.Count < WorldSimulator.MaxSites) world.sites.Add(new SiteState());
                if (budget == "records") while (world.chunks.records.Count < ChunkStreaming.MaxRecords) world.chunks.records.Add(new WorldChunkState { x = 900, y = 900 });
                string GenerationState() => System.Text.Json.JsonSerializer.Serialize(new
                { world.nextId, world.chunks.revision, world.chunks.records, world.sites, world.nodes, world.buildings }, Json);
                string before = GenerationState();
                FloorInteract(sim, p, stair);
                Require((p.x, p.y) == ((float)stair.x, (float)stair.y) && p.notice.Contains("预算") &&
                    GenerationState() == before, "floor refused non-atomically at " + budget);
            }
        });
        Run("floors save remembers destroyed walls and emptied crates across reentry", () =>
        {
            var sim = FloorFixture(out var root, out var p); var stair = FloorStair(root, '<');
            FloorInteract(sim, p, stair);
            var upper = sim.State.sites.Single(s => s.parentSiteId == root.id && s.level == 1);
            var cells = FloorCells(upper).ToHashSet();
            bool HasApproach(NodeState node) => FloorNeighbors((node.x, node.y)).Any(c => cells.Contains(c) && !sim.IsBlocked(c.x, c.y));
            var wall = sim.State.nodes.First(n => n.kind == NodeKind.Wall && cells.Contains((n.x, n.y)) && HasApproach(n));
            var crate = sim.State.nodes.First(n => n.kind == NodeKind.Crate && cells.Contains((n.x, n.y)) && HasApproach(n));
            foreach (var node in new[] { wall, crate })
            {
                var beside = FloorNeighbors((node.x, node.y)).First(c => cells.Contains(c) && !sim.IsBlocked(c.x, c.y));
                node.stock = 1;
                p.x = beside.x; p.y = beside.y; p.interactCooldown = 0;
                sim.Apply(Command(p, "interact", node.x, node.y));
                Require(!sim.State.nodes.Contains(node), "E did not deplete floor " + node.kind);
            }
            Require(!sim.IsBlocked(wall.x, wall.y), "destroyed wall still blocks movement");
            FloorInteract(sim, p, FloorStair(upper, '>'));
            var restored = new WorldSimulator(Saved(sim.State)); p = restored.State.players.Single();
            int next = restored.State.nextId, records = restored.State.chunks.records.Count;
            FloorInteract(restored, p, stair);
            Require((p.x, p.y) == ((float)upper.doorX, (float)upper.doorY) && next == restored.State.nextId && records == restored.State.chunks.records.Count &&
                !restored.State.nodes.Any(n => (n.x, n.y) == (wall.x, wall.y) || (n.x, n.y) == (crate.x, crate.y)), "save or reentry replenished a damaged instance");
        });
        Run("floors generator4 save keeps ground landmarks and ordinary reserved-band terrain", () =>
        {
            var sim = FloorFixture(out var root, out var p, gen: 4); var stair = FloorStair(root, '<');
            Require(FloorWorld.StairGlyph(sim.State, stair.x, stair.y) == '\0', "old generator exposed a stair");
            int next = sim.State.nextId, records = sim.State.chunks.records.Count;
            FloorInteract(sim, p, stair);
            Require((p.x, p.y) == ((float)stair.x, (float)stair.y) && sim.State.sites.All(s => s.parentSiteId == 0) &&
                sim.State.nextId == next && sim.State.chunks.records.Count == records, "old generator received an interior");
            Require(ChunkStreaming.GenerateChunk(sim.State, -4080, -4080) &&
                sim.State.chunks.records.Last().interiorSiteId == 0, "old generator lost access to ordinary far-west terrain");
        });
    }

    private static WorldSimulator FloorFixture(out SiteState root, out PlayerState player, int rotation = 0, int gen = 5)
    {
        var world = WorldGeneration.Generate(new WorldGenerationSettings { seed = 73149, chunked = true });
        world.generatorVersion = gen;
        var plan = SiteBlueprintCatalog.Get(SiteKind.Apartment);
        var site = new SiteState { id = world.nextId++, kind = SiteKind.Apartment, district = DistrictKind.Urban,
            x = 162, y = 162, rotation = rotation, layoutVersion = 1, width = SiteBlueprintCatalog.Width(plan, rotation), height = SiteBlueprintCatalog.Height(plan, rotation) };
        site.settlement = gen >= 5 ? SettlementGeneration.Sample(world, site.x, site.y) : SettlementKind.Unspecified;
        (site.doorX, site.doorY) = FloorCells(site).First(c => SiteBlueprintCatalog.At(site, c.x, c.y) == '+' &&
            (c.x == site.x || c.y == site.y || c.x == site.x + site.width - 1 || c.y == site.y + site.height - 1));
        world.sites.Add(site);
        world.chunks.records.Add(new WorldChunkState { x = 10, y = 10, ground = new string('4', 256), biomes = new string('2', 256) });
        world.chunks.revision++;
        PrepareLegacySaveFixture(world, gen < 5 ? 9 : 10, gen);
        var sim = new WorldSimulator(world); root = sim.State.sites.Single(s => s.id == site.id);
        player = sim.AddPlayer("floor-a", "A"); return sim;
    }

    private static IEnumerable<(int x, int y)> FloorCells(SiteState site)
    {
        for (int y = site.y; y < site.y + site.height; y++)
            for (int x = site.x; x < site.x + site.width; x++) yield return (x, y);
    }
    private static IEnumerable<(int x, int y)> FloorNeighbors((int x, int y) cell)
    {
        yield return (cell.x - 1, cell.y);
        yield return (cell.x + 1, cell.y);
        yield return (cell.x, cell.y - 1);
        yield return (cell.x, cell.y + 1);
    }
    private static void CheckFloorPlanConnectivity(SiteState site)
    {
        string label = $"{site.kind} level {site.level} rotation {site.rotation}";
        var glyphs = FloorCells(site).ToDictionary(c => c, c => SiteBlueprintCatalog.At(site, c.x, c.y));
        var perimeter = glyphs.Where(p => p.Key.x == 0 || p.Key.y == 0 || p.Key.x == site.width - 1 || p.Key.y == site.height - 1);
        Require(perimeter.All(p => p.Value == '#'), "floor perimeter has an exit: " + label);
        var walkable = glyphs.Where(p => p.Value != '#' && !SiteBlueprintCatalog.Crates.Contains(p.Value)).Select(p => p.Key).ToHashSet();
        var entrance = FloorStair(site, site.level > 0 ? '>' : '<');
        var visited = new HashSet<(int x, int y)> { entrance };
        var queue = new Queue<(int x, int y)>(); queue.Enqueue(entrance);
        while (queue.TryDequeue(out var cell))
            foreach (var neighbor in FloorNeighbors(cell))
                if (walkable.Contains(neighbor) && visited.Add(neighbor)) queue.Enqueue(neighbor);
        Require(visited.SetEquals(walkable), "floor has unreachable ground or doors: " + label);
        foreach (var cell in glyphs.Where(p => SiteBlueprintCatalog.Crates.Contains(p.Value)))
            Require(FloorNeighbors(cell.Key).Any(visited.Contains), "floor has an unreachable crate: " + label);
        foreach (var cell in glyphs.Where(p => p.Value == '<' || p.Value == '>'))
            Require(FloorNeighbors(cell.Key).Any(n => glyphs.TryGetValue(n, out char c) && c == '.'), "floor stair lacks plain approach: " + label);
    }

    private static (int x, int y) FloorStair(SiteState site, char glyph) =>
        FloorCells(site).Single(c => FloorWorld.StairGlyph(site, c.x, c.y) == glyph);

    private static void FloorInteract(WorldSimulator sim, PlayerState player, (int x, int y) stair)
    {
        player.x = stair.x; player.y = stair.y; player.interactCooldown = 0;
        sim.Apply(Command(player, "interact", stair.x, stair.y));
    }
}
