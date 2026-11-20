using DeadCircuit;

static partial class Checks
{
    // Public seams: new-world generation, sparse chunk generation and real interact commands.
    static void RunSteamResourceChecks()
    {
        Run("steam resources gather correct items with bare hands and existing pickaxes then deplete", () =>
        {
            foreach (bool chunked in new[] { false, true })
            foreach (GearKind? pick in new GearKind?[] { null, GearKind.StonePickaxe, GearKind.SteelPickaxe })
            foreach (var resource in new[] { (NodeKind.Coal, ItemKind.Coal), (NodeKind.Limestone, ItemKind.Limestone), (NodeKind.FireClay, ItemKind.FireClay) })
            {
                var world = SteamGatherWorld(resource.Item1, chunked);
                var sim = new WorldSimulator(world);
                var p = sim.AddPlayer("steam-gatherer", "Miner");
                var node = sim.State.nodes.Single(n => n.kind == resource.Item1 && n.x >= 22 && n.x <= 42 && n.y >= 22 && n.y <= 42);
                var crates = sim.State.nodes.Where(n => n.kind == NodeKind.Crate).ToDictionary(n => n.id, n => n.stock);
                node.stock = 7; p.x = node.x; p.y = node.y - 1;
                GearState tool = null;
                if (pick.HasValue)
                {
                    tool = p.gear.FirstOrDefault(g => g.kind == pick.Value);
                    if (tool == null) { tool = new GearState { id = sim.State.nextId++, kind = pick.Value }; p.gear.Add(tool); }
                    sim.Apply(Command(p, "equip", arg: tool.id));
                }
                else sim.Apply(Command(p, "switch", arg: 2));
                var before = (int[])p.items.Clone();
                int gathered = sim.State.gathered, durability = tool?.durability ?? 0;
                for (int attempt = 0; attempt < 4 && sim.State.nodes.Any(n => n.id == node.id); attempt++)
                { sim.Apply(Command(p, "interact", node.x, node.y)); Advance(sim, .6f); }
                Require(p.items[(int)resource.Item2] == before[(int)resource.Item2] + 7 && sim.State.gathered == gathered + 7,
                    $"gather did not yield exactly seven {resource.Item2} with {pick?.ToString() ?? "bare hands"}: {p.notice}");
                Require(sim.State.nodes.All(n => n.id != node.id), "depleted steam deposit survived");
                Require(Enumerable.Range(0, DemoCatalog.ItemCount).All(i => i == (int)resource.Item2 || p.items[i] == before[i]), "gather awarded an unrelated industrial item");
                Require(crates.All(c => sim.State.nodes.Any(n => n.id == c.Key && n.stock == c.Value)), "steam gather consumed a supply crate");
                Require(tool == null || tool.durability < durability, "equipped pickaxe was not used");
                Require(sim.State.era == TechEra.Primitive, "steam ore collection unexpectedly required an era unlock");
                int final = p.items[(int)resource.Item2];
                sim.Apply(Command(p, "interact", node.x, node.y));
                Require(p.items[(int)resource.Item2] == final, "empty steam deposit duplicated stock");
            }
        });
        Run("steam resources respect inventory capacity without losing remaining deposit stock", () =>
        {
            foreach (var resource in new[] { (NodeKind.Coal, ItemKind.Coal), (NodeKind.Limestone, ItemKind.Limestone), (NodeKind.FireClay, ItemKind.FireClay) })
            {
                var sim = new WorldSimulator(SteamGatherWorld(resource.Item1, false));
                var p = sim.AddPlayer("steam-cap", "Miner");
                var node = sim.State.nodes.Single(n => n.kind == resource.Item1 && n.x == 37 && n.y == 35);
                node.stock = 3; p.x = node.x; p.y = node.y - 1; p.activeSlot = 2;
                p.items[(int)resource.Item2] = WorldSimulator.InventoryLimit - 1;
                sim.Apply(Command(p, "interact", node.x, node.y));
                Require(p.items[(int)resource.Item2] == WorldSimulator.InventoryLimit && node.stock == 2, "capacity overflow lost steam resource stock");
                Advance(sim, .6f); sim.Apply(Command(p, "interact", node.x, node.y));
                Require(node.stock == 2 && p.items[(int)resource.Item2] == WorldSimulator.InventoryLimit, "full inventory still consumed steam resource stock");
            }
        });
        Run("steam resources favor coal forests and tundra limestone valleys and mountains and fire clay wetlands", () =>
        {
            int biomeCount = WorldGenerationCatalog.Biomes.Count;
            var area = new int[biomeCount]; var counts = new int[biomeCount, Enum.GetValues<NodeKind>().Length];
            foreach (var rule in BiomeResourceCatalog.All)
                Require(rule.coalPermille > 0 && rule.limestonePermille > 0 && rule.fireClayPermille > 0, "natural biome lacks background steam resources: " + rule.kind);
            for (int cy = -132; cy <= 132; cy += 12) for (int cx = -132; cx <= 132; cx += 12)
            {
                var world = TerrainBlank(73149, WorldPreset.Wilderness, 0); world.generatorVersion = WorldGeneration.CurrentGeneratorVersion;
                Require(ChunkTerrain.Generate(world, cx, cy), "steam resource survey chunk refused");
                for (int y = cy * 16 + 2; y < cy * 16 + 14; y++)
                    for (int x = cx * 16 + 2; x < cx * 16 + 14; x++)
                        if (WorldTiles.Ground(world, x, y) != '2') area[WorldTiles.Biome(world, x, y) - '0']++;
                foreach (var node in world.nodes)
                {
                    counts[WorldTiles.Biome(world, node.x, node.y) - '0', (int)node.kind]++;
                    if (node.kind == NodeKind.Coal || node.kind == NodeKind.Limestone || node.kind == NodeKind.FireClay)
                        Require(node.stock >= 60 && node.stock <= 160 && EquipmentCatalog.UsesPickaxe(node.kind), "natural deposit stock/tool classification invalid");
                }
                SteamResourceChunkAccess(world, cx, cy);
            }
            Require(area.All(n => n >= 150), "steam survey missed a natural biome: " + string.Join(",", area));
            double Rate(BiomeKind biome, NodeKind kind) => counts[(int)biome, (int)kind] / (double)area[(int)biome];
            foreach (var biome in new[] { BiomeKind.ConiferousForest, BiomeKind.Tundra })
                Require(Rate(biome, NodeKind.Coal) > Rate(BiomeKind.Meadow, NodeKind.Coal) * 2, "coal preference missing in " + biome);
            foreach (var biome in new[] { BiomeKind.RiverValley, BiomeKind.Desert, BiomeKind.SnowMountain })
                Require(Rate(biome, NodeKind.Limestone) > Rate(BiomeKind.DeciduousForest, NodeKind.Limestone) * 2, "limestone preference missing in " + biome);
            foreach (var biome in new[] { BiomeKind.Wetland, BiomeKind.Swamp, BiomeKind.RiverValley })
                Require(Rate(biome, NodeKind.FireClay) > Rate(BiomeKind.Meadow, NodeKind.FireClay) * 2, "fire clay preference missing in " + biome);
            foreach (BiomeKind biome in Enum.GetValues<BiomeKind>())
                foreach (NodeKind kind in new[] { NodeKind.Coal, NodeKind.Limestone, NodeKind.FireClay })
                    Require(counts[(int)biome, (int)kind] > 0, $"survey lacks background {kind} in {biome}");
        });
        Run("steam resources reproduce initial worlds and signed future chunks regardless of discovery order", () =>
        {
            foreach (int seed in new[] { int.MinValue, -991, 0, 73149, int.MaxValue })
            {
                var settings = new WorldGenerationSettings { seed = seed, chunked = true };
                var first = WorldGeneration.Generate(settings); var second = WorldGeneration.Generate(settings);
                Require(TerrainMapDump(first) == TerrainMapDump(second) && TerrainEntityDump(first) == TerrainEntityDump(second), "same seed rerolled initial steam resources");
                var chunks = new[] { (-9, 5), (11, -7), (6, 6), (-2, -3), (32, 0), (-32, -32) };
                foreach (var cell in chunks) Require(ChunkTerrain.Generate(first, cell.Item1, cell.Item2), "forward steam chunk refused");
                foreach (var cell in chunks.Reverse()) Require(ChunkTerrain.Generate(second, cell.Item1, cell.Item2), "reverse steam chunk refused");
                Require(TerrainMapDump(first) == TerrainMapDump(second) && TerrainEntityDump(first) == TerrainEntityDump(second), "steam resources depend on discovery order");
                foreach (var cell in chunks) SteamResourceChunkAccess(first, cell.Item1, cell.Item2);
            }
        });
        Run("steam resources use the same natural deposits in initial maps and later chunks", () =>
        {
            var matched = new int[3];
            foreach (int seed in new[] { -991, 0, 73149, 18417 })
            {
                var initial = WorldGeneration.Generate(new WorldGenerationSettings { seed = seed, preset = WorldPreset.Wilderness, cityDensity = 0 });
                var chunks = TerrainBlank(seed, WorldPreset.Wilderness, 0); chunks.generatorVersion = WorldGeneration.CurrentGeneratorVersion;
                for (int cy = 0; cy < 4; cy++) for (int cx = 0; cx < 4; cx++)
                    Require(ChunkTerrain.Generate(chunks, cx, cy), "shared-rule comparison chunk refused");
                var generated = chunks.nodes.ToDictionary(n => WorldTiles.Key(n.x, n.y));
                foreach (var node in initial.nodes.Where(n => n.kind == NodeKind.Coal || n.kind == NodeKind.Limestone || n.kind == NodeKind.FireClay))
                {
                    // Tutorial floors, settlement streets and access reservations
                    // differ; compare sampled deposits where both maps kept one.
                    char ground = WorldTiles.Ground(initial, node.x, node.y);
                    if (ground != '0' && ground != '1' || WorldTiles.Ground(chunks, node.x, node.y) != ground ||
                        ResourceBank(initial, node.x, node.y) != ResourceBank(chunks, node.x, node.y) ||
                        !generated.TryGetValue(WorldTiles.Key(node.x, node.y), out var other)) continue;
                    Require(WorldTiles.Biome(initial, node.x, node.y) == WorldTiles.Biome(chunks, node.x, node.y) &&
                        other.kind == node.kind && other.stock == node.stock, "initial and later chunk resource sampling diverged");
                    matched[(int)node.kind - (int)NodeKind.Coal]++;
                }
            }
            Require(matched.All(n => n > 0), "shared sampler comparison missed a steam resource: " + string.Join(",", matched));
        });
        Run("steam resources gather all three naturally generated future chunk materials", () =>
        {
            var world = WorldGeneration.Generate(new WorldGenerationSettings { chunked = true, preset = WorldPreset.Wilderness, cityDensity = 0 });
            var resources = new[] { (NodeKind.Coal, ItemKind.Coal), (NodeKind.Limestone, ItemKind.Limestone), (NodeKind.FireClay, ItemKind.FireClay) };
            bool Future(NodeState n) => n.x < 0 || n.y < 0 || n.x >= 64 || n.y >= 64;
            for (int cy = -10; cy <= 10 && !resources.All(r => world.nodes.Any(n => Future(n) && n.kind == r.Item1)); cy += 2)
                for (int cx = -10; cx <= 10 && !resources.All(r => world.nodes.Any(n => Future(n) && n.kind == r.Item1)); cx += 2)
                    Require(ChunkTerrain.Generate(world, cx, cy), "future gather fixture chunk refused");
            var sim = new WorldSimulator(world); var p = sim.AddPlayer("steam-natural", "Miner");
            var pick = new GearState { id = sim.State.nextId++, kind = GearKind.StonePickaxe }; p.gear.Add(pick);
            sim.Apply(Command(p, "equip", arg: pick.id));
            foreach (var resource in resources)
            {
                var node = sim.State.nodes.First(n => Future(n) && n.kind == resource.Item1);
                int cx = WorldTiles.FloorDiv(node.x, 16), cy = WorldTiles.FloorDiv(node.y, 16);
                var blocked = sim.State.nodes.Select(n => WorldTiles.Key(n.x, n.y)).ToHashSet();
                foreach (var b in sim.State.buildings.Where(b => b.kind != BuildKind.Door))
                    for (int y = b.y; y < b.y + DemoCatalog.Height(b); y++)
                        for (int x = b.x; x < b.x + DemoCatalog.Width(b); x++) blocked.Add(WorldTiles.Key(x, y));
                var reachable = TerrainFlood(sim.State, cx, cy, blocked);
                var approach = new[] { (node.x - 1, node.y), (node.x + 1, node.y), (node.x, node.y - 1), (node.x, node.y + 1) }
                    .First(c => reachable.Contains(WorldTiles.Key(c.Item1, c.Item2)));
                p.x = approach.Item1; p.y = approach.Item2;
                int before = p.items[(int)resource.Item2], stock = node.stock;
                sim.Apply(Command(p, "interact", node.x, node.y));
                int received = p.items[(int)resource.Item2] - before;
                Require(received > 0 && node.stock == stock - received && p.notice.Contains(DemoCatalog.ItemName(resource.Item2)), "natural future deposit did not yield " + resource.Item2);
                Advance(sim, .6f);
            }
        });
    }

