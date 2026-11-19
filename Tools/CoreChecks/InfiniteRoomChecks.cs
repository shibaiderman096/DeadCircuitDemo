using DeadCircuit;

static partial class Checks
{
    // Main wires this suite into the runner; exercise the public topology API.
    internal static void InfiniteRoomChecks()
    {
        Run("infinite interior a negative-coordinate rectangle shelters its dry floor", () =>
        {
            var world = InfiniteRoomBox(-12, -10);
            var layout = RoomLayout.Build(world);
            Require(layout.rooms.Count == 1 && layout.At(-10, -8) != null, "signed room was clipped by legacy map bounds");
            var room = layout.At(-10, -8);
            Require(room.cells.Length == 0 && room.cellKeys.Length == 9 && room.cellKeys.Distinct().Count() == 9, "infinite room did not expose nine unique global floor keys");
            Require(room.cellKeys.Contains(WorldTiles.Key(-11, -9)) && room.cellKeys.Contains(WorldTiles.Key(-9, -7)), "global floor key translation lost a corner");
            Require(layout.At(-12, -8) == null && layout.At(-10, -10) == null && layout.At(-13, -8) == null, "wall, door or outside counted as floor");
        });
        Run("infinite interior four-neighbor floor is enclosed by diagonal-touching wall fragments", () =>
        {
            var world = Flat(); world.chunks.enabled = true;
            InfiniteRoomFloor(world, -2, -2, 5, 5);
            foreach (var point in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) }) InteriorWall(world, point.Item1, point.Item2);
            var layout = RoomLayout.Build(world);
            Require(layout.rooms.Count == 1 && layout.At(0, 0)?.cellKeys.Length == 1, "disconnected wall fragments lost a cardinally enclosed floor cell");
            world.nodes.RemoveAt(0);
            Require(RoomLayout.Build(world).At(0, 0) == null, "a cardinal breach in fragmented walls retained shelter");
        });
        Run("infinite interior an out-of-range wall cannot seal the signed world edge", () =>
        {
            var world = InfiniteRoomBox(WorldTiles.CoordinateLimit - 4, 8);
            Require(RoomLayout.Build(world).rooms.Count == 0, "a wall beyond Inside bounds created edge shelter");
        });
        Run("infinite interior rectangles work across zero chunks and both signed limits", () =>
        {
            foreach (var point in new[] { (10, 10), (-2, -2), (-18, 14), (14, -18),
                (-WorldTiles.CoordinateLimit, -WorldTiles.CoordinateLimit),
                (WorldTiles.CoordinateLimit - 5, WorldTiles.CoordinateLimit - 5) })
            {
                var world = InfiniteRoomBox(point.Item1, point.Item2);
                var layout = RoomLayout.Build(world);
                var room = layout.At(point.Item1 + 2, point.Item2 + 2);
                Require(layout.rooms.Count == 1 && room?.cellKeys.Length == 9, "known signed rectangle lost floor");
                foreach (long key in room.cellKeys)
                    Require(ReferenceEquals(room, layout.At(WorldTiles.X(key), WorldTiles.Y(key))), "global room keys do not round-trip through At");
            }
        });
        Run("infinite interior two rooms thousands of cells apart need no global bounding map", () =>
        {
            var world = InfiniteRoomBox(-10004, -10004);
            InfiniteRoomFloor(world, 10004, 10004, 5, 5);
            InfiniteRoomShell(world, 10004, 10004);
            world.width = 0; world.height = int.MaxValue;
            var layout = RoomLayout.Build(world);
            var left = layout.At(-10002, -10002); var right = layout.At(10006, 10006);
            Require(layout.rooms.Count == 2 && left != null && right != null && !ReferenceEquals(left, right), "distant signed rooms collided or were clipped");
            Require(left.cellKeys.Length == 9 && right.cellKeys.Length == 9 && !left.cellKeys.Intersect(right.cellKeys).Any(), "distant floors do not have independent global keys");
            Require(layout.At(0, 0) == null && layout.At(int.MinValue, int.MaxValue) == null, "unloaded or extreme coordinate became shelter");
            layout.width = layout.height = 0;
            Require(ReferenceEquals(left, layout.At(-10002, -10002)), "sparse lookup depends on width or height");
        });
        Run("infinite interior open doors retain roofs and missing or dead barriers expose them", () =>
        {
            foreach (bool buildingWalls in new[] { false, true })
            {
                var world = InfiniteRoomBox(-18, 14, buildingWalls: buildingWalls);
                var door = world.buildings.Single(b => b.kind == BuildKind.Door);
                var closed = RoomLayout.Build(world).At(-16, 16);
                door.setting = 1;
                var open = RoomLayout.Build(world).At(-16, 16);
                Require(closed != null && open != null && closed.cellKeys.SequenceEqual(open.cellKeys) && open.doorIds.SequenceEqual(new[] { door.id }), "open door changed roof topology or lost its association");
                door.hp = 0;
                Require(RoomLayout.Build(world).rooms.Count == 0, "destroyed door retained shelter");
                door.hp = 220;
                if (buildingWalls) world.buildings.Single(b => b.x == -18 && b.y == 16).hp = 0;
                else world.nodes.Single(n => n.x == -18 && n.y == 16).stock = 0;
                Require(RoomLayout.Build(world).At(-16, 16) == null, "broken or depleted wall retained shelter");
                if (buildingWalls) world.buildings.RemoveAll(b => b.x == -18 && b.y == 16);
                else world.nodes.RemoveAll(n => n.x == -18 && n.y == 16);
                Require(RoomLayout.Build(world).rooms.Count == 0, "missing wall retained shelter");
            }
        });
        Run("infinite interior unknown cells water and missing chunks never become dry shelter", () =>
        {
            var world = InfiniteRoomBox(-2, -2);
            foreach (char ground in new[] { '?', '2' })
            {
                InfiniteRoomTile(world, 0, 0, ground);
                var exposed = RoomLayout.Build(world);
                Require(exposed.rooms.Count == 0 && exposed.At(-1, -1) == null, "one unknown or wet floor cell left partial shelter");
            }
            InfiniteRoomTile(world, 0, 0, '4');
            InfiniteRoomTile(world, -3, 0, '?');
            InfiniteRoomTile(world, 3, 0, '2');
            Require(RoomLayout.Build(world).rooms.Count == 1, "unknown or water beyond an intact wall invalidated a dry room");
            world.chunks.records.RemoveAll(c => c.x == 0 && c.y == 0);
            Require(RoomLayout.Build(world).rooms.Count == 0, "absent chunk supplied imaginary interior floor");
            world.chunks.records.Clear();
            world.ground = new string('4', 4096);
            Require(RoomLayout.Build(world).rooms.Count == 0, "legacy terrain filled unknown sparse chunks");
        });
        Run("infinite interior nested components keep every partition without duplicate rooms", () =>
        {
            var world = InfiniteRoomBox(-10, -10, 13, 13);
            InfiniteRoomShell(world, -6, -6);
            var layout = RoomLayout.Build(world);
            var outer = layout.At(-9, -9); var inner = layout.At(-4, -4);
            Require(layout.rooms.Count == 2 && outer?.cellKeys.Length == 96 && inner?.cellKeys.Length == 9, "nested shell lost an annulus or duplicated its inner room");
            Require(layout.rooms.SelectMany(r => r.cellKeys).Distinct().Count() == 105 && layout.At(-6, -4) == null, "nested wall appeared as floor or cells were repeated");
            var entrance = world.buildings.Single(b => b.x == -8 && b.y == -10);
            var sharedDoor = world.buildings.Single(b => b.x == -4 && b.y == -6);
            Require(outer.doorIds.SequenceEqual(new[] { entrance.id, sharedDoor.id }.OrderBy(id => id)) && inner.doorIds.SequenceEqual(new[] { sharedDoor.id }), "nested components lost shared door associations");
            sharedDoor.setting = 1;
            Require(RoomLayout.Build(world).rooms.Count == 2, "opening a shared nested door merged rooms");
            sharedDoor.hp = 0;
            var merged = RoomLayout.Build(world);
            Require(merged.rooms.Count == 1 && merged.At(-4, -4)?.cellKeys.Length == 106, "destroyed nested door did not merge the connected dry floor");
        });
        Run("infinite interior local allocation is capped without suppressing distant small rooms", () =>
        {
            var limit = InfiniteRoomBox(-128, -128, 254, 254, door: false);
            var largest = RoomLayout.Build(limit);
            Require(largest.rooms.Count == 1 && largest.At(0, 0)?.cellKeys.Length == 63504, "a room fitting the 256 by 256 local map was truncated");
            var oversized = InfiniteRoomBox(-150, -150, 300, 300, door: false);
            InfiniteRoomFloor(oversized, 10000, -10000, 5, 5);
            InfiniteRoomShell(oversized, 10000, -10000);
            var layout = RoomLayout.Build(oversized);
            Require(layout.rooms.Count == 1 && layout.At(0, 0) == null && layout.At(10002, -9998)?.cellKeys.Length == 9, "oversized component allocated a roof or suppressed another room");
        });
        Run("infinite interior machines never partition floor and missing entity lists are tolerated", () =>
        {
            var world = InfiniteRoomBox(-20, -20, 7, 7);
            Add(world, BuildKind.Assembler, -19, -17);
            Add(world, BuildKind.Generator, -16, -17);
            Ore(world, -16, -15);
            var room = RoomLayout.Build(world).At(-19, -19);
            Require(room?.cellKeys.Length == 25 && room.cellKeys.Contains(WorldTiles.Key(-16, -15)), "machines or resources removed sparse room floor");
            var nodeWalls = InfiniteRoomBox(-10, -10, door: false); nodeWalls.buildings = null;
            Require(RoomLayout.Build(nodeWalls).rooms.Count == 1 && nodeWalls.buildings == null, "missing buildings failed or were replaced");
            var buildingWalls = InfiniteRoomBox(-10, -10, buildingWalls: true); buildingWalls.nodes = null;
            Require(RoomLayout.Build(buildingWalls).rooms.Count == 1 && buildingWalls.nodes == null, "missing nodes failed or were replaced");
            buildingWalls.buildings = null;
            Require(RoomLayout.Build(buildingWalls).rooms.Count == 0, "empty sparse world contained rooms");
        });
        Run("infinite interior topology is deterministic detached and leaves source state unchanged", () =>
        {
            var world = InfiniteRoomBox(-10, -10, 13, 13);
            InfiniteRoomShell(world, -6, -6);
            var nodes = world.nodes; var buildings = world.buildings; var records = world.chunks.records;
            string before = Dump(world);
            var first = RoomLayout.Build(world); var second = RoomLayout.Build(world);
            Require(Dump(world) == before && ReferenceEquals(nodes, world.nodes) && ReferenceEquals(buildings, world.buildings) && ReferenceEquals(records, world.chunks.records), "sparse Build mutated source state or replaced a list");
            world.nodes.Reverse(); world.buildings.Reverse(); world.chunks.records.Reverse();
            string reordered = Dump(world); var reversed = RoomLayout.Build(world);
            Require(Dump(world) == reordered && first.rooms.Count == reversed.rooms.Count, "sparse Build reordered input or changed room count");
            for (int i = 0; i < first.rooms.Count; i++)
            {
                var a = first.rooms[i]; var b = reversed.rooms[i];
                Require(a.id == b.id && a.cellKeys.SequenceEqual(b.cellKeys) && a.doorIds.SequenceEqual(b.doorIds), "entity order changed sparse room IDs cells or doors");
                Require(!ReferenceEquals(a, second.rooms[i]) && !ReferenceEquals(a.cellKeys, second.rooms[i].cellKeys) && !ReferenceEquals(a.doorIds, second.rooms[i].doorIds), "separate sparse builds share mutable room state");
            }
        });
    }

    static WorldState InfiniteRoomBox(int x, int y, int width = 5, int height = 5, bool door = true, bool buildingWalls = false)
    {
        var world = Flat();
        world.chunks.enabled = true;
        // Legacy metadata is deliberately unusable as terrain for sparse rooms.
        world.ground = new string('?', 4096);
        InfiniteRoomFloor(world, x, y, width, height);
        InfiniteRoomShell(world, x, y, width, height, door, buildingWalls);
        return world;
    }

    static void InfiniteRoomFloor(WorldState world, int x, int y, int width, int height)
    {
        for (int cy = WorldTiles.FloorDiv(y, 16); cy <= WorldTiles.FloorDiv(y + height - 1, 16); cy++)
            for (int cx = WorldTiles.FloorDiv(x, 16); cx <= WorldTiles.FloorDiv(x + width - 1, 16); cx++)
                if (WorldTiles.Inside(world, cx * 16, cy * 16) && !world.chunks.records.Any(c => c.x == cx && c.y == cy)) world.chunks.records.Add(new WorldChunkState {
                    x = cx, y = cy, ground = new string('4', 256), biomes = new string('0', 256) });
    }

    static void InfiniteRoomShell(WorldState world, int x, int y, int width = 5, int height = 5, bool door = true, bool buildingWalls = false)
    {
        for (int iy = y; iy < y + height; iy++) for (int ix = x; ix < x + width; ix++)
        {
            if (ix != x && iy != y && ix != x + width - 1 && iy != y + height - 1) continue;
            if (door && ix == x + 2 && iy == y) InteriorDoor(world, ix, iy);
            else InteriorWall(world, ix, iy, buildingWalls);
        }
    }

    static void InfiniteRoomTile(WorldState world, int x, int y, char value)
    {
        int cx = WorldTiles.FloorDiv(x, 16), cy = WorldTiles.FloorDiv(y, 16);
        var chunk = world.chunks.records.Single(c => c.x == cx && c.y == cy);
        var ground = chunk.ground.ToCharArray();
        ground[x - cx * 16 + (y - cy * 16) * 16] = value;
        chunk.ground = new string(ground);
    }
}
