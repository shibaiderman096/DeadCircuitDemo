using DeadCircuit;

static partial class Checks
{
    static SiteState ExteriorSite(SiteKind kind, int rotation = 0, int x = 0, int y = 0)
    {
        var plan = SiteBlueprintCatalog.Get(kind);
        int doorX = x, doorY = y;
        bool foundDoor = false;
        for (int cy = 0; cy < SiteBlueprintCatalog.Height(plan, rotation) && !foundDoor; cy++)
        for (int cx = 0; cx < SiteBlueprintCatalog.Width(plan, rotation); cx++)
            if (SiteBlueprintCatalog.At(plan, rotation, cx, cy) == '+' &&
                (cx == 0 || cy == 0 || cx == SiteBlueprintCatalog.Width(plan, rotation) - 1 ||
                 cy == SiteBlueprintCatalog.Height(plan, rotation) - 1))
            { doorX = x + cx; doorY = y + cy; foundDoor = true; break; }
        return new SiteState { id = 123, x = x, y = y, kind = kind, rotation = rotation, layoutVersion = 1,
            width = SiteBlueprintCatalog.Width(plan, rotation), height = SiteBlueprintCatalog.Height(plan, rotation), doorX = doorX, doorY = doorY };
    }

    static void NaturalBuildingChecks()
    {
        Run("exteriors cover all 32 natural building shells in four rotations", () =>
        {
            foreach (var plan in SiteBlueprintCatalog.All) for (int rotation = 0; rotation < 4; rotation++)
            {
                var site = ExteriorSite(plan.kind, rotation, -19, -17);
                var shape = NaturalBuildingLayout.Create(site, true);
                Require(shape != null, $"missing exterior: {plan.kind}/{rotation}");
                int floors = FloorWorld.MaxLevel(plan.kind) + 1;
                Require(shape.floors == floors && shape.Rise == floors * NaturalBuildingLayout.FloorRise, "incorrect visible storeys");
                Require(shape.Rise <= NaturalBuildingLayout.MaxRiseCells, "roof extends past streaming margin");
                for (int y = 0; y < site.height; y++) for (int x = 0; x < site.width; x++)
                {
                    char cell = SiteBlueprintCatalog.At(site, site.x + x, site.y + y);
                    if (cell == ' ' || cell == ',') Require(!shape.Covers(x, y), "roof covers authored outdoor area");
                    if (shape.Covers(x, y)) Require(shape.IsInterior(x, y) || cell == '#' || cell == '+', "roof mask covers outdoor fixture");
                }
                Require(shape.Entrance(site.doorX - site.x, site.doorY - site.y) &&
                    shape.Contains(site.doorX - site.x, site.doorY - site.y), "primary exterior entrance is not preserved");
                Require(shape.Covers(site.doorX - site.x, site.doorY - site.y), "roof leaves a hole over the doorway");
            }
        });
        Run("exteriors preserve outdoor pumps and yards instead of roofing a bounding box", () =>
        {
            foreach (int rotation in Enumerable.Range(0, 4))
            {
                var site = ExteriorSite(SiteKind.GasStation, rotation);
                var shape = NaturalBuildingLayout.Create(site, true);
                int pumps = 0, indoors = 0, entries = 0;
                for (int y = 0; y < site.height; y++) for (int x = 0; x < site.width; x++)
                {
                    char cell = SiteBlueprintCatalog.At(site, x, y);
                    if (cell == 'p') { pumps++; Require(!shape.Covers(x, y), "outdoor fuel pump gets an opaque roof"); }
                    if (cell == 'F') { indoors++; Require(shape.Covers(x, y), "shop loot remains exposed outdoors"); }
                    if (shape.Entrance(x, y)) entries++;
                }
                Require(pumps == 2 && indoors == 1 && entries >= 1, "fixture did not exercise the forecourt/entry");
            }
            foreach (var plan in SiteBlueprintCatalog.All) for (int rotation = 0; rotation < 4; rotation++)
            {
                var site = ExteriorSite(plan.kind, rotation);
                var shape = NaturalBuildingLayout.Create(site, true);
                Require(shape.Entrance(site.doorX - site.x, site.doorY - site.y),
                    $"primary door is not exposed as an exterior entrance: {plan.kind}/{rotation}");
            }
            var facadeLayout = NaturalBuildingLayout.Create(ExteriorSite(SiteKind.Residence), true);
            bool foundSouthWall = false;
            for (int y = 0; y < facadeLayout.height; y++) for (int x = 0; x < facadeLayout.width; x++)
                if (facadeLayout.SouthFacade(x, y))
                {
                    foundSouthWall = true;
                    Require(facadeLayout.SouthFacade(x, y, true), "existing exterior wall loses its facade");
                    Require(!facadeLayout.SouthFacade(x, y, false), "demolished exterior wall keeps its facade");
                }
            Require(foundSouthWall, "test blueprint has no south-facing exterior wall");
        });
        Run("exteriors height follows above-ground floors and never basement count", () =>
        {
            var apartment = NaturalBuildingLayout.Create(ExteriorSite(SiteKind.Apartment), true);
            var bungalow = NaturalBuildingLayout.Create(ExteriorSite(SiteKind.HuntingCabin), true);
            var bunker = NaturalBuildingLayout.Create(ExteriorSite(SiteKind.Bunker), true);
            Require(apartment.floors == 4 && bungalow.floors == 1 && bunker.floors == 1, "high/low/basement silhouettes wrong");
            Require(apartment.Rise > bungalow.Rise, "high-rise exterior is flat");
            Require(NaturalBuildingLayout.Create(ExteriorSite(SiteKind.Apartment), false).floors == 1, "disabled floor world invents storeys");
            var upper = ExteriorSite(SiteKind.Apartment); upper.parentSiteId = 7; upper.level = 1;
            Require(NaturalBuildingLayout.Create(upper, true) == null, "isolated upper floor gets an outdoor roof");
            var basement = ExteriorSite(SiteKind.Bunker); basement.parentSiteId = 7; basement.level = -1;
            Require(NaturalBuildingLayout.Create(basement, true) == null, "basement gets an outdoor facade");
        });
        Run("exteriors entry and tall-building occlusion work at signed cross-chunk positions", () =>
        {
            foreach (int rotation in Enumerable.Range(0, 4))
            {
                var site = ExteriorSite(SiteKind.Apartment, rotation, -19, -17);
                var shape = NaturalBuildingLayout.Create(site, true);
                int entries = 0;
                for (int y = 0; y < site.height; y++) for (int x = 0; x < site.width; x++)
                {
                    if (!shape.Entrance(x, y)) continue;
                    entries++;
                    Require(shape.Covers(x, y), "roof mask cuts a hole over an open entrance");
                    float px = site.x + x, py = site.y + y;
                    Require(shape.Contains(px - site.x, py - site.y), "entry does not cut away the shell");
                }
                Require(entries > 0, "rotated exterior has no entry");
                Require(shape.Entrance(site.doorX - site.x, site.doorY - site.y), "recorded entrance differs from rotated shell mask");
                int top = site.height - 1, column = -1;
                for (int x = 0; x < site.width; x++) if (shape.Covers(x, top)) { column = x; break; }
                Require(column >= 0 && shape.Occludes(column, top + shape.Rise + .4f), $"raised roof cannot mark a local survivor within its projected facade: rotation={rotation}, size={site.width}x{site.height}, column={column}, top={top}, floors={shape.floors}, rise={shape.Rise}");
                Require(!shape.Contains(column, top + 1), "standing behind a high building exposes its interior");
                Require(!shape.Occludes(-1, top), "occlusion escapes the shell width");
            }
        });
        Run("exteriors reject missing or malformed natural site metadata", () =>
        {
            Require(NaturalBuildingLayout.Create(null, true) == null, "null site rendered");
            var site = ExteriorSite(SiteKind.Residence); site.layoutVersion = 0;
            Require(NaturalBuildingLayout.Create(site, true) == null, "no authored plan was invented");
            site.layoutVersion = 1; site.rotation = 4;
            Require(NaturalBuildingLayout.Create(site, true) == null, "invalid rotation accepted");
            site.rotation = 0; site.width++;
            Require(NaturalBuildingLayout.Create(site, true) == null, "invalid bbox accepted");
        });
    }
}
