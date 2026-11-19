using DeadCircuit;
using System.Text.Json;
using System.Text.Json.Nodes;

static partial class Checks
{
    static int passed, failed, selected;
    static string[] filters = Array.Empty<string>();
    static readonly JsonSerializerOptions Json = new() { IncludeFields = true };
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Near(float actual, float expected, string message, float tolerance = .001f) =>
        Require(float.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance, $"{message}: expected {expected}, got {actual}");
    static void Run(string name, Action test)
    {
        if (filters.Length > 0 && !filters.Any(f => name.Contains(f, StringComparison.OrdinalIgnoreCase))) return;
        selected++;
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {name}: {ex.GetType().Name}: {ex.Message}"); }
    }
    static void Advance(WorldSimulator sim, float seconds)
    {
        int ticks = (int)Math.Ceiling(seconds / (double)WorldSimulator.FixedStep - .00001);
        for (int n = 0; n < ticks; n++) sim.Step(WorldSimulator.FixedStep);
    }
    static PlayerCommand Command(PlayerState p, string action, int x = 0, int y = 0, int arg = 0, int rotation = 0) =>
        new() { playerId = p.id, action = action, x = x, y = y, arg = arg, rotation = rotation };
    static WorldSimulator New(out PlayerState p)
    {
        var sim = new WorldSimulator(73149); p = sim.AddPlayer("test-player", "测试者"); return sim;
    }
    // Legacy spatial/logistics fixtures explicitly opt into LV. A seeded new world must stay Primitive.
    static WorldState Flat(float elapsed = 0, TechEra era = TechEra.LowVoltage) => new()
    {
        version = 4, era = era, seed = 73149, ground = new string('4', 4096), elapsed = elapsed,
        tick = (long)Math.Round(elapsed / WorldSimulator.FixedStep), nextWaveAt = elapsed + 180f
    };
    static PlayerState Builder(WorldSimulator sim, string id = "p", string name = "P")
    {
        var player = sim.AddPlayer(id, name); Array.Fill(player.items, 100); return player;
    }
    static BuildingState Add(WorldState state, BuildKind kind, int x, int y, int rotation = 0, int footprintVersion = 1)
    {
        var b = new BuildingState { id = state.nextId++, kind = kind, x = x, y = y, rotation = rotation,
            footprintVersion = footprintVersion, hp = WorldSimulator.BuildingMaxHealth(kind) };
        state.buildings.Add(b); return b;
    }
    static NodeState Ore(WorldState state, int x, int y, int stock = 100)
    {
        var n = new NodeState { id = state.nextId++, kind = NodeKind.Ore, x = x, y = y, stock = stock };
        state.nodes.Add(n); return n;
    }
    static BuildingState Building(WorldSimulator sim, int id) => sim.State.buildings.Single(b => b.id == id);
    static string Dump(WorldState state) => JsonSerializer.Serialize(state, Json);
    static WorldState Saved(WorldState state) => JsonSerializer.Deserialize<WorldState>(Dump(state), Json);
    static int[] ItemTotals(WorldState state)
    {
        var totals = new int[DemoCatalog.ItemCount];
        foreach (var b in state.buildings) for (int i = 0; i < totals.Length; i++) totals[i] += b.items[i];
        // A dispatched item has left its source and is not in its destination yet.
        foreach (var c in state.cargo) totals[c.item]++;
        return totals;
    }
    static void Conserved(WorldState state, int[] expected)
    {
        Require(ItemTotals(state).SequenceEqual(expected), $"inventory + cargo changed at tick {state.tick}");
        Require(state.cargo.Count <= WorldSimulator.MaxCargo, "cargo limit exceeded");
        Require(state.cargo.Select(c => c.id).Distinct().Count() == state.cargo.Count, "duplicate cargo id");
        Require(state.cargo.Select(c => c.fromId).Distinct().Count() == state.cargo.Count, "source dispatched several simultaneous flights");
        foreach (var c in state.cargo)
            Require(c.duration > 0 && float.IsFinite(c.duration) && c.progress >= 0 && c.progress <= 1, "invalid live cargo timing");
        foreach (var b in state.buildings)
        {
            Require(b.items.All(n => n >= 0 && n <= WorldSimulator.BufferLimit), "invalid machine inventory");
            var incoming = state.cargo.Where(c => c.toId == b.id).ToArray();
            for (int i = 0; i < DemoCatalog.ItemCount; i++)
                Require(b.items[i] + incoming.Count(c => c.item == i) <= WorldSimulator.BufferLimit, "destination item capacity was over-reserved");
            if (b.kind == BuildKind.Belt) Require(b.items.Sum() + incoming.Length <= 2, "belt shared capacity was over-reserved");
        }
    }
    static CargoState AwaitCargo(WorldSimulator sim, int from, int to)
    {
        for (int n = 0; n < 40; n++)
        {
            var cargo = sim.State.cargo.FirstOrDefault(c => c.fromId == from && c.toId == to);
            if (cargo != null) return cargo;
            sim.Step(WorldSimulator.FixedStep);
        }
        throw new Exception($"no flight from {from} to {to} within two seconds");
    }
    static void Reject(WorldState state, string message)
    {
        bool rejected = false;
        try { _ = new WorldSimulator(state); } catch (ArgumentException) { rejected = true; }
        Require(rejected, message);
    }
    static int Main(string[] args)
    {
        if(args.Length==2 && args[0]=="--export-landmarks") { ExportLandmarks(args[1]); return 0; }
        filters = args;
        SiteBlueprintDefinitionChecks();
        LandmarkChecks();
        ChunkChecks();
        ChunkTerrainChecks();
        InfiniteRoomChecks();
        SeasonChecks();
        InfiniteSurvivalChecks();
        DoorSystemChecks();
        InteriorChecks(); SteamClusterChecks();
        CatalogAndWorldChecks(); CommandAndSpatialChecks(); PowerChecks(); CargoChecks(); SaveAndCombatChecks();
        TechnologyChecks(); EraEnergyChecks(); EraProductionChecks(); VersionThreeSaveChecks(); EquipmentChecks(); WorldGenerationChecks(); WorldArchiveChecks(); SurvivalChecks(); EnemyChecks(); ExpansionIntegrationChecks(); ForagingChecks();
        if (selected == 0) { Console.Error.WriteLine("No check names matched the supplied filters."); return 2; }
        Console.WriteLine($"CORE_CHECKS_{(failed == 0 ? "PASS" : "FAIL")} suites={selected} passed={passed} failed={failed}");
        return failed == 0 ? 0 : 1;
    }

