using DeadCircuit;
using System.Text.Json.Nodes;
using System.Text.Json;

static partial class Checks
{
    // Public seam: restore caller-owned save graphs with WorldSimulator(WorldState).
    // Program.Main registers PrimitiveSaveChecks alongside the other save checks.
    static WorldState PrimitiveVersionTenFixture()
    {
        var state = Flat();
        state.version = 10;
        WorldGeneration.LegacyMetadata(state);
        state.produced = Enumerable.Range(1, 25).ToArray();
        var from = Add(state, BuildKind.Belt, 10, 10);
        from.items = new int[25]; from.items[(int)ItemKind.Fuel] = 1;
        var chest = Add(state, BuildKind.Chest, 11, 10);
        chest.items = Enumerable.Range(21, 25).ToArray();
        state.players.Add(new PlayerState { id = "primitive-save", name = "Save", x = 32, y = 35,
            items = Enumerable.Range(11, 25).ToArray() });
        Ore(state, 3, 3, 51);
        state.cargo.Add(new CargoState { id = state.nextId++, fromId = from.id, toId = chest.id,
            item = (int)ItemKind.Wood, fromX = 10, fromY = 10, toX = 11, toY = 10, progress = .5f, duration = .4f });
        // These fields did not exist in v10 JSON; do not relabel a current graph.
        var json = JsonNode.Parse(Dump(state)).AsObject();
        json.Remove("primitiveVersion"); json.Remove("primitiveResearch");
        return JsonSerializer.Deserialize<WorldState>(json.ToJsonString(), Json);
    }

    static WorldState PrimitiveCurrentSaveFixture(int research = 255)
    {
        var state = Flat(); state.version = 11; state.primitiveVersion = 1; state.primitiveResearch = research;
        WorldGeneration.LegacyMetadata(state);
        state.produced = Enumerable.Range(1, 29).ToArray();
        Add(state, BuildKind.Chest, 10, 10).items = Enumerable.Range(21, 29).ToArray();
        state.players.Add(new PlayerState { id = "primitive-save", name = "Save", x = 32, y = 35,
            items = Enumerable.Range(11, 29).ToArray() });
        return state;
    }

    static bool PrimitiveMigratedItems(int[] current,int[] old) => current.Length==DemoCatalog.ItemCount && current.Take(old.Length).SequenceEqual(old) && current.Skip(old.Length).All(n=>n==0);

