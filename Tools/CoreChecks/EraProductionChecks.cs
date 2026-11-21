using DeadCircuit;

static partial class Checks
{
    static void EraProductionChecks()
    {
        // Fixed independent cases keep coverage from silently shrinking if a recipe is removed.
        foreach(var (id,era) in new[]
        {
            (2,TechEra.Primitive),(3,TechEra.Primitive),(10,TechEra.Primitive),(11,TechEra.Primitive),(12,TechEra.Primitive),
            (13,TechEra.Water),(20,TechEra.Water),(30,TechEra.Steam),(31,TechEra.Steam),(32,TechEra.Steam),(33,TechEra.Steam),
            (40,TechEra.LowVoltage),(41,TechEra.LowVoltage),(42,TechEra.LowVoltage),(43,TechEra.LowVoltage),(50,TechEra.LowVoltage),(60,TechEra.LowVoltage),
            (70,TechEra.MediumVoltage),(71,TechEra.MediumVoltage),(80,TechEra.HighVoltage),(90,TechEra.Nuclear),(91,TechEra.MediumVoltage),(92,TechEra.Nuclear),(93,TechEra.Nuclear),
            (52,TechEra.LowVoltage),(53,TechEra.LowVoltage),(54,TechEra.MediumVoltage),(55,TechEra.HighVoltage),(56,TechEra.LowVoltage),(57,TechEra.LowVoltage),
            (170,TechEra.HighVoltage),(171,TechEra.HighVoltage),(172,TechEra.HighVoltage),(173,TechEra.HighVoltage),
            (174,TechEra.HighVoltage),(175,TechEra.HighVoltage),(176,TechEra.HighVoltage),(177,TechEra.HighVoltage)
        })
        {
            Run($"era {era} recipe {id} consumes inputs and completes a real output batch", () =>
            {
                var r=TechCatalog.Recipe(id);Require(r!=null && r.era==era,$"required {era} recipe {id} missing or moved to a different era");
                var state=RecipeFixture(r);var machine=Add(state,r.building,10,10);machine.recipe=r.building==BuildKind.Workbench?0:r.id;
                if(r.building==BuildKind.Workbench)
                {
                    var manual=new WorldSimulator(state);var p=manual.AddPlayer("p","P");p.x=8;p.y=10;p.items=Copy(r.inputs);manual.Apply(Command(p,"craft",arg:r.id));Inventory(p.items,r.outputs,$"manual recipe {r.id}");return;
                }
                machine.items=Copy(r.inputs);
                if(r.building==BuildKind.StoneFurnace)machine.items[(int)ItemKind.Fuel]=1;else AddRecipePower(state,machine);
                if(TechCatalog.RequiresWater(r.building))Water(state,10,10+DemoCatalog.Height(r.building));
                var sim=new WorldSimulator(state);var m=Building(sim,machine.id);Advance(sim,r.duration+.15f);
                for(int i=0;i<DemoCatalog.ItemCount;i++)
                {
                    Require(m.items[i]==r.outputs[i],$"recipe {r.id} item {i}: expected {r.outputs[i]}, got {m.items[i]}");
                    Require(sim.State.produced[i]-state.produced[i]==r.outputs[i],$"recipe {r.id} production history mismatch for {i}");
                }
                if(r.building==BuildKind.StoneFurnace)Require(m.fuel>0,"stone furnace produced without lighting fuel");
            });
        }
        Run("machine recipes preserve scarce inputs when their output buffer is full", () =>
        {
            foreach(var r in TechCatalog.Recipes.Where(r=>r.building!=BuildKind.Workbench))
            {
                var state=RecipeFixture(r);var m=Add(state,r.building,10,10);m.recipe=r.id;m.items=Copy(r.inputs);
                for(int i=0;i<DemoCatalog.ItemCount;i++)if(r.outputs[i]>0)m.items[i]=WorldSimulator.BufferLimit-r.outputs[i]+1;
                if(m.kind==BuildKind.StoneFurnace)m.items[(int)ItemKind.Fuel]=1;else AddRecipePower(state,m);
                if(TechCatalog.RequiresWater(m.kind))Water(state,10,10+DemoCatalog.Height(m.kind));
                var before=Copy(m.items);var sim=new WorldSimulator(state);Advance(sim,r.duration+1);m=Building(sim,m.id);
                for(int i=0;i<DemoCatalog.ItemCount;i++)if(i!=(int)ItemKind.Fuel || m.kind!=BuildKind.StoneFurnace)Require(m.items[i]==before[i],$"blocked recipe {r.id} lost/created item {i}");
                Require(sim.State.produced.SequenceEqual(state.produced),$"blocked recipe {r.id} counted phantom production");
            }
        });
        Run("every non-free machine recipe stalls with one required input missing", () =>
        {
            foreach(var r in TechCatalog.Recipes.Where(r=>r.building!=BuildKind.Workbench && r.inputs.Any(v=>v>0)))
            {
                var state=RecipeFixture(r);var m=Add(state,r.building,10,10);m.recipe=r.id;m.items=Copy(r.inputs);int missing=Array.FindIndex(r.inputs,v=>v>0);m.items[missing]--;
                if(m.kind==BuildKind.StoneFurnace)m.items[(int)ItemKind.Fuel]=1;else AddRecipePower(state,m);
                if(TechCatalog.RequiresWater(m.kind))Water(state,10,10+DemoCatalog.Height(m.kind));
                var sim=new WorldSimulator(state);Advance(sim,r.duration+1);m=Building(sim,m.id);
                for(int i=0;i<DemoCatalog.ItemCount;i++)if(r.outputs[i]>0)Require(m.items[i]==0,$"recipe {r.id} manufactured without all inputs");
                Require(sim.State.produced.SequenceEqual(state.produced),"missing input fabricated production history");
            }
        });
        Run("all item types travel through their compatible solid or liquid ports as conserved cargo", () =>
        {
            for(int item=0;item<DemoCatalog.ItemCount;item++)
            {
                if(item==(int)ItemKind.NuclearWaste) continue;
                var state=Flat(era:TechEra.Nuclear);bool liquid=PetrochemicalCatalog.IsFluid(item);
                var from=Add(state,liquid?BuildKind.LiquidPipe:BuildKind.Belt,10,10);from.items[item]=1;
                var to=Add(state,liquid?BuildKind.LiquidTank:BuildKind.Chest,11,10);
                if(liquid)for(int setting=0;setting<WorldSimulator.FluidCount;setting++)if(WorldSimulator.FluidItem(setting)==item)from.setting=to.setting=setting;
                var sim=new WorldSimulator(state);var expected=ItemTotals(sim.State);var flight=AwaitCargo(sim,from.id,to.id);Require(flight.item==item,$"wrong cargo item for {item}");
                for(int n=0;n<20;n++){sim.Step(WorldSimulator.FixedStep);Conserved(sim.State,expected);}
                Require(Building(sim,to.id).items[item]==1 && Building(sim,from.id).items[item]==0,$"new item {item} failed delivery");
            }
        });
        Run("every machine recipe exports its declared output through its real port", () =>
        {
            foreach(var r in TechCatalog.Recipes.Where(r=>r.building!=BuildKind.Workbench))
            for(int item=0;item<DemoCatalog.ItemCount;item++)
            {
                if(r.outputs[item]==0)continue;
                var state=RecipeFixture(r);var source=Add(state,r.building,10,10);source.recipe=r.id;source.items=Copy(r.outputs);
                bool liquid=PetrochemicalCatalog.IsFluid(item);
                var target=Add(state,liquid?BuildKind.LiquidTank:BuildKind.Chest,DemoCatalog.OutputX(source),DemoCatalog.OutputY(source));
                if(liquid)for(int setting=0;setting<WorldSimulator.FluidCount;setting++)if(WorldSimulator.FluidItem(setting)==item)target.setting=setting;
                var sim=new WorldSimulator(state);var expected=ItemTotals(sim.State);
                for(int n=0;n<65;n++){sim.Step(WorldSimulator.FixedStep);Conserved(sim.State,expected);}
                Require(Building(sim,target.id).items[item]==r.outputs[item],$"recipe {r.id} export item {item}");
            }
        });
        Run("mechanical crushing then primitive smelting improves one real ore to two iron", () =>
        {
            var state=Flat(era:TechEra.Water);var crusher=Add(state,BuildKind.MechanicalCrusher,10,10);crusher.items[(int)ItemKind.Ore]=1;AddRecipePower(state,crusher);
            var belt=Add(state,BuildKind.Belt,12,10);var furnace=Add(state,BuildKind.StoneFurnace,13,10);furnace.recipe=13;furnace.items[(int)ItemKind.Fuel]=1;
            var chest=Add(state,BuildKind.Chest,15,10);var sim=new WorldSimulator(state);Advance(sim,8);
            Require(Building(sim,chest.id).items[(int)ItemKind.Iron]==2,"water-era chain did not produce two iron from one ore");
            Require(sim.State.buildings.Sum(b=>b.items[(int)ItemKind.Ore]+b.items[(int)ItemKind.CrushedOre])==0 && sim.State.cargo.Count==0,"chain did not consume and deliver all intermediate material");
            Require(sim.State.produced[(int)ItemKind.CrushedOre]==1 && sim.State.produced[(int)ItemKind.Iron]==2,"chain production counters lost a stage");
        });
        Run("electric mining preserves copper tin and uranium identity through logistics", () =>
        {
            foreach(var (node,item) in new[] { (NodeKind.CopperOre,ItemKind.CopperOre),(NodeKind.TinOre,ItemKind.TinOre),(NodeKind.UraniumOre,ItemKind.UraniumOre) })
            {
                var state=Flat(era:TechEra.Nuclear);state.nodes.Add(new NodeState { id=state.nextId++,kind=node,x=12,y=11,stock=2 });var miner=Add(state,BuildKind.Miner,10,10);Add(state,BuildKind.Generator,7,7).items[(int)ItemKind.Fuel]=1;
                Add(state,BuildKind.Belt,12,10);var chest=Add(state,BuildKind.Chest,13,10);var sim=new WorldSimulator(state);Advance(sim,8);
                Require(Building(sim,chest.id).items[(int)item]==2 && sim.State.gathered==2,$"{node} mining lost its item identity");Require(sim.State.nodes.Count==0 && sim.State.cargo.Count==0,"mineral chain did not exhaust and deliver two ore");
            }
        });
        Run("nuclear processor feeds a reactor which recirculates coolant and exports real waste", () =>
        {
            var state=Flat(era:TechEra.Nuclear);var processor=Add(state,BuildKind.NuclearProcessor,10,10);processor.recipe=90;processor.items[(int)ItemKind.UraniumConcentrate]=2;processor.items[(int)ItemKind.Steel]=1;
            var generator=Add(state,BuildKind.HighGenerator,4,10);generator.items[(int)ItemKind.Fuel]=100;Add(state,BuildKind.HighPole,9,14);var console=Add(state,BuildKind.ReactorControlConsole,10,14);console.setting=1;
            var reactor=Add(state,BuildKind.Reactor,13,10);var waste=Add(state,BuildKind.NuclearWasteVault,16,10);
            var tower=Add(state,BuildKind.CoolingTower,13,7);Water(state,13,6);Add(state,BuildKind.DieselGenerator,10,7).items[(int)ItemKind.Diesel]=100;
            tower.recipe=91;tower.items[(int)ItemKind.ProcessWater]=20;
            reactor.items[(int)ItemKind.Coolant]=6;Add(state,BuildKind.LiquidPipe,14,9,3).setting=7;
            foreach(var cell in new[] {(16,7,1),(16,8,1),(16,9,2),(15,9,1)})Add(state,BuildKind.LiquidPipe,cell.Item1,cell.Item2,cell.Item3).setting=8;
            var sim=new WorldSimulator(state);Advance(sim,18);reactor=Building(sim,reactor.id);
            Require(reactor.powerGeneration>0 && reactor.fuel>0,"delivered nuclear fuel/coolant did not start the reactor");Require(Building(sim,waste.id).items[(int)ItemKind.NuclearWaste]==1,"reactor failed to export its real nuclear waste");
            Require(sim.State.produced[(int)ItemKind.NuclearFuel]==1 && sim.State.produced[(int)ItemKind.Coolant]>=4 && sim.State.produced[(int)ItemKind.NuclearWaste]==1,"nuclear chain fabricated or omitted a production stage");
            var totals=ItemTotals(sim.State);
            Require(totals[(int)ItemKind.Coolant]+totals[(int)ItemKind.HotCoolant]==6 && totals[(int)ItemKind.ProcessWater]<20,"coolant loop lost mass, made new coolant, or did not debit process water");
            Require(Building(sim,processor.id).items[(int)ItemKind.UraniumConcentrate]==0 && Building(sim,processor.id).items[(int)ItemKind.Steel]==0,"nuclear processing did not debit concentrate/steel");
        });
    }

