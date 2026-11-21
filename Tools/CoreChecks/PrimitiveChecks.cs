using DeadCircuit;

static partial class Checks
{
    static WorldSimulator PrimitiveFixture(out PlayerState p, int research = 0, Action<WorldState> arrange = null)
    {
        var state = Flat(era: TechEra.Primitive);
        state.version = WorldSimulator.SaveVersion; state.primitiveVersion = 1; state.primitiveResearch = research;
        Add(state, BuildKind.Workbench, 10, 10);
        arrange?.Invoke(state);
        var sim = new WorldSimulator(state); p = Builder(sim); p.x = 8; p.y = 10;
        return sim;
    }

    static void PrimitiveUnlock(WorldSimulator sim, PlayerState p, PrimitiveTech tech, int[] cost)
    {
        Advance(sim, .55f); p.x = 8; p.y = 10; var before = Copy(p.items);
        int xp = p.xp, points = p.attributePoints, skills = p.skillPoints;
        sim.Apply(Command(p, "primitive", arg: (int)tech));
        Require((sim.State.primitiveResearch & (1 << (int)tech)) != 0, "research failed: " + tech + "; " + p.notice);
        Paid(p.items, before, cost, "research cost: " + tech);
        Require(p.xp == xp && p.attributePoints == points && p.skillPoints == skills, "research spent progression: " + tech);
    }

    static BuildingState PrimitiveBuild(WorldSimulator sim, PlayerState p, BuildKind kind, int x, int y)
    {
        Advance(sim, .55f); p.x = x - 1; p.y = y;
        int count = sim.State.buildings.Count; var before = Copy(p.items);
        sim.Apply(Command(p, "build", x, y, (int)kind));
        Require(sim.State.buildings.Count == count + 1, "production chain could not build " + kind + "; " + p.notice);
        Paid(p.items, before, DemoCatalog.BuildCost(kind), "chain building cost");
        return sim.State.buildings.Single(b => b.kind == kind && b.x == x && b.y == y);
    }

    static void PrimitiveInteract(WorldSimulator sim, PlayerState p, BuildingState b, string action = "interact", int arg = 0)
    {
        Advance(sim, .55f); p.x = b.x - 1; p.y = b.y;
        sim.Apply(Command(p, action, b.x, b.y, arg));
    }

    // Add unrelated, legal cargo until there is exactly this much free space.
    static void FillBackpackLeaving(PlayerState p, int freeGrams)
    {
        int wood = (int)ItemKind.Wood, ammo = (int)ItemKind.Ammo;
        int ballast = checked((int)(InventoryWeight.CapacityGrams(p) - freeGrams - InventoryWeight.CarriedGrams(p)));
        Require(ballast >= 0, "fixture already exceeds the requested backpack weight");
        int woodGrams = InventoryWeight.Grams(ItemKind.Wood), ammoGrams = InventoryWeight.Grams(ItemKind.Ammo);
        int logs = ballast / woodGrams;
        while (logs >= 0 && (ballast - logs * woodGrams) % ammoGrams != 0) logs--;
        Require(logs >= 0, "fixture cannot reach the requested backpack weight");
        p.items[wood] += logs;
        p.items[ammo] += (ballast - logs * woodGrams) / ammoGrams;
        Require(InventoryWeight.CarriedGrams(p) == InventoryWeight.CapacityGrams(p) - freeGrams,
            "fixture backpack weight differs from the requested boundary");
    }

