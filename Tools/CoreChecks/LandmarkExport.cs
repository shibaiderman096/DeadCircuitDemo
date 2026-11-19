using DeadCircuit;

static partial class Checks
{
    // Pure data export for offline visual review; never creates Unity objects.
    static void ExportLandmarks(string path)
    {
        var examples=new Dictionary<SiteKind,object>();
        for(int seed=0;seed<16 && examples.Count<32;seed++) for(int i=0;i<600;i++)
        {
            var world=LandmarkWorld(seed*971+11);int cx=i%30-18,cy=i/30-12;
            ChunkTerrain.Generate(world,cx,cy);
            foreach(var site in world.sites)
            {
                if(examples.ContainsKey(site.kind)) continue;
                examples.Add(site.kind,new {site,seed=world.seed,cx,cy,
                    name=WorldGenerationCatalog.Sites[(int)site.kind].name,
                    category=SiteBlueprintCatalog.Get(site.kind).category.ToString(),
                    description=SiteBlueprintCatalog.Get(site.kind).description,
                    floor=SiteBlueprintCatalog.Get(site.kind).floorArt,
                    cells=Enumerable.Range(0,site.height).Reverse().Select(y=>new string(Enumerable.Range(0,site.width).Select(x=>SiteBlueprintCatalog.At(site,site.x+x,site.y+y)).ToArray())).ToArray(),
                    world.chunks.records,world.nodes,world.buildings});
            }
        }
        Require(examples.Count==32,"visual export lacks generated building kinds");
        System.IO.File.WriteAllText(path,TerrainSerialize(examples.OrderBy(p=>p.Key).Select(p=>p.Value).ToArray()));
        Console.WriteLine("EXPORTED_32_GENERATED_LANDMARKS "+path);
    }
}
