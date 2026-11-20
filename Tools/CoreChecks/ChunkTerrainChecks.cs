using System;
using System.Collections.Generic;
using System.Linq;
using DeadCircuit;

static partial class Checks
{
    // Public seams: tile queries, sparse terrain commits and one-chunk streaming.
    static void ChunkTerrainChecks()
    {
        Run("sparse chunk site budget refuses atomically before exceeding save limit", () =>
        {
            var chosen=TerrainFindSite();var world=TerrainBlank(73149,WorldPreset.Urban,2);
            var existing=new SiteState();for(int i=0;i<32768;i++) world.sites.Add(existing);
            int next=world.nextId;
            Require(!ChunkStreaming.GenerateChunk(world,chosen.Item1,chosen.Item2),"site limit overflow accepted");
            Require(world.sites.Count==32768 && world.nodes.Count==0 && world.buildings.Count==0 && world.nextId==next && world.chunks.records.Count==0 && world.chunks.revision==0,"site refusal partially committed");
        });
        Run("sparse chunk terrain accepts negative coordinates without resizing legacy maps", () =>
        {
            var world = TerrainBlank(73149);
            string ground = world.ground, biomes = world.biomes;
            Require(ChunkTerrain.Generate(world, -1, -1), "first generation was refused");
            Require(world.width == 64 && world.height == 64 && world.ground == ground && world.biomes == biomes, "sparse generation changed legacy maps");
            Require(world.chunks.records.Count == 1 && world.chunks.generated == "" && world.chunks.revision == 1, "generation did not append exactly one sparse record");
            for (int y = -16; y < 0; y++) for (int x = -16; x < 0; x++)
            {
                Require(WorldTiles.Ground(world, x, y) >= '0' && WorldTiles.Ground(world, x, y) <= '4', "ground has unknown local cells");
                Require(WorldTiles.Biome(world, x, y) >= '0' && WorldTiles.Biome(world, x, y) < '0' + WorldGeneration.BiomeCount, "biome has unknown local cells");
            }
            Require(WorldTiles.Ground(world, -17, -1) == '?' && WorldTiles.Ground(world, 0, 0) == '?', "generation filled an unrequested neighbor");
            TerrainChunkContract(world, -1, -1);
        });
        Run("sparse chunk coordinate keys roundtrip every signed quadrant and floor negative divisions", () =>
        {
            var keys = new HashSet<long>();
            int[] values = { int.MinValue, -1048576, -65536, -17, -1, 0, 1, 16, 1048575, int.MaxValue };
            foreach (int x in values) foreach (int y in values)
            {
                long key = WorldTiles.Key(x, y);
                Require(keys.Add(key) && WorldTiles.X(key) == x && WorldTiles.Y(key) == y, "coordinate key collision or signed decode loss");
            }
            Require(WorldTiles.FloorDiv(-1, 16) == -1 && WorldTiles.FloorDiv(-16, 16) == -1 && WorldTiles.FloorDiv(-17, 16) == -2, "negative division truncated toward zero");
            Require(WorldTiles.FloorDiv(15, 16) == 0 && WorldTiles.FloorDiv(16, 16) == 1 && WorldTiles.FloorDiv(int.MinValue, 16) == -134217728, "floor division boundary");
            Require(WorldTiles.FloorDiv(-1, 32) == -1 && WorldTiles.FloorDiv(-33, 32) == -2, "street lattice truncates negatives");
            TerrainThrows<ArgumentOutOfRangeException>(() => WorldTiles.FloorDiv(1, 0));
        });
        Run("sparse chunk tile queries distinguish legacy maps from unknown sparse records", () =>
        {
            var world = TerrainBlank(11);
            Require(WorldTiles.Inside(world, -1048576, -1048576) && WorldTiles.Inside(world, 1048575, 1048575), "global legal boundary rejected");
            Require(!WorldTiles.Inside(world, -1048577, 0) && !WorldTiles.Inside(world, 1048576, 0) && !WorldTiles.Inside(null, 0, 0), "global invalid boundary accepted");
            Require(WorldTiles.Ground(world, 1, 1) == '?' && WorldTiles.Biome(world, -1, -1) == '?' && !ChunkStreaming.IsGenerated(world, 1, 1), "legacy bytes leaked into sparse unknown cells");
            world.chunks.enabled = false;
            Require(WorldTiles.Ground(world, 1, 1) == '0' && WorldTiles.Biome(world, 1, 1) == '0', "legacy tile lookup changed");
            Require(WorldTiles.Inside(world, 63, 63) && !WorldTiles.Inside(world, 64, 0) && !WorldTiles.Inside(world, -1, 0), "legacy bounds expanded");
            Require(ChunkStreaming.IsGenerated(world, 1, 1) && ChunkStreaming.IsActive(world, 1, 1), "legacy streaming behavior changed");
            Require(!ChunkStreaming.Step(world), "legacy world streamed a new chunk");
            TerrainThrows<ArgumentException>(() => ChunkStreaming.GenerateChunk(world, 4, 4));
        });
        Run("sparse chunk creation uses normal terrain and entities for the sixteen initial chunks", () =>
        {
            foreach (var options in new[] {
                new WorldGenerationSettings { seed = 11, chunked = true },
                new WorldGenerationSettings { seed = -991, preset = WorldPreset.Urban, cityDensity = 2, chunked = true }
            })
            {
                var world = WorldGeneration.Generate(options);
                Require(world.width == 64 && world.height == 64 && world.ground.Length == 4096 && world.biomes.Length == 4096, "bounded save metadata changed");
                Require(world.version == WorldSimulator.SaveVersion && world.generatorVersion == WorldGeneration.CurrentGeneratorVersion && world.generation.chunked && world.chunks.enabled, "new world version contract");
                Require(world.players.Count == 0 && world.chunks.records.Count == 16 && world.chunks.generated == "", "expected exactly sixteen initial sparse records and no survivors");
                var streamed = TerrainBlank(world.seed, options.preset, options.cityDensity);
                streamed.generatorVersion = WorldGeneration.CurrentGeneratorVersion;
                TerrainPopulate(streamed, reverse: true, minX: 0, maxX: 4, minY: 0, maxY: 4);
                var normalQuartz = streamed.nodes.Where(n => n.kind == NodeKind.Quartz).Select(n => (n.x, n.y)).ToHashSet();
                var openingQuartz = world.nodes.Where(n => n.kind == NodeKind.Quartz && !normalQuartz.Contains((n.x, n.y))).ToArray();
                Require(openingQuartz.Length <= 1, "more than one opening quartz reserve was added");
                if (openingQuartz.Length == 1)
                {
                    var n = openingQuartz[0];
                    var chunk = streamed.chunks.records.Single(c => c.x == n.x / 16 && c.y == n.y / 16);
                    int index = n.x % 16 + n.y % 16 * 16;
                    if (chunk.ground[index] == '2')
                    {
                        var tiles = chunk.ground.ToCharArray();
                        tiles[index] = '1';
                        chunk.ground = new string(tiles);
                    }
                    streamed.nodes.Add(n);
                }
                Require(TerrainMapDump(world) == TerrainMapDump(streamed) && TerrainEntityDump(world) == TerrainEntityDump(streamed),
                    $"seed {world.seed} initial chunks differ from normally streamed terrain or entities");
                Require(!ChunkStreaming.IsGenerated(world, -1, 35) && !ChunkStreaming.IsGenerated(world, 64, 35), "creation eagerly generated outside the initial region");
            }
        });
        Run("sparse chunk generation reaches all four directions beyond 256 and the global edge chunks", () =>
        {
            var world = TerrainBlank(73149, WorldPreset.Urban, 2);
            var chunks = new[] { (-1000, 0), (1000, 0), (0, -1000), (0, 1000), (-65536, -65536), (65535, 65535), (-65536, 65535), (65535, -65536) };
            foreach (var c in chunks)
            {
                Require(ChunkStreaming.GenerateChunk(world, c.Item1, c.Item2), "legal distant chunk refused");
                Require(ChunkStreaming.IsGenerated(world, c.Item1 * 16, c.Item2 * 16) && ChunkStreaming.IsGenerated(world, c.Item1 * 16 + 15, c.Item2 * 16 + 15), "distant chunk lookup failed");
                TerrainChunkContract(world, c.Item1, c.Item2);
            }
            Require(world.chunks.records.Count == chunks.Length && world.ground.Length == 4096, "distant query allocated a rectangular map");
            string before = TerrainSerialize(world);
            TerrainThrows<ArgumentOutOfRangeException>(() => ChunkStreaming.GenerateChunk(world, 65536, 0));
            TerrainThrows<ArgumentOutOfRangeException>(() => ChunkStreaming.GenerateChunk(world, -65537, 0));
            TerrainThrows<ArgumentOutOfRangeException>(() => ChunkStreaming.GenerateChunk(world, int.MaxValue, int.MinValue));
            Require(TerrainSerialize(world) == before, "invalid chunk request mutated world");
        });
        Run("sparse chunk Step selects nearest missing chunk across all connected living players", () =>
        {
            var world = TerrainBlank(11);
            world.players.Add(new PlayerState { id = "first", x = 80, y = 80 });
            world.players.Add(new PlayerState { id = "second", x = -112000, y = 144000 });
            world.players.Add(new PlayerState { id = "offline", x = -900000, y = -900000, connected = false });
            world.players.Add(new PlayerState { id = "dead", x = 900000, y = -900000, hp = 0 });
            ChunkStreaming.GenerateChunk(world, 5, 5);
            int before = world.chunks.records.Count;
            Require(ChunkStreaming.Step(world) && world.chunks.records.Count == before + 1, "Step did not commit exactly one chunk");
            Require(WorldTiles.TryChunk(world, -7000, 9000, out _), "first player's farther missing neighbor outranked second player's center");
            Require(!ChunkStreaming.IsGenerated(world, -900000, -900000) && !ChunkStreaming.IsGenerated(world, 900000, -900000), "offline/dead player generated terrain");
            Require(ChunkStreaming.IsActive(world, -112000, 144000) && !ChunkStreaming.IsActive(world, -900000, -900000), "active filtering ignored connection/life state");
            var reversed = TerrainBlank(11);
            reversed.players.AddRange(world.players.AsEnumerable().Reverse());
            ChunkStreaming.GenerateChunk(reversed, 5, 5);
            ChunkStreaming.Step(reversed);
            Require(TerrainMapDump(reversed) == TerrainMapDump(world), "player enumeration order changed nearest chunk");
        });
        Run("sparse chunk Step fills only radius three around a signed player cell", () =>
        {
            var world = TerrainBlank(11);
            var player = new PlayerState { x = -.6f, y = -.6f };
            world.players.Add(player);
            for (int i = 0; i < 49; i++)
            {
                int before = world.chunks.records.Count;
                Require(ChunkStreaming.Step(world) && world.chunks.records.Count == before + 1, "stream tick generated zero or multiple chunks");
                if (i == 0) Require(WorldTiles.TryChunk(world, -1, -1, out _), "negative player center rounded into positive chunk");
            }
            Require(!ChunkStreaming.Step(world) && world.chunks.records.All(c => c.x >= -4 && c.x <= 2 && c.y >= -4 && c.y <= 2), "streaming exceeded radius three");
            Require(!ChunkStreaming.IsGenerated(world, 48, 0) && ChunkStreaming.IsActive(world, -64, -64), "radius edge classification");
            player.connected = false;
            Require(!ChunkStreaming.Step(world) && !ChunkStreaming.IsActive(world, -1, -1), "offline player kept streaming or activation");
            player.connected = true; player.hp = 0;
            Require(!ChunkStreaming.Step(world), "dead player kept streaming");
            player.hp = 100; player.x = float.NaN;
            Require(!ChunkStreaming.Step(world), "non-finite player generated a chunk");
        });
        Run("sparse chunk records cache rebuilds after JSON roundtrip and list growth", () =>
        {
            var world = TerrainBlank(73149);
            ChunkStreaming.GenerateChunk(world, -7, 12);
            char tile = WorldTiles.Ground(world, -112, 192);
            string json = TerrainSerialize(world);
            Require(!json.Contains("indexedCount") && !json.Contains("indexSource") && !json.Contains("\"index\""), "lookup cache leaked into serialized state");
            var restored = System.Text.Json.JsonSerializer.Deserialize<WorldState>(json, new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
            Require(WorldTiles.Ground(restored, -112, 192) == tile && WorldTiles.TryChunk(restored, -7, 12, out _), "restored cache failed to rebuild");
            restored.chunks.records.Add(new WorldChunkState { x = 10000, y = -10000, ground = new string('4', 256), biomes = new string('5', 256) });
            Require(WorldTiles.Ground(restored, 160015, -159985) == '4' && WorldTiles.Biome(restored, 160015, -159985) == '5', "appended records were absent from a warm cache");
            restored.chunks.records = new List<WorldChunkState> { new WorldChunkState { x = -1, y = -1, ground = new string('1', 256), biomes = new string('2', 256) } };
            Require(WorldTiles.Ground(restored, -1, -1) == '1' && WorldTiles.Ground(restored, -112, 192) == '?', "replaced record list retained stale cache entries");
            Require(world.chunks.records.Count == 1, "restored chunk list aliased source");
        });
        Run("sparse chunk revisits preserve mined resources machines records and dynamic IDs", () =>
        {
            var world = TerrainBlank(73149, WorldPreset.Urban, 2);
            var chosen = TerrainFindSite();
            ChunkStreaming.GenerateChunk(world, chosen.Item1, chosen.Item2);
            Require(world.nodes.Count > 0, "fixture has no resources");
            world.nodes.RemoveAt(0);
            var door = world.buildings[0]; door.setting = 1; door.hp = 71; door.items[0] = 17;
            string before = TerrainSerialize(world);
            Require(!ChunkStreaming.GenerateChunk(world, chosen.Item1, chosen.Item2) && !ChunkTerrain.Generate(world, chosen.Item1, chosen.Item2), "duplicate generation was not idempotent");
            Require(TerrainSerialize(world) == before, "revisit replenished resources or reset machines/IDs");
        });
        Run("sparse chunk world coordinates retain smooth seeded water and all ten biomes", () =>
        {
            var world = TerrainBlank(73149); world.generatorVersion = WorldGeneration.CurrentGeneratorVersion; TerrainPopulate(world);
            var biomes = new HashSet<char>();
            int water = 0, land = 0, seamChanges = 0, seamPairs = 0, innerChanges = 0, innerPairs = 0;
            for (int y = -128; y < 128; y++) for (int x = -128; x < 128; x++)
            {
                // Broad biome coverage uses sparse world coordinates; continuity stays local.
                biomes.Add((char)('0' + (int)ChunkTerrain.SampleBiome(world.seed, x * 6, y * 6)));
                if (WorldTiles.Ground(world, x, y) == '2') water++; else land++;
                foreach (var step in new[] { (1, 0), (0, 1) })
                {
                    int nx = x + step.Item1, ny = y + step.Item2;
                    if (nx == 128 || ny == 128) continue;
                    bool seam = step.Item1 == 1 ? nx % 16 == 0 : ny % 16 == 0;
                    bool change = WorldTiles.Biome(world, x, y) != WorldTiles.Biome(world, nx, ny);
                    if (seam) { seamPairs++; if (change) seamChanges++; }
                    else { innerPairs++; if (change) innerChanges++; }
                }
            }
            Require(water > 1000 && land > 10000 && biomes.Count == WorldGeneration.BiomeCount, "local terrain lacks water/land or sparse world survey misses biomes: " + string.Join(",", biomes.OrderBy(c => c)));
            Require((double)innerChanges / innerPairs < .18, "biomes became per-cell random patches");
            Require((double)seamChanges / seamPairs <= (double)innerChanges / innerPairs * 1.8 + .015, "signed chunk seams concentrate biome discontinuities");
            var other = TerrainBlank(-73149); other.generatorVersion = WorldGeneration.CurrentGeneratorVersion; ChunkTerrain.Generate(other, -2, -2);
            Require(TerrainTileDump(world, -2, -2) != TerrainTileDump(other, -2, -2), "seed changes metadata only");
            Console.WriteLine($"  signed terrain sample: {water} water, {land} land, {biomes.Count} biomes");
        });
        Run("sparse chunk sites have reachable doors enclosed rooms containers and local budgets", () =>
        {
            var world = TerrainBlank(73149, WorldPreset.Urban, 2);
            TerrainPopulate(world, false, -16, 16, -12, 12);
            Require(world.sites.Count > 30 && world.sites.Select(s => s.kind).Distinct().Count() == 12, "sparse world lacks varied sites");
            foreach (var kind in new[] { NodeKind.Tree, NodeKind.Ore, NodeKind.CopperOre, NodeKind.TinOre, NodeKind.UraniumOre })
                Require(world.nodes.Any(n => n.kind == kind), "missing vegetation/mineral: " + kind);
            foreach (var c in world.chunks.records) TerrainChunkContract(world, c.x, c.y);
            Console.WriteLine($"  sparse sites sample: {world.chunks.records.Count} records, {world.nodes.Count} nodes, {world.sites.Count} sites");
        });
        Run("sparse chunk visitation order preserves geometry while allocating sequential dynamic IDs", () =>
        {
            foreach (int seed in new[] { 73149, int.MinValue, int.MaxValue, 0, -1 })
            {
                var a = TerrainBlank(seed, WorldPreset.Urban, 2); var b = TerrainBlank(seed, WorldPreset.Urban, 2);
                var chunks = new[] { (-12, -12), (-1, -1), (0, 0), (1, 0), (-1, 0), (0, -1), (5, 9), (9000, -10000), (-40000, 50000), (65535, -65536) };
                foreach (var c in chunks) ChunkStreaming.GenerateChunk(a, c.Item1, c.Item2);
                foreach (var c in chunks.Reverse()) ChunkStreaming.GenerateChunk(b, c.Item1, c.Item2);
                Require(TerrainMapDump(a) == TerrainMapDump(b) && TerrainEntityDump(a) == TerrainEntityDump(b), "visitation order or signed seed changed geometry/resources");
                var ids = a.nodes.Select(n => n.id).Concat(a.buildings.Select(n => n.id)).Concat(a.sites.Select(n => n.id)).OrderBy(id => id).ToArray();
                Require(ids.SequenceEqual(Enumerable.Range(10000000, ids.Length)) && a.nextId == 10000000 + ids.Length, "generated entities did not consume sequential nextId");
                foreach (var c in chunks) TerrainChunkContract(a, c.Item1, c.Item2);
            }
        });
        Run("sparse current generator biome geometry is invariant to signed chunk visit order", () =>
        {
            var first = TerrainBlank(-491, WorldPreset.Wilderness, 0); first.generatorVersion = WorldGeneration.CurrentGeneratorVersion;
            var second = TerrainBlank(-491, WorldPreset.Wilderness, 0); second.generatorVersion = WorldGeneration.CurrentGeneratorVersion;
            var cells = new[] { (-9, 5), (11, -7), (6, 6), (-2, -3), (32, 0), (-32, -32) };
            foreach (var cell in cells) Require(ChunkTerrain.Generate(first, cell.Item1, cell.Item2), "forward current chunk refused");
            foreach (var cell in cells.Reverse()) Require(ChunkTerrain.Generate(second, cell.Item1, cell.Item2), "reverse current chunk refused");
            Require(TerrainMapDump(first) == TerrainMapDump(second) && TerrainEntityDump(first) == TerrainEntityDump(second),
                "current generator biome geometry/resources depend on chunk visit order");
        });
        Run("sparse chunk terrain ignores unknown and edited neighbors", () =>
        {
            var isolated = TerrainBlank(11); var surrounded = TerrainBlank(11);
            ChunkTerrain.Generate(isolated, -4, -5);
            foreach (var c in new[] { (-5, -6), (-4, -6), (-3, -6), (-5, -5), (-3, -5), (-5, -4), (-4, -4), (-3, -4) })
                ChunkTerrain.Generate(surrounded, c.Item1, c.Item2);
            WorldTiles.TryChunk(surrounded, -5, -5, out var neighbor);
            neighbor.ground = new string('2', 256);
            string edited = neighbor.ground;
            ChunkTerrain.Generate(surrounded, -4, -5);
            Require(TerrainTileDump(isolated, -4, -5) == TerrainTileDump(surrounded, -4, -5) && TerrainEntityDump(isolated, -4, -5) == TerrainEntityDump(surrounded, -4, -5), "neighbor load/edit state changed chunk geometry");
            Require(neighbor.ground == edited, "generation repainted an edited neighbor");
        });
        Run("sparse chunk generation preserves unrelated objects above the old 24000 node cap", () =>
        {
            var reference = TerrainBlank(73149, WorldPreset.Urban, 2); var world = TerrainBlank(73149, WorldPreset.Urban, 2);
            for (int i = 0; i < 24000; i++) world.nodes.Add(new NodeState { id = i + 1, x = 150000, y = 150000, stock = 77, hp = 13 });
            var machine = new BuildingState { id = 30000, kind = BuildKind.Chest, x = 150001, y = 150000, hp = 71, progress = .25f };
            machine.items[0] = 91; world.buildings.Add(machine);
            world.sites.Add(new SiteState { id = 30001, x = 150000, y = 150000, width = 5, height = 5 });
            var nodes = world.nodes; var buildings = world.buildings; var sites = world.sites; var chunks = world.chunks; var settings = world.generation;
            string oldNodes = TerrainSerialize(nodes), oldBuildings = TerrainSerialize(buildings), oldSites = TerrainSerialize(sites), oldSettings = TerrainSerialize(settings);
            var chosen = TerrainFindSite();
            ChunkTerrain.Generate(reference, chosen.Item1, chosen.Item2); ChunkTerrain.Generate(world, chosen.Item1, chosen.Item2);
            Require(TerrainTileDump(reference, chosen.Item1, chosen.Item2) == TerrainTileDump(world, chosen.Item1, chosen.Item2) && TerrainEntityDump(reference, chosen.Item1, chosen.Item2) == TerrainEntityDump(world, chosen.Item1, chosen.Item2), "old global cap truncated a new chunk");
            Require(ReferenceEquals(nodes, world.nodes) && ReferenceEquals(buildings, world.buildings) && ReferenceEquals(sites, world.sites) && ReferenceEquals(chunks, world.chunks) && ReferenceEquals(settings, world.generation), "generator replaced existing lists/metadata");
            Require(TerrainSerialize(world.nodes.Take(24000)) == oldNodes && TerrainSerialize(world.buildings.Take(1)) == oldBuildings && TerrainSerialize(world.sites.Take(1)) == oldSites && TerrainSerialize(world.generation) == oldSettings, "generator mutated unrelated objects/settings");
            Require(ReferenceEquals(machine, world.buildings[0]) && world.nextId == reference.nextId, "existing machine identity or ID allocation changed");
        });
        Run("sparse chunk city density changes sites without negative half-plane roads", () =>
        {
            string climate = null;
            int[] counts = new int[3];
            foreach (WorldPreset preset in Enum.GetValues(typeof(WorldPreset)))
            {
                int previous = -1;
                for (int density = 0; density <= 2; density++)
                {
                    var world = TerrainBlank(73149, preset, density); TerrainPopulate(world);
                    string currentClimate = string.Concat(world.chunks.records.OrderBy(c => c.y).ThenBy(c => c.x).Select(c => c.biomes));
                    if (climate == null) climate = currentClimate;
                    Require(currentClimate == climate, "city settings changed natural climate");
                    Require(world.sites.Count > previous, "higher density did not increase actual sites");
                    if (density == 0) Require(world.sites.All(s => s.district == DistrictKind.Wilderness), "zero density created a city district");
                    previous = world.sites.Count;
                    if (density == 1) counts[(int)preset] = previous;
                    int roads = 0;
                    for (int y = -128; y < 0; y++) for (int x = -128; x < 0; x++) if (WorldTiles.Ground(world, x, y) == '3') roads++;
                    Require(roads < 128 * 128 / 3, "signed modulo painted negative half-plane as road");
                }
            }
            Require(counts[(int)WorldPreset.Wilderness] < counts[(int)WorldPreset.Balanced] && counts[(int)WorldPreset.Balanced] < counts[(int)WorldPreset.Urban], "presets change labels only");
        });
        Run("sparse chunk legacy versions keep the arterial and current worlds follow settlement roads", () =>
        {
            foreach (int version in new[] { 3, 9, WorldGeneration.CurrentGeneratorVersion })
            {
                var world = TerrainBlank(73149, WorldPreset.Wilderness, 0);
                world.generatorVersion = version;
                TerrainPopulate(world, minX: 2, maxX: 3, minY: -8, maxY: 8);
                bool continuousArterial = Enumerable.Range(-128, 256).All(y =>
                    WorldTiles.Ground(world, 32, y) == '3' && WorldTiles.Ground(world, 33, y) == '3');
                Require(version < 10 ? continuousArterial : !continuousArterial, $"generator {version} has wrong fixed arterial behavior");
            }
        });
        Run("sparse chunk record budget refuses a whole chunk without an empty record", () =>
        {
            var world = TerrainBlank(11);
            string ground = new string('0', 256), biomes = new string('0', 256);
            for (int i = 0; i < 65536; i++) world.chunks.records.Add(new WorldChunkState { x = i - 32768, y = 1000, ground = ground, biomes = biomes });
            int next = world.nextId;
            Require(!ChunkStreaming.GenerateChunk(world, 0, -1000), "record budget accepted another record");
            Require(world.chunks.records.Count == 65536 && world.chunks.revision == 0 && world.nextId == next && world.nodes.Count == 0 && world.buildings.Count == 0 && world.sites.Count == 0 && !ChunkStreaming.IsGenerated(world, 0, -16000), "record refusal partially mutated world");
        });
        Run("sparse chunk node budget rejects whole geometry then accepts an exact fit", () =>
        {
            var chosen = TerrainFindSite();
            var reference = TerrainBlank(73149, WorldPreset.Urban, 2);
            ChunkStreaming.GenerateChunk(reference, chosen.Item1, chosen.Item2);
            Require(reference.nodes.Count > 0, "budget fixture lacks nodes");
            var world = TerrainBlank(73149, WorldPreset.Urban, 2);
            // A shared distant sentinel keeps this count-budget fixture lightweight.
            var sentinel = new NodeState { x = 500000, y = 500000, id = 1 };
            world.nodes.AddRange(Enumerable.Repeat(sentinel, WorldSimulator.MaxNodes - reference.nodes.Count + 1));
            int before = world.nodes.Count, next = world.nextId;
            Require(!ChunkStreaming.GenerateChunk(world, chosen.Item1, chosen.Item2), "node budget truncated/accepted oversized chunk");
            Require(world.nodes.Count == before && world.buildings.Count == 0 && world.sites.Count == 0 && world.nextId == next && world.chunks.revision == 0 && world.chunks.records.Count == 0, "node refusal left a partial chunk");
            world.nodes.RemoveAt(0);
            Require(ChunkStreaming.GenerateChunk(world, chosen.Item1, chosen.Item2) && world.nodes.Count == WorldSimulator.MaxNodes, "exact node budget fit was refused");
            Require(TerrainEntityDump(world, chosen.Item1, chosen.Item2) == TerrainEntityDump(reference, chosen.Item1, chosen.Item2), "budget fit lost resources/walls/containers");
        });
        Run("sparse chunk building and ID budgets leave no partial terrain or consumed IDs", () =>
        {
            var chosen = TerrainFindSite();
            var reference = TerrainBlank(73149, WorldPreset.Urban, 2);
            ChunkStreaming.GenerateChunk(reference, chosen.Item1, chosen.Item2);
            var world = TerrainBlank(73149, WorldPreset.Urban, 2);
            var sentinel = new BuildingState { kind = BuildKind.Chest, x = 500000, y = 500000, id = 1 };
            world.buildings.AddRange(Enumerable.Repeat(sentinel, WorldSimulator.MaxBuildings));
            int next = world.nextId;
            Require(!ChunkStreaming.GenerateChunk(world, chosen.Item1, chosen.Item2), "building budget accepted a new door");
            Require(world.nodes.Count == 0 && world.sites.Count == 0 && world.chunks.records.Count == 0 && world.chunks.revision == 0 && world.nextId == next, "building budget left partial terrain/entities");
            world.buildings.RemoveAt(0);
            Require(ChunkStreaming.GenerateChunk(world, chosen.Item1, chosen.Item2) && world.buildings.Count == WorldSimulator.MaxBuildings, "exact building fit refused");
            world = TerrainBlank(73149, WorldPreset.Urban, 2);
            int needed = reference.nodes.Count + reference.buildings.Count + reference.sites.Count;
            world.nextId = int.MaxValue - 1 - needed;
            string before = TerrainSerialize(world);
            Require(!ChunkStreaming.GenerateChunk(world, chosen.Item1, chosen.Item2) && TerrainSerialize(world) == before, "ID exhaustion consumed part of a chunk");
            world.nextId--;
            Require(ChunkStreaming.GenerateChunk(world, chosen.Item1, chosen.Item2) && world.nextId == int.MaxValue - 2, "exact safe ID budget fit refused");
        });
    }

    static WorldState TerrainBlank(int seed, WorldPreset preset = WorldPreset.Balanced, int density = 1) => new WorldState
    {
        width = 64, height = 64, seed = seed, nextId = 10000000,
        generation = new WorldGenerationSettings { seed = seed, preset = preset, cityDensity = density, chunked = true },
        ground = new string('0', 4096), biomes = new string('0', 4096), chunks = new ChunkWorldState { enabled = true }
    };

    static void TerrainPopulate(WorldState world, bool reverse = false, int minX = -8, int maxX = 8, int minY = -8, int maxY = 8)
    {
        var cells = new List<(int x, int y)>();
        for (int y = minY; y < maxY; y++) for (int x = minX; x < maxX; x++) cells.Add((x, y));
        if (reverse) cells.Reverse();
        foreach (var c in cells) Require(ChunkTerrain.Generate(world, c.x, c.y), "sample chunk refused");
    }

    static (int, int) TerrainFindSite()
    {
        var world = TerrainBlank(73149, WorldPreset.Urban, 2);
        for (int cy = -4; cy <= 4; cy++) for (int cx = -4; cx <= 4; cx++)
        {
            ChunkTerrain.Generate(world, cx, cy);
            if (world.sites.Count > 0) return (cx, cy);
        }
        throw new Exception("no deterministic site fixture found");
    }

    static void TerrainThrows<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception("expected " + typeof(T).Name);
    }

