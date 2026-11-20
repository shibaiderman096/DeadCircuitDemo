using DeadCircuit;

static partial class Checks
{
    static WorldState PetrochemicalFixture()
    {
        var state = Flat(era: TechEra.MediumVoltage);
        state.mvResearch = 255;
        state.mvSurvey = 1;
        state.mvExtracted = state.mvPiped = state.mvDistilled = 20;
        state.mvDiesel = state.mvPolymer = state.mvLubricant = state.mvCooled = state.mvPowered = 20;
        Add(state, BuildKind.SteamFurnace, 50, 50);
        return state;
    }

    static NodeState PetrochemicalOil(WorldState state, int x, int y, int stock)
    {
        var oil = new NodeState { id = state.nextId++, kind = NodeKind.Oil, x = x, y = y, stock = stock };
        state.nodes.Add(oil);
        return oil;
    }

    static void PetrochemicalPower(WorldState state, BuildingState machine)
    {
        var kind = TechCatalog.EnergyTier(machine) == 3 ? BuildKind.DieselGenerator : BuildKind.Generator;
        Add(state, kind, 6, 10).fuel = 20;
    }

    // The main check runner registers this entry alongside the other vertical checks.
    static void PetrochemicalFlowChecks()
    {
        Run("petrochemical liquid settings cover nine fluids including cold coolant", () =>
        {
            int[] expected = { 46, 47, 48, 49, 50, 51, 52, 53, 16 };
            Require(WorldSimulator.FluidCount == 9, "fluid setting count");
            for(int setting = 0; setting < expected.Length; setting++)
                Require(WorldSimulator.FluidItem(setting) == expected[setting] && PetrochemicalCatalog.IsFluid(expected[setting]), "wrong liquid setting " + setting);
            foreach(int setting in new[] { int.MinValue, -1, 9, int.MaxValue })
                Require(WorldSimulator.FluidItem(setting) == -1, "invalid setting resolved to a real fluid");
        });

        Run("petrochemical diesel generator rejects primitive Fuel", () =>
        {
            var state = PetrochemicalFixture();
            var generator = Add(state, BuildKind.DieselGenerator, 10, 10);
            var sim = new WorldSimulator(state);
            var player = sim.AddPlayer("petro-fuel", "P"); player.x = 8; player.y = 10;
            player.items[(int)ItemKind.Fuel] = 1;
            sim.Apply(Command(player, "interact", 10, 10));
            Require(player.items[(int)ItemKind.Fuel] == 1 && Building(sim, generator.id).items[(int)ItemKind.Fuel] == 0,
                "diesel generator accepted primitive Fuel from a player");
            // Runtime fuel selection must remain safe even after an external invalid mutation.
            Building(sim, generator.id).items[(int)ItemKind.Fuel] = 1;
            Advance(sim, 1);
            var actual = Building(sim, generator.id);
            Require(actual.items[(int)ItemKind.Fuel] == 1 && actual.powerGeneration == 0,
                "primitive Fuel started the diesel generator");
            Reject(Saved(sim.State), "diesel generator restored an invalid primitive Fuel inventory");
        });

        Run("petrochemical oil extraction takes two powered seconds and exhausts a finite adjacent deposit", () =>
        {
            var state = PetrochemicalFixture();
            var pump = Add(state, BuildKind.OilPump, 10, 10);
            var oil = PetrochemicalOil(state, 12, 10, 2);
            PetrochemicalPower(state, pump);
            var sim = new WorldSimulator(state); int before = sim.State.mvExtracted;
            Advance(sim, 1.95f);
            Require(Building(sim, pump.id).items[(int)ItemKind.CrudeOil] == 0 && sim.State.nodes.Single(n => n.id == oil.id).stock == 2, "oil was extracted early");
            sim.Step(WorldSimulator.FixedStep);
            Require(Building(sim, pump.id).items[(int)ItemKind.CrudeOil] == 1 && sim.State.mvExtracted == before + 1, "first oil unit did not complete at two seconds");
            Advance(sim, 4);
            Require(Building(sim, pump.id).items[(int)ItemKind.CrudeOil] == 2 && sim.State.mvExtracted == before + 2 &&
                !sim.State.nodes.Any(n => n.id == oil.id), "finite oil source was not exhausted exactly once");
            Near(DemoCatalog.Demand(BuildKind.OilPump), 20, "oil extraction LV demand");
            Require(TechCatalog.EnergyTier(BuildKind.OilPump) == 2, "first oil pump requires future voltage");
        });

        Run("petrochemical oil pump requires adjacency power research and output capacity", () =>
        {
            foreach(int variant in Enumerable.Range(0, 4))
            {
                var state = PetrochemicalFixture(); var pump = Add(state, BuildKind.OilPump, 10, 10);
                var oil = PetrochemicalOil(state, variant == 0 ? 14 : 12, 10, 1);
                if(variant != 1) PetrochemicalPower(state, pump);
                if(variant == 3) pump.items[(int)ItemKind.CrudeOil] = WorldSimulator.BufferLimit;
                var sim = new WorldSimulator(state);
                if(variant == 2) sim.State.mvResearch &= ~(1 << (int)PetrochemicalTech.Extraction);
                int before = sim.State.mvExtracted;
                Advance(sim, 3);
                Require(sim.State.nodes.Single(n => n.id == oil.id).stock == 1 && sim.State.mvExtracted == before &&
                    Building(sim, pump.id).progress == 0, "blocked pump extracted oil; variant " + variant);
            }
        });

        Run("petrochemical water pump produces a two unit batch only at a powered shoreline", () =>
        {
            foreach(int variant in Enumerable.Range(0, 3))
            {
                var state = PetrochemicalFixture(); var pump = Add(state, BuildKind.ProcessWaterPump, 10, 10);
                PetrochemicalPower(state, pump);
                if(variant != 1) Water(state, 10, 12);
                if(variant == 2) pump.items[(int)ItemKind.ProcessWater] = 199;
                var sim = new WorldSimulator(state);
                Advance(sim, 2);
                Require(Building(sim, pump.id).items[(int)ItemKind.ProcessWater] == (variant == 0 ? 2 : variant == 1 ? 0 : 199),
                    "water pump split a batch or ignored the shoreline");
            }
            Near(DemoCatalog.Demand(BuildKind.ProcessWaterPump), 10, "water pump LV demand");
        });

        Run("petrochemical nine liquid types travel as conserved real pipe cargo", () =>
        {
            for(int setting = 0; setting < 9; setting++)
            {
                var state = PetrochemicalFixture();
                var pipe = Add(state, BuildKind.LiquidPipe, 10, 10); pipe.setting = setting;
                int item = WorldSimulator.FluidItem(setting); pipe.items[item] = 2;
                var tank = Add(state, BuildKind.LiquidTank, 11, 10); tank.setting = setting;
                var sim = new WorldSimulator(state); var totals = ItemTotals(sim.State); int before = sim.State.mvPiped;
                sim.Step(WorldSimulator.FixedStep);
                Require(sim.State.cargo.Count == 1 && sim.State.cargo[0].item == item && sim.State.mvPiped == before, "dispatch counted as liquid delivery");
                for(int tick = 0; tick < 40; tick++) { sim.Step(WorldSimulator.FixedStep); Conserved(sim.State, totals); }
                Require(Building(sim, tank.id).items[item] == 2 && sim.State.mvPiped == before + 2 &&
                    sim.State.cargo.Count == 0, "pipe did not deliver exactly two selected units; setting " + setting);
            }
        });

        Run("petrochemical liquid routes rotate and refuse opposite or mixed ports", () =>
        {
            for(int rotation = 0; rotation < 4; rotation++)
            {
                var state = PetrochemicalFixture();
                var pipe = Add(state, BuildKind.LiquidPipe, 10, 10, rotation);
                pipe.items[(int)ItemKind.CrudeOil] = 1;
                var target = Add(state, BuildKind.LiquidPipe, DemoCatalog.OutputX(pipe), DemoCatalog.OutputY(pipe), rotation);
                var sim = new WorldSimulator(state); var totals = ItemTotals(sim.State);
                Advance(sim, 1); Conserved(sim.State, totals);
                Require(Building(sim, target.id).items[(int)ItemKind.CrudeOil] == 1, "rotated liquid port did not deliver");
                Require(!WorldSimulator.PetrochemicalCargoRouteValid(target, pipe, (int)ItemKind.CrudeOil), "liquid used the back of a directional source");
            }
            var mixed = PetrochemicalFixture();
            var from = Add(mixed, BuildKind.LiquidPipe, 10, 10); from.items[(int)ItemKind.CrudeOil] = 1;
            var to = Add(mixed, BuildKind.LiquidTank, 11, 10); to.setting = 1;
            var stalled = new WorldSimulator(mixed); Advance(stalled, 1);
            Require(stalled.State.cargo.Count == 0 && Building(stalled, from.id).items[(int)ItemKind.CrudeOil] == 1, "different selected fluids mixed");
        });

        Run("petrochemical pump pipe tank conserves oil including depleted stock and in flight units", () =>
        {
            var state = PetrochemicalFixture();
            var pump = Add(state, BuildKind.OilPump, 10, 10); PetrochemicalPower(state, pump);
            PetrochemicalOil(state, 10, 9, 4);
            Add(state, BuildKind.LiquidPipe, 12, 10); var tank = Add(state, BuildKind.LiquidTank, 13, 10);
            var sim = new WorldSimulator(state); int extracted = sim.State.mvExtracted, piped = sim.State.mvPiped;
            for(int tick = 0; tick < 220; tick++)
            {
                sim.Step(WorldSimulator.FixedStep);
                int remaining = sim.State.nodes.Where(n => n.kind == NodeKind.Oil).Sum(n => n.stock);
                Require(remaining + ItemTotals(sim.State)[(int)ItemKind.CrudeOil] == 4, "oil disappeared between deposit and tank");
            }
            Require(Building(sim, tank.id).items[(int)ItemKind.CrudeOil] == 4 && sim.State.mvExtracted == extracted + 4 &&
                sim.State.mvPiped == piped + 8 && sim.State.cargo.Count == 0, "vertical extraction and two pipe hops did not finish");
        });

        Run("petrochemical all fluids including coolant refuse solid logistics and manual belt loading", () =>
        {
            foreach(var kind in new[] { BuildKind.Chest, BuildKind.Belt, BuildKind.FilterSplitter, BuildKind.MechanicalHopper })
            for(int setting = 0; setting < 9; setting++)
            {
                int item = WorldSimulator.FluidItem(setting); var state = PetrochemicalFixture();
                var pipe = Add(state, BuildKind.LiquidPipe, 10, 10); pipe.setting = setting; pipe.items[item] = 1;
                var solid = Add(state, kind, 11, 10);
                var sim = new WorldSimulator(state); var p = sim.AddPlayer("petro-solid", "P"); p.x = 11; p.y = 8;
                p.items[item] = 1; sim.Apply(Command(p, "interact", 11, 10, item)); Advance(sim, 1);
                Require(p.items[item] == 1 && Building(sim, solid.id).items[item] == 0 &&
                    Building(sim, pipe.id).items[item] == 1 && sim.State.cargo.Count == 0, "solid logistics accepted a fluid");
                Require(!WorldSimulator.PetrochemicalCargoRouteValid(solid, pipe, item), "invalid solid source can export fluid");
            }
        });

        Run("petrochemical empty hand filled tanks preserve selected fluid without transport evidence", () =>
        {
            var state = PetrochemicalFixture(); var tank = Add(state, BuildKind.LiquidTank, 10, 10); tank.setting = 8;
            var sim = new WorldSimulator(state); var p = sim.AddPlayer("petro-can", "P"); p.x = 8; p.y = 10;
            p.items[(int)ItemKind.Coolant] = 3; p.items[(int)ItemKind.ProcessWater] = 4;
            int before = sim.State.mvPiped;
            sim.Apply(Command(p, "interact", 10, 10));
            Require(p.items[(int)ItemKind.Coolant] == 0 && p.items[(int)ItemKind.ProcessWater] == 4 &&
                Building(sim, tank.id).items[(int)ItemKind.Coolant] == 3, "hand filling ignored selected cold coolant");
            Ready(p); sim.Apply(Command(p, "interact", 10, 10));
            Require(p.items[(int)ItemKind.Coolant] == 3 && Building(sim, tank.id).items.All(n => n == 0) && sim.State.mvPiped == before, "unloading immediately reinserted fluid or fabricated pipe evidence");
        });

        Run("petrochemical storage configuration requires empty inventory and no inbound or outbound cargo", () =>
        {
            var state = PetrochemicalFixture(); var pipe = Add(state, BuildKind.LiquidPipe, 10, 10);
            pipe.items[(int)ItemKind.CrudeOil] = 1; var tank = Add(state, BuildKind.LiquidTank, 11, 10);
            var sim = new WorldSimulator(state); var p = sim.AddPlayer("petro-config", "P"); p.x = 10; p.y = 8;
            sim.Apply(Command(p, "configure", 10, 10, 8));
            Require(Building(sim, pipe.id).setting == 0, "nonempty pipe changed fluid");
            sim.Step(WorldSimulator.FixedStep);
            Require(sim.State.cargo.Count == 1, "configuration fixture failed to dispatch");
            foreach(var b in new[] { pipe, tank })
            {
                Ready(p); sim.Apply(Command(p, "configure", b.x, b.y, 8));
                Require(Building(sim, b.id).setting == 0, "storage changed fluid with attached in flight cargo");
            }
            Advance(sim, 1);
            foreach(int invalid in new[] { -1, 9, int.MaxValue })
            { Ready(p); sim.Apply(Command(p, "configure", 10, 10, invalid)); Require(Building(sim, pipe.id).setting == 0, "invalid setting accepted"); }
            Ready(p); sim.Apply(Command(p, "configure", 10, 10, 8));
            Require(Building(sim, pipe.id).setting == 8, "empty idle pipe could not select cold coolant");
        });

        Run("petrochemical any full distillation coproduct blocks the entire batch without paying", () =>
        {
            foreach(var full in new[] { ItemKind.LightFraction, ItemKind.ChemicalFeedstock, ItemKind.HeavyFraction })
            {
                var state = PetrochemicalFixture(); var machine = Add(state, BuildKind.DistillationUnit, 10, 10); machine.recipe = 161;
                machine.items[(int)ItemKind.CrudeOil] = 4; machine.items[(int)ItemKind.Fuel] = 1;
                machine.items[(int)full] = WorldSimulator.BufferLimit;
                PetrochemicalPower(state, machine); Water(state, 10, 12);
                var sim = new WorldSimulator(state); var before = Copy(Building(sim, machine.id).items); int count = sim.State.mvDistilled;
                Advance(sim, 5);
                Inventory(Building(sim, machine.id).items, before, "full coproduct charged the distillation batch");
                Require(sim.State.mvDistilled == count && sim.State.produced.All(n => n == 0), "blocked batch fabricated production evidence");
            }
        });

        Run("petrochemical distillation exports one selected fraction and allows manual coproduct unloading", () =>
        {
            var state = PetrochemicalFixture(); var machine = Add(state, BuildKind.DistillationUnit, 10, 10); machine.recipe = 161;
            machine.items[(int)ItemKind.CrudeOil] = 4; machine.items[(int)ItemKind.Fuel] = 1;
            PetrochemicalPower(state, machine); Water(state, 10, 12);
            var pipe = Add(state, BuildKind.LiquidPipe, DemoCatalog.OutputX(machine), DemoCatalog.OutputY(machine)); pipe.setting = 1;
            var sim = new WorldSimulator(state); int count = sim.State.mvDistilled; Advance(sim, 5);
            var actual = Building(sim, machine.id);
            Require(Building(sim, pipe.id).items[(int)ItemKind.LightFraction] == 2 &&
                actual.items[(int)ItemKind.ChemicalFeedstock] == 1 && actual.items[(int)ItemKind.HeavyFraction] == 1 &&
                sim.State.mvDistilled == count + 4, "distillation lost a fraction or counted an output instead of crude input units");
            var p = sim.AddPlayer("petro-coproduct", "P"); p.x = 8; p.y = 10;
            sim.Apply(Command(p, "interact", 10, 10));
            Require(p.items[(int)ItemKind.ChemicalFeedstock] == 1 && p.items[(int)ItemKind.HeavyFraction] == 1 &&
                actual.items[(int)ItemKind.ChemicalFeedstock] == 0 && actual.items[(int)ItemKind.HeavyFraction] == 0, "E did not unload all coproducts");
        });

        Run("petrochemical pump outgoing liquid reserves its refund slot against new extraction", () =>
        {
            var state = PetrochemicalFixture(); var pump = Add(state, BuildKind.OilPump, 10, 10);
            pump.items[(int)ItemKind.CrudeOil] = WorldSimulator.BufferLimit;
            PetrochemicalOil(state, 10, 9, 1); PetrochemicalPower(state, pump);
            var pipe = Add(state, BuildKind.LiquidPipe, 12, 10);
            var sim = new WorldSimulator(state); var totals = ItemTotals(sim.State); int extracted = sim.State.mvExtracted;
            sim.Step(WorldSimulator.FixedStep);
            Require(sim.State.cargo.Count == 1 && Building(sim, pump.id).items[(int)ItemKind.CrudeOil] == 199, "full oil pump failed to dispatch");
            Building(sim, pipe.id).hp = 0; sim.Step(WorldSimulator.FixedStep);
            Conserved(sim.State, totals);
            Require(Building(sim, pump.id).items[(int)ItemKind.CrudeOil] == 200 && sim.State.mvExtracted == extracted &&
                sim.State.cargo.Count == 0, "lost downstream liquid could not refund to the pump");
        });

        Run("petrochemical multioutput recipes cannot fill an outgoing liquids reserved refund slot", () =>
        {
            var state = PetrochemicalFixture(); var machine = Add(state, BuildKind.DistillationUnit, 10, 10); machine.recipe = 161;
            machine.items[(int)ItemKind.CrudeOil] = 4; machine.items[(int)ItemKind.Fuel] = 1;
            machine.items[(int)ItemKind.LightFraction] = 199; machine.progress = 3.95f;
            Water(state, 10, 12); PetrochemicalPower(state, machine);
            var pipe = Add(state, BuildKind.LiquidPipe, 12, 10); pipe.setting = 1;
            var sim = new WorldSimulator(state); int before = sim.State.mvDistilled;
            sim.Step(WorldSimulator.FixedStep);
            // The source now has 198 liquid units plus one outbound reserved unit. A batch
            // of two must not use the 200th slot until that outbound unit is delivered.
            Require(sim.State.cargo.Count == 1 && Building(sim, machine.id).items[(int)ItemKind.LightFraction] == 198, "refund fixture failed to dispatch");
            sim.Step(WorldSimulator.FixedStep);
            Require(Building(sim, machine.id).items[(int)ItemKind.CrudeOil] == 4 && sim.State.mvDistilled == before, "next batch stole an outbound liquid refund slot");
            Building(sim, pipe.id).hp = 0; sim.Step(WorldSimulator.FixedStep);
            Require(Building(sim, machine.id).items[(int)ItemKind.LightFraction] == 199 && Building(sim, machine.id).items[(int)ItemKind.CrudeOil] == 4, "machine lost orphaned liquid after a blocked next batch");
        });

        Run("petrochemical current save resumes real liquid cargo and counts arrival exactly once", () =>
        {
            var state = PetrochemicalFixture(); var from = Add(state, BuildKind.LiquidPipe, 10, 10); from.setting = 8;
            from.items[(int)ItemKind.Coolant] = 1; var to = Add(state, BuildKind.LiquidTank, 11, 10); to.setting = 8;
            var sim = new WorldSimulator(state); sim.Step(WorldSimulator.FixedStep);
            int before = sim.State.mvPiped; var totals = ItemTotals(sim.State);
            var loaded = new WorldSimulator(Saved(sim.State));
            Require(loaded.State.cargo.Count == 1 && loaded.State.mvPiped == before, "restore eagerly delivered or counted liquid");
            for(int tick = 0; tick < 20; tick++)
            { sim.Step(WorldSimulator.FixedStep); loaded.Step(WorldSimulator.FixedStep); Conserved(loaded.State, totals); }
            Require(Building(loaded, to.id).items[(int)ItemKind.Coolant] == 1 && loaded.State.mvPiped == before + 1 &&
                Dump(sim.State) == Dump(loaded.State), "current liquid shipment diverged after save");
        });

        Run("petrochemical typed diesel cargo starts MV generation only after arrival", () =>
        {
            var state = PetrochemicalFixture(); var tank = Add(state, BuildKind.LiquidTank, 10, 10); tank.setting = 4;
            tank.items[(int)ItemKind.Diesel] = 1; var generator = Add(state, BuildKind.DieselGenerator, 12, 10);
            var sim = new WorldSimulator(state); int before = sim.State.mvPiped; sim.Step(WorldSimulator.FixedStep);
            Require(sim.State.cargo.Count == 1 && Building(sim, generator.id).powerGeneration == 0 && sim.State.mvPiped == before, "diesel started before delivery");
            Advance(sim, 1);
            var actual = Building(sim, generator.id);
            Near(actual.powerGeneration, 240, "diesel power output");
            Require(actual.items[(int)ItemKind.Diesel] == 0 && actual.fuel > 19 && sim.State.mvPiped == before + 1, "diesel did not burn one delivered unit");
        });

        Run("petrochemical all refining polymer lubrication and cooling batches record only real completion", () =>
        {
            var cases = new (int recipe, ItemKind item, int amount, ItemKind? secondary, int secondAmount)[]
            {
                (70,ItemKind.Polymer,2,null,0), (71,ItemKind.Coolant,2,null,0),
                (160,ItemKind.ChemicalFeedstock,2,null,0), (162,ItemKind.Lubricant,2,null,0),
                (163,ItemKind.Fuel,2,null,0), (164,ItemKind.Polymer,4,ItemKind.HotCoolant,1),
                (165,ItemKind.Diesel,2,null,0), (91,ItemKind.Coolant,2,null,0)
            };
            foreach(var test in cases)
            {
                var recipe = TechCatalog.Recipe(test.recipe);
                Require(recipe != null, "missing specified petrochemical recipe " + test.recipe);
                var state = PetrochemicalFixture(); var machine = Add(state, recipe.building, 10, 10); machine.recipe = test.recipe;
                machine.items = Copy(recipe.inputs); PetrochemicalPower(state, machine);
                if(test.recipe != 164) Water(state, 10, 10 + DemoCatalog.Height(machine));
                var sim = new WorldSimulator(state);
                int distilled = sim.State.mvDistilled, diesel = sim.State.mvDiesel, polymer = sim.State.mvPolymer;
                int lubricant = sim.State.mvLubricant, cooled = sim.State.mvCooled, powered = sim.State.mvPowered;
                Advance(sim, recipe.duration - .05f);
                Inventory(Building(sim, machine.id).items, recipe.inputs, "recipe paid before real completion " + test.recipe);
                sim.Step(WorldSimulator.FixedStep);
                var expected = new int[DemoCatalog.ItemCount]; expected[(int)test.item] = test.amount;
                if(test.secondary.HasValue) expected[(int)test.secondary.Value] = test.secondAmount;
                Inventory(Building(sim, machine.id).items, expected, "specified petrochemical batch " + test.recipe);
                Inventory(sim.State.produced, expected, "production history " + test.recipe);
                Require(sim.State.mvDistilled == distilled && sim.State.mvDiesel == diesel + (test.recipe == 165 ? 2 : 0) &&
                    sim.State.mvPolymer == polymer + (test.recipe == 70 ? 2 : test.recipe == 164 ? 4 : 0) &&
                    sim.State.mvLubricant == lubricant + (test.recipe == 162 ? 2 : 0) && sim.State.mvCooled == cooled + (test.recipe == 91 ? 2 : 0) &&
                    sim.State.mvPowered == powered + (test.recipe == 164 ? 4 : 0), "wrong progression counter for recipe " + test.recipe);
            }
        });

        Run("petrochemical basic reactors distillation and tower stall without shoreline cooling", () =>
        {
            foreach(int id in new[] { 70, 71, 160, 161, 162, 163, 165, 91 })
            {
                var recipe = TechCatalog.Recipe(id); var state = PetrochemicalFixture();
                var machine = Add(state, recipe.building, 10, 10); machine.recipe = id; machine.items = Copy(recipe.inputs);
                PetrochemicalPower(state, machine); var sim = new WorldSimulator(state);
                Advance(sim, recipe.duration + 1);
                Inventory(Building(sim, machine.id).items, recipe.inputs, "dry machine consumed inputs " + id);
                Require(sim.State.produced.All(n => n == 0), "dry machine fabricated output");
            }
        });

        Run("petrochemical cooling loop requires hot coolant and actual process water", () =>
        {
            foreach(var missing in new[] { ItemKind.HotCoolant, ItemKind.ProcessWater })
            {
                var state = PetrochemicalFixture(); var tower = Add(state, BuildKind.CoolingTower, 10, 10); tower.recipe = 91;
                tower.items[(int)ItemKind.HotCoolant] = 2; tower.items[(int)ItemKind.ProcessWater] = 1; tower.items[(int)missing] = 0;
                Water(state, 10, 10 + DemoCatalog.Height(tower)); PetrochemicalPower(state, tower);
                var sim = new WorldSimulator(state); int before = sim.State.mvCooled; Advance(sim, 4);
                Require(Building(sim, tower.id).items[(int)ItemKind.Coolant] == 0 && sim.State.mvCooled == before, "near water alone manufactured free cold coolant");
            }
        });
    }
}
