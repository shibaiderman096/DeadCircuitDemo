using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public sealed partial class RoomLayout
    {
        // Only a barrier component's small neighborhood is ever made dense.
        // Distant rooms and empty space between generated chunks cost no cells.
        private const int SparseRoomSideLimit = 256;
        private Dictionary<long, RoomState> sparseRoomByCell;

        private struct SparseBoundary
        {
            public int x, y, doorId;
        }

        private static RoomLayout BuildSparse(WorldState world)
        {
            var result = new RoomLayout { width = world.width, height = world.height,
                sparseRoomByCell = new Dictionary<long, RoomState>() };
            var boundaries = new Dictionary<long, SparseBoundary>();
            if (world.nodes != null) foreach (var node in world.nodes)
                if (node != null && node.kind == NodeKind.Wall && node.hp > 0f && node.stock > 0 && WorldTiles.Inside(world, node.x, node.y))
                    AddSparseBoundary(boundaries, node.x, node.y, 0);
            if (world.buildings != null) foreach (var building in world.buildings)
            {
                if (building == null || building.hp <= 0f || (building.kind != BuildKind.Wall && building.kind != BuildKind.Door)) continue;
                for (int dy = 0; dy < DemoCatalog.Height(building); dy++)
                    for (int dx = 0; dx < DemoCatalog.Width(building); dx++)
                    {
                        long x = (long)building.x + dx, y = (long)building.y + dy;
                        if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue) continue;
                        if (!WorldTiles.Inside(world, (int)x, (int)y)) continue;
                        AddSparseBoundary(boundaries, (int)x, (int)y, building.kind == BuildKind.Door ? building.id : 0);
                    }
            }

            var starts = new List<long>(boundaries.Keys);
            starts.Sort((a, b) =>
            {
                var left = boundaries[a]; var right = boundaries[b];
                int order = left.y.CompareTo(right.y);
                return order != 0 ? order : left.x.CompareTo(right.x);
            });
            var visited = new HashSet<long>();
            var queue = new Queue<long>();
            var found = new List<(RoomState room, int x, int y)>();
            foreach (long start in starts)
            {
                if (!visited.Add(start)) continue;
                var first = boundaries[start];
                int minX = first.x, maxX = first.x, minY = first.y, maxY = first.y;
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    var cell = boundaries[queue.Dequeue()];
                    minX = Math.Min(minX, cell.x); maxX = Math.Max(maxX, cell.x);
                    minY = Math.Min(minY, cell.y); maxY = Math.Max(maxY, cell.y);
                    // Four-neighbor floor can be sealed by walls that only touch
                    // diagonally. Group those fragments for the candidate box;
                    // the legacy flood-fill still uses four-neighbor movement.
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        long x = (long)cell.x + dx, y = (long)cell.y + dy;
                        if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue) continue;
                        long key = WorldTiles.Key((int)x, (int)y);
                        if (boundaries.ContainsKey(key) && visited.Add(key)) queue.Enqueue(key);
                    }
                }
                long width = (long)maxX - minX + 3, height = (long)maxY - minY + 3;
                if (width > SparseRoomSideLimit || height > SparseRoomSideLimit ||
                    minX == int.MinValue || minY == int.MinValue || maxX == int.MaxValue || maxY == int.MaxValue) continue;

                int originX = minX - 1, originY = minY - 1;
                var local = new WorldState { width = (int)width, height = (int)height, chunks = null };
                var ground = new char[local.width * local.height];
                for (int y = 0; y < local.height; y++) for (int x = 0; x < local.width; x++)
                {
                    int globalX = originX + x, globalY = originY + y;
                    ground[x + y * local.width] = WorldTiles.Ground(world, globalX, globalY);
                    // Include every barrier in this box, including disconnected
                    // partitions and nested shells belonging to other components.
                    if (!boundaries.TryGetValue(WorldTiles.Key(globalX, globalY), out var boundary)) continue;
                    if (boundary.doorId > 0) local.buildings.Add(new BuildingState {
                        id = boundary.doorId, kind = BuildKind.Door, x = x, y = y, hp = 1f });
                    else local.nodes.Add(new NodeState { kind = NodeKind.Wall, x = x, y = y, hp = 1f, stock = 1 });
                }
                local.ground = new string(ground);
                foreach (var localRoom in Build(local).rooms)
                {
                    var keys = new long[localRoom.cells.Length];
                    bool duplicate = false;
                    for (int i = 0; i < keys.Length; i++)
                    {
                        int cell = localRoom.cells[i];
                        keys[i] = WorldTiles.Key(originX + cell % local.width, originY + cell / local.width);
                        if (result.sparseRoomByCell.ContainsKey(keys[i])) duplicate = true;
                    }
                    if (duplicate) continue;
                    var room = new RoomState { cells = Array.Empty<int>(), cellKeys = keys, doorIds = localRoom.doorIds };
                    int firstCell = localRoom.cells[0];
                    found.Add((room, originX + firstCell % local.width, originY + firstCell / local.width));
                    foreach (long key in keys) result.sparseRoomByCell.Add(key, room);
                }
            }
            found.Sort((a, b) => { int order = a.y.CompareTo(b.y); return order != 0 ? order : a.x.CompareTo(b.x); });
            foreach (var entry in found)
            {
                entry.room.id = result.rooms.Count + 1;
                result.rooms.Add(entry.room);
            }
            return result;
        }

        private static void AddSparseBoundary(Dictionary<long, SparseBoundary> boundaries, int x, int y, int doorId)
        {
            long key = WorldTiles.Key(x, y);
            if (boundaries.TryGetValue(key, out var existing) && existing.doorId > 0 &&
                (doorId <= 0 || existing.doorId < doorId)) doorId = existing.doorId;
            boundaries[key] = new SparseBoundary { x = x, y = y, doorId = doorId };
        }
    }
}