    static string TerrainTileDump(WorldState world, int chunkX, int chunkY)
    {
        var text = new System.Text.StringBuilder();
        for (int y = chunkY * 16; y < chunkY * 16 + 16; y++) for (int x = chunkX * 16; x < chunkX * 16 + 16; x++)
        { text.Append(WorldTiles.Ground(world, x, y)); text.Append(WorldTiles.Biome(world, x, y)); }
        return text.ToString();
    }

    static string TerrainMapDump(WorldState world) => TerrainSerialize(world.chunks.records.OrderBy(c => c.y).ThenBy(c => c.x));
    static string TerrainSerialize<T>(T value) => System.Text.Json.JsonSerializer.Serialize(value, new System.Text.Json.JsonSerializerOptions { IncludeFields = true });

    // IDs intentionally follow visit order. Compare geometry/resources by world
    // coordinates, never by their dynamically allocated entity IDs.
    static string TerrainEntityDump(WorldState world, int? cx = null, int? cy = null)
    {
        bool Inside(int x, int y) => !cx.HasValue || (WorldTiles.FloorDiv(x, 16) == cx.Value && WorldTiles.FloorDiv(y, 16) == cy.Value);
        return TerrainSerialize(new
        {
            nodes = world.nodes.Where(n => Inside(n.x, n.y)).OrderBy(n => n.x).ThenBy(n => n.y)
                .Select(n => new { n.x, n.y, n.kind, n.stock, n.hp }).ToArray(),
            buildings = world.buildings.Where(b => Inside(b.x, b.y)).OrderBy(b => b.x).ThenBy(b => b.y)
                .Select(b => new { b.x, b.y, b.kind, b.rotation, b.footprintVersion, b.hp, b.setting, b.items }).ToArray(),
            sites = world.sites.Where(s => Inside(s.x, s.y)).OrderBy(s => s.x).ThenBy(s => s.y)
                .Select(s => new { s.x, s.y, s.kind, s.district, s.width, s.height, s.doorX, s.doorY }).ToArray()
        });
    }

