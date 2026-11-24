using DeadCircuit;

static partial class Checks
{
    static readonly (MachineModuleRole role,int x,int y,int rotation)[] ModularU = {
        (MachineModuleRole.Controller,0,0,0), (MachineModuleRole.Input,0,1,0),
        (MachineModuleRole.Energy,0,2,2), (MachineModuleRole.Maintenance,0,3,0),
        (MachineModuleRole.Casing,1,0,0), (MachineModuleRole.Process,2,0,0),
        (MachineModuleRole.Casing,3,0,0), (MachineModuleRole.Output,3,1,0),
        (MachineModuleRole.Exhaust,3,2,0), (MachineModuleRole.Casing,3,3,0)
    };
    static readonly (MachineModuleRole role,int x,int y,int rotation)[] ModularL = {
        (MachineModuleRole.Controller,0,0,0), (MachineModuleRole.Casing,1,0,0),
        (MachineModuleRole.Process,2,0,0), (MachineModuleRole.Casing,3,0,0),
        (MachineModuleRole.Maintenance,4,0,0), (MachineModuleRole.Input,0,1,0),
        (MachineModuleRole.Energy,0,2,0), (MachineModuleRole.Output,0,3,2), (MachineModuleRole.Exhaust,0,4,1)
    };
    static void HoldModularWrench(WorldSimulator sim,PlayerState p)
    {
        var wrench=p.gear.FirstOrDefault(g=>g.kind==GearKind.Wrench && g.durability>0);
        if(wrench==null) { wrench=new GearState {id=sim.State.nextId++,kind=GearKind.Wrench,durability=50}; p.gear.Add(wrench); }
        p.toolId=wrench.id; p.activeSlot=1;
    }
    static void ModularStand(WorldSimulator sim,PlayerState p,int x,int y)
    {
        foreach(var (dx,dy) in new[] {(-1,0),(0,-1),(1,0),(0,1),(-1,-1),(1,1),(-1,1),(1,-1)})
        {
            if(sim.IsBlocked(x+dx,y+dy)) continue;
            p.x=x+dx; p.y=y+dy; return;
        }
        throw new Exception("fixture module has no external approach");
    }
    static void PlanModule(WorldSimulator sim,PlayerState p,BuildingState b,MachineModuleRole role,int x,int y,int rotation=0)
    {
        ModularStand(sim,p,x,y); Advance(sim,.3f);
        sim.Apply(new PlayerCommand {playerId=p.id,action="moduleplan",buildingId=b.id,arg=(int)role,x=x,y=y,rotation=rotation});
    }
    static WorldSimulator ModularFixture(out PlayerState p,BuildKind kind=BuildKind.LargeOreProcessor,bool lShape=false,int rotation=0,TechEra era=TechEra.LowVoltage)
    {
        var sim=new WorldSimulator(Flat(era:era)); p=sim.AddPlayer("p", "P"); Array.Clear(p.items); HoldModularWrench(sim,p); p.x=9;p.y=10;
        sim.Apply(Command(p,"build",10,10,(int)kind)); var b=sim.State.buildings.Single();
        foreach(var cell in (lShape ? ModularL : ModularU).Skip(1))
        {
            int x=cell.x,y=cell.y;
            for(int r=0;r<rotation;r++) { int old=x;x=-y;y=old; }
            PlanModule(sim,p,b,cell.role,10+x,10+y,(cell.rotation+rotation)&3);
        }
        return sim;
    }
    static void SupplyModularMaterials(PlayerState p, int[] required)
    {
        for (int i = 0; i < required.Length; i++) p.items[i] = Math.Max(p.items[i], required[i]);
        Require(InventoryWeight.CarriedGrams(p) <= InventoryWeight.CapacityGrams(p), "modular fixture supplies exceed backpack capacity");
    }
    static void CompleteModular(WorldSimulator sim,PlayerState p,BuildingState b)
    {
        SupplyModularMaterials(p, ModularMachineCatalog.RequiredItems(b));
        for(int i=0;i<ModularMachineCatalog.ModuleCount(b);i++)
        {
            int x=ModularMachineCatalog.ModuleX(b,i),y=ModularMachineCatalog.ModuleY(b,i); ModularStand(sim,p,x,y); Advance(sim,.3f);
            sim.Apply(Command(p,"interact",x,y));
        }
        Advance(sim,.3f); Require(ModularMachineCatalog.IsComplete(b),"free shape failed to form: "+ModularMachineCatalog.Status(sim.State,b));
    }
    static BuildingState ModularPower(WorldSimulator sim,BuildingState b)
    {
        int x=ModularMachineCatalog.EnergyInputX(b),y=ModularMachineCatalog.EnergyInputY(b);
        if(b.kind==BuildKind.LargeSteamFurnace)
        {
            // The U fixture's inner energy pipe joins an existing, shoreline small boiler.
            Add(sim.State,BuildKind.SteamPipe,x,y); Add(sim.State,BuildKind.SteamPipe,x,y+1);
            var boiler=Add(sim.State,BuildKind.Boiler,x,y+2); Water(sim.State,x-1,y+2);
            boiler.items[(int)ItemKind.Fuel]=20; return boiler;
        }
        var pole=Add(sim.State,BuildKind.PowerPole,x,y);
        var generator=Add(sim.State,BuildKind.Generator,7,7); generator.items[(int)ItemKind.Fuel]=20; return generator;
    }
    static void EraseModule(WorldSimulator sim,PlayerState p,BuildingState b,int index)
    {
        int x=ModularMachineCatalog.ModuleX(b,index),y=ModularMachineCatalog.ModuleY(b,index); ModularStand(sim,p,x,y); Advance(sim,.3f);
        sim.Apply(new PlayerCommand {playerId=p.id,action="moduleerase",buildingId=b.id,x=x,y=y});
    }
    static WorldSimulator ModularSparse(out PlayerState p,int bx=-17,int by=-17)
    {
        var s=Flat();s.generatorVersion=WorldGeneration.CurrentGeneratorVersion;s.generation.chunked=true;s.chunks.enabled=true;
        for(int y=-6;y<4;y++) for(int x=-8;x<4;x++) s.chunks.records.Add(new WorldChunkState {x=x,y=y,ground=new string('0',256),biomes=new string('0',256)});
        var sim=new WorldSimulator(s);p=sim.AddPlayer("p", "P");Array.Clear(p.items);HoldModularWrench(sim,p);p.x=bx-1;p.y=by;
        sim.Apply(Command(p,"build",bx,by,(int)BuildKind.LargeOreProcessor));var b=sim.State.buildings.Single();
        foreach(var m in ModularU.Skip(1)) PlanModule(sim,p,b,m.role,bx+m.x,by+m.y,m.rotation);
        return sim;
    }
    static void ModularMachineChecks()
    {
        Run("modular free blueprint needs a held wrench and reserves only the chosen controller cell",()=>
        {
            var sim=new WorldSimulator(Flat()); var p=Builder(sim); p.x=9;p.y=10;
            var wrench=new GearState {id=sim.State.nextId++,kind=GearKind.Wrench,durability=50}; p.gear.Add(wrench);
            sim.Apply(Command(p,"build",10,10,63)); Require(sim.State.buildings.Count==0,"owned unheld wrench placed blueprint");
            Advance(sim,.3f); p.toolId=wrench.id;p.activeSlot=1;
            var before=Copy(p.items); sim.Apply(Command(p,"build",10,10,63));
            Require(sim.State.buildings.Count==1,"held wrench could not start controller"); var b=sim.State.buildings.Single();
            Require(sim.IsBlocked(10,10) && !sim.IsBlocked(11,10) && DemoCatalog.Width(b)==1 && DemoCatalog.Height(b)==1,"controller pre-reserved template cells");
            Require(p.items.SequenceEqual(before) && b.modularModules.Count==1 && b.modularModules[0].role==MachineModuleRole.Controller,"planning charged materials or created hidden modules");
        });
        Run("modular free L and U shapes support independent rotated ports extra casing and true holes",()=>
        {
            foreach(bool l in new[] {false,true}) for(int rotation=0;rotation<4;rotation++)
            {
                var sim=ModularFixture(out var p,lShape:l,rotation:rotation); var b=sim.State.buildings.Single();
                Require(b.modularModules.Count==(l?9:10),"free plan forced fixed module count"); CompleteModular(sim,p,b);
                foreach(var m in b.modularModules) Require(ModularMachineCatalog.ModuleAt(b,m.x,m.y)>=0,"sparse module mapping lost cell");
                Require(ModularMachineCatalog.FormationIssue(b)=="","legal arbitrary shape rejected");
                _=new WorldSimulator(Saved(sim.State));
            }
            var world=ModularFixture(out var actor); var machine=world.State.buildings.Single(); CompleteModular(world,actor,machine);
            Require(!world.IsBlocked(12,12) && !DemoCatalog.Contains(machine,12,12),"U gap reserved or blocked");
            actor.x=11;actor.y=12; world.Apply(Command(actor,"move",0,0));
            world.Apply(new PlayerCommand {playerId=actor.id,action="move",moveX=1}); Advance(world,.25f); Require(actor.x>11.5f,"player could not walk through U hole");
            SupplyModularMaterials(actor, DemoCatalog.BuildCost(BuildKind.Chest));
            world.Apply(Command(actor,"move")); actor.x=11;actor.y=13; actor.interactCooldown=0; BuildNow(world, actor, 12, 12, (int)BuildKind.Chest);
            Require(world.State.buildings.Any(v=>v.kind==BuildKind.Chest && v.x==12 && v.y==12),"U hole could not contain another building");
            _=new WorldSimulator(Saved(world.State));
            PlanModule(world,actor,machine,MachineModuleRole.Casing,14,13); Require(machine.modularModules.Count==11 && !ModularMachineCatalog.IsComplete(machine),"extra free casing not planned");
        });
        Run("modular module planning rejects range walls closed doors broken unheld tools limits and duplicate roles",()=>
        {
            var sim=ModularFixture(out var p); var b=sim.State.buildings.Single(); int count=b.modularModules.Count;
            p.activeSlot=0; PlanModule(sim,p,b,MachineModuleRole.Casing,14,13); Require(b.modularModules.Count==count,"unheld wrench edited plan");
            p.activeSlot=1; p.gear.Single(g=>g.id==p.toolId).durability=0; PlanModule(sim,p,b,MachineModuleRole.Casing,14,13); Require(b.modularModules.Count==count,"broken wrench edited plan");
            p.gear.Single(g=>g.id==p.toolId).durability=1;
            PlanModule(sim,p,b,MachineModuleRole.Process,14,13); Require(b.modularModules.Count==count,"duplicate process accepted");
            PlanModule(sim,p,b,MachineModuleRole.Casing,22,13); Require(b.modularModules.Count==count,"13-cell span accepted");
            p.x=5;p.y=5; Advance(sim,.3f);sim.Apply(new PlayerCommand {playerId=p.id,action="moduleplan",buildingId=b.id,arg=0,x=14,y=13}); Require(b.modularModules.Count==count,"remote plan accepted");
            var door=Add(sim.State,BuildKind.Door,14,14); p.x=14;p.y=15;Advance(sim,.3f);
            sim.Apply(new PlayerCommand {playerId=p.id,action="moduleplan",buildingId=b.id,arg=0,x=14,y=13}); Require(b.modularModules.Count==count,"closed door bypassed LOS");
            Advance(sim,.3f); sim.Apply(Command(p,"interact",14,14)); Advance(sim,.3f);
            sim.Apply(new PlayerCommand {playerId=p.id,action="moduleplan",buildingId=b.id,arg=0,x=14,y=13}); Require(b.modularModules.Count==count+1,"open door did not permit visible plan");
            door.kind=BuildKind.Wall;door.setting=0;p.x=15;p.y=14;Advance(sim,.3f);
            sim.Apply(new PlayerCommand {playerId=p.id,action="moduleplan",buildingId=b.id,arg=0,x=13,y=14});Require(b.modularModules.Count==count+1,"wall bypassed LOS");
        });
        Run("modular teammates pay partial module costs once and cancel with detached exact refunds",()=>
        {
            var sim=ModularFixture(out var p);var b=sim.State.buildings.Single();int module=4;
            Array.Clear(p.items);p.items[(int)ItemKind.SteelPlate]=1;ModularStand(sim,p,11,10);Advance(sim,.3f);sim.Apply(Command(p,"interact",11,10));
            Require(b.modularModules[module].paid[(int)ItemKind.SteelPlate]==1 && p.items[(int)ItemKind.SteelPlate]==0,"partial payment not charged");
            var other=sim.AddPlayer("other","Other");
            other.items = DemoCatalog.Materials(ItemKind.SteelPlate, 2, ItemKind.RefractoryBrick, 3, ItemKind.Wood, 1);
            var beforePayment = Copy(other.items);
            HoldModularWrench(sim,other);ModularStand(sim,other,11,10);sim.Apply(Command(other,"interact",11,10));
            Require(ModularMachineCatalog.ModuleComplete(b,module), "teammate could not finish the partially paid module");
            Paid(other.items, beforePayment, DemoCatalog.Materials(ItemKind.SteelPlate, 1, ItemKind.RefractoryBrick, 2), "teammate paid more than the remainder");
            Advance(sim,.3f);var before=Copy(other.items);sim.Apply(Command(other,"interact",11,10));Require(other.items.SequenceEqual(before),"duplicate payment charged");
            var saved=Saved(sim.State); var restored=new WorldSimulator(saved);var copy=Building(restored,b.id);
            Require(copy.modularModules[module].paid.SequenceEqual(b.modularModules[module].paid),"save lost payment");
            saved.buildings[0].modularModules[module].paid[(int)ItemKind.SteelPlate]=0;Require(copy.modularModules[module].paid[(int)ItemKind.SteelPlate]==2,"restore aliases module payment");
            var actor=restored.State.players.Single(v=>v.id==p.id);
            Array.Clear(actor.items);
            var refund = DemoCatalog.Materials(ItemKind.SteelPlate, 2, ItemKind.RefractoryBrick, 2);
            FillBackpackLeaving(actor, (int)InventoryWeight.MaterialsGrams(refund));
            var expectedRefund = actor.items.Zip(refund, (held, returned) => held + returned).ToArray();
            EraseModule(restored,actor,copy,module);
            Require(actor.items.SequenceEqual(expectedRefund) && copy.modularModules.Count==9 &&
                InventoryWeight.CarriedGrams(actor)==InventoryWeight.CapacityGrams(actor),"single-cell erase did not refund exact real payment");
            EraseModule(restored,actor,copy,0);Require(restored.State.buildings.Contains(copy),"controller erased while other modules remained");
        });
        Run("modular U energy terminal inside bbox powers ore batching with maintenance and byproduct blockage",()=>
        {
            var sim=ModularFixture(out var p);var b=sim.State.buildings.Single();CompleteModular(sim,p,b);ModularPower(sim,b);
            b.items[(int)ItemKind.Ore]=2;Advance(sim,1);Require(b.progress==0 && b.powerDemand==0 && ModularMachineCatalog.Status(sim.State,b).Contains("缺维护"),"missing maintenance requested energy");
            b.items[(int)ItemKind.Bearing]=1;b.items[(int)ItemKind.Stone]=200;Advance(sim,1);
            Require(b.progress==0 && b.items[(int)ItemKind.Ore]==2 && b.items[(int)ItemKind.Bearing]==1 && b.powerDemand==0,"blocked byproduct spent materials");
            Require(ModularMachineCatalog.Status(sim.State,b).Contains("输出阻塞"),"byproduct block status missing");
            b.items[(int)ItemKind.Stone]=0;Advance(sim,1.9f);Near(b.powerDemand,50,"large crusher rated load");
            Require(b.items[(int)ItemKind.Ore]==2 && b.items[(int)ItemKind.Bearing]==1,"incomplete batch charged inputs or service");
            Advance(sim,.1f);Require(b.items[(int)ItemKind.Ore]==0 && b.items[(int)ItemKind.CrushedOre]==4 && b.items[(int)ItemKind.Stone]==1 && b.modularService==7,"ore full batch failed");
            Advance(sim,.1f);Require(b.powerDemand==0 && ModularMachineCatalog.Status(sim.State,b).Contains("待输入"),"empty input kept energy demand");
        });
        Run("modular assembly needs additional bearing for maintenance and damage stops until paid held wrench repair",()=>
        {
            var sim=ModularFixture(out var p,BuildKind.LargeAssemblyPlant);var b=sim.State.buildings.Single();CompleteModular(sim,p,b);
            ModularStand(sim,p,10,10);sim.Apply(Command(p,"recipe",10,10,211));Advance(sim,.3f);ModularPower(sim,b);
            var r=TechCatalog.Recipe(211);b.items=Copy(r.inputs);Advance(sim,3.1f);
            Require(b.progress==0 && b.items[(int)ItemKind.Bearing]==2 && b.powerDemand==0,"maintenance stole recipe's two bearings");
            b.items[(int)ItemKind.Bearing]=3;Advance(sim,1);float progress=b.progress;b.hp-=1;Advance(sim,2.1f);
            Near(b.progress,progress,"damage advanced production");Require(b.items[(int)ItemKind.Bearing]==3 && b.powerDemand==0,"damaged machine paid inputs");
            SupplyModularMaterials(p, DemoCatalog.Materials(ItemKind.SteelPlate, 1));
            p.activeSlot=0;ModularStand(sim,p,10,10);sim.Apply(Command(p,"repair",10,10));Require(b.hp<WorldSimulator.BuildingMaxHealth(b.kind),"unheld wrench repaired machine");
            Advance(sim,.3f);p.activeSlot=1;int plates=p.items[(int)ItemKind.SteelPlate];sim.Apply(Command(p,"repair",10,10));Require(p.items[(int)ItemKind.SteelPlate]==plates-1 && b.hp==WorldSimulator.BuildingMaxHealth(b.kind),"repair failed real payment");
            Advance(sim,2);Require(b.items[(int)ItemKind.Motor]==2 && b.items[(int)ItemKind.Bearing]==0 && b.modularService==7,"assembly failed resume or exact maintenance input combination");
        });
        Run("modular controller unload atomically returns raw maintenance and unfinished batch then permits editing",()=>
        {
            var sim=ModularFixture(out var p);var b=sim.State.buildings.Single();CompleteModular(sim,p,b);ModularPower(sim,b);
            b.items[(int)ItemKind.Ore]=2;b.items[(int)ItemKind.Bearing]=1;Advance(sim,1);Require(b.progress>0,"partial batch fixture did not start");
            Array.Clear(p.items);ModularStand(sim,p,10,10);sim.Apply(new PlayerCommand {playerId=p.id,action="moduleunload",buildingId=b.id,x=10,y=10});
            Require(b.items.All(n=>n==0) && b.progress==0 && p.items[(int)ItemKind.Ore]==2 && p.items[(int)ItemKind.Bearing]==1,"controller could not cancel unfinished batch and return inputs");
            PlanModule(sim,p,b,MachineModuleRole.Casing,14,13);Require(b.modularModules.Count==11,"unloaded machine stayed uneditable");
        });
        Run("modular independently oriented ports deliver only through real input output cells with one flight per source",()=>
        {
            for(int rotation=0;rotation<4;rotation++)
            {
                var sim=ModularFixture(out var p,lShape:true,rotation:rotation);var b=sim.State.buildings.Single();CompleteModular(sim,p,b);
                int input=ModularMachineCatalog.RoleIndex(b,MachineModuleRole.Input),direction=ModularMachineCatalog.ModuleRotation(b,input);
                int ix=ModularMachineCatalog.ModuleX(b,input),iy=ModularMachineCatalog.ModuleY(b,input);
                var from=Add(sim.State,BuildKind.Chest,ix-DemoCatalog.DirectionX(direction),iy-DemoCatalog.DirectionY(direction),direction);from.items[(int)ItemKind.Ore]=1;
                var to=Add(sim.State,BuildKind.Chest,ModularMachineCatalog.OutputX(b),ModularMachineCatalog.OutputY(b));
                b.items[(int)ItemKind.CrushedOre]=4;b.items[(int)ItemKind.Stone]=1;
                var expected=ItemTotals(sim.State);Advance(sim,.05f);Require(sim.State.cargo.Any(c=>c.toId==b.id),"rotated input failed dispatch");
                var restored=new WorldSimulator(Saved(sim.State));
                for(int n=0;n<70;n++)
                {
                    sim.Step(WorldSimulator.FixedStep);restored.Step(WorldSimulator.FixedStep);Conserved(sim.State,expected);
                    Require(sim.State.cargo.Count(c=>c.fromId==b.id)<=1,"machine dispatched concurrent output flights");
                }
                Require(b.items[(int)ItemKind.Ore]==1 && Building(sim,to.id).items[(int)ItemKind.CrushedOre]==4 && Building(sim,to.id).items[(int)ItemKind.Stone]==1,"port delivery lost input or byproduct");
                Require(ItemTotals(restored.State).SequenceEqual(expected) && Building(restored,to.id).items.SequenceEqual(Building(sim,to.id).items),"midflight restore diverged");
            }
            var wrong=ModularFixture(out var actor);var machine=wrong.State.buildings.Single();CompleteModular(wrong,actor,machine);
            var casingSource=Add(wrong.State,BuildKind.Chest,11,9,1);casingSource.items[(int)ItemKind.Ore]=1;
            Advance(wrong,1);Require(casingSource.items[(int)ItemKind.Ore]==1 && machine.items[(int)ItemKind.Ore]==0 && wrong.State.cargo.Count==0,"cargo entered casing instead of input");
        });
        Run("modular unload and edits refuse full inventory active cargo and noncontroller without partial mutation",()=>
        {
            var sim=ModularFixture(out var p);var b=sim.State.buildings.Single();CompleteModular(sim,p,b);b.items[(int)ItemKind.Ore]=2;b.items[(int)ItemKind.Bearing]=1;b.progress=.5f;
            FillBackpackLeaving(p, 0);
            ModularStand(sim,p,10,10);var before=Copy(b.items);var beforeBackpack=Copy(p.items);
            sim.Apply(new PlayerCommand {playerId=p.id,action="moduleunload",buildingId=b.id,x=10,y=10});
            Require(b.items.SequenceEqual(before) && b.progress==.5f && p.items.SequenceEqual(beforeBackpack),"full backpack unload partially mutated");
            int count=b.modularModules.Count;PlanModule(sim,p,b,MachineModuleRole.Casing,14,13);Require(b.modularModules.Count==count,"live inventory plan accepted");
            EraseModule(sim,p,b,4);Require(b.modularModules.Count==count,"live inventory erase accepted");
            Array.Clear(p.items);ModularStand(sim,p,10,11);Advance(sim,.3f);sim.Apply(new PlayerCommand {playerId=p.id,action="moduleunload",buildingId=b.id,x=10,y=11});
            Require(b.items.SequenceEqual(before) && b.progress==.5f,"input module performed controller unload");
            ModularStand(sim,p,10,10);Advance(sim,.3f);sim.Apply(new PlayerCommand {playerId=p.id,action="moduleunload",buildingId=b.id,x=10,y=10});
            var source=Add(sim.State,BuildKind.Chest,9,11);source.items[(int)ItemKind.Ore]=1;Advance(sim,.05f);Require(sim.State.cargo.Count==1,"cargo unload fixture not in flight");
            b.progress=0;Array.Clear(b.items);ModularStand(sim,p,10,10);p.interactCooldown=0;
            sim.Apply(new PlayerCommand {playerId=p.id,action="moduleunload",buildingId=b.id,x=10,y=10});Require(sim.State.cargo.Count==1,"unload swallowed active cargo");
            PlanModule(sim,p,b,MachineModuleRole.Casing,14,13);Require(b.modularModules.Count==count,"inflight edit accepted");
        });
        Run("modular entire cancellation refunds paid modules inventory and flights exactly or rejects atomically",()=>
        {
            var sim=ModularFixture(out var p);var b=sim.State.buildings.Single();CompleteModular(sim,p,b);
            var source=Add(sim.State,BuildKind.Chest,9,11);source.items[(int)ItemKind.Ore]=1;
            Add(sim.State,BuildKind.Chest,14,11);b.items[(int)ItemKind.CrushedOre]=1;Advance(sim,.05f);
            Require(sim.State.cargo.Count==2,"cancellation fixture requires incoming and outgoing flight");
            var expected=new int[DemoCatalog.ItemCount];foreach(var m in b.modularModules) for(int i=0;i<expected.Length;i++) expected[i]+=m.paid[i];
            for(int i=0;i<expected.Length;i++) expected[i]+=b.items[i];foreach(var cargo in sim.State.cargo) if(cargo.fromId==b.id || cargo.toId==b.id) expected[cargo.item]++;
            Array.Clear(p.items);FillBackpackLeaving(p, 0);ModularStand(sim,p,10,10);p.interactCooldown=0;
            var beforeBackpack = Copy(p.items);
            var beforeMachine = Copy(b.items);
            var beforePayments = b.modularModules.SelectMany(m => m.paid).ToArray();
            var beforeFlights = sim.State.cargo.Select(c => c.id).ToArray();
            sim.Apply(Command(p,"dismantle",10,10));
            Require(sim.State.buildings.Contains(b) && sim.State.cargo.Select(c => c.id).SequenceEqual(beforeFlights) &&
                p.items.SequenceEqual(beforeBackpack) && b.items.SequenceEqual(beforeMachine) &&
                b.modularModules.SelectMany(m => m.paid).SequenceEqual(beforePayments), "capacity refusal partially dismantled");
            Array.Clear(p.items);FillBackpackLeaving(p, (int)InventoryWeight.MaterialsGrams(expected));
            var expectedBackpack = p.items.Zip(expected, (held, returned) => held + returned).ToArray();
            p.interactCooldown=0;sim.Apply(Command(p,"dismantle",10,10));
            Require(!sim.State.buildings.Contains(b) && sim.State.cargo.Count==0 && p.items.SequenceEqual(expectedBackpack) &&
                InventoryWeight.CarriedGrams(p)==InventoryWeight.CapacityGrams(p),"whole cancellation failed exact payment inventory flight refund");
        });
        Run("modular signed cross chunk plans save restore and snapshots include intersecting cells with detached payments",()=>
        {
            var sim=ModularSparse(out var p);var b=sim.State.buildings.Single();CompleteModular(sim,p,b);
            Require(b.modularModules.Select(m=>(WorldTiles.FloorDiv(m.x,16),WorldTiles.FloorDiv(m.y,16))).Distinct().Count()==4,"negative plan did not cross four chunks");
            _=new WorldSimulator(Saved(sim.State));Require(!sim.IsBlocked(-15,-15),"negative U hole blocked");
            var edge=ModularSparse(out var local,-65,10);var machine=edge.State.buildings.Single();CompleteModular(edge,local,machine);local.x=0;local.y=10;
            var snapshot=WorldSnapshot.ForPlayer(edge.State,local.id);Require(snapshot.buildings.Any(v=>v.id==machine.id),"snapshot dropped machine anchored outside subscription despite visible module");
            var copy=snapshot.buildings.Single(v=>v.id==machine.id);copy.modularModules[0].paid[(int)ItemKind.Circuit]=0;
            Require(machine.modularModules[0].paid[(int)ItemKind.Circuit]==2,"snapshot payment array aliases authority");
        });
        Run("modular current restore rejects malformed roles geometry costs reserve progress and unfinished inventories",()=>
        {
            var sim=ModularFixture(out var p);var b=sim.State.buildings.Single();CompleteModular(sim,p,b);var baseline=Saved(sim.State);
            foreach(var corrupt in new Action<WorldState>[] {
                s=>s.buildings[0].modularModules=null,
                s=>s.buildings[0].modularModules[0].paid=null,
                s=>s.buildings[0].modularModules[0].paid=new int[1],
                s=>s.buildings[0].modularModules[0].paid[0]=-1,
                s=>s.buildings[0].modularModules[0].paid[0]=1,
                s=>s.buildings[0].modularModules[0].rotation=4,
                s=>s.buildings[0].modularModules[0].role=(MachineModuleRole)99,
                s=>s.buildings[0].modularModules[1].role=MachineModuleRole.Controller,
                s=>s.buildings[0].modularModules[1].x+=100,
                s=>s.buildings[0].x++,
                s=>s.buildings[0].modularService=8,
                s=>s.buildings[0].progress=1,
                s=>{s.buildings[0].modularModules[0].paid[(int)ItemKind.Circuit]=0;s.buildings[0].items[(int)ItemKind.Ore]=1;},
                s=>s.version=WorldSimulator.SaveVersion-1
            }) { var forged=Saved(baseline);corrupt(forged);Reject(forged,"malformed modular save accepted"); }
            var resumed=new WorldSimulator(baseline);baseline.buildings[0].modularModules.Clear();Require(resumed.State.buildings[0].modularModules.Count==10,"restored module list aliases source");
        });
        Run("modular public plans enforce thirty two cells allow disconnected sketches and expose inward port issues",()=>
        {
            var sim=new WorldSimulator(Flat());var p=Builder(sim);HoldModularWrench(sim,p);p.x=9;p.y=10;sim.Apply(Command(p,"build",10,10,65));var b=sim.State.buildings.Single();
            for(int n=1;n<32;n++) PlanModule(sim,p,b,MachineModuleRole.Casing,10+n%6,10+n/6);
            Require(b.modularModules.Count==32,"legal 32-cell sketch failed");var before=Copy(p.items);
            PlanModule(sim,p,b,MachineModuleRole.Casing,12,15);Require(b.modularModules.Count==32 && p.items.SequenceEqual(before),"33rd cell accepted or charged");
            _=new WorldSimulator(Saved(sim.State));
            var shape=ModularFixture(out var actor);var machine=shape.State.buildings.Single();CompleteModular(shape,actor,machine);
            PlanModule(shape,actor,machine,MachineModuleRole.Casing,15,14);Require(machine.modularModules.Count==11 && ModularMachineCatalog.FormationIssue(machine).Contains("断连"),"disconnected sketch was forced contiguous or shown valid");
            var gap=machine.modularModules.Last();
            SupplyModularMaterials(actor, ModularMachineCatalog.ModuleCost(machine.kind, gap.role));
            ModularStand(shape,actor,gap.x,gap.y);Advance(shape,.3f);shape.Apply(Command(actor,"interact",gap.x,gap.y));
            Require(ModularMachineCatalog.ModuleComplete(machine, machine.modularModules.Count-1) &&
                !ModularMachineCatalog.IsComplete(machine) && machine.powerDemand==0,"paid disconnected sketch became operational or was not fully paid");
            EraseModule(shape,actor,machine,10);Require(ModularMachineCatalog.IsComplete(machine),"erase did not restore formation");
            int output=ModularMachineCatalog.RoleIndex(machine,MachineModuleRole.Output);var m=machine.modularModules[output];int x=m.x,y=m.y;EraseModule(shape,actor,machine,output);
            PlanModule(shape,actor,machine,MachineModuleRole.Output,x,y,3);Require(ModularMachineCatalog.FormationIssue(machine).Contains("接口朝内"),"inward-facing output accepted as formed");
        });
        Run("modular primary constructs a process module from outside without walking into paid structure",()=>
        {
            var sim=ModularFixture(out var p);var b=sim.State.buildings.Single();int process=ModularMachineCatalog.RoleIndex(b,MachineModuleRole.Process);
            SupplyModularMaterials(p, ModularMachineCatalog.RequiredItems(b));
            for(int i=0;i<b.modularModules.Count;i++) if(i!=process)
            {
                var m=b.modularModules[i];ModularStand(sim,p,m.x,m.y);Advance(sim,.3f);sim.Apply(Command(p,"interact",m.x,m.y));
                Require(ModularMachineCatalog.ModuleComplete(b,i), "could not build a module before approaching the process cell");
            }
            p.x=5;p.y=12;Advance(sim,.3f);sim.Apply(Command(p,"primary",12,10));
            for(int i=0;i<120 && !ModularMachineCatalog.ModuleComplete(b,process);i++)
            { sim.Step(WorldSimulator.FixedStep);Require(!DemoCatalog.Contains(b,(int)Math.Floor(p.x+.5f),(int)Math.Floor(p.y+.5f)),"primary entered reserved structure"); }
            Require(ModularMachineCatalog.IsComplete(b),"primary could not reach real process cell");
        });
        Run("modular real upstairs controller respects unknown floor edges survives save and stays floor local",()=>
        {
            var sim=FloorFixture(out var root,out var p);FloorInteract(sim,p,FloorStair(root,'<'));var w=sim.State;var evidence=Flat();
            w.era=TechEra.LowVoltage;w.primitiveVersion=w.primitiveResearch=w.waterVersion=w.waterResearch=0;
            w.steamResearch=evidence.steamResearch;w.steamCoked=evidence.steamCoked;w.steamRefractory=evidence.steamRefractory;w.steamPigIron=evidence.steamPigIron;
            w.steamSteel=evidence.steamSteel;w.steamPlates=evidence.steamPlates;w.steamFittings=evidence.steamFittings;w.steamDriven=evidence.steamDriven;
            w.lvResearch=evidence.lvResearch;w.lvWire=evidence.lvWire;w.lvInsulators=evidence.lvInsulators;w.lvCable=evidence.lvCable;w.lvCoils=evidence.lvCoils;
            w.lvMotors=evidence.lvMotors;w.lvRelays=evidence.lvRelays;w.lvCircuits=evidence.lvCircuits;w.lvElectric=evidence.lvElectric;w.lvAutomated=evidence.lvAutomated;w.lvSorted=evidence.lvSorted;w.lvProtected=evidence.lvProtected;
            HoldModularWrench(sim,p);
            var floor=w.sites.Single(s=>s.parentSiteId==root.id && s.level==1);
            var cell=FloorCells(floor).First(c=>WorldTiles.Ground(w,c.x,c.y)=='4' && !FloorWorld.IsProtectedCell(w,c.x,c.y) && !sim.IsBlocked(c.x,c.y) &&
                FloorNeighbors(c).Any(n=>!sim.IsBlocked(n.x,n.y)));
            ModularStand(sim,p,cell.x,cell.y);p.interactCooldown=0;sim.Apply(Command(p,"build",cell.x,cell.y,65));
            var machine=w.buildings.Single(b=>b.kind==BuildKind.LargeOreProcessor);Require(machine.modularModules.Count==1,"upstairs controller not placed");
            var restored=new WorldSimulator(Saved(w));Require(Building(restored,machine.id).modularModules[0].x==cell.x && FloorWorld.IsInterior(restored.State,cell.x,cell.y),"upstairs save changed real coordinates");
            int count=machine.modularModules.Count;sim.Apply(new PlayerCommand {playerId=p.id,action="moduleplan",buildingId=machine.id,arg=0,x=root.x+1,y=root.y+1});Require(machine.modularModules.Count==count,"plan jumped between floors");
            var edge=FloorCells(floor).FirstOrDefault(c=>!sim.IsBlocked(c.x,c.y) && FloorNeighbors(c).Any(n=>WorldTiles.Ground(w,n.x,n.y)=='?'));
            if(edge!=(0,0))
            { var unknown=FloorNeighbors(edge).First(n=>WorldTiles.Ground(w,n.x,n.y)=='?');p.x=edge.x;p.y=edge.y;p.interactCooldown=0;sim.Apply(new PlayerCommand {playerId=p.id,action="moduleplan",buildingId=machine.id,arg=0,x=unknown.x,y=unknown.y});Require(machine.modularModules.Count==count,"plan entered unknown floor void"); }
        });
        Run("modular completed intact steam furnace substitutes only the LV facility requirement",()=>
        {
            var sim=ModularFixture(out var p,BuildKind.LargeSteamFurnace,era:TechEra.Steam);var b=sim.State.buildings.Single();
            var researchCost = TechCatalog.ResearchCost(TechEra.LowVoltage);
            var constructionCost = ModularMachineCatalog.RequiredItems(b);
            SupplyModularMaterials(p, researchCost.Zip(constructionCost, (research, construction) => research + construction).ToArray());
            Add(sim.State,BuildKind.ResearchBench,14,15);
            Require(!TechCatalog.CanResearch(sim.State,p,TechEra.LowVoltage),"unfinished blueprint impersonated facility");
            CompleteModular(sim,p,b);p.x=14;p.y=14;Require(TechCatalog.CanResearch(sim.State,p,TechEra.LowVoltage),"completed large furnace could not substitute small furnace facility: "+TechCatalog.ResearchRequirement(sim.State,p,TechEra.LowVoltage));
            b.hp-=1;Require(!TechCatalog.CanResearch(sim.State,p,TechEra.LowVoltage),"damaged large furnace impersonated intact facility");b.hp=WorldSimulator.BuildingMaxHealth(b.kind);
            PlanModule(sim,p,b,MachineModuleRole.Casing,15,14);p.x=14;p.y=14;Require(!TechCatalog.CanResearch(sim.State,p,TechEra.LowVoltage),"disconnected drawing impersonated facility");EraseModule(sim,p,b,b.modularModules.Count-1);p.x=14;p.y=14;
            sim.State.steamDriven=3;Require(!TechCatalog.CanResearch(sim.State,p,TechEra.LowVoltage),"large facility bypassed genuine production evidence");sim.State.steamDriven=4;
            var before=Copy(p.items);p.interactCooldown=0;sim.Apply(Command(p,"research",arg:(int)TechEra.LowVoltage));
            Require(sim.State.era==TechEra.LowVoltage,"valid large steam factory could not unlock LV");Paid(p.items,before,TechCatalog.ResearchCost(TechEra.LowVoltage),"large facility LV research");
        });
        Run("modular steam exhaust blocks actual operation without inputs or service loss and resumes",()=>
        {
            var sim=ModularFixture(out var p,BuildKind.LargeSteamFurnace);var b=sim.State.buildings.Single();CompleteModular(sim,p,b);ModularPower(sim,b);
            b.items[(int)ItemKind.PigIron]=6;b.items[(int)ItemKind.Limestone]=2;b.items[(int)ItemKind.Bronze]=1;
            var wall=Add(sim.State,BuildKind.Wall,ModularMachineCatalog.ExhaustX(b),ModularMachineCatalog.ExhaustY(b));Advance(sim,4.1f);
            Require(b.progress==0 && b.powerDemand==0 && b.items[(int)ItemKind.PigIron]==6 && b.items[(int)ItemKind.Bronze]==1,"blocked steam exhaust consumed resources");
            Require(ModularMachineCatalog.Status(sim.State,b).Contains("排气阻塞"),"missing blocked exhaust status");
            sim.State.buildings.Remove(wall);Advance(sim,4);Require(b.items[(int)ItemKind.Steel]==6 && b.modularService==7,"steam unblocked machine failed batch");
        });
    }
}
