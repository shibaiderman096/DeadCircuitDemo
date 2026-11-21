using DeadCircuit;

static partial class Checks
{
    static readonly BuildKind[] ResearchMachines = { BuildKind.Workbench, BuildKind.MechanicalCrusher, BuildKind.SteamFurnace, BuildKind.CircuitAssembler, BuildKind.ChemicalPlant, BuildKind.PrecisionAssembler };
    static readonly BuildKind[] NewlyUnlocked = { BuildKind.Shaft, BuildKind.CokeOven, BuildKind.PowerPole, BuildKind.MediumPole, BuildKind.SiliconRefiner, BuildKind.NuclearProcessor };
    static void Water(WorldState state, int x, int y)
    {
        var ground=state.ground.ToCharArray(); ground[x+y*state.width]='2'; state.ground=new string(ground);
    }
    static int[] Copy(int[] values) => (int[])values.Clone();
    static void Inventory(int[] actual, int[] expected, string message) => Require(actual.Length==DemoCatalog.ItemCount && actual.SequenceEqual(expected), message+": inventory differs");
    static void Paid(int[] actual, int[] before, int[] cost, string message)
    {
        for(int i=0;i<DemoCatalog.ItemCount;i++) Require(actual[i]==before[i]-cost[i], $"{message}: item {i}");
    }
    static void Ready(PlayerState player) => player.interactCooldown=0;