    static void TerrainChunkContract(WorldState world, int cx, int cy)
    {
        int sx = cx * 16, sy = cy * 16;
        bool Inside(int x, int y) => x >= sx && x < sx + 16 && y >= sy && y < sy + 16;
        var nodes = world.nodes.Where(n => Inside(n.x, n.y)).ToArray();
        var buildings = world.buildings.Where(b => Inside(b.x, b.y)).ToArray();
        var sites = world.sites.Where(s => Inside(s.x, s.y)).ToArray();
        Require(nodes.Length <= 75 && nodes.Count(n => n.kind != NodeKind.Wall && n.kind != NodeKind.Crate) <= 40 && sites.Length <= 1, "per-chunk population budget exceeded");
        var occupied = new HashSet<long>();
        foreach (var node in nodes)
        {
            Require(node.x >= sx + 2 && node.x < sx + 14 && node.y >= sy + 2 && node.y < sy + 14, "entity blocks two-cell boundary corridor");
            Require(WorldTiles.Ground(world, node.x, node.y) != '2' && occupied.Add(WorldTiles.Key(node.x, node.y)), "node overlaps water/entity");
        }
        foreach (var b in buildings) for (int y = b.y; y < b.y + DemoCatalog.Height(b); y++) for (int x = b.x; x < b.x + DemoCatalog.Width(b); x++)
            Require(Inside(x, y) && WorldTiles.Ground(world, x, y) != '2' && occupied.Add(WorldTiles.Key(x, y)), "building overlaps node/water/another chunk");
        foreach (var door in buildings.Where(b => b.kind == BuildKind.Door)) occupied.Remove(WorldTiles.Key(door.x, door.y));
        var reachable = TerrainFlood(world, cx, cy, occupied);
        foreach (var node in nodes.Where(n => n.kind != NodeKind.Wall))
            Require(TerrainNeighbor(reachable, node.x, node.y), "resource/container cannot be gathered from chunk boundary");
        foreach (var site in sites)
        {
            Require(site.x >= sx + 2 && site.y >= sy + 2 && site.x + site.width <= sx + 14 && site.y + site.height <= sy + 14, "site escaped local lot");
            Require(reachable.Contains(WorldTiles.Key(site.doorX, site.doorY)), "site door unreachable");
            Require(buildings.Count(b => b.kind == BuildKind.Door && b.x == site.doorX && b.y == site.doorY && b.hp == 220 && b.footprintVersion == 1 && b.setting == 0) == 1, "site lacks usable real closed door");
            Require(nodes.Any(n => n.kind == NodeKind.Crate && n.x > site.x && n.x < site.x + site.width - 1 && n.y > site.y && n.y < site.y + site.height - 1), "site lacks interior container");
            for (int y = site.y; y < site.y + site.height; y++) for (int x = site.x; x < site.x + site.width; x++)
            {
                Require(WorldTiles.Ground(world, x, y) == '4', "site floor missing");
                bool wall = x == site.x || x == site.x + site.width - 1 || y == site.y || y == site.y + site.height - 1;
                if (wall && !(x == site.doorX && y == site.doorY)) Require(nodes.Any(n => n.kind == NodeKind.Wall && n.x == x && n.y == y), "site wall gap");
                else if (!occupied.Contains(WorldTiles.Key(x, y))) Require(reachable.Contains(WorldTiles.Key(x, y)), "site interior inaccessible");
            }
        }
    }

