using DeadCircuit;

static partial class Checks
{
    // Main wires this suite in; fixtures are observed through the public topology API.
    internal static void InteriorChecks()
    {
        Run("interior a missing entrance door exposes a room until a real door fills it", () =>
        {
            var world = InteriorBox(door: false);
            Require(RoomLayout.Build(world).rooms.Count == 0, "wall opening counted as shelter");
            var door = InteriorDoor(world, 12, 10);
            var layout = RoomLayout.Build(world);
            Require(layout.width == 64 && layout.height == 64 && layout.rooms.Count == 1, "closed room not found");
            var room = layout.At(12, 12);
            Require(room != null && room.id == 716, "room ID is not minimum row-major cell plus one");
            Require(room.cells.SequenceEqual(new[] { 715, 716, 717, 779, 780, 781, 843, 844, 845 }), "room cells include boundaries or omit dry floor");
            Require(room.doorIds.SequenceEqual(new[] { door.id }), "entrance door not associated with room");
            Require(ReferenceEquals(room, layout.At(11, 11)) && layout.At(12, 10) == null && layout.At(10, 12) == null && layout.At(9, 12) == null, "At does not distinguish room, door, wall and outside");
        });
        Run("interior live building walls and either door setting retain the same roof", () =>
        {
            var world = InteriorBox(buildingWalls: true);
            var door = world.buildings.Single(b => b.kind == BuildKind.Door);
            var closed = RoomLayout.Build(world).At(12, 12);
            door.setting = 1;
            var open = RoomLayout.Build(world).At(12, 12);
            Require(closed != null && open != null && closed.id == open.id && closed.cells.SequenceEqual(open.cells) && closed.doorIds.SequenceEqual(open.doorIds), "opening a door removed or changed its room");
        });
        Run("interior public door opening entry dismantling and rebuilding refresh rooms", () =>
        {
            var sim = new WorldSimulator(InteriorBox());
            var player = Builder(sim); player.x = 12; player.y = 8.5f;
            Require(sim.IsBlocked(12, 10) && sim.Interiors.At(12, 12) != null, "closed door fixture lacks blocked entrance or room");
            sim.Apply(Command(player, "interact", 12, 10));
            Require(!sim.IsBlocked(12, 10) && sim.Interiors.At(12, 12) != null, "opening door changed roof topology");
            sim.Apply(new PlayerCommand { playerId = player.id, action = "move", moveY = 1 });
            Advance(sim, .9f);
            sim.Apply(new PlayerCommand { playerId = player.id, action = "move" });
            Require(player.y > 10.5f && player.y < 13.5f && RoomLayout.Build(sim.State).At(12, 11) != null, "player could not enter enclosed floor through real door");
            sim.Apply(Command(player, "dismantle", 12, 10));
            Require(!sim.State.buildings.Any(b => b.kind == BuildKind.Door) && sim.Interiors.rooms.Count == 0 && RoomLayout.Build(sim.State).rooms.Count == 0, "public door removal did not expose room");
            Advance(sim, .3f);
            sim.Apply(Command(player, "build", 12, 10, (int)BuildKind.Door));
            Require(sim.State.buildings.Any(b => b.kind == BuildKind.Door && b.hp == 220f) && sim.Interiors.rooms.Count == 1 && RoomLayout.Build(sim.State).rooms.Count == 1, "public door rebuilding did not restore room");
        });
        Run("interior broken node walls building walls and doors expose their room", () =>
        {
            foreach (bool buildingWalls in new[] { false, true })
            {
                var world = InteriorBox(buildingWalls: buildingWalls);
                Require(RoomLayout.Build(world).rooms.Count == 1, "live perimeter did not enclose room");
                if (buildingWalls) world.buildings.Single(b => b.x == 10 && b.y == 12).hp = 0f;
                else world.nodes.Single(n => n.x == 10 && n.y == 12).hp = 0f;
                Require(RoomLayout.Build(world).rooms.Count == 0, "destroyed wall still sheltered room");
            }
            var depleted = InteriorBox();
            var wall = depleted.nodes.Single(n => n.x == 10 && n.y == 12);
            wall.stock = 0;
            Require(RoomLayout.Build(depleted).rooms.Count == 0, "depleted node wall still enclosed room");
            wall.stock = 1; wall.hp = .01f;
            Require(RoomLayout.Build(depleted).rooms.Count == 1, "damaged but live node wall stopped enclosing room");
            var door = depleted.buildings.Single();
            foreach (float hp in new[] { 0f, -1f })
            {
                door.hp = hp;
                Require(RoomLayout.Build(depleted).rooms.Count == 0, "broken door retained roof");
            }
            door.hp = 220f;
            Require(RoomLayout.Build(depleted).rooms.Count == 1, "repairing a door did not restore room");
        });
        Run("interior a shared partition and door keep two rooms with sorted door associations", () =>
        {
            var world = InteriorBox(width: 9);
            var leftDoor = world.buildings.Single();
            for (int y = 11; y <= 13; y++) InteriorWall(world, 14, y);
            var split = RoomLayout.Build(world);
            Require(split.rooms.Count == 2 && split.rooms.Select(r => r.id).SequenceEqual(new[] { 716, 720 }), "partition did not split room with stable IDs");
            Require(split.At(12, 12).cells.Length == 9 && split.At(16, 12).cells.Length == 9 && split.At(14, 12) == null, "partition floor membership incorrect");
            Require(split.At(12, 12).doorIds.SequenceEqual(new[] { leftDoor.id }) && split.At(16, 12).doorIds.Length == 0, "wall-only partition associated unrelated doors");
            world.nodes.RemoveAll(n => n.x == 14 && n.y == 12);
            var sharedDoor = InteriorDoor(world, 14, 12);
            world.nodes.RemoveAll(n => n.x == 16 && n.y == 10);
            var rightDoor = InteriorDoor(world, 16, 10);
            var connected = RoomLayout.Build(world);
            Require(connected.rooms.Count == 2, "shared door merged rooms");
            Require(connected.At(12, 12).doorIds.SequenceEqual(new[] { leftDoor.id, sharedDoor.id }.OrderBy(id => id)), "left room lacks its shared and exterior doors");
            Require(connected.At(16, 12).doorIds.SequenceEqual(new[] { sharedDoor.id, rightDoor.id }.OrderBy(id => id)), "right room lacks its shared and exterior doors");
            sharedDoor.setting = 1;
            Require(RoomLayout.Build(world).rooms.Count == 2, "open shared door merged rooms");
            sharedDoor.hp = 0f;
            var merged = RoomLayout.Build(world);
            Require(merged.rooms.Count == 1 && merged.At(14, 12).cells.Length == 19 && !merged.rooms[0].doorIds.Contains(sharedDoor.id), "broken partition door did not merge dry rooms");
        });
        Run("interior machine footprints trees and ore never partition dry floor", () =>
        {
            var world = InteriorBox(width: 7, height: 7);
            Add(world, BuildKind.Assembler, 11, 13);
            Add(world, BuildKind.Generator, 14, 13);
            world.nodes.Add(new NodeState { id = world.nextId++, kind = NodeKind.Tree, x = 12, y = 11 });
            Ore(world, 14, 15);
            var layout = RoomLayout.Build(world);
            var room = layout.At(11, 11);
            Require(layout.rooms.Count == 1 && room.cells.Length == 25, "machine row or resource split room");
            foreach (var point in new[] { (11, 13), (13, 14), (14, 13), (15, 14), (12, 11), (14, 15), (15, 15) })
                Require(ReferenceEquals(room, layout.At(point.Item1, point.Item2)), "occupied dry floor excluded from room");
        });
        Run("interior four-neighbor topology neither leaks nor merges diagonally", () =>
        {
            var world = InteriorBox();
            world.nodes.RemoveAll(n => n.x == 10 && n.y == 10);
            Require(RoomLayout.Build(world).rooms.Count == 1, "diagonal corner opening exposed room");
            world.nodes.RemoveAll(n => n.x == 11 && n.y == 10);
            Require(RoomLayout.Build(world).rooms.Count == 0, "cardinal opening failed to expose room");
            var diagonal = Flat();
            foreach (var point in new[] { (10, 11), (11, 10), (12, 11), (11, 12), (13, 12), (12, 13) })
                InteriorWall(diagonal, point.Item1, point.Item2);
            var layout = RoomLayout.Build(diagonal);
            Require(layout.rooms.Select(r => r.id).SequenceEqual(new[] { 716, 781 }) && layout.rooms.All(r => r.cells.Length == 1), "diagonally adjacent floor merged or leaked outdoors");
        });
        Run("interior water in an enclosed region prevents dry shelter", () =>
        {
            var world = InteriorBox();
            var ground = world.ground.ToCharArray(); ground[12 + 12 * 64] = '2'; world.ground = new string(ground);
            Require(RoomLayout.Build(world).rooms.Count == 0, "walled pond became dry shelter");
            ground[12 + 12 * 64] = '4'; ground[9 + 12 * 64] = '2'; world.ground = new string(ground);
            Require(RoomLayout.Build(world).rooms.Count == 1, "water outside an intact wall invalidated dry room");
        });
        Run("interior boundary-connected regions and off-map queries are outside", () =>
        {
            var empty = RoomLayout.Build(Flat());
            Require(empty.rooms.Count == 0 && empty.At(0, 0) == null, "open map contained rooms");
            foreach (var point in new[] { (-1, 0), (0, -1), (64, 0), (0, 64), (int.MaxValue, int.MaxValue), (int.MinValue, int.MinValue) })
                Require(empty.At(point.Item1, point.Item2) == null, "off-map At query returned room");
            var world = InteriorBox(x: 0);
            Require(RoomLayout.Build(world).rooms.Count == 1, "intact boundary wall prevented enclosure");
            world.nodes.RemoveAll(n => n.x == 0 && n.y == 12);
            Require(RoomLayout.Build(world).rooms.Count == 0, "region touching map edge became room");
        });
        Run("interior door IDs are unique even when one door touches a room on four sides", () =>
        {
            var world = InteriorBox(width: 7, height: 7);
            var entrance = world.buildings.Single();
            var pillar = InteriorDoor(world, 13, 13);
            var room = RoomLayout.Build(world).At(11, 11);
            Require(room != null && room.cells.Length == 24 && room.doorIds.SequenceEqual(new[] { entrance.id, pillar.id }), "door association repeated or floor counted a door");
        });
        Run("interior missing entity lists are tolerated without replacing them", () =>
        {
            var nodeWalls = InteriorBox(door: false);
            InteriorWall(nodeWalls, 12, 10); nodeWalls.buildings = null;
            Require(RoomLayout.Build(nodeWalls).rooms.Count == 1 && nodeWalls.buildings == null, "missing building list failed or was replaced");
            var buildingWalls = InteriorBox(buildingWalls: true); buildingWalls.nodes = null;
            Require(RoomLayout.Build(buildingWalls).rooms.Count == 1 && buildingWalls.nodes == null, "missing node list failed or was replaced");
            buildingWalls.buildings = null;
            Require(RoomLayout.Build(buildingWalls).rooms.Count == 0 && buildingWalls.buildings == null && buildingWalls.nodes == null, "empty lists were not tolerated unchanged");
        });
        Run("interior topology is deterministic row-major and never mutates input", () =>
        {
            var world = InteriorBox(width: 9);
            for (int y = 11; y <= 13; y++) InteriorWall(world, 14, y);
            world.nodes.RemoveAll(n => n.x == 16 && n.y == 10);
            InteriorDoor(world, 16, 10);
            var nodes = world.nodes; var buildings = world.buildings; var items = buildings[0].items;
            string before = Dump(world);
            var first = RoomLayout.Build(world); var second = RoomLayout.Build(world);
            Require(Dump(world) == before && ReferenceEquals(nodes, world.nodes) && ReferenceEquals(buildings, world.buildings) && ReferenceEquals(items, world.buildings[0].items), "Build changed input state, lists or inventory");
            world.nodes.Reverse(); world.buildings.Reverse();
            string reordered = Dump(world);
            var reversed = RoomLayout.Build(world);
            Require(Dump(world) == reordered, "Build reordered input entities");
            Require(first.rooms.Select(r => r.id).SequenceEqual(new[] { 716, 720 }), "rooms not ordered by first row-major cell");
            for (int i = 0; i < first.rooms.Count; i++)
            {
                var a = first.rooms[i]; var b = reversed.rooms[i];
                Require(a.id == b.id && a.cells.SequenceEqual(b.cells) && a.doorIds.SequenceEqual(b.doorIds), "entity list order changed topology");
                Require(a.cells.SequenceEqual(a.cells.OrderBy(c => c)) && a.doorIds.SequenceEqual(a.doorIds.OrderBy(id => id)), "cell or door enumeration is unstable");
                Require(!ReferenceEquals(a, second.rooms[i]) && !ReferenceEquals(a.cells, second.rooms[i].cells) && !ReferenceEquals(a.doorIds, second.rooms[i].doorIds), "separate builds share mutable output");
            }
        });
        Run("interior a whole-map enclosure covers every one of its 3844 dry cells", () =>
        {
            var world = InteriorBox(x: 0, y: 0, width: 64, height: 64);
            var layout = RoomLayout.Build(world);
            Require(layout.rooms.Count == 1 && layout.rooms[0].id == 66 && layout.rooms[0].cells.Length == 3844, "full 4096-cell map enclosure incomplete");
            Require(ReferenceEquals(layout.At(1, 1), layout.At(62, 62)) && layout.At(63, 62) == null, "whole-map lookup lost far cells or included perimeter");
        });
    }