    static void TechnologyChecks()
    {
        Run("v3 appended ordinals harvest the right new minerals", () =>
        {
            var items=new[] { ItemKind.Wood,ItemKind.Ore,ItemKind.Iron,ItemKind.Scrap,ItemKind.Fuel,ItemKind.Ammo,ItemKind.Food,ItemKind.CopperOre,ItemKind.Copper,ItemKind.Bronze,ItemKind.Steel,ItemKind.Circuit,ItemKind.Polymer,ItemKind.AdvancedCircuit,ItemKind.UraniumOre,ItemKind.NuclearFuel,ItemKind.Coolant,ItemKind.NuclearWaste,ItemKind.CrushedOre,ItemKind.TinOre,ItemKind.Tin };
            Require(items.Length==21 && DemoCatalog.ItemCount==66 && (int)ItemKind.UraniumConcentrate==62,"item ordinals and current inventory length");
            for(int i=0;i<items.Length;i++) Require((int)items[i]==i,$"saved item {items[i]} was renumbered");
            var appended=new[] { BuildKind.Workbench,BuildKind.StoneFurnace,BuildKind.Waterwheel,BuildKind.Shaft,BuildKind.MechanicalCrusher,BuildKind.Boiler,BuildKind.SteamPipe,BuildKind.SteamMiner,BuildKind.SteamFurnace,BuildKind.SteamTurbine,BuildKind.CircuitAssembler,BuildKind.DieselGenerator,BuildKind.ChemicalPlant,BuildKind.MediumPole,BuildKind.Transformer,BuildKind.HighGenerator,BuildKind.HighPole,BuildKind.PrecisionAssembler,BuildKind.NuclearProcessor,BuildKind.Reactor,BuildKind.CoolingTower };
            for(int i=0;i<appended.Length;i++) Require((int)appended[i]==10+i,$"saved building {appended[i]} was renumbered");
            foreach(var (node,item,ordinal) in new[] { (NodeKind.CopperOre,ItemKind.CopperOre,5),(NodeKind.TinOre,ItemKind.TinOre,6),(NodeKind.UraniumOre,ItemKind.UraniumOre,7) })
            {
                Require((int)node==ordinal,"node ordinal changed");
                var state=Flat(era:TechEra.Primitive); state.nodes.Add(new NodeState { id=state.nextId++,kind=node,x=10,y=10,stock=9, hp=12 });
                var sim=new WorldSimulator(state); var p=sim.AddPlayer("p","P");p.x=8;p.y=10;var before=Copy(p.items);
                sim.Apply(Command(p, "equip", arg: p.gear.Single(g => g.kind == GearKind.StonePickaxe).id));
                sim.Apply(Command(p,"interact",10,10));
                Require(sim.State.nodes.Count==0 && p.items[(int)item]==before[(int)item]+9 && sim.State.gathered==9,$"{node} did not yield its own mineral");
                for(int i=0;i<items.Length;i++) if(i!=(int)item)Require(p.items[i]==before[i],"mineral harvest changed another item");
            }
        });
        Run("fresh primitive world has no free modern factory or research", () =>
        {
            var sim=New(out var p);Require(sim.State.era==TechEra.Primitive && sim.State.version==WorldSimulator.SaveVersion,"new world skipped progression");
            Require(sim.State.produced.Length==DemoCatalog.ItemCount && sim.State.produced.All(n=>n==0),"new world fabricated production history");
            Require(sim.State.buildings.All(b=>TechCatalog.RequiredEra(b.kind)==TechEra.Primitive),"prebuilt modern factory");
            Array.Fill(p.items,100);sim.Apply(Command(p,"research",arg:6));Require(sim.State.era==TechEra.Primitive,"funded malicious research jumped to Nuclear");
        });
        for(int stage=1;stage<=6;stage++)
        {
            var target=(TechEra)stage;var previous=(TechEra)(stage-1);int index=stage-1;
            Run($"research {target} requires materials and a live built prerequisite and charges once for both players", () =>
            {
                var entry=Flat(era:previous); if(target==TechEra.Nuclear) { entry.hvPowered=4; entry.produced=PrecisionEvidence(TechEra.Nuclear); }
                var sim=new WorldSimulator(entry);var a=Builder(sim,"a","A");var b=Builder(sim,"b","B");
                var cost=TechCatalog.ResearchCost(target);Require(cost.Length==DemoCatalog.ItemCount && cost.Any(v=>v>0),"research has no material cost");
                a.items = Copy(cost); b.items = Copy(cost);
                var before=Copy(a.items);var partner=Copy(b.items);
                Require(!TechCatalog.CanResearch(sim.State,a,target) && TechCatalog.ResearchRequirement(sim.State,target).Length>0,"unbuilt prerequisite accepted");
                sim.Apply(Command(a,"research",arg:(int)target));Inventory(a.items,before,"missing-prerequisite research charged");Require(sim.State.era==previous,"missing-prerequisite research unlocked");
                var prerequisite=Add(sim.State,ResearchMachines[index],10,10);prerequisite.hp=0;
                Require(!TechCatalog.CanResearch(sim.State,a,target),"destroyed prerequisite accepted");sim.Apply(Command(a,"research",arg:(int)target));Require(sim.State.era==previous,"destroyed prerequisite advanced era");
                prerequisite.hp=WorldSimulator.BuildingMaxHealth(prerequisite.kind);int missing=Array.FindIndex(cost,c=>c>0);a.items[missing]=cost[missing]-1;var shortInventory=Copy(a.items);
                Require(!TechCatalog.CanResearch(sim.State,a,target),"one-short material cost accepted");sim.Apply(Command(a,"research",arg:(int)target));Require(sim.State.era==previous,"underfunded research unlocked");Inventory(a.items,shortInventory,"underfunded research partially paid");
                a.items=Copy(before);Ready(a);Require(TechCatalog.CanResearch(sim.State,a,target),"funded live prerequisite cannot research");
                int attributes=a.attributePoints,skills=a.skillPoints;var ranks=Copy(a.attributes);var specs=Copy(a.skills);
                sim.Apply(Command(a,"research",arg:(int)target));Require(sim.State.era==target,"valid research did not advance exactly one era");Paid(a.items,before,cost,"research exact material payment");
                Require(a.attributePoints==attributes && a.skillPoints==skills && a.attributes.SequenceEqual(ranks) && a.skills.SequenceEqual(specs),"research spent personal growth points");
                var paid=Copy(a.items);sim.Apply(Command(b,"research",arg:(int)target));Ready(a);sim.Apply(Command(a,"research",arg:(int)target));Inventory(a.items,paid,"duplicate research charged first player");Inventory(b.items,partner,"duplicate research charged teammate");
                if(target==TechEra.Steam)
                {
                    Require(!TechCatalog.IsUnlocked(sim.State,BuildKind.CokeOven),"era entry skipped coking research");
                    b.items = Copy(SteamCatalog.Definition((int)SteamTech.Coking).cost);
                    Add(sim.State,BuildKind.Workbench,10,13);b.x=8;b.y=13;Ready(b);sim.Apply(Command(b,"steam",arg:0));
                }
                if(target==TechEra.LowVoltage)
                {
                    Require(sim.State.lvResearch==0 && !TechCatalog.IsUnlocked(sim.State,BuildKind.PowerPole),"era entry bypassed electrical research");
                    return; // The real LV manufacturing/research chain has its own progression check.
                }
                if(target==TechEra.MediumVoltage)
                {
                    Require(sim.State.mvResearch==0 && !TechCatalog.IsUnlocked(sim.State,BuildKind.OilPump),"era entry bypassed petroleum research");
                    return;
                }
                if(target==TechEra.HighVoltage)
                {
                    Require(sim.State.hvResearch==0 && PrecisionCatalog.BuildRequirement(sim.State,BuildKind.HighPole).Length>0,"era entry bypassed precision research");
                    return;
                }
                b.items = DemoCatalog.BuildCost(NewlyUnlocked[index]);
                Require(TechCatalog.IsUnlocked(sim.State,NewlyUnlocked[index]),"teammate did not see shared unlock");b.x=8;b.y=15;Ready(b);int buildings=sim.State.buildings.Count;sim.Apply(Command(b,"build",10,15,(int)NewlyUnlocked[index]));
                Require(sim.State.buildings.Count==buildings+1 && sim.State.buildings.Any(m=>m.x==10 && m.y==15 && m.kind==NewlyUnlocked[index]),"teammate cannot use shared research");
            });
        }
        Run("research rejects dead disconnected foreign and invalid target commands", () =>
        {
            foreach(int arg in new[] { int.MinValue,-1,0,2,3,4,5,6,7,int.MaxValue })
            {
                var state=Flat(era:TechEra.Primitive);Add(state,BuildKind.Workbench,10,10);var sim=new WorldSimulator(state);var p=Builder(sim);var before=Copy(p.items);
                sim.Apply(Command(p,"research",arg:arg));Require(sim.State.era==TechEra.Primitive,$"invalid/skipped research {arg} changed era");Inventory(p.items,before,"invalid research charged");
            }
            foreach(bool dead in new[] { true,false })
            {
                var state=Flat(era:TechEra.Primitive);Add(state,BuildKind.Workbench,10,10);var sim=new WorldSimulator(state);var p=Builder(sim);var before=Copy(p.items);if(dead)p.hp=0;else sim.SetConnected(p.id,false);
                Require(!TechCatalog.CanResearch(sim.State,p,TechEra.Water),"ineligible actor accepted by research query");sim.Apply(Command(p,"research",arg:1));Require(sim.State.era==TechEra.Primitive,"ineligible actor researched");Inventory(p.items,before,"ineligible actor paid");
            }
            var valid=Flat(era:TechEra.Primitive);Add(valid,BuildKind.Workbench,10,10);var owner=new WorldSimulator(valid);var stranger=new PlayerState { id="forged" };Array.Fill(stranger.items,100);
            Require(!TechCatalog.CanResearch(owner.State,stranger,TechEra.Water),"foreign player can research");owner.Apply(Command(stranger,"research",arg:1));Require(owner.State.era==TechEra.Primitive,"forged player unlocked technology");
        });
        Run("every locked building rejects a funded command then builds after its actual era unlock", () =>
        {
            foreach(BuildKind kind in Enum.GetValues<BuildKind>().Where(k=>TechCatalog.RequiredEra(k)>TechEra.Primitive))
            {
                var state=Flat(era:TechEra.Primitive);int w=DemoCatalog.Width(kind),h=DemoCatalog.Height(kind);if(!ModularMachineCatalog.IsModular(kind)) Water(state,10,10+h);if(kind==BuildKind.Miner || kind==BuildKind.SteamMiner)Ore(state,10+w,10);
                if(kind==BuildKind.OilPump)state.nodes.Add(new NodeState{id=state.nextId++,kind=NodeKind.Oil,x=10+w,y=10,stock=100});
                var sim=new WorldSimulator(state);var p=Builder(sim);p.items=DemoCatalog.BuildCost(kind);p.x=8;p.y=10;var before=Copy(p.items);sim.Apply(Command(p,"build",10,10,(int)kind));Require(sim.State.buildings.Count==0,$"locked {kind} built");Inventory(p.items,before,$"locked {kind} paid");
                if(ModularMachineCatalog.IsModular(kind)) HoldModularWrench(sim,p);
                sim.State.era=TechCatalog.RequiredEra(kind);sim.State.steamResearch=255;sim.State.lvResearch=255;sim.State.mvResearch=255;sim.State.hvResearch=PrecisionCatalog.AllResearch;Ready(p);sim.Apply(Command(p,"build",10,10,(int)kind));Require(sim.State.buildings.Count==1 && sim.State.buildings[0].kind==kind,$"{kind} negative control was invalid even when unlocked");if(ModularMachineCatalog.IsModular(kind))
                {
                    Require(p.items.SequenceEqual(before),"free controller plan charged materials");
                    var machine=sim.State.buildings[0];
                    foreach(var module in ModularL.Skip(1)) PlanModule(sim,p,machine,module.role,10+module.x,10+module.y,module.rotation);
                    CompleteModular(sim,p,machine);
                }
                Paid(p.items,before,DemoCatalog.BuildCost(kind),$"{kind} construction");
            }
        });
        Run("all era hotbars expose unlocked kinds that actually build and charge correctly", () =>
        {
            foreach(TechEra era in Enum.GetValues<TechEra>())
            {
                var hotbar=TechCatalog.Hotbar(era);Require(hotbar.Length==10,$"{era} hotbar lost numeric slots");
                foreach(var kind in hotbar)
                {
                    var state=Flat(era:era);Water(state,10,10+DemoCatalog.Height(kind));if(kind==BuildKind.Miner || kind==BuildKind.SteamMiner)Ore(state,10+DemoCatalog.Width(kind),10);
                    if(kind==BuildKind.OilPump)state.nodes.Add(new NodeState{id=state.nextId++,kind=NodeKind.Oil,x=10+DemoCatalog.Width(kind),y=10,stock=100});
                    Require(TechCatalog.IsUnlocked(state,kind),$"{era} hotbar offers locked {kind}");var sim=new WorldSimulator(state);var p=Builder(sim);p.items=DemoCatalog.BuildCost(kind);p.x=8;p.y=10;var before=Copy(p.items);sim.Apply(Command(p,"build",10,10,(int)kind));
                    Require(sim.State.buildings.Count==1 && sim.State.buildings[0].kind==kind,$"{era} hotbar {kind} cannot actually build");Paid(p.items,before,DemoCatalog.BuildCost(kind),"hotbar construction");
                }
            }
        });
        Run("locked recipe selection cannot be bypassed by craft interact or configure arguments", () =>
        {
            foreach(string action in new[] { "recipe","configure","craft","interact" })
            {
                var state=Flat(era:TechEra.Primitive);var furnace=Add(state,BuildKind.StoneFurnace,10,10);furnace.items[(int)ItemKind.CrushedOre]=2;var sim=new WorldSimulator(state);var p=Builder(sim);p.x=8;p.y=10;var before=Copy(p.items);var machine=Building(sim,furnace.id);int selectedRecipe=machine.recipe;
                sim.Apply(Command(p,action,10,10,13));Require(machine.recipe==selectedRecipe && sim.State.era==TechEra.Primitive,$"{action} selected locked water recipe");
                if(action!="interact")Inventory(p.items,before,$"{action} paid for locked recipe");Require(machine.items[(int)ItemKind.Iron]==0,"locked recipe fabricated iron");
                if(action=="recipe"){sim.State.era=TechEra.Water;Ready(p);sim.Apply(Command(p,"recipe",10,10,13));Require(machine.recipe==13,"unlocked same-machine recipe cannot be selected");}
            }
        });
        Run("recipe command validates machine identity range actor and malicious IDs", () =>
        {
            foreach(var (arg,x,dead,connected) in new[] { (-1,10,false,true),(int.MaxValue,10,false,true),(90,10,false,true),(42,50,false,true),(42,10,true,true),(42,10,false,false) })
            {
                var state=Flat(era:TechEra.Nuclear);var furnace=Add(state,BuildKind.Smelter,x,10);var sim=new WorldSimulator(state);var p=Builder(sim);p.x=8;p.y=10;if(dead)p.hp=0;if(!connected)sim.SetConnected(p.id,false);var m=Building(sim,furnace.id);int old=m.recipe;var before=Copy(p.items);
                sim.Apply(Command(p,"recipe",x,10,arg));Require(m.recipe==old,"malformed or unauthorized recipe command changed machine");Inventory(p.items,before,"invalid recipe command consumed inventory");
            }
            var good=Flat();var target=Add(good,BuildKind.Smelter,10,10);var world=new WorldSimulator(good);var actor=Builder(world);actor.x=8;actor.y=10;world.Apply(Command(actor,"recipe",10,10,42));Require(Building(world,target.id).recipe==42,"reachable unlocked copper recipe rejected");
        });
        Run("legacy hand fuel and ammo retain material counts and award no XP", () =>
        {
            var sim=new WorldSimulator(Flat(era:TechEra.Primitive));var p=Builder(sim);int xp=p.xp;var before=Copy(p.items);sim.Apply(Command(p,"craft",arg:0));var expected=Copy(before);expected[(int)ItemKind.Wood]-=3;expected[(int)ItemKind.Fuel]+=2;Inventory(p.items,expected,"manual fuel");
            Advance(sim,.55f);sim.Apply(Command(p,"craft",arg:1));Inventory(p.items,expected,"manual ammo remains locked before LV equipment research");Require(p.xp==xp,"manual crafting awarded farmable XP");
        });
        Run("legacy manual bronze is primitive and requires a nearby workbench", () =>
        {
            foreach(var (era,benchX,allowed) in new[] { (TechEra.Primitive,40,false),(TechEra.Primitive,10,true),(TechEra.Water,10,true) })
            {
                var state=Flat(era:era);Add(state,BuildKind.Workbench,benchX,10);var sim=new WorldSimulator(state);var p=Builder(sim);p.items=Copy(TechCatalog.Recipe(2).inputs);p.x=8;p.y=10;var expected=Copy(p.items);sim.Apply(Command(p,"craft",arg:2));if(allowed){expected[(int)ItemKind.Copper]-=3;expected[(int)ItemKind.Tin]--;expected[(int)ItemKind.Bronze]+=4;}Inventory(p.items,expected,"workbench bronze gate");
            }
        });
        Run("machine recipe IDs cannot be submitted as manual crafting even at Nuclear", () =>
        {
            foreach(var recipe in TechCatalog.Recipes.Where(r=>r.building!=BuildKind.Workbench))
            {
                var sim=new WorldSimulator(Flat(era:TechEra.Nuclear));var p=Builder(sim);var before=Copy(p.items);sim.Apply(Command(p,"craft",arg:recipe.id));Inventory(p.items,before,$"machine recipe {recipe.id} handcrafted");
            }
        });
    }
}