    static HashSet<long> TerrainFlood(WorldState world, int cx, int cy, HashSet<long> blocked)
    {
        var seen = new HashSet<long>(); var queue = new Queue<long>();
        int sx = cx * 16, sy = cy * 16;
        void Visit(int x, int y)
        {
            long cell = WorldTiles.Key(x, y);
            if (x < sx || y < sy || x >= sx + 16 || y >= sy + 16 || WorldTiles.Ground(world, x, y) == '2' || blocked.Contains(cell) || !seen.Add(cell)) return;
            queue.Enqueue(cell);
        }
        for (int i = 0; i < 16; i++) { Visit(sx + i, sy); Visit(sx + i, sy + 15); Visit(sx, sy + i); Visit(sx + 15, sy + i); }
        while (queue.Count > 0)
        {
            long cell = queue.Dequeue(); int x = WorldTiles.X(cell), y = WorldTiles.Y(cell);
            Visit(x - 1, y); Visit(x + 1, y); Visit(x, y - 1); Visit(x, y + 1);
        }
        return seen;
    }

    static bool TerrainNeighbor(HashSet<long> cells, int x, int y) =>
        cells.Contains(WorldTiles.Key(x - 1, y)) || cells.Contains(WorldTiles.Key(x + 1, y)) ||
        cells.Contains(WorldTiles.Key(x, y - 1)) || cells.Contains(WorldTiles.Key(x, y + 1));
}
