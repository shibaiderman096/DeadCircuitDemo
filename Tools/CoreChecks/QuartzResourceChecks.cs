using DeadCircuit;

static partial class Checks
{
    static void QuartzResourceChecks()
    {
        int generator = WorldGeneration.CurrentGeneratorVersion;
        if (generator >= 10)
        {
            Run("quartz resources are reachable from the natural opening spawn within initial chunk budgets", () =>
            {
                foreach (int seed in new[] { int.MinValue, -991, 0, 73149, int.MaxValue })
                foreach (WorldPreset preset in Enum.GetValues<WorldPreset>())
                foreach (int density in new[] { 0, 1, 2 }) foreach (bool chunked in new[] { false, true })
                {
                    var settings = new WorldGenerationSettings { seed = seed, preset = preset, cityDensity = density, chunked = chunked };
                    var sim = new WorldSimulator(settings);
                    var world = sim.State;
                    var spawn = sim.AddPlayer("quartz-opening", "Explorer");
                    string scenario = $"{seed}/{preset}/{density}/{chunked}";
                    var blocked = world.nodes.Select(n => WorldTiles.Key(n.x, n.y)).ToHashSet();
                    foreach (var b in world.buildings.Where(b => b.kind != BuildKind.Door))
                        for (int y = b.y; y < b.y + DemoCatalog.Height(b); y++)
                            for (int x = b.x; x < b.x + DemoCatalog.Width(b); x++) blocked.Add(WorldTiles.Key(x, y));
                    var reached = new HashSet<long>();
                    var queue = new Queue<long>();
                    void Visit(int x, int y)
                    {
                        long key = WorldTiles.Key(x, y);
                        if (x < 0 || x >= 64 || y < 0 || y >= 64 || blocked.Contains(key) ||
                            WorldTiles.Ground(world, x, y) is '?' or '2' || !reached.Add(key)) return;
                        queue.Enqueue(key);
                    }
                    Visit((int)spawn.x, (int)spawn.y);
                    while (queue.TryDequeue(out long cell))
                    {
                        int x = WorldTiles.X(cell), y = WorldTiles.Y(cell);
                        Visit(x - 1, y); Visit(x + 1, y); Visit(x, y - 1); Visit(x, y + 1);
                    }
                    var quartz = world.nodes.Where(n => n.kind == NodeKind.Quartz).ToArray();
                    Require(quartz.Any(n => PetrochemicalAdjacent(reached, n.x, n.y)), "no reachable opening quartz: " + scenario);
                    Require(quartz.All(n => n.stock >= 80 && n.stock <= 160 && n.x > 0 && n.x < 63 && n.y > 0 && n.y < 63 &&
                        WorldTiles.Ground(world, n.x, n.y) is '0' or '1'), "quartz occupies the outer edge or has invalid ground or stock: " + scenario);
                    Require(quartz.Select(n => (n.x, n.y, n.stock)).SequenceEqual(WorldGeneration.Generate(settings).nodes
                        .Where(n => n.kind == NodeKind.Quartz).Select(n => (n.x, n.y, n.stock))), "opening quartz is not deterministic: " + scenario);
                    if (!chunked) continue;
                    Require(world.chunks.records.Count == 16, "quartz explored extra chunks: " + scenario);
                    foreach (var chunk in world.nodes.GroupBy(n => (n.x / 16, n.y / 16)))
                        Require(chunk.Count() <= ChunkTerrain.MaxNodesPerChunk &&
                            chunk.Count(n => n.kind != NodeKind.Wall && n.kind != NodeKind.Crate) <= ChunkTerrain.MaxResourcesPerChunk,
                            "quartz exceeds the initial chunk budget: " + scenario);
                }
            });
            return;
        }

        Run("quartz resources leave a finite dry starter deposit beyond the tutorial and preserve oil pump access", () =>
        {
            var locations = new HashSet<long>();
            foreach (int seed in new[] { int.MinValue, -991, 0, 73149, int.MaxValue })
            foreach (WorldPreset preset in Enum.GetValues<WorldPreset>())
            foreach (int density in new[] { 0, 1, 2 })
            {
                var options = new WorldGenerationSettings { seed = seed, preset = preset, cityDensity = density };
                var world = WorldGeneration.Generate(options);
                options.chunked = true;
                var chunked = WorldGeneration.Generate(options);
                string scenario = $"{seed}/{preset}/{density}";
                Require(world.generatorVersion == 9 && chunked.generatorVersion == 9, "quartz changed the generator version");
                Require(chunked.chunks.records.Count == 16 && world.ground == chunked.ground &&
                    TerrainEntityDump(world) == TerrainEntityDump(chunked), "chunked start differs from the same finite start: " + scenario);

                var reached = PetrochemicalReachable(world);
                var quartz = world.nodes.Where(n => n.kind == NodeKind.Quartz).ToArray();
                Require(quartz.Length > 0, "missing starter quartz: " + scenario);
                foreach (var node in quartz)
                {
                    locations.Add(WorldTiles.Key(node.x, node.y));
                    Require(node.stock >= 80 && node.stock <= 160, "quartz stock outside the finite 80..160 range");
                    Require(node.x >= 3 && node.x <= 60 && node.y >= 3 && node.y <= 60 &&
                        (node.x < 22 || node.x > 42 || node.y < 22 || node.y > 42) &&
                        (WorldTiles.Ground(world, node.x, node.y) == '0' || WorldTiles.Ground(world, node.x, node.y) == '1'),
                        "quartz occupied a border, tutorial, structured or water tile: " + scenario);
                    Require(PetrochemicalAdjacent(reached, node.x, node.y), "quartz approach closed: " + scenario);
                }
                for (int i = 0; i < 64; i++)
                    Require(reached.Contains(WorldTiles.Key(i, 0)) && reached.Contains(WorldTiles.Key(i, 63)) &&
                        reached.Contains(WorldTiles.Key(0, i)) && reached.Contains(WorldTiles.Key(63, i)), "the outer rim is no longer traversable: " + scenario);
                Require(world.nodes.Any(n => n.kind == NodeKind.Oil && PetrochemicalAdjacent(reached, n.x, n.y) &&
                    PetrochemicalHasPumpSpace(world, n)), "quartz closed access to a usable oil well: " + scenario);
            }
            Require(locations.Count > 5, "starter quartz uses a fixed location");
        });
    }
}