    static WorldState InteriorBox(int x = 10, int y = 10, int width = 5, int height = 5, bool door = true, bool buildingWalls = false, int doorOffset = 2)
    {
        var world = Flat();
        world.version = WorldSimulator.SaveVersion;
        WorldGeneration.LegacyMetadata(world);
        for (int iy = y; iy < y + height; iy++) for (int ix = x; ix < x + width; ix++)
        {
            if (ix != x && iy != y && ix != x + width - 1 && iy != y + height - 1) continue;
            if (ix == x + doorOffset && iy == y) continue;
            InteriorWall(world, ix, iy, buildingWalls);
        }
        if (door) InteriorDoor(world, x + doorOffset, y);
        return world;
    }

    static void InteriorWall(WorldState world, int x, int y, bool building = false)
    {
        if (building) Add(world, BuildKind.Wall, x, y);
        else world.nodes.Add(new NodeState { id = world.nextId++, x = x, y = y, kind = NodeKind.Wall, hp = 160f, stock = 14 });
    }

    static BuildingState InteriorDoor(WorldState world, int x, int y)
    {
        var door = new BuildingState { id = world.nextId++, kind = BuildKind.Door, x = x, y = y, footprintVersion = 1, hp = 220f, setting = 0 };
        world.buildings.Add(door);
        return door;
    }
}
