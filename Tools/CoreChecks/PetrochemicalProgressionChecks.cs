using DeadCircuit;

static partial class Checks
{
    static WorldState mvPlayerCheckpoint, mvPlayerEarned;
    static readonly ItemKind[] MvPriorMaterials =
    {
        ItemKind.Wood, ItemKind.Stone, ItemKind.Scrap, ItemKind.Food, ItemKind.Fuel, ItemKind.Clay,
        ItemKind.Brick, ItemKind.Charcoal, ItemKind.Copper, ItemKind.Iron, ItemKind.Bronze, ItemKind.Steel,
        ItemKind.IronPlate, ItemKind.SteelPlate, ItemKind.Gear, ItemKind.Bearing, ItemKind.RefractoryBrick,
        ItemKind.PipeFitting, ItemKind.CopperWire, ItemKind.Insulator, ItemKind.Cable, ItemKind.Coil,
        ItemKind.Motor, ItemKind.Relay, ItemKind.Circuit, ItemKind.Coal
    };
    static readonly ItemKind[] MvPlayerProducts =
    {
        ItemKind.CrudeOil, ItemKind.LightFraction, ItemKind.ChemicalFeedstock,
        ItemKind.HeavyFraction, ItemKind.Diesel, ItemKind.Polymer, ItemKind.Lubricant,
        ItemKind.ProcessWater, ItemKind.Coolant, ItemKind.HotCoolant
    };
    static readonly (string name, Func<WorldState, int> get, Action<WorldState, int> set, int minimum)[] MvPlayerCounters =
    {
        ("survey", s => s.mvSurvey, (s,n) => s.mvSurvey=n, 1),
        ("extracted", s => s.mvExtracted, (s,n) => s.mvExtracted=n, 12),
        ("piped", s => s.mvPiped, (s,n) => s.mvPiped=n, 8),
        ("distilled", s => s.mvDistilled, (s,n) => s.mvDistilled=n, 12),
        ("diesel", s => s.mvDiesel, (s,n) => s.mvDiesel=n, 4),
        ("polymer", s => s.mvPolymer, (s,n) => s.mvPolymer=n, 4),
        ("lubricant", s => s.mvLubricant, (s,n) => s.mvLubricant=n, 2),
        ("cooled", s => s.mvCooled, (s,n) => s.mvCooled=n, 4),
        ("powered", s => s.mvPowered, (s,n) => s.mvPowered=n, 4)
    };
    static int[] MvPlayerEvidence(WorldState s) =>
        new[] { s.mvResearch }.Concat(MvPlayerCounters.Select(c => c.get(s))).ToArray();

    static WorldSimulator MvPlayerStart(out PlayerState p)
    {
        // ONLY the completed LV history, shoreline, deposit and existing LV utilities
        // are synthetic. No petroleum item, MV machine or MV evidence is supplied.
        var s = Flat(era: TechEra.LowVoltage); s.era = TechEra.MediumVoltage;
        s.mvResearch = 0;
        foreach (var counter in MvPlayerCounters) counter.set(s, 0);
        foreach (var item in MvPlayerProducts) s.produced[(int)item] = 0;
        s.nextWaveAt = 9999;
        var ground = s.ground.ToCharArray();
        // This industrial fixture is an island: ambient edge spawns must not
        // destroy plumbing during a several-minute recipe route. Combat is not
        // under test; production, survival commands and material accounting are live.
        for (int y = 0; y < s.height; y++) for (int x = 0; x < s.width; x++)
            if (x < 4 || y < 4 || x >= s.width - 4 || y >= s.height - 4) ground[y * s.width + x] = '2';
        foreach (int y in new[] { 12, 22, 32 })
            for (int x = 12; x <= 30; x++) ground[y * s.width + x] = '2';
        s.ground = new string(ground);
        s.nodes.Add(new NodeState { id = s.nextId++, kind = NodeKind.Oil, x = 13, y = 10, stock = 2000 });
        Add(s, BuildKind.Workbench, 10, 10);
        foreach (var (x,y) in new[] { (14,7), (20,7), (26,7), (14,17) }) Add(s, BuildKind.Generator, x, y);
        var sim = new WorldSimulator(s); p = sim.AddPlayer("mv-builder", "MV builder");
        Array.Clear(p.items);
        RestockPriorEra(p, TechCatalog.ResearchCost(TechEra.HighVoltage), MvPriorMaterials);
        p.x = 8; p.y = 10;
        sim.AddPlayer("mv-peer", "MV teammate");
        Require(s.version == WorldSimulator.SaveVersion && LowVoltageCatalog.MediumVoltageRequirement(sim.State).Length == 0,
            "MV player fixture lacks current-save completed LV history");
        Require(MvPlayerEvidence(sim.State).All(n => n == 0), "MV entry inherited synthetic evidence");
        foreach (var item in MvPlayerProducts)
            Require(sim.State.players.All(q => q.items[(int)item] == 0) && sim.State.produced[(int)item] == 0 &&
                sim.State.buildings.All(b => b.items[(int)item] == 0), "startup gifted MV material: " + item);
        Require(sim.State.buildings.All(b => TechCatalog.RequiredEra(b.kind) <= TechEra.LowVoltage), "startup gifted MV machine");
        return sim;
    }

