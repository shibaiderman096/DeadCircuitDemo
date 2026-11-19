using DeadCircuit;
using System.Text.Json;
using System.Text.Json.Nodes;

static partial class Checks
{
    static WorldState VersionThreeFixture()
    {
        var state=Flat(era:TechEra.Nuclear);var reactor=Add(state,BuildKind.Reactor,10,10);reactor.temperature=440;reactor.fuel=3;reactor.progress=.4f;reactor.items[(int)ItemKind.NuclearFuel]=2;reactor.items[(int)ItemKind.Coolant]=8;
        var processor=Add(state,BuildKind.NuclearProcessor,20,10);processor.recipe=90;processor.items[(int)ItemKind.UraniumOre]=2;processor.items[(int)ItemKind.Steel]=1;
        Add(state,BuildKind.Transformer,25,10).setting=1;state.produced[(int)ItemKind.NuclearWaste]=7;
        var sim=new WorldSimulator(state);Builder(sim);return Saved(sim.State);
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
            Run($"v{version} seven-slot migration keeps layout inventory cargo and personal points and infers old solar as MV", () =>
            {
                var legacy=LegacyFixture(version);var original=Dump(legacy);var oldCargo=JsonSerializer.Serialize(legacy.cargo,Json);var sim=new WorldSimulator(legacy);var restored=sim.State;
                Require(restored.version==3 && restored.era==TechEra.MediumVoltage,"legacy solar did not infer MV in v3");Require(Dump(legacy)==original,"migration mutated caller-owned legacy data");
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
                    Require(current.items.Length==21 && current.items.Take(7).SequenceEqual(old.items) && current.items.Skip(7).All(n=>n==0),"legacy building inventory lost or invented items");
                }
                var p=restored.players.Single();var source=legacy.players.Single();
                Require(p.items.Length==21 && p.items.Take(7).SequenceEqual(source.items) && p.items.Skip(7).All(n=>n==0),"legacy player inventory migration");
                Require(p.level==source.level && p.xp==source.xp && p.attributePoints==4 && p.skillPoints==2 && p.attributes.SequenceEqual(source.attributes) && p.skills.SequenceEqual(source.skills),"migration changed personal progression");
                Require(JsonSerializer.Serialize(restored.cargo,Json)==oldCargo,"migration lost in-flight identity or timing");
                var cargo=restored.cargo.Single();var sink=Building(sim,cargo.toId);int ammo=sink.items[(int)ItemKind.Ammo];var totals=ItemTotals(restored);
                for(int tick=0;tick<6;tick++){sim.Step(WorldSimulator.FixedStep);Conserved(restored,totals);}Require(sink.items[(int)ItemKind.Ammo]==ammo+1,"migrated packet was not delivered exactly once");
                var again=new WorldSimulator(Saved(restored));Require(again.State.era==TechEra.MediumVoltage && again.State.players[0].items.SequenceEqual(p.items),"migrated v3 second restore lost research or inventory");
            });
        }
        Run("v3 restore isolates production research thermal recipe setting and player state", () =>
        {
            var saved=VersionThreeFixture();var restored=new WorldSimulator(saved);string before=Dump(restored.State);var oldReactor=saved.buildings.Single(b=>b.kind==BuildKind.Reactor);var newReactor=restored.State.buildings.Single(b=>b.kind==BuildKind.Reactor);
            Require(!ReferenceEquals(saved.produced,restored.State.produced) && !ReferenceEquals(saved.buildings,restored.State.buildings),"v3 restore aliases research/production collections");
            Require(newReactor.temperature==440 && newReactor.progress==.4f && newReactor.fuel==3 && !newReactor.tripped,"restore changed reactor operating reserves");
            Require(restored.State.buildings.Single(b=>b.kind==BuildKind.NuclearProcessor).recipe==90 && restored.State.buildings.Single(b=>b.kind==BuildKind.Transformer).setting==1,"restore lost recipe/converter configuration");
            saved.era=TechEra.Primitive;saved.produced[17]=999;oldReactor.temperature=1199;oldReactor.tripped=true;oldReactor.items[15]=99;saved.buildings.Single(b=>b.kind==BuildKind.NuclearProcessor).recipe=int.MaxValue;saved.buildings.Single(b=>b.kind==BuildKind.Transformer).setting=0;saved.players[0].items[20]=999;saved.players[0].attributes[0]=9;saved.players[0].skillPoints=0;
            Require(Dump(restored.State)==before,"caller mutation crossed a v3 deep-copy boundary");
        });
        Run("v3 save roundtrip resumes the same reactor temperature fuel coolant waste and power", () =>
        {
            var live=new WorldSimulator(VersionThreeFixture());Advance(live,1.25f);var restored=new WorldSimulator(Saved(live.State));
            for(int tick=0;tick<160;tick++){live.Step(WorldSimulator.FixedStep);restored.Step(WorldSimulator.FixedStep);}
            Require(Dump(live.State)==Dump(restored.State),"v3 reactor/production replay diverged after a mid-run save");
        });
        Run("v3 validation rejects corrupted research eras and production arrays", () =>
        {
            foreach(var corrupt in new Action<WorldState>[] { s=>s.era=(TechEra)(-1),s=>s.era=(TechEra)7,s=>s.produced=null,s=>s.produced=new int[7],s=>s.produced=new int[20],s=>s.produced=new int[22],s=>s.produced[20]=-1 })
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
            foreach(int length in new[] { 0,7,20,22 })
            {
                var player=VersionThreeFixture();player.players[0].items=new int[length];Reject(player,$"v3 player inventory length {length} accepted");var machine=VersionThreeFixture();machine.buildings[0].items=new int[length];Reject(machine,$"v3 machine inventory length {length} accepted");
            }
            foreach(var corrupt in new Action<WorldState>[] { s=>s.players[0].items=null,s=>s.buildings[0].items=null,s=>s.players[0].items[20]=-1,s=>s.players[0].items[20]=1000,s=>s.buildings[0].items[15]=201,s=>s.buildings[0].items[15]=-1,s=>s.players[0].attributes=new int[4],s=>s.players[0].skills=new int[4] })
            {var invalid=VersionThreeFixture();corrupt(invalid);Reject(invalid,"corrupted v3 inventory/stat array accepted");}
        });
        Run("v3 validation rejects a selected recipe from an unresearched era", () =>
        {
            var state=Flat(era:TechEra.Primitive);var furnace=Add(state,BuildKind.StoneFurnace,10,10);furnace.recipe=13;furnace.items[(int)ItemKind.CrushedOre]=1;
            Reject(state,"v3 primitive save accepted the locked Water recipe 13");
        });
        Run("v3 validation rejects an HV converter setting saved before HV research", () =>
        {
            var state=Flat(era:TechEra.MediumVoltage);Add(state,BuildKind.Transformer,10,10).setting=1;
            Reject(state,"v3 MV save accepted the locked HV-to-MV converter setting");
        });
        Run("v3 validation rejects invalid power tier summaries and newly invalid cargo items", () =>
        {
            foreach(var corrupt in new Action<PowerNetworkState>[] { n=>n.tier=-1,n=>n.tier=5,n=>n.generation=float.NaN,n=>n.ratio=1.01f,n=>n.nodes=0 })
            {var invalid=VersionThreeFixture();Require(invalid.powerNetworks.Count>0,"power-corruption fixture has no network");corrupt(invalid.powerNetworks[0]);Reject(invalid,"corrupt energy summary accepted");}
            var state=Flat(era:TechEra.Nuclear);var from=Add(state,BuildKind.Belt,10,10);from.items[20]=1;var to=Add(state,BuildKind.Chest,11,10);var sim=new WorldSimulator(state);AwaitCargo(sim,from.id,to.id);
            var valid=new WorldSimulator(Saved(sim.State));Require(valid.State.cargo.Single().item==20,"valid new tin cargo rejected by v3");
            foreach(int item in new[] { -1,21,int.MaxValue }){var invalid=Saved(sim.State);invalid.cargo[0].item=item;Reject(invalid,"invalid v3 cargo item accepted");}
        });
    }
}
