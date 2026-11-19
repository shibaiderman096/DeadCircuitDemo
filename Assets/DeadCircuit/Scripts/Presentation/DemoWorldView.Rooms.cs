using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeadCircuit
{
    public sealed partial class DemoWorldView
    {
        readonly HashSet<int> roomBoundaryCells = new HashSet<int>();
        readonly HashSet<int> roomFloorCells = new HashSet<int>();
        RoomLayout roomLayout, drawnRoomLayout;
        ulong roomTopology;
        string roomGround;
        int roomWidth, roomHeight;
        Mesh roomFloorMesh;
        Material roomFloorMaterial;
        MeshRenderer roomFloorRenderer;

        // HUD and world rendering share this cache. Only boundary topology changes
        // flood-fill the world; damage, fuel and opening a door keep the same room.
        RoomLayout EnsureRoomLayout(WorldState world)
        {
            ulong topology = RoomTopology(world);
            // Deserialized LAN snapshots have a new WorldState reference. Their
            // identical topology can reuse the layout and its floor mesh. Genuine
            // world/session resets explicitly invalidate this in ResetRooms.
            if (roomLayout != null && roomWidth == world.width && roomHeight == world.height &&
                roomTopology == topology && roomGround == world.ground) return roomLayout;
            roomTopology = topology;
            roomGround = world.ground;
            roomWidth = world.width; roomHeight = world.height;
            roomLayout = RoomLayout.Build(world);
            roomBoundaryCells.Clear();
            if (world.nodes != null) foreach (var node in world.nodes)
                if (node != null && node.kind == NodeKind.Wall && node.hp > 0 && node.stock > 0)
                    AddRoomBoundary(node.x, node.y, 1, 1);
            if (world.buildings != null) foreach (var building in world.buildings)
                if (RoomBoundary(building))
                    AddRoomBoundary(building.x, building.y, DemoCatalog.Width(building), DemoCatalog.Height(building));
            return roomLayout;
        }

        internal RoomState RoomAt(WorldState world, int x, int y)
        {
            return world == null ? null : EnsureRoomLayout(world).At(x, y);
        }

        static bool RoomBoundary(BuildingState building) => building != null && building.hp > 0 &&
            (building.kind == BuildKind.Wall || building.kind == BuildKind.Door);

        static ulong RoomTopology(WorldState world)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                hash = (hash ^ (uint)world.width) * 1099511628211UL;
                hash = (hash ^ (uint)world.height) * 1099511628211UL;
                if (world.nodes != null) foreach (var node in world.nodes)
                {
                    if (node == null || node.kind != NodeKind.Wall || node.hp <= 0 || node.stock <= 0) continue;
                    hash = (hash ^ (uint)node.id) * 1099511628211UL;
                    hash = (hash ^ (uint)node.x) * 1099511628211UL;
                    hash = (hash ^ (uint)node.y) * 1099511628211UL;
                }
                if (world.buildings != null) foreach (var building in world.buildings)
                {
                    if (!RoomBoundary(building)) continue;
                    hash = (hash ^ (uint)building.id) * 1099511628211UL;
                    hash = (hash ^ (uint)building.kind) * 1099511628211UL;
                    hash = (hash ^ (uint)building.x) * 1099511628211UL;
                    hash = (hash ^ (uint)building.y) * 1099511628211UL;
                    hash = (hash ^ (uint)DemoCatalog.Width(building)) * 1099511628211UL;
                    hash = (hash ^ (uint)DemoCatalog.Height(building)) * 1099511628211UL;
                }
                return hash;
            }
        }

        void AddRoomBoundary(int x, int y, int w, int h)
        {
            for (int iy = y; iy < y + h; iy++) for (int ix = x; ix < x + w; ix++)
                if (ix >= 0 && iy >= 0 && ix < roomWidth && iy < roomHeight) roomBoundaryCells.Add(ix + iy * roomWidth);
        }

        bool HasRoomBoundary(int x, int y) => x >= 0 && y >= 0 && x < roomWidth && y < roomHeight &&
            roomBoundaryCells.Contains(x + y * roomWidth);

        Sprite ConnectedWall(int x, int y)
        {
            int mask = (HasRoomBoundary(x + 1, y) ? 1 : 0) | (HasRoomBoundary(x, y + 1) ? 2 : 0) |
                (HasRoomBoundary(x - 1, y) ? 4 : 0) | (HasRoomBoundary(x, y - 1) ? 8 : 0);
            return DemoArt.Get("wall_" + mask);
        }

        void DrawRooms(WorldState world)
        {
            var layout = EnsureRoomLayout(world);
            if (ReferenceEquals(drawnRoomLayout, layout)) return;
            Sprite floor = DemoArt.Get("floor_room");
            if (floor == null) return; // Retry if the SVG atlas has not been loaded yet.
            drawnRoomLayout = layout;
            roomFloorCells.Clear();
            var doorIds = new HashSet<int>();
            foreach (var room in layout.rooms)
            {
                if (room.cells != null) foreach (int cell in room.cells) roomFloorCells.Add(cell);
                if (room.doorIds != null) foreach (int door in room.doorIds) doorIds.Add(door);
            }
            if (world.buildings != null) foreach (var door in world.buildings)
                if (door != null && door.hp > 0 && door.kind == BuildKind.Door && doorIds.Contains(door.id))
                    roomFloorCells.Add(door.x + door.y * world.width);
            if (roomFloorRenderer == null)
            {
                var root = new GameObject("Room floor tiles / cutaway interior", typeof(MeshFilter), typeof(MeshRenderer));
                root.transform.SetParent(worldRoot, false);
                roomFloorMesh = new Mesh { name = "DeadCircuit tiled room floors" };
                root.GetComponent<MeshFilter>().sharedMesh = roomFloorMesh;
                roomFloorRenderer = root.GetComponent<MeshRenderer>();
                roomFloorMaterial = new Material(terrainMaterial) { name = "DeadCircuit SVG room floors" };
                roomFloorRenderer.sharedMaterial = roomFloorMaterial;
                roomFloorRenderer.sortingOrder = -18000; // Above ground props, below actors and equipment.
                roomFloorRenderer.shadowCastingMode = ShadowCastingMode.Off;
                roomFloorRenderer.receiveShadows = false;
            }
            roomFloorMaterial.mainTexture = floor.texture;
            roomFloorMesh.Clear();
            var vertices = new List<Vector3>(); var uv = new List<Vector2>();
            var triangles = new List<int>(); var colors = new List<Color32>();
            var r = floor.rect;
            var tex = new Rect(r.x / floor.texture.width, r.y / floor.texture.height,
                r.width / floor.texture.width, r.height / floor.texture.height);
            foreach (int cell in roomFloorCells)
            {
                if (cell < 0 || cell >= world.width * world.height) continue;
                int x = cell % world.width, y = cell / world.width;
                AddQuad(vertices, uv, triangles, colors, x - .5f, y - .5f, 1, 1, tex, Color.white);
            }
            roomFloorMesh.SetVertices(vertices); roomFloorMesh.SetUVs(0, uv);
            roomFloorMesh.SetTriangles(triangles, 0); roomFloorMesh.SetColors(colors); roomFloorMesh.RecalculateBounds();
            roomFloorRenderer.enabled = vertices.Count > 0;
            // Roofs stay cut away by default, including above open doors. RoomLayout
            // still treats the live door as an enclosing boundary; no opaque roof
            // sprite can hide movement, combat or the doorway below it.
        }

        static bool BehindWall(float actorX, float actorY, int x, int y) =>
            Mathf.Abs(actorX - x) < .85f && actorY >= y - .15f && actorY < y + 1.1f;

        void ConfigureWall(Visual visual, WorldState world, int x, int y)
        {
            visual.shadow.enabled = false;
            // The connected SVG is exactly one cell, without the old tall node offset.
            if (visual.body.sprite != null && visual.body.sprite.name.StartsWith("wall_", System.StringComparison.Ordinal))
            {
                visual.body.transform.localScale = Vector3.one;
                visual.body.transform.localPosition = Vector3.zero;
                visual.body.transform.localRotation = Quaternion.identity;
            }
            bool cutaway = false;
            if (world.players != null) foreach (var actor in world.players)
                if (actor != null && actor.hp > 0 && BehindWall(actor.x, actor.y, x, y)) { cutaway = true; break; }
            if (!cutaway && world.zombies != null) foreach (var actor in world.zombies)
                if (actor != null && actor.hp > 0 && BehindWall(actor.x, actor.y, x, y)) { cutaway = true; break; }
            if (!cutaway && world.survival != null && world.survival.creatures != null) foreach (var actor in world.survival.creatures)
                if (actor != null && actor.hp > 0 && BehindWall(actor.x, actor.y, x, y)) { cutaway = true; break; }
            Color color = visual.body.color; color.a = cutaway ? .32f : 1f; visual.body.color = color;
        }

        void ResetRooms()
        {
            roomLayout = drawnRoomLayout = null; roomTopology = 0; roomGround = null;
            roomBoundaryCells.Clear(); roomFloorCells.Clear();
            if (roomFloorRenderer != null) roomFloorRenderer.enabled = false;
        }

        void DisposeRooms()
        {
            if (roomFloorMesh != null) Destroy(roomFloorMesh);
            if (roomFloorMaterial != null) Destroy(roomFloorMaterial);
        }
    }
}
