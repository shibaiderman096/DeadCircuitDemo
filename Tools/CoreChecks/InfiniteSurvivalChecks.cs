using System;
using System.Linq;
using DeadCircuit;

static partial class Checks
{
    // Main registers InfiniteSurvivalChecks(); fixtures never resize the legacy maps.
    static WorldSimulator InfiniteSurvivalFixture(out PlayerState player)
    {
        var sim = new WorldSimulator(new WorldGenerationSettings
        { seed = 73149, chunked = true, preset = WorldPreset.Wilderness, cityDensity = 0 });
        player = sim.AddPlayer("infinite-survival", "Survival");
        foreach (var c in new[] { (-2, -2), (-1, -2), (-2, -1), (-1, -1), (2, -2) })
        {
            ChunkStreaming.GenerateChunk(sim.State, c.Item1, c.Item2);
            Require(WorldTiles.TryChunk(sim.State, c.Item1, c.Item2, out var chunk), "fixture chunk missing");
            chunk.ground = new string('0', 256); chunk.biomes = new string('1', 256);
        }
        sim.State.nodes.Clear(); sim.State.buildings.Clear(); sim.State.sites.Clear();
        sim.State.zombies.Clear(); sim.State.survival.creatures.Clear();
        player.needs = new SurvivalPlayerState { initialized = true };
        player.activeSlot = 2; player.x = -18; player.y = -17; Array.Fill(player.items, 100);
        return sim;
    }

