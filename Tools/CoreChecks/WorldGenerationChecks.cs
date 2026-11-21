using DeadCircuit;

static partial class Checks
{
    // Main must invoke this alongside the existing suites in Program.cs.
    static void WorldGenerationChecks()
    {
        Run("world generation finite maps contain ruins but no tutorial facilities or floors", () =>
        {
            foreach (int seed in new[] { 73149, -7, 42 })
            {
                var world = WorldGeneration.Generate(new WorldGenerationSettings { seed = seed, chunked = false });
                Require(world.sites.Count > 0 && world.buildings.Count == world.sites.Count &&
                    world.buildings.All(b => b.kind == BuildKind.Door), $"seed {seed} has preset facilities outside ruins");
                Require(world.era == TechEra.Primitive && world.players.Count == 0, "generation advanced the era or created a survivor");
                for (int y = 0; y < world.height; y++) for (int x = 0; x < world.width; x++)
                    if (WorldTiles.Ground(world, x, y) == '4')
                        Require(world.sites.Any(s => x >= s.x && x < s.x + s.width && y >= s.y && y < s.y + s.height),
                            $"seed {seed} has floor outside a ruin at ({x},{y})");
            }
        });
        Run("world generation finite center keeps natural water and ground outside construction", () =>
        {
            foreach (int seed in new[] { 73149, -7, 42 })
            {
                var world = WorldGeneration.Generate(new WorldGenerationSettings { seed = seed });
                for (int y = 22; y <= 42; y++) for (int x = 22; x <= 42; x++)
                {
                    char actual = WorldTiles.Ground(world, x, y);
                    if (actual == '3' || actual == '4') continue;
                    ChunkTerrain.SampleContinuous(seed, x, y, out char natural, out _);
                    Require(actual == natural, $"seed {seed} replaced natural ground at ({x},{y})");
                }
            }
        });
        Run("world generation public seed constructor produces current save and generator versions", () =>
        {
            var sim = new WorldSimulator(73149);
            Require(sim.State.version == WorldSimulator.SaveVersion, "seed constructor did not create a current world");
            Require(sim.State.generatorVersion == WorldGeneration.CurrentGeneratorVersion, "seed constructor used an obsolete generator");
            Require(sim.State.generation != null && sim.State.generation.seed == 73149 && sim.State.sites.Count > 0, "seed constructor bypassed the generator");
        });
        Run("world generation seed text has stable numeric and literal answers", () =>
        {
            Require(WorldSeed.Resolve(null) == 73149 && WorldSeed.Resolve("  ") == 73149, "empty seed default");
            Require(WorldSeed.Resolve("-2147483648") == int.MinValue, "minimum seed rejected");
            Require(WorldSeed.Resolve(" +2147483647 ") == int.MaxValue, "maximum seed rejected");
            Require(WorldSeed.Resolve("00042") == 42 && WorldSeed.Resolve("0") == 0, "numeric seed changed");
            Require(WorldSeed.Resolve("hello") == 1335831723, "stable FNV-1a reference vector changed");
            Require(WorldSeed.Resolve("hello") != WorldSeed.Resolve("Hello"), "text seed lost case");
        });
        Run("world generation settings defaults and clone are isolated", () =>
        {
            var defaults = new WorldGenerationSettings();
            Require(defaults.worldName == "末日新世界" && defaults.seed == 73149 && defaults.seedText == "", "world defaults changed");
            Require(defaults.preset == WorldPreset.Balanced && defaults.cityDensity == 1, "preset defaults changed");
            var copy = WorldGeneration.CloneSettings(defaults);
            copy.worldName = "副本"; copy.seed = -7; copy.seedText = "hello"; copy.cityDensity = 2;
            Require(defaults.worldName == "末日新世界" && defaults.seed == 73149 && defaults.seedText == "" && defaults.cityDensity == 1, "settings clone aliases caller");
        });
        Run("world generation 100 seeds deterministic varied complete and reachable", () =>
        {
            var kinds = new HashSet<SiteKind>();
            int biomesSeen = 0;
            for (int i = 0; i < 100; i++)
            {
                var settings = new WorldGenerationSettings { seed = i - 50, preset = (WorldPreset)(i % 3), cityDensity = i / 3 % 3 };
                var a = WorldGeneration.Generate(settings);
                var b = WorldGeneration.Generate(WorldGeneration.CloneSettings(settings));
                Require(Dump(a) == Dump(b), $"seed {settings.seed} is not deterministic");
                var different = WorldGeneration.CloneSettings(settings); different.seed++;
                var c = WorldGeneration.Generate(different);
                Require(a.ground != c.ground || a.biomes != c.biomes, $"seed {settings.seed} changes metadata only");
                Require(!ReferenceEquals(settings, a.generation) && settings.seed == i - 50, "generation mutates or owns caller settings");
                GenerationMapContract(a);
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                    biomesSeen |= 1 << (int)ChunkTerrain.SampleBiome(a.seed, x * 6, y * 6);
                foreach (var site in a.sites) kinds.Add(site.kind);
            }
            Require(kinds.Count == 12, "100 seeds did not exercise every declared site type");
            Require(biomesSeen == WorldGeneration.AllBiomeMask, "100 seeded world-space surveys did not exercise all ten natural biomes: " + biomesSeen.ToString("X"));
        });
        Run("world generation city presets vary construction independently of natural biomes", () =>
        {
            string biomes = WorldGeneration.Generate(null).biomes;
            foreach (WorldPreset preset in Enum.GetValues<WorldPreset>())
            {
                int previous = -1;
                for (int density = 0; density <= 2; density++)
                {
                    var state = WorldGeneration.Generate(new WorldGenerationSettings { preset = preset, cityDensity = density });
                    Require(state.biomes == biomes, "city settings changed natural biome layer");
                    Require(state.sites.Count > previous, "city density did not increase actual building count");
                    if (density == 0) Require(state.sites.All(s => s.district == DistrictKind.Wilderness), "density zero created city districts");
                    previous = state.sites.Count;
                }
            }
            int wilderness = WorldGeneration.Generate(new WorldGenerationSettings { preset = WorldPreset.Wilderness }).sites.Count;
            int balanced = WorldGeneration.Generate(new WorldGenerationSettings()).sites.Count;
            int urban = WorldGeneration.Generate(new WorldGenerationSettings { preset = WorldPreset.Urban }).sites.Count;
            Require(wilderness < balanced && balanced < urban, "presets change labels only");
            var districts = WorldGeneration.Generate(new WorldGenerationSettings { preset = WorldPreset.Urban, cityDensity = 2 }).sites.Select(s => s.district).ToHashSet();
            Require(districts.Contains(DistrictKind.Urban) && districts.Contains(DistrictKind.Industrial) && districts.Contains(DistrictKind.Wilderness), "urban map lacks district variety");
        });
        Run("world generation extreme signed seeds and seed text use resolved seed without mutating input", () =>
        {
            foreach (int seed in new[] { int.MinValue, int.MaxValue, 0, -1, 73149 })
            {
                var a = WorldGeneration.Generate(new WorldGenerationSettings { seed = seed });
                var b = WorldGeneration.Generate(new WorldGenerationSettings { seed = 99, seedText = seed.ToString(System.Globalization.CultureInfo.InvariantCulture) });
                Require(a.seed == seed && b.seed == seed && a.ground == b.ground && a.biomes == b.biomes, "resolved integer seed altered map");
                GenerationMapContract(a);
            }
            var settings = new WorldGenerationSettings { seed = 13, seedText = " hello ", worldName = " 世界 ", preset = WorldPreset.Urban, cityDensity = 2 };
            var generated = WorldGeneration.Generate(settings);
            var simulator = new WorldSimulator(settings);
            Require(simulator.State.seed == generated.seed && simulator.State.ground == generated.ground && simulator.State.biomes == generated.biomes && simulator.State.sites.Count == generated.sites.Count, "settings constructor bypassed generation options");
            Require(generated.seed == 1335831723 && generated.generation.seed == generated.seed && generated.generation.seedText == "hello" && generated.generation.worldName == "世界", "text seed/settings not normalized");
            Require(settings.seed == 13 && settings.seedText == " hello " && settings.worldName == " 世界 ", "generation rewrote caller settings");
            var afterRandom = WorldGeneration.Generate(settings); _ = WorldSeed.Random();
            Require(Dump(afterRandom) == Dump(WorldGeneration.Generate(settings)), "random seed helper contaminated deterministic generation");
            Require(WorldGeneration.Generate(new WorldGenerationSettings { seed = int.MinValue }).biomes != WorldGeneration.Generate(new WorldGenerationSettings { seed = int.MaxValue }).biomes, "signed seed endpoints collapsed");
        });
        Run("world generation restore deep copies settings sites and biome metadata", () =>
        {
            var source = WorldGeneration.Generate(new WorldGenerationSettings { seedText = "废土一号", preset = WorldPreset.Urban, cityDensity = 2 });
            var sim = new WorldSimulator(source);
            Require(sim.State.generatorVersion == WorldGeneration.CurrentGeneratorVersion && sim.State.biomes == source.biomes && sim.State.sites.Count == source.sites.Count, "restore dropped generation metadata");
            Require(!ReferenceEquals(source.generation, sim.State.generation) && !ReferenceEquals(source.sites, sim.State.sites), "restore aliases mutable metadata");
            for (int i = 0; i < source.sites.Count; i++)
            {
                var a = source.sites[i]; var b = sim.State.sites[i];
                Require(!ReferenceEquals(a, b) && a.id == b.id && a.x == b.x && a.y == b.y && a.width == b.width && a.height == b.height && a.doorX == b.doorX && a.doorY == b.doorY && a.kind == b.kind && a.district == b.district, "restore lost or aliased site fields");
            }
            int x = sim.State.sites[0].x; source.sites[0].x++; source.generation.worldName = "修改来源"; source.biomes = new string('0', 4096);
            Require(sim.State.sites[0].x == x && sim.State.generation.worldName != source.generation.worldName && sim.State.biomes != source.biomes, "restored world follows later caller mutations");
        });
        Run("world generation legacy metadata never regenerates terrain entities or inventories", () =>
        {
            var legacy = Flat(); legacy.seed = -991;
            legacy.nodes.Add(new NodeState { id = legacy.nextId++, x = 4, y = 5, stock = 63, kind = NodeKind.Tree });
            var machine = Add(legacy, BuildKind.Chest, 8, 9); machine.items[(int)ItemKind.Wood] = 17;
            PrepareLegacySaveFixture(legacy, 4, 0); legacy.generation = null; legacy.biomes = "";
            var nodes = legacy.nodes; var machines = legacy.buildings; var sites = legacy.sites; var inventory = machine.items;
            string before = Dump(legacy);
            WorldGeneration.LegacyMetadata(legacy);
            Require(legacy.generatorVersion == 0 && legacy.sites.Count == 0 && legacy.generation.seed == -991 && legacy.biomes.Length == 4096, "legacy metadata/gen0 incorrect");
            Require(ReferenceEquals(nodes, legacy.nodes) && ReferenceEquals(machines, legacy.buildings) && ReferenceEquals(inventory, legacy.buildings[0].items) && ReferenceEquals(sites, legacy.sites), "legacy helper replaced entity lists/inventory");
            var settings = legacy.generation; string biomes = legacy.biomes;
            legacy.generation = null; legacy.biomes = "";
            Require(Dump(legacy) == before, "metadata migration changed original world fields");
            legacy.generation = settings; legacy.biomes = biomes; WorldGeneration.Validate(legacy);
            string once = Dump(legacy); WorldGeneration.LegacyMetadata(legacy);
            Require(Dump(legacy) == once && ReferenceEquals(settings, legacy.generation), "legacy metadata migration is not idempotent");
            var current = WorldGeneration.Generate(null); string currentBefore = Dump(current); WorldGeneration.LegacyMetadata(current);
            Require(Dump(current) == currentBefore, "metadata helper overwrote current generator state");
        });
        RunHistorical("world generation generator one saves retain their existing open entrances", () =>
        {
            var legacy = WorldGeneration.Generate(new WorldGenerationSettings { seed = -991 });
            legacy.buildings.RemoveAll(b => b.kind == BuildKind.Door);
            PrepareLegacySaveFixture(legacy, 6, 1);
            string before = Dump(legacy);
            WorldGeneration.Validate(legacy); WorldGeneration.LegacyMetadata(legacy);
            Require(Dump(legacy) == before, "legacy metadata/validation changed generator-one entities");
            var sim = new WorldSimulator(Saved(legacy));
            Require(Dump(legacy) == before, "restore mutated generator-one input");
            Require(sim.State.generatorVersion == 1 && sim.State.ground == legacy.ground && sim.State.biomes == legacy.biomes && sim.State.nextId == legacy.nextId, "restore regenerated generator-one terrain or IDs");
            Require(sim.State.nodes.Count == legacy.nodes.Count && sim.State.buildings.Count == legacy.buildings.Count && sim.State.sites.Count == legacy.sites.Count && !sim.State.buildings.Any(b => b.kind == BuildKind.Door), "restore added doors or replaced legacy entities");
            for (int i = 0; i < legacy.nodes.Count; i++)
            {
                var a = legacy.nodes[i]; var b = sim.State.nodes[i];
                Require(a.id == b.id && a.kind == b.kind && a.x == b.x && a.y == b.y && a.hp == b.hp && a.stock == b.stock, "legacy node changed on restore");
            }
            for (int i = 0; i < legacy.buildings.Count; i++)
            {
                var a = legacy.buildings[i]; var b = sim.State.buildings[i];
                Require(a.id == b.id && a.kind == b.kind && a.x == b.x && a.y == b.y && a.rotation == b.rotation && a.hp == b.hp && a.footprintVersion == b.footprintVersion && ExpandedLegacyFixtureInventory(a.items).SequenceEqual(b.items), "legacy machine changed on restore");
            }
            foreach (var site in sim.State.sites)
                Require(RoomLayout.Build(sim.State).At(site.x + 1, site.y + 1) == null, "legacy open entrance acquired an implicit roof");
        });
        Run("world generation malformed settings and metadata are rejected", () =>
        {
            foreach (int density in new[] { -1, 3, int.MinValue, int.MaxValue })
                GenerationRejects(() => WorldGeneration.Generate(new WorldGenerationSettings { cityDensity = density }), "invalid density");
            foreach (int preset in new[] { -1, 3, int.MaxValue })
                GenerationRejects(() => WorldGeneration.Generate(new WorldGenerationSettings { preset = (WorldPreset)preset }), "invalid preset");
            foreach (string name in new[] { null, "", "   ", "bad\nname", new string('a', 65) })
                GenerationRejects(() => WorldGeneration.Generate(new WorldGenerationSettings { worldName = name }), "invalid name");
            foreach (string text in new[] { null, "bad\nseed", new string('a', 129) })
                GenerationRejects(() => WorldGeneration.Generate(new WorldGenerationSettings { seedText = text }), "invalid seed text");
            foreach (Action<WorldState> corrupt in new Action<WorldState>[]
            {
                s => s.generation = null, s => s.generation.seed++, s => s.generation.seedText = "different seed",
                s => s.biomes = null, s => s.biomes = "0", s => s.biomes = new string((char)('0' + WorldGeneration.BiomeCount), 4096),
                s => s.generatorVersion = -1, s => s.generatorVersion = WorldGeneration.CurrentGeneratorVersion + 1,
                s => s.sites = null, s => s.sites.Add(null), s => s.sites.Add(s.sites[0]),
                s => s.sites[0].kind = (SiteKind)12, s => s.sites[0].district = (DistrictKind)4,
                s => s.sites[0].width = int.MaxValue, s => s.sites[0].x = int.MaxValue,
                s => s.sites[0].doorX = s.sites[0].x - 1, s => s.sites[0].doorY = s.sites[0].y - 1,
                s => { s.sites[0].doorX = s.sites[0].x; s.sites[0].doorY = s.sites[0].y; },
                s => s.sites[0].id = s.nextId,
                s => { var a = s.sites[0]; var b = s.sites[1]; b.x = a.x; b.y = a.y; b.width = a.width; b.height = a.height; b.doorX = a.doorX; b.doorY = a.doorY; }
            })
            {
                var state = WorldGeneration.Generate(null); corrupt(state);
                GenerationRejects(() => WorldGeneration.Validate(state), "corrupt generation metadata");
            }
            var played = WorldGeneration.Generate(null); played.nodes.RemoveAll(n => n.kind == NodeKind.Wall || n.kind == NodeKind.Crate);
            played.buildings.RemoveAll(b => b.kind == BuildKind.Door);
            WorldGeneration.Validate(played); // Legal salvage/destruction must remain saveable.
            var restored = new WorldSimulator(Saved(played));
            Require(restored.State.sites.Count == played.sites.Count && restored.State.nodes.All(n => n.kind != NodeKind.Wall && n.kind != NodeKind.Crate) && !restored.State.buildings.Any(b => b.kind == BuildKind.Door), "save restore requires demolished walls/doors or searched containers");
        });
    }

