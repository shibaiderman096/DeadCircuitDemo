using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public enum SiteCategory { Residential, Commercial, Civic, Industrial, Rural, Special }

    // Immutable, version-one authored plans. Rows are stored north to south;
    // public cell coordinates follow the world's north-positive Y convention.
    public sealed class SiteBlueprint
    {
        public readonly SiteKind kind;
        public readonly SiteCategory category;
        public readonly string description, floorArt;
        public readonly int biomeMask;
        public IReadOnlyList<string> Rows { get; }
        public int Width => Rows[0].Length;
        public int Height => Rows.Count;
        internal SiteBlueprint(SiteKind kind, SiteCategory category, string description,
            string floorArt, int biomeMask, params string[] rows)
        {
            this.kind = kind; this.category = category; this.description = description;
            this.floorArt = floorArt; this.biomeMask = biomeMask;
            Rows = Array.AsReadOnly((string[])rows.Clone());
        }
        public char At(int x, int y) => Rows[Height - 1 - y][x];
    }

    public static partial class SiteBlueprintCatalog
    {
        public const int LayoutVersion = 1;
        public const string Props = "bsthnmpkglruvodfwea";
        public const string Crates = "FMATEBSK";
        public static readonly IReadOnlyList<SiteBlueprint> All = Array.AsReadOnly(CreateDefinitions());
        public static SiteBlueprint Get(SiteKind kind) => (int)kind >= 0 && (int)kind < All.Count ? All[(int)kind] : null;
        public static SiteBlueprint Get(SiteState site) => site == null ? null : FloorWorld.GetPlan(site);
        public static int Width(SiteBlueprint plan, int rotation) => (rotation & 1) == 0 ? plan.Width : plan.Height;
        public static int Height(SiteBlueprint plan, int rotation) => (rotation & 1) == 0 ? plan.Height : plan.Width;
        public static char At(SiteBlueprint plan, int rotation, int x, int y)
        {
            switch (rotation & 3)
            {
                case 1: return plan.At(y, plan.Height - 1 - x);
                case 2: return plan.At(plan.Width - 1 - x, plan.Height - 1 - y);
                case 3: return plan.At(plan.Width - 1 - y, x);
                default: return plan.At(x, y);
            }
        }
        public static char At(SiteState site, int x, int y)
        {
            var plan = Get(site);
            if (site == null || site.layoutVersion != LayoutVersion || plan == null ||
                x < site.x || y < site.y || x >= site.x + site.width || y >= site.y + site.height) return ' ';
            return At(plan, site.rotation, x - site.x, y - site.y);
        }
        public static bool ValidMetadata(SiteState site)
        {
            if (site == null || site.parentSiteId < 0 || (site.parentSiteId == 0) != (site.level == 0)) return false;
            if (site.layoutVersion == 0) return site.parentSiteId == 0 && site.rotation == 0 && (int)site.kind < WorldGeneration.LegacySiteCount;
            var plan = Get(site);
            return site.layoutVersion == LayoutVersion && site.rotation >= 0 && site.rotation < 4 && plan != null &&
                site.width == Width(plan, site.rotation) && site.height == Height(plan, site.rotation) &&
                (site.parentSiteId == 0 ? At(site, site.doorX, site.doorY) == '+' :
                    site.doorX >= site.x && site.doorX < site.x + site.width &&
                    site.doorY >= site.y && site.doorY < site.y + site.height);
        }
        public static char CrateAt(WorldState world, int x, int y)
        {
            foreach (var site in world.sites)
            {
                char cell = At(site, x, y);
                if (Crates.IndexOf(cell) >= 0) return cell;
            }
            return '\0';
        }
    }
}