    static void CatalogAndWorldChecks()
    {
        Run("enum compatibility, catalog footprints and output ports", () =>
        {
            var kinds = new[] { BuildKind.Wall, BuildKind.Generator, BuildKind.Miner, BuildKind.Belt, BuildKind.Smelter, BuildKind.Assembler, BuildKind.Turret, BuildKind.Chest, BuildKind.PowerPole, BuildKind.SolarPanel };
            var widths = new[] { 1, 2, 2, 1, 2, 3, 1, 1, 1, 3 }; var heights = new[] { 1, 2, 2, 1, 2, 2, 1, 1, 1, 2 };
            var demands = new[] { 0f, 0f, 20f, 0f, 35f, 45f, 20f, 0f, 0f, 0f };
            for (int i = 0; i < kinds.Length; i++)
            {
                Require((int)kinds[i] == i, "saved BuildKind ordinal changed"); Near(DemoCatalog.Demand(kinds[i]), demands[i], $"{kinds[i]} kW demand");
                Require(DemoCatalog.IsPowerNode(kinds[i]) == (i == 1 || i == 8 || i == 9), "power node classification");
                for (int r = 0; r < 4; r++)
                {
                    int w = r % 2 == 0 ? widths[i] : heights[i], h = r % 2 == 0 ? heights[i] : widths[i];
                    var b = new BuildingState { kind = kinds[i], x = 10, y = 12, rotation = r, footprintVersion = 1 };
                    Require(DemoCatalog.Width(kinds[i], r) == w && DemoCatalog.Height(kinds[i], r) == h, $"{kinds[i]} rotation {r} dimensions");
                    Require(DemoCatalog.Width(b) == w && DemoCatalog.Height(b) == h, "state footprint dimensions");
                    Near(DemoCatalog.CenterX(b), 10 + (w - 1) / 2f, "center x"); Near(DemoCatalog.CenterY(b), 12 + (h - 1) / 2f, "center y");
                    for (int y = 11; y <= 12 + h; y++) for (int x = 9; x <= 10 + w; x++)
                        Require(DemoCatalog.Contains(b, x, y) == (x >= 10 && x < 10 + w && y >= 12 && y < 12 + h), "footprint membership");
                    int ox = r == 0 ? 10 + w : r == 2 ? 9 : 10 + (w - 1) / 2;
                    int oy = r == 1 ? 12 + h : r == 3 ? 11 : 12 + (h - 1) / 2;
                    Require(DemoCatalog.OutputX(b) == ox && DemoCatalog.OutputY(b) == oy, $"{kinds[i]} rotation {r} lower-middle output port");
                    b.footprintVersion = 0; Require(DemoCatalog.Width(b) == 1 && DemoCatalog.Height(b) == 1, "legacy building was expanded");
                }
            }
            Near(DemoCatalog.LinkRange, 7, "link range"); Near(DemoCatalog.SupplyRadius, 4, "supply radius"); Near(DemoCatalog.CargoSpeed, 2.5f, "cargo speed");
            Require(WorldSimulator.MaxCargo == 1024, "cargo contract limit");
        });
        Run("seed reproducibility and variation", () =>
        {
            var a = new WorldSimulator(123); var b = new WorldSimulator(123); var c = new WorldSimulator(124);
            Require(Dump(a.State) == Dump(b.State), "same seed differs"); Require(a.State.ground != c.State.ground, "different seeds identical");
        });
        Run("24 seeds have legal footprints and safe spawn", () =>
        {
            for (int seed = -12; seed < 12; seed++)
            {
                var sim = new WorldSimulator(seed); var p = sim.AddPlayer("p", "P");
                Require(sim.State.version == WorldSimulator.SaveVersion && sim.State.ground.Length == 4096, "new world version/map length"); Require(!sim.IsBlocked((int)p.x, (int)p.y), "blocked spawn");
                var cells = new HashSet<(int, int)>();
                foreach (var n in sim.State.nodes) Require(n.x >= 0 && n.x < 64 && n.y >= 0 && n.y < 64 && n.stock > 0 && cells.Add((n.x, n.y)), "invalid/overlapping node");
                foreach (var b in sim.State.buildings)
                {
                    Require(b.footprintVersion == 1, "starter kept legacy footprint");
                    for (int y = b.y; y < b.y + DemoCatalog.Height(b); y++) for (int x = b.x; x < b.x + DemoCatalog.Width(b); x++)
                        Require(x >= 0 && x < 64 && y >= 0 && y < 64 && sim.State.ground[x + y * 64] != '2' && cells.Add((x, y)), "overlapping/outside/water starter footprint");
                }
                Require(sim.State.nodes.Any(n => n.kind == NodeKind.Ore && n.x == 24 && n.y == 30), "starter ore moved");
            }
        });
        Run("primitive starter leaves a safe usable construction area", () =>
        {
            var sim = New(out var p);
            Require(sim.State.era == TechEra.Primitive, "seeded world skipped primitive progression");
            Require(sim.State.buildings.All(b => TechCatalog.RequiredEra(b.kind) == TechEra.Primitive), "modern starter production line bypassed progression");
            Require(!sim.State.buildings.Any(b => DemoCatalog.Contains(b, 34, 30) || DemoCatalog.Contains(b, 36, 30)), "tutorial gap already occupied"); Near(p.x, 32, "spawn x"); Near(p.y, 35, "spawn y");
        });
    }

