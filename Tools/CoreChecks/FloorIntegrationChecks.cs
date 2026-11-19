using DeadCircuit;

static partial class Checks
{
    static void FloorIntegrationChecks()
    {
        Run("floors LAN snapshots and streaming follow indoor and outdoor players independently", () =>
        {
            var sim = FloorFixture(out var root, out var indoor);
            var outdoor = sim.AddPlayer("explorer", "Explorer");
            var world = sim.State;
            FloorInteract(sim, indoor, FloorStair(root, '<'));
            var floor = world.sites.Single(s => s.parentSiteId == root.id);
            var chunk = world.chunks.records.Single(c => c.interiorSiteId == floor.id);
            var indoors = WorldSnapshot.ForPlayer(world, indoor.id);
            var outdoors = WorldSnapshot.ForPlayer(world, outdoor.id);
            Require(indoors.chunks.records.Single().interiorSiteId == floor.id && indoors.sites.Any(s => s.id == root.id), "indoor snapshot lost floor or ground identity");
            Require(!outdoors.sites.Any(s => s.id == floor.id) && !outdoors.chunks.records.Any(c => c.interiorSiteId != 0), "outdoor snapshot leaked indoor map");
            Require(indoors.nodes.All(n => FloorWorld.IsInterior(world, n.x, n.y)), "outdoor entities leaked into floor snapshot");
            int records = world.chunks.records.Count;
            for (int i = 0; i < 4; i++) sim.Step(WorldSimulator.FixedStep);
            Require(world.chunks.records.Count == records + 4 && world.chunks.records.Count(c => c.interiorSiteId != 0) == 1, "indoor player stopped or redirected outdoor streaming");
            Require(!MapExploration.IsExplored(world, indoor, chunk.x * 16, chunk.y * 16), "interior void was revealed");
        });
        Run("floors reject forged parents chunks terrain and occupied stairs on restore", () =>
        {
            var sim = FloorFixture(out var root, out var player);
            FloorInteract(sim, player, FloorStair(root, '<'));
            var original = Saved(sim.State);
            var restored = new WorldSimulator(original).State;
            var restoredPlayer = restored.players.Single();
            Require((restoredPlayer.x, restoredPlayer.y) == (player.x, player.y), "restore moved a player out of their floor");
            foreach (string corruption in new[] { "parent", "chunk", "void", "level", "stairs", "settlement" })
            {
                var bad = Saved(original);
                var floor = bad.sites.Single(s => s.parentSiteId == root.id);
                var chunk = bad.chunks.records.Single(c => c.interiorSiteId == floor.id);
                switch (corruption)
                {
                    case "parent": floor.parentSiteId = floor.id; break;
                    case "chunk": chunk.interiorSiteId = root.id; break;
                    case "void": chunk.ground = "4" + chunk.ground.Substring(1); chunk.biomes = "0" + chunk.biomes.Substring(1); break;
                    case "level": floor.level = 3; break;
                    case "stairs":
                        var groundStair = FloorStair(root, '<');
                        Add(bad, BuildKind.Belt, groundStair.x, groundStair.y);
                        break;
                    case "settlement": bad.sites.Single(s => s.id == root.id).settlement = SettlementKind.Unspecified; break;
                }
                Reject(bad, "accepted corrupt floor " + corruption);
            }
        });
    }
}
