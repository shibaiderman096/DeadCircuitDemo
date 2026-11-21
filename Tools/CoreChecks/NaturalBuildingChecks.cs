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
        Run("exteriors interior door thresholds never close the indoor cutaway", () =>
        {
            int doors = 0;
            foreach (var plan in SiteBlueprintCatalog.All) for (int rotation = 0; rotation < 4; rotation++)
            {
                var shape = NaturalBuildingLayout.Create(ExteriorSite(plan.kind, rotation, -19, -17), true);
                for (int y = 0; y < shape.height; y++) for (int x = 0; x < shape.width; x++)
                {
                    if (shape.Cell(x, y) != '+' || !shape.Covers(x, y) || shape.Entrance(x, y)) continue;
                    doors++;
                    foreach (var offset in new[] { (0f, 0f), (-.49f, 0f), (.49f, 0f), (0f, -.49f), (0f, .49f) })
                        Require(shape.Contains(x + offset.Item1, y + offset.Item2),
                            $"standing in an internal doorway closes the roof: {plan.kind}/{rotation} at {x},{y}");
                }
            }
            Require(doors > 0, "no interior doorway was exercised");
        });
        Run("exteriors indoor cutaway still excludes walls and outdoor yards", () =>
        {
            foreach (var plan in SiteBlueprintCatalog.All) for (int rotation = 0; rotation < 4; rotation++)
            {
                var shape = NaturalBuildingLayout.Create(ExteriorSite(plan.kind, rotation), true);
                for (int y = -1; y <= shape.height; y++) for (int x = -1; x <= shape.width; x++)
                    if (shape.Cell(x, y) == '#' || !shape.Covers(x, y))
                        Require(!shape.Contains(x, y), "wall/outdoor position exposes a building interior");
            }
        });
        Run("exteriors real entrance doors stay above the opaque shell in every rotation", () =>
        {
            foreach (var plan in SiteBlueprintCatalog.All) for (int rotation = 0; rotation < 4; rotation++)
            {
                var shape = NaturalBuildingLayout.Create(ExteriorSite(plan.kind, rotation), true);
                for (int y = 0; y < shape.height; y++) for (int x = 0; x < shape.width; x++)
                {
                    int normal = 6000 - y * 80;
                    int shell = 6000 - (shape.minY * 80 - 40) + 24;
                    if (shape.Entrance(x, y))
                        Require(shape.EntranceOrder(x, y, normal, shell) > shell,
                            $"door is hidden behind its roof/facade: {plan.kind}/{rotation} at {x},{y}");
                    else Require(shape.EntranceOrder(x, y, normal, shell) == normal,
                        "interior door or non-door acquired an exterior overlay");
                }
            }
        });
        Run("exteriors entrance guides point from real indoor doors to outdoor space", () =>
        {
            var directions = new HashSet<(int, int)>();
            foreach (var plan in SiteBlueprintCatalog.All) for (int rotation = 0; rotation < 4; rotation++)
            {
                var shape = NaturalBuildingLayout.Create(ExteriorSite(plan.kind, rotation, -19, -17), true);
                for (int y = 0; y < shape.height; y++) for (int x = 0; x < shape.width; x++)
                {
                    bool entry = shape.EntranceOutward(x, y, out int dx, out int dy);
                    Require(entry == shape.Entrance(x, y), "entrance has no guide or internal door has a guide");
                    if (!entry) continue;
                    Require(Math.Abs(dx) + Math.Abs(dy) == 1 && shape.IsInterior(x - dx, y - dy) &&
                        !shape.Covers(x + dx, y + dy), $"guide points into the building: {plan.kind}/{rotation}");
                    directions.Add((dx, dy));
                }
            }
            Require(directions.Count == 4, "entrance guides did not cover all four directions");
        });
        Run("exteriors primary entrances still open and admit a survivor in four directions", () =>
        {
            foreach (var plan in SiteBlueprintCatalog.All) for (int rotation = 0; rotation < 4; rotation++)
            {
                var site = ExteriorSite(plan.kind, rotation, 24, 24);
                var shape = NaturalBuildingLayout.Create(site, true);
                Require(shape.EntranceOutward(site.doorX - site.x, site.doorY - site.y, out int dx, out int dy), "missing entry approach");
                var sim = new WorldSimulator(Flat(era: TechEra.Primitive));
                var p = Builder(sim);
                for (int y = 0; y < site.height; y++) for (int x = 0; x < site.width; x++)
                {
                    char cell = shape.Cell(x, y);
                    if (cell == '#') Add(sim.State, BuildKind.Wall, site.x + x, site.y + y);
                    if (cell == '+') Add(sim.State, BuildKind.Door, site.x + x, site.y + y, x == 0 || x == site.width - 1 ? 1 : 0);
                }
                p.x = site.doorX + dx * 1.5f; p.y = site.doorY + dy * 1.5f;
                Advance(sim, .05f);
                Require(sim.IsBlocked(site.doorX, site.doorY), "closed door did not block entry");
                sim.Apply(Command(p, "interact", site.doorX, site.doorY));
                Require(!sim.IsBlocked(site.doorX, site.doorY), $"real door could not open: {plan.kind}/{rotation}");
                sim.Apply(new PlayerCommand { playerId = p.id, action = "move", moveX = -dx, moveY = -dy });
                Advance(sim, .5f);
                Require(shape.Contains(p.x - site.x, p.y - site.y), $"survivor could not reach entry: {plan.kind}/{rotation}");
            }
        });
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
