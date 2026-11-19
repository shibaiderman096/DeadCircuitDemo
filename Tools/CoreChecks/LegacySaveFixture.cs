using DeadCircuit;

static partial class Checks
{
    // Arrange a genuine pre-primitive input before taking the test's immutable
    // baseline. This edits only the fixture, never the restored simulation.
    static void PrepareLegacySaveFixture(WorldState state, int version, int? generatorVersion = null)
    {
        Require(version >= 4 && version <= 10, "legacy fixture version must use the 25-slot contract");
        state.version = version;
        state.primitiveVersion = 0; state.primitiveResearch = 0;
        int maxGenerator = version >= 10 ? 5 : version == 9 ? 4 : version == 8 ? 3 : version >= 7 ? 2 : 1;
        state.generatorVersion = generatorVersion ?? Math.Min(state.generatorVersion, maxGenerator);
        Require(state.generatorVersion >= 0 && state.generatorVersion <= maxGenerator, "legacy fixture generator is too new");
        state.produced = LegacyFixtureInventory(state.produced);
        foreach (var player in state.players) player.items = LegacyFixtureInventory(player.items);
        foreach (var building in state.buildings) building.items = LegacyFixtureInventory(building.items);
        state.nodes.RemoveAll(n => n.kind == NodeKind.Stone || n.kind == NodeKind.Clay);
        Require(state.buildings.All(b => (int)b.kind < (version < 7 ? 31 : 36)), "legacy fixture contains a future building");
        Require(state.cargo.All(c => c.item >= 0 && c.item < 25), "legacy fixture contains future cargo");
    }

    static int[] LegacyFixtureInventory(int[] items)
    {
        Require(items != null && (items.Length == 25 || items.Length == DemoCatalog.ItemCount), "invalid legacy fixture inventory source");
        return items.Take(25).ToArray();
    }

    // Construct the full expected migrated inventory, including every new zero
    // slot. Comparing this sequence keeps the original content assertion exact.
    static int[] ExpandedLegacyFixtureInventory(int[] items)
    {
        Require(items != null && (items.Length == 25 || items.Length == DemoCatalog.ItemCount), "invalid expected legacy inventory");
        var expanded = new int[DemoCatalog.ItemCount];
        Array.Copy(items, expanded, items.Length);
        return expanded;
    }
}
