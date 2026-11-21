using DeadCircuit;

static partial class Checks
{
    static int NuclearBit(NuclearTech id) => 1 << (int)id;

    static void EarnNuclearEvidence(WorldState state)
    {
        state.produced[(int)ItemKind.NuclearFuel] = 2;
        state.produced[(int)ItemKind.NuclearWaste] = 8;
        state.produced[(int)ItemKind.HotCoolant] = 12;
        state.produced[(int)ItemKind.Coolant] = 4;
    }

    static void NuclearProgressionChecks()
    {
        Run("nuclear technology validates all eight studies and saves their full progression", () =>
        {
            var state = Flat(era: TechEra.Nuclear);
            EarnNuclearEvidence(state);
            var sim = new WorldSimulator(state);
            var player = Builder(sim);
            player.x = 8; player.y = 10;
            Add(sim.State, BuildKind.Workbench, 10, 10);
            foreach (var definition in NuclearCatalog.All)
            {
                Ready(player);
                string requirement = NuclearCatalog.ResearchRequirement(sim.State, player, (int)definition.id);
                Require(requirement.Length == 0, definition.name + " should be researchable: " + requirement);
                int[] before = Copy(player.items);
                sim.Apply(Command(player, "nuclear", arg: (int)definition.id));
                Require(NuclearCatalog.HasResearch(sim.State, definition.id), definition.name + " did not unlock for the team");
                Paid(player.items, before, definition.cost, definition.name + " research");
                Advance(sim, .55f);
            }
            Require(sim.State.nuclearResearch == NuclearCatalog.AllResearch, "one or more nuclear studies were skipped");
            var restored = new WorldSimulator(Saved(sim.State));
            Require(restored.State.version == WorldSimulator.SaveVersion && restored.State.nuclearResearch == NuclearCatalog.AllResearch,
                "full nuclear research did not survive current-format restore");
        });

        Run("nuclear saves reject unknown bits missing parents missing production evidence and early research", () =>
        {
            var badMask = Flat(era: TechEra.Nuclear);
            badMask.nuclearResearch = 1 << 8;
            Reject(badMask, "unknown nuclear research bit was accepted");

            var badParent = Flat(era: TechEra.Nuclear);
            badParent.nuclearResearch = NuclearBit(NuclearTech.FuelAssembly);
            badParent.produced[(int)ItemKind.NuclearFuel] = 2;
            badParent.produced[(int)ItemKind.NuclearWaste] = 2;
            Reject(badParent, "fuel assembly without uranium enrichment was accepted");

            var badEvidence = Flat(era: TechEra.Nuclear);
            badEvidence.nuclearResearch = NuclearBit(NuclearTech.UraniumEnrichment);
            Reject(badEvidence, "uranium enrichment without real fuel evidence was accepted");

            var early = Flat(era: TechEra.HighVoltage);
            early.nuclearResearch = NuclearBit(NuclearTech.UraniumEnrichment);
            early.produced[(int)ItemKind.NuclearFuel] = 2;
            Reject(early, "nuclear research before the nuclear era was accepted");

            var valid = Flat(era: TechEra.Nuclear);
            valid.nuclearResearch = NuclearBit(NuclearTech.UraniumEnrichment);
            valid.produced[(int)ItemKind.NuclearFuel] = 2;
            Require(new WorldSimulator(Saved(valid)).State.nuclearResearch == valid.nuclearResearch,
                "valid nuclear research evidence did not restore");
        });

        Run("uranium enrichment gates the high-yield fuel recipe and the technology tree command", () =>
        {
            var state = Flat(era: TechEra.Nuclear);
            state.produced[(int)ItemKind.NuclearFuel] = 2;
            var sim = new WorldSimulator(state);
            var player = Builder(sim);
            Add(sim.State, BuildKind.Workbench, 10, 10);
            var processor = Add(sim.State, BuildKind.NuclearProcessor, 10, 14);
            processor.recipe = 90;
            var recipe = TechCatalog.Recipe(92);
            var node = TechnologyTreeModel.Find(601);
            Require(recipe != null && node != null && node.command == "nuclear" && node.researchId == (int)NuclearTech.UraniumEnrichment,
                "nuclear recipe or tree node is missing");

            player.x = 8; player.y = 14;
            sim.Apply(Command(player, "recipe", 10, 14, recipe.id));
            Require(processor.recipe == 90 && !PrimitiveCatalog.IsRecipeUnlocked(sim.State, recipe),
                "high-yield fuel recipe bypassed uranium enrichment");

            Advance(sim, .55f);
            player.x = 8; player.y = 10;
            Require(TechnologyTreeModel.CanResearch(sim.State, player, node), "tree model hid available uranium enrichment");
            int[] before = Copy(player.items);
            sim.Apply(Command(player, node.command, arg: node.researchId));
            Require(TechnologyTreeModel.Known(sim.State, node) && PrimitiveCatalog.IsRecipeUnlocked(sim.State, recipe),
                "tree research did not unlock the high-yield fuel recipe");
            Paid(player.items, before, node.cost, "uranium enrichment tree command");
            Advance(sim, .55f);
            player.x = 8; player.y = 14;
            sim.Apply(Command(player, "recipe", 10, 14, recipe.id));
            Require(processor.recipe == recipe.id, "researched high-yield fuel recipe could not be selected");

            var saved = Saved(sim.State);
            Require(Building(new WorldSimulator(saved), processor.id).recipe == recipe.id,
                "researched fuel recipe did not survive restore");
            saved.nuclearResearch = 0;
            Reject(saved, "unresearched high-yield fuel recipe survived restore");
        });

        Run("nuclear studies change reactor endurance cooling output and thermal protection", () =>
        {
            var state = Flat(era: TechEra.Nuclear);
            EarnNuclearEvidence(state);
            state.nuclearResearch = NuclearCatalog.AllResearch;
            Require(NuclearCatalog.FuelSeconds(state) == 30f && NuclearCatalog.CoolantSeconds(state) == 4f &&
                NuclearCatalog.TripTemperature(state) == 1100f && NuclearCatalog.PowerOutput(state) == 1500f,
                "nuclear technology modifiers were not applied");

            var reactor = Add(state, BuildKind.Reactor, 13, 10);
            AddNuclearFacility(state, reactor);
            reactor.items[(int)ItemKind.NuclearFuel] = 3;
            reactor.items[(int)ItemKind.Coolant] = 10;
            var sim = new WorldSimulator(state);
            Advance(sim, 2.1f);
            reactor = Building(sim, reactor.id);
            Near(reactor.powerGeneration, 1500f, "researched reactor output");
            Require(reactor.fuel > 27f && reactor.progress > 1.8f && reactor.progress < 2f,
                "researched fuel or coolant duration did not reach the reactor");

            var restored = new WorldSimulator(Saved(sim.State));
            var restoredReactor = Building(restored, reactor.id);
            Near(restoredReactor.fuel, reactor.fuel, "researched fuel reserve after restore");
            Near(restoredReactor.progress, reactor.progress, "researched coolant reserve after restore");
            restored.Step(WorldSimulator.FixedStep);
            Near(restoredReactor.powerGeneration, 1500f, "researched reactor output after restore");

            reactor.temperature = 1099f;
            reactor.fuel = 1f;
            reactor.progress = 1f;
            reactor.tripped = false;
            sim.Step(WorldSimulator.FixedStep);
            Require(reactor.tripped && reactor.temperature >= 1100f && reactor.powerGeneration == 0f,
                "researched thermal limit did not trip at 1100 degrees");
        });

        Run("shielded vault capacity and researched vault-to-vault waste transfer survive restore", () =>
        {
            var state = Flat(era: TechEra.Nuclear);
            state.produced[(int)ItemKind.NuclearFuel] = 2;
            state.produced[(int)ItemKind.NuclearWaste] = 4;
            state.nuclearResearch = NuclearBit(NuclearTech.UraniumEnrichment) |
                NuclearBit(NuclearTech.CoreDiagnostics) | NuclearBit(NuclearTech.ShieldedStorage);
            var fullVault = Add(state, BuildKind.NuclearWasteVault, 24, 24);
            fullVault.items[(int)ItemKind.NuclearWaste] = WorldSimulator.BufferLimit * 2;
            var accepted = new WorldSimulator(Saved(state));
            Require(Building(accepted, fullVault.id).items[(int)ItemKind.NuclearWaste] == 400,
                "shielded storage capacity did not restore");

            var overCapacity = Flat(era: TechEra.Nuclear);
            var ordinaryVault = Add(overCapacity, BuildKind.NuclearWasteVault, 24, 24);
            ordinaryVault.items[(int)ItemKind.NuclearWaste] = WorldSimulator.BufferLimit + 1;
            Reject(overCapacity, "unresearched vault exceeded its storage limit");

            var transfer = Flat(era: TechEra.Nuclear);
            EarnNuclearEvidence(transfer);
            transfer.nuclearResearch = NuclearCatalog.AllResearch;
            var source = Add(transfer, BuildKind.NuclearWasteVault, 10, 10);
            var destination = Add(transfer, BuildKind.NuclearWasteVault, 13, 10);
            source.items[(int)ItemKind.NuclearWaste] = 1;
            var sim = new WorldSimulator(transfer);
            sim.Step(WorldSimulator.FixedStep);
            Require(sim.State.cargo.Any(c => c.fromId == source.id && c.toId == destination.id && c.item == (int)ItemKind.NuclearWaste),
                "researched dedicated vault transfer did not start");
            var restored = new WorldSimulator(Saved(sim.State));
            Advance(restored, 1f);
            Require(Building(restored, destination.id).items[(int)ItemKind.NuclearWaste] == 1,
                "restored vault transfer did not deliver nuclear waste");

            var locked = Flat(era: TechEra.Nuclear);
            var lockedSource = Add(locked, BuildKind.NuclearWasteVault, 10, 10);
            var lockedDestination = Add(locked, BuildKind.NuclearWasteVault, 13, 10);
            lockedSource.items[(int)ItemKind.NuclearWaste] = 1;
            var lockedSim = new WorldSimulator(locked);
            lockedSim.Step(WorldSimulator.FixedStep);
            Require(lockedSim.State.cargo.Count == 0 && Building(lockedSim, lockedSource.id).items[(int)ItemKind.NuclearWaste] == 1,
                "unresearched dedicated vault transfer moved waste");
        });
    }
}