    static void CommandAndSpatialChecks()
    {
        Run("finite input, fixed tick, speed limit and disconnect", () =>
        {
            var sim = New(out var p); float x = p.x, y = p.y;
            sim.Apply(new PlayerCommand { playerId = p.id, moveX = float.NaN }); Advance(sim, 1); Require(p.x == x && p.y == y, "NaN accepted");
            sim.Apply(new PlayerCommand { playerId = p.id, moveX = 999, moveY = 999 }); Advance(sim, 1);
            Require(float.IsFinite(p.x) && Math.Sqrt(Math.Pow(p.x - x, 2) + Math.Pow(p.y - y, 2)) <= 3.3, "speed exploit");
            sim.SetConnected(p.id, false); x = p.x; y = p.y; Advance(sim, 1); Require(p.x == x && p.y == y, "disconnected player moves");
            sim.SetConnected(p.id, true); Advance(sim, 1); Require(p.x == x && p.y == y, "stale movement after reconnect");
            long tick = sim.State.tick; sim.Step(float.NaN); sim.Step(float.PositiveInfinity); sim.Step(-1); sim.Step(0); Require(sim.State.tick == tick, "invalid dt advanced simulation");
            sim.Step(100); Require(sim.State.tick == tick + 20, "large dt bypassed one-second limit");
        });
        Run("attribute and skill budgets remain separate", () =>
        {
            var sim = New(out var p); sim.Apply(Command(p, "attribute", arg: 1)); Require(p.attributes[1] == 1 && p.attributePoints == 1 && p.skillPoints == 1, "attribute budget");
            sim.Apply(Command(p, "skill", arg: 0)); Require(p.skills[0] == 1 && p.skillPoints == 0 && p.attributePoints == 1, "skill budget");
            p.skillPoints = 1; sim.Apply(Command(p, "skill", arg: 0)); Require(p.skills[0] == 1 && p.skillPoints == 1, "level prerequisite bypass");
            sim.Apply(Command(p, "attribute", arg: 99)); Require(p.attributePoints == 1, "invalid attribute charged");
        });
        Run("building is atomic across two players", () =>
        {
            var sim = new WorldSimulator(Flat()); var a = Builder(sim, "first", "A"); var b = Builder(sim, "second", "B"); a.x = 29; a.y = 34; b.x = 29; b.y = 35;
            int before = a.items.Sum() + b.items.Sum(); int count = sim.State.buildings.Count;
            sim.Apply(Command(a, "build", 28, 34, (int)BuildKind.Wall)); sim.Apply(Command(b, "build", 28, 34, (int)BuildKind.Wall));
            Require(sim.State.buildings.Count == count + 1, "duplicate placement"); Require(a.items.Sum() + b.items.Sum() == before - 3, "double cost or free build");
        });
        Run("new multi-cell building blocks every cell and rejects subcell overlap", () =>
        {
            var sim = new WorldSimulator(Flat()); var p = Builder(sim); p.x = 8; p.y = 10; sim.Apply(Command(p, "build", 10, 10, (int)BuildKind.Assembler));
            var machine = sim.State.buildings.Single(); Require(machine.footprintVersion == 1, "new construction kept legacy footprint");
            for (int y = 10; y <= 11; y++) for (int x = 10; x <= 12; x++) Require(sim.IsBlocked(x, y), "free machine subcell");
            Require(!sim.IsBlocked(13, 10) && !sim.IsBlocked(12, 12), "footprint blocks outside cells");
            Advance(sim, .3f); p.x = 13; p.y = 11; int before = p.items.Sum(); sim.Apply(Command(p, "build", 12, 11, (int)BuildKind.Wall));
            Require(sim.State.buildings.Count == 1 && p.items.Sum() == before, "overlapping subcell build spent resources");
        });
        Run("rotated footprints reject map-edge overflow without spending", () =>
        {
            foreach (var (x,y,r,legal) in new[] { (62,10,0,false), (62,10,1,true), (10,62,1,false), (10,62,0,true), (63,10,1,false), (10,63,0,false) })
            {
                var sim = new WorldSimulator(Flat()); var p = Builder(sim); p.x = x - 2; p.y = y;
                int before = p.items.Sum(); sim.Apply(Command(p, "build", x, y, (int)BuildKind.Assembler, r));
                Require((sim.State.buildings.Count == 1) == legal, $"edge placement ({x},{y}) rotation {r}");
                Require(p.items.Sum() == before - (legal ? DemoCatalog.BuildCost(BuildKind.Assembler).Sum() : 0), "edge placement charged incorrect cost");
            }
        });
        Run("all footprint cells validate water, nodes, actors and spawn clearance", () =>
        {
            foreach (string obstacle in new[] { "water", "node", "actor", "spawn" })
            {
                var state = Flat(); int x = obstacle == "spawn" ? 29 : 10, y = obstacle == "spawn" ? 34 : 10;
                if (obstacle == "water") { var ground = state.ground.ToCharArray(); ground[12 + 11 * 64] = '2'; state.ground = new string(ground); }
                if (obstacle == "node") Ore(state, 12, 11);
                var sim = new WorldSimulator(state); var p = Builder(sim); p.x = x - 2; p.y = y;
                if (obstacle == "actor") { var other = sim.AddPlayer("other", "Other"); other.x = 12; other.y = 11; }
                int before = p.items.Sum(); sim.Apply(Command(p, "build", x, y, (int)BuildKind.Assembler));
                Require(sim.State.buildings.Count == 0 && p.items.Sum() == before, $"non-anchor {obstacle} ignored");
            }
        });
        Run("subcell interaction and dismantle recover inventory and clear footprint", () =>
        {
            var state = Flat(); var source = Add(state, BuildKind.Assembler, 10, 10); source.items[(int)ItemKind.Ammo] = 9;
            var sim = new WorldSimulator(state); var p = sim.AddPlayer("p", "P"); p.x = 13; p.y = 11; p.items[(int)ItemKind.Iron] = 0;
            int ammo = p.items[(int)ItemKind.Ammo]; sim.Apply(Command(p, "interact", 12, 11));
            Require(p.items[(int)ItemKind.Ammo] == ammo + 9 && Building(sim, source.id).items[(int)ItemKind.Ammo] == 0, "interaction missed non-anchor subcell");
            Advance(sim, .3f); var before = (int[])p.items.Clone(); sim.Apply(Command(p, "dismantle", 12, 11)); Require(sim.State.buildings.Count == 0, "dismantle missed non-anchor subcell");
            var cost = DemoCatalog.BuildCost(BuildKind.Assembler); for (int i = 0; i < DemoCatalog.ItemCount; i++) Require(p.items[i] == before[i] + cost[i] / 2, "dismantle refund changed");
            for (int y = 10; y <= 11; y++) for (int x = 10; x <= 12; x++) Require(!sim.IsBlocked(x, y), "dismantle left ghost occupancy");
        });
        Run("miner searches the expanded whole footprint for ore", () =>
        {
            var state = Flat(); var ore = Ore(state, 12, 11); var gen = Add(state, BuildKind.Generator, 7, 7); gen.fuel = 20;
            var sim = new WorldSimulator(state); var p = Builder(sim); p.x = 8; p.y = 10; sim.Apply(Command(p, "build", 10, 10, (int)BuildKind.Miner));
            Require(sim.State.buildings.Any(b => b.kind == BuildKind.Miner), "ore beside far footprint edge rejected"); Advance(sim, 4);
            Require(sim.State.nodes.Single(n => n.id == ore.id).stock < ore.stock && sim.State.gathered > 0, "far-edge ore was not mined");
        });
        Run("invalid build commands leave inventories and structures unchanged", () =>
        {
            foreach (var (x,y,kind,rotation) in new[] { (10,10,-1,0), (10,10,DemoCatalog.BuildNames.Length,0), (10,10,0,-1), (10,10,0,4), (int.MaxValue,-999,1,0), (50,50,0,0) })
            {
                var sim = new WorldSimulator(Flat()); var p = sim.AddPlayer("p", "P"); p.x = 8; p.y = 10;
                int[] before = (int[])p.items.Clone(); sim.Apply(Command(p, "build", x, y, kind, rotation)); Require(sim.State.buildings.Count == 0 && p.items.SequenceEqual(before), "invalid build accepted/charged");
            }
        });
    }