    static void GenerationMapContract(WorldState state)
    {
        Require(state.version == WorldSimulator.SaveVersion && state.generatorVersion == WorldGeneration.CurrentGeneratorVersion && state.width == 64 && state.height == 64, "generation version/dimensions");
        Require(state.generation != null && state.generation.seed == state.seed, "missing/resolved seed metadata");
        Require(state.ground.Length == 4096 && state.ground.All(c => c >= '0' && c <= '4'), "invalid terrain");
        Require(state.biomes.Length == 4096 && state.biomes.All(c => c >= '0' && c < '0' + WorldGeneration.BiomeCount), "missing or invalid biome layer");
        Require(state.nodes.Count <= 1400 && state.sites.Count > 0 && state.sites.Count <= 32, "entity limits");
        var occupied = new HashSet<int>(); var ids = new HashSet<int>();
        foreach (var n in state.nodes)
        {
            Require(n.x >= 3 && n.y >= 3 && n.x <= 60 && n.y <= 60 && n.stock > 0 && n.hp > 0, "invalid node or blocked dry border");
            Require(state.ground[n.x + n.y * 64] != '2' && occupied.Add(n.x + n.y * 64) && ids.Add(n.id), "overlapping/water node or duplicate ID");
        }
        foreach (var b in state.buildings)
        {
            Require(ids.Add(b.id) && b.footprintVersion == 1, "invalid machine ID/footprint");
            for (int y = b.y; y < b.y + DemoCatalog.Height(b); y++) for (int x = b.x; x < b.x + DemoCatalog.Width(b); x++)
                Require(x >= 0 && x < 64 && y >= 0 && y < 64 && state.ground[x + y * 64] != '2' && occupied.Add(x + y * 64), "machine overlaps node/water/bounds");
        }
        // Structural access assumes each reachable real door can be opened. Doors
        // remain occupied for overlap checks above and for actual closed-door play.
        var passageBlocked = new HashSet<int>(occupied);
        foreach (var door in state.buildings.Where(b => b.kind == BuildKind.Door))
            passageBlocked.Remove(door.x + door.y * 64);
        var reachable = GenerationFlood(state, passageBlocked);
        for (int i = 0; i < 64; i++) foreach (int cell in new[] { i, i + 63 * 64, i * 64, 63 + i * 64 })
            Require(state.ground[cell] != '2' && reachable.Contains(cell), "dry world boundary is unreachable");
        foreach (var node in state.nodes.Where(n => n.kind != NodeKind.Wall && n.kind != NodeKind.Crate))
            Require(GenerationHasReachableNeighbor(reachable, node.x, node.y), $"seed {state.seed}: resource at ({node.x},{node.y}) has no approach from dry land");
        var lots = new HashSet<int>();
        var interiors = RoomLayout.Build(state);
        Require(interiors.rooms.Count == state.sites.Count, "fresh sites are not individual enclosed rooms");
        foreach (var site in state.sites)
        {
            Require(ids.Add(site.id) && site.id > 0, "duplicate site ID");
            Require(site.x >= 3 && site.y >= 3 && site.width >= 5 && site.height >= 5 && site.x + site.width <= 61 && site.y + site.height <= 61, "invalid site bounds");
            Require((int)site.kind >= 0 && (int)site.kind < 12 && (int)site.district >= 0 && (int)site.district < 4, "invalid site type/district");
            bool doorHorizontal = (site.doorY == site.y || site.doorY == site.y + site.height - 1) && site.doorX > site.x && site.doorX < site.x + site.width - 1;
            bool doorVertical = (site.doorX == site.x || site.doorX == site.x + site.width - 1) && site.doorY > site.y && site.doorY < site.y + site.height - 1;
            Require(doorHorizontal || doorVertical, "door is not a noncorner wall opening");
            Require(reachable.Contains(site.doorX + site.doorY * 64), "POI entrance unreachable from the dry boundary");
            var interior = interiors.At(site.x + 1, site.y + 1);
            var entrance = state.buildings.Single(b => b.kind == BuildKind.Door && b.x == site.doorX && b.y == site.doorY);
            Require(interior != null && interior.cells.Length == (site.width - 2) * (site.height - 2) && interior.doorIds.SequenceEqual(new[] { entrance.id }), "site walls and real door do not enclose all interior floor");
            bool hasCrate = false;
            for (int y = site.y; y < site.y + site.height; y++) for (int x = site.x; x < site.x + site.width; x++)
            {
                int cell = x + y * 64;
                Require(lots.Add(cell), "site overlaps another POI");
                Require(state.ground[cell] == '4', "POI lacks floor");
                var node = state.nodes.SingleOrDefault(n => n.x == x && n.y == y);
                bool wall = x == site.x || y == site.y || x == site.x + site.width - 1 || y == site.y + site.height - 1;
                if (x == site.doorX && y == site.doorY)
                {
                    var door = state.buildings.SingleOrDefault(b => b.x == x && b.y == y);
                    Require(node == null && door != null && door.kind == BuildKind.Door && door.hp == 220f && door.setting == 0 && door.footprintVersion == 1, "POI entrance lacks its real closed door");
                }
                else if (wall) Require(node != null && node.kind == NodeKind.Wall && node.hp > 0 && node.stock > 0, "missing destructible wall");
                else if (node != null)
                {
                    Require(node.kind == NodeKind.Crate, "resource generated inside POI"); hasCrate = true;
                    Require(GenerationHasReachableNeighbor(reachable, x, y), "container cannot be searched");
                }
                else Require(reachable.Contains(cell), "POI interior unreachable");
            }
            Require(hasCrate, "POI lacks searchable container");
            int outsideX = site.doorX, outsideY = site.doorY;
            if (site.doorY == site.y) outsideY--; else if (site.doorY == site.y + site.height - 1) outsideY++;
            else if (site.doorX == site.x) outsideX--; else outsideX++;
            Require(state.ground[outsideX + outsideY * 64] == '3' && reachable.Contains(outsideX + outsideY * 64), "POI door has no usable road approach");
        }
        Require(state.zombies.Count == 6, "initial enemy count changed");
        foreach (var z in state.zombies)
        {
            Require(ids.Add(z.id) && z.x >= 0 && z.x < 64 && z.y >= 0 && z.y < 64, "enemy ID/bounds");
            int cell = (int)z.x + (int)z.y * 64;
            Require(state.ground[cell] != '2' && !occupied.Contains(cell), "enemy starts in water or a solid cell");
        }
        Require(ids.All(id => id > 0 && id < state.nextId), "nextId does not cover site/entity IDs");
        WorldGeneration.Validate(state);
    }