    static void MvPlayerAct(WorldSimulator sim, PlayerState p, string action, BuildingState b, int arg = 0)
    {
        Advance(sim, .55f); p.x = b.x; p.y = b.y - 1;
        sim.Apply(Command(p, action, b.x, b.y, arg));
        Require(p.hp > 0, "MV player died during " + action);
    }
    static BuildingState MvPlayerBuild(WorldSimulator sim, PlayerState p, BuildKind kind, int x, int y, int rotation = 0, ItemKind[] supplies = null)
    {
        RestockPriorEra(p, DemoCatalog.BuildCost(kind), supplies ?? MvPriorMaterials);
        Advance(sim, .55f); p.x = x; p.y = y - 1;
        int count = sim.State.buildings.Count; var before = Copy(p.items);
        sim.Apply(Command(p, "build", x, y, (int)kind, rotation));
        Require(sim.State.buildings.Count == count + 1, $"MV player cannot build {kind} at {x},{y}: {p.notice}");
        Paid(p.items, before, DemoCatalog.BuildCost(kind), "MV player build " + kind);
        return sim.State.buildings.Single(b => b.x == x && b.y == y && b.kind == kind);
    }
    static BuildingState MvPlayerPipe(WorldSimulator sim, PlayerState p, int x, int y, ItemKind item, int rotation = 0, bool tank = false)
    {
        int setting = Enumerable.Range(0, WorldSimulator.FluidCount).Single(i => WorldSimulator.FluidItem(i) == (int)item);
        var b = MvPlayerBuild(sim, p, tank ? BuildKind.LiquidTank : BuildKind.LiquidPipe, x, y, rotation);
        MvPlayerAct(sim, p, "configure", b, setting);
        Require(b.setting == setting, "player could not configure liquid type: " + item + ": " + p.notice);
        return b;
    }
    static void MvPlayerUnlock(WorldSimulator sim, PlayerState p, PetrochemicalTech id)
    {
        Advance(sim, .55f); p.x = 8; p.y = 10;
        RestockPriorEra(p, PetrochemicalCatalog.Definition((int)id).cost, MvPriorMaterials);
        var peer = sim.State.players.Single(q => q.id == "mv-peer");
        var before = Copy(p.items); var peerBefore = Copy(peer.items); int mask = sim.State.mvResearch;
        int xp = p.xp, skills = p.skillPoints, attributes = p.attributePoints;
        sim.Apply(Command(p, "mv", arg: (int)id));
        Require(sim.State.mvResearch == (mask | (1 << (int)id)), "MV research failed: " + id + ": " + p.notice);
        Paid(p.items, before, PetrochemicalCatalog.Definition((int)id).cost, "MV research " + id);
        Inventory(peer.items, peerBefore, "MV research charged teammate");
        Require(p.xp == xp && p.skillPoints == skills && p.attributePoints == attributes, "MV research spent character progression");
        Advance(sim, .55f); peer.x = 8; peer.y = 9; var paid = Copy(p.items);
        sim.Apply(Command(peer, "mv", arg: (int)id));
        Require(PetrochemicalCatalog.HasResearch(sim.State, id) && sim.State.mvResearch == (mask | (1 << (int)id)), "shared MV research duplicated");
        Inventory(peer.items, peerBefore, "replayed MV research charged teammate");
        Inventory(p.items, paid, "replayed MV research charged builder");
        peer.x = 6; peer.y = 6;
    }
    static void MvPlayerWait(WorldSimulator sim, PlayerState p, Func<bool> done, string message, Action service = null)
    {
        for (int n = 0; n < 240 && !done(); n++)
        {
            foreach (var g in sim.State.buildings.Where(b => b.kind == BuildKind.Generator || b.kind == BuildKind.DieselGenerator))
            {
                int fuel = (int)(g.kind == BuildKind.DieselGenerator ? ItemKind.Diesel : ItemKind.Fuel);
                if (g.items[fuel] >= 2) continue;
                if (g.kind == BuildKind.Generator)
                    RestockPriorEra(p, DemoCatalog.Materials(ItemKind.Fuel, 8), MvPriorMaterials);
                if (p.items[fuel] > 0) MvPlayerAct(sim, p, "interact", g);
            }
            if (p.hunger < 65)
            {
                RestockPriorEra(p, DemoCatalog.Materials(ItemKind.Food, 1), MvPriorMaterials);
                Advance(sim, .55f); sim.Apply(Command(p, "eat"));
            }
            service?.Invoke(); Advance(sim, 2);
            Require(p.hp > 0, "MV player died while waiting: " + message + "; " + MvPlayerDiagnostic(sim, p));
        }
        Require(done(), message + "; " + MvPlayerDiagnostic(sim, p));
    }
    static string MvPlayerDiagnostic(WorldSimulator sim, PlayerState p) =>
        "evidence=" + string.Join(",", MvPlayerEvidence(sim.State)) + "; player=" + MvPlayerInventory(p.items) +
        "; machines=" + string.Join(" | ", sim.State.buildings.Where(b => TechCatalog.EnergyTier(b) == 3 ||
            b.kind == BuildKind.LiquidPipe && b.y >= 29).Select(b =>
            $"{b.kind}@{b.x},{b.y}: power={b.powerRatio}, recipe={b.recipe}, type={b.setting}, items=[{MvPlayerInventory(b.items)}]")) + "; " + p.notice;
    static string MvPlayerInventory(int[] items) => string.Join(",",
        Enumerable.Range(0, items.Length).Where(i => items[i] > 0).Select(i => $"{(ItemKind)i}={items[i]}"));
    static void MvPlayerProduce(WorldSimulator sim, PlayerState p, BuildingState b, int recipe, ItemKind output, int minimum)
    {
        MvPlayerAct(sim, p, "recipe", b, recipe);
        Require(TechCatalog.SelectedRecipe(b)?.id == recipe, "MV player cannot select recipe " + recipe + ": " + p.notice);
        MvPlayerAct(sim, p, "interact", b);
        MvPlayerWait(sim, p, () => p.items[(int)output] >= minimum,
            $"MV recipe {recipe} failed to supply {minimum} {output}", () => MvPlayerAct(sim, p, "interact", b));
        Require(b.powered && b.powerRatio > 0, "MV bootstrap recipe ran without actual LV power");
    }

