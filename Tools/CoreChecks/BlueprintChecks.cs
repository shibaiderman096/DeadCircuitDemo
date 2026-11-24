using DeadCircuit;

static partial class Checks
{
    // Public seams only: build places a plan, interact delivers materials, dismantle cancels.
    static void BlueprintChecks()
    {
        Run("blueprint placement is free walkable and completed by hand delivery", () =>
        {
            var sim = new WorldSimulator(Flat()); var p = Builder(sim);
            p.items = DemoCatalog.BuildCost(BuildKind.Workbench); p.x = 8; p.y = 10;
            int before = p.items.Sum();
            sim.Apply(Command(p, "build", 10, 10, (int)BuildKind.Workbench));
            Require(sim.State.buildings.Count == 0 && sim.State.blueprints.Count == 1, "workbench skipped its blueprint");
            Require(p.items.Sum() == before, "blueprint placement charged materials");
            Require(!sim.IsBlocked(10, 10), "blueprint blocked walking");
            var plan = sim.State.blueprints[0];
            Require(plan.kind == BuildKind.Workbench && plan.delivered.All(n => n == 0), "blueprint identity or delivery");
            p.interactCooldown = 0f; sim.Apply(Command(p, "interact", 10, 10));
            Require(sim.State.blueprints.Count == 0 && sim.State.buildings.Single().kind == BuildKind.Workbench, "delivery did not build the workbench");
            Require(p.items.Sum() == before - DemoCatalog.BuildCost(BuildKind.Workbench).Sum(), "delivery charged the wrong amount");
            Require(sim.State.buildings[0].hp == WorldSimulator.BuildingMaxHealth(BuildKind.Workbench), "workbench built at partial health");
            Require(sim.IsBlocked(10, 10), "built workbench did not block");
        });

        Run("partial blueprint delivery persists until the whole cost arrives", () =>
        {
            var sim = new WorldSimulator(Flat()); var p = Builder(sim);
            Array.Clear(p.items); p.items[(int)ItemKind.Wood] = 1; p.x = 8; p.y = 10;
            sim.Apply(Command(p, "build", 10, 10, (int)BuildKind.Wall));
            p.interactCooldown = 0f; sim.Apply(Command(p, "interact", 10, 10));
            var plan = sim.State.blueprints.Single();
            Require(plan.delivered[(int)ItemKind.Wood] == 1 && p.items[(int)ItemKind.Wood] == 0 && sim.State.buildings.Count == 0,
                "partial delivery lost the wall materials or built early");
            p.items[(int)ItemKind.Wood] = 2; p.interactCooldown = 0f; sim.Apply(Command(p, "interact", 10, 10));
            Require(sim.State.blueprints.Count == 0 && sim.State.buildings.Single().kind == BuildKind.Wall, "funded wall did not complete");
            Require(p.items[(int)ItemKind.Wood] == 0, "completion kept unspent wall wood");
        });

        Run("cancelling a blueprint refunds delivered materials exactly once", () =>
        {
            var sim = new WorldSimulator(Flat()); var p = Builder(sim);
            Array.Clear(p.items); p.items[(int)ItemKind.Wood] = 2; p.x = 8; p.y = 10;
            sim.Apply(Command(p, "build", 10, 10, (int)BuildKind.Wall));
            p.interactCooldown = 0f; sim.Apply(Command(p, "interact", 10, 10));
            Require(sim.State.blueprints.Single().delivered[(int)ItemKind.Wood] == 2 && p.items[(int)ItemKind.Wood] == 0, "partial wall delivery");
            p.interactCooldown = 0f; sim.Apply(Command(p, "dismantle", 10, 10));
            Require(sim.State.blueprints.Count == 0 && sim.State.buildings.Count == 0 && p.items[(int)ItemKind.Wood] == 2, "cancel did not refund exactly the delivered wood");
        });

        Run("blueprints reject overlapping plans buildings resources and water", () =>
        {
            var sim = new WorldSimulator(Flat()); var p = Builder(sim);
            p.items = DemoCatalog.BuildCost(BuildKind.Assembler); p.x = 8; p.y = 10;
            sim.Apply(Command(p, "build", 10, 10, (int)BuildKind.Assembler));
            Require(sim.State.blueprints.Count == 1, "assembler plan missing");
            p.interactCooldown = 0f; sim.Apply(Command(p, "build", 11, 10, (int)BuildKind.Wall));
            Require(sim.State.blueprints.Count == 1 && p.notice.Contains("蓝图"), "overlapping plan on a blueprint was accepted");
            p.interactCooldown = 0f; sim.Apply(Command(p, "interact", 10, 10));
            Require(sim.State.buildings.Count == 1, "assembler failed to complete after the overlap rejection");
            p.interactCooldown = 0f; sim.Apply(Command(p, "build", 10, 10, (int)BuildKind.Wall));
            Require(sim.State.blueprints.Count == 0, "plan placed on a finished building");
            var state = Flat(); var ground = state.ground.ToCharArray(); ground[10 + 10 * 64] = '2'; state.ground = new string(ground);
            var wet = new WorldSimulator(state); var q = Builder(wet); q.x = 8; q.y = 10;
            wet.Apply(Command(q, "build", 10, 10, (int)BuildKind.Wall));
            Require(wet.State.blueprints.Count == 0, "plan placed on water");
        });

        Run("blueprints survive save restore with their partial delivery", () =>
        {
            var sim = new WorldSimulator(Flat()); var p = Builder(sim);
            Array.Clear(p.items); p.items[(int)ItemKind.Wood] = 1; p.x = 8; p.y = 10;
            sim.Apply(Command(p, "build", 10, 10, (int)BuildKind.Wall));
            p.interactCooldown = 0f; sim.Apply(Command(p, "interact", 10, 10));
            var restored = new WorldSimulator(Saved(sim.State));
            var plan = restored.State.blueprints.Single();
            Require(plan.kind == BuildKind.Wall && plan.delivered[(int)ItemKind.Wood] == 1, "save lost the delivered plan materials");
            var actor = restored.State.players.Single();
            actor.items[(int)ItemKind.Wood] = 2; actor.interactCooldown = 0f;
            restored.Apply(Command(actor, "interact", 10, 10));
            Require(restored.State.blueprints.Count == 0 && restored.State.buildings.Single().kind == BuildKind.Wall, "restored plan could not finish");
        });

        Run("save validation rejects blueprints overpaid or overlapping buildings", () =>
        {
            var sim = new WorldSimulator(Flat()); var p = Builder(sim); p.x = 8; p.y = 10;
            sim.Apply(Command(p, "build", 10, 10, (int)BuildKind.Wall));
            var overpaid = Saved(sim.State);
            overpaid.blueprints.Single().delivered[(int)ItemKind.Wood] = 99;
            Reject(overpaid, "overpaid blueprint was accepted");
            var overlap = Saved(sim.State);
            overlap.buildings.Add(new BuildingState { id = overlap.nextId++, kind = BuildKind.Wall, x = 10, y = 10, footprintVersion = 1, hp = 120f });
            Reject(overlap, "blueprint overlapping a building was accepted");
        });
    }
}