    static HashSet<int> GenerationFlood(WorldState state, HashSet<int> blocked)
    {
        var seen = new HashSet<int>(); var queue = new Queue<int>();
        Require(!blocked.Contains(0) && state.ground[0] != '2', "blocked dry boundary");
        seen.Add(0); queue.Enqueue(0);
        while (queue.Count > 0)
        {
            int c = queue.Dequeue(), x = c % 64, y = c / 64;
            foreach (var step in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + step.Item1, ny = y + step.Item2, next = nx + ny * 64;
                if (nx < 0 || ny < 0 || nx >= 64 || ny >= 64 || state.ground[next] == '2' || blocked.Contains(next) || !seen.Add(next)) continue;
                queue.Enqueue(next);
            }
        }
        return seen;
    }

    static bool GenerationHasReachableNeighbor(HashSet<int> cells, int x, int y) =>
        (x > 0 && cells.Contains(x - 1 + y * 64)) || (x < 63 && cells.Contains(x + 1 + y * 64)) ||
        (y > 0 && cells.Contains(x + (y - 1) * 64)) || (y < 63 && cells.Contains(x + (y + 1) * 64));

    static void GenerationRejects(Action action, string message)
    {
        bool rejected = false;
        try { action(); } catch (ArgumentException) { rejected = true; }
        Require(rejected, message + " was accepted");
    }

}
