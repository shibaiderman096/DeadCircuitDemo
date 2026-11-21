using DeadCircuit;
using System.Text.Json;
using System.Text.Json.Nodes;

static partial class Checks
{
    static WorldState VersionThreeFixture(bool current = false)
    {
        var state=Flat(era:TechEra.Nuclear);var reactor=Add(state,BuildKind.Reactor,10,10);reactor.temperature=440;reactor.fuel=3;reactor.progress=.4f;reactor.items[(int)ItemKind.NuclearFuel]=2;reactor.items[(int)ItemKind.Coolant]=8;
        var processor=Add(state,BuildKind.NuclearProcessor,20,10);processor.recipe=90;processor.items[(int)ItemKind.UraniumConcentrate]=2;processor.items[(int)ItemKind.Steel]=1;
        Add(state,BuildKind.Transformer,25,10).setting=1;state.produced[(int)ItemKind.NuclearWaste]=7;
        var sim=new WorldSimulator(state);Builder(sim);return current ? Saved(sim.State) : AsVersionThree(Saved(sim.State));
    }
    // Actual pre-equipment JSON: 21 slots and no v4 equipment fields, never a v4
    // graph relabeled as v3 with current inventories. Preserve the caller graph.
    static WorldState AsVersionThree(WorldState source)
    {
        var state=Saved(source);state.version=3;state.produced=state.produced.Take(21).ToArray();
        foreach(var b in state.buildings)b.items=b.items.Take(21).ToArray();
        foreach(var p in state.players)p.items=p.items.Take(21).ToArray();
        var root=JsonNode.Parse(Dump(state)).AsObject();
        root.Remove("primitiveVersion");root.Remove("primitiveResearch");
        foreach(var p in root["players"].AsArray())
            foreach(string field in new[] { "gear","parts","weaponId","toolId","activeSlot","armorIds","selectedAmmo","reloadRemaining","reloadingGearId","recoil","aiming","moving" })p.AsObject().Remove(field);
        return JsonSerializer.Deserialize<WorldState>(root.ToJsonString(),Json);
    }
    static WorldState LegacyFixture(int version)
    {
        var state=Flat();state.version=version;int footprint=version==1?0:1;
        Ore(state,3,3,51);state.nodes.Add(new NodeState { id=state.nextId++,kind=NodeKind.Tree,x=5,y=3,stock=9 });
        var assembler=Add(state,BuildKind.Assembler,10,10,footprintVersion:footprint);assembler.items=new[] { 0,0,2,0,0,4,0 };
        var belt=Add(state,BuildKind.Belt,14,10,footprintVersion:footprint);belt.items=new[] { 0,1,0,0,0,0,0 };
        var chest=Add(state,BuildKind.Chest,15,10,footprintVersion:footprint);chest.items=new[] { 9,0,0,0,0,0,0 };
        Add(state,BuildKind.SolarPanel,20,10,footprintVersion:footprint).items=new int[7];
        state.players.Add(new PlayerState { id="legacy",name="Legacy",seat=0,x=32,y=35,level=3,xp=10,attributePoints=4,skillPoints=2,attributes=new[] { 2,0,0,0,0 },skills=new[] { 1,0,0 },items=new[] { 11,12,13,14,15,16,17 } });
        state.cargo.Add(new CargoState { id=state.nextId++,fromId=belt.id,toId=chest.id,item=(int)ItemKind.Ammo,fromX=14,fromY=10,toX=15,toY=10,progress=.5f,duration=.4f });
        var root=JsonNode.Parse(Dump(state)).AsObject();root.Remove("era");root.Remove("produced");root.Remove("powerNetworks");
        root.Remove("primitiveVersion");root.Remove("primitiveResearch");
        foreach(var p in root["players"].AsArray())
            foreach(string field in new[] { "gear","parts","weaponId","toolId","activeSlot","armorIds","selectedAmmo","reloadRemaining","reloadingGearId","recoil","aiming","moving" })p.AsObject().Remove(field);
        foreach(var node in root["buildings"].AsArray())
        {
            foreach(string field in new[] { "recipe","setting","temperature","tripped","networkId","powerRatio","powerDemand","powerGeneration" })node.AsObject().Remove(field);
            if(version==1)node.AsObject().Remove("footprintVersion");
        }
        return JsonSerializer.Deserialize<WorldState>(root.ToJsonString(),Json);
    }
    static void VersionThreeSaveChecks()
    {
        foreach(int version in new[] { 1,2 })
        {
            RunHistorical($"v{version} seven-slot migration keeps layout inventory cargo and personal points and infers old solar as MV", () =>
            {
                var legacy=LegacyFixture(version);var original=Dump(legacy);var oldCargo=JsonSerializer.Serialize(legacy.cargo,Json);var sim=new WorldSimulator(legacy);var restored=sim.State;
                Require(restored.version==WorldSimulator.SaveVersion && restored.era==TechEra.MediumVoltage,"legacy solar did not infer MV in v4");Require(Dump(legacy)==original,"migration mutated caller-owned legacy data");
                Require(restored.buildings.Count==legacy.buildings.Count,"migration dropped old buildings");
                Require(restored.ground==legacy.ground,"migration changed old terrain");
                foreach(var oldNode in legacy.nodes)
                {
                    var current=restored.nodes.Single(n=>n.id==oldNode.id);Require(current.kind==oldNode.kind && current.x==oldNode.x && current.y==oldNode.y && current.stock==oldNode.stock && current.hp==oldNode.hp,"migration replaced an old resource node");
                }
                for(int i=0;i<legacy.buildings.Count;i++)
                {
                    var old=legacy.buildings[i];var current=restored.buildings.Single(b=>b.id==old.id);
                    Require(current.kind==old.kind && current.x==old.x && current.y==old.y && current.rotation==old.rotation && current.footprintVersion==old.footprintVersion,"legacy layout changed");
                    Require(current.items.Length==DemoCatalog.ItemCount && current.items.Take(7).SequenceEqual(old.items) && current.items.Skip(7).All(n=>n==0),"legacy building inventory lost or invented items");
                }
                var p=restored.players.Single();var source=legacy.players.Single();
                Require(p.items.Length==DemoCatalog.ItemCount && p.items.Take(7).SequenceEqual(source.items) && p.items.Skip(7).All(n=>n==0),"legacy player inventory migration");
                Require(p.level==source.level && p.xp==source.xp && p.attributePoints==4 && p.skillPoints==2 && p.attributes.SequenceEqual(source.attributes) && p.skills.SequenceEqual(source.skills),"migration changed personal progression");
                Require(JsonSerializer.Serialize(restored.cargo,Json)==oldCargo,"migration lost in-flight identity or timing");
                var cargo=restored.cargo.Single();var sink=Building(sim,cargo.toId);int ammo=sink.items[(int)ItemKind.Ammo];var totals=ItemTotals(restored);
                for(int tick=0;tick<6;tick++){sim.Step(WorldSimulator.FixedStep);Conserved(restored,totals);}Require(sink.items[(int)ItemKind.Ammo]==ammo+1,"migrated packet was not delivered exactly once");
                var again=new WorldSimulator(Saved(restored));Require(again.State.version==WorldSimulator.SaveVersion && again.State.era==TechEra.MediumVoltage && again.State.players[0].items.SequenceEqual(p.items),"migrated v4 second restore lost research or inventory");
            });
        }
        Run("current restore isolates production research thermal recipe setting and player state", () =>
        {
            var saved=VersionThreeFixture(current:true);var restored=new WorldSimulator(saved);string before=Dump(restored.State);var oldReactor=saved.buildings.Single(b=>b.kind==BuildKind.Reactor);var newReactor=restored.State.buildings.Single(b=>b.kind==BuildKind.Reactor);
            Require(!ReferenceEquals(saved.produced,restored.State.produced) && !ReferenceEquals(saved.buildings,restored.State.buildings),"v3 restore aliases research/production collections");
            Require(restored.State.produced.Length==DemoCatalog.ItemCount && restored.State.produced.SequenceEqual(saved.produced),"current production counters were lost or invented during restoration");
            Require(restored.State.players.All(p=>p.items.Length==DemoCatalog.ItemCount) && restored.State.buildings.All(b=>b.items.Length==DemoCatalog.ItemCount),"v3 inventories did not expand to the current length");
            Require(newReactor.temperature==440 && newReactor.progress==.4f && newReactor.fuel==3 && !newReactor.tripped,"restore changed reactor operating reserves");
            Require(restored.State.buildings.Single(b=>b.kind==BuildKind.NuclearProcessor).recipe==90 && restored.State.buildings.Single(b=>b.kind==BuildKind.Transformer).setting==1,"restore lost recipe/converter configuration");
            saved.era=TechEra.Primitive;saved.produced[17]=999;oldReactor.temperature=1199;oldReactor.tripped=true;oldReactor.items[15]=99;saved.buildings.Single(b=>b.kind==BuildKind.NuclearProcessor).recipe=int.MaxValue;saved.buildings.Single(b=>b.kind==BuildKind.Transformer).setting=0;saved.players[0].items[20]=999;saved.players[0].attributes[0]=9;saved.players[0].skillPoints=0;
            Require(Dump(restored.State)==before,"caller mutation crossed a v3 deep-copy boundary");
        });
        Run("current nuclear facility restore preserves interlock mode and dedicated waste inventory", () =>
        {
            var state=Flat(era:TechEra.Nuclear);var console=Add(state,(BuildKind)61,10,10);console.setting=1;var vault=Add(state,(BuildKind)62,13,10);vault.items[(int)ItemKind.NuclearWaste]=3;
            var restored=new WorldSimulator(Saved(state));
            Require(DemoCatalog.BuildNames.Length==66 && (int)BuildKind.NuclearWasteVault==62,"current catalog lost appended nuclear facilities");
            Require(Building(restored,console.id).setting==1,"current restore lost armed reactor console");
            Require(Building(restored,vault.id).items[(int)ItemKind.NuclearWaste]==3,"current restore lost dedicated waste inventory");
        });
        Run("current nuclear restore permits staged waste while a vault route is temporarily disconnected", () =>
        {
            var state=Flat(era:TechEra.Nuclear);var reactor=Add(state,BuildKind.Reactor,10,10);reactor.items[(int)ItemKind.NuclearWaste]=3;var vault=Add(state,BuildKind.NuclearWasteVault,20,20);
            var restored=new WorldSimulator(Saved(state));
            Require(Building(restored,reactor.id).items[(int)ItemKind.NuclearWaste]==3 && Building(restored,vault.id).items[(int)ItemKind.NuclearWaste]==0,"current restore rejected or changed staged reactor waste");
            state.buildings.Remove(vault);
            var withoutVault=new WorldSimulator(Saved(state));
            Require(Building(withoutVault,reactor.id).items[(int)ItemKind.NuclearWaste]==3,"current restore lost staged waste after the only vault was destroyed");

            var ordinary=Flat(era:TechEra.Nuclear);var chest=Add(ordinary,BuildKind.Chest,10,10);chest.items[(int)ItemKind.NuclearWaste]=1;Reject(Saved(ordinary),"ordinary inventory accepted nuclear waste");
            var illegalCargo=Flat(era:TechEra.Nuclear);var from=Add(illegalCargo,BuildKind.Reactor,10,10);var to=Add(illegalCargo,BuildKind.Chest,11,10);illegalCargo.cargo.Add(new CargoState { id=illegalCargo.nextId++,fromId=from.id,toId=to.id,item=(int)ItemKind.NuclearWaste,fromX=10,fromY=10,toX=11,toY=10,progress=.2f,duration=.4f });Reject(Saved(illegalCargo),"reactor nuclear waste cargo to an ordinary chest accepted");
        });
        Run("current nuclear restore enforces vault inventory and incoming waste capacity", () =>
        {
            int waste=(int)ItemKind.NuclearWaste, limit=WorldSimulator.BufferLimit;
            var state=Flat(era:TechEra.Nuclear);
            var reactor=Add(state,BuildKind.Reactor,10,10);
            var vault=Add(state,BuildKind.NuclearWasteVault,13,10);
            reactor.items[waste]=1;
            vault.items[waste]=limit-1;
            var live=new WorldSimulator(state);
            var flight=AwaitCargo(live,reactor.id,vault.id);
            Require(flight.item==waste,"capacity fixture did not dispatch waste to the vault");

            var saved=Saved(live.State);
            var restored=new WorldSimulator(saved);
            Require(Building(restored,vault.id).items[waste]==limit-1 && restored.State.cargo.Single().id==flight.id,
                "restore changed a vault with exactly one slot reserved for incoming waste");

            var savedVault=saved.buildings.Single(b=>b.id==vault.id);
            savedVault.items[waste]=limit;
            Reject(saved,"full vault accepted an additional incoming waste reservation");
            saved.cargo.Clear();
            Require(Building(new WorldSimulator(saved),vault.id).items[waste]==limit,"restore changed a full vault with no incoming reservation");
            savedVault.items[waste]=limit+1;
            Reject(saved,"single vault accepted an inventory above its buffer limit");
        });
        Run("current nuclear restore preserves and delivers in-flight reactor hot coolant", () =>
        {
            int hot=(int)ItemKind.HotCoolant;
            // Tanks touch the south/east/north/west right outlet for rotations 0/1/2/3.
            foreach(var (rotation,tankX,tankY) in new[] { (0,11,8),(1,12,11),(2,11,12),(3,8,11) })
            {
                var state=Flat(era:TechEra.Nuclear);
                var reactor=Add(state,BuildKind.Reactor,10,10,rotation);
                var tank=Add(state,BuildKind.LiquidTank,tankX,tankY);
                tank.setting=7; // Hot coolant.
                reactor.items[hot]=2;
                var live=new WorldSimulator(state);
                var flight=AwaitCargo(live,reactor.id,tank.id);
                live.Step(WorldSimulator.FixedStep);
                Require(flight.item==hot && flight.progress>0f && flight.progress<1f,
                    "save fixture did not have hot coolant partway to the tank");

                var restored=new WorldSimulator(Saved(live.State));
                var restoredReactor=Building(restored,reactor.id);
                var restoredTank=Building(restored,tank.id);
                Require(restoredReactor.items[hot]==1 && restoredTank.items[hot]==0 &&
                    JsonSerializer.Serialize(restored.State.cargo.Single(),Json)==JsonSerializer.Serialize(flight,Json),
                    "restore changed buffered hot coolant or its in-flight packet");

                int deliveriesBefore=live.State.mvPiped;
                var liveTank=Building(live,tank.id);
                for(int tick=0;tick<40;tick++)
                {
                    live.Step(WorldSimulator.FixedStep);
                    restored.Step(WorldSimulator.FixedStep);
                    Require(restoredTank.items[hot]==liveTank.items[hot],"restored hot coolant arrived on a different tick");
                    Require(ItemTotals(restored.State)[hot]==2,"restored hot coolant was lost or duplicated");
                }
                Require(restoredTank.items[hot]==2 && restored.State.cargo.Count==0 && restored.State.mvPiped==deliveriesBefore+2,
                    "restored reactor did not deliver both hot coolant units exactly once");
                Require(Dump(live.State)==Dump(restored.State),$"hot coolant replay diverged after restore at rotation {rotation}");
            }
        });
        Run("current save roundtrip resumes the same reactor temperature fuel coolant waste and power", () =>
        {
            var live=new WorldSimulator(VersionThreeFixture(current:true));Advance(live,1.25f);var restored=new WorldSimulator(Saved(live.State));
            for(int tick=0;tick<160;tick++){live.Step(WorldSimulator.FixedStep);restored.Step(WorldSimulator.FixedStep);}
            Require(Dump(live.State)==Dump(restored.State),"v3 reactor/production replay diverged after a mid-run save");
        });
        Run("v3 validation rejects corrupted research eras and production arrays", () =>
        {
            foreach(var corrupt in new Action<WorldState>[] { s=>s.era=(TechEra)(-1),s=>s.era=(TechEra)7,s=>s.produced=null,s=>s.produced=new int[7],s=>s.produced=new int[20],s=>s.produced=new int[22],s=>s.produced=new int[25],s=>s.produced=new int[29],s=>s.produced[20]=-1 })
            {
                var invalid=VersionThreeFixture();corrupt(invalid);Reject(invalid,"corrupted research/production state accepted");
            }
        });
        Run("v3 validation rejects corrupt thermal recipe and converter fields", () =>
        {
            foreach(var corrupt in new Action<BuildingState>[] { b=>b.temperature=float.NaN,b=>b.temperature=float.PositiveInfinity,b=>b.temperature=19,b=>b.temperature=1201,b=>b.progress=1.01f,b=>b.fuel=20.1f,b=>b.recipe=-1,b=>b.recipe=90,b=>b.recipe=int.MaxValue,b=>b.setting=1 })
            {
                var invalid=VersionThreeFixture();corrupt(invalid.buildings.Single(b=>b.kind==BuildKind.Reactor));Reject(invalid,"corrupted reactor state accepted");
            }
            foreach(var corrupt in new Action<BuildingState>[] { b=>b.recipe=10,b=>b.recipe=int.MaxValue,b=>b.temperature=21,b=>b.tripped=true,b=>b.setting=-1 })
            {
                var invalid=VersionThreeFixture();corrupt(invalid.buildings.Single(b=>b.kind==BuildKind.NuclearProcessor));Reject(invalid,"corrupted machine recipe/state accepted");
            }
            foreach(int setting in new[] { -1,2,int.MaxValue }){var invalid=VersionThreeFixture();invalid.buildings.Single(b=>b.kind==BuildKind.Transformer).setting=setting;Reject(invalid,"corrupt transformer mode accepted");}
        });
        Run("v3 validation rejects malformed old-length inventory arrays without silently migrating them", () =>
        {
            foreach(int length in new[] { 0,7,20,22,25,29 })
            {
                var player=VersionThreeFixture();player.players[0].items=new int[length];Reject(player,$"v3 player inventory length {length} accepted");var machine=VersionThreeFixture();machine.buildings[0].items=new int[length];Reject(machine,$"v3 machine inventory length {length} accepted");
            }
            foreach(var corrupt in new Action<WorldState>[] { s=>s.players[0].items=null,s=>s.buildings[0].items=null,s=>s.players[0].items[20]=-1,s=>s.players[0].items[20]=1000,s=>s.buildings[0].items[15]=201,s=>s.buildings[0].items[15]=-1,s=>s.players[0].attributes=new int[4],s=>s.players[0].skills=new int[4] })
            {var invalid=VersionThreeFixture();corrupt(invalid);Reject(invalid,"corrupted v3 inventory/stat array accepted");}
        });
        Run("v3 validation rejects a selected recipe from an unresearched era", () =>
        {
            var state=Flat(era:TechEra.Primitive);var furnace=Add(state,BuildKind.StoneFurnace,10,10);furnace.recipe=13;furnace.items[(int)ItemKind.CrushedOre]=1;
            Reject(AsVersionThree(state),"v3 primitive save accepted the locked Water recipe 13");
        });
        Run("v3 validation rejects an HV converter setting saved before HV research", () =>
        {
            var state=Flat(era:TechEra.MediumVoltage);Add(state,BuildKind.Transformer,10,10).setting=1;
            Reject(AsVersionThree(state),"v3 MV save accepted the locked HV-to-MV converter setting");
        });
        Run("v3 validation rejects invalid power tier summaries and newly invalid cargo items", () =>
        {
            foreach(var corrupt in new Action<PowerNetworkState>[] { n=>n.tier=-1,n=>n.tier=5,n=>n.generation=float.NaN,n=>n.ratio=1.01f,n=>n.nodes=0 })
            {var invalid=VersionThreeFixture();Require(invalid.powerNetworks.Count>0,"power-corruption fixture has no network");corrupt(invalid.powerNetworks[0]);Reject(invalid,"corrupt energy summary accepted");}
            var state=Flat(era:TechEra.Nuclear);var from=Add(state,BuildKind.Belt,10,10);from.items[20]=1;var to=Add(state,BuildKind.Chest,11,10);var sim=new WorldSimulator(state);AwaitCargo(sim,from.id,to.id);
            var valid=new WorldSimulator(Saved(sim.State));Require(valid.State.cargo.Single().item==20,"valid new tin cargo rejected by v3");
            foreach(int item in new[] { -1,21,int.MaxValue }){var invalid=AsVersionThree(sim.State);invalid.cargo[0].item=item;Reject(invalid,"invalid v3 cargo item accepted");}
        });
    }
}
