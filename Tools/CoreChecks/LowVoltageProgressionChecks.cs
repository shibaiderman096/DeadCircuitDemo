using DeadCircuit;

static partial class Checks
{
    // All positive LV evidence used below is earned by this player-command route.
    // This deliberately does not inherit Flat's synthetic, fully researched LV fixture.
    static WorldState lvProgressionEarned;
    static readonly ItemKind[] LvPriorMaterials =
    {
        ItemKind.Wood, ItemKind.Stone, ItemKind.Scrap, ItemKind.Food, ItemKind.Clay, ItemKind.Brick,
        ItemKind.Charcoal, ItemKind.Copper, ItemKind.Iron, ItemKind.Bronze, ItemKind.Steel, ItemKind.IronPlate,
        ItemKind.SteelPlate, ItemKind.Gear, ItemKind.Bearing, ItemKind.RefractoryBrick, ItemKind.Ore
    };
    static readonly ItemKind[] LvProgressionItems =
    {
        ItemKind.CopperWire, ItemKind.Insulator, ItemKind.Cable, ItemKind.Coil,
        ItemKind.Motor, ItemKind.Relay, ItemKind.Circuit
    };
    static readonly (string name, Func<WorldState, int> get, Action<WorldState, int> set, int minimum)[] LvProgressionCounters =
    {
        ("wire", s => s.lvWire, (s,n) => s.lvWire=n, 12),
        ("insulators", s => s.lvInsulators, (s,n) => s.lvInsulators=n, 8),
        ("cable", s => s.lvCable, (s,n) => s.lvCable=n, 8),
        ("coils", s => s.lvCoils, (s,n) => s.lvCoils=n, 4),
        ("motors", s => s.lvMotors, (s,n) => s.lvMotors=n, 2),
        ("relays", s => s.lvRelays, (s,n) => s.lvRelays=n, 4),
        ("circuits", s => s.lvCircuits, (s,n) => s.lvCircuits=n, 6),
        ("electric", s => s.lvElectric, (s,n) => s.lvElectric=n, 8),
        ("automated", s => s.lvAutomated, (s,n) => s.lvAutomated=n, 4),
        ("sorted", s => s.lvSorted, (s,n) => s.lvSorted=n, 4),
        ("protected", s => s.lvProtected, (s,n) => s.lvProtected=n, 4)
    };

    static int[] LvProgressionEvidence(WorldState s) =>
        new[] { s.lvResearch }.Concat(LvProgressionCounters.Select(c => c.get(s))).ToArray();

    static WorldSimulator LvProgressionStart(out PlayerState p)
    {
        var s = Flat(era: TechEra.Steam);
        s.lvResearch = 0;
        foreach (var counter in LvProgressionCounters) counter.set(s, 0);
        s.nextWaveAt = 9999;
        // Completed pre-LV industrial history, with the current format and opt-in
        // primitive/water research enabled rather than historical migration bypasses.
        s.primitiveVersion = s.waterVersion = 1;
        s.primitiveResearch = 255; s.waterResearch = 127;
        s.produced[(int)ItemKind.Bronze] = 4; s.produced[(int)ItemKind.Iron] = 6;
        s.produced[(int)ItemKind.Brick] = 4; s.produced[(int)ItemKind.Charcoal] = 2;
        s.waterCrushed = 4; s.waterSmelted = 6; s.waterPressed = 4;
        s.waterGears = 2; s.waterBearings = 2; s.waterTransported = 4;
        Add(s, BuildKind.Workbench, 10, 10);
        Add(s, BuildKind.ResearchBench, 9, 12);
        Add(s, BuildKind.ClayKiln, 14, 10);
        WaterDrive(s, BuildKind.MachineTool);
        Add(s, BuildKind.SteamFurnace, 50, 50);
        var sim = new WorldSimulator(s);
        p = sim.AddPlayer("lv-builder", "LV builder");
        Array.Clear(p.items);
        RestockPriorEra(p, TechCatalog.ResearchCost(TechEra.LowVoltage), LvPriorMaterials);
        p.x = 8; p.y = 10;
        Require(sim.State.version == WorldSimulator.SaveVersion && DemoCatalog.ItemCount == 66 && sim.State.produced.Length == 66 && p.items.Length == 66,
            "LV fixture is not the current 66-slot save format");
        Require(LvProgressionEvidence(sim.State).All(n => n == 0), "LV startup inherited earned research/evidence");
        foreach (var item in LvProgressionItems)
            Require(p.items[(int)item] == 0 && sim.State.produced[(int)item] == 0 &&
                sim.State.buildings.All(b => b.items[(int)item] == 0), "startup gifted LV component: " + item);
        Require(sim.State.buildings.All(b => TechCatalog.RequiredEra(b.kind) <= TechEra.Steam), "startup gifted LV equipment");
        return sim;
    }

