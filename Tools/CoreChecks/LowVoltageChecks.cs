using DeadCircuit;

static partial class Checks
{
    static void LowVoltageChecks()
    {
        Run("LV era entry does not bypass electrical research", () =>
        {
            var state = Flat(era: TechEra.Steam); state.era = TechEra.LowVoltage;
            var sim = new WorldSimulator(state); var p = Builder(sim);
            p.x = 8; p.y = 10; var before = Copy(p.items);
            sim.Apply(Command(p, "build", 10, 10, (int)BuildKind.Generator));
            Require(sim.State.buildings.Count == 0, "entering LV immediately granted generator manufacturing");
            Inventory(p.items, before, "locked electrical equipment charged materials");
        });
        Run("LV starter recipes use existing ceramics and mechanical power without advanced inputs", () =>
        {
            var state=Flat(era:TechEra.Steam); state.era=TechEra.LowVoltage;
            Require(TechCatalog.EnergyTier(BuildKind.WindingBench)==0 && DemoCatalog.Demand(BuildKind.WindingBench)==25,"first coils require electrical power");
            foreach(int id in new[] {140,141})
                Require(PrimitiveCatalog.IsRecipeUnlocked(state,TechCatalog.Recipe(id)),"starter wire or insulation requires electrical research");
            foreach(int id in Enumerable.Range(140,11).Append(60))
            {
                var r=TechCatalog.Recipe(id);
                foreach(ItemKind item in new[] {ItemKind.Polymer,ItemKind.AdvancedCircuit,ItemKind.NuclearFuel})
                    Require(r.inputs[(int)item]==0,"LV recipe has a future-tier material dependency");
            }
            Require(DemoCatalog.BuildCost(BuildKind.Generator)[(int)ItemKind.Motor]==0,"first generator needs an electric motor");
        });
        Run("LV unfunded premature and duplicate research never charges inventory", () =>
        {
            var state=Flat(era:TechEra.Steam); state.era=TechEra.LowVoltage;
            Add(state,BuildKind.Workbench,10,10);
            var sim=new WorldSimulator(state);var p=Builder(sim);p.x=8;p.y=10;
            var before=Copy(p.items);
            foreach(int id in new[] {int.MinValue,-1,0,1,2,3,4,5,6,7,8,int.MaxValue})
            {
                sim.Apply(Command(p,"lv",arg:id));sim.Step(WorldSimulator.FixedStep);
            }
            Require(sim.State.lvResearch==0,"samples bypassed actual startup production");
            Inventory(p.items,before,"premature research paid materials");
            sim.State.lvWire=4;sim.State.lvInsulators=2;
            var definition=LowVoltageCatalog.Definition((int)LowVoltageTech.Generation);
            p.items[(int)ItemKind.CopperWire]=definition.cost[(int)ItemKind.CopperWire]-1;before=Copy(p.items);
            sim.Apply(Command(p,"lv",arg:(int)LowVoltageTech.Generation));
            Require(sim.State.lvResearch==0,"one-short sample was accepted");Inventory(p.items,before,"failed research partially paid");
            p.items[(int)ItemKind.CopperWire]++;before=Copy(p.items);
            int skills=p.skillPoints,attributes=p.attributePoints;
            sim.Apply(Command(p,"lv",arg:(int)LowVoltageTech.Generation));
            Require(LowVoltageCatalog.HasResearch(sim.State,LowVoltageTech.Generation),"valid research rejected: "+p.notice);
            Paid(p.items,before,definition.cost,"LV research exact payment");
            Require(p.skillPoints==skills && p.attributePoints==attributes,"research consumed personal growth");
            before=Copy(p.items);Ready(p);sim.Apply(Command(p,"lv",arg:(int)LowVoltageTech.Generation));
            Inventory(p.items,before,"repeated research charged twice");
        });
        Run("LV research requires a live connected reachable eligible scientist", () =>
        {
            foreach(int variant in Enumerable.Range(0,5))
            {
                var state=Flat(era:TechEra.Steam); state.era=TechEra.LowVoltage;state.lvWire=4;state.lvInsulators=2;
                Add(state,BuildKind.Workbench,10,10);
                var sim=new WorldSimulator(state);var p=Builder(sim);p.x=8;p.y=10;
                if(variant==0)p.hp=0;else if(variant==1)sim.SetConnected(p.id,false);
                else if(variant==2)p.x=30;else if(variant==3)p.interactCooldown=1;
                else sim.State.buildings[0].hp=0;
                var before=Copy(p.items);sim.Apply(Command(p,"lv",arg:0));
                Require(sim.State.lvResearch==0,"ineligible scientist researched");Inventory(p.items,before,"ineligible scientist paid");
            }
            var world=Flat();var stranger=new PlayerState{id="outsider"};
            Require(!LowVoltageCatalog.CanResearch(world,stranger,0),"foreign scientist accepted");
        });
        Run("LV locked workbench parts reject commands without material or production changes", () =>
        {
            foreach(int id in new[] {142,144,145})
            {
                var state=Flat(era:TechEra.Steam);state.era=TechEra.LowVoltage;Add(state,BuildKind.Workbench,10,10);
                var sim=new WorldSimulator(state);var p=Builder(sim);p.x=8;p.y=10;var before=Copy(p.items);
                sim.Apply(Command(p,"craft",arg:id));
                Inventory(p.items,before,"locked workbench part crafted");
                Require(sim.State.lvCable==0 && sim.State.lvMotors==0 && sim.State.lvRelays==0 && sim.State.produced.All(n=>n==0),"rejected craft fabricated production evidence");
            }
        });
    }
}
