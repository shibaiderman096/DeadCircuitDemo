using DeadCircuit;

static partial class Checks
{
    static WorldState LandmarkWorld(int seed = 73149)
    {
        var world = TerrainBlank(seed, WorldPreset.Urban, 2);
        world.generatorVersion = 4;
        return world;
    }

    static void LandmarkChecks()
    {
        Run("landmarks generators 3 and 4 keep their committed terrain fingerprints", () =>
        {
            // Compiled from d94b656 and 9cc7b4d, independently of generator 5.
            foreach (int version in new[] { 3, 4 })
            {
            var world=TerrainBlank(11,WorldPreset.Urban,2); world.generatorVersion=version;
            for(int x=-8;x<8;x++) ChunkTerrain.Generate(world,x,-5);
            var text=new System.Text.StringBuilder();
            foreach(var c in world.chunks.records) text.Append($"{c.x},{c.y}:{c.ground}:{c.biomes};");
            foreach(var n in world.nodes) text.Append($"N{n.id},{n.x},{n.y},{(int)n.kind},{n.stock};");
            foreach(var b in world.buildings) text.Append($"B{b.id},{b.x},{b.y},{(int)b.kind},{b.rotation};");
            foreach(var s in world.sites) text.Append($"S{s.id},{s.x},{s.y},{(int)s.kind},{(int)s.district},{s.width},{s.height},{s.doorX},{s.doorY};");
            string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text.ToString())));
            string expected = version == 3 ? "51BC357C6091249E71EDB53FF82B25B1AD6F467C8B0B16D36DBD0E1A659AAD91" :
                "BEBB24B7400782FDE36341ACEE3C032FB1EE10A25FFE6A2696780B6E96F0AE59";
            Require(hash==expected,$"generator {version} terrain, resources, layouts or id order changed");
            }
        });
        Run("landmarks all 32 kinds generate as reachable rotated real geometry", () =>
        {
            var kinds = new HashSet<SiteKind>(); var rotations = new HashSet<int>();
            int generated = 0, checkedSites = 0;
            for (int seed = 0; seed < 16 && kinds.Count < 32; seed++)
            for (int i = 0; i < 600; i++)
            {
                var world = LandmarkWorld(seed * 971 + 11);
                int cx = i % 30 - 18, cy = i / 30 - 12;
                Require(ChunkTerrain.Generate(world, cx, cy), "blueprint chunk rejected"); generated++;
                if (world.sites.Count == 0) continue;
                var site = world.sites.Single(); kinds.Add(site.kind); rotations.Add(site.rotation); checkedSites++;
                Require(site.layoutVersion == 1 && SiteBlueprintCatalog.ValidMetadata(site), "generated metadata is invalid");
                var blocked = world.nodes.Select(n => WorldTiles.Key(n.x, n.y)).ToHashSet();
                var reachable = TerrainFlood(world, cx, cy, blocked);
                Require(world.nodes.Count <= 75 && world.buildings.Count <= 5, "blueprint exceeds chunk budgets");
                for (int y = site.y; y < site.y + site.height; y++) for (int x = site.x; x < site.x + site.width; x++)
                {
                    char c = SiteBlueprintCatalog.At(site, x, y);
                    var node = world.nodes.SingleOrDefault(n => n.x == x && n.y == y);
                    var door = world.buildings.SingleOrDefault(b => b.x == x && b.y == y);
                    if (c == '#') Require(node?.kind == NodeKind.Wall && door == null, "missing real wall");
                    else if (c == '+') Require(door?.kind == BuildKind.Door && node == null && reachable.Contains(WorldTiles.Key(x, y)), "missing/unreachable real door");
                    else if (SiteBlueprintCatalog.Crates.Contains(c)) Require(node?.kind == NodeKind.Crate && TerrainNeighbor(reachable, x, y), "missing/unreachable crate");
                    else if (c != ' ') Require(node == null && door == null && reachable.Contains(WorldTiles.Key(x, y)), "furniture blocks a walkable floor");
                    if (c != ' ') Require(WorldTiles.Ground(world, x, y) == (c == ',' ? '1' : '4'), "blueprint floor differs from cell grammar");
                }
                foreach (var node in world.nodes.Where(n => n.kind != NodeKind.Wall))
                    Require(TerrainNeighbor(reachable, node.x, node.y), "natural scattering sealed a source");
            }
            Require(kinds.Count == 32, "missing generated kinds: " + string.Join(",", Enum.GetValues<SiteKind>().Except(kinds)));
            Require(rotations.Count == 4 && checkedSites > 100 && generated > checkedSites, "rotation/sample coverage incomplete");
        });
        Run("landmarks seed and signed coordinates are independent of visit order and entity ids", () =>
        {
            var a = LandmarkWorld(); var b = LandmarkWorld(); b.nextId += 10000;
            var cells = new[] { (-8,-5), (4,-7), (-2,10), (10,10), (-16,13), (14,-19) };
            foreach (var (x,y) in cells) ChunkTerrain.Generate(a,x,y);
            foreach (var (x,y) in cells.Reverse()) ChunkTerrain.Generate(b,x,y);
            Require(TerrainEntityDump(a) == TerrainEntityDump(b) && TerrainMapDump(a) == TerrainMapDump(b), "blueprint depends on discovery order");
            var signatureA = a.sites.OrderBy(s=>s.x).Select(s=>(s.x,s.y,s.layoutVersion,s.rotation));
            var signatureB = b.sites.OrderBy(s=>s.x).Select(s=>(s.x,s.y,s.layoutVersion,s.rotation));
            Require(signatureA.SequenceEqual(signatureB), "layout metadata depends on entity ids");
        });
        Run("landmarks v8 generator3 worlds keep old chunks and future legacy layouts", () =>
        {
            var old = WorldGeneration.Generate(new WorldGenerationSettings { seed=11,chunked=true });
            PrepareLegacySaveFixture(old, 8, 3);
            var source = Saved(old); string before = Dump(source);
            var restored = new WorldSimulator(source).State;
            Require(restored.version == WorldSimulator.SaveVersion && restored.generatorVersion == 3 && Dump(source) == before, "v8 migration changed generation or caller");
            for(int x=-8;x<8;x++) { ChunkTerrain.Generate(old,x,-5); ChunkTerrain.Generate(restored,x,-5); }
            Require(restored.sites.Any(s=>s.y<0) && restored.sites.All(s=>s.layoutVersion==0 && (int)s.kind<12), "legacy world received new plans");
            var expected = Saved(old);
            foreach (var building in expected.buildings) building.items = ExpandedLegacyFixtureInventory(building.items);
            Require(TerrainEntityDump(expected)==TerrainEntityDump(restored) && TerrainMapDump(old)==TerrainMapDump(restored), "legacy future generation changed after version migration");
        });
        Run("landmarks typed containers pay once on depletion and survive partial saves", () =>
        {
            foreach(char profile in SiteBlueprintCatalog.Crates)
            {
                var sim = LandmarkLootFixture(profile, out var player, out var node);
                int item = profile=='A' ? (int)ItemKind.Ammo : profile=='T' ? (int)ItemKind.Scrap :
                    profile=='E' ? (int)ItemKind.Circuit : profile=='B' ? (int)ItemKind.Wood :
                    profile=='M' || profile=='K' ? (int)ItemKind.Polymer : (int)ItemKind.Food;
                var inventory = (int[])player.items.Clone();
                sim.Apply(Command(player,"interact",node.x,node.y));
                Require(node.stock==1 && player.items[item]==inventory[item]+3 && player.needs.foods.Sum()==0 && player.needs.seeds.Sum()==0,"partial typed crate reward");
                var restored = new WorldSimulator(Saved(sim.State)); var actor=restored.State.players.Single();
                // Refresh cooldown without advancing the streaming frontier or survival clock.
                actor.interactCooldown=0;
                restored.Apply(Command(actor,"interact",node.x,node.y));
                Require(!restored.State.nodes.Any(n=>n.id==node.id) && actor.items[item]==inventory[item]+4,"typed inventory/depletion changed on restore");
                if(profile=='F') Require(actor.needs.foods[(int)FoodKind.Bread]>=2,"food crate lacks food");
                if(profile=='M') Require(actor.needs.foods[(int)FoodKind.Herb]>=3 && actor.needs.waterClean==2,"medical crate lacks herb/water");
                if(profile=='S') Require(actor.needs.seeds.Sum()>=5,"seed crate lacks seeds");
                var exhausted=new WorldSimulator(Saved(restored.State)); var p=exhausted.State.players.Single();
                string after=TerrainSerialize(new {p.items,p.needs.foods,p.needs.seeds,p.needs.waterClean});
                p.interactCooldown=0; exhausted.Apply(Command(p,"interact",node.x,node.y));
                Require(after==TerrainSerialize(new {p.items,p.needs.foods,p.needs.seeds,p.needs.waterClean}),"exhausted crate refilled/rewarded");
            }
        });
        Run("landmarks validate plan metadata yet preserve destroyed walls doors and depleted crates", () =>
        {
            var sim=LandmarkLootFixture('M',out var player,out var node); var site=sim.State.sites.Single(s=>s.layoutVersion==1);
            var wall=sim.State.nodes.First(n=>n.kind==NodeKind.Wall && n.x>=site.x && n.x<site.x+site.width && n.y>=site.y && n.y<site.y+site.height);
            sim.State.nodes.Remove(wall); sim.State.nodes.Remove(node);
            sim.State.buildings.RemoveAll(b=>b.kind==BuildKind.Door && b.x==site.doorX && b.y==site.doorY);
            var saved=Saved(sim.State); var copy=new WorldSimulator(saved).State;
            Require(!copy.nodes.Any(n=>n.id==wall.id || n.id==node.id) && !copy.buildings.Any(b=>b.x==site.doorX && b.y==site.doorY),"restore reconstructed a damaged plan");
            Require(!ReferenceEquals(copy.sites.First(),saved.sites.First()),"site metadata was not deep copied");
            foreach(string mode in new[]{"rotation","layout","size","kind","door","oldVersion"})
            {
                var bad=Saved(copy); var s=bad.sites.Single(s=>s.layoutVersion==1);
                if(mode=="rotation") s.rotation=4;
                if(mode=="layout") s.layoutVersion=2;
                if(mode=="size") s.width++;
                if(mode=="kind") s.kind=(SiteKind)32;
                if(mode=="door") { s.doorX=s.x;s.doorY=s.y; }
                if(mode=="oldVersion") PrepareLegacySaveFixture(bad,8,3);
                Reject(bad,"invalid plan metadata accepted: "+mode);
            }
            var snapshot=WorldSnapshot.ForPlayer(copy,player.id);
            Require(snapshot.sites.Any(s=>s.id==site.id && s.layoutVersion==1 && s.rotation==site.rotation),"LAN snapshot lost layout metadata");
        });
        Run("landmarks site budget failure leaves no partial geometry or consumed ids", () =>
        {
            var found=LandmarkFind('A'); var world=LandmarkWorld(found.seed);
            for(int i=0;i<WorldSimulator.MaxSites;i++) world.sites.Add(new SiteState());
            int id=world.nextId;
            Require(!ChunkTerrain.Generate(world,found.cx,found.cy),"site budget accepted");
            Require(world.nextId==id && world.nodes.Count==0 && world.buildings.Count==0 && world.chunks.records.Count==0 && world.chunks.revision==0,"refusal partially committed blueprint");
        });
    }

    static (int seed,int cx,int cy,WorldState world) LandmarkFind(char profile)
    {
        for(int i=0;i<1200;i++)
        {
            var world=LandmarkWorld(); int cx=i%40-20,cy=i/40-15;
            ChunkTerrain.Generate(world,cx,cy);
            if(world.nodes.Any(n=>n.kind==NodeKind.Crate && SiteBlueprintCatalog.CrateAt(world,n.x,n.y)==profile)) return(world.seed,cx,cy,world);
        }
        throw new Exception("no typed crate fixture: "+profile);
    }

    static WorldSimulator LandmarkLootFixture(char profile,out PlayerState player,out NodeState node)
    {
        var found=LandmarkFind(profile);
        var world=WorldGeneration.Generate(new WorldGenerationSettings {seed=found.seed,chunked=true,preset=WorldPreset.Urban,cityDensity=2});
        world.generatorVersion=4; // LandmarkFind searches the frozen gen4 layouts.
        Require(!ChunkStreaming.IsGenerated(world,found.cx*16,found.cy*16),"fixture falls in tutorial");
        ChunkTerrain.Generate(world,found.cx,found.cy);
        var sim=new WorldSimulator(world); player=sim.AddPlayer("landmark-looter","探索者");
        node=sim.State.nodes.First(n=>n.kind==NodeKind.Crate && SiteBlueprintCatalog.CrateAt(sim.State,n.x,n.y)==profile);node.stock=4;
        player.activeSlot=2;Array.Fill(player.items,0);Array.Fill(player.needs.foods,0);Array.Fill(player.needs.seeds,0);player.needs.waterClean=0;
        var blocked=sim.State.nodes.Select(n=>WorldTiles.Key(n.x,n.y)).ToHashSet();
        foreach(var b in sim.State.buildings) blocked.Add(WorldTiles.Key(b.x,b.y));
        foreach(var (dx,dy) in new[]{(-1,0),(1,0),(0,-1),(0,1)})
            if(!blocked.Contains(WorldTiles.Key(node.x+dx,node.y+dy))) {player.x=node.x+dx;player.y=node.y+dy;break;}
        Require(Math.Abs(player.x-node.x)+Math.Abs(player.y-node.y)==1,"crate has no free interaction neighbor");
        return sim;
    }
}
