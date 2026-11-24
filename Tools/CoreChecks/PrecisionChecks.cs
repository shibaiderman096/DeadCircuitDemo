using DeadCircuit;

static partial class Checks
{
    static void PrecisionChecks()
    {
        Run("HV precision starts with a locked high grid and unlocks silicon research at a research bench", () =>
        {
            var state=Flat(era:TechEra.MediumVoltage); Add(state,BuildKind.Workbench,10,10);Add(state,BuildKind.ResearchBench,9,12);
            var sim=new WorldSimulator(state); var p=Builder(sim); p.x=8; p.y=10;
            sim.State.era=TechEra.HighVoltage;
            p.items = Copy(PrecisionCatalog.Definition(0).cost);
            var poleCost = DemoCatalog.BuildCost(BuildKind.HighPole);
            for (int i = 0; i < p.items.Length; i++) p.items[i] += poleCost[i];
            var before=Copy(p.items);
            BuildNow(sim,p,10,14,(int)BuildKind.HighPole);
            Require(sim.State.buildings.All(building=>building.kind!=BuildKind.HighPole),"HV era alone unlocked the high grid");
            Inventory(p.items,before,"locked HV construction");
            Ready(p); sim.Apply(Command(p,"hv",arg:0));
            Require(p.notice.Contains("硅材制备") && p.items[(int)ItemKind.Circuit]==before[(int)ItemKind.Circuit]-2,
                "silicon research did not unlock and charge once: "+p.notice);
        });
        Run("HV precision interlock admits full input stacks and waits for room for each entire output batch", () =>
        {
            var state=Flat(era:TechEra.HighVoltage);
            var machine=Add(state,BuildKind.PrecisionAssembler,10,10);
            var recipe=TechCatalog.SelectedRecipe(machine);
            Require(recipe?.id==80,"new assembler did not default to control modules");
            var source=Add(state,BuildKind.HighGenerator,7,10);
            source.items[(int)ItemKind.Fuel]=4;
            foreach(int i in Enumerable.Range(0,DemoCatalog.ItemCount).Where(i=>recipe.inputs[i]>0)) machine.items[i]=20;
            int control=(int)ItemKind.ControlModule, hot=(int)ItemKind.HotCoolant;
            machine.items[control]=7; machine.items[hot]=8;
            machine.items[(int)ItemKind.ArmorPiercingAmmo]=20; // Stock left by a previous recipe.
            var sim=new WorldSimulator(state); machine=Building(sim,machine.id);
            var p=Builder(sim); p.x=10; p.y=8;
            sim.Apply(Command(p,"configure",10,10,1));
            Require(machine.setting==1,"automatic control did not configure the assembler");
            var before=Copy(machine.items);
            Advance(sim,5); Inventory(machine.items,before,"partial output room");
            Require(machine.powered && machine.progress==0 && sim.State.hvPowered==0,"blocked batch advanced");
            machine.items[control]--; before=Copy(machine.items);
            Advance(sim,5); Inventory(machine.items,before,"full heat byproduct");
            Require(sim.State.hvPowered==0,"full byproduct counted HV production");
            machine.items[hot]--; Advance(sim,5);
            Require(sim.State.hvPowered==2 && machine.items[control]==8 && machine.items[hot]==8,
                "clearing both outputs did not resume one batch");
            Require(machine.items[(int)ItemKind.AdvancedCircuit]==18 && machine.items[(int)ItemKind.ArmorPiercingAmmo]==20,
                "interlock applied the output cap to unrelated inventory");
        });
        Run("HV precision current save preserves five second recipe mid-batch and rejects forged evidence", () =>
        {
            var state=Flat(era:TechEra.HighVoltage);
            var machine=Add(state,BuildKind.ElectronicsBench,10,10);
            machine.recipe=176; machine.progress=4.5f; machine.items=Copy(TechCatalog.Recipe(176).inputs);
            var source=Add(state,BuildKind.DieselGenerator,7,10); source.items[(int)ItemKind.Diesel]=1;
            var restored=new WorldSimulator(Saved(state));
            Near(Building(restored,machine.id).progress,4.5f,"saved progress above four seconds");
            Advance(restored,.6f);
            Require(restored.State.produced[(int)ItemKind.ControlModule]==3 && restored.State.hvPowered==0,
                "restored MV batch failed or was counted as HV");
            foreach(var corrupt in new Action<WorldState>[] {
                s=>s.hvResearch=256,
                s=>s.hvResearch=1<<(int)PrecisionTech.Grid,
                s=>s.hvPowered=3,
                s=>s.produced[(int)ItemKind.PureSilicon]=0,
                s=>s.buildings[0].progress=5.01f
            }) { var invalid=Saved(state); corrupt(invalid); Reject(invalid,"forged HV save accepted"); }
            var locked=Flat(era:TechEra.HighVoltage); locked.hvResearch=0;
            Add(locked,BuildKind.PrecisionAssembler,10,10).setting=1;
            Reject(locked,"unresearched assembly interlock saved");
            var badSetting=Flat(era:TechEra.HighVoltage);
            Add(badSetting,BuildKind.PrecisionAssembler,10,10).setting=2;
            Reject(badSetting,"invalid assembly mode saved");
        });
    }
}