    static WorldState MvPlayerCheckpoint()
    {
        if (mvPlayerCheckpoint != null) return Saved(mvPlayerCheckpoint);
        var sim = MvPlayerStart(out var p);
        MvPlayerUnlock(sim, p, PetrochemicalTech.Prospecting);
        Advance(sim, .55f); sim.Apply(Command(p, "prospect"));
        Require(sim.State.mvSurvey == 1 && sim.State.mvExtracted == 0, "prospect did not observe fixture deposit without gifting oil");
        MvPlayerUnlock(sim, p, PetrochemicalTech.Extraction);
        var pump = MvPlayerBuild(sim, p, BuildKind.OilPump, 14, 10);
        MvPlayerPipe(sim, p, 16, 10, ItemKind.CrudeOil);
        var oil = MvPlayerPipe(sim, p, 17, 10, ItemKind.CrudeOil, tank: true);
        MvPlayerWait(sim, p, () => oil.items[(int)ItemKind.CrudeOil] >= 12 && sim.State.mvPiped >= 2, "extracted oil never arrived through liquid pipe");
        MvPlayerAct(sim, p, "interact", oil);
        Require(p.items[(int)ItemKind.CrudeOil] >= 12 && sim.State.mvExtracted >= 12, "tank collection lost extracted research samples");
        MvPlayerUnlock(sim, p, PetrochemicalTech.Distillation);
        var distill = MvPlayerBuild(sim, p, BuildKind.DistillationUnit, 20, 10);
        MvPlayerPipe(sim, p, 19, 10, ItemKind.CrudeOil);
        MvPlayerPipe(sim, p, 22, 10, ItemKind.LightFraction);
        var light = MvPlayerPipe(sim, p, 23, 10, ItemKind.LightFraction, tank: true);
        MvPlayerBuild(sim, p, BuildKind.ProcessWaterPump, 26, 10);
        MvPlayerPipe(sim, p, 28, 10, ItemKind.ProcessWater);
        var water = MvPlayerPipe(sim, p, 29, 10, ItemKind.ProcessWater, tank: true);
        MvPlayerAct(sim, p, "recipe", distill, 161);
        MvPlayerAct(sim, p, "interact", distill);
        MvPlayerWait(sim, p, () => p.items[(int)ItemKind.LightFraction] >= 64 &&
            p.items[(int)ItemKind.ChemicalFeedstock] >= 32 && p.items[(int)ItemKind.HeavyFraction] >= 32 &&
            p.items[(int)ItemKind.ProcessWater] >= 80, "player distillation/water supply did not bootstrap research", () =>
        {
            if (p.items[(int)ItemKind.ChemicalFeedstock] < 32 || p.items[(int)ItemKind.HeavyFraction] < 32)
            {
                RestockPriorEra(p, DemoCatalog.Materials(ItemKind.Fuel, 2), MvPriorMaterials);
                MvPlayerAct(sim, p, "interact", distill);
            }
            if (light.items[(int)ItemKind.LightFraction] > 0 && p.items[(int)ItemKind.LightFraction] < 64) MvPlayerAct(sim, p, "interact", light);
            if (water.items[(int)ItemKind.ProcessWater] > 0 && p.items[(int)ItemKind.ProcessWater] < 80)
                MvPlayerAct(sim, p, "interact", water);
        });
        Require(pump.powered && distill.powered && TechCatalog.EnergyTier(distill) == 2 && TechCatalog.NearWater(sim.State, distill),
            "bootstrap distillation lacks LV power or shoreline");
        MvPlayerUnlock(sim, p, PetrochemicalTech.Refining);
        var reactor = MvPlayerBuild(sim, p, BuildKind.ChemicalReactor, 14, 20);
        MvPlayerProduce(sim, p, reactor, 165, ItemKind.Diesel, 16);
        MvPlayerUnlock(sim, p, PetrochemicalTech.Polymerization);
        MvPlayerProduce(sim, p, reactor, 160, ItemKind.ChemicalFeedstock, p.items[(int)ItemKind.ChemicalFeedstock] + 32);
        MvPlayerProduce(sim, p, reactor, 70, ItemKind.Polymer, 32);
        Require(sim.State.mvPowered == 0 && sim.State.mvCooled == 0, "LV bootstrap fabricated MV power/cooling acceptance");
        MvPlayerUnlock(sim, p, PetrochemicalTech.Lubrication);
        MvPlayerProduce(sim, p, reactor, 162, ItemKind.Lubricant, 8);
        MvPlayerUnlock(sim, p, PetrochemicalTech.Transformation);
        MvPlayerProduce(sim, p, reactor, 71, ItemKind.Coolant, 12);
        // Interact loads up to 20 polymer back into recipe 71. Stop consuming it
        // before the save/build phase, and collect the unspent, genuinely made parts.
        MvPlayerAct(sim, p, "recipe", reactor, 165);
        MvPlayerAct(sim, p, "interact", reactor);
        MvPlayerUnlock(sim, p, PetrochemicalTech.Cooling);
        Require(sim.State.mvResearch == 255 && sim.State.mvCooled == 0 && sim.State.mvPowered == 0, "eight real studies did not retain pending industrial acceptance");
        MvPlayerAct(sim, p, "interact", reactor);
        // A genuinely active diesel batch plus an oil packet crosses this save.
        Require(p.items[(int)ItemKind.Polymer] >= 12 && p.items[(int)ItemKind.ChemicalFeedstock] >= 20,
            "checkpoint lost real construction/polymerization reserves: polymer=" + p.items[(int)ItemKind.Polymer] +
            ", feedstock=" + p.items[(int)ItemKind.ChemicalFeedstock]);
        MvPlayerWait(sim, p, () => reactor.progress > 0 && sim.State.cargo.Any(c => c.item == (int)ItemKind.CrudeOil),
            "mid-chain save lacks active recipe and in-flight extracted fluid");
        mvPlayerCheckpoint = Saved(sim.State);
        return Saved(mvPlayerCheckpoint);
    }

