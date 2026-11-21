using DeadCircuit;

static partial class Checks
{
    static void DoorSystemChecks()
    {
        Run("door closed blocks movement open permits entry and save restores occupancy", () =>
        {
            var s=Flat(); s.version=WorldSimulator.SaveVersion; WorldGeneration.LegacyMetadata(s);
            var door=Add(s,BuildKind.Door,10,10);
            var sim=new WorldSimulator(s); var p=Builder(sim); p.x=10; p.y=8.4f;
            Require(sim.IsBlocked(10,10),"closed door allowed entry");
            sim.Apply(Command(p,"interact",10,10));
            Require(!sim.IsBlocked(10,10),"E did not open door");
            sim.Apply(new PlayerCommand { playerId=p.id, action="move", moveY=1 }); Advance(sim,.5f);
            Require(p.y>9.7f,"player could not enter doorway");
            sim.Apply(new PlayerCommand { playerId=p.id, action="move" });
            sim.Apply(Command(p,"interact",10,10));
            Require(!sim.IsBlocked(10,10),"door closed on player");
            var restored=new WorldSimulator(Saved(sim.State));
            Require(!restored.IsBlocked(10,10),"open door setting/occupied player failed restore");
            p.x=10; p.y=11.5f; Advance(sim,.3f); sim.Apply(Command(p,"interact",10,10));
            Require(sim.IsBlocked(10,10),"vacant doorway could not close");
        });
        Run("door commands respect reach walls materials and invalid settings", () =>
        {
            var s=Flat(); s.version=WorldSimulator.SaveVersion; WorldGeneration.LegacyMetadata(s); var d=Add(s,BuildKind.Door,10,10);
            var sim=new WorldSimulator(s); var p=Builder(sim); p.x=10; p.y=6;
            sim.Apply(Command(p,"interact",10,10)); Require(Building(sim,d.id).setting==0,"remote door command accepted");
            p.x=10; p.y=8.5f; sim.Apply(Command(p,"configure",10,10,9));
            Require(Building(sim,d.id).setting==0,"invalid configure opened door");
            var invalid=Saved(sim.State); invalid.buildings.Single().setting=2; Reject(invalid,"invalid door setting restored");
            var builder=new WorldSimulator(Flat(era:TechEra.Primitive)); var actor=Builder(builder); actor.x=8;actor.y=10;
            var before=actor.items.ToArray(); builder.Apply(Command(actor,"build",10,10,(int)BuildKind.Door));
            Require(builder.State.buildings.Count==1,"primitive door not buildable");
            Require(actor.items[(int)ItemKind.Wood]<before[(int)ItemKind.Wood],"door was free");
        });
        Run("door cannot close on another survivor or living enemy and a wall blocks interaction", () =>
        {
            var s=Flat(); s.version=WorldSimulator.SaveVersion; WorldGeneration.LegacyMetadata(s);
            var door=Add(s,BuildKind.Door,10,10); door.setting=1;
            var sim=new WorldSimulator(s); var p=Builder(sim); p.x=10;p.y=8.5f;
            var q=sim.AddPlayer("guest","Guest");q.x=q.y=10;
            sim.Apply(Command(p,"interact",10,10));Require(Building(sim,door.id).setting==1,"closed on guest");
            q.x=15;q.y=15;Advance(sim,.3f);
            var z=new ZombieState {id=sim.State.nextId++,x=10,y=10};sim.State.zombies.Add(z);
            sim.Apply(Command(p,"configure",10,10,0));Require(Building(sim,door.id).setting==1,"closed on enemy");
            sim.State.zombies.Clear();Advance(sim,.3f);p.y=8;
            Add(sim.State,BuildKind.Wall,10,9);sim.Apply(Command(p,"interact",10,10));
            Require(Building(sim,door.id).setting==1,"used door through wall");
        });
        Run("new boiler is modular without shoreline and fuel goes into the separate chamber", () =>
        {
            var sim=new WorldSimulator(Flat(era:TechEra.Steam));var p=Builder(sim);p.x=8;p.y=10;
            p.items = DemoCatalog.BuildCost(BuildKind.Boiler);
            p.items[(int)ItemKind.Fuel] = 2;
            sim.Apply(Command(p,"build",10,10,(int)BuildKind.Boiler));
            var boiler=sim.State.buildings.Single();Require(boiler.modularBoiler,"new boiler silently retained old integrated burner");
            Advance(sim,.3f);int fuel=p.items[(int)ItemKind.Fuel];sim.Apply(Command(p,"interact",10,10));
            Require(p.items[(int)ItemKind.Fuel]==fuel && boiler.items[(int)ItemKind.Fuel]==0,"module boiler accepted firebox fuel");
            Near(boiler.powerGeneration,0,"incomplete boiler generated steam");
            var restored=new WorldSimulator(Saved(sim.State));Require(restored.State.buildings.Single().modularBoiler,"modular flag lost on save");
        });
        Run("steam engine converts steam to mechanical work and stops after input pipe removal", () =>
        {
            var s=Flat(era:TechEra.Steam);s.version=WorldSimulator.SaveVersion;WorldGeneration.LegacyMetadata(s);
            var oldBoiler=Add(s,BuildKind.Boiler,10,10);oldBoiler.items[(int)ItemKind.Fuel]=2;Water(s,10,12);
            var pipe=Add(s,BuildKind.SteamPipe,12,10);var engine=Add(s,BuildKind.SteamEngine,13,10);
            Add(s,BuildKind.Shaft,16,10);var crusher=Add(s,BuildKind.MechanicalCrusher,17,10);crusher.items[(int)ItemKind.Ore]=10;
            var sim=new WorldSimulator(s);Advance(sim,2.1f);
            Near(Building(sim,engine.id).powerGeneration,48,"steam engine conversion efficiency");
            Require(Building(sim,crusher.id).items[(int)ItemKind.CrushedOre]>0,"engine shaft did not power production");
            sim.State.buildings.Remove(Building(sim,pipe.id));Advance(sim,.1f);
            Near(Building(sim,engine.id).powerGeneration,0,"disconnected engine generated work");
            Require(!Building(sim,crusher.id).powered,"disconnected engine left phantom mechanical power");
        });
        Run("building catalog preserves old ordinals and exposes all modular footprints and costs", () =>
        {
            Require((int)BuildKind.CoolingTower==30 && (int)BuildKind.Door==31 && (int)BuildKind.SteamEngine==35,"serialized ordinals drifted");
            Require(DemoCatalog.BuildNames.Length==67 && DemoCatalog.BuildDescriptions.Length==DemoCatalog.BuildNames.Length,"catalog mismatch");
            foreach(BuildKind kind in Enum.GetValues<BuildKind>())
            {
                Require(DemoCatalog.BuildCost(kind).Sum()>0,"free or unknown building");
                for(int r=0;r<4;r++)Require(DemoCatalog.Width(kind,r)>0 && DemoCatalog.Height(kind,r)>0,"empty footprint");
            }
            Require(DemoCatalog.Width(BuildKind.SteamEngine)==3 && DemoCatalog.Height(BuildKind.SteamEngine)==2,"engine footprint");
            Require(DemoCatalog.Width(BuildKind.FeedwaterPump,1)==2 && DemoCatalog.Height(BuildKind.FeedwaterPump,1)==1,"pump rotation");
        });
        Run("enclosed steam factory draws feedwater through an intact exterior wall", () =>
        {
            foreach(bool nodeWall in new[] {false,true})
            {
                var s=Flat(era:TechEra.Steam);s.version=WorldSimulator.SaveVersion;WorldGeneration.LegacyMetadata(s);
                for(int y=6;y<=17;y++)for(int x=6;x<=17;x++)if(x==6 || x==17 || y==6 || y==17)
                {
                    if(x==12 && y==17)Add(s,BuildKind.Door,x,y);
                    else if(nodeWall)s.nodes.Add(new NodeState {id=s.nextId++,kind=NodeKind.Wall,x=x,y=y,stock=14,hp=160});
                    else Add(s,BuildKind.Wall,x,y);
                }
                var pump=Add(s,BuildKind.FeedwaterPump,10,7);Water(s,10,5);
                var boiler=Add(s,BuildKind.Boiler,10,9);boiler.modularBoiler=true;
                var chamber=Add(s,BuildKind.CombustionChamber,8,9);chamber.items[(int)ItemKind.Fuel]=2;
                Add(s,BuildKind.Chimney,12,9);
                var sim=new WorldSimulator(s);Advance(sim,.1f);
                Require(sim.Interiors.At(10,10)!=null,"factory shell was not enclosed");
                Require(TechCatalog.NearWater(sim.State,Building(sim,pump.id)),"wall intake did not reach water");
                Near(Building(sim,boiler.id).powerGeneration,120,"indoor factory did not produce steam");
                if(nodeWall)sim.State.nodes.RemoveAll(n=>n.x==10 && n.y==6);
                else sim.State.buildings.RemoveAll(b=>b.x==10 && b.y==6);
                Advance(sim,.1f);
                Require(sim.Interiors.At(10,10)==null,"breached wall retained room shelter");
                Near(Building(sim,boiler.id).powerGeneration,0,"missing wall intake acted as a free long water pipe");
            }
        });
        Run("weather shelter follows actual player walls and is lost on a breach", () =>
        {
            var sim=SurvivalFixture(out var p,s=>
            {
                HoldSurvivalWeather(s,15,1,1);
                for(int y=7;y<=13;y++)for(int x=5;x<=11;x++)
                    if(x==5 || x==11 || y==7 || y==13)Add(s,BuildKind.Wall,x,y);
            });
            Advance(sim,1);float sheltered=p.needs.wetness;
            Require(sheltered>0 && sheltered<2,"enclosed player did not receive shelter");
            sim.State.buildings.RemoveAll(b=>b.x==5 && b.y==10);Advance(sim,1);
            Require(sim.Interiors.At(8,10)==null && p.needs.wetness-sheltered>9,"breached player room retained site-based weather shelter");
        });
    }
}
