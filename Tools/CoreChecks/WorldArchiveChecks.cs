using DeadCircuit;

static partial class Checks
{
    static WorldArchive ArchiveFixture(out string directory)
    {
        directory=Path.Combine(Environment.CurrentDirectory,"Logs","ArchiveChecks-"+Guid.NewGuid().ToString("N"));
        return new WorldArchive(directory,Dump,json=>System.Text.Json.JsonSerializer.Deserialize<WorldState>(json,Json));
    }
    static void WorldArchiveChecks()
    {
        Run("world archive creates independent worlds without overwriting either inventory",()=>
        {
            var archive=ArchiveFixture(out _);
            string a=Guid.NewGuid().ToString("N"),b=Guid.NewGuid().ToString("N");
            var first=New(out var p);p.items[(int)ItemKind.Wood]=73;first.State.generation.worldName="林间营地";
            archive.Write(a,first.State);
            var second=new WorldSimulator(new WorldGenerationSettings{seed=-19,worldName="荒野哨站"});
            archive.Write(b,second.State);
            Require(archive.List().Count==2,"new world replaced an existing save");
            Require(archive.Read(a).players.Single().items[(int)ItemKind.Wood]==73,"other world changed inventory");
            Require(archive.Read(b).seed==-19 && archive.Read(a).generation.worldName=="林间营地","world identities mixed");
        });
        Run("world archive rejected corrupt write preserves last good save and backup",()=>
        {
            var archive=ArchiveFixture(out _);string id=Guid.NewGuid().ToString("N");var sim=New(out var p);
            p.items[0]=20;archive.Write(id,sim.State);p.items[0]=40;archive.Write(id,sim.State);
            Require(File.Exists(archive.PathFor(id)+".bak"),"atomic replacement retained no previous good save");
            p.items[0]=-1;bool rejected=false;try{archive.Write(id,sim.State);}catch(ArgumentException){rejected=true;}
            Require(rejected && archive.Read(id).players.Single().items[0]==40,"invalid write damaged current save");
            Require(archive.ReadPath(archive.PathFor(id)+".bak").players.Single().items[0]==20,"backup is not the previous save");
        });
        Run("world archive ignores unreadable entries while retaining valid worlds",()=>
        {
            var archive=ArchiveFixture(out string root);string good=Guid.NewGuid().ToString("N"),bad=Guid.NewGuid().ToString("N");
            archive.Write(good,New(out _).State);File.WriteAllText(archive.PathFor(bad),"{broken");
            var entries=archive.List();Require(entries.Count==1 && entries[0].saveId==good && archive.UnreadableCount==1,"one corrupt file hid valid worlds");
            Require(entries[0].summary.Contains("73149") && entries[0].modified.Length>0,"load menu has no seed or timestamp");
        });
        Run("world archive rejects path traversal and oversized input",()=>
        {
            var archive=ArchiveFixture(out string root);
            foreach(string id in new[]{"../outside","legacy","","C:/outside",new string('g',32)})
            {bool rejected=false;try{archive.PathFor(id);}catch(ArgumentException){rejected=true;}Require(rejected,"unsafe save identity accepted");}
            Directory.CreateDirectory(root);string huge=Guid.NewGuid().ToString("N");
            using(var stream=File.Create(archive.PathFor(huge)))stream.SetLength(WorldArchive.MaxBytes+1L);
            bool oversized=false;try{archive.Read(huge);}catch(InvalidDataException){oversized=true;}Require(oversized,"oversized JSON read into memory");
        });
        Run("world archive migration leaves legacy file untouched",()=>
        {
            var archive=ArchiveFixture(out string root);Directory.CreateDirectory(root);string old=Path.Combine(root,"legacy-input.json");
            string json=Dump(LegacyFixture(2));File.WriteAllText(old,json);
            var listed=archive.List(old);Require(listed.Count==1 && listed[0].saveId=="legacy","legacy world omitted");
            string id=Guid.NewGuid().ToString("N");archive.Write(id,archive.ReadPath(old));
            Require(File.ReadAllText(old)==json && archive.Read(id).version==WorldSimulator.SaveVersion,"legacy migration overwrote old file");
        });
    }
}