    static void LvProgressionAct(WorldSimulator sim, PlayerState p, string action, BuildingState b, int arg = 0)
    {
        Advance(sim, .55f); p.x = b.x; p.y = b.y - 1;
        sim.Apply(Command(p, action, b.x, b.y, arg));
        Require(p.hp > 0, "LV builder died during " + action);
    }

    static void LvProgressionCraft(WorldSimulator sim, PlayerState p, int id, int batches)
    {
        var recipe = id == 0 ? new RecipeDefinition { building = BuildKind.Workbench,
            inputs = DemoCatalog.Materials(ItemKind.Wood, 3), outputs = DemoCatalog.Materials(ItemKind.Fuel, 2) } : TechCatalog.Recipe(id);
        Require(recipe != null && recipe.building == BuildKind.Workbench, "invalid workbench recipe " + id);
        for (int n = 0; n < batches; n++)
        {
            RestockPriorEra(p, recipe.inputs, LvPriorMaterials);
            Advance(sim, .55f); p.x = 8; p.y = 10; var before = Copy(p.items);
            sim.Apply(Command(p, "craft", arg: id));
            for (int i = 0; i < DemoCatalog.ItemCount; i++)
                Require(p.items[i] == before[i] - recipe.inputs[i] + recipe.outputs[i],
                    $"workbench recipe {id} batch {n} material balance item {i}: {p.notice}");
        }
    }

    static void LvProgressionUnlock(WorldSimulator sim, PlayerState p, LowVoltageTech id)
    {
        Advance(sim, .55f); p.x = 8; p.y = 10;
        RestockPriorEra(p, LowVoltageCatalog.Definition((int)id).cost, LvPriorMaterials);
        var peer = sim.State.players.Single(q => q.id == "lv-peer");
        var peerBefore = Copy(peer.items); var before = Copy(p.items); int mask = sim.State.lvResearch;
        int xp = p.xp, skills = p.skillPoints, attributes = p.attributePoints;
        sim.Apply(Command(p, "lv", arg: (int)id));
        Require(sim.State.lvResearch == (mask | (1 << (int)id)), "LV research failed: " + id + ": " + p.notice);
        Paid(p.items, before, LowVoltageCatalog.Definition((int)id).cost, "LV research " + id);
        Inventory(peer.items, peerBefore, "LV team research charged teammate");
        Require(p.xp == xp && p.skillPoints == skills && p.attributePoints == attributes, "LV research spent character progression");
        var paid = Copy(p.items);
        Advance(sim, .55f); peer.x = 8; peer.y = 10;
        Require(LowVoltageCatalog.HasResearch(sim.State, id), "teammate cannot see shared research");
        sim.Apply(Command(peer, "lv", arg: (int)id));
        Require(sim.State.lvResearch == (mask | (1 << (int)id)), "teammate duplicated research");
        Inventory(peer.items, peerBefore, "teammate repeat research was charged");
        Inventory(p.items, paid, "teammate repeat charged original researcher");
    }

