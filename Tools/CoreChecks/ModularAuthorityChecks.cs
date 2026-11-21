using DeadCircuit;

static partial class Checks
{
    static void ModularAuthorityChecks()
    {
        Run("modular current restore rejects duplicate physical cells even with otherwise valid module costs", () =>
        {
            var sim = ModularFixture(out var player);
            var machine = sim.State.buildings.Single();
            CompleteModular(sim, player, machine);
            var forged = Saved(sim.State);
            var modules = forged.buildings[0].modularModules;
            modules[1].x = modules[0].x; modules[1].y = modules[0].y;
            ModularMachineCatalog.RefreshBounds(forged.buildings[0]);
            Reject(forged, "duplicate free-layout module coordinates were accepted");
        });
        Run("modular current restore rejects forged physical cargo endpoints from a legitimate port flight", () =>
        {
            var sim = ModularFixture(out var player, lShape: true);
            var machine = sim.State.buildings.Single();
            CompleteModular(sim, player, machine);
            int input = ModularMachineCatalog.RoleIndex(machine, MachineModuleRole.Input);
            int direction = ModularMachineCatalog.ModuleRotation(machine, input);
            int x = ModularMachineCatalog.ModuleX(machine, input), y = ModularMachineCatalog.ModuleY(machine, input);
            var source = Add(sim.State, BuildKind.Chest, x - DemoCatalog.DirectionX(direction), y - DemoCatalog.DirectionY(direction), direction);
            source.items[(int)ItemKind.Ore] = 1;
            Advance(sim, .05f);
            Require(sim.State.cargo.Count == 1, "legitimate input flight was not dispatched");
            var baseline = Saved(sim.State); _ = new WorldSimulator(baseline);
            foreach (var corrupt in new Action<CargoState>[] { c => c.fromX += 1, c => c.fromY += 1, c => c.toX += 1, c => c.toY += 1 })
            {
                var forged = Saved(baseline); corrupt(forged.cargo[0]);
                Reject(forged, "forged modular flight endpoint was accepted");
            }
        });
    }
}