    static void MvPlayerFinish(WorldSimulator sim)
    {
        var p = sim.State.players.Single(q => q.id == "mv-builder");
        // Reserve a small haul by collecting from the outlet pipe while the tank retains bulk stock.
        int processWater = (int)ItemKind.ProcessWater;
        var waterSupply = sim.State.buildings.Single(b => b.kind == BuildKind.LiquidTank && b.x == 29 && b.y == 10);
        MvPlayerWait(sim, p, () => waterSupply.items[processWater] >= 20, "water pump failed to refill tower supply");
        var outlet = MvPlayerPipe(sim, p, DemoCatalog.OutputX(waterSupply), DemoCatalog.OutputY(waterSupply), ItemKind.ProcessWater);
        while (p.items[processWater] < 20)
        {
            LvProgressionAwait(sim, () => p.interactCooldown <= 0 && outlet.items[processWater] > 0,
                "pump outlet never offered water for the tower");
            p.x = outlet.x; p.y = outlet.y - 1;
            int carried = p.items[processWater];
            sim.Apply(Command(p, "interact", outlet.x, outlet.y));
            Require(p.items[processWater] > carried, "tower water could not be collected from the outlet");
        }
        Require(p.items[processWater] >= 20, "tower water samples were not collected");
        var diesel = MvPlayerBuild(sim, p, BuildKind.DieselGenerator, 14, 27);
        MvPlayerBuild(sim, p, BuildKind.MediumPole, 20, 27);
        var plant = MvPlayerBuild(sim, p, BuildKind.ChemicalPlant, 14, 30);
        var tower = MvPlayerBuild(sim, p, BuildKind.CoolingTower, 20, 30);
        MvPlayerAct(sim, p, "recipe", plant, 164);
        MvPlayerAct(sim, p, "recipe", tower, 91);
        var feed = MvPlayerPipe(sim, p, 13, 30, ItemKind.ChemicalFeedstock);
        var cold = MvPlayerPipe(sim, p, 14, 29, ItemKind.Coolant, rotation: 1);
        foreach (int x in new[] { 17, 18, 19 }) MvPlayerPipe(sim, p, x, 30, ItemKind.HotCoolant);
        var water = MvPlayerPipe(sim, p, 20, 29, ItemKind.ProcessWater, rotation: 1);
        MvPlayerPipe(sim, p, 23, 30, ItemKind.Coolant);
        var recovered = MvPlayerPipe(sim, p, 24, 30, ItemKind.Coolant, tank: true);
        int beforeDiesel = p.items[(int)ItemKind.Diesel];
        MvPlayerAct(sim, p, "interact", diesel);
        Require(p.items[(int)ItemKind.Diesel] < beforeDiesel && diesel.items[(int)ItemKind.Fuel] == 0, "MV power did not load genuinely refined Diesel");
        MvPlayerAct(sim, p, "interact", feed);
        MvPlayerAct(sim, p, "interact", water);
        Advance(sim, 8);
        Require(diesel.powerGeneration > 0 && plant.powered && plant.networkId == tower.networkId &&
            TechCatalog.EnergyTier(plant) == 3 && TechCatalog.NearWater(sim.State, tower), "actual MV diesel network/shoreline missing");
        Require(plant.items[(int)ItemKind.ChemicalFeedstock] >= 4 && plant.progress == 0 && sim.State.mvPowered == 0,
            "MV plant produced before coolant physically arrived");
        Require(p.items[(int)ItemKind.Coolant] >= 4, "MV plant lost genuine bootstrap coolant");
        MvPlayerAct(sim, p, "interact", cold);
        MvPlayerWait(sim, p, () => sim.State.mvPowered >= 16 && sim.State.mvCooled >= 4 &&
            recovered.items[(int)ItemKind.Coolant] >= 4, "MV polymer/hot coolant pipe/tower recovery chain stalled");
        MvPlayerAct(sim, p, "interact", plant);
        MvPlayerAct(sim, p, "interact", recovered);
        Require(sim.State.produced[(int)ItemKind.HotCoolant] >= 4 && sim.State.mvCooled >= 4, "tower acceptance lacks plant heat production");
        Require(MvPlayerCounters.All(c => c.get(sim.State) >= c.minimum) &&
            MvPlayerProducts.All(i => sim.State.produced[(int)i] > 0), "a petroleum acceptance/product was never actually earned");
        Require(PetrochemicalCatalog.HighVoltageRequirement(sim.State).Length == 0 &&
            TechCatalog.ResearchRequirement(sim.State, TechEra.HighVoltage).Length == 0, "earned MV chain cannot reach HV");
        RestockPriorEra(p, TechCatalog.ResearchCost(TechEra.HighVoltage), MvPriorMaterials);
        Advance(sim, .55f); p.x = 8; p.y = 10; var before = Copy(p.items);
        sim.Apply(Command(p, "research", arg: (int)TechEra.HighVoltage));
        Require(sim.State.era == TechEra.HighVoltage, "earned MV player cannot enter HV: " + p.notice);
        Paid(p.items, before, TechCatalog.ResearchCost(TechEra.HighVoltage), "earned HV samples");
    }
    static WorldState MvPlayerEarned()
    {
        if (mvPlayerEarned == null)
        {
            var sim = new WorldSimulator(MvPlayerCheckpoint()); MvPlayerFinish(sim);
            mvPlayerEarned = Saved(sim.State);
        }
        return Saved(mvPlayerEarned);
    }

