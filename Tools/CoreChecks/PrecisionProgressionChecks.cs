using DeadCircuit;

static partial class Checks
{
    static readonly ItemKind[] HvPriorMaterials =
    {
        ItemKind.Food, ItemKind.Fuel, ItemKind.Coke, ItemKind.Diesel, ItemKind.Steel, ItemKind.SteelPlate,
        ItemKind.Copper, ItemKind.CopperWire, ItemKind.Coil, ItemKind.Motor, ItemKind.Bearing, ItemKind.Cable,
        ItemKind.Relay, ItemKind.Circuit, ItemKind.RefractoryBrick, ItemKind.PipeFitting, ItemKind.Polymer,
        ItemKind.Lubricant, ItemKind.ProcessWater, ItemKind.Coolant
    };
    static readonly ItemKind[] HvProgressionMaterials =
    {
        ItemKind.Quartz, ItemKind.IndustrialSilicon, ItemKind.PureSilicon, ItemKind.Wafer,
        ItemKind.Semiconductor, ItemKind.AdvancedCircuit, ItemKind.PrecisionPart,
        ItemKind.ControlModule, ItemKind.HeatAlloy, ItemKind.HotCoolant
    };

    static WorldSimulator HvProgressionStart(out PlayerState p)
    {
        // Supply completed MV history and utilities. The HV factory starts empty.
        var state = Flat(era: TechEra.MediumVoltage);
        state.nextWaveAt = 9999;
        var ground = state.ground.ToCharArray();
        for (int y = 0; y < state.height; y++) for (int x = 0; x < state.width; x++)
            if (x < 4 || y < 4 || x >= state.width - 4 || y >= state.height - 4)
                ground[y * state.width + x] = '2';
        ground[24 * state.width + 8] = '2';
        state.ground = new string(ground);
        state.nodes.Add(new NodeState { id = state.nextId++, kind = NodeKind.Quartz, x = 14, y = 10, stock = 95, hp = 35 });
        Add(state, BuildKind.Workbench, 10, 10);
        Add(state, BuildKind.Boiler, 8, 22);
        Add(state, BuildKind.SteamPipe, 10, 22);
        Add(state, BuildKind.SteamFurnace, 11, 22);
        Add(state, BuildKind.DieselGenerator, 21, 17);
        Add(state, BuildKind.ChemicalPlant, 50, 12);
        var sim = new WorldSimulator(state);
        p = sim.AddPlayer("hv-builder", "HV builder");
        Array.Clear(p.items);
        RestockPriorEra(p, TechCatalog.ResearchCost(TechEra.HighVoltage), HvPriorMaterials);
        Require(sim.State.era == TechEra.MediumVoltage && sim.State.version == WorldSimulator.SaveVersion,
            "HV route did not start with a current MV fixture");
        Require(sim.State.hvResearch == 0 && sim.State.hvPowered == 0 &&
            HvProgressionMaterials.All(i => sim.State.produced[(int)i] == 0 &&
                sim.State.players.All(q => q.items[(int)i] == 0) &&
                sim.State.buildings.All(b => b.items[(int)i] == 0)) &&
            sim.State.buildings.All(b => TechCatalog.RequiredEra(b.kind) < TechEra.HighVoltage) &&
            sim.State.cargo.Count == 0, "MV fixture gifted HV materials, machines or evidence");
        return sim;
    }

    static void HvProgressionMine(WorldSimulator sim, PlayerState p)
    {
        var quartz = sim.State.nodes.Single(n => n.kind == NodeKind.Quartz);
        int item = (int)ItemKind.Quartz, stock = quartz.stock, gathered = sim.State.gathered;
        p.x = quartz.x - 1; p.y = quartz.y;
        var pickaxe = EquipGatheringTool(sim, p, GearKind.StonePickaxe);
        foreach (int hp in new[] { 23, 11 })
        {
            Advance(sim, .55f);
            sim.Apply(Command(p, "interact", quartz.x, quartz.y));
            Require(quartz.hp == hp && quartz.stock == stock && p.items[item] == 0 && sim.State.gathered == gathered,
                "partial quartz hit damaged stock or awarded material early: " + p.notice);
        }
        Advance(sim, .55f); sim.Apply(Command(p, "interact", quartz.x, quartz.y));
        Require(p.items[item] == stock && sim.State.gathered - gathered == stock && pickaxe.durability == 97 &&
            !sim.State.nodes.Any(n => n.id == quartz.id), "quartz did not exhaust at its original stock");
        Advance(sim, .55f); sim.Apply(Command(p, "interact", quartz.x, quartz.y));
        Require(p.items[item] == stock && sim.State.gathered - gathered == stock, "depleted quartz yielded more material");
    }

