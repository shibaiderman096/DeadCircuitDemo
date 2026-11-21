using System;
using System.Collections.Generic;
using System.Linq;
using DeadCircuit;

static partial class Checks
{
    // Public seams: new-world generation, sparse chunk commits, current-format
    // save restoration and Apply("prospect"). No private simulator calls.
    static void PetrochemicalResourceChecks()
    {
        Run("petrochemical resources allow oil-free openings and keep generated oil finite", () =>
        {
            foreach (bool chunked in new[] { false, true })
            {
                bool foundOilFree = false, foundOil = false;
                foreach (int seed in Enumerable.Range(-16, 32).Concat(new[] { int.MinValue, -991, 73149, int.MaxValue }))
                {
                    var options = new WorldGenerationSettings { seed = seed, preset = WorldPreset.Wilderness, cityDensity = 0, chunked = chunked };
                    var world = WorldGeneration.Generate(options);
                    var oils = world.nodes.Where(n => n.kind == NodeKind.Oil).ToArray();
                    foundOilFree |= oils.Length == 0; foundOil |= oils.Length > 0;
                    foreach (var oil in oils)
                    {
                        Require(WorldTiles.Ground(world, oil.x, oil.y) is '0' or '1', "oil is not on dry natural ground");
                        var biome = (BiomeKind)(WorldTiles.Biome(world, oil.x, oil.y) - '0');
                        var rule = BiomeResourceCatalog.Get(biome);
                        Require(oil.stock >= rule.oilMinStock && oil.stock <= rule.oilMaxStock, "initial oil stock outside biome limits");
                    }
                    Require(TerrainEntityDump(world) == TerrainEntityDump(WorldGeneration.Generate(options)), "same settings rerolled oil");
                }
                Require(foundOilFree && foundOil, $"chunked={chunked}: opening survey did not find both oil-free and oil-bearing worlds");
            }
        });
        Run("petrochemical resources support a real Apply build oil pump beside a known dry deposit", () =>
        {
            foreach (bool chunked in new[] { false, true })
            {
                var world = PetrochemicalOilFixture(chunked, out var oil);
                var sim = new WorldSimulator(world); var p = sim.AddPlayer("oil-builder", "Builder");
                sim.State.era = TechEra.MediumVoltage;
                sim.State.mvResearch = (1 << (int)PetrochemicalTech.Prospecting) | (1 << (int)PetrochemicalTech.Extraction);
                sim.State.mvSurvey = 1;
                Array.Copy(DemoCatalog.BuildCost(BuildKind.OilPump), p.items, p.items.Length);
                p.x = oil.x - 3; p.y = oil.y;
                sim.Apply(Command(p, "build", oil.x - 2, oil.y, (int)BuildKind.OilPump));
                Require(sim.State.buildings.Any(b => b.kind == BuildKind.OilPump && b.x == oil.x - 2 && b.y == oil.y),
                    $"real oil pump build failed: chunked={chunked}: {p.notice}");
                Require(p.items.All(n => n == 0), "real pump build did not pay exact cost");
            }
        });
        Run("petrochemical resources favor sedimentary biomes and remain reachable in natural future chunks", () =>
        {
            var area = new int[WorldGeneration.BiomeCount];
            var count = new int[area.Length];
            foreach (var rule in BiomeResourceCatalog.All)
                Require(rule.oilPermille > 0 && rule.oilMinStock > 0 && rule.oilMaxStock <= 100000, "biome has no finite background oil");
            for (int cy = -288; cy <= 288; cy += 16)
            for (int cx = -288; cx <= 288; cx += 16)
            {
                var world = TerrainBlank(73149, WorldPreset.Wilderness, 0);
                world.generatorVersion = WorldGeneration.CurrentGeneratorVersion;
                Require(ChunkTerrain.Generate(world, cx, cy), "natural oil survey chunk refused");
                for (int y = cy * 16 + 2; y < cy * 16 + 14; y++)
                for (int x = cx * 16 + 2; x < cx * 16 + 14; x++)
                    if (WorldTiles.Ground(world, x, y) != '2') area[WorldTiles.Biome(world, x, y) - '0']++;
                var blocked = world.nodes.Select(n => WorldTiles.Key(n.x, n.y)).ToHashSet();
                foreach (var b in world.buildings.Where(b => b.kind != BuildKind.Door))
                    for (int y = b.y; y < b.y + DemoCatalog.Height(b); y++)
                        for (int x = b.x; x < b.x + DemoCatalog.Width(b); x++) blocked.Add(WorldTiles.Key(x, y));
                var reached = TerrainFlood(world, cx, cy, blocked);
                foreach (var oil in world.nodes.Where(n => n.kind == NodeKind.Oil))
                {
                    int biome = WorldTiles.Biome(world, oil.x, oil.y) - '0'; count[biome]++;
                    var rule = BiomeResourceCatalog.Get((BiomeKind)biome);
                    Require(oil.stock >= rule.oilMinStock && oil.stock <= rule.oilMaxStock, "natural oil stock outside biome limits");
                    Require(PetrochemicalAdjacent(reached, oil.x, oil.y), "natural oil has no dry approach from its chunk boundary");
                    Require(oil.x >= cx * 16 + 2 && oil.x < cx * 16 + 14 && oil.y >= cy * 16 + 2 && oil.y < cy * 16 + 14,
                        "oil blocks a chunk boundary corridor");
                }
                Require(world.nodes.Count <= ChunkTerrain.MaxNodesPerChunk &&
                    world.nodes.Count(n => n.kind != NodeKind.Wall && n.kind != NodeKind.Crate) <= ChunkTerrain.MaxResourcesPerChunk, "oil exceeds chunk budgets");
            }
            Require(area.All(n => n >= 200), "survey missed a biome: " + string.Join(",", area));
            double meadowRate = count[(int)BiomeKind.Meadow] / (double)area[(int)BiomeKind.Meadow];
            foreach (var biome in new[] { BiomeKind.Desert, BiomeKind.Wetland, BiomeKind.Swamp, BiomeKind.RiverValley })
                Require(count[(int)biome] > 0 && count[(int)biome] / (double)area[(int)biome] > meadowRate * 3, "oil preference absent in " + biome);
            Require(count.Where((n, i) => i != (int)BiomeKind.Desert && i != (int)BiomeKind.Wetland &&
                i != (int)BiomeKind.Swamp && i != (int)BiomeKind.RiverValley).Sum() > 0, "oil exists exclusively in favored biomes");
        });
        Run("petrochemical resources reproduce signed future chunks independently of discovery order", () =>
        {
            foreach (int seed in new[] { int.MinValue, 73149, int.MaxValue })
            {
                var first = WorldGeneration.Generate(new WorldGenerationSettings { seed = seed, chunked = true });
                var second = WorldGeneration.Generate(new WorldGenerationSettings { seed = seed, chunked = true });
                var chunks = new List<(int x, int y)>();
                for (int cy = -72; cy <= 72; cy += 24)
                    for (int cx = -72; cx <= 72; cx += 24)
                        if (cx != 0 || cy != 0) chunks.Add((cx, cy));
                foreach (var cell in chunks) Require(ChunkTerrain.Generate(first, cell.x, cell.y), "forward oil chunk refused");
                chunks.Reverse();
                foreach (var cell in chunks) Require(ChunkTerrain.Generate(second, cell.x, cell.y), "reverse oil chunk refused");
                Require(TerrainMapDump(first) == TerrainMapDump(second) && TerrainEntityDump(first) == TerrainEntityDump(second), "oil depends on chunk discovery order");
                Require(first.nodes.Any(n => n.kind == NodeKind.Oil && (n.x < 0 || n.y < 0 || n.x >= 64 || n.y >= 64)), "future chunks contain no natural oil");
                string frozen = Dump(first);
                Require(!ChunkTerrain.Generate(first, chunks[0].x, chunks[0].y) && Dump(first) == frozen, "revisiting chunk replenished oil");
            }
        });
        Run("petrochemical resources current saves deep copy finite stock and never refill depleted sources", () =>
        {
            foreach (bool chunked in new[] { false, true })
            {
                var sim = new WorldSimulator(PetrochemicalOilFixture(chunked, out var deposit));
                var oil = sim.State.nodes.Single(n => n.id == deposit.id);
                oil.stock -= 7;
                var source = Saved(sim.State); string frozen = Dump(source);
                var restored = new WorldSimulator(source);
                Require(restored.State.version == WorldSimulator.SaveVersion && restored.State.generatorVersion == WorldGeneration.CurrentGeneratorVersion, "current save changed generator/schema");
                var copied = restored.State.nodes.Single(n => n.id == oil.id);
                Require(copied.stock == oil.stock && !ReferenceEquals(copied, source.nodes.Single(n => n.id == oil.id)), "restored oil stock aliases save or refills");
                copied.stock--;
                Require(Dump(source) == frozen, "restored oil mutated caller graph");
                restored.State.nodes.Remove(copied);
                var exhausted = new WorldSimulator(Saved(restored.State));
                Require(exhausted.State.nodes.All(n => n.id != oil.id), "restore replenished an exhausted oil source");
                if (chunked)
                {
                    Require(!ChunkTerrain.Generate(exhausted.State, WorldTiles.FloorDiv(oil.x, 16), WorldTiles.FloorDiv(oil.y, 16)), "depleted source chunk regenerated");
                    Require(ChunkTerrain.Generate(sim.State, -11, 7) && ChunkTerrain.Generate(exhausted.State, -11, 7), "restored future oil chunk refused");
                    Require(TerrainEntityDump(sim.State, -11, 7) == TerrainEntityDump(exhausted.State, -11, 7), "restore rerolled future oil");
                }
                foreach (int stock in new[] { -1, 0, 100001 })
                {
                    var invalid = Saved(source); invalid.nodes.Single(n => n.id == oil.id).stock = stock;
                    TerrainThrows<ArgumentException>(() => new WorldSimulator(invalid));
                }
            }
        });
        Run("petrochemical prospecting requires MV research a living connected player and interaction cooldown", () =>
        {
            foreach (string rejection in new[] { "era", "research", "dead", "offline", "cooldown" })
            {
                var sim = PetrochemicalSurveyFixture(out var p);
                if (rejection == "era") sim.State.era = TechEra.LowVoltage;
                if (rejection == "research") sim.State.mvResearch = 0;
                if (rejection == "dead") p.hp = 0;
                if (rejection == "offline") p.connected = false;
                if (rejection == "cooldown") p.interactCooldown = .5f;
                string nodes = TerrainEntityDump(sim.State); var items = (int[])p.items.Clone();
                sim.Apply(Command(p, "prospect"));
                Require(sim.State.mvSurvey == 0 && nodes == TerrainEntityDump(sim.State) && items.SequenceEqual(p.items), "prospecting bypassed " + rejection);
            }
        });
        Run("petrochemical prospecting reports real coordinates direction distance and biome without products", () =>
        {
            var sim = PetrochemicalSurveyFixture(out var p);
            var oil = sim.State.nodes.Single(n => n.kind == NodeKind.Oil);
            p.x = oil.x - 3; p.y = oil.y - 4;
            string nodes = TerrainEntityDump(sim.State), map = TerrainMapDump(sim.State), explored = p.explored;
            var items = (int[])p.items.Clone(); var produced = (int[])sim.State.produced.Clone();
            int gathered = sim.State.gathered;
            sim.Apply(Command(p, "prospect"));
            Require(sim.State.mvSurvey == 1 && p.interactCooldown == .5f, "real oil did not grant survey evidence/cooldown");
            Require(p.notice.Contains("(" + oil.x + "," + oil.y + ")") && p.notice.Contains("东北") &&
                p.notice.Contains("5.0格") && p.notice.Contains("草甸") && p.notice.Contains("小油藏"), "prospect omitted location/direction/distance/biome explanation: " + p.notice);
            Require(TerrainEntityDump(sim.State) == nodes && TerrainMapDump(sim.State) == map && p.explored == explored &&
                p.items.SequenceEqual(items) && sim.State.produced.SequenceEqual(produced) && sim.State.gathered == gathered, "prospecting manufactured, consumed or explored resources");
            string notice = p.notice; sim.State.mvSurvey = 0; sim.Apply(Command(p, "prospect"));
            Require(sim.State.mvSurvey == 0 && p.notice == notice, "cooldown allowed a second survey");
            p.interactCooldown = 0; sim.Apply(Command(p, "prospect"));
            var saved = Saved(sim.State); var copy = new WorldSimulator(saved);
            Require(copy.State.mvSurvey == 1, "current save lost actual survey evidence");
            copy.State.mvSurvey = 0; Require(saved.mvSurvey == 1, "survey evidence aliases save");
        });
        Run("petrochemical prospecting selects the nearest source and includes the exact 48 tile boundary", () =>
        {
            var sim = PetrochemicalSurveyFixture(out var p); sim.State.nodes.Clear(); p.x = 8; p.y = 8;
            var east = new NodeState { id = sim.State.nextId++, x = 56, y = 8, kind = NodeKind.Oil, stock = 100 };
            var north = new NodeState { id = sim.State.nextId++, x = 8, y = 56, kind = NodeKind.Oil, stock = 100 };
            sim.State.nodes.Add(north); sim.State.nodes.Add(east);
            sim.Apply(Command(p, "prospect"));
            Require(sim.State.mvSurvey == 1 && p.notice.Contains("(56,8)") && p.notice.Contains("东方") && p.notice.Contains("48.0格"), "boundary/tie ordering failed: " + p.notice);
            sim.State.nodes.Reverse(); p.interactCooldown = 0; sim.Apply(Command(p, "prospect"));
            Require(p.notice.Contains("(56,8)"), "source enumeration order changed tie result");
            east.x = 10; p.interactCooldown = 0; sim.Apply(Command(p, "prospect"));
            Require(p.notice.Contains("(10,8)") && p.notice.Contains("2.0格"), "prospecting selected a farther source");
        });
        Run("petrochemical prospecting absence exhaustion outside radius and unknown chunks create no oil or exploration", () =>
        {
            foreach (string absence in new[] { "empty", "exhausted", "outside", "unknown" })
            {
                var sim = PetrochemicalSurveyFixture(out var p);
                var oil = sim.State.nodes.Single(n => n.kind == NodeKind.Oil);
                p.x = 8; p.y = 8; oil.x = 56; oil.y = 8;
                if (absence == "empty") sim.State.nodes.Clear();
                if (absence == "exhausted") oil.stock = 0;
                if (absence == "outside") p.x = 7.9f;
                if (absence == "unknown")
                {
                    p.x = 32; p.y = 35; oil.x = 64; oil.y = 35;
                    sim.State.chunks.enabled = true; // no records: no known terrain
                }
                string nodes = TerrainEntityDump(sim.State), map = TerrainMapDump(sim.State);
                var items = (int[])p.items.Clone();
                sim.Apply(Command(p, "prospect"));
                Require(sim.State.mvSurvey == 0 && p.interactCooldown == .5f && p.notice.Contains("继续探索") &&
                    p.notice.Contains("荒漠") && p.notice.Contains("湿地") && p.notice.Contains("沼泽") && p.notice.Contains("河谷"), "absence was not explained: " + absence + ": " + p.notice);
                Require(TerrainEntityDump(sim.State) == nodes && TerrainMapDump(sim.State) == map && p.items.SequenceEqual(items) &&
                    sim.State.chunks.records.Count == 0 && p.exploredChunks.Count == 0, "negative survey generated oil or explored chunks");
            }
        });
    }