    static void PowerChecks()
    {
        Run("independent distant power networks do not share generation", () =>
        {
            var state = Flat(); var ga = Add(state, BuildKind.Generator, 4, 4); ga.fuel = 20; Add(state, BuildKind.Generator, 45, 45);
            var a = Add(state, BuildKind.Smelter, 7, 4); a.items[(int)ItemKind.Ore] = 10; var b = Add(state, BuildKind.Smelter, 48, 45); b.items[(int)ItemKind.Ore] = 10;
            var sim = new WorldSimulator(state); Advance(sim, 3); a = Building(sim,a.id); b = Building(sim,b.id);
            Require(a.powered && !b.powered && a.networkId != b.networkId, "distant networks bridged"); Near(a.powerRatio, 1, "active network ratio"); Near(b.powerRatio, 0, "empty network ratio");
            Require(a.items[(int)ItemKind.Iron] > 0 && b.items[(int)ItemKind.Iron] == 0, "remote machine received another network's power");
            Require(sim.State.powerNetworks.Count == 2 && sim.State.powerNetworks.All(n => n.nodes == 1 && n.consumers == 1), "network UI membership");
            Near(sim.State.powerNetworks.Sum(n => n.generation), 120, "network generation total"); Near(sim.State.powerNetworks.Sum(n => n.demand), 70, "network demand total");
        });
        Run("remote machine stops, player-built pole powers it, dismantle disconnects", () =>
        {
            var state = Flat(); var gen = Add(state, BuildKind.Generator, 4, 4); gen.fuel = 20; var target = Add(state, BuildKind.Smelter, 15, 4); target.items[(int)ItemKind.Ore] = 10;
            var sim = new WorldSimulator(state); var p = Builder(sim); p.x = 11; p.y = 6; Advance(sim, 3); var machine = Building(sim,target.id);
            Require(!machine.powered && machine.items[(int)ItemKind.Iron] == 0 && machine.progress == 0, "remote machine ran without pole");
            sim.Apply(Command(p, "build", 11, 4, (int)BuildKind.PowerPole)); Require(sim.State.buildings.Any(b => b.kind == BuildKind.PowerPole), "new pole enum cannot be built");
            Advance(sim, 3); Require(machine.powered && machine.items[(int)ItemKind.Iron] > 0, "pole did not supply remote machine");
            sim.Apply(Command(p, "dismantle", 11, 4)); Require(!sim.State.buildings.Any(b => b.kind == BuildKind.PowerPole), "pole not dismantled");
            int iron = machine.items[(int)ItemKind.Iron]; float progress = machine.progress; Advance(sim, 3);
            Require(!machine.powered && machine.items[(int)ItemKind.Iron] == iron && machine.progress == progress, "removed pole left power ghost");
        });
        Run("power-node links use Euclidean centers and include exact range", () =>
        {
            var state = Flat(); var gen = Add(state, BuildKind.Generator, 3, 10); gen.fuel = 20;
            var a = Add(state, BuildKind.PowerPole, 10, 10); var b = Add(state, BuildKind.PowerPole, 17, 10); var c = Add(state, BuildKind.PowerPole, 24, 11);
            var sim = new WorldSimulator(state); Advance(sim, .05f);
            Require(Building(sim,a.id).networkId == Building(sim,b.id).networkId, "exact seven-cell link omitted"); Require(Building(sim,b.id).networkId != Building(sim,c.id).networkId, "diagonal distance beyond seven linked");
            Require(sim.State.powerNetworks.Count == 2 && sim.State.powerNetworks.Sum(n => n.nodes) == 4, "node component summaries");
        });
        Run("consumer in overlapping coverage attaches once and never bridges grids", () =>
        {
            var state = Flat(); var a = Add(state, BuildKind.Generator, 8, 10); a.fuel = 20; var b = Add(state, BuildKind.Generator, 18, 10);
            var c = Add(state, BuildKind.Assembler, 12, 10); c.items[(int)ItemKind.Iron] = 5; var sim = new WorldSimulator(state); Advance(sim, .05f); c = Building(sim,c.id);
            Require(sim.State.powerNetworks.Count == 2 && Building(sim,a.id).networkId != Building(sim,b.id).networkId, "consumer bridged power nodes");
            Require(c.networkId == Building(sim,a.id).networkId || c.networkId == Building(sim,b.id).networkId, "consumer did not attach to one eligible network");
            Require(sim.State.powerNetworks.Sum(n => n.consumers) == 1, "consumer counted more than once"); Near(sim.State.powerNetworks.Sum(n => n.demand), 45, "overlapping coverage double-counted demand");
            Near(c.powerRatio, sim.State.powerNetworks.Single(n => n.id == c.networkId).ratio, "consumer ratio differs from selected grid");
        });
        Run("supply includes a four-cell footprint gap but excludes a farther diagonal", () =>
        {
            var state = Flat(); var gen = Add(state,BuildKind.Generator,10,10); gen.fuel = 20;
            var edge = Add(state,BuildKind.Smelter,16,10); edge.items[(int)ItemKind.Ore] = 2;
            var diagonal = Add(state,BuildKind.Smelter,16,13); diagonal.items[(int)ItemKind.Ore] = 2;
            var sim = new WorldSimulator(state); Advance(sim,3);
            Require(Building(sim,edge.id).powered && Building(sim,edge.id).items[(int)ItemKind.Iron] > 0, "exact footprint-edge supply radius omitted");
            Require(!Building(sim,diagonal.id).powered && Building(sim,diagonal.id).items[(int)ItemKind.Iron] == 0, "out-of-radius diagonal received power");
            Near(sim.State.powerNetworks.Single().demand,35,"outside consumer charged the grid");
        });
        Run("finite supply shares proportional power and slows observable production", () =>
        {
            var state = Flat(); var gen = Add(state, BuildKind.Generator, 14, 12); gen.fuel = 20;
            foreach (var (x,y) in new[] { (10,10), (10,13), (16,10), (16,13) }) Add(state, BuildKind.Assembler, x, y).items[(int)ItemKind.Iron] = 10;
            var full = Saved(state); full.buildings.RemoveAll(b => b.kind == BuildKind.Assembler && (b.x != 10 || b.y != 10));
            var limitedSim = new WorldSimulator(state); var fullSim = new WorldSimulator(full); Advance(limitedSim, 1); Advance(fullSim, 1);
            var network = limitedSim.State.powerNetworks.Single(); Near(network.generation,120,"generator kW"); Near(network.demand,180,"aggregate kW demand"); Near(network.ratio, 2f / 3f, "limited grid ratio");
            Require(network.nodes == 1 && network.consumers == 4, "limited grid members");
            foreach (var machine in limitedSim.State.buildings.Where(b => b.kind == BuildKind.Assembler))
            {
                Require(machine.powered, "partial supply marked off"); Near(machine.powerRatio, 2f / 3f, "consumer proportional ratio"); Near(machine.powerDemand,45,"machine kW");
                Near(machine.progress, fullSim.State.buildings.Single(b => b.kind == BuildKind.Assembler).progress * 2f / 3f, "partial supply did not slow processing");
            }
            Advance(limitedSim,2.1f); Advance(fullSim,2.1f);
            Require(fullSim.State.manufactured == 4 && limitedSim.State.manufactured == 0, "power ratio did not affect completed output");
        });
        Run("solar output follows the day and stops production at night", () =>
        {
            int bright = 0, dark = 0; float peak = 0;
            for (int phase = 0; phase < 16; phase++)
            {
                var state = Flat(phase * 240f / 16, TechEra.MediumVoltage); var panel = Add(state, BuildKind.SolarPanel, 10, 10); var target = Add(state, BuildKind.ChemicalPlant, 14, 10);
                target.items[(int)ItemKind.Wood] = 10; target.items[(int)ItemKind.Fuel] = 10;
                var sim = new WorldSimulator(state); Advance(sim, .05f); panel = Building(sim,panel.id); target = Building(sim,target.id);
                Require(float.IsFinite(panel.powerGeneration) && panel.powerGeneration >= 0 && panel.powerGeneration <= 40.001f, "solar output outside 0..40 kW");
                peak = Math.Max(peak,panel.powerGeneration); Near(target.powerRatio,Math.Min(1,panel.powerGeneration/80),"solar consumer ratio");
                if (panel.powerGeneration <= .001f) { dark++; Require(!target.powered && target.progress == 0, "solar powered factory at night"); }
                else { bright++; Require(target.powered && target.progress > 0, "sunlit factory did not run"); }
            }
            Require(bright >= 2 && dark >= 2 && peak > 35, "solar has no useful day/night cycle");
        });
        Run("generator fuel lasts twenty seconds and passive devices need no supply", () =>
        {
            var state = Flat(); var gen = Add(state, BuildKind.Generator, 10, 10); gen.items[(int)ItemKind.Fuel] = 2;
            Add(state, BuildKind.Belt, 45, 45); Add(state, BuildKind.Chest, 47, 45); Add(state, BuildKind.Wall, 49, 45); var sim = new WorldSimulator(state); Advance(sim, 19); gen = Building(sim,gen.id);
            Require(gen.items[(int)ItemKind.Fuel] == 1 && gen.fuel > 0 && gen.fuel <= 1.01f, "one fuel did not last twenty seconds"); Near(gen.powerGeneration,120,"fueled generator output");
            Advance(sim,2); Require(gen.items[(int)ItemKind.Fuel] == 0 && gen.fuel > 18, "next fuel not committed once");
            foreach (var passive in sim.State.buildings.Where(b => b.kind != BuildKind.Generator)) Require(passive.powered && passive.powerDemand == 0, "passive device requires electricity");
        });
    }

