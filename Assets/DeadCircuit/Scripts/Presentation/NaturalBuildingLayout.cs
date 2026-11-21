using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    // Read-only presentation footprint. Outdoor yards and their furniture are
    // flood-filled from the plan's open edges; they never acquire a roof.
    internal sealed class NaturalBuildingLayout
    {
        public const float FloorRise = .875f;
        public const int MaxRiseCells = 4;
        public readonly int width, height, floors, minY, doorX, doorY;
        public readonly string roofArt, facadeArt;
        public readonly struct EntryOpening
        {
            public readonly float minX, minY, maxX, maxY;
            public EntryOpening(int x, int y, int dy, float rise)
            {
                minX = x - .625f; maxX = x + .625f;
                minY = y - .625f; maxY = y + .625f + (dy > 0 ? rise : 0);
            }
        }
        readonly List<EntryOpening> entryOpenings = new List<EntryOpening>();
        public IReadOnlyList<EntryOpening> EntryOpenings => entryOpenings;
        readonly bool[] covered, interior, entrances, outside;
        readonly char[] cells;
        public float Rise => floors * FloorRise;

        NaturalBuildingLayout(SiteState site, SiteBlueprint plan, bool floorWorld)
        {
            width = site.width; height = site.height; doorX = site.doorX; doorY = site.doorY;
            floors = floorWorld ? FloorWorld.MaxLevel(site.kind) + 1 : 1;
            roofArt = site.kind == SiteKind.Greenhouse ? "exterior_roof_glass" :
                plan.category == SiteCategory.Industrial ? "exterior_roof_metal" :
                plan.category == SiteCategory.Residential || plan.category == SiteCategory.Rural ?
                    "exterior_roof_tile" : "exterior_roof_flat";
            facadeArt = plan.category == SiteCategory.Industrial || site.kind == SiteKind.Greenhouse ?
                "exterior_facade_industrial" : floors >= 3 || plan.category == SiteCategory.Civic ||
                plan.category == SiteCategory.Special ? "exterior_facade_concrete" : "exterior_facade_brick";
            cells = new char[width * height]; covered = new bool[cells.Length];
            interior = new bool[cells.Length]; entrances = new bool[cells.Length]; outside = new bool[cells.Length];
            var queue = new Queue<int>();
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int i = x + y * width;
                char cell = cells[i] = SiteBlueprintCatalog.At(plan, site.rotation, x, y);
                if (cell == ' ' || cell == ',' || (x == 0 || y == 0 || x == width - 1 || y == height - 1) && !Boundary(cell))
                { outside[i] = true; queue.Enqueue(i); }
            }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(), x = i % width, y = i / width;
                Visit(x - 1, y); Visit(x + 1, y); Visit(x, y - 1); Visit(x, y + 1);
            }
            void Visit(int x, int y)
            {
                if (x < 0 || y < 0 || x >= width || y >= height) return;
                int i = x + y * width;
                if (outside[i] || Boundary(cells[i])) return;
                outside[i] = true; queue.Enqueue(i);
            }
            int bottom = height;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int i = x + y * width;
                interior[i] = !outside[i] && !Boundary(cells[i]);
            }
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int i = x + y * width;
                if (interior[i]) covered[i] = true;
                else if (Boundary(cells[i]))
                {
                    bool hasRoom = InteriorAt(x - 1, y) || InteriorAt(x + 1, y) ||
                        InteriorAt(x, y - 1) || InteriorAt(x, y + 1);
                    bool hasOutside = OutsideAt(x - 1, y) || OutsideAt(x + 1, y) ||
                        OutsideAt(x, y - 1) || OutsideAt(x, y + 1);
                    entrances[i] = cells[i] == '+' && hasRoom && hasOutside;
                    // The roof still covers the doorway from above; entrance
                    // visibility and movement use the separate entrance mask.
                    covered[i] = hasRoom;
                }
                if (covered[i]) bottom = Math.Min(bottom, y);
            }
            minY = bottom;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                if (EntranceOutward(x, y, out int dx, out int dy))
                    entryOpenings.Add(new EntryOpening(x, y, dy, Rise));
        }

        static bool Boundary(char cell) => cell == '#' || cell == '+';
        bool InteriorAt(int x, int y) => x >= 0 && y >= 0 && x < width && y < height && interior[x + y * width];
        bool OutsideAt(int x, int y) => x < 0 || y < 0 || x >= width || y >= height || outside[x + y * width];
        public bool SouthFacade(int x, int y) => SouthFacade(x, y, true);
        public bool SouthFacade(int x, int y, bool boundaryExists) => boundaryExists &&
            Boundary(Cell(x, y)) && OutsideAt(x, y - 1) && InteriorAt(x, y + 1);
        public char Cell(int x, int y) => x < 0 || y < 0 || x >= width || y >= height ? ' ' : cells[x + y * width];
        public bool Covers(int x, int y) => x >= 0 && y >= 0 && x < width && y < height && covered[x + y * width];
        public bool IsInterior(int x, int y) => x >= 0 && y >= 0 && x < width && y < height && interior[x + y * width];
        public bool Contains(float x, float y)
        {
            int ix = (int)Math.Floor(x + .5f), iy = (int)Math.Floor(y + .5f);
            // Internal doors separate rooms but remain part of the indoor
            // cutaway, just like exterior thresholds. Walls stay excluded.
            return IsInterior(ix, iy) || Covers(ix, iy) && Cell(ix, iy) == '+';
        }
        public bool Entrance(int x, int y) => x >= 0 && y >= 0 && x < width && y < height && entrances[x + y * width];

        public bool EntranceOutward(int x, int y, out int dx, out int dy)
        {
            dx = dy = 0;
            if (!Entrance(x, y)) return false;
            if (OutsideAt(x, y - 1) && InteriorAt(x, y + 1)) dy = -1;
            else if (OutsideAt(x, y + 1) && InteriorAt(x, y - 1)) dy = 1;
            else if (OutsideAt(x - 1, y) && InteriorAt(x + 1, y)) dx = -1;
            else if (OutsideAt(x + 1, y) && InteriorAt(x - 1, y)) dx = 1;
            return dx != 0 || dy != 0;
        }

        // Entrance cues and the real open/closed door must survive the raised
        // shell projection, including doors on the north side of a tall site.
        public int EntranceOrder(int x, int y, int normalOrder, int shellOrder) =>
            Entrance(x, y) ? Math.Max(normalOrder, shellOrder + 4) : normalOrder;

        public bool Occludes(float x, float y)
        {
            int ix = (int)Math.Floor(x + .5f);
            for (int iy = 0; iy < height; iy++)
                if (Covers(ix, iy) && y >= iy - .5f && y <= iy - .5f + Rise + 1) return true;
            return false;
        }

        public static NaturalBuildingLayout Create(SiteState site, bool floorWorld)
        {
            if (site == null || site.parentSiteId != 0 || site.level != 0 || site.layoutVersion != 1 ||
                site.rotation < 0 || site.rotation > 3) return null;
            var plan = SiteBlueprintCatalog.Get(site.kind);
            if (plan == null || site.width != SiteBlueprintCatalog.Width(plan, site.rotation) ||
                site.height != SiteBlueprintCatalog.Height(plan, site.rotation)) return null;
            var result = new NaturalBuildingLayout(site, plan, floorWorld);
            return result.minY < result.height ? result : null;
        }
    }
}