    static WorldState PetrochemicalOilFixture(bool chunked, out NodeState oil)
    {
        var world = chunked ? WorldGeneration.Generate(new WorldGenerationSettings { chunked = true }) : Flat();
        world.generatorVersion = WorldGeneration.CurrentGeneratorVersion;
        if (chunked) InfiniteClearChunk(world, -7, -2);
        else world.ground = new string('0', 4096);
        int x = chunked ? -7 * 16 + 8 : 16, y = chunked ? -2 * 16 + 8 : 16;
        oil = new NodeState { id = world.nextId++, x = x, y = y, kind = NodeKind.Oil, hp = 35, stock = 2400 };
        world.nodes.Add(oil);
        return world;
    }

    static WorldSimulator PetrochemicalSurveyFixture(out PlayerState p)
    {
        // Keep earned LV acceptance while starting MV with no survey/production
        // evidence; the shared Flat(MV) fixture may represent a finished factory.
        var world = Flat(era: TechEra.LowVoltage); world.era = TechEra.MediumVoltage;
        var sim = new WorldSimulator(world);
        p = sim.AddPlayer("oil-surveyor", "Surveyor");
        sim.State.mvResearch = 1 << (int)PetrochemicalTech.Prospecting;
        sim.State.nodes.Add(new NodeState { id = sim.State.nextId++, x = 38, y = 37, kind = NodeKind.Oil, stock = 2400 });
        return sim;
    }