    static void LvProgressionProduce(WorldSimulator sim, PlayerState p, BuildingState b, int recipe, ItemKind output, int minimum)
    {
        LvProgressionAct(sim, p, "recipe", b, recipe);
        Require(TechCatalog.SelectedRecipe(b)?.id == recipe, "cannot select LV recipe " + recipe + ": " + p.notice);
        var definition = TechCatalog.Recipe(recipe);
        for (int cycle = 0; cycle < 160 && p.items[(int)output] < minimum; cycle++)
        {
            foreach (var generator in sim.State.buildings.Where(machine => machine.kind == BuildKind.Generator))
                if (generator.items[(int)ItemKind.Fuel] < 4)
                {
                    if (p.items[(int)ItemKind.Fuel] < 8) LvProgressionCraft(sim, p, 0, 4);
                    LvProgressionAct(sim, p, "interact", generator);
                }
            // Interact loads every applicable input. Refill only when a batch
            // actually needs input, avoiding an unrealistic flood of spare parts.
            bool needsInput = Enumerable.Range(0, DemoCatalog.ItemCount).Any(i => b.items[i] < definition.inputs[i]);
            if (needsInput) RestockPriorEra(p, definition.inputs, LvPriorMaterials, batches: 8);
            if (p.items[(int)output] + b.items[(int)output] >= minimum || needsInput)
                LvProgressionAct(sim, p, "interact", b);
            if (p.items[(int)output] >= minimum) break;
            if (p.hunger < 65)
            {
                RestockPriorEra(p, DemoCatalog.Materials(ItemKind.Food, 1), LvPriorMaterials);
                Ready(p); sim.Apply(Command(p, "eat"));
            }
            Advance(sim, 8);
        }
        Require(p.items[(int)output] >= minimum,
            $"LV recipe {recipe} failed to supply {minimum} {output}: have={p.items[(int)output]}, power={b.powerRatio}, network={b.networkId}, buffer={string.Join(",", b.items)}, notice={p.notice}");
    }

    static void LvProgressionAwait(WorldSimulator sim, Func<bool> done, string message, int ticks = 400)
    {
        for (int tick = 0; tick < ticks && !done(); tick++) sim.Step(WorldSimulator.FixedStep);
        Require(done(), message);
    }