    static WorldState RecipeFixture(RecipeDefinition recipe)
    {
        var state=Flat(era:recipe.era);
        if(recipe.id==92 || recipe.id==93)
        {
            state.nuclearUraniumMined=state.gathered=4;
            state.produced[(int)ItemKind.NuclearFuel]=2;
            state.nuclearResearch=NuclearBit(NuclearTech.UraniumBeneficiation) |
                (recipe.id==92 ? NuclearBit(NuclearTech.UraniumEnrichment) : 0);
        }
        return state;
    }

    static void AddRecipePower(WorldState state, BuildingState machine)
    {
        int tier=TechCatalog.EnergyTier(machine.kind);
        if(tier<0) return; // Self-heated kilns need recipe inputs, not a power network.
        var kind=tier==0?BuildKind.Waterwheel:tier==1?BuildKind.Boiler:tier==2?BuildKind.Generator:tier==3?BuildKind.DieselGenerator:BuildKind.HighGenerator;
        var source=Add(state,kind,tier==4?6:7,10);
        if(tier<=1){Water(state,7,12);Add(state,tier==0?BuildKind.Shaft:BuildKind.SteamPipe,9,10);}
        if(TechCatalog.IsFuelBurner(kind))source.items[(int)(kind==BuildKind.DieselGenerator?ItemKind.Diesel:ItemKind.Fuel)]=4;
    }
}