    static void InfiniteSurvivalChecks()
    {
        Run("infinite survival negative shoreline pump retains one intact wall intake and water collection", () =>
        {
            var sim = InfiniteSurvivalFixture(out var p); var world = sim.State;
            Require(WorldTiles.TryChunk(world, -2, -2, out var shore), "shore fixture missing");
            var ground = shore.ground.ToCharArray(); ground[13 + 15 * 16] = '2'; shore.ground = new string(ground); // (-19,-17)
            var pump = new BuildingState { kind = BuildKind.FeedwaterPump, x = -17, y = -17, footprintVersion = 1 };
            Require(!TechCatalog.NearWater(world, pump), "intake crossed an empty gap instead of a wall");
            var wall = new NodeState { id = world.nextId++, kind = NodeKind.Wall, x = -18, y = -17, hp = 10, stock = 1 };
            world.nodes.Add(wall);
            Require(TechCatalog.NearWater(world, pump), "negative-coordinate intact wall blocked feedwater intake");
            wall.stock = 0; Require(!TechCatalog.NearWater(world, pump), "depleted wall still supported intake");
            wall.stock = 1; wall.hp = 0; Require(!TechCatalog.NearWater(world, pump), "destroyed wall still supported intake");
            world.nodes.Clear(); var builtWall = Add(world, BuildKind.Wall, -18, -17);
            Require(TechCatalog.NearWater(world, pump), "built wall intake stopped working at negative coordinates");
            builtWall.hp = 0; Require(!TechCatalog.NearWater(world, pump), "destroyed built wall supported intake");
            world.buildings.Clear();
            sim.Apply(Command(p, "collectwater", -19, -17));
            Require(p.needs.waterRaw == 1, "negative shoreline did not supply raw water");
            pump.x = -10000; pump.y = -10000;
            Require(!TechCatalog.NearWater(world, pump), "unknown footprint accepted a water intake");
        });

        Run("infinite survival signed cross chunk farms save without legacy linear cell collisions", () =>
        {
            var sim = InfiniteSurvivalFixture(out var p); var world = sim.State;
            const int row = -17;
            foreach (var c in new[] { (x: -17, y: row), (x: -16, y: row), (x: 47, y: row - 1) })
            {
                p.x = c.x - 1; p.y = c.y; p.interactCooldown = 0;
                sim.Apply(Command(p, "till", c.x, c.y));
            }
            Require(world.survival.plots.Count == 3 && p.items[(int)ItemKind.Wood] == 97, "signed farming did not create three paid plots");
            var source = Saved(world); string before = Dump(source);
            var restored = new WorldSimulator(source);
            Require(restored.State.survival.plots.Select(plot => (plot.x, plot.y)).ToHashSet().SetEquals(
                new[] { (-17, row), (-16, row), (47, row - 1) }), "cross chunk farms collided or moved on restore");
            var actor = restored.State.players.Single(); actor.x = -18; actor.y = row; actor.interactCooldown = 0;
            actor.needs.seeds[(int)CropKind.Carrot] = 1;
            restored.Apply(Command(actor, "sow", -17, row, (int)CropKind.Carrot));
            Require(restored.State.survival.plots.Single(plot => plot.x == -17).crop == (int)CropKind.Carrot &&
                actor.needs.seeds[(int)CropKind.Carrot] == 0, "restored negative farm cannot consume a seed");
            Require(Dump(source) == before, "restored farming aliases caller save");
            source.survival.plots.Add(new FarmPlotState { id = source.nextId++, x = -17, y = row });
            Reject(source, "duplicate signed plot accepted");
        });

        Run("infinite survival forage supports every generated version and signed wilderness sources", () =>
        {
            for (int generator = 1; generator <= WorldGeneration.CurrentGeneratorVersion; generator++)
            {
                var legacy = ForagingFixture(out var actor, NodeKind.Crate, generator: generator);
                legacy.Apply(Command(actor, "interact", 10, 10));
                Require(actor.needs.foods[(int)FoodKind.Egg] >= 2 && actor.needs.foods[(int)FoodKind.RawFish] >= 1,
                    "generated version has no forage: " + generator);
            }
            var sim = InfiniteSurvivalFixture(out var p); var world = sim.State;
            world.nodes.Add(new NodeState { id = world.nextId++, kind = NodeKind.Crate, x = -17, y = -17, stock = 1 });
            var reorderedSave = Saved(world); reorderedSave.nodes.Single().id = reorderedSave.nextId++;
            var reordered = new WorldSimulator(reorderedSave); var other = reordered.State.players.Single();
            sim.Apply(Command(p, "interact", -17, -17));
            reordered.Apply(Command(other, "interact", -17, -17));
            Require(world.nodes.Count == 0 && p.needs.foods[(int)FoodKind.Egg] >= 2 && p.needs.foods[(int)FoodKind.RawFish] >= 1,
                "signed wilderness crate supplied no cooking ingredients");
            Require(p.needs.foods.SequenceEqual(other.needs.foods) && p.needs.seeds.SequenceEqual(other.needs.seeds),
                "chunk source loot changed with discovery-order entity ID");
            var food = (int[])p.needs.foods.Clone(); p.interactCooldown = 0;
            sim.Apply(Command(p, "interact", -17, -17));
            Require(p.needs.foods.SequenceEqual(food), "exhausted signed source paid twice");
            foreach (bool enabled in new[] { false, true })
            {
                var inert = ForagingFixture(out var owner, NodeKind.Crate, generator: enabled ? 0 : 1, enabled: enabled);
                inert.Apply(Command(owner, "interact", 10, 10));
                Require(owner.needs.foods.All(value => value == 0), "legacy or disabled survival acquired forage");
            }
        });

        Run("infinite survival negative workbench allows crafting while retaining intervening wall obstruction", () =>
        {
            var sim = InfiniteSurvivalFixture(out var p); var world = sim.State;
            p.x = -18; p.y = -18; var bench = Add(world, BuildKind.Workbench, -16, -18);
            sim.Apply(Command(p,"primitive",arg:(int)PrimitiveTech.Tools)); Ready(p);
            Require(PrimitiveCatalog.HasResearch(world,PrimitiveTech.Tools),"negative workbench could not research tools");
            Require(EquipmentCatalog.HasWorkbench(world, p), "legacy bounds hid reachable negative workbench");
            int count = p.gear.Count; sim.Apply(Command(p, "gearcraft", arg: (int)GearKind.StoneAxe));
            Require(p.gear.Count == count + 1 && p.gear.Last().kind == GearKind.StoneAxe, "negative workbench cannot craft");
            var wall = Add(world, BuildKind.Wall, -17, -18);
            Require(!EquipmentCatalog.HasWorkbench(world, p), "signed workbench LOS passed through a live wall");
            world.buildings.Remove(wall); bench.x = -10000; bench.y = -10000; p.x = bench.x - 2; p.y = bench.y;
            Require(!EquipmentCatalog.HasWorkbench(world, p), "workbench LOS crossed unknown terrain");
        });
    }
}