    static WorldState LvProgressionEarnedState()
    {
        if (lvProgressionEarned != null) return Saved(lvProgressionEarned);
        var sim = LvProgressionStart(out var p);
        sim.AddPlayer("lv-peer", "LV teammate");
        // Only pre-LV materials are supplied; even generator fuel is player-crafted.
        Advance(sim, .55f); var entry = Copy(p.items);
        sim.Apply(Command(p, "research", arg: (int)TechEra.LowVoltage));
        Require(sim.State.era == TechEra.LowVoltage, "completed steam cannot enter LV: " + p.notice);
        Paid(p.items, entry, TechCatalog.ResearchCost(TechEra.LowVoltage), "LV era entry");
        Require(LvProgressionEvidence(sim.State).All(n => n == 0), "LV entry awarded unearned evidence");
        var kiln = sim.State.buildings.Single(b => b.kind == BuildKind.ClayKiln);
        var tool = sim.State.buildings.Single(b => b.kind == BuildKind.MachineTool);
        LvProgressionProduce(sim, p, kiln, 140, ItemKind.Insulator, 100);
        LvProgressionProduce(sim, p, tool, 141, ItemKind.CopperWire, 320);
        Require(sim.State.lvElectric == 0 && !sim.State.buildings.Any(b => TechCatalog.EnergyTier(b) == 2),
            "startup insulation/wire depended on future electrical equipment");
        LvProgressionUnlock(sim, p, LowVoltageTech.Generation);
        LvProgressionUnlock(sim, p, LowVoltageTech.Cabling);
        LvProgressionCraft(sim, p, 142, 32);
        for (int y = 21; y <= 23; y++) WaterBuild(sim, p, BuildKind.Shaft, 22, y, LvPriorMaterials);
        var winder = WaterBuild(sim, p, BuildKind.WindingBench, 23, 23, LvPriorMaterials);
        Require(TechCatalog.EnergyTier(winder) == 0 && DemoCatalog.Demand(winder.kind) == 25, "winder is not a 25-unit mechanical load");
        LvProgressionProduce(sim, p, winder, 143, ItemKind.Coil, 40);
        Require(winder.powered && winder.networkId > 0 && sim.State.lvElectric == 0, "mechanical winding required electricity");
        // Manual loading also leaves insulation in the winder. Produce the
        // next ceramic supply instead of gifting components or draining buffers.
        LvProgressionProduce(sim, p, kiln, 140, ItemKind.Insulator, 60);
        LvProgressionUnlock(sim, p, LowVoltageTech.Motors);
        LvProgressionCraft(sim, p, 144, 6);
        LvProgressionUnlock(sim, p, LowVoltageTech.Processing);
        LvProgressionUnlock(sim, p, LowVoltageTech.Relays);
        LvProgressionCraft(sim, p, 145, 24);
        var generator = WaterBuild(sim, p, BuildKind.Generator, 34, 20, LvPriorMaterials);
        var secondGenerator = WaterBuild(sim, p, BuildKind.Generator, 34, 23, LvPriorMaterials);
        LvProgressionCraft(sim, p, 0, 8);
        LvProgressionAct(sim, p, "interact", generator);
        LvProgressionAct(sim, p, "interact", secondGenerator);
        WaterBuild(sim, p, BuildKind.PowerPole, 38, 22, LvPriorMaterials);
        WaterBuild(sim, p, BuildKind.PowerPole, 40, 22, LvPriorMaterials);
        var crusher = WaterBuild(sim, p, BuildKind.ElectricCrusher, 38, 20, LvPriorMaterials);
        LvProgressionProduce(sim, p, crusher, 146, ItemKind.CrushedOre, 4);
        Require(generator.powerGeneration > 0 && crusher.powered && sim.State.lvElectric >= 4, "fuel generator did not drive actual crushing");
        var circuits = WaterBuild(sim, p, BuildKind.CircuitAssembler, 38, 23, LvPriorMaterials);
        LvProgressionProduce(sim, p, circuits, 60, ItemKind.Circuit, 18);
        Require(sim.State.lvCircuits >= 18 && sim.State.lvCircuits <= sim.State.lvElectric, "actual relay circuit evidence missing");
        LvProgressionProduce(sim, p, winder, 143, ItemKind.Coil, 8);
        LvProgressionProduce(sim, p, kiln, 140, ItemKind.Insulator, 60);
        LvProgressionCraft(sim, p, 145, 4);
        LvProgressionUnlock(sim, p, LowVoltageTech.Assembly);
        LvProgressionCraft(sim, p, 142, 8);
        LvProgressionUnlock(sim, p, LowVoltageTech.Sorting);
        LvProgressionUnlock(sim, p, LowVoltageTech.Protection);
        var assembler = WaterBuild(sim, p, BuildKind.Assembler, 41, 20, LvPriorMaterials);
        var smelter = WaterBuild(sim, p, BuildKind.Smelter, 38, 26, LvPriorMaterials);
        LvProgressionProduce(sim, p, smelter, 147, ItemKind.CopperWire, p.items[(int)ItemKind.CopperWire] + 4);
        var breaker = WaterBuild(sim, p, BuildKind.CircuitBreaker, 36, 22, LvPriorMaterials);
        LvProgressionAct(sim, p, "configure", breaker, 1);
        Require(breaker.setting == 0 && !breaker.tripped, "new player-built breaker could not close");
        int protectedBefore = sim.State.lvProtected, automatedBefore = sim.State.lvAutomated;
        LvProgressionProduce(sim, p, assembler, 148, ItemKind.Cable, p.items[(int)ItemKind.Cable] + 16);
        Require(assembler.networkId == breaker.networkId && assembler.networkId > 0 &&
            sim.State.lvProtected >= protectedBefore + 4 && sim.State.lvAutomated >= automatedBefore + 4,
            "closed same-network breaker did not certify actual automated output");
        LvProgressionProduce(sim, p, assembler, 149, ItemKind.Motor, p.items[(int)ItemKind.Motor] + 1);
        LvProgressionProduce(sim, p, assembler, 150, ItemKind.Relay, p.items[(int)ItemKind.Relay] + 1);
        var splitter = WaterBuild(sim, p, BuildKind.FilterSplitter, 41, 24, LvPriorMaterials);
        var destination = WaterBuild(sim, p, BuildKind.Chest, 42, 24, LvPriorMaterials);
        WaterBuild(sim, p, BuildKind.Chest, 41, 25, LvPriorMaterials);
        LvProgressionAct(sim, p, "configure", splitter, (int)ItemKind.Cable);
        // Keep one independently fed electric batch live at the save checkpoint.
        RestockPriorEra(p, DemoCatalog.Materials(ItemKind.Ore, 8), LvPriorMaterials);
        LvProgressionAct(sim, p, "interact", crusher);
        LvProgressionAct(sim, p, "interact", splitter, (int)ItemKind.Cable);
        Require(sim.State.lvSorted == 0, "loading splitter fabricated delivery evidence");
        LvProgressionAwait(sim, () => destination.items[(int)ItemKind.Cable] >= 4, "player-built splitter did not deliver four matching cables");
        Require(sim.State.lvSorted == destination.items[(int)ItemKind.Cable], "sorted evidence differs from matching arrivals");
        Require(sim.State.lvResearch == 255 && LowVoltageCatalog.MediumVoltageRequirement(sim.State).Length == 0,
            "full player LV chain cannot satisfy MV: " + LowVoltageCatalog.MediumVoltageRequirement(sim.State));
        p.x=8;p.y=10;Ready(p);
        Require(TechCatalog.ResearchRequirement(sim.State, p, TechEra.MediumVoltage).Length == 0, "MV gate rejected earned LV acceptance");
        Require(LvProgressionItems.All(item => sim.State.produced[(int)item] > 0), "one LV component was never genuinely produced");
        LvProgressionAwait(sim, () => sim.State.cargo.Any(c => c.fromId == splitter.id) &&
            sim.State.buildings.Any(b => b.progress > 0 && TechCatalog.EnergyTier(b) == 2),
            "earned save checkpoint lacks active LV processing and matching cargo");
        RestockPriorEra(p, TechCatalog.ResearchCost(TechEra.MediumVoltage), LvPriorMaterials);
        lvProgressionEarned = Saved(sim.State);
        return Saved(lvProgressionEarned);
    }

