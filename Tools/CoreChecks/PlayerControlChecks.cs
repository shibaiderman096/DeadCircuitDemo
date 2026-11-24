using DeadCircuit;

static partial class Checks
{
    static WorldSimulator PlayerControlFixture(out PlayerState p, bool survival = false)
    {
        var state = Flat();
        state.survival = new SurvivalWorldState { enabled = survival, initialized = survival };
        var sim = new WorldSimulator(state);
        p = sim.AddPlayer("control", "Control"); p.x = 10; p.y = 10; p.activeSlot = 2;
        return sim;
    }

    static void PlayerControlChecks()
    {
        Run("primitive gathering starts from loose materials and pays once across a shared saved node", () =>
        {
            var world = Flat(era: TechEra.Primitive); world.primitiveVersion = 1;
            var sim = new WorldSimulator(world);
            var p = sim.AddPlayer("gatherer", "Gatherer");
            var partner = sim.AddPlayer("partner", "Partner");
            p.activeSlot = partner.activeSlot = 2;
            p.x = 10; p.y = 10; partner.x = 10; partner.y = 11;
            Require(p.gear.Count == 0 && sim.State.buildings.Count == 0, "fixture skipped the handcraft start");
            var tree = new NodeState { id = sim.State.nextId++, kind = NodeKind.Tree, x = 11, y = 10, stock = 13, hp = 35 };
            sim.State.nodes.Add(tree);
            foreach (var (kind, item, x, stock, action) in new[]
            {
                (NodeKind.Branch, ItemKind.Branch, 10, 2, "interact"),
                (NodeKind.LooseStone, ItemKind.LooseStone, 9, 5, "primary"),
                (NodeKind.DryGrass, ItemKind.DryGrass, 8, 2, "work")
            })
            {
                var pile = new NodeState { id = sim.State.nextId++, kind = kind, x = x, y = 10, stock = stock, hp = 1 };
                sim.State.nodes.Add(pile);
                Require(!sim.IsBlocked(x, 10), "loose material blocked walking");
                sim.Apply(Command(p, action, x, 10)); Advance(sim, .6f);
                Require(!sim.State.nodes.Contains(pile) && p.items[(int)item] == stock, "bare hands could not pick up " + kind);
            }
            var savedMaterials = new WorldSimulator(Saved(sim.State));
            var restoredPlayer = savedMaterials.State.players.Single(a => a.id == p.id);
            Require(restoredPlayer.items[(int)ItemKind.LooseStone] == 5 && restoredPlayer.items[(int)ItemKind.DryGrass] == 2,
                "loose materials did not survive save and restore");
            foreach (var kind in new[] { GearKind.StoneAxe, GearKind.StonePickaxe })
            {
                sim.Apply(Command(p, "gearcraft", arg: (int)kind)); Advance(sim, .6f);
            }
            Require(p.gear.Count == 2 && sim.State.primitiveResearch == 0 && p.items.Skip(63).All(n => n == 0),
                "handcraft required research or did not spend the picked materials");
            int wood = p.items[(int)ItemKind.Wood];
            var axe = p.gear.Single(g => g.kind == GearKind.StoneAxe);
            sim.Apply(Command(p, "equip", arg: axe.id));
            sim.Apply(Command(p, "interact", 11, 10));
            Require(tree.hp == 23 && tree.stock == 13 && p.items[(int)ItemKind.Wood] == wood,
                "first hit paid early or failed to damage the tree");
            sim = new WorldSimulator(Saved(sim.State));
            p = sim.State.players.Single(a => a.id == p.id);
            partner = sim.State.players.Single(a => a.id == partner.id);
            tree = sim.State.nodes.Single();
            int partnerWood = partner.items[(int)ItemKind.Wood];
            Require(tree.hp == 23 && tree.stock == 13, "damaged node was reset by a save");
            Advance(sim, .6f);
            sim.Apply(Command(partner, "interact", 11, 10));
            Require(tree.hp == 23, "a second player harvested with bare hands");
            sim.Apply(Command(p, "primary", 11, 10)); Advance(sim, 1.2f);
            Require(sim.State.nodes.Count == 0 && p.items[(int)ItemKind.Wood] == wood + 13 &&
                partner.items[(int)ItemKind.Wood] == partnerWood, "last hit did not award the entire node to the correct player");
            sim.Apply(Command(partner, "primary", 11, 10)); Advance(sim, .6f);
            Require(partner.items[(int)ItemKind.Wood] == partnerWood, "destroyed node paid twice");
        });
        Run("player control click walk persists through zero movement heartbeat and stops at destination", () =>
        {
            var sim = PlayerControlFixture(out var p);
            sim.Apply(Command(p, "walk", 15, 10));
            for (int tick = 0; tick < 60; tick++)
            {
                sim.Apply(Command(p, "move")); sim.Step(WorldSimulator.FixedStep);
            }
            Near(p.x, 15, "click walk did not reach destination", .08f);
            Near(p.y, 10, "click walk changed row", .08f);
            float x = p.x; Advance(sim, 1); Near(p.x, x, "walk overshot destination");
        });
        Run("player control primary approaches a resource and stops permanently on depletion", () =>
        {
            var sim = PlayerControlFixture(out var p);
            sim.Apply(Command(p, "equip", arg: p.gear.Single(g => g.kind == GearKind.StonePickaxe).id));
            var node = Ore(sim.State, 16, 10, 7);
            int initial = p.items[(int)ItemKind.Ore];
            sim.Apply(Command(p, "primary", 16, 10)); Advance(sim, 4);
            Require(!sim.State.nodes.Contains(node) && p.items[(int)ItemKind.Ore] == initial + 7, "resource not gathered completely");
            float x = p.x, y = p.y; Advance(sim, 1);
            Near(p.x, x, "depletion did not stop walking"); Near(p.y, y, "depletion changed row");
            Require(p.attackCooldown == 0 && p.gear.All(g => g.durability > 0), "resource intent became combat");
        });
        Run("player control held work searches locally without mouse and expires without refresh", () =>
        {
            var sim = PlayerControlFixture(out var p);
            sim.Apply(Command(p, "equip", arg: p.gear.Single(g => g.kind == GearKind.StonePickaxe).id));
            Ore(sim.State, 15, 10, 10); int initial = p.items[(int)ItemKind.Ore];
            for (int tick = 0; tick < 70; tick++)
            {
                sim.Apply(Command(p, "work", 999999, -999999)); sim.Step(WorldSimulator.FixedStep);
            }
            Require(sim.State.nodes.Count == 0 && p.items[(int)ItemKind.Ore] == initial + 10, "work used the mouse or failed to gather locally");
            Ore(sim.State, 20, 10, 50); sim.Apply(Command(p, "work")); Advance(sim, .5f);
            float x = p.x, y = p.y; int gathered = p.items[(int)ItemKind.Ore]; Advance(sim, 1);
            Near(p.x, x, "stale work kept moving"); Near(p.y, y, "stale work changed row");
            Require(p.items[(int)ItemKind.Ore] == gathered, "stale work kept gathering");
        });
        Run("player control secondary opens doors repairs devices and cancels on empty ground", () =>
        {
            var sim = PlayerControlFixture(out var p);
            var door = Add(sim.State, BuildKind.Door, 16, 10);
            sim.Apply(Command(p, "secondary", 16, 10)); Advance(sim, 3);
            Require(door.setting == 1, "secondary did not open door");
            Advance(sim, 1); Require(door.setting == 1, "secondary toggled door more than once");
            var machine = Add(sim.State, BuildKind.Assembler, 17, 10); machine.hp -= 60;
            int scrap = p.items[(int)ItemKind.Scrap]; float hp = machine.hp;
            sim.Apply(Command(p, "secondary", 19, 11)); Advance(sim, 3);
            Require(machine.hp > hp && p.items[(int)ItemKind.Scrap] == scrap - 1, "secondary did not repair clicked footprint once");
            sim.Apply(Command(p, "walk", 25, 10)); Advance(sim, .1f);
            sim.Apply(Command(p, "secondary", 24, 20)); float x = p.x, y = p.y; Advance(sim, 1);
            Near(p.x, x, "empty secondary did not stop"); Near(p.y, y, "empty secondary changed row");
        });
        Run("player control held work collects mature crops and carcasses but never operates doors or machines", () =>
        {
            var sim = PlayerControlFixture(out var p, true);
            var crop = new FarmPlotState { id = sim.State.nextId++, x = 15, y = 10, crop = (int)CropKind.Carrot, growth = 1 };
            sim.State.survival.plots.Add(crop);
            sim.State.survival.creatures.Add(new CreatureState { id = sim.State.nextId++, kind = CreatureKind.Hare, x = 16, y = 10, hp = 0, meatLeft = 1 });
            var door = Add(sim.State, BuildKind.Door, 13, 11);
            var chest = Add(sim.State, BuildKind.Chest, 14, 11); chest.items[(int)ItemKind.Wood] = 10;
            for (int tick = 0; tick < 70; tick++) { sim.Apply(Command(p, "work")); sim.Step(WorldSimulator.FixedStep); }
            Require(crop.crop == -1 && p.needs.foods[(int)FoodKind.Carrot] > 0, "work did not harvest mature crop");
            Require(sim.State.survival.creatures.Count == 0 && p.needs.foods[(int)FoodKind.RawMeat] == 1, "work did not collect carcass");
            Require(door.setting == 0 && chest.items[(int)ItemKind.Wood] == 10, "work operated a door or machine");
        });
        Run("player control held fight pursues enemies and never swings without a target", () =>
        {
            var sim = PlayerControlFixture(out var p);
            var zombie = new ZombieState { id = sim.State.nextId++, x = 17, y = 10, hp = 100 }; sim.State.zombies.Add(zombie);
            for (int tick = 0; tick < 65; tick++) { sim.Apply(Command(p, "fight")); sim.Step(WorldSimulator.FixedStep); }
            Require(zombie.hp < 100 && p.x > 10, "fight did not chase and attack enemy");
            sim.State.zombies.Clear(); Advance(sim, 1);
            for (int tick = 0; tick < 20; tick++) { sim.Apply(Command(p, "fight")); sim.Step(WorldSimulator.FixedStep); }
            Require(p.attackCooldown == 0, "no-target fight swung an empty attack");
        });
        Run("player control ordinary fight protects a closer neutral animal in the same melee sector", () =>
        {
            var sim = PlayerControlFixture(out var p, true); p.skills[1] = 3;
            var sheep = new CreatureState { id = sim.State.nextId++, kind = CreatureKind.Sheep, x = 10.4f, y = 10, hp = 48, decisionTicks = 200 };
            sim.State.survival.creatures.Add(sheep);
            var zombie = new ZombieState { id = sim.State.nextId++, x = 11.1f, y = 10, hp = 100 }; sim.State.zombies.Add(zombie);
            sim.Apply(Command(p, "fight")); sim.Step(WorldSimulator.FixedStep);
            Require(zombie.hp < 100, "ordinary fight failed to hit hostile target");
            Near(sheep.hp, 48, "automatic melee hit the closer neutral animal, including cleave");
            sim.Apply(Command(p, "cancel")); p.attackCooldown = 0;
            sim.Apply(new PlayerCommand { playerId = p.id, action = "attack", aimX = 1 });
            Require(sheep.hp < 48, "automatic filtering leaked into manual attack");
        });
        Run("player control releasing control modifier drops a neutral fight lock immediately", () =>
        {
            var sim = PlayerControlFixture(out var p, true);
            var sheep = new CreatureState { id = sim.State.nextId++, kind = CreatureKind.Sheep, x = 11, y = 10, hp = 48, decisionTicks = 200 };
            sim.State.survival.creatures.Add(sheep);
            sim.Apply(Command(p, "fight", arg: 1)); sim.Step(WorldSimulator.FixedStep);
            Require(sheep.hp < 48, "control fight did not allow a neutral animal");
            float hp = sheep.hp, x = p.x, y = p.y; p.attackCooldown = 0;
            for (int tick = 0; tick < 15; tick++) { sim.Apply(Command(p, "fight", arg: 0)); sim.Step(WorldSimulator.FixedStep); }
            Near(sheep.hp, hp, "ordinary fight retained the neutral target after modifier release");
            Near(p.x, x, "modifier release kept pursuing neutral target"); Near(p.y, y, "modifier release kept moving");
            Require(p.attackCooldown == 0, "ordinary fight still swung at a neutral animal");
        });
        Run("player control click walk faces the actual movement direction", () =>
        {
            var sim = PlayerControlFixture(out var p); p.aimX = 1; p.aimY = 0;
            sim.Apply(Command(p, "walk", 10, 15)); Advance(sim, .15f);
            Near(p.aimX, 0, "walk retained old facing"); Near(p.aimY, 1, "walk did not face north");
        });
        Run("player control carcass approach uses its actual fractional position", () =>
        {
            var sim = PlayerControlFixture(out var p, true);
            sim.State.survival.creatures.Add(new CreatureState { id = sim.State.nextId++, kind = CreatureKind.Hare, x = 12.49f, y = 10, hp = 0, meatLeft = 1 });
            for (int tick = 0; tick < 20; tick++) { sim.Apply(Command(p, "work")); sim.Step(WorldSimulator.FixedStep); }
            Require(sim.State.survival.creatures.Count == 0 && p.needs.foods[(int)FoodKind.RawMeat] == 1, "rounded goal stopped outside actual butchery range");
        });
    }
}