    // Harvesting fixtures provide a deposit explicitly; new worlds no longer grant one.
    static WorldState SteamGatherWorld(NodeKind kind, bool chunked)
    {
        var world = WorldGeneration.Generate(new WorldGenerationSettings { chunked = chunked });
        world.nodes.Add(new NodeState { id = world.nextId++, kind = kind, x = 37, y = 35, stock = 7 });
        return world;
    }

    static void SteamResourceChunkAccess(WorldState world, int cx, int cy)
    {
        var nodes = world.nodes.Where(n => WorldTiles.FloorDiv(n.x, 16) == cx && WorldTiles.FloorDiv(n.y, 16) == cy).ToArray();
        Require(nodes.Length <= ChunkTerrain.MaxNodesPerChunk && nodes.Count(n => n.kind != NodeKind.Wall && n.kind != NodeKind.Crate) <= ChunkTerrain.MaxResourcesPerChunk,
            "steam resources exceeded chunk entity budgets");
        var blocked = nodes.Select(n => WorldTiles.Key(n.x, n.y)).ToHashSet();
        foreach (var b in world.buildings.Where(b => b.kind != BuildKind.Door))
            for (int y = b.y; y < b.y + DemoCatalog.Height(b); y++)
                for (int x = b.x; x < b.x + DemoCatalog.Width(b); x++) blocked.Add(WorldTiles.Key(x, y));
        var reachable = TerrainFlood(world, cx, cy, blocked);
        foreach (var node in nodes.Where(n => n.kind == NodeKind.Coal || n.kind == NodeKind.Limestone || n.kind == NodeKind.FireClay))
        {
            Require(node.x >= cx * 16 + 2 && node.x < cx * 16 + 14 && node.y >= cy * 16 + 2 && node.y < cy * 16 + 14, "steam resource obstructs the chunk boundary corridor");
            Require(WorldTiles.Ground(world, node.x, node.y) != '2' && TerrainNeighbor(reachable, node.x, node.y), "natural steam resource cannot be gathered from the chunk boundary");
        }
    }
}
