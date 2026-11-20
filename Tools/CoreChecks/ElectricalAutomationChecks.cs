using DeadCircuit;

static partial class Checks
{
    static WorldState ElectricalFixture()
    {
        var state = Flat();
        state.lvResearch = 255;
        state.lvWire = state.lvInsulators = state.lvCable = state.lvCoils = 20;
        state.lvMotors = state.lvRelays = state.lvCircuits = 20;
        state.lvElectric = 40;
        state.lvAutomated = 4;
        state.lvSorted = 0;
        state.lvProtected = 0;
        Add(state, BuildKind.SteamFurnace, 50, 50);
        return state;
    }

    static WorldSimulator ElectricalSplitterFixture(out int splitterId, out int forwardId, out int leftId, int rotation = 0)
    {
        var state = ElectricalFixture();
        var splitter = Add(state, BuildKind.FilterSplitter, 10, 10, rotation);
        splitter.setting = (int)ItemKind.Ore;
        splitterId = splitter.id;
        forwardId = Add(state, BuildKind.Chest, 10 + DemoCatalog.DirectionX(rotation), 10 + DemoCatalog.DirectionY(rotation), rotation).id;
        leftId = Add(state, BuildKind.Chest, 10 + DemoCatalog.DirectionX(rotation + 1), 10 + DemoCatalog.DirectionY(rotation + 1), (rotation + 1) & 3).id;
        Add(state, BuildKind.Generator, 6, 7).fuel = 20;
        return new WorldSimulator(state);
    }

    static WorldSimulator ElectricalBreakerFixture(out int breakerId, out int[] machineIds, bool overloaded = true)
    {
        var state = ElectricalFixture();
        Add(state, BuildKind.Generator, 6, 7).fuel = 20;
        breakerId = Add(state, BuildKind.CircuitBreaker, 9, 7).id;
        var ids = new List<int>();
        foreach (var (x, y) in overloaded ? new[] { (8, 10), (11, 10), (8, 13) } : new[] { (8, 10) })
        {
            var machine = Add(state, BuildKind.Assembler, x, y);
            machine.items[(int)ItemKind.Iron] = 10;
            ids.Add(machine.id);
        }
        machineIds = ids.ToArray();
        return new WorldSimulator(state);
    }