    public static void RunLowVoltageProgressionChecks()
    {
        Run("LV progression zero entry rejects invalid research and premature MV", () =>
        {
            foreach (int id in new[] { -1, 256, (int)LowVoltageTech.Generation, (int)LowVoltageTech.Motors })
            {
                var sim = LvProgressionStart(out var p);
                Advance(sim, .55f); sim.Apply(Command(p, "research", arg: (int)TechEra.LowVoltage));
                Advance(sim, .55f); var before = Copy(p.items);
                sim.Apply(Command(p, "lv", arg: id));
                Inventory(p.items, before, "unearned LV research charged materials");
                Require(LvProgressionEvidence(sim.State).All(n => n == 0), "unearned/invalid LV research accepted: " + id);
                Ready(p); sim.Apply(Command(p, "research", arg: (int)TechEra.MediumVoltage));
                Require(sim.State.era == TechEra.LowVoltage, "zero LV evidence allowed MV entry");
                Inventory(p.items, before, "premature MV charged samples");
            }
        });
        Run("LV progression complete player chain earns eight shared studies all recipes and legal MV", () =>
        {
            var source = LvProgressionEarnedState();
            var sim = new WorldSimulator(source); var p = sim.State.players.Single(q => q.id == "lv-builder");
            p.x=8;p.y=10;Ready(p);
            Advance(sim, .55f); var before = Copy(p.items);
            sim.Apply(Command(p, "research", arg: (int)TechEra.MediumVoltage));
            Require(sim.State.era == TechEra.MediumVoltage, "earned progression cannot enter MV: " + p.notice);
            Paid(p.items, before, TechCatalog.ResearchCost(TechEra.MediumVoltage), "MV entry cost");
            var restored = new WorldSimulator(Saved(sim.State));
            Require(restored.State.era == TechEra.MediumVoltage && LvProgressionEvidence(restored.State).SequenceEqual(LvProgressionEvidence(sim.State)),
                "current-save restore lost legal MV acceptance");
        });
        Run("LV progression current save deep copies component inventories research and counters", () =>
        {
            var source = LvProgressionEarnedState(); string frozen = Dump(source);
            var restored = new WorldSimulator(source);
            Require(Dump(source) == frozen && !ReferenceEquals(source, restored.State), "restore mutated or reused caller save");
            Require(LvProgressionEvidence(source).SequenceEqual(LvProgressionEvidence(restored.State)), "restored LV evidence differs");
            foreach (var player in source.players)
            {
                var copy = restored.State.players.Single(q => q.id == player.id);
                Require(!ReferenceEquals(player.items, copy.items) && copy.items.SequenceEqual(player.items), "player component inventory was aliased/lost");
            }
            foreach (var b in source.buildings)
            {
                var copy = Building(restored, b.id);
                Require(!ReferenceEquals(b.items, copy.items) && copy.items.SequenceEqual(b.items), "machine component inventory was aliased/lost");
                Require(b.recipe == copy.recipe && b.setting == copy.setting && b.tripped == copy.tripped, "LV machine configuration lost");
                Near(copy.progress, b.progress, "LV work-in-progress lost");
            }
            Advance(restored, 3);
            Require(Dump(source) == frozen, "stepping restored LV world mutated original save graph");
            var restoredFrozen = Dump(restored.State);
            source.players[0].items[(int)ItemKind.Cable]++;
            source.buildings.Single(b => b.kind == BuildKind.FilterSplitter).items[(int)ItemKind.Cable]++;
            source.lvSorted++; source.produced[(int)ItemKind.Cable]++;
            Require(Dump(restored.State) == restoredFrozen, "caller save mutation leaked into restored world");
        });
        Run("LV progression in-flight matching delivery and active processing resume exactly once", () =>
        {
            var sim = new WorldSimulator(LvProgressionEarnedState());
            var packet = sim.State.cargo.FirstOrDefault(c => Building(sim, c.fromId).kind == BuildKind.FilterSplitter);
            Require(packet != null, "earned chain checkpoint has no real matching cargo in flight");
            Require(sim.State.buildings.Any(b => b.progress > 0 && TechCatalog.EnergyTier(b) == 2), "earned checkpoint has no active electrical batch");
            var source = Saved(sim.State); string frozen = Dump(source);
            var restored = new WorldSimulator(source);
            int sorted = source.lvSorted, received = Building(sim, packet.toId).items[packet.item];
            Advance(sim, 3); Advance(restored, 3);
            Require(sim.State.lvSorted > sorted && sim.State.lvSorted - sorted == Building(sim, packet.toId).items[packet.item] - received,
                "resumed deliveries were lost or counted twice");
            Require(Dump(sim.State) == Dump(restored.State), "same current save replay diverged in production/cargo/counters");
            Require(Dump(source) == frozen, "LV replay mutated supplied save");
        });
        Run("LV progression current save rejects illegal research masks prerequisites and missing evidence", () =>
        {
            foreach (int mask in new[] { -1, 256, int.MaxValue, 1 << (int)LowVoltageTech.Motors,
                1 << (int)LowVoltageTech.Assembly, 1 << (int)LowVoltageTech.Protection })
            {
                var bad = LvProgressionEarnedState(); bad.lvResearch = mask;
                Reject(bad, "current LV save accepted illegal research mask/prerequisites " + mask);
            }
            foreach (var counter in LvProgressionCounters.Take(8).Where(c => c.name != "relays"))
            {
                var bad = LvProgressionEarnedState(); counter.set(bad, 0);
                Reject(bad, "current LV save accepted research without production evidence: " + counter.name);
            }
        });
        Run("LV progression current save rejects every negative LV production counter", () =>
        {
            foreach (var counter in LvProgressionCounters)
            {
                var bad = LvProgressionEarnedState(); bad.lvResearch = 0;
                foreach (var field in LvProgressionCounters) field.set(bad, 0);
                counter.set(bad, -1);
                Reject(bad, "current LV save accepted negative " + counter.name);
            }
        });
        Run("LV progression current save rejects material evidence before its research and impossible electric totals", () =>
        {
            foreach (var counter in LvProgressionCounters.Where(c => c.name != "wire" && c.name != "insulators" && c.name != "electric"))
            {
                var bad = LvProgressionEarnedState(); bad.lvResearch = 0;
                // Retain only this genuinely earned output (and its electric
                // denominator), so another branch's prerequisite cannot mask it.
                foreach (var field in LvProgressionCounters)
                    if (field.name != counter.name && field.name != "electric") field.set(bad, 0);
                Reject(bad, "current LV save accepted " + counter.name + " before its own research");
            }
            foreach (var (name, mutate) in new (string, Action<WorldState>)[]
            {
                ("circuits exceed electric", s => s.lvCircuits = s.lvElectric + 1),
                ("automated exceed electric", s => s.lvAutomated = s.lvElectric + 1),
                ("protected exceed electric", s => s.lvProtected = s.lvElectric + 1)
            })
            {
                var bad = LvProgressionEarnedState(); mutate(bad);
                Reject(bad, "current LV save accepted " + name);
            }
        });
        Run("LV progression current save rejects cross-era research and forged MV acceptance", () =>
        {
            var preLv = LvProgressionEarnedState(); preLv.era = TechEra.Steam;
            Reject(preLv, "steam save retained future LV research/evidence");
            var noSteam = LvProgressionEarnedState(); noSteam.steamDriven = 0;
            Reject(noSteam, "LV save accepted fabricated upstream steam acceptance");
            var missingResearch = LvProgressionEarnedState(); missingResearch.era = TechEra.MediumVoltage;
            missingResearch.lvResearch &= ~(1 << (int)LowVoltageTech.Sorting); missingResearch.lvSorted = 0;
            Reject(missingResearch, "MV save skipped an LV research branch");
            foreach (var counter in LvProgressionCounters)
            {
                var bad = LvProgressionEarnedState(); bad.era = TechEra.MediumVoltage; counter.set(bad, counter.minimum - 1);
                Reject(bad, "MV save accepted insufficient LV acceptance: " + counter.name);
            }
        });
        Run("LV progression player MV command requires each actual acceptance threshold without payment", () =>
        {
            foreach (var counter in LvProgressionCounters)
            {
                var sim = new WorldSimulator(LvProgressionEarnedState());
                var p = sim.State.players.Single(q => q.id == "lv-builder");
                // Remove earned evidence in the live test world; never manufacture positive counters.
                counter.set(sim.State, counter.minimum - 1); Ready(p); var before = Copy(p.items);
                sim.Apply(Command(p, "research", arg: (int)TechEra.MediumVoltage));
                Require(sim.State.era == TechEra.LowVoltage, "MV command skipped threshold: " + counter.name);
                Inventory(p.items, before, "blocked MV charged materials for " + counter.name);
            }
        });
    }
}
