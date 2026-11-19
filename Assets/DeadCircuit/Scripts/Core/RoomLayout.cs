using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public sealed class RoomState
    {
        public int id;
        public int[] cells;
        public int[] doorIds;
    }

    // Derived topology only: doors remain boundaries even while open. Movement and
    // machine occupancy must not change which cells share a room or a roof.
    public sealed class RoomLayout
    {
        public List<RoomState> rooms = new List<RoomState>();
        public int width, height;
        private RoomState[] roomByCell;

        public RoomState At(int x, int y)
        {
            if ((uint)x >= (uint)width || (uint)y >= (uint)height || roomByCell == null) return null;
            return roomByCell[x + y * width];
        }

        public static RoomLayout Build(WorldState world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (world.width <= 0 || world.height <= 0) throw new ArgumentException("Room map dimensions must be positive.", nameof(world));
            int size = checked(world.width * world.height);
            var result = new RoomLayout { width = world.width, height = world.height, roomByCell = new RoomState[size] };
            var boundary = new bool[size];
            var doorAt = new int[size];
            if (world.nodes != null) foreach (var node in world.nodes)
            {
                if (node == null || node.kind != NodeKind.Wall || node.hp <= 0f || node.stock <= 0) continue;
                if ((uint)node.x < (uint)world.width && (uint)node.y < (uint)world.height) boundary[node.x + node.y * world.width] = true;
            }
            if (world.buildings != null) foreach (var building in world.buildings)
            {
                if (building == null || building.hp <= 0f || (building.kind != BuildKind.Wall && building.kind != BuildKind.Door)) continue;
                int endX = Math.Min(world.width, building.x + DemoCatalog.Width(building));
                int endY = Math.Min(world.height, building.y + DemoCatalog.Height(building));
                for (int y = Math.Max(0, building.y); y < endY; y++) for (int x = Math.Max(0, building.x); x < endX; x++)
                {
                    int cell = x + y * world.width;
                    boundary[cell] = true;
                    if (building.kind == BuildKind.Door) doorAt[cell] = building.id;
                }
            }

            var visited = new bool[size];
            var queue = new int[size];
            for (int start = 0; start < size; start++)
            {
                if (boundary[start] || visited[start]) continue;
                int head = 0, tail = 1;
                queue[0] = start; visited[start] = true;
                bool outside = false;
                var doors = new HashSet<int>();
                while (head < tail)
                {
                    int cell = queue[head++], x = cell % world.width, y = cell / world.width;
                    if (x == 0 || y == 0 || x == world.width - 1 || y == world.height - 1 || world.ground[cell] == (char)('0' + (int)GroundKind.Water)) outside = true;
                    for (int direction = 0; direction < 4; direction++)
                    {
                        int nx = x, ny = y;
                        if (direction == 0) nx--; else if (direction == 1) nx++; else if (direction == 2) ny--; else ny++;
                        if ((uint)nx >= (uint)world.width || (uint)ny >= (uint)world.height) continue;
                        int next = nx + ny * world.width;
                        if (boundary[next])
                        {
                            if (doorAt[next] > 0) doors.Add(doorAt[next]);
                            continue;
                        }
                        if (visited[next]) continue;
                        visited[next] = true; queue[tail++] = next;
                    }
                }
                // A connected region touching any water is not dry shelter, even
                // when the pond itself is enclosed by walls.
                if (outside) continue;
                var cells = new int[tail];
                Array.Copy(queue, cells, tail); Array.Sort(cells);
                var doorIds = new int[doors.Count];
                doors.CopyTo(doorIds); Array.Sort(doorIds);
                var room = new RoomState { id = start + 1, cells = cells, doorIds = doorIds };
                result.rooms.Add(room);
                foreach (int cell in cells) result.roomByCell[cell] = room;
            }
            return result;
        }
    }
}
