using DeadCircuit;

static partial class Checks
{
    static void PetrochemicalChecks()
    {
        Run("MV new world starts with finite petroleum available for exploration", () =>
        {
            var sim = new WorldSimulator(73149);
            Require(sim.State.nodes.Any(n => (int)n.kind == 13 && n.stock > 0), "new world has no oil source");
        });
        Run("MV entering era keeps petrochemical machinery and processing individually locked", () =>
        {
            var state=Flat(); state.era=TechEra.MediumVoltage;
            var sim=new WorldSimulator(state); var p=Builder(sim); p.x=8;p.y=10;
            var before=Copy(p.items);
            foreach(var kind in new[] {BuildKind.OilPump,BuildKind.DistillationUnit,BuildKind.ChemicalReactor,BuildKind.DieselGenerator,BuildKind.CoolingTower})
            {
                Ready(p);BuildNow(sim,p,10,10,(int)kind);sim.Step(WorldSimulator.FixedStep);
            }
            Require(sim.State.buildings.Count==0 && sim.State.mvResearch==0,"MV era granted industrial processes");
            Inventory(p.items,before,"locked MV construction charged");
        });
        Run("MV research samples cannot replace real exploration and production", () =>
        {
            var state=Flat();state.era=TechEra.MediumVoltage;Add(state,BuildKind.Workbench,10,10);Add(state,BuildKind.ResearchBench,9,12);
            var sim=new WorldSimulator(state);var p=Builder(sim);p.x=8;p.y=10;
            sim.Apply(Command(p,"mv",arg:0));
            Require(sim.State.mvResearch==1,"LV graduate cannot research petroleum exploration: "+p.notice);
            var before=Copy(p.items);
            for(int id=1;id<8;id++) { Advance(sim,.55f);sim.Apply(Command(p,"mv",arg:id)); }
            Require(sim.State.mvResearch==1,"inventory samples bypassed exploration/production");
            Inventory(p.items,before,"rejected research charged");
            Require(sim.State.mvExtracted==0 && sim.State.mvDistilled==0 && sim.State.mvPolymer==0,"rejected research fabricated history");
        });
        Run("MV research checks live connected nearby scientists and exact one-time payment", () =>
        {
            for(int variant=0;variant<6;variant++)
            {
                var state=Flat();state.era=TechEra.MediumVoltage;Add(state,BuildKind.Workbench,10,10);Add(state,BuildKind.ResearchBench,9,12);
                var sim=new WorldSimulator(state);var p=Builder(sim);p.x=8;p.y=10;
                if(variant==0)p.hp=0; else if(variant==1)sim.SetConnected(p.id,false);
                else if(variant==2)p.x=40;else if(variant==3)p.interactCooldown=1;
                else if(variant==4)p.items[(int)ItemKind.Circuit]=1;
                var before=Copy(p.items);sim.Apply(Command(p,"mv",arg:0));
                if(variant<5) { Require(sim.State.mvResearch==0,"ineligible MV scientist researched");Inventory(p.items,before,"ineligible MV scientist paid"); }
                else
                {
                    Paid(p.items,before,PetrochemicalCatalog.Definition(0).cost,"MV exact research payment");
                    before=Copy(p.items);Advance(sim,.55f);sim.Apply(Command(p,"mv",arg:0));Inventory(p.items,before,"duplicate MV research paid twice");
                }
            }
        });
        Run("MV invalid research commands preserve technology inventory and personal growth", () =>
        {
            var state=Flat();state.era=TechEra.MediumVoltage;Add(state,BuildKind.Workbench,10,10);Add(state,BuildKind.ResearchBench,9,12);
            var sim=new WorldSimulator(state);var p=Builder(sim);p.x=8;p.y=10;var before=Copy(p.items);
            foreach(int id in new[] {int.MinValue,-1,8,int.MaxValue}) { sim.Apply(Command(p,"mv",arg:id));sim.Step(WorldSimulator.FixedStep); }
            Require(sim.State.mvResearch==0 && p.skillPoints==1 && p.attributePoints==2,"invalid MV command changed progression");
            Inventory(p.items,before,"invalid MV command charged");
        });
        Run("MV startup paths need LV components and water without future materials", () =>
        {
            foreach(var kind in new[] {BuildKind.OilPump,BuildKind.ProcessWaterPump,BuildKind.DistillationUnit,BuildKind.ChemicalReactor})
            {
                Require(TechCatalog.EnergyTier(kind)==2,"first petrochemical plant needs MV power");
                var cost=DemoCatalog.BuildCost(kind);
                foreach(var item in new[] {ItemKind.Polymer,ItemKind.Lubricant,ItemKind.AdvancedCircuit,ItemKind.Coolant})
                    Require(cost[(int)item]==0,"starter depends on its own product");
            }
            Require(TechCatalog.Recipe(70).inputs[(int)ItemKind.Wood]==0 && TechCatalog.Recipe(70).inputs[(int)ItemKind.ChemicalFeedstock]==2,"polymer still bypasses petroleum");
            Require(TechCatalog.Recipe(91).inputs[(int)ItemKind.HotCoolant]==2,"cooling tower fabricates free coolant");
        });
    }
}
