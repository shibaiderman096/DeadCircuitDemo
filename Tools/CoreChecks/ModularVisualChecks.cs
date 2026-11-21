using DeadCircuit;

static partial class Checks
{
    static BuildingState VisualModules(int id, params (int x, int y)[] cells) => new()
    {
        id = id, kind = BuildKind.LargeOreProcessor,
        modularModules = cells.Select(p => new MachineModuleState { x = p.x, y = p.y }).ToList()
    };

    static void ModularVisualChecks()
    {
        Run("modular visual cardinal joins are reciprocal and ignore independent role rotations", () =>
        {
            var b = VisualModules(1, (0, 0), (1, 0), (0, 1), (-1, 0), (0, -1));
            b.modularModules[0].role = MachineModuleRole.Controller;
            for (int i = 0; i < b.modularModules.Count; i++) b.modularModules[i].rotation = i & 3;
            Require(ModularVisualLayout.ConnectionMask(b, 0) == 15, "centre did not connect on all four sides");
            Require(ModularVisualLayout.ConnectionMask(b, 1) == 4, "east cell must join west");
            Require(ModularVisualLayout.ConnectionMask(b, 2) == 8, "north cell must join south");
            Require(ModularVisualLayout.ConnectionMask(b, 3) == 1, "west cell must join east");
            Require(ModularVisualLayout.ConnectionMask(b, 4) == 2, "south cell must join north");
            Require(ModularVisualLayout.ConnectionMask(b, -1) == 0 &&
                ModularVisualLayout.ConnectionMask(b, 5) == 0, "invalid module acquired joins");
        });
        Run("modular visual sparse L U holes and diagonals never acquire bridging joins", () =>
        {
            var b = VisualModules(2, (0, 0), (1, 0), (2, 0), (0, 1), (2, 1), (0, 2), (2, 2));
            Require(ModularVisualLayout.ConnectionMask(b, 3) == 10 &&
                ModularVisualLayout.ConnectionMask(b, 4) == 10, "U side connected across empty centre");
            Require(ModularVisualLayout.ConnectionMask(b, 5) == 8 &&
                ModularVisualLayout.ConnectionMask(b, 6) == 8, "U tips acquired a horizontal bridge");
            var l = VisualModules(3, (0, 0), (1, 0), (0, 1));
            Require(ModularVisualLayout.ConnectionMask(l, 1) == 4 &&
                ModularVisualLayout.ConnectionMask(l, 2) == 8, "L inside corner connected diagonally");
            var diagonal = VisualModules(4, (0, 0), (1, 1), (2, 0));
            for (int i = 0; i < 3; i++) Require(ModularVisualLayout.ConnectionMask(diagonal, i) == 0, "diagonal or gap bridged");
            Require(!DemoCatalog.Contains(b, 1, 1) && !DemoCatalog.Contains(l, 1, 1), "visual query filled a sparse hole");
        });
        Run("modular visual adjacent different building ids and nested groups stay separate", () =>
        {
            var a = VisualModules(10, (0, 0), (1, 0));
            var b = VisualModules(11, (2, 0), (2, 1));
            Require(a.kind == b.kind && a.id != b.id, "fixture must cover same-kind different-group machines");
            Require(ModularVisualLayout.ConnectionMask(a, 1) == 4, "first group joined adjacent foreign cell");
            Require(ModularVisualLayout.ConnectionMask(b, 0) == 2, "second group joined adjacent foreign cell");
            var outer = VisualModules(12, (-1, 0), (-1, 1), (0, 1), (1, 1), (1, 0));
            var nested = VisualModules(13, (0, 0));
            Require(ModularVisualLayout.ConnectionMask(nested, 0) == 0, "nested foreign controller joined enclosing group");
            Require((ModularVisualLayout.ConnectionMask(outer, 0) & 1) == 0 &&
                (ModularVisualLayout.ConnectionMask(outer, 2) & 8) == 0, "enclosing group linked across foreign controller");
        });
        Run("modular visual negative chunk edges and integer limits retain exact neighbours", () =>
        {
            foreach (int x in new[] { -17, -16, -1, 0 })
            {
                var b = VisualModules(20, (x, -16), (x + 1, -16), (x, -15));
                Require(ModularVisualLayout.ConnectionMask(b, 0) == 3 &&
                    ModularVisualLayout.ConnectionMask(b, 1) == 4, "negative coordinate/chunk boundary broke joins");
                var bounds = ModularVisualLayout.FrameBounds(b);
                Near(bounds.Left, x - .62f, "negative frame left");
                Near(bounds.Right, x + 1.62f, "negative frame right");
                Near(bounds.Bottom, -16.62f, "negative frame bottom");
                Near(bounds.Top, -14.38f, "negative frame top");
            }
            var limits = VisualModules(21, (int.MaxValue, 0), (int.MinValue, 0), (0, int.MaxValue), (0, int.MinValue));
            for (int i = 0; i < 4; i++) Require(ModularVisualLayout.ConnectionMask(limits, i) == 0, "coordinate overflow linked opposite integer limits");
        });
        Run("modular visual erasing middle and extremity refreshes joins and group frame", () =>
        {
            var b = VisualModules(30, (-2, 0), (-1, 0), (0, 0), (0, 1));
            var before = ModularVisualLayout.FrameBounds(b);
            Require(ModularVisualLayout.ConnectionMask(b, 1) == 5, "initial interior join missing");
            b.modularModules.RemoveAt(1);
            Require(ModularVisualLayout.ConnectionMask(b, 0) == 0 &&
                ModularVisualLayout.ConnectionMask(b, 1) == 2, "erased middle left a bridge");
            b.modularModules.RemoveAt(0);
            var after = ModularVisualLayout.FrameBounds(b);
            Near(after.Left, -.62f, "frame failed to shrink after erasing extreme cell");
            Require(after.Left > before.Left, "frame retained stale group bounds");
            b.modularModules[1].y = -1; // Same-count edit must not retain a north connection.
            Require(ModularVisualLayout.ConnectionMask(b, 0) == 8, "same-count edit retained cached north join");
            Near(ModularVisualLayout.FrameBounds(b).Top, .62f, "same-count frame edit stayed stale");
            b.modularModules.Clear();
            Require(!ModularVisualLayout.FrameBounds(b).HasCells, "empty group retained a frame");
        });
        Run("modular visual bbox encloses actual cells with padding and handles single cells", () =>
        {
            var b = VisualModules(40, (-4, -2), (-2, -2), (-2, 1));
            b.x = 100; b.y = 200; // Layout derives extents from actual cells, not cached metadata.
            string before = System.Text.Json.JsonSerializer.Serialize(b, Json);
            var bounds = ModularVisualLayout.FrameBounds(b);
            Require(bounds.HasCells, "nonempty machine has no frame");
            Near(bounds.Left, -4.62f, "sparse bbox left"); Near(bounds.Right, -1.38f, "sparse bbox right");
            Near(bounds.Bottom, -2.62f, "sparse bbox bottom"); Near(bounds.Top, 1.62f, "sparse bbox top");
            foreach (var m in b.modularModules)
                Require(bounds.Left < m.x - .5f && bounds.Right > m.x + .5f &&
                    bounds.Bottom < m.y - .5f && bounds.Top > m.y + .5f, "frame does not enclose a cell");
            Require(System.Text.Json.JsonSerializer.Serialize(b, Json) == before, "presentation geometry mutated state");
            var single = ModularVisualLayout.FrameBounds(VisualModules(41, (-1, 0)));
            Near(single.Right - single.Left, 1f + 2f * ModularVisualLayout.FramePadding, "single-cell horizontal annotation");
            Near(single.Top - single.Bottom, 1f + 2f * ModularVisualLayout.FramePadding, "single-cell vertical annotation");
            Require(!ModularVisualLayout.FrameBounds(null).HasCells &&
                ModularVisualLayout.ConnectionMask(null, 0) == 0, "null group exposes geometry");
            var invalid = VisualModules(42); invalid.modularModules.Add(null);
            Require(!ModularVisualLayout.FrameBounds(invalid).HasCells &&
                ModularVisualLayout.ConnectionMask(invalid, 0) == 0, "null module exposes geometry");
        });
    }
}
