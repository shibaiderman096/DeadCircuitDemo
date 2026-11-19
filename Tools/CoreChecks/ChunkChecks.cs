using DeadCircuit;

static partial class Checks
{
    static WorldChunkState InfiniteClearChunk(WorldState s,int cx,int cy)
    {
        ChunkStreaming.GenerateChunk(s,cx,cy);
        var record=s.chunks.records.Single(c=>c.x==cx && c.y==cy);
        record.ground=new string('4',256);record.biomes=new string('0',256);s.chunks.revision++;
        s.nodes.RemoveAll(n=>WorldTiles.FloorDiv(n.x,16)==cx && WorldTiles.FloorDiv(n.y,16)==cy);
        s.buildings.RemoveAll(b=>WorldTiles.FloorDiv(b.x,16)==cx && WorldTiles.FloorDiv(b.y,16)==cy);
        return record;
    }
    static void ChunkChecks()
    {
        Run("chunk budget refusal announces once and resumes after budget changes", () =>
        {
            var sim=new WorldSimulator(new WorldGenerationSettings {seed=11,chunked=true});sim.AddPlayer("p","P");
            sim.State.nextId=int.MaxValue-2;
            int count=sim.State.chunks.records.Count;
            Require(!ChunkStreaming.Step(sim.State) && sim.State.chunks.records.Count==count,"exhausted ID budget generated chunk");
            Require(sim.State.announcement.Contains("预算"),"budget refusal has no player explanation");
            sim.State.announcement="another event";
            for(int i=0;i<20;i++) Require(!ChunkStreaming.Step(sim.State),"blocked chunk retried as successful");
            Require(sim.State.announcement=="another event","budget refusal replaced announcements every tick");
            sim.State.nextId=20000000;
            Require(ChunkStreaming.Step(sim.State) && sim.State.chunks.records.Count==count+1,"budget release did not resume streaming");
        });
        Run("generated chunk at final usable entity IDs remains restorable", () =>
        {
            var s=WorldGeneration.Generate(new WorldGenerationSettings {seed=11,chunked=true});
            var probe=Saved(s);int before=probe.nextId;
            Require(ChunkStreaming.GenerateChunk(probe,-1,-1),"reference chunk missing");
            int needed=probe.nextId-before;Require(needed>0,"reference chunk empty");
            s.nextId=int.MaxValue-2-needed;
            Require(ChunkStreaming.GenerateChunk(s,-1,-1),"last valid generation rejected");
            _=new WorldSimulator(Saved(s));
            var invalid=WorldGeneration.Generate(new WorldGenerationSettings {seed=11,chunked=true});
            invalid.nextId=int.MaxValue-1-needed;string json=Dump(invalid);
            Require(!ChunkStreaming.GenerateChunk(invalid,-1,-1) && Dump(invalid)==json,"generation consumed reserved terminal ID");
        });
        Run("signed outermost cells accept actor save and restore", () =>
        {
            var s=WorldGeneration.Generate(new WorldGenerationSettings {seed=11,chunked=true});
            InfiniteClearChunk(s,-65536,-65536);InfiniteClearChunk(s,65535,65535);
            var sim=new WorldSimulator(s);var p=sim.AddPlayer("p","P");
            p.x=p.y=-1048576;MapExploration.Reveal(sim.State,p);
            var restored=new WorldSimulator(Saved(sim.State));
            Require(restored.State.players[0].x==-1048576,"negative outermost cell lost");
            p.x=p.y=1048575;_=new WorldSimulator(Saved(sim.State));
        });
        Run("infinite chunk world starts partial and extends beyond 256 in all directions", () =>
        {
            var sim=new WorldSimulator(new WorldGenerationSettings {seed=11,chunked=true});var p=sim.AddPlayer("p","P");
            Require(sim.State.chunks.records.Count==16,"new world pre-generated distant land");
            Near(p.x,32,"original tutorial spawn x");Near(p.y,35,"original tutorial spawn y");
            Require(!sim.IsBlocked((int)p.x,(int)p.y) && sim.IsBlocked(-1000,-1000),"spawn or unknown collision");
            foreach(var coord in new[] {(-300,400),(1000,-2000),(-3000,-4000),(-3000,-5000),(4000,5000)})
            {
                if(FloorWorld.IsReservedChunk(coord.Item1,coord.Item2))
                {
                    Require(!ChunkStreaming.GenerateChunk(sim.State,coord.Item1,coord.Item2),"outdoor generation entered the indoor reserve");
                    continue;
                }
                Require(ChunkStreaming.GenerateChunk(sim.State,coord.Item1,coord.Item2),"distant generation blocked by finite map");
                Require(ChunkStreaming.IsGenerated(sim.State,coord.Item1*16,coord.Item2*16),"distant chunk not recorded");
                Require(WorldTiles.Ground(sim.State,coord.Item1*16,coord.Item2*16)!='?',"distant terrain missing");
            }
            Require(!MapExploration.IsExplored(sim.State,p,-1000,-1000),"unvisited land revealed");
            _=new WorldSimulator(Saved(sim.State));
        });
        Run("chunk generation advances once per host tick and stops for disconnected players", () =>
        {
            var sim=new WorldSimulator(new WorldGenerationSettings {seed=11,chunked=true});var p=sim.AddPlayer("p","P");
            int before=sim.State.chunks.records.Count;sim.Step(.05f);
            Require(sim.State.chunks.records.Count==before+1,"tick did not generate exactly one");
            sim.SetConnected(p.id,false);before=sim.State.chunks.records.Count;Advance(sim,.5f);
            Require(sim.State.chunks.records.Count==before,"disconnected player generated map");
        });
        Run("chunk visits cannot replenish mined resources or reset machines", () =>
        {
            var sim=new WorldSimulator(new WorldGenerationSettings {seed=631,chunked=true});sim.AddPlayer("p","P");
            for(int cx=-1;cx>=-6;cx--) ChunkStreaming.GenerateChunk(sim.State,cx,-2);
            var node=sim.State.nodes.First(n=>n.x<0);int removed=node.id;sim.State.nodes.Remove(node);
            var chest=sim.State.buildings.First(b=>b.kind==BuildKind.Chest);chest.items[0]=17;
            var restored=new WorldSimulator(Saved(sim.State));
            Require(!ChunkStreaming.GenerateChunk(restored.State,WorldTiles.FloorDiv(node.x,16),WorldTiles.FloorDiv(node.y,16)),"visited chunk regenerated");
            Require(restored.State.nodes.All(n=>n.id!=removed) && Building(restored,chest.id).items[0]==17,"mined stock or inventory reset");
            Require(!ReferenceEquals(restored.State.chunks.records,sim.State.chunks.records) && !ReferenceEquals(restored.State.chunks.records[0],sim.State.chunks.records[0]),"chunk save aliases source");
        });
        Run("signed sparse collision keys do not alias across old row major boundaries", () =>
        {
            var s=WorldGeneration.Generate(new WorldGenerationSettings {seed=7,chunked=true});
            InfiniteClearChunk(s,-1,0);InfiniteClearChunk(s,3,-1);
            s.nodes.Add(new NodeState {id=s.nextId++,x=-1,y=0,kind=NodeKind.Ore});
            var sim=new WorldSimulator(s);
            Require(WorldTiles.Key(-1,0)!=WorldTiles.Key(63,-1),"coordinate keys collide");
            Require(sim.IsBlocked(-1,0) && !sim.IsBlocked(63,-1),"sparse collision leaked across unrelated cells");
            Require(WorldTiles.FloorDiv(-1,16)==-1 && WorldTiles.FloorDiv(-16,16)==-1 && WorldTiles.FloorDiv(-17,16)==-2,"negative chunk division");
        });
        Run("map fog persists signed discovery and hides distant and unknown entities", () =>
        {
            var sim=new WorldSimulator(new WorldGenerationSettings {seed=21,chunked=true});var p=sim.AddPlayer("p","P");
            int x=(int)p.x,y=(int)p.y;Require(MapExploration.IsExplored(sim.State,p,x,y),"spawn fog");
            InfiniteClearChunk(sim.State,-50,80);p.x=-50*16+8;p.y=80*16+8;MapExploration.Reveal(sim.State,p);
            Require(MapExploration.IsExplored(sim.State,p,x,y) && !MapExploration.IsVisible(sim.State,p,x,y),"history or current visibility wrong");
            Require(MapExploration.IsExplored(sim.State,p,(int)p.x,(int)p.y) && !MapExploration.IsVisible(sim.State,p,0,-500),"signed discovery failed");
            var restored=new WorldSimulator(Saved(sim.State));var rp=restored.State.players.Single();
            Require(MapExploration.IsExplored(restored.State,rp,(int)p.x,(int)p.y),"signed fog lost on restore");
            Require(!ReferenceEquals(rp.exploredChunks,p.exploredChunks) && !ReferenceEquals(rp.exploredChunks[0],p.exploredChunks[0]),"fog save aliases source");
            var second=restored.AddPlayer("other","O");Require(!MapExploration.IsExplored(restored.State,second,(int)p.x,(int)p.y),"another player inherited fog");
            string previous=Dump(sim.State);p.hp=0;MapExploration.Reveal(sim.State,p);Require(!MapExploration.IsVisible(sim.State,p,(int)p.x,(int)p.y),"dead player vision");
        });
        Run("infinite movement crosses zero and cell boundaries without a map clamp", () =>
        {
            var sim=new WorldSimulator(new WorldGenerationSettings {seed=8,chunked=true});var p=sim.AddPlayer("p","P");
            InfiniteClearChunk(sim.State,0,0);InfiniteClearChunk(sim.State,-1,0);p.x=.7f;p.y=8;
            sim.Apply(new PlayerCommand {playerId=p.id,action="move",moveX=-1});Advance(sim,1);
            Require(p.x< -2 && p.x> -4 && Math.Abs(p.y-8)<.01,"player hit zero map edge");
            Require(MapExploration.IsExplored(sim.State,p,-1,8),"negative movement did not reveal fog");
            _=new WorldSimulator(Saved(sim.State));
        });
        Run("offscreen automation preserves in flight cargo and keeps producing", () =>
        {
            var s=WorldGeneration.Generate(new WorldGenerationSettings {seed=17,chunked=true});
            InfiniteClearChunk(s,-100,50);
            var from=Add(s,BuildKind.Chest,-1598,805);from.items[0]=1;
            Add(s,BuildKind.Belt,-1597,805);var to=Add(s,BuildKind.Chest,-1596,805);
            var sim=new WorldSimulator(s);sim.AddPlayer("p","P");
            Require(!ChunkStreaming.IsActive(sim.State,from.x,from.y),"far factory still active for graphics");
            var flight=AwaitCargo(sim,from.id,sim.State.buildings.Single(b=>b.x==-1597).id);
            var restored=new WorldSimulator(Saved(sim.State));
            Near(restored.State.cargo.Single().progress,flight.progress,"save reset cargo progress");
            Advance(restored,5);
            Require(Building(restored,to.id).items[0]==1 && Building(restored,from.id).items[0]==0 && restored.State.cargo.Count==0,"unloaded view stopped or duplicated automation");
            Require(!ChunkStreaming.GenerateChunk(restored.State,-100,50),"factory chunk was regenerated");
        });
        Run("chunk save rejects duplicates unknown entities and malformed exploration", () =>
        {
            var sim=new WorldSimulator(new WorldGenerationSettings {seed=1,chunked=true});sim.AddPlayer("p","P");
            var s=Saved(sim.State);s.chunks.records.Add(s.chunks.records[0]);Reject(s,"duplicate chunks accepted");
            s=Saved(sim.State);s.nodes.Add(new NodeState {id=s.nextId++,x=-9999,y=9999});Reject(s,"node in unknown accepted");
            s=Saved(sim.State);s.players[0].exploredChunks[0].bits="ff";Reject(s,"short exploration accepted");
            s=Saved(sim.State);s.players[0].exploredChunks[0].x=-9999;Reject(s,"explored unknown accepted");
            s=Saved(sim.State);s.chunks.enabled=false;Reject(s,"infinite data masquerades as old64");
            s=Saved(sim.State);s.chunks.records[0].x=int.MaxValue;Reject(s,"overflow chunk accepted");
        });
        Run("LAN snapshot remains local while two players explore distant signed regions", () =>
        {
            var sim=new WorldSimulator(new WorldGenerationSettings {seed=14,chunked=true});var p=sim.AddPlayer("p","P");var other=sim.AddPlayer("other","O");
            InfiniteClearChunk(sim.State,1000,-1000);other.x=16008;other.y=-15992;MapExploration.Reveal(sim.State,other);
            for(int i=0;i<15;i++) ChunkStreaming.Step(sim.State);
            string before=Dump(sim.State);var first=WorldSnapshot.ForPlayer(sim.State,p.id);var second=WorldSnapshot.ForPlayer(sim.State,other.id);
            Require(Dump(sim.State)==before,"snapshot mutated authority");
            Require(first.chunks.records.All(c=>Math.Abs(c.x-2)<=4 && Math.Abs(c.y-2)<=4),"first client received far chunks");
            Require(second.chunks.records.All(c=>c.x>990 && c.y<-990) && second.chunks.records.Count>0,"remote client got origin map");
            Require(first.nodes.All(n=>n.x<200) && second.nodes.All(n=>n.x>15000),"unbounded entity snapshot");
            Require(System.Text.Encoding.UTF8.GetByteCount(Dump(second))<1024*1024,"local snapshot exceeds budget");
            Require(second.players.Find(q=>q.id==p.id).exploredChunks.Count==0,"other player's fog leaked");
        });
        Run("old v7 save retains original cells and acquires no distant world", () =>
        {
            var original=new WorldSimulator(55);original.AddPlayer("p","P");var old=Saved(original.State);old.version=7;old.generatorVersion=2;
            string ground=old.ground;int next=old.nextId;var sim=new WorldSimulator(old);
            Require(sim.State.width==64 && sim.State.ground==ground && sim.State.nextId==next && !ChunkStreaming.Enabled(sim.State),"legacy world regenerated");
            sim.Step(.05f);Require(sim.IsBlocked(64,30),"legacy map silently expanded");
        });
    }
}
