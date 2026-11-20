using DeadCircuit;
using System.Security.Cryptography;
using System.Text;

static partial class Checks
{
    static void BiomeResourceChecks()
    {
        Run("biome resources favor desert stone wetland clay and conifer iron in generated chunks", () =>
        {
            int biomeCount = WorldGenerationCatalog.Biomes.Count;
            var area = new int[biomeCount]; var counts = new int[biomeCount, Enum.GetValues<NodeKind>().Length];
            var bankArea = new int[biomeCount, 2]; var bankClay = new int[biomeCount, 2];
            for (int cy = -108; cy <= 108; cy += 12) for (int cx = -108; cx <= 108; cx += 12)
            {
                var world = TerrainBlank(73149, WorldPreset.Wilderness, 0);
                world.generatorVersion = WorldGeneration.CurrentGeneratorVersion;
                Require(ChunkTerrain.Generate(world, cx, cy), "resource survey chunk refused");
                for (int y = cy * 16 + 2; y < cy * 16 + 14; y++)
                    for (int x = cx * 16 + 2; x < cx * 16 + 14; x++)
                        if (WorldTiles.Ground(world, x, y) != '2')
                        {
                            int biome = WorldTiles.Biome(world, x, y) - '0'; area[biome]++;
                            bankArea[biome, ResourceBank(world, x, y) ? 1 : 0]++;
                        }
                foreach (var node in world.nodes)
                {
                    int biome = WorldTiles.Biome(world, node.x, node.y) - '0'; counts[biome, (int)node.kind]++;
                    if (node.kind == NodeKind.Clay) bankClay[biome, ResourceBank(world, node.x, node.y) ? 1 : 0]++;
                }
            }
            Require(area.Take(6).All(n => n >= 150), "resource survey did not cover the six baseline biomes: " + string.Join(",", area));
            double Rate(BiomeKind biome, NodeKind kind) => counts[(int)biome, (int)kind] / (double)area[(int)biome];
            Require(Rate(BiomeKind.Desert, NodeKind.Stone) > Rate(BiomeKind.Meadow, NodeKind.Stone) * 1.7, "desert lacks a visible stone advantage");
            Require(Rate(BiomeKind.Wetland, NodeKind.Clay) > Rate(BiomeKind.ConiferousForest, NodeKind.Clay) * 2, "wetland lacks a visible clay advantage");
            Require(Rate(BiomeKind.ConiferousForest, NodeKind.Ore) > Rate(BiomeKind.Wetland, NodeKind.Ore) * 1.8, "conifer forest lacks an iron advantage");
            double expected = 0; int actual = 0, banks = 0;
            for (int biome = 0; biome < WorldGeneration.BiomeCount; biome++)
            {
                Require(bankArea[biome, 0] > 0, "missing inland survey");
                expected += bankArea[biome, 1] * bankClay[biome, 0] / (double)bankArea[biome, 0];
                actual += bankClay[biome, 1]; banks += bankArea[biome, 1];
            }
            Require(banks > 100 && actual > expected * 1.5, $"shoreline lacks a clay advantage: {actual} vs biome-adjusted {expected:0.0}, {banks} bank cells");
        });
        Run("biome resources preserve generator three through six future chunk output", () =>
        {
            var actual = new string[4];
            for (int version = 3; version <= 6; version++)
            {
                var world = TerrainBlank(-491, WorldPreset.Wilderness, 0); world.generatorVersion = version;
                foreach (var cell in new[] { (-9, 5), (11, -7), (6, 6), (-2, -3) })
                    Require(ChunkTerrain.Generate(world, cell.Item1, cell.Item2), "legacy chunk refused");
                actual[version - 3] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(TerrainMapDump(world) + TerrainEntityDump(world))));
            }
            const string old = "E45E54DBE87E10CA1A2C38C6766BFCC806EE385A329FDF3BE92FBEC9EEC7CBFF";
            Require(actual.SequenceEqual(new[] { old, old, old, "45D253F881142679182063A36F3C4D2DFA133D983B1700C5B912F9E092E9F745" }),
                "legacy generation changed: " + string.Join(",", actual));
        });
        Run("biome resources new worlds omit fixed starting supplies across seeds presets and map modes", () =>
        {
            int naturalResources = 0;
            foreach (int seed in new[] { -991, 0, 73149 }) foreach (WorldPreset preset in Enum.GetValues<WorldPreset>())
            foreach (int density in new[] { 0, 2 }) foreach (bool chunked in new[] { false, true })
            {
                var settings = new WorldGenerationSettings { seed = seed, preset = preset, cityDensity = density, chunked = chunked };
                var world = WorldGeneration.Generate(settings);
                Require(!world.nodes.Any(n => n.x >= 22 && n.x <= 42 && n.y >= 22 && n.y <= 42),
                    $"fixed starting resource remains: seed={seed}, preset={preset}, density={density}, chunked={chunked}");
                Require(world.buildings.Single(b => b.kind == BuildKind.Chest).items.All(n => n == 0), "starting chest grants free materials");
                foreach (var kind in new[] { BuildKind.Workbench, BuildKind.StoneFurnace, BuildKind.Chest })
                    Require(world.buildings.Any(b => b.kind == kind), "removing resources removed a starting facility: " + kind);
                naturalResources += world.nodes.Count(n => n.kind != NodeKind.Wall && n.kind != NodeKind.Crate);
                Require(TerrainEntityDump(world) == TerrainEntityDump(WorldGeneration.Generate(settings)), "seeded resources changed between identical generations");
            }
            Require(naturalResources > 0, "natural resources disappeared with starting supplies");
        });
        Run("biome resources preserve depleted nodes generator choice and future output through save restore", () =>
        {
            foreach (int generator in new[] { 6, 7 })
            {
                var world = WorldGeneration.Generate(new WorldGenerationSettings { chunked = true });
                int stoneId = world.nextId++;
                world.nodes.Add(new NodeState { id = stoneId, kind = NodeKind.Stone, x = 27, y = 34, stock = 1 });
                var sim = new WorldSimulator(world);
                sim.State.generatorVersion = generator;
                var p = sim.AddPlayer("biome-save", "Gatherer"); p.x = 27; p.y = 35;
                var stone = sim.State.nodes.Single(n => n.id == stoneId);
                stone.stock = 1; int before = p.items[(int)ItemKind.Stone];
                sim.Apply(Command(p, "interact", 27, 34));
                Require(p.items[(int)ItemKind.Stone] == before + 1 && sim.State.nodes.All(n => n.id != stone.id), "stone did not deplete once");
                var source = Saved(sim.State); string frozen = Dump(source); var restored = new WorldSimulator(source).State;
                Require(restored.generatorVersion == generator && restored.version == WorldSimulator.SaveVersion, "save changed the distribution or schema");
                Require(restored.nodes.All(n => n.x != 27 || n.y != 34) && !ChunkStreaming.GenerateChunk(restored, 1, 2), "save replenished a harvested chunk");
                Require(ChunkTerrain.Generate(sim.State, -9, 5) && ChunkTerrain.Generate(restored, -9, 5), "future chunk refused");
                Require(TerrainEntityDump(sim.State, -9, 5) == TerrainEntityDump(restored, -9, 5) &&
                    TerrainTileDump(sim.State, -9, 5) == TerrainTileDump(restored, -9, 5), "save rerolled future resources");
                Require(Dump(source) == frozen, "restore or future generation mutated caller save");
            }
        });
        Run("biome resources keep signed chunk geometry independent of discovery order and accessible", () =>
        {
            var first = TerrainBlank(-491); first.generatorVersion = 7;
            var second = TerrainBlank(-491); second.generatorVersion = 7;
            var cells = new[] { (-9, 5), (11, -7), (6, 6), (-2, -3), (32, 0), (-32, -32) };
            foreach (var cell in cells) Require(ChunkTerrain.Generate(first, cell.Item1, cell.Item2), "forward chunk refused");
            foreach (var cell in cells.Reverse()) Require(ChunkTerrain.Generate(second, cell.Item1, cell.Item2), "reverse chunk refused");
            Require(TerrainMapDump(first) == TerrainMapDump(second) && TerrainEntityDump(first) == TerrainEntityDump(second), "resource distribution depends on discovery order");
            foreach (var cell in cells) TerrainChunkContract(first, cell.Item1, cell.Item2);
        });
    }

    static bool ResourceBank(WorldState world, int x, int y) =>
        WorldTiles.Ground(world, x - 1, y) == '2' || WorldTiles.Ground(world, x + 1, y) == '2' ||
        WorldTiles.Ground(world, x, y - 1) == '2' || WorldTiles.Ground(world, x, y + 1) == '2';
}