    static void PrimitiveChecks()
    {
        Run("primitive bronze recipe belongs to the primitive era", () =>
        {
            Require(TechCatalog.Recipe(2)?.era == TechEra.Primitive,
                "bronze recipe must be Primitive so primitive research can unlock it before Water");
        });
        Run("primitive tools research pays personal materials and shares team knowledge", () =>
        {
            var sim = PrimitiveFixture(out var p);
            var teammate = Builder(sim, "teammate");
            var before = Copy(p.items); var otherBefore = Copy(teammate.items);
            int xp = p.xp, attributes = p.attributePoints, skills = p.skillPoints;
            sim.Apply(Command(p, "primitive", arg: 0));
            Require(sim.State.primitiveResearch == 1, "Tools command did not unlock shared world research");
            var expected = Copy(before); expected[(int)ItemKind.Wood] -= 2; expected[(int)ItemKind.Stone] -= 2;
            Inventory(p.items, expected, "Tools personal cost");
            Inventory(teammate.items, otherBefore, "teammate was charged for research");
            Require(p.xp == xp && p.attributePoints == attributes && p.skillPoints == skills, "research altered character progression");
            Require(p.interactCooldown > 0, "successful research did not start cooldown");
            Require(PrimitiveCatalog.HasResearch(sim.State, PrimitiveTech.Tools), "teammate cannot use shared Tools research");
            before = Copy(p.items); Ready(p); sim.Apply(Command(p, "primitive", arg: 0));
            Inventory(p.items, before, "duplicate research charged twice");
            Require(sim.State.primitiveResearch == 1, "repeated research changed shared knowledge");
        });
        Run("primitive research rejects short materials skips cooldown range and occlusion atomically", () =>
        {
            foreach (string rejection in new[] { "short", "skip", "negative", "huge", "cooldown", "range", "wall", "missing", "dead", "offline" })
            {
                var sim = PrimitiveFixture(out var p, arrange: s =>
                {
                    if (rejection == "wall") Add(s, BuildKind.Wall, 9, 10);
                    if (rejection == "missing") s.buildings.Clear();
                });
                if (rejection == "short") p.items[(int)ItemKind.Stone] = 1;
                if (rejection == "cooldown") p.interactCooldown = 1;
                if (rejection == "range") p.x = 3;
                if (rejection == "dead") p.hp = 0;
                if (rejection == "offline") sim.SetConnected(p.id, false);
                int arg = rejection == "skip" ? 1 : rejection == "negative" ? -1 : rejection == "huge" ? int.MaxValue : 0;
                var before = Copy(p.items); int xp = p.xp, skills = p.skillPoints, attributes = p.attributePoints;
                float cooldown = p.interactCooldown;
                sim.Apply(Command(p, "primitive", arg: arg));
                Require(sim.State.primitiveResearch == 0 && sim.State.era == TechEra.Primitive, "rejected research unlocked knowledge: " + rejection);
                Inventory(p.items, before, "rejected research paid: " + rejection);
                Require(p.xp == xp && p.skillPoints == skills && p.attributePoints == attributes && p.interactCooldown == cooldown, "rejected research changed progression or cooldown: " + rejection);
            }
        });
        Run("primitive new world closes research builds equipment and manual bronze", () =>
        {
            var fresh = new WorldSimulator(73149); var newcomer = fresh.AddPlayer("new", "New");
            Require(fresh.State.primitiveVersion == 1 && fresh.State.primitiveResearch == 0, "new world did not opt into primitive research");
            Require(!newcomer.gear.Any(g => g.kind == GearKind.StoneAxe || g.kind == GearKind.StonePickaxe || g.kind == GearKind.Knife), "new world grants locked starter equipment");
            foreach (BuildKind kind in new[] { BuildKind.CharcoalKiln, BuildKind.ClayKiln, BuildKind.Wall, BuildKind.Door, BuildKind.Chest, BuildKind.Belt })
            {
                var sim = PrimitiveFixture(out var p); p.x = 8; p.y = 12; var before = Copy(p.items);
                sim.Apply(Command(p, "build", 10, 12, (int)kind));
                Require(sim.State.buildings.Count == 1, "funded locked building constructed: " + kind);
                Inventory(p.items, before, "locked building paid: " + kind);
            }
            var gearSim = PrimitiveFixture(out var smith); var beforeGear = Copy(smith.items);
            gearSim.Apply(Command(smith, "gearcraft", arg: (int)GearKind.Knife));
            Require(smith.gear.Count == 0, "funded locked knife crafted");
            Inventory(smith.items, beforeGear, "locked knife paid");
            var craft = PrimitiveFixture(out var crafter); var materials = Copy(crafter.items);
            craft.Apply(Command(crafter, "craft", arg: 2));
            Inventory(crafter.items, materials, "locked manual bronze bypassed research");
            Require(craft.State.produced[(int)ItemKind.Bronze] == 0, "locked bronze awarded production");
        });
        Run("primitive workbench and stone furnace can be built before tools", () =>
        {
            foreach (BuildKind kind in new[] { BuildKind.Workbench, BuildKind.StoneFurnace })
            {
                var sim = PrimitiveFixture(out var p); p.x = 8; p.y = 12; var before = Copy(p.items);
                sim.Apply(Command(p, "build", 10, 12, (int)kind));
                Require(sim.State.buildings.Count == 2 && sim.State.buildings.Last().kind == kind, "primitive starter station cannot be built: " + kind);
                Paid(p.items, before, DemoCatalog.BuildCost(kind), "starter station construction");
            }
        });
        Run("primitive construction unlocks walls doors chests belts and bloomery unlocks knife", () =>
        {
            foreach (BuildKind kind in new[] { BuildKind.Wall, BuildKind.Door, BuildKind.Chest, BuildKind.Belt })
            {
                var sim = PrimitiveFixture(out var p, 1);
                PrimitiveUnlock(sim, p, PrimitiveTech.Construction, DemoCatalog.Materials(ItemKind.Wood, 4, ItemKind.Stone, 4));
                PrimitiveBuild(sim, p, kind, 14, 10);
            }
            var gearWorld = PrimitiveFixture(out var crafter, 31, s => s.produced[(int)ItemKind.Bronze] = 4);
            crafter.items = DemoCatalog.Materials(ItemKind.Bronze, 2, ItemKind.Brick, 4, ItemKind.Scrap, 1, ItemKind.Iron, 2);
            PrimitiveUnlock(gearWorld, crafter, PrimitiveTech.Bloomery, DemoCatalog.Materials(ItemKind.Bronze, 2, ItemKind.Brick, 4));
            Advance(gearWorld, .55f); int count = crafter.gear.Count; var before = Copy(crafter.items);
            gearWorld.Apply(Command(crafter, "gearcraft", arg: (int)GearKind.Knife));
            Require(crafter.gear.Count == count + 1 && crafter.gear.Last().kind == GearKind.Knife, "Bloomery did not enable knife crafting");
            Paid(crafter.items, before, EquipmentCatalog.Gear(GearKind.Knife).cost, "researched knife cost");
        });
        Run("primitive default furnace and locked recipe commands do not burn inputs", () =>
        {
            foreach (int recipe in new[] { 0, 10, 11, 12 })
            {
                var sim = PrimitiveFixture(out var p, arrange: s =>
                {
                    var b = Add(s, BuildKind.StoneFurnace, 14, 10); b.recipe = recipe;
                    b.items[(int)ItemKind.Fuel] = 2; b.items[(int)ItemKind.Ore] = 2;
                    b.items[(int)ItemKind.Charcoal] = 2;
                    b.items[(int)ItemKind.CopperOre] = 2; b.items[(int)ItemKind.TinOre] = 2;
                });
                p.x = 13; var machine = sim.State.buildings.Single(b => b.kind == BuildKind.StoneFurnace);
                var before = Copy(machine.items); int initial = machine.recipe;
                foreach (string action in new[] { "recipe", "configure" })
                {
                    Ready(p); sim.Apply(Command(p, action, 14, 10, recipe == 0 ? 11 : recipe));
                    Require(machine.recipe == initial, "locked recipe was selected through " + action);
                }
                Advance(sim, 4);
                Inventory(machine.items, before, "locked furnace consumed fuel or raw material");
                Require(machine.progress == 0 && machine.fuel == 0 && sim.State.produced.Sum() == 0, "locked default furnace progressed or recorded production");
            }
        });
        Run("primitive production milestones cannot be replaced by held materials", () =>
        {
            foreach (var entry in new[] { (mask: 7, tech: 3), (mask: 15, tech: 4), (mask: 31, tech: 5) })
            {
                var sim = PrimitiveFixture(out var p, entry.mask); var before = Copy(p.items);
                sim.Apply(Command(p, "primitive", arg: entry.tech));
                Inventory(p.items, before, "held materials bypassed actual production milestone");
                Require(sim.State.primitiveResearch == entry.mask && sim.State.produced.Sum() == 0, "research manufactured a production milestone");
            }
        });
        Run("primitive research milestones reject near misses and accept exact production totals", () =>
        {
            foreach (var entry in new (int mask, int tech, int brick, int copper, int tin, int bronze, bool allowed)[] { (7, 3, 3, 0, 0, 0, false),
                (7, 3, 4, 0, 0, 0, true), (15, 4, 4, 1, 1, 0, false), (15, 4, 4, 2, 0, 0, false),
                (15, 4, 4, 2, 1, 0, true), (31, 5, 4, 2, 1, 3, false), (31, 5, 4, 2, 1, 4, true) })
            {
                var sim = PrimitiveFixture(out var p, entry.mask, s =>
                {
                    s.produced[(int)ItemKind.Brick] = entry.brick; s.produced[(int)ItemKind.Copper] = entry.copper;
                    s.produced[(int)ItemKind.Tin] = entry.tin; s.produced[(int)ItemKind.Bronze] = entry.bronze;
                });
                p.items = Copy(PrimitiveCatalog.Definition(entry.tech).cost);
                var before = Copy(p.items); sim.Apply(Command(p, "primitive", arg: entry.tech));
                Require(sim.State.primitiveResearch == (entry.allowed ? entry.mask | (1 << entry.tech) : entry.mask), "research production boundary is wrong for tech " + entry.tech);
                if (!entry.allowed) Inventory(p.items, before, "near-miss production milestone was charged");
            }
        });
        Run("primitive kiln batches require research and exactly four seconds", () =>
        {
            foreach (var entry in new[] { (kind: BuildKind.CharcoalKiln, input: ItemKind.Wood, output: ItemKind.Charcoal, quantity: 2),
                (kind: BuildKind.ClayKiln, input: ItemKind.Clay, output: ItemKind.Brick, quantity: 4) })
            foreach (int research in new[] { 0, 7 })
            {
                var sim = PrimitiveFixture(out var p, research, s =>
                {
                    var b = Add(s, entry.kind, 14, 10); b.items[(int)entry.input] = 4;
                    if (entry.kind == BuildKind.ClayKiln) b.items[(int)ItemKind.Charcoal] = 1;
                });
                var b = sim.State.buildings.Single(b => b.kind == entry.kind); var before = Copy(b.items);
                Advance(sim, 3.95f); Inventory(b.items, before, "kiln consumed batch before four seconds");
                sim.Step(WorldSimulator.FixedStep);
                if (research == 0)
                {
                    Inventory(b.items, before, "unresearched kiln consumed a batch");
                    Require(b.progress == 0 && sim.State.produced[(int)entry.output] == 0, "unresearched kiln awarded progress");
                }
                else
                {
                    var expected = Copy(before); expected[(int)entry.input] -= 4; expected[(int)entry.output] += entry.quantity;
                    if (entry.kind == BuildKind.ClayKiln) expected[(int)ItemKind.Charcoal]--;
                    Inventory(b.items, expected, "kiln four-second batch");
                    Require(sim.State.produced[(int)entry.output] == entry.quantity, "kiln production counter does not count actual output");
                }
            }
        });
        Run("primitive bronze still crafts at full backpack weight", () =>
        {
            var sim = PrimitiveFixture(out var p, 31);
            p.items = DemoCatalog.Materials(ItemKind.Copper, 3, ItemKind.Tin, 1);
            FillBackpackLeaving(p, 0);
            var expected = Copy(p.items);
            expected[(int)ItemKind.Copper] -= 3;
            expected[(int)ItemKind.Tin] -= 1;
            expected[(int)ItemKind.Bronze] += 4;
            sim.Apply(Command(p, "craft", arg: 2));
            Inventory(p.items, expected, "bronze crafting balance at the weight limit");
            Require(sim.State.produced[(int)ItemKind.Bronze] == 4 && InventoryWeight.CarriedGrams(p) == InventoryWeight.CapacityGrams(p),
                "weight-neutral crafting did not complete at the full backpack weight");
        });
        Run("primitive crafting rejects short inputs and insufficient backpack weight without side effects", () =>
        {
            foreach (string rejection in new[] { "short", "full" })
            {
                var sim = PrimitiveFixture(out var p, 63);
                bool full = rejection == "full";
                // Bronze trades equal weight. Scrap adds weight and needs room for the net increase.
                int recipe = full ? 3 : 2;
                p.items = full ? DemoCatalog.Materials(ItemKind.Iron, 1) : DemoCatalog.Materials(ItemKind.Copper, 2, ItemKind.Tin, 1);
                if (full) FillBackpackLeaving(p, 3 * InventoryWeight.Grams(ItemKind.Scrap) - InventoryWeight.Grams(ItemKind.Iron) - 5);
                var before = Copy(p.items); var produced = Copy(sim.State.produced);
                sim.Apply(Command(p, "craft", arg: recipe));
                Inventory(p.items, before, "rejected craft consumed material: " + rejection);
                Inventory(sim.State.produced, produced, "rejected craft awarded production");
                Require(p.interactCooldown == 0, "rejected craft started cooldown");
            }
        });
        Run("primitive kiln refuses full output without consuming input", () =>
        {
            foreach (var entry in new[] { (kind: BuildKind.CharcoalKiln, input: ItemKind.Wood, output: ItemKind.Charcoal, amount: 4, room: 1),
                (kind: BuildKind.ClayKiln, input: ItemKind.Clay, output: ItemKind.Brick, amount: 4, room: 3) })
            {
                var sim = PrimitiveFixture(out var p, 7, s =>
                {
                    var b = Add(s, entry.kind, 14, 10); b.items[(int)entry.input] = entry.amount;
                    b.items[(int)entry.output] = WorldSimulator.BufferLimit - entry.room;
                    if (entry.kind == BuildKind.ClayKiln) b.items[(int)ItemKind.Charcoal] = 1;
                });
                var b = sim.State.buildings.Single(b => b.kind == entry.kind); var before = Copy(b.items);
                Advance(sim, 4.1f);
                Inventory(b.items, before, "full kiln consumed its batch");
                Require(b.progress == 0 && sim.State.produced[(int)entry.output] == 0, "full kiln progressed or awarded production");
            }
        });
        Run("primitive real kiln copper tin bronze iron chain unlocks water", () =>
        {
            var sim = PrimitiveFixture(out var p);
            GrantReinforcedBackpack(sim.State, p);
            Array.Clear(p.items);
            p.items[(int)ItemKind.Wood] = 75; p.items[(int)ItemKind.Stone] = 24; p.items[(int)ItemKind.Clay] = 28;
            p.items[(int)ItemKind.Ore] = 20; p.items[(int)ItemKind.CopperOre] = 20; p.items[(int)ItemKind.TinOre] = 20;
            PrimitiveUnlock(sim, p, PrimitiveTech.Tools, DemoCatalog.Materials(ItemKind.Wood, 2, ItemKind.Stone, 2));
            PrimitiveUnlock(sim, p, PrimitiveTech.Charcoal, DemoCatalog.Materials(ItemKind.Wood, 4, ItemKind.Stone, 2));
            var charcoal = PrimitiveBuild(sim, p, BuildKind.CharcoalKiln, 14, 10);
            PrimitiveInteract(sim, p, charcoal); Advance(sim, 12.1f); PrimitiveInteract(sim, p, charcoal);
            Require(p.items[(int)ItemKind.Charcoal] >= 6 && sim.State.produced[(int)ItemKind.Charcoal] >= 6, "charcoal kiln did not make real charcoal");
            PrimitiveUnlock(sim, p, PrimitiveTech.Ceramics, DemoCatalog.Materials(ItemKind.Clay, 4, ItemKind.Charcoal, 1));
            var clay = PrimitiveBuild(sim, p, BuildKind.ClayKiln, 18, 10);
            PrimitiveInteract(sim, p, clay); Advance(sim, 8.1f); PrimitiveInteract(sim, p, clay);
            Require(p.items[(int)ItemKind.Brick] >= 8 && sim.State.produced[(int)ItemKind.Brick] >= 8, "clay kiln did not make real bricks");
            PrimitiveInteract(sim, p, charcoal);
            PrimitiveUnlock(sim, p, PrimitiveTech.CopperTin, DemoCatalog.Materials(ItemKind.Brick, 2, ItemKind.Charcoal, 1));
            PrimitiveInteract(sim, p, charcoal);
            p.items[(int)ItemKind.Wood] = Math.Max(p.items[(int)ItemKind.Wood], 22);
            var furnace = PrimitiveBuild(sim, p, BuildKind.StoneFurnace, 22, 10);
            PrimitiveInteract(sim, p, furnace, "recipe", 11);
            Require(furnace.recipe == 11, "researched copper recipe remained closed");
            PrimitiveInteract(sim, p, furnace); Advance(sim, 15.1f); PrimitiveInteract(sim, p, furnace);
            Require(p.items[(int)ItemKind.Copper] >= 5 && sim.State.produced[(int)ItemKind.Copper] >= 5, "stone furnace did not smelt copper");
            PrimitiveInteract(sim, p, furnace, "recipe", 12);
            PrimitiveInteract(sim, p, furnace); Advance(sim, 6.1f); PrimitiveInteract(sim, p, furnace);
            Require(p.items[(int)ItemKind.Tin] >= 2 && sim.State.produced[(int)ItemKind.Tin] >= 2, "stone furnace did not smelt tin");
            PrimitiveUnlock(sim, p, PrimitiveTech.Bronze, DemoCatalog.Materials(ItemKind.Copper, 2, ItemKind.Tin, 1));
            Advance(sim, .55f); p.x = 8; p.y = 10; var before = Copy(p.items);
            sim.Apply(Command(p, "craft", arg: 2));
            var expected = Copy(before); expected[(int)ItemKind.Copper] -= 3; expected[(int)ItemKind.Tin]--; expected[(int)ItemKind.Bronze] += 4;
            Inventory(p.items, expected, "primitive bronze output batch");
            Require(sim.State.produced[(int)ItemKind.Bronze] == 4 && sim.State.era == TechEra.Primitive, "bronze production skipped or advanced era");
            PrimitiveUnlock(sim, p, PrimitiveTech.Bloomery, DemoCatalog.Materials(ItemKind.Bronze, 2, ItemKind.Brick, 4));
            PrimitiveInteract(sim, p, furnace, "recipe", 10);
            PrimitiveInteract(sim, p, furnace); Advance(sim, 18.1f); PrimitiveInteract(sim, p, furnace);
            Require(p.items[(int)ItemKind.Iron] >= 6 && sim.State.produced[(int)ItemKind.Iron] >= 6, "stone furnace did not smelt six iron");
            Advance(sim, .55f); p.x = 8; p.y = 10; before = Copy(p.items);
            sim.Apply(Command(p, "research", arg: (int)TechEra.Water));
            Require(sim.State.era == TechEra.Primitive, "water bypassed Construction research");
            Inventory(p.items, before, "rejected water research charged");
            PrimitiveUnlock(sim, p, PrimitiveTech.Construction, DemoCatalog.Materials(ItemKind.Wood, 4, ItemKind.Stone, 4));
            Advance(sim, .55f);
            sim.Apply(Command(p, "build", 9, 12, (int)BuildKind.ResearchBench));
            Require(sim.State.buildings.Any(building => building.kind == BuildKind.ResearchBench), "Construction research did not unlock the research bench");
            p.items[(int)ItemKind.Wood] = Math.Max(p.items[(int)ItemKind.Wood], 12);
            Advance(sim, .55f); before = Copy(p.items);
            sim.Apply(Command(p, "research", arg: (int)TechEra.Water));
            Require(sim.State.era == TechEra.Water, "actual primitive production chain could not unlock Water: " + p.notice);
            Paid(p.items, before, DemoCatalog.Materials(ItemKind.Wood, 12, ItemKind.Iron, 6), "water era research cost");
        });
        Run("primitive water research requires each technology and every produced threshold", () =>
        {
            foreach (string missing in new[] { "bronze-tech", "iron-tech", "construction", "bronze", "iron", "brick", "charcoal", "none" })
            {
                int mask = missing == "bronze-tech" ? 79 : missing == "iron-tech" ? 95 : missing == "construction" ? 63 : 127;
                var sim = PrimitiveFixture(out var p, mask, s =>
                {
                    s.produced[(int)ItemKind.Bronze] = missing == "bronze" ? 3 : 4;
                    s.produced[(int)ItemKind.Iron] = missing == "iron" ? 5 : 6;
                    s.produced[(int)ItemKind.Brick] = missing == "brick" ? 3 : 4;
                    s.produced[(int)ItemKind.Charcoal] = missing == "charcoal" ? 1 : 2;
                });
                Add(sim.State, BuildKind.ResearchBench, 9, 12);
                var before = Copy(p.items); sim.Apply(Command(p, "research", arg: (int)TechEra.Water));
                if (missing == "none")
                {
                    Require(sim.State.era == TechEra.Water, "completed primitive gates rejected water research");
                    Paid(p.items, before, DemoCatalog.Materials(ItemKind.Wood, 12, ItemKind.Iron, 6), "water positive control");
                }
                else
                {
                    Require(sim.State.era == TechEra.Primitive, "held materials bypassed water gate: " + missing);
                    Inventory(p.items, before, "water prerequisite rejection charged: " + missing);
                }
            }
        });
        Run("primitive cooking research enables food and clean water without spending character points", () =>
        {
            var sim = PrimitiveFixture(out var p, 7, s =>
            {
                s.survival = new SurvivalWorldState { enabled = true, initialized = true };
                Add(s, BuildKind.StoneFurnace, 14, 10);
            });
            p.x = 13; p.needs.foods[(int)FoodKind.RawMeat] = 1; p.needs.waterRaw = 1;
            int clean = p.needs.waterClean; var before = Copy(p.items);
            sim.Apply(Command(p, "cook", 14, 10, 0));
            sim.Apply(Command(p, "boilwater", 14, 10));
            Inventory(p.items, before, "locked cooking or boiling spent fuel");
            Require(p.needs.foods[(int)FoodKind.RawMeat] == 1 && p.needs.foods[(int)FoodKind.RoastMeat] == 0 && p.needs.waterRaw == 1 && p.needs.waterClean == clean, "locked cooking or boiling changed food and water");
            PrimitiveUnlock(sim, p, PrimitiveTech.Cooking, DemoCatalog.Materials(ItemKind.Brick, 2, ItemKind.Clay, 2));
            p.x = 13; Advance(sim, .55f); int fuel = p.items[(int)ItemKind.Fuel];
            sim.Apply(Command(p, "cook", 14, 10, 0));
            Require(p.needs.foods[(int)FoodKind.RawMeat] == 0 && p.needs.foods[(int)FoodKind.RoastMeat] == 1 && p.items[(int)ItemKind.Fuel] == fuel - 1, "Cooking did not enable actual food processing");
            Advance(sim, .55f); sim.Apply(Command(p, "boilwater", 14, 10));
            Require(p.needs.waterRaw == 0 && p.needs.waterClean == clean + 1 && p.items[(int)ItemKind.Fuel] == fuel - 2, "Cooking did not enable actual water purification");
        });
        Run("primitive stone and clay harvesting preserve stock at full capacity", () =>
        {
            foreach (var entry in new[] { (node: NodeKind.Stone, item: ItemKind.Stone), (node: NodeKind.Clay, item: ItemKind.Clay) })
            {
                var sim = PrimitiveFixture(out var p, 1, s => s.nodes.Add(new NodeState { id = s.nextId++, kind = entry.node, x = 14, y = 10, stock = 6 }));
                p.items = Copy(EquipmentCatalog.Gear(GearKind.StonePickaxe).cost);
                sim.Apply(Command(p, "gearcraft", arg: (int)GearKind.StonePickaxe));
                var pickaxe = p.gear.Single(g => g.kind == GearKind.StonePickaxe);
                sim.Apply(Command(p, "equip", arg: pickaxe.id));
                p.x = 13;
                int unitGrams = InventoryWeight.Grams(entry.item);
                FillBackpackLeaving(p, 5 * unitGrams);
                var full = Copy(p.items);
                for (int swing = 0; swing < 3; swing++)
                {
                    Advance(sim, .55f); sim.Apply(Command(p, "interact", 14, 10));
                }
                Require(pickaxe.durability == 98 && sim.State.nodes.Single().stock == 6 && sim.State.nodes.Single().hp == 6 &&
                    sim.State.gathered == 0, "insufficient capacity consumed the last swing or stock");
                Inventory(p.items, full, "insufficient capacity changed held items");
                Advance(sim, .55f); Array.Clear(p.items);
                FillBackpackLeaving(p, 6 * unitGrams);
                var before = Copy(p.items);
                sim.Apply(Command(p, "interact", 14, 10));
                var expected = Copy(before); expected[(int)entry.item] = 6;
                Inventory(p.items, expected, "primitive node yielded the wrong resource");
                Require(sim.State.nodes.Count == 0 && sim.State.gathered == 6 && sim.State.produced.Sum() == 0, "harvesting fabricated a production milestone");
            }
        });
        Run("primitive legacy bypass preserves recipe eras and permits existing primitive production", () =>
        {
            var sim = PrimitiveFixture(out var p, arrange: s =>
            {
                s.primitiveVersion = 0; var b = Add(s, BuildKind.StoneFurnace, 14, 10);
                b.items[(int)ItemKind.Ore] = 1; b.items[(int)ItemKind.Fuel] = 1;
            });
            var furnace = sim.State.buildings.Single(b => b.kind == BuildKind.StoneFurnace);
            p.items = DemoCatalog.Materials(ItemKind.Copper, 3, ItemKind.Tin, 1, ItemKind.Wood, 12, ItemKind.Iron, 6);
            var before = Copy(p.items); sim.Apply(Command(p, "primitive", arg: 0));
            Inventory(p.items, before, "legacy bypass accepted unnecessary research payment");
            Require(sim.State.primitiveResearch == 0 && PrimitiveCatalog.WaterRequirement(sim.State).Length == 0, "legacy acquired primitive locks");
            sim.Apply(Command(p, "craft", arg: 2));
            Require(p.items[(int)ItemKind.Bronze] == before[(int)ItemKind.Bronze] + 4, "legacy primitive bronze was locked");
            Advance(sim, 3.1f); Require(furnace.items[(int)ItemKind.Iron] == 1, "legacy default furnace stopped producing");
            p.x = 13; Ready(p); sim.Apply(Command(p, "recipe", 14, 10, 13));
            Require(furnace.recipe == 0 && !PrimitiveCatalog.IsRecipeUnlocked(sim.State, TechCatalog.Recipe(13)), "legacy bypass skipped water recipe era");
            Ready(p); p.x = 8; before = Copy(p.items); sim.Apply(Command(p, "craft", arg: 56));
            Inventory(p.items, before, "legacy bypass crafted future-era ammunition");
            Add(sim.State, BuildKind.ResearchBench, 9, 12);
            Advance(sim, .55f);
            Ready(p); sim.Apply(Command(p, "research", arg: (int)TechEra.Water));
            Require(sim.State.era == TechEra.Water, "legacy water research acquired production milestones");
        });
    }
}
