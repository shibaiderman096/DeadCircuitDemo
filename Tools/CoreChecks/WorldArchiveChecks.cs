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
        Run("world archive deletes only the requested world and its sidecars",()=>
        {
            var archive=ArchiveFixture(out _);
            string removed=Guid.NewGuid().ToString("N"),retained=Guid.NewGuid().ToString("N");
            var sim=New(out var p);p.items[0]=20;archive.Write(removed,sim.State);archive.Write(retained,sim.State);
            p.items[0]=40;archive.Write(removed,sim.State);archive.Write(retained,sim.State);
            string removedPath=archive.PathFor(removed),retainedPath=archive.PathFor(retained);
            File.WriteAllText(removedPath+".tmp","pending removal");
            File.WriteAllText(retainedPath+".tmp","pending write");

            archive.Delete(removed);

            Require(!File.Exists(removedPath) && !File.Exists(removedPath+".tmp") && !File.Exists(removedPath+".bak"),"deleted world left a save or sidecar");
            Require(archive.List().Single().saveId==retained,"archive did not retain only the other world");
            Require(archive.Read(retained).players.Single().items[0]==40 && archive.ReadPath(retainedPath+".bak").players.Single().items[0]==20,"other world or its backup changed");
            Require(File.ReadAllText(retainedPath+".tmp")=="pending write","other world's pending write changed");
        });
        Run("world archive deletion preserves a readable save if sidecar removal fails",()=>
        {
            var archive=ArchiveFixture(out _);string id=Guid.NewGuid().ToString("N");
            var sim=New(out var p);p.items[0]=40;archive.Write(id,sim.State);
            string blocked=archive.PathFor(id)+".bak";
            Directory.CreateDirectory(blocked);

            bool failed=false;
            try{archive.Delete(id);}catch(Exception e) when(e is IOException || e is UnauthorizedAccessException){failed=true;}

            Require(failed && Directory.Exists(blocked),"sidecar failure was ignored or removed a directory");
            Require(archive.Read(id).players.Single().items[0]==40,"sidecar failure removed or changed the main save");
        });
        Run("world archive deletion is idempotent and clears orphaned sidecars",()=>
        {
            var archive=ArchiveFixture(out string root);string id=Guid.NewGuid().ToString("N");
            archive.Delete(id);
            Require(!Directory.Exists(root),"deleting a missing world created its parent directory");

            Directory.CreateDirectory(root);
            string path=archive.PathFor(id);
            File.WriteAllText(path+".tmp","orphaned write");
            File.WriteAllText(path+".bak","orphaned backup");
            archive.Delete(id);
            archive.Delete(id);

            Require(!File.Exists(path+".tmp") && !File.Exists(path+".bak"),"missing main save left orphaned sidecars");
        });
        Run("world archive deletes a controlled path without matching other files",()=>
        {
            ArchiveFixture(out string root);Directory.CreateDirectory(root);
            string path=Path.Combine(root,"legacy-input.json"),retained=Path.Combine(root,"legacy-input-other.json");
            foreach(string suffix in new[]{"",".tmp",".bak"})File.WriteAllText(path+suffix,"fixture");
            File.WriteAllText(retained,"other fixture");

            WorldArchive.DeletePath(path);

            Require(!File.Exists(path) && !File.Exists(path+".tmp") && !File.Exists(path+".bak"),"controlled path left a save or sidecar");
            Require(File.ReadAllText(retained)=="other fixture","controlled path deletion touched a neighboring file");
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
            foreach(string id in new[]{"../outside","legacy","",null,"C:/outside",new string('g',32)})
            {
                bool rejected=false;try{archive.PathFor(id);}catch(ArgumentException){rejected=true;}Require(rejected,"unsafe save identity accepted");
                rejected=false;try{archive.Delete(id);}catch(ArgumentException){rejected=true;}Require(rejected,"delete accepted an unsafe save identity");
            }
            Directory.CreateDirectory(root);string huge=Guid.NewGuid().ToString("N");
            using(var stream=File.Create(archive.PathFor(huge)))stream.SetLength(WorldArchive.MaxBytes+1L);
            bool oversized=false;try{archive.Read(huge);}catch(InvalidDataException){oversized=true;}Require(oversized,"oversized JSON read into memory");
        });
        RunHistorical("world archive migration leaves legacy file untouched",()=>
        {
            var archive=ArchiveFixture(out string root);Directory.CreateDirectory(root);string old=Path.Combine(root,"legacy-input.json");
            string json=Dump(LegacyFixture(2));File.WriteAllText(old,json);
            var listed=archive.List(old);Require(listed.Count==1 && listed[0].saveId=="legacy","legacy world omitted");
            string id=Guid.NewGuid().ToString("N");archive.Write(id,archive.ReadPath(old));
            Require(File.ReadAllText(old)==json && archive.Read(id).version==WorldSimulator.SaveVersion,"legacy migration overwrote old file");
        });
    }
}