    static void ElectricalAutomationChecks()
    {
        Run("electrical splitter delivers matching cargo forward and all other cargo left", () =>
        {
            var state = ElectricalFixture();
            var splitter = Add(state, BuildKind.FilterSplitter, 10, 10);
            splitter.setting = (int)ItemKind.Ore;
            splitter.items[(int)ItemKind.Ore] = 1;
            splitter.items[(int)ItemKind.Wood] = 1;
            var forward = Add(state, BuildKind.Chest, 11, 10);
            var left = Add(state, BuildKind.Chest, 10, 11);
            Add(state, BuildKind.Generator, 6, 7).fuel = 20;
            var sim = new WorldSimulator(state);
            var totals = ItemTotals(sim.State);
            for (int tick = 0; tick < 50; tick++)
            {
                sim.Step(WorldSimulator.FixedStep);
                Conserved(sim.State, totals);
            }
            Require(Building(sim, forward.id).items[(int)ItemKind.Ore] == 1 &&
                Building(sim, forward.id).items[(int)ItemKind.Wood] == 0, "matching output did not stay on its forward route");
            Require(Building(sim, left.id).items[(int)ItemKind.Wood] == 1 &&
                Building(sim, left.id).items[(int)ItemKind.Ore] == 0, "other output did not stay on its left route");
            Require(sim.State.lvSorted == 1, "dispatch or unmatched arrivals counted as sorted cargo");
        });
        Run("electrical splitter rotates both physical ports and restores side cargo", () =>
        {
            for (int rotation = 0; rotation < 4; rotation++)
            {
                var sim = ElectricalSplitterFixture(out int splitterId, out int forwardId, out int leftId, rotation);
                var splitter = Building(sim, splitterId);
                splitter.items[(int)ItemKind.Wood] = 1;
                var totals = ItemTotals(sim.State);
                sim.Step(WorldSimulator.FixedStep);
                Require(sim.State.cargo.Single().toId == leftId && sim.State.lvSorted == 0, "side dispatch used the forward port or counted early");
                string before = Dump(sim.State);
                var loaded = new WorldSimulator(Saved(sim.State));
                Require(Dump(sim.State) == before, "restore mutated caller state");
                for (int tick = 0; tick < 20; tick++)
                {
                    sim.Step(WorldSimulator.FixedStep); loaded.Step(WorldSimulator.FixedStep);
                    Conserved(sim.State, totals); Conserved(loaded.State, totals);
                }
                Require(Building(loaded, leftId).items[(int)ItemKind.Wood] == 1 &&
                    Building(loaded, forwardId).items[(int)ItemKind.Wood] == 0 && loaded.State.lvSorted == 0, "side shipment changed route after restore");
                Require(Dump(sim.State) == Dump(loaded.State), "current-save side route replay diverged");
            }
        });
        Run("electrical splitter blocks a full route without leaking into the other output", () =>
        {
            var sim = ElectricalSplitterFixture(out int splitterId, out int forwardId, out int leftId);
            Building(sim, splitterId).items[(int)ItemKind.Ore] = 1;
            Building(sim, splitterId).items[(int)ItemKind.Wood] = 1;
            Building(sim, forwardId).items[(int)ItemKind.Ore] = WorldSimulator.BufferLimit;
            var totals = ItemTotals(sim.State);
            for (int tick = 0; tick < 30; tick++) { sim.Step(WorldSimulator.FixedStep); Conserved(sim.State, totals); }
            Require(Building(sim, splitterId).items[(int)ItemKind.Ore] == 1 &&
                Building(sim, leftId).items[(int)ItemKind.Ore] == 0 && Building(sim, leftId).items[(int)ItemKind.Wood] == 1 &&
                sim.State.lvSorted == 0, "blocked match escaped or starved the open nonmatching route");
            Building(sim, forwardId).items[(int)ItemKind.Ore]--; totals[(int)ItemKind.Ore]--;
            for (int tick = 0; tick < 20; tick++) { sim.Step(WorldSimulator.FixedStep); Conserved(sim.State, totals); }
            Require(Building(sim, forwardId).items[(int)ItemKind.Ore] == WorldSimulator.BufferLimit &&
                sim.State.lvSorted == 1, "freeing the matching output did not deliver exactly once");
        });
        Run("electrical splitter pauses matching cargo on power loss and resumes once after save", () =>
        {
            var sim = ElectricalSplitterFixture(out int splitterId, out int forwardId, out _);
            Building(sim, splitterId).items[(int)ItemKind.Ore] = 1;
            var totals = ItemTotals(sim.State);
            sim.Step(WorldSimulator.FixedStep);
            Require(sim.State.cargo.Count == 1 && sim.State.lvSorted == 0, "dispatch already counted as delivery");
            sim.State.buildings.Single(b => b.kind == BuildKind.Generator).fuel = 0;
            float progress = sim.State.cargo[0].progress;
            Advance(sim, 1);
            Require(sim.State.cargo.Count == 1 && sim.State.cargo[0].progress == progress && sim.State.lvSorted == 0, "unpowered in-flight splitter packet advanced");
            var loaded = new WorldSimulator(Saved(sim.State));
            Require(loaded.State.lvSorted == 0 && loaded.State.cargo.Single().progress == progress, "restoring a paused packet counted or advanced it");
            loaded.State.buildings.Single(b => b.kind == BuildKind.Generator).fuel = 20;
            for (int tick = 0; tick < 30; tick++) { loaded.Step(WorldSimulator.FixedStep); Conserved(loaded.State, totals); }
            Require(Building(loaded, forwardId).items[(int)ItemKind.Ore] == 1 && loaded.State.lvSorted == 1 &&
                loaded.State.cargo.Count == 0, "power restoration failed to deliver exactly one packet");
        });
        Run("electrical splitter reserves its orphan return slot against concurrent input", () =>
        {
            var sim = ElectricalSplitterFixture(out int splitterId, out _, out int leftId);
            Building(sim, splitterId).items[(int)ItemKind.Wood] = WorldSimulator.BufferLimit;
            var state = Saved(sim.State);
            var incoming = Add(state, BuildKind.Belt, 9, 10); incoming.items[(int)ItemKind.Wood] = 1;
            sim = new WorldSimulator(state);
            var totals = ItemTotals(sim.State);
            sim.Step(WorldSimulator.FixedStep);
            Require(sim.State.cargo.Count == 1 && sim.State.cargo[0].fromId == splitterId &&
                Building(sim, incoming.id).items[(int)ItemKind.Wood] == 1, "input stole the dispatched item's return slot");
            var p = sim.AddPlayer("electrical-return", "P"); p.x = 10; p.y = 8;
            p.items[(int)ItemKind.Wood] = WorldSimulator.InventoryLimit;
            sim.Apply(Command(p, "interact", 10, 10, (int)ItemKind.Wood));
            Require(Building(sim, splitterId).items[(int)ItemKind.Wood] == WorldSimulator.BufferLimit - 1 &&
                p.items[(int)ItemKind.Wood] == WorldSimulator.InventoryLimit, "manual loading stole the packet's reserved return slot");
            sim.State.buildings.Single(b => b.kind == BuildKind.Generator).fuel = 0;
            Building(sim, leftId).hp = 0;
            sim.Step(WorldSimulator.FixedStep);
            Conserved(sim.State, totals);
            Require(sim.State.cargo.Count == 0 && Building(sim, splitterId).items[(int)ItemKind.Wood] == WorldSimulator.BufferLimit &&
                sim.State.lvSorted == 0, "lost target could not refund a paused packet into its reserved slot");
        });
        Run("electrical splitter configure rejects invalid settings and any in-flight reconfiguration", () =>
        {
            var sim = ElectricalSplitterFixture(out int splitterId, out int forwardId, out _);
            var p = sim.AddPlayer("electrical-config", "P"); p.x = 10; p.y = 8;
            var splitter = Building(sim, splitterId);
            foreach (int arg in new[] { -1, DemoCatalog.ItemCount, int.MaxValue })
            {
                p.interactCooldown = 0; sim.Apply(Command(p, "configure", 10, 10, arg));
                Require(splitter.setting == (int)ItemKind.Ore, "invalid filter setting was accepted");
            }
            splitter.items[(int)ItemKind.Ore] = 1; sim.Step(WorldSimulator.FixedStep);
            p.interactCooldown = 0; sim.Apply(Command(p, "configure", 10, 10, (int)ItemKind.CopperOre));
            Require(splitter.setting == (int)ItemKind.Ore && sim.State.cargo.Single().toId == forwardId, "live packet was rerouted by configure");
            sim = new WorldSimulator(Saved(sim.State)); Advance(sim, 1); p = sim.State.players.Single();
            p.interactCooldown = 0; sim.Apply(Command(p, "configure", 10, 10, (int)ItemKind.CopperOre));
            Require(Building(sim, splitterId).setting == (int)ItemKind.CopperOre && sim.State.lvSorted == 1, "idle filter could not be configured after delivery");
        });
        Run("electrical splitter dismantle refunds attached cargo without sorting evidence", () =>
        {
            foreach (bool source in new[] { false, true })
            {
                var sim = ElectricalSplitterFixture(out int splitterId, out int forwardId, out _);
                Building(sim, splitterId).items[(int)ItemKind.Ore] = 1; sim.Step(WorldSimulator.FixedStep);
                var p = sim.AddPlayer("electrical-dismantle", "P");
                var target = Building(sim, source ? splitterId : forwardId);
                p.x = target.x; p.y = target.y - 1;
                sim.Apply(Command(p, "dismantle", target.x, target.y));
                Require(!sim.State.buildings.Any(b => b.id == target.id), "dismantle command did not reach endpoint; source=" + source);
                Require(sim.State.cargo.Count == 0 && p.items[(int)ItemKind.Ore] == 1 && sim.State.lvSorted == 0,
                    "dismantle lost or counted attached splitter cargo; source=" + source);
            }
        });
        Run("electrical breaker trips before production and refuses a persistently overloaded reset", () =>
        {
            var sim = ElectricalBreakerFixture(out int breakerId, out int[] machineIds);
            var breaker = Building(sim, breakerId);
            Require(!breaker.tripped, "nonconsuming constructor preview manufactured a trip");
            Near(sim.State.powerNetworks.Single(n => n.tier == 2).generation, 120, "fixture supply");
            Near(sim.State.powerNetworks.Single(n => n.tier == 2).demand, 135, "fixture overload");
            sim.Step(WorldSimulator.FixedStep);
            Require(breaker.tripped && machineIds.All(id => !Building(sim, id).powered && Building(sim, id).progress == 0), "overload did not cut real processing in the trip tick");
            Near(sim.State.powerNetworks.Single(n => n.tier == 2).ratio, 0, "tripped grid allocation");
            var p = sim.AddPlayer("electrical-reset", "P"); p.x = 9; p.y = 5;
            sim.Apply(Command(p, "configure", breaker.x, breaker.y, 1));
            Require(breaker.tripped && machineIds.All(id => !Building(sim, id).powered), "persistent overload accepted a reset");
            Building(sim, machineIds[2]).hp = 0; sim.Step(WorldSimulator.FixedStep);
            Require(breaker.tripped && !Building(sim, machineIds[0]).powered, "reducing load silently auto-reset the breaker");
            p.interactCooldown = 0; sim.Apply(Command(p, "configure", breaker.x, breaker.y, 1));
            Require(!breaker.tripped && breaker.setting == 0 && Building(sim, machineIds[0]).powered, "safe manual reset did not reconnect actual consumers");
            Advance(sim, 3);
            Require(sim.State.lvProtected > 0 && machineIds.Take(2).Any(id => Building(sim, id).items[(int)ItemKind.Ammo] > 0), "reset failed to resume protected production");
        });
        Run("electrical breaker manual switch uses inverse stored setting and E only reports state", () =>
        {
            var sim = ElectricalBreakerFixture(out int breakerId, out int[] machineIds, false);
            var p = sim.AddPlayer("electrical-switch", "P"); p.x = 9; p.y = 5;
            var breaker = Building(sim, breakerId);
            sim.Apply(Command(p, "configure", breaker.x, breaker.y, 0));
            Require(breaker.setting == 1 && !Building(sim, machineIds[0]).powered && !breaker.tripped, "manual open did not isolate the grid");
            p.interactCooldown = 0; sim.Apply(Command(p, "interact", breaker.x, breaker.y));
            Require(breaker.setting == 1 && !breaker.tripped, "E interaction changed breaker state");
            foreach (int arg in new[] { -1, 2, int.MaxValue })
            {
                p.interactCooldown = 0; sim.Apply(Command(p, "configure", breaker.x, breaker.y, arg));
                Require(breaker.setting == 1, "invalid breaker command changed its setting");
            }
            sim = new WorldSimulator(Saved(sim.State)); breaker = Building(sim, breakerId); p = sim.State.players.Single();
            Require(breaker.setting == 1 && !Building(sim, machineIds[0]).powered, "restore reconnected a manually open breaker");
            p.interactCooldown = 0; sim.Apply(Command(p, "configure", breaker.x, breaker.y, 1));
            Require(breaker.setting == 0 && Building(sim, machineIds[0]).powered, "manual close failed after restore");
        });
        Run("electrical breaker preview and repeated current saves preserve a latched outage", () =>
        {
            var sim = ElectricalBreakerFixture(out int breakerId, out int[] machineIds);
            sim.Step(WorldSimulator.FixedStep);
            var state = Saved(sim.State); string before = Dump(state);
            for (int restore = 0; restore < 3; restore++)
            {
                sim = new WorldSimulator(state);
                Require(Building(sim, breakerId).tripped && !Building(sim, machineIds[0]).powered && sim.State.lvProtected == 0, "restoring a latch created protected output or restored supply");
                Require(Dump(state) == before, "restore mutated the caller's saved breaker state");
                state = Saved(sim.State); before = Dump(state);
            }
        });
        Run("electrical protection never trips or credits an independent remote LV network", () =>
        {
            var sim = ElectricalBreakerFixture(out int breakerId, out int[] localIds);
            var state = Saved(sim.State);
            Add(state, BuildKind.Generator, 40, 7).fuel = 20;
            var remote = Add(state, BuildKind.Assembler, 42, 10); remote.items[(int)ItemKind.Iron] = 10;
            sim = new WorldSimulator(state); Advance(sim, 3);
            Require(Building(sim, breakerId).tripped && localIds.All(id => !Building(sim, id).powered) &&
                Building(sim, remote.id).powered && Building(sim, remote.id).items[(int)ItemKind.Ammo] > 0, "local trip affected independent remote production");
            Require(sim.State.lvProtected == 0 && sim.State.lvElectric > 40, "a different network's breaker credited unprotected output");
        });
        Run("electrical unlocked sorting is required for both live power and cargo progression", () =>
        {
            var sim = ElectricalSplitterFixture(out int splitterId, out _, out _);
            var state = Saved(sim.State); state.lvResearch &= ~(1 << (int)LowVoltageTech.Sorting);
            state.buildings.Single(b => b.id == splitterId).items[(int)ItemKind.Ore] = 1;
            sim = new WorldSimulator(state); Advance(sim, 1);
            Require(!Building(sim, splitterId).powered && sim.State.cargo.Count == 0 && sim.State.lvSorted == 0, "locked sorting machinery transported or counted cargo");
        });
        Run("electrical breaker neither invents a no-generation trip nor trips at exact supply", () =>
        {
            var sim = ElectricalBreakerFixture(out int breakerId, out _, false);
            sim.State.buildings.Single(b => b.kind == BuildKind.Generator).fuel = 0;
            sim.Step(WorldSimulator.FixedStep);
            Require(!Building(sim, breakerId).tripped && sim.State.lvProtected == 0, "a powerless network manufactured a protection event");
            var state = Saved(sim.State);
            state.buildings.Single(b => b.kind == BuildKind.Generator).fuel = 20;
            Add(state, BuildKind.Assembler, 11, 10);
            Add(state, BuildKind.ElectricCrusher, 8, 13);
            Add(state, BuildKind.FilterSplitter, 11, 8);
            sim = new WorldSimulator(state); sim.Step(WorldSimulator.FixedStep);
            var network = sim.State.powerNetworks.Single(n => n.tier == 2);
            Near(network.demand, 120, "exact-capacity fixture demand");
            Require(!Building(sim, breakerId).tripped && network.ratio == 1f, "exactly matched generation incorrectly tripped");
        });
        Run("electrical splitter E loads a selected item and then unloads without changing its filter", () =>
        {
            var sim = ElectricalSplitterFixture(out int splitterId, out _, out _);
            var p = sim.AddPlayer("electrical-inventory", "P"); p.x = 10; p.y = 8;
            p.items[(int)ItemKind.CopperOre] = 3;
            sim.Apply(Command(p, "interact", 10, 10, (int)ItemKind.CopperOre));
            Require(Building(sim, splitterId).items[(int)ItemKind.CopperOre] == 3 && p.items[(int)ItemKind.CopperOre] == 0, "E did not load the requested inventory item");
            p.interactCooldown = 0; sim.Apply(Command(p, "interact", 10, 10, (int)ItemKind.CopperOre));
            Require(Building(sim, splitterId).items[(int)ItemKind.CopperOre] == 0 && p.items[(int)ItemKind.CopperOre] == 3 &&
                Building(sim, splitterId).setting == (int)ItemKind.Ore && sim.State.lvSorted == 0, "E lost inventory or changed filter/evidence");
        });
        Run("electrical splitter side route respects the receiving belt's output face", () =>
        {
            var sim = ElectricalSplitterFixture(out int splitterId, out _, out int leftId);
            var state = Saved(sim.State);
            var left = state.buildings.Single(b => b.id == leftId);
            left.kind = BuildKind.Belt; left.rotation = 3; left.hp = WorldSimulator.BuildingMaxHealth(left.kind);
            state.buildings.Single(b => b.id == splitterId).items[(int)ItemKind.Wood] = 1;
            sim = new WorldSimulator(state); var totals = ItemTotals(sim.State); Advance(sim, 1);
            Conserved(sim.State, totals);
            Require(sim.State.cargo.Count == 0 && Building(sim, splitterId).items[(int)ItemKind.Wood] == 1, "side port back-fed the receiving belt's output");
            Building(sim, leftId).rotation = 1; Advance(sim, 1); Conserved(sim.State, totals);
            Require(Building(sim, leftId).items[(int)ItemKind.Wood] == 1 && sim.State.lvSorted == 0, "correctly oriented receiving belt rejected side cargo");
        });
    }
}
