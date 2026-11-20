using DeadCircuit;

static partial class Checks
{
    static void EraEnergyChecks()
    {
        Run("waterwheel needs a dry shoreline footprint and stops when water disappears", () =>
        {
            foreach(bool wet in new[] { false,true })
            {
                var state=Flat(era:TechEra.Water);if(wet)Water(state,10,12);var sim=new WorldSimulator(state);var p=Builder(sim);p.x=8;p.y=10;var before=Copy(p.items);sim.Apply(Command(p,"build",10,10,(int)BuildKind.Waterwheel));
                Require((sim.State.buildings.Count==1)==wet,"waterwheel ignored shoreline placement rule");
                if(!wet){Inventory(p.items,before,"dry waterwheel charged materials");continue;}
                var wheel=sim.State.buildings.Single();Advance(sim,.05f);Require(wheel.powerGeneration>0,"shore waterwheel generated no mechanical power");
                sim.State.ground=new string('4',4096);Advance(sim,.1f);Near(wheel.powerGeneration,0,"dry waterwheel kept producing power");
            }
            var flooded=Flat(era:TechEra.Water);Water(flooded,11,11);var world=new WorldSimulator(flooded);var actor=Builder(world);actor.x=8;actor.y=10;var inventory=Copy(actor.items);world.Apply(Command(actor,"build",10,10,(int)BuildKind.Waterwheel));
            Require(world.State.buildings.Count==0,"waterwheel footprint submerged a cell");Inventory(actor.items,inventory,"submerged placement charged materials");
        });
        foreach(var (era,sourceKind,linkKind,machineKind,recipe,output) in new[]
        {
            (TechEra.Water,BuildKind.Waterwheel,BuildKind.Shaft,BuildKind.MechanicalCrusher,20,ItemKind.CrushedOre),
            (TechEra.Steam,BuildKind.Boiler,BuildKind.SteamPipe,BuildKind.SteamFurnace,30,ItemKind.Steel)
        })
        {
            Run($"{era} transmission requires touching links and stops after the link is dismantled", () =>
            {
                var state=Flat(era:era);Water(state,7,12);var source=Add(state,sourceKind,7,10);if(sourceKind==BuildKind.Boiler)source.items[(int)(source.kind==BuildKind.DieselGenerator ? ItemKind.Diesel : ItemKind.Fuel)]=2;
                var m=Add(state,machineKind,10,10);m.recipe=recipe;m.items=Copy(TechCatalog.Recipe(recipe).inputs);var sim=new WorldSimulator(state);Advance(sim,1);var target=Building(sim,m.id);
                Require(Building(sim,source.id).powerGeneration>0 && !target.powered && target.progress==0,"one-cell air gap transmitted mechanical/steam power");
                var p=Builder(sim);p.x=9;p.y=8;sim.Apply(Command(p,"build",9,10,(int)linkKind));Require(sim.State.buildings.Any(b=>b.kind==linkKind),"connecting link did not build");Advance(sim,TechCatalog.Recipe(recipe).duration+.1f);
                Require(target.powered && target.items[(int)output]>0,"touching link did not run actual production");
                sim.Apply(Command(p,"dismantle",9,10));Require(!sim.State.buildings.Any(b=>b.kind==linkKind),"link was not dismantled");float progress=target.progress;int count=target.items[(int)output];Advance(sim,1);
                Require(!target.powered && target.progress==progress && target.items[(int)output]==count,"dismantled link left phantom transmission");
            });
        }
        Run("mechanical steam LV MV and HV nodes never form a bare cross-tier grid", () =>
        {
            var sources=new[] { BuildKind.Waterwheel,BuildKind.Boiler,BuildKind.Generator,BuildKind.DieselGenerator,BuildKind.HighGenerator };
            for(int first=0;first<5;first++)for(int second=first+1;second<5;second++)
            {
                var state=Flat(era:TechEra.Nuclear);var a=Add(state,sources[first],10,10);var b=Add(state,sources[second],13,10);Water(state,10,12);Water(state,13,12);
                if(TechCatalog.IsFuelBurner(a.kind))a.items[(int)(a.kind==BuildKind.DieselGenerator ? ItemKind.Diesel : ItemKind.Fuel)]=1;if(TechCatalog.IsFuelBurner(b.kind))b.items[(int)(b.kind==BuildKind.DieselGenerator ? ItemKind.Diesel : ItemKind.Fuel)]=1;
                var sim=new WorldSimulator(state);Advance(sim,.1f);a=Building(sim,a.id);b=Building(sim,b.id);
                Require(a.powerGeneration>0 && b.powerGeneration>0,"cross-tier isolation fixture lacks live sources");
                Require(a.networkId!=0 && b.networkId!=0 && a.networkId!=b.networkId,$"bare tiers {first}/{second} connected");
                Require(sim.State.powerNetworks.Single(n=>n.id==a.networkId).tier==first && sim.State.powerNetworks.Single(n=>n.id==b.networkId).tier==second,"network lost its energy identity");
            }
        });
        Run("wrong-tier generators cannot power otherwise productive consumers", () =>
        {
            var sources=new[] { BuildKind.Waterwheel,BuildKind.Boiler,BuildKind.Generator,BuildKind.DieselGenerator,BuildKind.HighGenerator };
            var consumers=new[] { BuildKind.MechanicalCrusher,BuildKind.SteamFurnace,BuildKind.Smelter,BuildKind.ChemicalPlant,BuildKind.PrecisionAssembler };
            for(int input=0;input<5;input++)for(int output=0;output<5;output++)if(input!=output)
            {
                var state=Flat(era:TechEra.Nuclear);var source=Add(state,sources[input],10,10);Water(state,10,12);if(TechCatalog.IsFuelBurner(source.kind))source.items[(int)(source.kind==BuildKind.DieselGenerator ? ItemKind.Diesel : ItemKind.Fuel)]=1;
                var machine=Add(state,consumers[output],13,10);var r=TechCatalog.DefaultRecipe(machine.kind);machine.items=Copy(r.inputs);machine.recipe=r.id;
                var sim=new WorldSimulator(state);Advance(sim,r.duration+1);machine=Building(sim,machine.id);
                Require(Building(sim,source.id).powerGeneration>0,"wrong-tier negative control had no live generation");
                Require(!machine.powered && machine.progress==0 && sim.State.produced.All(v=>v==0),$"tier {input} powered a tier {output} recipe");
                Inventory(machine.items,r.inputs,"wrong-tier machine consumed recipe material");
            }
        });
        Run("boiler requires water and burns exactly one fuel for a twenty-second steam supply", () =>
        {
            foreach(var (wet,fueled) in new[] { (false,true),(true,false),(true,true) })
            {
                var state=Flat(era:TechEra.Steam);var boiler=Add(state,BuildKind.Boiler,10,10);if(wet)Water(state,10,12);if(fueled)boiler.items[(int)ItemKind.Fuel]=2;
                var sim=new WorldSimulator(state);Advance(sim,1);boiler=Building(sim,boiler.id);
                if(wet && fueled)
                {
                    Near(boiler.powerGeneration,120,"fueled boiler steam output");Require(boiler.items[(int)ItemKind.Fuel]==1 && boiler.fuel>18,"boiler failed to debit one real fuel");
                    Advance(sim,18);Require(boiler.items[(int)ItemKind.Fuel]==1,"boiler burned second fuel before twenty seconds");Advance(sim,2);Require(boiler.items[(int)ItemKind.Fuel]==0 && boiler.fuel>18,"boiler did not commit second fuel once");
                }
                else {Near(boiler.powerGeneration,0,"boiler generated free steam");Require(boiler.items[(int)ItemKind.Fuel]==(fueled?2:0),"idle dry boiler wasted fuel");}
            }
        });
        Run("steam turbine cannot generate without steam but a fueled boiler runs a real LV recipe", () =>
        {
            foreach(bool steam in new[] { false,true })
            {
                var state=Flat();var turbine=Add(state,BuildKind.SteamTurbine,10,10);var target=Add(state,BuildKind.Smelter,14,10);target.items[(int)ItemKind.Ore]=1;
                if(steam){var boiler=Add(state,BuildKind.Boiler,7,10);boiler.items[(int)ItemKind.Fuel]=1;Water(state,7,12);Add(state,BuildKind.SteamPipe,9,10);}
                var sim=new WorldSimulator(state);Advance(sim,3);turbine=Building(sim,turbine.id);target=Building(sim,target.id);
                if(steam){Near(turbine.powerGeneration,64,"turbine 80 steam at 80% conversion");Require(target.items[(int)ItemKind.Iron]==1 && target.powered,"steam did not power LV smelting");}
                else {Near(turbine.powerGeneration,0,"turbine invented electricity");Require(!target.powered && target.items[(int)ItemKind.Ore]==1 && target.items[(int)ItemKind.Iron]==0,"unfed turbine powered LV consumer");}
            }
        });
        Run("transformer output respects both its input energy and two-hundred input cap", () =>
        {
            foreach(bool solar in new[] { false,true })
            {
                var state=Flat(era:TechEra.MediumVoltage);var source=Add(state,solar?BuildKind.SolarPanel:BuildKind.DieselGenerator,7,10);if(!solar)source.items[(int)(source.kind==BuildKind.DieselGenerator ? ItemKind.Diesel : ItemKind.Fuel)]=1;
                var transformer=Add(state,BuildKind.Transformer,11,10);var machine=Add(state,BuildKind.Smelter,15,10);machine.items[(int)ItemKind.Ore]=1;
                var sim=new WorldSimulator(state);Advance(sim,3);source=Building(sim,source.id);transformer=Building(sim,transformer.id);machine=Building(sim,machine.id);
                Require(transformer.powerGeneration>0 && transformer.powerGeneration<=180.001f,"transformer output exceeded capped 90% conversion");
                Require(transformer.powerGeneration<=source.powerGeneration*.9f+.001f,"transformer created energy beyond its source");Near(transformer.powerGeneration,Math.Min(200,source.powerGeneration)*.9f,"transformer bounded conversion");
                Require(machine.powered && machine.items[(int)ItemKind.Iron]==1,"valid transformer failed to power an LV recipe");
                Require(sim.State.powerNetworks.Single(n=>n.id==source.networkId).tier==3 && sim.State.powerNetworks.Single(n=>n.id==transformer.networkId).tier==2,"transformer shorted voltage tiers");
            }
        });
        Run("parallel transformers share finite upstream power without multiplying energy", () =>
        {
            var state=Flat(era:TechEra.MediumVoltage);var source=Add(state,BuildKind.DieselGenerator,4,10);source.items[(int)(source.kind==BuildKind.DieselGenerator ? ItemKind.Diesel : ItemKind.Fuel)]=1;Add(state,BuildKind.Transformer,7,10);Add(state,BuildKind.Transformer,7,13);
            var sim=new WorldSimulator(state);Advance(sim,.1f);var converters=sim.State.buildings.Where(b=>b.kind==BuildKind.Transformer).ToArray();
            Require(converters.All(b=>b.powerGeneration>0),"parallel transformer was starved rather than proportionally supplied");Near(converters.Sum(b=>b.powerGeneration),216,"240 input shared at 90% efficiency");
            Near(sim.State.powerNetworks.Single(n=>n.tier==3).demand,400,"converter input demand not reserved once per transformer");
        });
        Run("unfed transformer chains cannot bootstrap electricity or reuse stale generation", () =>
        {
            var state=Flat(era:TechEra.Nuclear);var high=Add(state,BuildKind.Transformer,10,10);high.setting=1;high.powerGeneration=180;var low=Add(state,BuildKind.Transformer,13,10);low.powerGeneration=180;
            var machine=Add(state,BuildKind.Smelter,17,10);machine.items[(int)ItemKind.Ore]=1;var sim=new WorldSimulator(state);Advance(sim,4);
            Require(sim.State.powerNetworks.All(n=>n.generation==0),"unfed chain invented power");Require(sim.State.buildings.Where(b=>b.kind==BuildKind.Transformer).All(b=>b.powerGeneration==0),"stale converter generation survived restore");
            Require(Building(sim,machine.id).items[(int)ItemKind.Iron]==0,"unfed chain completed production");
        });
        Run("HV to MV to LV conversion powers production with both conversion losses", () =>
        {
            var state=Flat(era:TechEra.HighVoltage);var source=Add(state,BuildKind.HighGenerator,4,10);source.items[(int)(source.kind==BuildKind.DieselGenerator ? ItemKind.Diesel : ItemKind.Fuel)]=1;
            var high=Add(state,BuildKind.Transformer,7,10);high.setting=1;var low=Add(state,BuildKind.Transformer,10,10);var machine=Add(state,BuildKind.Smelter,14,10);machine.items[(int)ItemKind.Ore]=1;
            var sim=new WorldSimulator(state);Advance(sim,3);Near(Building(sim,high.id).powerGeneration,180,"HV-to-MV capped conversion");Near(Building(sim,low.id).powerGeneration,162,"second converter ignored upstream loss");
            Require(Building(sim,machine.id).items[(int)ItemKind.Iron]==1,"two-stage conversion did not run actual production");Require(sim.State.powerNetworks.Select(n=>n.tier).OrderBy(t=>t).SequenceEqual(new[] { 2,3,4 }),"converter chain collapsed voltage networks");
        });
        Run("transformer HV mode is itself gated and malicious configure arguments are rejected", () =>
        {
            var state=Flat(era:TechEra.MediumVoltage);var target=Add(state,BuildKind.Transformer,10,10);var sim=new WorldSimulator(state);var p=Builder(sim);p.x=8;p.y=10;var b=Building(sim,target.id);
            foreach(int arg in new[] { -1,1,2,90,int.MaxValue }){Ready(p);sim.Apply(Command(p,"configure",10,10,arg));Require(b.setting==0,"MV actor selected HV converter mode or invalid setting");}
            sim.State.era=TechEra.HighVoltage;Ready(p);sim.Apply(Command(p,"configure",10,10,1));Require(b.setting==1,"HV actor could not select HV-to-MV mode");
        });
        Run("reactor consumes nuclear fuel and coolant and produces tracked nuclear waste", () =>
        {
            var state=Flat(era:TechEra.Nuclear);var reactor=Add(state,BuildKind.Reactor,10,10);reactor.items[(int)ItemKind.NuclearFuel]=2;reactor.items[(int)ItemKind.Coolant]=10;
            var sim=new WorldSimulator(state);Advance(sim,2.1f);reactor=Building(sim,reactor.id);Near(reactor.powerGeneration,1200,"cooled reactor output");
            Require(reactor.items[(int)ItemKind.NuclearFuel]==1 && reactor.fuel>17 && reactor.items[(int)ItemKind.Coolant]<10,"reactor produced without consuming both resources");
            Require(reactor.items[(int)ItemKind.NuclearWaste]==1 && sim.State.produced[(int)ItemKind.NuclearWaste]==1,"nuclear waste was lost or not recorded");
            Require(float.IsFinite(reactor.temperature) && reactor.temperature>20,"operating reactor generated no heat");
        });
        Run("reactor stops on missing fuel missing coolant or a full nuclear-waste buffer", () =>
        {
            foreach(string missing in new[] { "fuel","coolant","waste room" })
            {
                var state=Flat(era:TechEra.Nuclear);var reactor=Add(state,BuildKind.Reactor,10,10);reactor.items[(int)ItemKind.NuclearFuel]=missing=="fuel"?0:2;reactor.items[(int)ItemKind.Coolant]=missing=="coolant"?0:10;reactor.items[(int)ItemKind.NuclearWaste]=missing=="waste room"?WorldSimulator.BufferLimit:0;
                var before=Copy(reactor.items);var sim=new WorldSimulator(state);Advance(sim,1);reactor=Building(sim,reactor.id);Near(reactor.powerGeneration,0,$"reactor ignored missing {missing}");Inventory(reactor.items,before,"blocked reactor consumed or created material");
                if(missing=="waste room"){reactor.items[(int)ItemKind.NuclearWaste]--;Advance(sim,.05f);Require(reactor.powerGeneration>0,"clearing waste capacity did not resume reactor");}
            }
        });
        Run("explicit hot reactor trips and recovers only below the thermal reset boundary", () =>
        {
            // Inject a valid hot save instead of requiring a properly cooled reactor to overheat.
            var state=Flat(era:TechEra.Nuclear);var reactor=Add(state,BuildKind.Reactor,10,10);reactor.temperature=920;reactor.items[(int)ItemKind.NuclearFuel]=2;reactor.items[(int)ItemKind.Coolant]=10;
            var before=Copy(reactor.items);var sim=new WorldSimulator(state);reactor=Building(sim,reactor.id);sim.Step(WorldSimulator.FixedStep);
            Require(reactor.tripped && reactor.temperature>=900,"explicit overheat did not trip the reactor");Near(reactor.powerGeneration,0,"overheated reactor still generated");Inventory(reactor.items,before,"tripped reactor consumed resources");
            reactor.temperature=301.1f;sim.Step(WorldSimulator.FixedStep);
            Require(reactor.tripped && reactor.temperature>300,"reactor reset above the 300-degree boundary");Near(reactor.powerGeneration,0,"latched reactor generated above reset temperature");Inventory(reactor.items,before,"latched reactor consumed resources");
            reactor.temperature=300;sim.Step(WorldSimulator.FixedStep);
            Require(!reactor.tripped && reactor.temperature<=300 && reactor.powerGeneration>0,"cooled reactor did not reset and resume");Require(reactor.items[(int)ItemKind.NuclearFuel]==1 && reactor.items[(int)ItemKind.Coolant]==9 && reactor.items[(int)ItemKind.NuclearWaste]==1,"thermal recovery did not resume real resource consumption");
        });
        Run("reactor sustains 120 seconds with adequate cooling then stops safely when coolant is cut", () =>
        {
            var state=Flat(era:TechEra.Nuclear);var reactor=Add(state,BuildKind.Reactor,10,10);reactor.items[(int)ItemKind.NuclearFuel]=10;reactor.items[(int)ItemKind.Coolant]=200;
            var sim=new WorldSimulator(state);reactor=Building(sim,reactor.id);double energy=0;
            for(int tick=0;tick<2400;tick++)
            {
                sim.Step(WorldSimulator.FixedStep);
                Require(!reactor.tripped && reactor.powerGeneration>0 && reactor.powerGeneration<=1200.001f,$"adequately cooled reactor interrupted at tick {tick+1}");
                Require(float.IsFinite(reactor.temperature) && reactor.temperature>=20 && reactor.temperature<900,$"adequately cooled reactor exceeded thermal limits at tick {tick+1}");
                Require(reactor.items[(int)ItemKind.NuclearFuel]+reactor.items[(int)ItemKind.NuclearWaste]==10,"sustained reactor lost nuclear fuel/waste conservation");
                energy+=reactor.powerGeneration*WorldSimulator.FixedStep;
            }
            Require(Math.Abs(energy-144000)<=2,$"120-second reactor energy budget differed: {energy}");Require(reactor.items[(int)ItemKind.Coolant]<100 && reactor.items[(int)ItemKind.NuclearWaste]>=6,"continuous output did not consume real fuel and coolant");
            reactor.items[(int)ItemKind.Coolant]=0;Advance(sim,1.25f);Near(reactor.powerGeneration,0,"reactor kept generating after its last coolant reserve expired");
            var stopped=Copy(reactor.items);float fuel=reactor.fuel,temperature=reactor.temperature;Advance(sim,2);
            Inventory(reactor.items,stopped,"uncoolable reactor kept consuming resources");Near(reactor.fuel,fuel,"shutdown consumed the burning fuel reserve");Require(reactor.temperature<=temperature,"uncoolable stopped reactor kept heating");
            reactor.items[(int)ItemKind.Coolant]=1;sim.Step(WorldSimulator.FixedStep);Require(!reactor.tripped && reactor.powerGeneration>0,"restoring coolant did not resume the safely stopped reactor");
        });
    }
}