    static void HvProgressionUnlock(WorldSimulator sim, PlayerState p, PrecisionTech tech)
    {
        Advance(sim, .55f); p.x = 8; p.y = 10;
        RestockPriorEra(p, PrecisionCatalog.Definition((int)tech).cost, HvPriorMaterials);
        int mask = sim.State.hvResearch; var before = Copy(p.items);
        sim.Apply(Command(p, "hv", arg: (int)tech));
        Require(sim.State.hvResearch == (mask | (1 << (int)tech)), "HV research failed: " + tech + ": " + p.notice);
        Paid(p.items, before, PrecisionCatalog.Definition((int)tech).cost, "HV research " + tech);
    }

    static void HvProgressionProduce(WorldSimulator sim, PlayerState p, BuildingState machine,
        int id, ItemKind output, int minimum)
    {
        int item = (int)output, produced = sim.State.produced[item];
        var recipe = TechCatalog.Recipe(id);
        Require(recipe != null && recipe.building == machine.kind && recipe.outputs[item] > 0 && p.items[item] < minimum,
            "invalid production target for HV recipe " + id);
        MvPlayerAct(sim, p, "recipe", machine, id);
        Require(machine.recipe == id, "could not select HV recipe " + id + ": " + p.notice);
        RestockPriorEra(p, recipe.inputs, HvPriorMaterials, batches: 8);
        MvPlayerAct(sim, p, "interact", machine);
        for (int n = 0; n < 240 && p.items[item] < minimum; n++)
        {
            foreach (var source in sim.State.buildings)
            {
                int fuel = source.kind == BuildKind.Boiler ? (int)ItemKind.Fuel :
                    source.kind == BuildKind.DieselGenerator ? (int)ItemKind.Diesel : -1;
                if (fuel >= 0 && source.items[fuel] < 2)
                {
                    RestockPriorEra(p, DemoCatalog.Materials((ItemKind)fuel, 8), HvPriorMaterials);
                    MvPlayerAct(sim, p, "interact", source);
                }
            }
            if (p.hunger < 65)
            {
                RestockPriorEra(p, DemoCatalog.Materials(ItemKind.Food, 1), HvPriorMaterials);
                Advance(sim, .55f); sim.Apply(Command(p, "eat"));
            }
            bool needsInput = Enumerable.Range(0, DemoCatalog.ItemCount).Any(i => machine.items[i] < recipe.inputs[i]);
            if (needsInput) RestockPriorEra(p, recipe.inputs, HvPriorMaterials, batches: 8);
            if (p.items[item] + machine.items[item] >= minimum || needsInput)
                MvPlayerAct(sim, p, "interact", machine);
            if (p.items[item] < minimum) Advance(sim, 2);
        }
        Require(p.items[item] >= minimum && sim.State.produced[item] > produced && machine.powered,
            $"HV recipe {id} did not produce {minimum} {output}: inventory={p.items[item]}, " +
            $"power={machine.powerRatio}, buffer=[{MvPlayerInventory(machine.items)}], notice={p.notice}");
        Require(sim.State.hvPowered == 0, "steam or MV production granted HV acceptance");
    }

    static void HvProgressionRejectNuclear(WorldSimulator sim, PlayerState p, int expectedPowered)
    {
        Advance(sim, .55f);
        var cost = TechCatalog.ResearchCost(TechEra.Nuclear);
        RestockPriorEra(p, cost, HvPriorMaterials);
        var before = Copy(p.items);
        Require(sim.State.hvResearch == PrecisionCatalog.AllResearch &&
            sim.State.hvPowered == expectedPowered && sim.State.produced[(int)ItemKind.ControlModule] >= 4 &&
            sim.State.produced[(int)ItemKind.HeatAlloy] >= 6 &&
            sim.State.buildings.Any(b => b.kind == BuildKind.PrecisionAssembler && b.hp > 0) &&
            cost.Select((amount, i) => before[i] >= amount).All(enough => enough),
            "nuclear rejection did not isolate insufficient HV output from payment and other requirements");
        sim.Apply(Command(p, "research", arg: (int)TechEra.Nuclear));
        Require(sim.State.era == TechEra.HighVoltage && sim.State.hvPowered == expectedPowered,
            "stockpiled MV components bypassed HV production at " + expectedPowered);
        Inventory(p.items, before, "premature nuclear entry charged samples");
    }