    static void CargoChecks()
    {
        Run("cargo leaves source, moves through half flight and arrives once", () =>
        {
            var state = Flat(); var source = Add(state, BuildKind.Belt, 10, 10); source.items[(int)ItemKind.Ore] = 1; var target = Add(state, BuildKind.Chest, 11, 10);
            var sim = new WorldSimulator(state); var expected = ItemTotals(sim.State); var flight = AwaitCargo(sim,source.id,target.id);
            Require(Building(sim,source.id).items.Sum() == 0 && Building(sim,target.id).items.Sum() == 0, "in-flight item still in an inventory");
            Near(flight.fromX,10,"flight origin x"); Near(flight.fromY,10,"flight origin y"); Near(flight.toX,11,"flight destination x"); Near(flight.toY,10,"flight destination y"); Near(flight.duration,1f/DemoCatalog.CargoSpeed,"flight duration");
            for (int n = 0; n < 20 && flight.progress < .4f; n++) { sim.Step(WorldSimulator.FixedStep); Conserved(sim.State,expected); }
            Require(flight.progress >= .4f && flight.progress <= .65f && sim.State.cargo.Contains(flight), "no visible half-flight state");
            Near(flight.fromX + (flight.toX-flight.fromX)*flight.progress,10+flight.progress,"half-flight position"); Require(Building(sim,target.id).items.Sum() == 0, "destination credited before arrival");
            float progress = flight.progress; sim.Step(WorldSimulator.FixedStep); Near(flight.progress-progress,WorldSimulator.FixedStep/flight.duration,"progress is not simulation-time based");
            Advance(sim,1); Conserved(sim.State,expected); Require(sim.State.cargo.Count == 0 && Building(sim,target.id).items[(int)ItemKind.Ore] == 1, "arrival lost/duplicated item");
        });
        Run("one item cannot cross several belts instantly", () =>
        {
            var state = Flat(); var a = Add(state, BuildKind.Belt, 10, 10); a.items[(int)ItemKind.Ore] = 1; var b = Add(state, BuildKind.Belt, 11, 10); Add(state, BuildKind.Belt, 12, 10); var sink = Add(state, BuildKind.Chest, 13, 10);
            var sim = new WorldSimulator(state); int[] expected = ItemTotals(sim.State); AwaitCargo(sim,a.id,b.id);
            Require(sim.State.cargo.Count == 1 && sim.State.cargo[0].fromId == a.id && Building(sim,sink.id).items.Sum() == 0, "first dispatch skipped a belt"); float launch = sim.State.elapsed;
            for (int n = 0; n < 60; n++)
            {
                sim.Step(WorldSimulator.FixedStep); Conserved(sim.State,expected);
                if (sim.State.elapsed - launch < 3 / DemoCatalog.CargoSpeed - WorldSimulator.FixedStep - .001f) Require(Building(sim,sink.id).items.Sum() == 0, "item traversed route faster than physical flights");
            }
            Require(Building(sim,sink.id).items[(int)ItemKind.Ore] == 1, "route never delivered");
        });
        Run("transport accepts non-anchor target footprint cells in four directions", () =>
        {
            foreach (var (r,x,y) in new[] { (0,11,9), (1,9,11), (2,8,9), (3,9,8) })
            {
                var state = Flat(); var source = Add(state, BuildKind.Belt, 10, 10, r); source.items[(int)ItemKind.Ore] = 1; var target = Add(state, BuildKind.Smelter, x, y);
                var sim = new WorldSimulator(state); int[] expected = ItemTotals(sim.State); for (int n = 0; n < 50; n++) { sim.Step(WorldSimulator.FixedStep); Conserved(sim.State,expected); }
                Require(Building(sim,target.id).items[(int)ItemKind.Ore] == 1, $"rotation {r} could not target occupied subcell");
            }
        });
        Run("rotated multi-cell sources export from their outside port", () =>
        {
            foreach (var (r,x,y) in new[] { (0,13,10), (1,10,13), (2,9,10), (3,10,9) })
            {
                var state = Flat(); var source = Add(state, BuildKind.Assembler, 10, 10, r); source.items[(int)ItemKind.Ammo] = 1; var target = Add(state, BuildKind.Chest, x, y);
                var sim = new WorldSimulator(state); int[] expected = ItemTotals(sim.State); for (int n = 0; n < 60; n++) { sim.Step(WorldSimulator.FixedStep); Conserved(sim.State,expected); }
                Require(Building(sim,target.id).items[(int)ItemKind.Ammo] == 1, $"rotation {r} output missed outside port");
            }
        });
        Run("full belt blocks dispatch and resumes after one slot is freed", () =>
        {
            var state = Flat(); var a = Add(state, BuildKind.Belt, 10, 10); a.items[(int)ItemKind.Ore] = 1; var b = Add(state, BuildKind.Belt, 11, 10, 1); b.items[(int)ItemKind.Iron] = 2;
            var sim = new WorldSimulator(state); var expected = ItemTotals(sim.State); Advance(sim,1); Conserved(sim.State,expected);
            Require(Building(sim,a.id).items[(int)ItemKind.Ore] == 1 && sim.State.cargo.Count == 0, "dispatched into full belt"); Building(sim,b.id).items[(int)ItemKind.Iron]--; expected[(int)ItemKind.Iron]--;
            for (int n = 0; n < 30; n++) { sim.Step(WorldSimulator.FixedStep); Conserved(sim.State,expected); }
            Require(Building(sim,b.id).items.Sum() == 2 && Building(sim,a.id).items.Sum() == 0, "freed capacity did not resume transport");
        });
        Run("multiple mixed inputs reserve a belt's shared last slot", () =>
        {
            var state = Flat(); var target = Add(state, BuildKind.Belt, 11, 10, 3); target.items[(int)ItemKind.Ammo] = 1;
            Add(state,BuildKind.Belt,10,10,0).items[(int)ItemKind.Ore] = 1; Add(state,BuildKind.Belt,11,11,3).items[(int)ItemKind.Iron] = 1; Add(state,BuildKind.Belt,12,10,2).items[(int)ItemKind.Fuel] = 1;
            var sim = new WorldSimulator(state); var expected = ItemTotals(sim.State); bool sawFlight = false;
            for (int n = 0; n < 40; n++) { sim.Step(WorldSimulator.FixedStep); Conserved(sim.State,expected); int incoming = sim.State.cargo.Count(c => c.toId == target.id); sawFlight |= incoming > 0; Require(incoming <= 1, "multiple senders claimed one belt slot"); }
            Require(sawFlight && Building(sim,target.id).items.Sum() == 2 && sim.State.buildings.Where(b => b.id != target.id).Sum(b => b.items.Sum()) == 2, "mixed-input contention lost/created items");
        });
        Run("multiple inputs reserve per-item buffer capacity including flights", () =>
        {
            var state = Flat(); var target = Add(state,BuildKind.Chest,11,10); target.items[(int)ItemKind.Ore] = WorldSimulator.BufferLimit-1;
            Add(state,BuildKind.Belt,10,10,0).items[(int)ItemKind.Ore] = 1; Add(state,BuildKind.Belt,11,11,3).items[(int)ItemKind.Ore] = 1; Add(state,BuildKind.Belt,12,10,2).items[(int)ItemKind.Ore] = 1;
            var sim = new WorldSimulator(state); var expected = ItemTotals(sim.State); bool sawFlight = false;
            for (int n = 0; n < 40; n++) { sim.Step(WorldSimulator.FixedStep); Conserved(sim.State,expected); sawFlight |= sim.State.cargo.Count > 0; }
            Require(sawFlight && Building(sim,target.id).items[(int)ItemKind.Ore] == WorldSimulator.BufferLimit, "buffer never filled");
            Require(sim.State.buildings.Where(b => b.id != target.id).Sum(b => b.items.Sum()) == 2, "reserved buffer overfilled or dropped source items");
        });
        Run("cyclic mixed logistics conserve every item regardless of building order", () =>
        {
            foreach (bool reverse in new[] { false,true })
            {
                var state = Flat(); Add(state,BuildKind.Belt,10,10,0).items[(int)ItemKind.Ore] = 1; Add(state,BuildKind.Belt,11,10,1).items[(int)ItemKind.Iron] = 1;
                Add(state,BuildKind.Belt,11,11,2).items[(int)ItemKind.Fuel] = 1; Add(state,BuildKind.Belt,10,11,3).items[(int)ItemKind.Ammo] = 1; if (reverse) state.buildings.Reverse();
                var sim = new WorldSimulator(state); var expected = ItemTotals(sim.State); bool sawFlight = false;
                for (int n = 0; n < 240; n++) { sim.Step(WorldSimulator.FixedStep); Conserved(sim.State,expected); sawFlight |= sim.State.cargo.Count > 0; } Require(sawFlight, "cyclic logistics made no progress");
            }
        });
        Run("full-size logistics stay bounded and conserve inventories plus cargo", () =>
        {
            var state = Flat(); for (int y = 0; y < 16; y++) for (int x = 0; x < 32; x++) { Add(state,BuildKind.Belt,x*2,y*2).items[(int)ItemKind.Ore] = 2; Add(state,BuildKind.Chest,x*2+1,y*2,1); }
            var sim = new WorldSimulator(state); var expected = ItemTotals(sim.State); int peak = 0;
            for (int n = 0; n < 40; n++) { sim.Step(WorldSimulator.FixedStep); peak = Math.Max(peak,sim.State.cargo.Count); Conserved(sim.State,expected); }
            Require(peak > 0 && sim.State.cargo.Count == 0 && sim.State.buildings.Where(b => b.kind == BuildKind.Chest).Sum(b => b.items.Sum()) == 1024, "bounded logistics did not eventually deliver every item");
        });
        Run("1024 simultaneous flights preserve the cargo limit and save contract", () =>
        {
            var state = Flat();
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
            {
                Add(state,BuildKind.Belt,x*2,y*2,0).items[(int)ItemKind.Ore] = 1;
                Add(state,BuildKind.Belt,x*2+1,y*2,1).items[(int)ItemKind.Ore] = 1;
                Add(state,BuildKind.Belt,x*2+1,y*2+1,2).items[(int)ItemKind.Ore] = 1;
                Add(state,BuildKind.Belt,x*2,y*2+1,3).items[(int)ItemKind.Ore] = 1;
            }
            var sim = new WorldSimulator(state); int[] expected = ItemTotals(sim.State); int peak = 0;
            for (int n = 0; n < 30; n++) { sim.Step(WorldSimulator.FixedStep); peak = Math.Max(peak,sim.State.cargo.Count); Conserved(sim.State,expected); }
            Require(peak == WorldSimulator.MaxCargo, "stress fixture did not reach the cargo limit");
            AwaitCargo(sim,state.buildings[0].id,state.buildings[1].id);
            var restored = new WorldSimulator(Saved(sim.State)); Conserved(restored.State,expected);
            Require(restored.State.cargo.Count == WorldSimulator.MaxCargo, "maximum-flight save lost packets");
        });
        Run("a restored arrived packet cannot forward again in the same tick", () =>
        {
            var state = Flat(); var from = Add(state,BuildKind.Belt,10,10); var middle = Add(state,BuildKind.Belt,11,10); var sink = Add(state,BuildKind.Chest,12,10);
            state.cargo.Add(new CargoState { id = state.nextId++, fromId = from.id, toId = middle.id, item = (int)ItemKind.Ore, fromX = 10, fromY = 10, toX = 11, toY = 10, progress = 1, duration = 1f/DemoCatalog.CargoSpeed });
            var sim = new WorldSimulator(state); var expected = ItemTotals(sim.State); sim.Step(WorldSimulator.FixedStep); Conserved(sim.State,expected);
            Require(Building(sim,middle.id).items[(int)ItemKind.Ore] == 1 && Building(sim,sink.id).items.Sum() == 0 && sim.State.cargo.Count == 0, "arrival was re-exported in the same tick");
            Advance(sim,1); Conserved(sim.State,expected); Require(Building(sim,sink.id).items[(int)ItemKind.Ore] == 1, "restored arrival never resumed");
        });
        Run("manual machine loading respects capacity reserved by an incoming packet", () =>
        {
            var state = Flat(); var source = Add(state,BuildKind.Belt,10,10); source.items[(int)ItemKind.Ore] = 1;
            var target = Add(state,BuildKind.Smelter,11,9); target.items[(int)ItemKind.Ore] = WorldSimulator.BufferLimit-1;
            var sim = new WorldSimulator(state); var p = sim.AddPlayer("p","P"); p.x = 13; p.y = 10; p.items[(int)ItemKind.Ore] = 20;
            AwaitCargo(sim,source.id,target.id); var expected = ItemTotals(sim.State); sim.Apply(Command(p,"interact",12,10));
            Require(p.items[(int)ItemKind.Ore] == 20 && Building(sim,target.id).items[(int)ItemKind.Ore] == WorldSimulator.BufferLimit-1, "manual loading stole the flight reservation");
            for (int n = 0; n < 20; n++) { sim.Step(WorldSimulator.FixedStep); Conserved(sim.State,expected); }
            Require(Building(sim,target.id).items[(int)ItemKind.Ore] == WorldSimulator.BufferLimit, "reserved delivery did not arrive");
        });
        Run("starter chain respects direction and conserves recipe material in flight", () =>
        {
            // Recreate the old tutorial as an explicit LV fixture, rather than prebuilding it in a new game.
            var state = Flat(); Ore(state,24,30,1600);
            Add(state,BuildKind.Miner,25,30); Add(state,BuildKind.Belt,27,30); Add(state,BuildKind.Smelter,28,30);
            Add(state,BuildKind.Belt,30,30); Add(state,BuildKind.Assembler,31,30); Add(state,BuildKind.Chest,35,30);
            Add(state,BuildKind.Generator,27,26).items[(int)ItemKind.Fuel] = 8; Add(state,BuildKind.PowerPole,31,28);
            var sim = new WorldSimulator(state); var p = Builder(sim); p.x = 34; p.y = 32; sim.Apply(Command(p,"build",34,30,(int)BuildKind.Belt));
            var belt = sim.State.buildings.Single(b => b.kind == BuildKind.Belt && b.x == 34 && b.y == 30); belt.rotation = 2; Advance(sim,15); var chest = sim.State.buildings.Single(b => b.kind == BuildKind.Chest);
            Require(chest.items[(int)ItemKind.Ammo] == 0, "wrong-direction belt delivered ammo"); belt.rotation = 0; int initialOre = sim.State.nodes.Single(n => n.x == 24 && n.y == 30).stock + sim.State.gathered;
            for (int n = 0; n < 900; n++)
            {
                sim.Step(WorldSimulator.FixedStep); int mined = initialOre - sim.State.nodes.Single(n => n.x == 24 && n.y == 30).stock; int[] totals = ItemTotals(sim.State);
                Require(4*totals[(int)ItemKind.Ore]+4*totals[(int)ItemKind.Iron]+totals[(int)ItemKind.Ammo] == 4*mined, "ore/iron/ammo plus in-flight cargo violated recipes");
                Require(sim.State.buildings.All(b => b.items.All(v => v >= 0)), "negative machine inventory");
            }
            Require(chest.items[(int)ItemKind.Ammo] > 0 && sim.State.manufactured > 0, "starter chain did not deliver ammo");
        });
    }

