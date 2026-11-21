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
            return WorldGeneration.Generate(new WorldGenerationSettings { seed = seed });
        }

        // Called only by the new-world generator. Restore/migration must remain inert.
        internal static void PrepareGeneratedWorld(WorldState world)
        {
            world.waterVersion = 1;
            InitializeSurvivalWorld(world, true);
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
            nodeGrid.Clear(); buildingGrid.Clear();
            foreach (NodeState n in State.nodes) if (Inside(n.x, n.y)) nodeGrid[Cell(n.x, n.y)] = n;
            foreach (BuildingState b in State.buildings)
                for (int y = b.y; y < b.y + DemoCatalog.Height(b); y++) for (int x = b.x; x < b.x + DemoCatalog.Width(b); x++)
                    if (Inside(x, y) && DemoCatalog.Contains(b,x,y)) buildingGrid[Cell(x, y)] = b;
            RefreshInteriors();
        }
        private bool BlockedAt(int x, int y)
        {
            char ground=WorldTiles.Ground(State,x,y);
            if (!Inside(x, y) || ground == '2' || ground == '?') return true;
            NodeState n = nodeGrid[Cell(x, y)]; if (n != null && n.hp > 0f && n.stock > 0 && !DemoCatalog.IsGroundPickup(n.kind)) return true;
            BuildingState b = buildingGrid[Cell(x, y)]; return b != null && b.hp > 0f && !DemoCatalog.Walkable(b);
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
        private bool LineClear(float x, float y, float tx, float ty, int ignoreX, int ignoreY, BuildingState ignoreMachine = null)
        {
            BuildingState ignoredBuilding = ignoreMachine ?? BuildingAt(ignoreX, ignoreY);
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
        private bool NearSpawn(int x, int y) => State.generatorVersion < WorldGeneration.NaturalOpeningVersion &&
            Math.Abs(x - State.width / 2) <= 1 && Math.Abs(y - (State.height / 2 + 3)) <= 1;
        private bool OccupiedByActor(int x, int y)
        {
            foreach (PlayerState p in State.players) if (p.hp > 0f && Math.Abs(p.x - x) < 0.8f && Math.Abs(p.y - y) < 0.8f) return true;
            foreach (ZombieState z in State.zombies) if (Math.Abs(z.x - x) < 0.8f && Math.Abs(z.y - y) < 0.8f) return true;
            return false;
        }
        private bool FindSpawn(out int x, out int y, PlayerState returning = null)
        {
            if (State.generatorVersion < WorldGeneration.NaturalOpeningVersion) return FindLegacySpawn(out x, out y);

            PlayerState nearby = returning;
            if (nearby == null || FloorWorld.IsInterior(State, RoundCell(nearby.x), RoundCell(nearby.y)))
                nearby = State.players.Find(p => p.connected && p.hp > 0f &&
                    !FloorWorld.IsInterior(State, RoundCell(p.x), RoundCell(p.y)));
            // Try near the player or a teammate first, then consider the entire initial terrain.
            if (nearby != null)
            {
                int cx = RoundCell(nearby.x), cy = RoundCell(nearby.y);
                if (FindLanding(cx - 16, cy - 16, cx + 16, cy + 16, nearby, false, out x, out y)) return true;
            }
            return FindLanding(0, 0, State.width - 1, State.height - 1, null, true, out x, out y);
        }

        private bool FindLanding(int left, int top, int right, int bottom, PlayerState nearby,
            bool allowCloseEnemies, out int x, out int y)
        {
            x = y = 0;
            double best = double.NegativeInfinity;
            for (int iy = top; iy <= bottom; iy++) for (int ix = left; ix <= right; ix++)
            {
                char ground = WorldTiles.Ground(State, ix, iy);
                if ((ground != '0' && ground != '1' && ground != '3') || BlockedAt(ix, iy) ||
                    FloorWorld.IsProtectedCell(State, ix, iy) || OccupiedByActor(ix, iy) || SurvivalOccupiesCell(ix, iy)) continue;
                float threat = 64f;
                foreach (ZombieState z in State.zombies) threat = Math.Min(threat, DistSq(z.x, z.y, ix, iy));
                if (threat < (allowCloseEnemies ? 2.25f : 64f)) continue;
                int exits = 0;
                for (int d = 0; d < DirectionX.Length; d++)
                    if (!BlockedAt(ix + DirectionX[d], iy + DirectionY[d])) exits++;
                if (exits == 0) continue;
                uint rank = LandingRank(State.seed, ix, iy);
                double score = (threat >= 64f ? 10000 : 0) + exits * 2 + rank / 4294967296.0;
                if (nearby != null) score -= Math.Abs(DistSq(nearby.x, nearby.y, ix, iy) - 9f);
                if (score <= best) continue;
                best = score; x = ix; y = iy;
            }
            return !double.IsNegativeInfinity(best);
        }

        private static uint LandingRank(int seed, int x, int y)
        {
            unchecked
            {
                uint n = (uint)seed ^ (uint)x * 0x9e3779b9u ^ (uint)y * 0x85ebca6bu;
                n ^= n >> 16; n *= 0x7feb352du; n ^= n >> 15; n *= 0x846ca68bu;
                return n ^ (n >> 16);
            }
        }

        private bool FindLegacySpawn(out int x, out int y)
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
                bool mineral=n!=null && (b.kind==BuildKind.SteamMiner ? EquipmentCatalog.UsesPickaxe(n.kind) :
                    n.kind==NodeKind.Ore || n.kind==NodeKind.CopperOre || n.kind==NodeKind.TinOre || n.kind==NodeKind.UraniumOre);
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