    static void PrecisionProgressionChecks()
    {
        Run("HV progression finite quartz to eight studies and paid nuclear entry through actual HV production", () =>
        {
            var sim = HvProgressionStart(out var p);
            var before = Copy(p.items);
            sim.Apply(Command(p, "research", arg: (int)TechEra.HighVoltage));
            Require(sim.State.era == TechEra.HighVoltage && sim.State.hvResearch == 0 && sim.State.hvPowered == 0,
                "completed MV cannot enter HV with zero HV evidence: " + p.notice);
            Paid(p.items, before, TechCatalog.ResearchCost(TechEra.HighVoltage), "HV era entry");
            HvProgressionMine(sim, p);

            var furnace = sim.State.buildings.Single(b => b.kind == BuildKind.SteamFurnace);
            var boiler = sim.State.buildings.Single(b => b.kind == BuildKind.Boiler);
            var diesel = sim.State.buildings.Single(b => b.kind == BuildKind.DieselGenerator);
            HvProgressionUnlock(sim, p, PrecisionTech.Silicon);
            var refiner = MvPlayerBuild(sim, p, BuildKind.SiliconRefiner, 23, 18, supplies: HvPriorMaterials);
            HvProgressionProduce(sim, p, furnace, 170, ItemKind.IndustrialSilicon, 24);
            HvProgressionProduce(sim, p, refiner, 171, ItemKind.PureSilicon, 20);
            HvProgressionUnlock(sim, p, PrecisionTech.Wafers);
            HvProgressionProduce(sim, p, refiner, 172, ItemKind.Wafer, 32);
            Require(p.items[(int)ItemKind.HotCoolant] > 0 && sim.State.produced[(int)ItemKind.HotCoolant] > 0,
                "wafer cutting did not yield hot coolant");
            HvProgressionUnlock(sim, p, PrecisionTech.Devices);
            var bench = MvPlayerBuild(sim, p, BuildKind.ElectronicsBench, 21, 21, supplies: HvPriorMaterials);
            HvProgressionProduce(sim, p, bench, 173, ItemKind.Semiconductor, 24);
            HvProgressionUnlock(sim, p, PrecisionTech.Circuits);
            HvProgressionProduce(sim, p, bench, 174, ItemKind.AdvancedCircuit, 34);
            HvProgressionUnlock(sim, p, PrecisionTech.Assembly);
            HvProgressionProduce(sim, p, bench, 175, ItemKind.PrecisionPart, 30);
            HvProgressionUnlock(sim, p, PrecisionTech.Control);
            HvProgressionProduce(sim, p, bench, 176, ItemKind.ControlModule, 10);
            // Unload the unused circuits/parts from 176 and stop MV module output before metering 80.
            MvPlayerAct(sim, p, "recipe", bench, 175);
            MvPlayerAct(sim, p, "interact", bench);
            Require(bench.recipe == 175 && diesel.powerGeneration > 0 &&
                refiner.networkId == diesel.networkId && bench.networkId == diesel.networkId &&
                sim.State.powerNetworks.Single(n => n.id == diesel.networkId).tier == 3,
                "the bootstrap route did not use the MV diesel network");
            HvProgressionUnlock(sim, p, PrecisionTech.Alloys);
            HvProgressionProduce(sim, p, furnace, 177, ItemKind.HeatAlloy, 12);
            Require(boiler.powerGeneration > 0 && furnace.networkId == boiler.networkId &&
                sim.State.powerNetworks.Single(n => n.id == furnace.networkId).tier == 1,
                "silicon and alloy furnace did not use the steam network");
            HvProgressionUnlock(sim, p, PrecisionTech.Grid);
            Require(sim.State.hvResearch == PrecisionCatalog.AllResearch && HvProgressionMaterials
                .Where(i => i != ItemKind.Quartz).All(i => sim.State.produced[(int)i] > 0),
                "the HV route skipped a research or recipe product");

            var generator = MvPlayerBuild(sim, p, BuildKind.HighGenerator, 38, 20, supplies: HvPriorMaterials);
            var pole = MvPlayerBuild(sim, p, BuildKind.HighPole, 45, 20, supplies: HvPriorMaterials);
            var assembler = MvPlayerBuild(sim, p, BuildKind.PrecisionAssembler, 49, 20, supplies: HvPriorMaterials);
            HvProgressionRejectNuclear(sim, p, 0);
            int module = (int)ItemKind.ControlModule, hot = (int)ItemKind.HotCoolant;
            int modulesBefore = sim.State.produced[module];
            MvPlayerAct(sim, p, "recipe", assembler, 80);
            Require(assembler.recipe == 80, "could not select HV module recipe: " + p.notice);
            var recipe = TechCatalog.Recipe(80);
            RestockPriorEra(p, recipe.inputs, HvPriorMaterials, batches: 2);
            MvPlayerAct(sim, p, "interact", assembler);
            var loaded = Copy(assembler.items);
            Require(recipe.inputs.Select((amount, i) => loaded[i] >= 2 * amount).All(enough => enough),
                "HV assembler lacked two batches of earned inputs");
            RestockPriorEra(p, DemoCatalog.Materials(ItemKind.Fuel, 4), HvPriorMaterials);
            int fuelBefore = p.items[(int)ItemKind.Fuel];
            MvPlayerAct(sim, p, "interact", generator);
            Require(p.items[(int)ItemKind.Fuel] < fuelBefore, "player did not fuel the HV generator");
            Advance(sim, 1);
            Require(generator.powerGeneration > 0 && assembler.powered && generator.networkId > 0 &&
                assembler.networkId == generator.networkId && pole.networkId == generator.networkId &&
                sim.State.powerNetworks.Single(n => n.id == assembler.networkId).tier == 4,
                "built generator and pole did not power the HV assembler");

            Require(assembler.progress > 0 && assembler.progress < recipe.duration && sim.State.hvPowered == 0,
                "HV checkpoint missed the first active module batch");
            var saved = Saved(sim.State); string frozen = Dump(saved);
            sim = new WorldSimulator(saved);
            p = sim.State.players.Single(q => q.id == "hv-builder");
            assembler = Building(sim, assembler.id);
            Require(assembler.recipe == 80 && sim.State.hvResearch == PrecisionCatalog.AllResearch &&
                sim.State.hvPowered == 0 && sim.State.produced[module] == modulesBefore, "HV restore changed recipe or evidence");
            Near(assembler.progress, saved.buildings.Single(b => b.id == assembler.id).progress, "HV restore lost active progress");
            Inventory(assembler.items, loaded, "HV restore changed loaded inputs");

            LvProgressionAwait(sim, () => sim.State.hvPowered >= 2, "first HV batch did not finish after restore");
            Require(sim.State.hvPowered == 2 && sim.State.produced[module] - modulesBefore == 2,
                "first HV batch did not count exactly two modules");
            HvProgressionRejectNuclear(sim, p, 2);
            LvProgressionAwait(sim, () => sim.State.hvPowered >= 4, "second powered HV batch did not finish");
            Require(sim.State.hvPowered == 4 && sim.State.produced[module] - modulesBefore == 4 &&
                assembler.items[module] == 4 && assembler.items[hot] == 2,
                "two HV batches did not produce four modules and two hot coolant");
            for (int i = 0; i < loaded.Length; i++)
                Require(assembler.items[i] == loaded[i] - 2 * recipe.inputs[i] + 2 * recipe.outputs[i],
                    "HV module production balance lost item " + (ItemKind)i);
            int heldModules = p.items[module];
            MvPlayerAct(sim, p, "interact", assembler);
            Require(p.items[module] == heldModules + 4, "player did not collect the four HV modules");
            RestockPriorEra(p, TechCatalog.ResearchCost(TechEra.Nuclear), HvPriorMaterials);
            Advance(sim, .55f); before = Copy(p.items);
            sim.Apply(Command(p, "research", arg: (int)TechEra.Nuclear));
            Require(sim.State.era == TechEra.Nuclear && sim.State.hvPowered == 4,
                "earned HV route failed to enter nuclear: " + p.notice);
            Paid(p.items, before, DemoCatalog.Materials(ItemKind.ControlModule, 4, ItemKind.HeatAlloy, 4, ItemKind.Coolant, 8),
                "nuclear entry samples");
            Require(Dump(saved) == frozen, "continuing HV production mutated its saved checkpoint");
            var restored = new WorldSimulator(Saved(sim.State));
            Require(restored.State.era == TechEra.Nuclear && restored.State.hvResearch == PrecisionCatalog.AllResearch &&
                restored.State.hvPowered == 4 && restored.State.produced.SequenceEqual(sim.State.produced),
                "current save lost paid nuclear entry or actual HV production");
        });
    }
}
