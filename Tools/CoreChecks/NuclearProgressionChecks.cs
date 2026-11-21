using System;
using System.Text.Json;
using DeadCircuit;

static partial class Checks
{
    static int NuclearBit(NuclearTech id) => 1 << (int)id;

    static void EarnNuclearEvidence(WorldState state)
    {
        state.nuclearUraniumMined = 4;
        state.gathered = Math.Max(state.gathered, 4);
        state.produced[(int)ItemKind.NuclearFuel] = 2;
        state.produced[(int)ItemKind.NuclearWaste] = 8;
        state.produced[(int)ItemKind.HotCoolant] = 12;
        state.produced[(int)ItemKind.Coolant] = 4;
    }

    static void NuclearProgressionChecks()
    {
        Run("uranium mining records finite ore from both powered miner families without claiming production", () =>
        {
            foreach(var kind in new[] {BuildKind.Miner,BuildKind.SteamMiner})
            {
                var state=Flat(era:TechEra.Nuclear);
                var miner=Add(state,kind,10,10);
                AddRecipePower(state,miner);
                var ore=new NodeState {id=state.nextId++,kind=NodeKind.UraniumOre,x=12,y=11,stock=4};
                state.nodes.Add(ore);
                var sim=new WorldSimulator(state);
                Advance(sim,13f);
                Require(sim.State.nuclearUraniumMined==4 && sim.State.gathered==4 && sim.State.nodes.All(n=>n.id!=ore.id) &&
                    ItemTotals(sim.State)[(int)ItemKind.UraniumOre]==4 && sim.State.produced[(int)ItemKind.UraniumOre]==0,
                    kind+" lost finite ore, duplicated mining evidence or claimed fabricated manufacturing");
                var restored=new WorldSimulator(Saved(sim.State));
                Advance(restored,4f);
                Require(restored.State.nuclearUraniumMined==4 && restored.State.gathered==4 && restored.State.nodes.All(n=>n.id!=ore.id),
                    kind+" replenished ore or recounted mining on restore");
            }
        });

        Run("uranium current saves reject false mining evidence locked leaching and malformed concentrate slots", () =>
        {
            foreach(int mined in new[] {-1,5})
            {
                var state=Flat(era:TechEra.Nuclear);
                state.gathered=4; state.nuclearUraniumMined=mined;
                Reject(state,"invalid uranium mining evidence was accepted");
            }
            var insufficient=Flat(era:TechEra.Nuclear);
            insufficient.gathered=4; insufficient.nuclearUraniumMined=3;
            insufficient.nuclearResearch=NuclearBit(NuclearTech.UraniumBeneficiation);
            Reject(insufficient,"beneficiation without four actually mined uranium was accepted");
            var locked=Flat(era:TechEra.Nuclear);
            Add(locked,BuildKind.ChemicalPlant,10,10).recipe=93;
            Reject(locked,"locked beneficiation recipe was restored");
            var truncated=Flat(era:TechEra.Nuclear);
            truncated.produced=new int[62];
            Reject(truncated,"current-format production history omitted the new material slot");
            var duplicate=Flat(era:TechEra.Nuclear);
            Add(duplicate,BuildKind.NuclearWasteVault,10,10).items[(int)ItemKind.UraniumConcentrate]=1;
            Reject(duplicate,"uranium concentrate was accepted in a dedicated waste vault");
        });

        Run("uranium beneficiation progresses from real finite mining through research and two powered production stages", () =>
        {
            var state = Flat(era: TechEra.Nuclear);
            Add(state, BuildKind.Workbench, 10, 10);
            var ore = new NodeState { id=state.nextId++, kind=NodeKind.UraniumOre, x=10, y=14, stock=6, hp=35 };
            state.nodes.Add(ore);
            var plant = Add(state, BuildKind.ChemicalPlant, 10, 20);
            Add(state, BuildKind.DieselGenerator, 7, 20).items[(int)ItemKind.Diesel] = 20;
            foreach (int x in new[] {13,14,15}) Add(state, BuildKind.Belt, x, 20);
            var processor = Add(state, BuildKind.NuclearProcessor, 16, 20);
            processor.items[(int)ItemKind.Steel] = 1;
            Add(state, BuildKind.HighGenerator, 20, 20).items[(int)ItemKind.Fuel] = 20;
            var output = Add(state, BuildKind.Chest, 19, 20);
            var sim = new WorldSimulator(state);
            var player = Builder(sim);
            player.items[(int)ItemKind.UraniumConcentrate] = player.items[(int)ItemKind.NuclearFuel] = 0;
            player.x=8; player.y=10;
            int[] lockedInventory=Copy(player.items);
            sim.Apply(Command(player,"nuclear",arg:(int)NuclearTech.UraniumBeneficiation));
            Require(sim.State.nuclearResearch==0 && player.items.SequenceEqual(lockedInventory),
                "stockpiled ore bypassed the real mining prerequisite");
            player.items[(int)ItemKind.UraniumOre]=0;
            player.x=8; player.y=14;
            var deposit = sim.State.nodes.Single(n => n.id == ore.id);
            var pickaxe = EquipGatheringTool(sim, player, GearKind.StonePickaxe);
            foreach (int hp in new[] { 23, 11 })
            {
                Ready(player); sim.Apply(Command(player,"interact",10,14)); Advance(sim,.55f);
                Require(deposit.hp == hp && deposit.stock == 6 && player.items[(int)ItemKind.UraniumOre] == 0 &&
                    sim.State.nuclearUraniumMined == 0 && sim.State.gathered == 0,
                    "partial uranium mining paid early or counted toward beneficiation research");
            }
            Ready(player); sim.Apply(Command(player,"interact",10,14)); Advance(sim,.55f);
            Require(sim.State.nodes.All(n=>n.id!=ore.id) && sim.State.nuclearUraniumMined==6 && sim.State.gathered==6 &&
                player.items[(int)ItemKind.UraniumOre]==6 && sim.State.produced[(int)ItemKind.UraniumOre]==0 && pickaxe.durability == 97,
                "finite uranium mining did not record the real mined amount independently of manufactured output");
            player.x=10; player.y=18.8f; Ready(player);
            sim.Apply(Command(player,"recipe",10,20,93));
            Require(Building(sim,plant.id).recipe!=93,"unresearched beneficiation recipe could be selected");
            player.x=8; player.y=10; Ready(player);
            int[] before=Copy(player.items);
            sim.Apply(Command(player,"nuclear",arg:(int)NuclearTech.UraniumBeneficiation));
            Require(NuclearCatalog.HasResearch(sim.State,NuclearTech.UraniumBeneficiation),"mined ore could not unlock the startup research");
            Paid(player.items,before,NuclearCatalog.Definition((int)NuclearTech.UraniumBeneficiation).cost,"beneficiation research");
            Advance(sim,.55f); player.x=10; player.y=18.8f; Ready(player);
            sim.Apply(Command(player,"recipe",10,20,93));
            Advance(sim,.3f);
            player.items[(int)ItemKind.ChemicalFeedstock]=1; player.items[(int)ItemKind.ProcessWater]=2;
            sim.Apply(Command(player,"interact",10,20));
            Advance(sim,1f);
            Require(Building(sim,plant.id).progress>0 && sim.State.produced[(int)ItemKind.UraniumConcentrate]==0,
                "beneficiation did not start a powered, unfinished batch");
            string folder=Path.Combine(Path.GetTempPath(),"dead-circuit-uranium-"+Guid.NewGuid().ToString("N"));
            try
            {
                var archive=new WorldArchive(folder,Dump,text=>JsonSerializer.Deserialize<WorldState>(text,Json));
                string id=Guid.NewGuid().ToString("N");
                archive.Write(id,sim.State);
                var restored=new WorldSimulator(archive.Read(id));
                Require(restored.State.nuclearUraniumMined==6 && NuclearCatalog.HasResearch(restored.State,NuclearTech.UraniumBeneficiation),
                    "archive read lost the actual mining evidence or startup research");
                Advance(restored,12f);
                Require(Building(restored,output.id).items[(int)ItemKind.NuclearFuel]==1 &&
                    restored.State.produced[(int)ItemKind.UraniumConcentrate]==2 && restored.State.produced[(int)ItemKind.NuclearFuel]==1,
                    "restored beneficiation and logistics did not feed a real nuclear fuel batch");
                Require(Building(restored,plant.id).items[(int)ItemKind.UraniumOre]==0 &&
                    Building(restored,plant.id).items[(int)ItemKind.ChemicalFeedstock]==0 &&
                    Building(restored,plant.id).items[(int)ItemKind.ProcessWater]==0 &&
                    Building(restored,processor.id).items[(int)ItemKind.Steel]==0 &&
                    ItemTotals(restored.State)[(int)ItemKind.UraniumConcentrate]==0,
                    "the uranium chain fabricated or failed to consume a required material");
            }
            finally { if(Directory.Exists(folder)) Directory.Delete(folder,true); }
        });

        Run("uranium beneficiation routes uranium through concentrate and preserves the current save slot", () =>
        {
            bool hasConcentrate = Enum.TryParse("UraniumConcentrate", out ItemKind concentrate);
            bool hasBeneficiation = Enum.TryParse("UraniumBeneficiation", out NuclearTech beneficiation);
            Require(hasConcentrate && (int)concentrate == 62 && DemoCatalog.ItemCount == 66,
                "uranium concentrate did not retain slot 62 in the current inventory");
            Require(hasBeneficiation && NuclearCatalog.AllResearch == (1 << 11) - 1 && NuclearCatalog.Definition((int)beneficiation) != null,
                "uranium beneficiation is missing from the nuclear research catalog");

            var milling = TechCatalog.Recipe(93);
            var baseFuel = TechCatalog.Recipe(90);
            var enrichedFuel = TechCatalog.Recipe(92);
            Require(milling != null && milling.building == BuildKind.ChemicalPlant && milling.inputs[(int)ItemKind.UraniumOre] == 4 &&
                milling.inputs[(int)ItemKind.ChemicalFeedstock] == 1 && milling.inputs[(int)ItemKind.ProcessWater] == 2 &&
                milling.outputs[(int)concentrate] == 2, "the uranium beneficiation recipe is incomplete");
            Require(baseFuel.inputs[(int)concentrate] == 2 && baseFuel.inputs[(int)ItemKind.UraniumOre] == 0 &&
                enrichedFuel.inputs[(int)concentrate] == 3 && enrichedFuel.inputs[(int)ItemKind.UraniumOre] == 0,
                "nuclear fuel can still bypass beneficiation");

            var state = Flat(era: TechEra.Nuclear);
            state.nuclearUraniumMined = state.gathered = 4;
            state.nuclearResearch = NuclearBit(beneficiation);
            var sim = new WorldSimulator(state);
            var player = Builder(sim);
            player.items[(int)concentrate] = 3;
            var plant = Add(sim.State, BuildKind.ChemicalPlant, 10, 10);
            plant.recipe = milling.id;
            plant.items[(int)concentrate] = 2;
            sim.State.produced[(int)concentrate] = 3;

            var restored = new WorldSimulator(Saved(sim.State));
            Require(restored.State.version == WorldSimulator.SaveVersion && restored.State.produced[(int)concentrate] == 3 &&
                restored.State.players[0].items[(int)concentrate] == 3 && Building(restored, plant.id).items[(int)concentrate] == 2,
                "current-format save did not preserve uranium concentrate in world counters and inventories");
        });

        Run("nuclear technology validates all eleven studies and saves their full progression", () =>
        {
            var state = Flat(era: TechEra.Nuclear);
            EarnNuclearEvidence(state);
            var sim = new WorldSimulator(state);
            var player = Builder(sim);
            player.x = 8; player.y = 10;
            Add(sim.State, BuildKind.Workbench, 10, 10);
            foreach (var definition in NuclearCatalog.All.OrderBy(d => d.id == NuclearTech.UraniumBeneficiation ? -1 : (int)d.id))
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
            badMask.nuclearResearch = 1 << 11;
            Reject(badMask, "unknown nuclear research bit was accepted");

            var badParent = Flat(era: TechEra.Nuclear);
            badParent.nuclearResearch = NuclearBit(NuclearTech.FuelAssembly);
            badParent.produced[(int)ItemKind.NuclearFuel] = 2;
            badParent.produced[(int)ItemKind.NuclearWaste] = 2;
            Reject(badParent, "fuel assembly without uranium enrichment was accepted");

            var badEvidence = Flat(era: TechEra.Nuclear);
            badEvidence.nuclearResearch = NuclearBit(NuclearTech.UraniumBeneficiation) | NuclearBit(NuclearTech.UraniumEnrichment);
            badEvidence.nuclearUraniumMined = badEvidence.gathered = 4;
            Reject(badEvidence, "uranium enrichment without real fuel evidence was accepted");

            var early = Flat(era: TechEra.HighVoltage);
            early.nuclearResearch = NuclearBit(NuclearTech.UraniumEnrichment);
            early.produced[(int)ItemKind.NuclearFuel] = 2;
            Reject(early, "nuclear research before the nuclear era was accepted");

            var valid = Flat(era: TechEra.Nuclear);
            valid.nuclearResearch = NuclearBit(NuclearTech.UraniumBeneficiation) | NuclearBit(NuclearTech.UraniumEnrichment);
            valid.nuclearUraniumMined = valid.gathered = 4;
            valid.produced[(int)ItemKind.NuclearFuel] = 2;
            Require(new WorldSimulator(Saved(valid)).State.nuclearResearch == valid.nuclearResearch,
                "valid nuclear research evidence did not restore");
        });

        Run("uranium enrichment gates the high-yield fuel recipe and the technology tree command", () =>
        {
            var state = Flat(era: TechEra.Nuclear);
            state.nuclearResearch = NuclearBit(NuclearTech.UraniumBeneficiation);
            state.nuclearUraniumMined = state.gathered = 4;
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
            state.nuclearResearch = NuclearBit(NuclearTech.UraniumBeneficiation) | NuclearBit(NuclearTech.UraniumEnrichment) |
                NuclearBit(NuclearTech.CoreDiagnostics) | NuclearBit(NuclearTech.ShieldedStorage);
            state.nuclearUraniumMined = state.gathered = 4;
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