    static void PrimitiveSaveChecks()
    {
        RunHistorical("primitive save genuine v10 25-slot migration conserves inventories production cargo and resources", () =>
        {
            var source = PrimitiveVersionTenFixture(); string before = Dump(source);
            var sim = new WorldSimulator(source); var state = sim.State;
            Require(state.version == WorldSimulator.SaveVersion && state.primitiveVersion == 0 && state.primitiveResearch == 0, "v10 migration enabled primitive locks/research");
            Require(Dump(source) == before, "v10 migration mutated its caller");
            Require(state.produced.Length == DemoCatalog.ItemCount && state.produced.Take(25).SequenceEqual(source.produced) && state.produced.Skip(25).All(n => n == 0), "v10 production history changed");
            Require(state.players[0].items.Length == DemoCatalog.ItemCount && state.players[0].items.Take(25).SequenceEqual(source.players[0].items) && state.players[0].items.Skip(25).All(n => n == 0), "v10 player migration lost or gifted inventory");
            foreach (var old in source.buildings)
            {
                var current = state.buildings.Single(b => b.id == old.id);
                Require(current.items.Length == DemoCatalog.ItemCount && current.items.Take(25).SequenceEqual(old.items) && current.items.Skip(25).All(n => n == 0), "v10 machine migration lost or gifted inventory");
                Require(!ReferenceEquals(old.items, current.items), "v10 machine inventory aliases caller");
            }
            Require(!ReferenceEquals(source.produced, state.produced) && !ReferenceEquals(source.players[0].items, state.players[0].items), "v10 migration aliases production/player arrays");
            Require(JsonSerializer.Serialize(source.nodes, Json) == JsonSerializer.Serialize(state.nodes, Json) && state.ground == source.ground && state.nextId == source.nextId, "v10 restore regenerated resources or identity");
            Require(JsonSerializer.Serialize(source.cargo, Json) == JsonSerializer.Serialize(state.cargo, Json), "v10 migration changed in-flight cargo");
            var totals = ItemTotals(state); int wood = state.buildings.Single(b => b.kind == BuildKind.Chest).items[(int)ItemKind.Wood];
            for (int tick = 0; tick < 6; tick++) { sim.Step(WorldSimulator.FixedStep); Conserved(state, totals); }
            Require(state.buildings.Single(b => b.kind == BuildKind.Chest).items[(int)ItemKind.Wood] == wood + 1, "v10 migration lost or duplicated cargo delivery");
            string isolated = Dump(state);
            source.produced[0]++; source.players[0].items[0]++; source.buildings[0].items[0]++; source.nodes[0].stock--;
            Require(Dump(state) == isolated, "v10 caller mutation reached restored state");
            var again = new WorldSimulator(Saved(state)).State;
            Require(again.primitiveVersion == 0 && again.primitiveResearch == 0 && again.produced.SequenceEqual(state.produced), "migrated v10 lost bypass on second restore");
        });
        RunHistorical("primitive save v4 through v9 retain genuine 25-slot inventories and research bypass", () =>
        {
            for (int version = 4; version <= 9; version++)
            {
                var source = PrimitiveVersionTenFixture(); source.version = version;
                string before = Dump(source); var state = new WorldSimulator(source).State;
                Require(state.version == WorldSimulator.SaveVersion && state.primitiveVersion == 0 && state.primitiveResearch == 0, "old save enabled new research gates: v" + version);
                Require(state.produced.Length == DemoCatalog.ItemCount && state.produced.Take(25).SequenceEqual(source.produced) && state.produced.Skip(25).All(n => n == 0), "old production migration: v" + version);
                Require(state.players[0].items.Length == DemoCatalog.ItemCount && state.players[0].items.Take(25).SequenceEqual(source.players[0].items) && state.players[0].items.Skip(25).All(n => n == 0), "old player migration: v" + version);
                Require(state.buildings.All(b => b.items.Length == DemoCatalog.ItemCount && b.items.Skip(25).All(n => n == 0)), "old machine migration: v" + version);
                Require(Dump(source) == before && state.nextId == source.nextId && state.nodes.Count == source.nodes.Count, "old migration mutated caller or regenerated nodes: v" + version);
            }
        });
        RunHistorical("primitive save v11 research and appended inventories roundtrip with deep copies", () =>
        {
            foreach (int research in new[] { 0, 255 })
            {
                var source = PrimitiveCurrentSaveFixture(research); string before = Dump(source);
                var state = new WorldSimulator(source).State; var again = new WorldSimulator(Saved(state)).State;
                Require(state.version == WorldSimulator.SaveVersion && state.primitiveVersion == 1 && state.primitiveResearch == research && again.primitiveVersion == 1 && again.primitiveResearch == research, "v11 primitive progression changed on roundtrip");
                Require(Dump(source) == before && Dump(state) == Dump(again), "v11 restore mutated input or changed second restore");
                Require(PrimitiveMigratedItems(state.produced,source.produced) && PrimitiveMigratedItems(state.players[0].items,source.players[0].items) && PrimitiveMigratedItems(state.buildings[0].items,source.buildings[0].items), "v11 restore lost appended inventory/history");
                Require(!ReferenceEquals(source.produced, state.produced) && !ReferenceEquals(source.players[0].items, state.players[0].items) && !ReferenceEquals(source.buildings[0].items, state.buildings[0].items), "v11 restore aliases caller arrays");
                string isolated = Dump(state);
                source.primitiveResearch = 0; source.primitiveVersion = 0; source.produced[28]++; source.players[0].items[25]++; source.buildings[0].items[27]++;
                again.produced[26]++; again.players[0].items[27]++; again.buildings[0].items[28]++;
                Require(Dump(state) == isolated, "v11 restored state shares mutable data with caller or second restore");
            }
        });
        RunHistorical("primitive save v11 rejects invalid version bits and bypass research", () =>
        {
            foreach (int version in new[] { -1, 2, int.MinValue, int.MaxValue })
            {
                var invalid = PrimitiveCurrentSaveFixture(0); invalid.primitiveVersion = version;
                Reject(invalid, "invalid primitive version accepted: " + version);
            }
            foreach (int research in new[] { -1, 256, 511, int.MinValue, int.MaxValue })
            {
                var invalid = PrimitiveCurrentSaveFixture(); invalid.primitiveResearch = research;
                Reject(invalid, "invalid primitive bits accepted: " + research);
            }
            var bypass = PrimitiveCurrentSaveFixture(1); bypass.primitiveVersion = 0;
            Reject(bypass, "primitiveVersion0 accepted research");
            bypass.primitiveResearch = 0;
            Require(new WorldSimulator(bypass).State.primitiveVersion == 0, "v11 valid migrated bypass rejected");
        });
        RunHistorical("primitive save v11 rejects missing direct and transitive research prerequisites", () =>
        {
            // The six-step metallurgy chain occupies bits 0..5; construction bit6
            // needs Tools, while Cooking bit7 needs Ceramics and its ancestors.
            foreach (int research in new[] { 0, 1, 3, 7, 15, 31, 63, 65, 67, 71, 135, 255 })
            {
                var state = PrimitiveCurrentSaveFixture(research);
                Require(new WorldSimulator(state).State.primitiveResearch == research, "valid closed research set changed: " + research);
            }
            foreach (int research in new[] { 2, 4, 8, 16, 32, 64, 128, 5, 9, 17, 33, 129, 6, 14, 30, 62, 254, 251 })
                Reject(PrimitiveCurrentSaveFixture(research), "research dependencies missing for bits " + research);
        });
        RunHistorical("primitive save rejects wrong historical and v11 item array lengths and values", () =>
        {
            foreach (int version in new[] { 10, 11 })
            {
                Func<WorldState> fixture = version == 10 ? PrimitiveVersionTenFixture : () => PrimitiveCurrentSaveFixture();
                int count = version == 10 ? 25 : 29;
                foreach (int length in new[] { 0, 7, 21, 24, 25, 26, 28, 29, 30 }.Where(n => n != count))
                {
                    foreach (var corrupt in new Action<WorldState>[] { s => s.produced = new int[length], s => s.players[0].items = new int[length], s => s.buildings.Single(b => b.kind == BuildKind.Chest).items = new int[length] })
                    {
                        var invalid = fixture();
                        corrupt(invalid); Reject(invalid, $"v{version} accepted item array length {length}");
                    }
                }
                foreach (var corrupt in new Action<WorldState>[] { s => s.produced = null, s => s.players[0].items = null, s => s.buildings[0].items = null,
                    s => s.produced[count - 1] = -1, s => s.players[0].items[count - 1] = -1, s => s.players[0].items[count - 1] = WorldSimulator.InventoryLimit + 1,
                    s => s.buildings.Single(b => b.kind == BuildKind.Chest).items[count - 1] = -1, s => s.buildings.Single(b => b.kind == BuildKind.Chest).items[count - 1] = WorldSimulator.BufferLimit + 1 })
                { var invalid = fixture(); corrupt(invalid); Reject(invalid, $"v{version} accepted null/invalid item array"); }
            }
        });
        RunHistorical("primitive save new nodes kilns and four-second recipes require v11", () =>
        {
            foreach (int kind in new[] { 8, 9 })
            {
                var current = PrimitiveCurrentSaveFixture(); current.nodes.Add(new NodeState { id = current.nextId++, x = 3, y = 3, kind = (NodeKind)kind });
                Require(new WorldSimulator(current).State.nodes.Single().kind == (NodeKind)kind, "v11 primitive resource rejected");
                var old = PrimitiveVersionTenFixture(); old.nodes[0].kind = (NodeKind)kind;
                Reject(old, "v10 accepted primitive node " + kind);
            }
            foreach (var pair in new[] { (kind: 36, recipe: 100), (kind: 37, recipe: 101) })
            {
                var current = PrimitiveCurrentSaveFixture(); var kiln = Add(current, (BuildKind)pair.kind, 20, 10);
                kiln.items = new int[29]; kiln.recipe = pair.recipe; kiln.progress = 4;
                var state = new WorldSimulator(current).State;
                Require(state.buildings.Single(b => (int)b.kind == pair.kind).progress == 4 && DemoCatalog.Width(kiln) == 2 && DemoCatalog.Height(kiln) == 2, "v11 kiln footprint or recipe progress rejected");
                kiln.progress = 4.001f; Reject(current, "kiln progress exceeded four seconds"); kiln.progress = 4;
                current.nodes.Add(new NodeState { id = current.nextId++, x = 21, y = 11, kind = NodeKind.Ore });
                Reject(current, "kiln footprint accepted overlapping resource");
                var old = PrimitiveVersionTenFixture(); var invalid = Add(old, (BuildKind)pair.kind, 20, 10); invalid.items = new int[25];
                Reject(old, "v10 accepted primitive kiln " + pair.kind);
            }
        });
        Run("primitive save fresh world restores with no research or free furnace", () =>
        {
            var fresh = new WorldSimulator(73149); fresh.AddPlayer("primitive-new", "New");
            Require(fresh.State.primitiveVersion == 1 && fresh.State.primitiveResearch == 0, "new world did not opt into unresearched primitive progression");
            var saved = Saved(fresh.State); string before = Dump(saved); var state = new WorldSimulator(saved).State;
            Require(state.primitiveVersion == 1 && state.primitiveResearch == 0 && state.buildings.All(b => b.kind != BuildKind.StoneFurnace),
                "fresh save gained research or a free furnace");
            Require(Dump(saved) == before, "new-world restoration mutated its caller");
        });
        Run("primitive save unresearched player-built stone furnace restores with default recipe10", () =>
        {
            var world = Flat(era: TechEra.Primitive); world.primitiveVersion = 1;
            var sim = new WorldSimulator(world);
            var p = sim.AddPlayer("primitive-builder", "Builder"); p.x = 10; p.y = 10;
            Array.Copy(DemoCatalog.BuildCost(BuildKind.StoneFurnace), p.items, p.items.Length);
            sim.Apply(Command(p, "build", 11, 10, (int)BuildKind.StoneFurnace));
            var furnace = sim.State.buildings.Single(b => b.kind == BuildKind.StoneFurnace);
            Require(TechCatalog.SelectedRecipe(furnace).id == 10, "new furnace lost default recipe10");
            var restored = new WorldSimulator(Saved(sim.State)).State;
            var copy = restored.buildings.Single(b => b.id == furnace.id && b.kind == BuildKind.StoneFurnace);
            Require(restored.primitiveVersion == 1 && restored.primitiveResearch == 0 && TechCatalog.SelectedRecipe(copy).id == 10,
                "restore changed an unresearched furnace or its default recipe");
        });
    }
}