    static HashSet<long> PetrochemicalReachable(WorldState world)
    {
        var blocked = world.nodes.Select(n => WorldTiles.Key(n.x, n.y)).ToHashSet();
        foreach (var b in world.buildings.Where(b => b.kind != BuildKind.Door))
            for (int y = b.y; y < b.y + DemoCatalog.Height(b); y++)
                for (int x = b.x; x < b.x + DemoCatalog.Width(b); x++) blocked.Add(WorldTiles.Key(x, y));
        var reached = new HashSet<long> { WorldTiles.Key(32, 35) };
        var queue = new Queue<(int x, int y)>(); queue.Enqueue((32, 35));
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var next in new[] { (cell.x - 1, cell.y), (cell.x + 1, cell.y), (cell.x, cell.y - 1), (cell.x, cell.y + 1) })
            {
                char ground = WorldTiles.Ground(world, next.Item1, next.Item2);
                long key = WorldTiles.Key(next.Item1, next.Item2);
                if (ground == '?' || ground == '2' || blocked.Contains(key) || !reached.Add(key)) continue;
                queue.Enqueue(next);
            }
        }
        return reached;
    }

    static bool PetrochemicalHasPumpSpace(WorldState world, NodeState oil) => PetrochemicalPumpSites(world, oil).Any();

    static IEnumerable<(int x, int y)> PetrochemicalPumpSites(WorldState world, NodeState oil)
    {
        int width = DemoCatalog.Width(BuildKind.OilPump), height = DemoCatalog.Height(BuildKind.OilPump);
        var blocked = world.nodes.Select(n => WorldTiles.Key(n.x, n.y)).ToHashSet();
        foreach (var b in world.buildings)
            for (int y = b.y; y < b.y + DemoCatalog.Height(b); y++)
                for (int x = b.x; x < b.x + DemoCatalog.Width(b); x++) blocked.Add(WorldTiles.Key(x, y));
        for (int py = oil.y - height; py <= oil.y + 1; py++)
        for (int px = oil.x - width; px <= oil.x + 1; px++)
        {
            bool clear = true;
            for (int y = py; y < py + height; y++) for (int x = px; x < px + width; x++)
            {
                char ground = WorldTiles.Ground(world, x, y);
                if (ground == '2' || ground == '?' || blocked.Contains(WorldTiles.Key(x, y)) ||
                    world.generatorVersion < 10 && Math.Abs(x - 32) <= 1 && Math.Abs(y - 35) <= 1 ||
                    FloorWorld.IsProtectedCell(world, x, y)) clear = false;
                if (world.zombies.Any(z => Math.Abs(z.x - x) < .8f && Math.Abs(z.y - y) < .8f)) clear = false;
                if (world.survival != null && world.survival.enabled &&
                    (world.survival.plots.Any(plot => plot.x == x && plot.y == y) ||
                    world.survival.creatures.Any(c => Math.Abs(c.x - x) < .8f && Math.Abs(c.y - y) < .8f))) clear = false;
            }
            if (clear) yield return (px, py);
        }
    }

    static bool PetrochemicalAdjacent(HashSet<long> cells, int x, int y) =>
        cells.Contains(WorldTiles.Key(x - 1, y)) || cells.Contains(WorldTiles.Key(x + 1, y)) ||
        cells.Contains(WorldTiles.Key(x, y - 1)) || cells.Contains(WorldTiles.Key(x, y + 1));
}