    static void SaveAndCombatChecks()
    {
        Run("flight save roundtrip preserves timing, identity, conservation and deep copy", () =>
        {
            var state = Flat(); var source = Add(state,BuildKind.Belt,10,10); source.items[(int)ItemKind.Ore] = 1; var sink = Add(state,BuildKind.Chest,11,10);
            var sim = new WorldSimulator(state); var flight = AwaitCargo(sim,source.id,sink.id); Advance(sim,.1f); Require(flight.progress > 0 && flight.progress < 1, "save fixture is not mid-flight");
            var saved = Saved(sim.State); string serializedFlight = JsonSerializer.Serialize(saved.cargo,Json); var restored = new WorldSimulator(saved);
            Require(JsonSerializer.Serialize(restored.State.cargo,Json) == serializedFlight, "restore changed flight fields");
            Require(!ReferenceEquals(saved.cargo,restored.State.cargo) && !ReferenceEquals(saved.cargo[0],restored.State.cargo[0]), "restore aliases cargo");
            Require(!ReferenceEquals(saved.buildings[0].items,restored.State.buildings[0].items), "restore aliases building inventory"); saved.cargo[0].progress = .99f; saved.buildings[0].items[(int)ItemKind.Ore] = 99;
            Require(restored.State.cargo[0].progress != .99f && restored.State.buildings[0].items[(int)ItemKind.Ore] == 0, "caller mutation leaked into restored world"); int[] expected = ItemTotals(sim.State);
            for (int n = 0; n < 30; n++) { sim.Step(WorldSimulator.FixedStep); restored.Step(WorldSimulator.FixedStep); Conserved(sim.State,expected); Conserved(restored.State,expected); }
            Require(Dump(sim.State) == Dump(restored.State) && Building(restored,sink.id).items[(int)ItemKind.Ore] == 1, "saved flight diverged or credited twice");
        });
        Run("power save roundtrip deep-copies summaries and recomputes supply", () =>
        {
            var state = Flat(); var gen = Add(state,BuildKind.Generator,10,10); gen.fuel = 20; Add(state,BuildKind.Smelter,13,10).items[(int)ItemKind.Ore] = 2;
            var sim = new WorldSimulator(state); Advance(sim,.1f); var saved = Saved(sim.State); var restored = new WorldSimulator(saved);
            Require(restored.State.powerNetworks.Count == 1 && !ReferenceEquals(saved.powerNetworks,restored.State.powerNetworks) && !ReferenceEquals(saved.powerNetworks[0],restored.State.powerNetworks[0]), "restore aliases power summary");
            saved.powerNetworks[0].generation = -1; saved.buildings[0].fuel = 0; Near(restored.State.powerNetworks[0].generation,120,"caller mutated network summary"); Advance(restored,.05f);
            Require(restored.State.buildings.Single(b => b.kind == BuildKind.Smelter).powered, "saved network lost power");
        });
        Run("v1 JSON keeps old single cells alongside newly built v4 footprints", () =>
        {
            var state = Flat(); state.version = 1; Add(state,BuildKind.Assembler,10,10,footprintVersion:0); Add(state,BuildKind.Belt,11,10,footprintVersion:0);
            foreach(var building in state.buildings) building.items = new int[7];
            var root = JsonNode.Parse(Dump(state)).AsObject(); root.Remove("cargo"); root.Remove("powerNetworks");
            foreach (var node in root["buildings"].AsArray()) foreach (string field in new[] { "footprintVersion","networkId","powerRatio","powerDemand","powerGeneration" }) node.AsObject().Remove(field);
            var legacy = JsonSerializer.Deserialize<WorldState>(root.ToJsonString(),Json); var sim = new WorldSimulator(legacy);
            Require(sim.State.version == WorldSimulator.SaveVersion && sim.State.buildings.All(b => b.footprintVersion == 0), "v1 migration expanded buildings or did not upgrade version"); Require(sim.IsBlocked(10,10) && !sim.IsBlocked(10,11), "legacy assembler does not remain one cell");
            var p = Builder(sim); p.x = 8; p.y = 11; sim.Apply(Command(p,"build",10,11,(int)BuildKind.Generator)); var built = sim.State.buildings.Single(b => b.kind == BuildKind.Generator);
            Require(built.footprintVersion == 1 && sim.IsBlocked(11,12), "new building in migrated save is not multi-cell"); var restored = new WorldSimulator(Saved(sim.State));
            Require(restored.State.buildings.Count == 3 && restored.State.buildings.Count(b => b.footprintVersion == 0) == 2 && DemoCatalog.Width(Building(restored,built.id)) == 2, "mixed-footprint roundtrip changed dimensions");
        });
        Run("save validation rejects footprint overlap, overflow and unsupported version", () =>
        {
            var outside = Flat(); Add(outside,BuildKind.Assembler,63,10); Reject(outside,"out-of-map footprint accepted"); var rotated = Flat(); Add(rotated,BuildKind.Assembler,10,62,1); Reject(rotated,"rotated out-of-map footprint accepted");
            var overlap = Flat(); Add(overlap,BuildKind.Generator,10,10); Add(overlap,BuildKind.Wall,11,11); Reject(overlap,"overlapping saved subcells accepted");
            var nodeOverlap = Flat(); Add(nodeOverlap,BuildKind.Generator,10,10); Ore(nodeOverlap,11,11); Reject(nodeOverlap,"node inside saved footprint accepted");
            var water = Flat(); Add(water,BuildKind.Generator,10,10); var tiles = water.ground.ToCharArray(); tiles[11+11*64] = '2'; water.ground = new string(tiles); Reject(water,"saved footprint water subcell accepted");
            var future = Flat(); future.version = WorldSimulator.SaveVersion + 1; Reject(future,"unknown future version accepted"); var format = Flat(); Add(format,BuildKind.Generator,10,10,footprintVersion:2); Reject(format,"unknown footprint version accepted"); Reject(new WorldState { width = 4096 },"malformed world accepted");
        });
        Run("save validation rejects corrupt flight timing, item and duplicate id", () =>
        {
            var state = Flat(); var a = Add(state,BuildKind.Belt,10,10); a.items[(int)ItemKind.Ore] = 1; var b = Add(state,BuildKind.Chest,11,10); var sim = new WorldSimulator(state); AwaitCargo(sim,a.id,b.id);
            foreach (var corrupt in new Action<CargoState>[] { c => c.duration = 0, c => c.duration = float.NaN, c => c.progress = -.01f, c => c.progress = 1.01f, c => c.fromX = float.PositiveInfinity, c => c.item = 25 })
            { var invalid = Saved(sim.State); corrupt(invalid.cargo[0]); Reject(invalid,"corrupt cargo accepted"); }
            var duplicate = Saved(sim.State); duplicate.cargo.Add(Saved(sim.State).cargo[0]); Reject(duplicate,"duplicate cargo id accepted");
        });
        Run("save roundtrip isolates players, combat entities and held input", () =>
        {
            var sim = New(out var p); sim.Apply(new PlayerCommand { playerId = p.id, action = "attack", aimX = 1 }); sim.Apply(new PlayerCommand { playerId = p.id, moveX = 1 }); Advance(sim,.1f);
            var saved = Saved(sim.State); var restored = new WorldSimulator(saved); Require(restored.State.seed == sim.State.seed && restored.State.tick == sim.State.tick, "roundtrip world");
            Require(restored.State.players[0].items.SequenceEqual(p.items) && restored.State.zombies.Count == sim.State.zombies.Count && restored.State.traces.Count == sim.State.traces.Count, "roundtrip inventory/combat");
            Require(!ReferenceEquals(saved.players[0].items,restored.State.players[0].items) && !ReferenceEquals(saved.players[0].attributes,restored.State.players[0].attributes) && !ReferenceEquals(saved.players[0].skills,restored.State.players[0].skills), "restore aliases player arrays");
            Require(!ReferenceEquals(saved.zombies[0],restored.State.zombies[0]), "restore aliases zombies"); if (saved.traces.Count > 0) Require(!ReferenceEquals(saved.traces[0],restored.State.traces[0]), "restore aliases traces");
            restored.State.players[0].items[0] = 998; Require(p.items[0] != 998 && saved.players[0].items[0] != 998, "restore inventory alias"); float x = restored.State.players[0].x, y = restored.State.players[0].y; Advance(restored,.5f);
            Near(restored.State.players[0].x,x,"restored held input x"); Near(restored.State.players[0].y,y,"restored held input y");
        });
        Run("wave scheduling and survival invariants", () =>
        {
            var sim = New(out var p);
            for (int n = 0; n < 3800; n++)
            {
                if (p.hp <= 0) sim.Apply(Command(p,"respawn")); if (n % 10 == 0) sim.Apply(new PlayerCommand { playerId = p.id, action = "attack", aimX = 1, aimY = 0 }); sim.Step(WorldSimulator.FixedStep);
            }
            Require(sim.State.wave >= 1 && sim.State.nextWaveAt > sim.State.elapsed, "wave did not schedule"); Require(sim.State.zombies.Count <= WorldSimulator.MaxZombies && sim.State.traces.Count <= WorldSimulator.MaxTraces && sim.State.cargo.Count <= WorldSimulator.MaxCargo, "unbounded entities");
            Require(p.items.All(v => v >= 0) && p.hp >= 0 && float.IsFinite(p.x), "survival invariant"); _ = new WorldSimulator(Saved(sim.State));
        });
    }
}
