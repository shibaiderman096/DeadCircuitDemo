using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private static readonly int[] DirectionX = { 1, 0, -1, 0 };
        private static readonly int[] DirectionY = { 0, 1, 0, -1 };

        private static WorldState CreateWorld(int seed)
        {
            var s = new WorldState { seed = seed };
            var random = new Random(seed);
            var ground = new char[64 * 64];
            for (int i = 0; i < ground.Length; i++) ground[i] = random.Next(100) < 18 ? '1' : '0';
            // Small lakes leave a dry perimeter and a clear road-connected central base.
            for (int n = 0; n < 12; n++)
            {
                int cx = random.Next(6, 58), cy = random.Next(6, 58), r = random.Next(2, 5);
                for (int y = cy - r; y <= cy + r; y++) for (int x = cx - r; x <= cx + r; x++)
                    if (x > 2 && y > 2 && x < 61 && y < 61 && DistSq(x, y, cx, cy) <= r * r &&
                        (x < 22 || x > 42 || y < 22 || y > 42)) ground[x + y * 64] = '2';
            }
            var occupied = new HashSet<int>();
            // Ruins with floor, salvage and destructible wall fragments.
            for (int ruin = 0; ruin < 10; ruin++)
            {
                int rx = random.Next(5, 55), ry = random.Next(5, 55);
                if (rx > 17 && rx < 43 && ry > 17 && ry < 43) continue;
                for (int y = ry; y < ry + 5; y++) for (int x = rx; x < rx + 5; x++)
                {
                    if (x == 32 || x == 33 || y == 32 || y == 33) continue;
                    ground[x + y * 64] = '4';
                    if (occupied.Contains(x + y * 64)) continue;
                    NodeKind kind;
                    if ((x == rx || y == ry) && random.Next(3) != 0) kind = NodeKind.Wall;
                    else if (random.Next(5) == 0) kind = NodeKind.Crate;
                    else if (random.Next(4) == 0) kind = NodeKind.Scrap;
                    else continue;
                    s.nodes.Add(NewNode(s.nextId++, x, y, kind, kind == NodeKind.Crate ? 8 : 14)); occupied.Add(x + y * 64);
                }
            }
            for (int y = 26; y <= 39; y++) for (int x = 23; x <= 40; x++) ground[x + y * 64] = '4';
            for (int i = 0; i < 64; i++)
            {
                ground[i + 32 * 64] = ground[i + 33 * 64] = '3';
                ground[32 + i * 64] = ground[33 + i * 64] = '3';
            }
            // Coherent forest clusters, finite mineral deposits, salvage and food caches.
            for (int y = 3; y < 61; y++) for (int x = 3; x < 61; x++)
            {
                if (s.nodes.Count >= MaxNodes - 16 || occupied.Contains(x + y * 64) || ground[x + y * 64] == '2' ||
                    ground[x + y * 64] == '3' || (x >= 22 && x <= 42 && y >= 23 && y <= 41)) continue;
                double forest = Math.Sin(x * 0.29 + seed % 97) * Math.Cos(y * 0.23) > 0.12 ? 0.37 : 0.035;
                double roll = random.NextDouble(); NodeKind kind; int stock;
                if (roll < 0.012) { kind = NodeKind.Crate; stock = random.Next(6, 12); }
                else if (roll < 0.028) { kind = NodeKind.Ore; stock = random.Next(80, 180); }
                else if (roll < 0.04) { kind = NodeKind.CopperOre; stock = random.Next(80, 160); }
                else if (roll < 0.05) { kind = NodeKind.TinOre; stock = random.Next(60, 120); }
                else if (roll < 0.055) { kind = NodeKind.UraniumOre; stock = random.Next(40, 80); }
                else if (roll < 0.075) { kind = NodeKind.Scrap; stock = random.Next(10, 24); }
                else if (roll < forest) { kind = NodeKind.Tree; stock = random.Next(12, 24); }
                else continue;
                s.nodes.Add(NewNode(s.nextId++, x, y, kind, stock)); occupied.Add(x + y * 64);
            }
            // Remove any ruin accidentally placed in the guaranteed tutorial area.
            s.nodes.RemoveAll(n => n.x >= 22 && n.x <= 42 && n.y >= 23 && n.y <= 41);
            // Dry access around a guaranteed river bank; the waterwheel sits at (25,25).
            for(int y=24;y<=29;y++) for(int x=23;x<=24;x++) ground[x+y*64]='2';
            s.nodes.Add(NewNode(s.nextId++, 24, 30, NodeKind.Ore, 3000));
            s.nodes.Add(NewNode(s.nextId++, 24, 34, NodeKind.CopperOre, 1600));
            s.nodes.Add(NewNode(s.nextId++, 24, 37, NodeKind.TinOre, 1000));
            s.nodes.Add(NewNode(s.nextId++, 40, 37, NodeKind.UraniumOre, 500));
            s.nodes.Add(NewNode(s.nextId++, 25, 34, NodeKind.Tree, 30));
            s.nodes.Add(NewNode(s.nextId++, 25, 35, NodeKind.Tree, 30));
            s.nodes.Add(NewNode(s.nextId++, 26, 36, NodeKind.Tree, 30));
            s.nodes.Add(NewNode(s.nextId++, 36, 34, NodeKind.Scrap, 60));
            s.nodes.Add(NewNode(s.nextId++, 35, 35, NodeKind.Crate, 16));
            s.nodes.Add(NewNode(s.nextId++, 36, 37, NodeKind.Crate, 16));
            AddStarter(s, BuildKind.Workbench, 29, 35);
            AddStarter(s, BuildKind.StoneFurnace, 28, 37);
            AddStarter(s, BuildKind.Chest, 30, 37).items[(int)ItemKind.Wood]=12;
            s.ground = new string(ground);
            int[] initialX = { 14, 50, 32, 32, 12, 54 };
            int[] initialY = { 32, 32, 14, 54, 33, 33 };
            for (int i = 0; i < initialX.Length; i++)
                s.zombies.Add(new ZombieState { id = s.nextId++, x = initialX[i], y = initialY[i], variant = i == 5 ? 1 : 0, hp = i == 5 ? 32f : 45f });
            s.announcement = "原始时代：工作台(29,35)、石炉(28,37)。采铁铜锡，手制燃料后石炉冶炼；研究水力，岸位(25,25)可建水车。科技全队共享，个人加点独立。";
            s.announcementUntil = 28f;
            return s;
        }

        private static NodeState NewNode(int id, int x, int y, NodeKind kind, int stock)
        {
            return new NodeState { id = id, x = x, y = y, kind = kind, stock = stock, hp = kind == NodeKind.Wall ? 160f : 35f };
        }
        private static void EnsureMigratedResources(WorldState s)
        {
            // Add new ore kinds on unused dry land only. Never replace an old node,
            // move a machine, edit terrain, or disturb an in-flight shipment.
            var occupied=new HashSet<int>();
            foreach(var n in s.nodes) occupied.Add(n.x+n.y*64);
            foreach(var b in s.buildings) for(int y=b.y;y<b.y+DemoCatalog.Height(b);y++) for(int x=b.x;x<b.x+DemoCatalog.Width(b);x++) occupied.Add(x+y*64);
            foreach(var p in s.players) occupied.Add(RoundCell(p.x)+RoundCell(p.y)*64);
            foreach(var z in s.zombies) occupied.Add(RoundCell(z.x)+RoundCell(z.y)*64);
            foreach(NodeKind kind in new[] { NodeKind.CopperOre,NodeKind.TinOre,NodeKind.UraniumOre })
            {
                if(s.nodes.Exists(n=>n.kind==kind) || s.nodes.Count>=MaxNodes || s.nextId>=int.MaxValue-2) continue;
                bool added=false;
                for(int r=3;r<32 && !added;r++) for(int dy=-r;dy<=r && !added;dy++) for(int dx=-r;dx<=r;dx++)
                {
                    if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=r) continue;
                    int x=32+dx,y=35+dy; if(x<1 || y<1 || x>62 || y>62 || occupied.Contains(x+y*64) || s.ground[x+y*64]=='2') continue;
                    if(s.ground[x-1+y*64]=='2' && s.ground[x+1+y*64]=='2' && s.ground[x+(y-1)*64]=='2' && s.ground[x+(y+1)*64]=='2') continue;
                    s.nodes.Add(NewNode(s.nextId++,x,y,kind,1600)); occupied.Add(x+y*64); added=true; break;
                }
            }
        }
        private static BuildingState AddStarter(WorldState s, BuildKind kind, int x, int y)
        {
            var b = new BuildingState { id = s.nextId++, kind = kind, x = x, y = y, rotation = 0, footprintVersion = 1, hp = BuildingMaxHealth(kind) };
            s.buildings.Add(b); return b;
        }

        private void RefreshSpatial()
        {
            Array.Clear(nodeGrid, 0, nodeGrid.Length); Array.Clear(buildingGrid, 0, buildingGrid.Length);
            foreach (NodeState n in State.nodes) if (Inside(n.x, n.y)) nodeGrid[Cell(n.x, n.y)] = n;
            foreach (BuildingState b in State.buildings)
                for (int y = b.y; y < b.y + DemoCatalog.Height(b); y++) for (int x = b.x; x < b.x + DemoCatalog.Width(b); x++)
                    if (Inside(x, y)) buildingGrid[Cell(x, y)] = b;
        }
        private bool BlockedAt(int x, int y)
        {
            if (!Inside(x, y) || State.ground[Cell(x, y)] == '2') return true;
            NodeState n = nodeGrid[Cell(x, y)]; if (n != null && n.hp > 0f && n.stock > 0) return true;
            BuildingState b = buildingGrid[Cell(x, y)]; return b != null && b.hp > 0f && b.kind != BuildKind.Belt;
        }
        private bool CircleBlocked(float x, float y, float radius)
        {
            for (int iy = RoundCell(y - radius); iy <= RoundCell(y + radius); iy++)
                for (int ix = RoundCell(x - radius); ix <= RoundCell(x + radius); ix++)
                {
                    if (!BlockedAt(ix, iy)) continue;
                    float dx = Math.Max(0f, Math.Abs(x - ix) - 0.5f), dy = Math.Max(0f, Math.Abs(y - iy) - 0.5f);
                    if (dx * dx + dy * dy < radius * radius - 0.00001f) return true;
                }
            return false;
        }
        private void MoveCircle(ref float x, ref float y, float dx, float dy, float radius)
        {
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(dx), Math.Abs(dy)) / 0.18f));
            for (int i = 0; i < steps; i++)
            {
                float nx = x + dx / steps, ny = y + dy / steps;
                if (!CircleBlocked(nx, y, radius)) x = nx;
                if (!CircleBlocked(x, ny, radius)) y = ny;
            }
        }
        private bool LineClear(float x, float y, float tx, float ty, int ignoreX, int ignoreY)
        {
            BuildingState ignoredBuilding = BuildingAt(ignoreX, ignoreY);
            double length = Math.Sqrt(DistSq(x, y, tx, ty));
            int steps = Math.Max(1, (int)Math.Ceiling(length / 0.16));
            for (int i = 1; i <= steps; i++)
            {
                int cx = RoundCell(x + (tx - x) * i / steps), cy = RoundCell(y + (ty - y) * i / steps);
                if ((cx == ignoreX && cy == ignoreY) || (ignoredBuilding != null && DemoCatalog.Contains(ignoredBuilding, cx, cy)) ||
                    (cx == RoundCell(x) && cy == RoundCell(y))) continue;
                if (BlockedAt(cx, cy)) return false;
            }
            return true;
        }
        private bool NearSpawn(int x, int y) { return Math.Abs(x - State.width / 2) <= 1 && Math.Abs(y - (State.height / 2 + 3)) <= 1; }
        private bool OccupiedByActor(int x, int y)
        {
            foreach (PlayerState p in State.players) if (p.hp > 0f && Math.Abs(p.x - x) < 0.8f && Math.Abs(p.y - y) < 0.8f) return true;
            foreach (ZombieState z in State.zombies) if (Math.Abs(z.x - x) < 0.8f && Math.Abs(z.y - y) < 0.8f) return true;
            return false;
        }
        private bool FindSpawn(out int x, out int y)
        {
            int cx = State.width / 2, cy = State.height / 2 + 3;
            // Prefer eight-cell clearance; protection still permits rescue if a horde surrounds the base.
            for (int pass = 0; pass < 2; pass++) for (int r = 0; r <= 8; r++)
                for (int oy = -r; oy <= r; oy++) for (int ox = -r; ox <= r; ox++)
                {
                    if (Math.Max(Math.Abs(ox), Math.Abs(oy)) != r) continue;
                    int tx = cx + ox, ty = cy + oy;
                    if (!Inside(tx, ty) || CircleBlocked(tx, ty, 0.3f)) continue;
                    bool safe = true;
                    foreach (PlayerState p in State.players) if (p.connected && p.hp > 0f && DistSq(p.x, p.y, tx, ty) < 1f) { safe = false; break; }
                    if (!safe) continue;
                    foreach (ZombieState z in State.zombies) if (DistSq(z.x, z.y, tx, ty) < (pass == 0 ? 64f : 2.25f)) { safe = false; break; }
                    if (safe) { x = tx; y = ty; return true; }
                }
            x = y = 0; return false;
        }
        private NodeState NearbyOre(int x, int y)
        { return NearbyOre(new BuildingState { x = x, y = y }); }
        private NodeState NearbyOre(BuildingState b)
        {
            NodeState best = null;
            for (int y = b.y - 1; y <= b.y + DemoCatalog.Height(b); y++) for (int x = b.x - 1; x <= b.x + DemoCatalog.Width(b); x++)
            {
                NodeState n = NodeAt(x, y);
                bool mineral=n!=null && (n.kind==NodeKind.Ore || (b.kind!=BuildKind.SteamMiner && (n.kind==NodeKind.CopperOre || n.kind==NodeKind.TinOre || n.kind==NodeKind.UraniumOre)));
                if (mineral && n.stock > 0 && (best == null || n.id < best.id)) best = n;
            }
            return best;
        }
        private void RemoveNode(NodeState n) { State.nodes.Remove(n); nodeGrid[Cell(n.x, n.y)] = null; flowRevision = -1; }
        private void RemoveBuilding(BuildingState b)
        {
            State.buildings.Remove(b);
            // Destruction burns the device's contents, including shipments attached to it.
            State.cargo.RemoveAll(c => c.fromId == b.id || c.toId == b.id);
            RefreshSpatial(); flowRevision = -1;
        }
        private void RefreshBonuses()
        {
            engineeringRate = 1f; turretBonus = 1f;
            foreach (PlayerState p in State.players) if (p.connected && p.hp > 0f)
            {
                engineeringRate = Math.Max(engineeringRate, 1f + p.skills[0] * 0.15f + p.attributes[4] * 0.025f);
                turretBonus = Math.Max(turretBonus, 1f + p.skills[1] * 0.12f);
            }
        }

        private static bool HasControl(string value) { foreach (char c in value) if (char.IsControl(c)) return true; return false; }
        private static string CleanName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "幸存者";
            name = name.Trim(); var chars = new List<char>(24);
            foreach (char c in name) { if (!char.IsControl(c) && c != '<' && c != '>') chars.Add(c); if (chars.Count >= 24) break; }
            return chars.Count == 0 ? "幸存者" : new string(chars.ToArray());
        }
    }
}