    static void PetrochemicalProgressionChecks()
    {
        Run("MV progression zero entry rejects invalid research prospect and premature HV without charging", () =>
        {
            foreach (int id in new[] { -1, 8, 255, (int)PetrochemicalTech.Extraction, (int)PetrochemicalTech.Transformation, (int)PetrochemicalTech.Cooling })
            {
                var sim = MvPlayerStart(out var p); var before = Copy(p.items);
                sim.Apply(Command(p, "mv", arg: id));
                sim.Apply(Command(p, "prospect"));
                Ready(p); sim.Apply(Command(p, "research", arg: (int)TechEra.HighVoltage));
                Require(sim.State.era == TechEra.MediumVoltage && MvPlayerEvidence(sim.State).All(n => n == 0), "invalid MV action gifted research/evidence: " + id);
                Inventory(p.items, before, "rejected MV action charged player");
            }
        });
        Run("MV progression complete player chain earns eight studies actual cooling MV output and legal HV", () =>
        {
            var source = MvPlayerEarned(); var restored = new WorldSimulator(source);
            Require(restored.State.era == TechEra.HighVoltage && MvPlayerEvidence(source).SequenceEqual(MvPlayerEvidence(restored.State)),
                "current save lost genuinely earned HV acceptance");
        });
        Run("MV progression current save deep copies research counters fluids inventories and active packets", () =>
        {
            var source = MvPlayerCheckpoint(); string frozen = Dump(source); var restored = new WorldSimulator(source);
            Require(Dump(source) == frozen && !ReferenceEquals(source, restored.State), "MV restore reused/mutated caller world");
            Require(MvPlayerEvidence(source).SequenceEqual(MvPlayerEvidence(restored.State)), "MV restore lost evidence");
            Require(!ReferenceEquals(source.produced, restored.State.produced) && source.produced.SequenceEqual(restored.State.produced), "MV production totals aliased/lost");
            foreach (var p in source.players)
            {
                var copy = restored.State.players.Single(q => q.id == p.id);
                Require(!ReferenceEquals(p.items, copy.items) && p.items.SequenceEqual(copy.items), "MV player inventory aliased/lost");
            }
            foreach (var b in source.buildings)
            {
                var copy = Building(restored, b.id);
                Require(!ReferenceEquals(b.items, copy.items) && b.items.SequenceEqual(copy.items), "MV machine fluid inventory aliased/lost");
                Require(b.recipe == copy.recipe && b.setting == copy.setting, "MV recipe/liquid selection lost");
                Near(copy.progress, b.progress, "MV active recipe progress lost"); Near(copy.fuel, b.fuel, "MV fuel reserve lost");
            }
            foreach (var c in source.cargo)
            {
                var copy = restored.State.cargo.Single(q => q.id == c.id);
                Require(!ReferenceEquals(c, copy) && c.fromId == copy.fromId && c.toId == copy.toId && c.item == copy.item, "MV packet aliased/lost");
                Near(copy.progress, c.progress, "MV liquid flight progress lost");
            }
            Advance(restored, 2); Require(Dump(source) == frozen, "restored simulation mutated caller MV save");
            string isolated = Dump(restored.State);
            source.players[0].items[(int)ItemKind.Cable]++; source.produced[(int)ItemKind.CrudeOil]++;
            source.buildings.Single(b => b.kind == BuildKind.ChemicalReactor).items[(int)ItemKind.Coolant]++;
            source.cargo[0].progress = 0; source.nodes.Single(n => n.kind == NodeKind.Oil).stock--;
            Require(Dump(restored.State) == isolated, "caller mutation leaked into restored MV graph");
        });
        Run("MV progression mid-chain save resumes processing and fluid arrivals deterministically through HV", () =>
        {
            var control = new WorldSimulator(MvPlayerCheckpoint()); var source = Saved(control.State); string frozen = Dump(source);
            var resumed = new WorldSimulator(source); int before = resumed.State.mvPiped;
            for (int n = 0; n < 80; n++) { control.Step(WorldSimulator.FixedStep); resumed.Step(WorldSimulator.FixedStep); }
            Require(Dump(control.State) == Dump(resumed.State) && resumed.State.mvPiped > before, "MV restored packet/process diverged or never arrived");
            MvPlayerFinish(control); MvPlayerFinish(resumed);
            Require(Dump(control.State) == Dump(resumed.State) && resumed.State.era == TechEra.HighVoltage, "mid-chain continuation diverged before HV");
            Require(Dump(source) == frozen, "MV continuation mutated checkpoint");
        });
        Run("MV progression malformed current saves reject illegal research masks prerequisites and counters", () =>
        {
            // Deliberately corrupt clones ONLY for rejection. These never supply
            // positive evidence to MvPlayerCheckpoint/Finish/Earned.
            foreach (int mask in new[] { -1, 256, int.MaxValue, 1 << (int)PetrochemicalTech.Extraction, 1 << (int)PetrochemicalTech.Transformation })
            {
                var bad = Saved(MvPlayerStart(out _).State); bad.mvResearch = mask;
                Reject(bad, "current MV save accepted illegal research mask/prerequisites " + mask);
            }
            foreach (var counter in MvPlayerCounters)
            {
                var bad = Saved(MvPlayerStart(out _).State); counter.set(bad, -1);
                Reject(bad, "current MV save accepted negative " + counter.name);
                bad = MvPlayerEarned(); counter.set(bad, counter.minimum - 1);
                Reject(bad, "HV save accepted insufficient earned MV threshold: " + counter.name);
                bad = Saved(MvPlayerStart(out _).State); counter.set(bad, 1);
                Reject(bad, "MV save accepted evidence before its research: " + counter.name);
            }
            var overflow = MvPlayerEarned(); overflow.mvPowered = overflow.mvPolymer + 1;
            Reject(overflow, "MV save accepted powered polymer beyond total polymer");
            var survey = Saved(MvPlayerStart(out _).State); survey.mvResearch = 1; survey.mvSurvey = 2;
            Reject(survey, "MV save accepted invalid survey count");
            var early = MvPlayerCheckpoint(); early.era = TechEra.LowVoltage;
            Reject(early, "LV save retained future MV research/evidence");
        });
    }
}
