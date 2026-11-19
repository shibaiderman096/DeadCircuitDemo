using System;
using System.Collections.Generic;
using System.Text;
using DeadCircuit;

static partial class Checks
{
    // The orchestrator registers this suite in Program.cs.
    // Deliberately use only All, Get and the unrotated blueprint's public cell data.
    static void SiteBlueprintDefinitionChecks()
    {
        Run("site blueprint definitions cover all 32 stable kind indices", () =>
        {
            SiteKind[] expected =
            {
                SiteKind.Residence, SiteKind.Shop, SiteKind.Warehouse, SiteKind.Workshop,
                SiteKind.Clinic, SiteKind.Farmhouse, SiteKind.GasStation, SiteKind.PoliceStation,
                SiteKind.School, SiteKind.Apartment, SiteKind.Greenhouse, SiteKind.Substation,
                SiteKind.Restaurant, SiteKind.Pharmacy, SiteKind.HardwareStore, SiteKind.Supermarket,
                SiteKind.Library, SiteKind.FireStation, SiteKind.Hospital, SiteKind.ResearchLab,
                SiteKind.PowerPlant, SiteKind.WaterTreatment, SiteKind.RadioStation, SiteKind.Bunker,
                SiteKind.LumberMill, SiteKind.MineOffice, SiteKind.TrainDepot, SiteKind.Barn,
                SiteKind.HuntingCabin, SiteKind.RangerStation, SiteKind.Motel, SiteKind.RecyclingYard
            };
            Require(Enum.GetValues(typeof(SiteKind)).Length == expected.Length, "SiteKind must contain exactly 32 entries");
            Require(SiteBlueprintCatalog.All != null && SiteBlueprintCatalog.All.Count == expected.Length,
                "catalog must contain exactly 32 definitions");
            for (int i = 0; i < expected.Length; i++)
            {
                Require((int)expected[i] == i, $"{expected[i]} no longer has stable index {i}");
                var plan = SiteBlueprintCatalog.All[i];
                Require(plan != null && plan.kind == expected[i], $"definition index {i} is missing or unordered");
                var found = SiteBlueprintCatalog.Get(expected[i]);
                Require(found != null && found.kind == plan.kind && found.category == plan.category &&
                    found.description == plan.description && found.floorArt == plan.floorArt &&
                    found.biomeMask == plan.biomeMask && found.Width == plan.Width && found.Height == plan.Height,
                    $"{plan.kind}: Get disagrees with All");
                for (int row = 0; row < plan.Rows.Count; row++)
                    Require(found.Rows[row] == plan.Rows[row], $"{plan.kind}: Get returned different row data");
            }
        });
        Run("site blueprint definitions have category art descriptions and nonempty biome eligibility", () =>
        {
            int categories = 0;
            foreach (var plan in SiteBlueprintCatalog.All)
            {
                Require(Enum.IsDefined(typeof(SiteCategory), plan.category), $"{plan.kind}: invalid category");
                categories |= 1 << (int)plan.category;
                Require(!string.IsNullOrWhiteSpace(plan.description), $"{plan.kind}: missing structural description");
                Require(plan.floorArt == "landmark_floor_" + (int)plan.category, $"{plan.kind}: wrong category floor art");
                Require(plan.biomeMask != 0 && (plan.biomeMask & ~WorldGeneration.AllBiomeMask) == 0,
                    $"{plan.kind}: no eligible natural biome or unknown biome bits");
            }
            Require(categories == 63, "the definitions must cover all six site categories");
        });
        Run("site blueprint definitions obey dimensions symbols and north positive cell coordinates", () =>
        {
            const string allowed = " #+.,FMATEBSKbsthnmpkglruvodfwea";
            foreach (var plan in SiteBlueprintCatalog.All)
            {
                Require(plan.Width >= 5 && plan.Width <= 12 && plan.Height >= 5 && plan.Height <= 12,
                    $"{plan.kind}: dimensions must be 5 through 12");
                Require(plan.Rows.Count == plan.Height, $"{plan.kind}: row count differs from Height");
                for (int row = 0; row < plan.Height; row++)
                {
                    Require(plan.Rows[row] != null && plan.Rows[row].Length == plan.Width,
                        $"{plan.kind}: row {row} is null or has a different width");
                    for (int x = 0; x < plan.Width; x++)
                    {
                        char glyph = plan.Rows[row][x];
                        Require(allowed.IndexOf(glyph) >= 0, $"{plan.kind}: unknown ASCII glyph '{glyph}'");
                        Require(plan.At(x, plan.Height - 1 - row) == glyph,
                            $"{plan.kind}: At does not map north-first rows to upward local Y");
                    }
                }
            }
        });
        Run("site blueprint definitions stay within wall door and container budgets", () =>
        {
            foreach (var plan in SiteBlueprintCatalog.All)
            {
                int nodes = 0, doors = 0, crates = 0, floors = 0;
                for (int y = 0; y < plan.Height; y++) for (int x = 0; x < plan.Width; x++)
                {
                    char glyph = plan.At(x, y);
                    if (glyph == '#' || glyph == '+' || SiteBlueprintDefinitionIsCrate(glyph)) nodes++;
                    if (glyph == '+') doors++;
                    if (SiteBlueprintDefinitionIsCrate(glyph)) crates++;
                    if (glyph == '.') floors++;
                }
                Require(nodes <= 75, $"{plan.kind}: {nodes} wall/door/container nodes exceeds 75");
                Require(doors >= 1 && doors <= 5, $"{plan.kind}: door count {doors} is outside 1 through 5");
                Require(crates >= 1 && crates <= 4, $"{plan.kind}: container count {crates} is outside 1 through 4");
                Require(floors > 0, $"{plan.kind}: no enterable indoor floor");
            }
        });
        Run("site blueprint definitions retain unique wall plans under rotations and reflections", () =>
        {
            var signatures = new Dictionary<string, SiteKind>(StringComparer.Ordinal);
            foreach (var plan in SiteBlueprintCatalog.All)
            {
                // Ignore furniture and crates, and treat every door as a wall opening's
                // underlying wall. Names, loot and door relocation alone cannot add a plan.
                string signature = SiteBlueprintDefinitionWallSignature(plan);
                Require(!signatures.TryGetValue(signature, out var earlier),
                    $"{plan.kind}: same wall plan as {earlier} after ignoring props and orientation");
                signatures.Add(signature, plan.kind);
            }
        });
        Run("site blueprint definitions have bbox entrances and traversable door thresholds", () =>
        {
            foreach (var plan in SiteBlueprintCatalog.All)
            {
                bool entrance = false;
                for (int y = 0; y < plan.Height; y++) for (int x = 0; x < plan.Width; x++)
                {
                    if (plan.At(x, y) != '+') continue;
                    bool horizontal = SiteBlueprintDefinitionCanStep(plan, x - 1, y) &&
                        SiteBlueprintDefinitionCanStep(plan, x + 1, y);
                    bool vertical = SiteBlueprintDefinitionCanStep(plan, x, y - 1) &&
                        SiteBlueprintDefinitionCanStep(plan, x, y + 1);
                    Require(horizontal || vertical, $"{plan.kind}: door ({x},{y}) has no clear approach axis");
                    entrance |= x == 0 && SiteBlueprintDefinitionHasIndoorApproach(plan, x + 1, y) ||
                        x == plan.Width - 1 && SiteBlueprintDefinitionHasIndoorApproach(plan, x - 1, y) ||
                        y == 0 && SiteBlueprintDefinitionHasIndoorApproach(plan, x, y + 1) ||
                        y == plan.Height - 1 && SiteBlueprintDefinitionHasIndoorApproach(plan, x, y - 1);
                }
                Require(entrance, $"{plan.kind}: no bbox-edge main entrance leading into the building");
            }
        });
        Run("site blueprint definitions let outside flood reach every walkable cell and interact with every container", () =>
        {
            foreach (var plan in SiteBlueprintCatalog.All)
            {
                bool[] reachable = SiteBlueprintDefinitionExteriorFlood(plan, sealDoors: false);
                int stride = plan.Width + 2;
                for (int y = 0; y < plan.Height; y++) for (int x = 0; x < plan.Width; x++)
                {
                    char glyph = plan.At(x, y);
                    if (glyph == '#') continue;
                    if (!SiteBlueprintDefinitionIsCrate(glyph))
                    {
                        Require(reachable[x + 1 + (y + 1) * stride],
                            $"{plan.kind}: sealed walkable cell ({x},{y}) '{glyph}'");
                        continue;
                    }
                    bool canInteract = false;
                    for (int direction = 0; direction < 4; direction++)
                    {
                        int nx = x + SiteBlueprintDefinitionDx[direction];
                        int ny = y + SiteBlueprintDefinitionDy[direction];
                        if (nx < 0 || ny < 0 || nx >= plan.Width || ny >= plan.Height) continue;
                        char neighbor = plan.At(nx, ny);
                        if (neighbor != ' ' && SiteBlueprintDefinitionCanStep(plan, nx, ny) &&
                            reachable[nx + 1 + (ny + 1) * stride]) canInteract = true;
                    }
                    Require(canInteract, $"{plan.kind}: container ({x},{y}) has no reachable adjacent interaction cell");
                }
            }
        });
        Run("site blueprint definitions enclose indoor floors with real walls and doors", () =>
        {
            foreach (var plan in SiteBlueprintCatalog.All)
            {
                // Containers deliberately do not seal the envelope: loot must never
                // substitute for a missing exterior wall. Outdoor commas/props may stay open.
                bool[] outside = SiteBlueprintDefinitionExteriorFlood(plan, sealDoors: true);
                int stride = plan.Width + 2;
                for (int y = 0; y < plan.Height; y++) for (int x = 0; x < plan.Width; x++)
                    if (plan.At(x, y) == '.')
                        Require(!outside[x + 1 + (y + 1) * stride],
                            $"{plan.kind}: indoor floor ({x},{y}) leaks outside without crossing a wall or door");
            }
        });
    }

