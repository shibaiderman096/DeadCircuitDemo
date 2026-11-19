using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public static partial class ChunkTerrain
    {
        private static void AddBlueprintSite(Region r, int chunkX, int chunkY)
        {
            int seed = r.world.seed;
            double city = Urbanity(seed, r.x + 8, r.y + 8);
            double chance = (r.settings.preset == WorldPreset.Urban ? .52 : r.settings.preset == WorldPreset.Wilderness ? .06 : .22) +
                r.settings.cityDensity * (r.settings.preset == WorldPreset.Wilderness ? .11 : .2);
            if (Unit(seed, chunkX, chunkY, 0x517eu) >= chance) return;
            bool developed = r.settings.cityDensity > 0 && city >= CityThreshold(r.settings);
            int biome = r.biomes[8 + 8 * Size] - '0';
            var eligible = new List<SiteBlueprint>();
            foreach (var candidate in SiteBlueprintCatalog.All)
            {
                if ((candidate.biomeMask & (1 << biome)) == 0) continue;
                if (!developed && candidate.category != SiteCategory.Rural && candidate.category != SiteCategory.Special &&
                    candidate.kind != SiteKind.HuntingCabin && candidate.kind != SiteKind.LumberMill && candidate.kind != SiteKind.MineOffice) continue;
                eligible.Add(candidate);
            }
            if (eligible.Count == 0) return;
            var plan = eligible[(int)(Hash(seed, chunkX, chunkY, 0x517fu) % (uint)eligible.Count)];
            int rotation = (int)(Hash(seed, chunkX, chunkY, 0xb100u) & 3);
            int width = SiteBlueprintCatalog.Width(plan, rotation), height = SiteBlueprintCatalog.Height(plan, rotation);
            var positions = new List<int>();
            for (int y = 2; y <= 14 - height; y++) for (int x = 2; x <= 14 - width; x++) positions.Add(x + y * Size);
            SortCells(positions, r, 0x5170u);
            foreach (int position in positions)
            {
                int sx = position % Size, sy = position / Size;
                bool clear = true;
                for (int y = sy - 1; y <= sy + height; y++) for (int x = sx - 1; x <= sx + width; x++)
                {
                    int cell = x + y * Size;
                    bool inside = x >= sx && x < sx + width && y >= sy && y < sy + height;
                    if (r.ground[cell] == '2' || r.occupied[cell] || (inside && r.reserved[cell])) clear = false;
                }
                if (!clear) continue;
                var blocked = (bool[])r.blocked.Clone();
                int doorX = -1, doorY = -1, nodes = 0;
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    char c = SiteBlueprintCatalog.At(plan, rotation, x, y);
                    if (c == '#' || SiteBlueprintCatalog.Crates.IndexOf(c) >= 0) { blocked[sx + x + (sy + y) * Size] = true; nodes++; }
                    if (c == '+' && doorX < 0 && (x == 0 || y == 0 || x == width - 1 || y == height - 1))
                    { doorX = sx + x; doorY = sy + y; }
                }
                if (doorX < 0 || nodes > MaxNodesPerChunk) continue;
                int[] parents = Paths(r.ground, blocked);
                for (int y = 0; y < height && clear; y++) for (int x = 0; x < width; x++)
                {
                    char c = SiteBlueprintCatalog.At(plan, rotation, x, y);
                    int cell = sx + x + (sy + y) * Size;
                    if (c == ' ' || c == '#') continue;
                    if (SiteBlueprintCatalog.Crates.IndexOf(c) >= 0)
                    {
                        if (parents[cell - 1] == -2 && parents[cell + 1] == -2 && parents[cell - Size] == -2 && parents[cell + Size] == -2) clear = false;
                    }
                    else if (parents[cell] == -2) clear = false;
                }
                if (!clear) continue;
                var site = new SiteState { id = r.nextId++, kind = plan.kind, x = r.x + sx, y = r.y + sy,
                    width = width, height = height, doorX = r.x + doorX, doorY = r.y + doorY,
                    layoutVersion = SiteBlueprintCatalog.LayoutVersion, rotation = rotation,
                    district = plan.category == SiteCategory.Industrial ? DistrictKind.Industrial :
                        plan.category == SiteCategory.Rural || !developed ? DistrictKind.Wilderness : city > .6 ? DistrictKind.Urban : DistrictKind.Suburb };
                r.sites.Add(site);
                for (int y = sy - 1; y <= sy + height; y++) for (int x = sx - 1; x <= sx + width; x++) r.reserved[x + y * Size] = true;
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    char c = SiteBlueprintCatalog.At(plan, rotation, x, y);
                    int cell = sx + x + (sy + y) * Size;
                    if (c != ' ') r.ground[cell] = c == ',' ? '1' : '4';
                    if (c == '#') AddNode(r, cell, NodeKind.Wall, 14);
                    else if (SiteBlueprintCatalog.Crates.IndexOf(c) >= 0)
                        AddNode(r, cell, NodeKind.Crate, 8 + (int)(Hash(seed, r.x + sx + x, r.y + sy + y, 0xc2a0u) % 9));
                    else if (c == '+')
                    {
                        r.buildings.Add(new BuildingState { id = r.nextId++, kind = BuildKind.Door, x = r.x + sx + x, y = r.y + sy + y,
                            rotation = (x == 0 || x == width - 1) ? 1 : 0,
                            footprintVersion = 1, hp = WorldSimulator.BuildingMaxHealth(BuildKind.Door) });
                        r.occupied[cell] = true;
                        // Every exterior entrance keeps a dry approach to the chunk boundary.
                        if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                            for (int path = parents[cell]; path >= 0; path = parents[path])
                                if (path % Size < sx || path % Size >= sx + width || path / Size < sy || path / Size >= sy + height)
                                { r.ground[path] = '3'; r.reserved[path] = true; }
                    }
                }
                return;
            }
        }
    }
}