    static readonly int[] SiteBlueprintDefinitionDx = { 1, 0, -1, 0 };
    static readonly int[] SiteBlueprintDefinitionDy = { 0, 1, 0, -1 };
    static bool SiteBlueprintDefinitionIsCrate(char glyph) => "FMATEBSK".IndexOf(glyph) >= 0;

    static bool SiteBlueprintDefinitionCanStep(SiteBlueprint plan, int x, int y)
    {
        if (x < 0 || y < 0 || x >= plan.Width || y >= plan.Height) return true;
        char glyph = plan.At(x, y);
        return glyph != '#' && !SiteBlueprintDefinitionIsCrate(glyph);
    }

    static bool SiteBlueprintDefinitionHasIndoorApproach(SiteBlueprint plan, int x, int y)
    {
        if (x < 0 || y < 0 || x >= plan.Width || y >= plan.Height) return false;
        char glyph = plan.At(x, y);
        return glyph != ' ' && glyph != ',' && SiteBlueprintDefinitionCanStep(plan, x, y);
    }

    // Independent four-neighbor BFS over a one-cell exterior halo. It does not
    // call generation, placement, rotation, room topology or catalog flood helpers.
    static bool[] SiteBlueprintDefinitionExteriorFlood(SiteBlueprint plan, bool sealDoors)
    {
        int width = plan.Width + 2, height = plan.Height + 2;
        var seen = new bool[width * height];
        var queue = new Queue<int>();
        seen[0] = true;
        queue.Enqueue(0);
        while (queue.Count > 0)
        {
            int cell = queue.Dequeue(), x = cell % width, y = cell / width;
            for (int direction = 0; direction < 4; direction++)
            {
                int nx = x + SiteBlueprintDefinitionDx[direction];
                int ny = y + SiteBlueprintDefinitionDy[direction];
                if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                int next = nx + ny * width;
                if (seen[next]) continue;
                char glyph = nx == 0 || ny == 0 || nx == width - 1 || ny == height - 1
                    ? ' ' : plan.At(nx - 1, ny - 1);
                if (glyph == '#' || (sealDoors ? glyph == '+' : SiteBlueprintDefinitionIsCrate(glyph))) continue;
                seen[next] = true;
                queue.Enqueue(next);
            }
        }
        return seen;
    }

    static string SiteBlueprintDefinitionWallSignature(SiteBlueprint plan)
    {
        int width = plan.Width, height = plan.Height;
        var grid = new char[width, height];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            char glyph = plan.At(x, y);
            grid[x, y] = glyph == '#' || glyph == '+' ? '#' : glyph == ',' || glyph == ' ' ? glyph : '.';
        }
        string best = null;
        for (int rotation = 0; rotation < 4; rotation++)
        {
            for (int mirror = 0; mirror < 2; mirror++)
            {
                var signature = new StringBuilder();
                signature.Append(width).Append('x').Append(height).Append(':');
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    signature.Append(grid[mirror == 0 ? x : width - 1 - x, y]);
                string candidate = signature.ToString();
                if (best == null || string.CompareOrdinal(candidate, best) < 0) best = candidate;
            }
            var turned = new char[height, width];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                turned[height - 1 - y, x] = grid[x, y];
            grid = turned;
            int previousWidth = width; width = height; height = previousWidth;
        }
        return best;
    }
}
