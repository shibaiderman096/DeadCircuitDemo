using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DeadCircuit;

static partial class Checks
{
    // Program.cs registration belongs to the orchestrator.
    static void SettlementChecks()
    {
        Run("settlement stable indices and immutable Chinese catalog", () =>
        {
            string[] names = { "未指定", "高密度市区", "低密度市区", "高密度郊区", "低密度郊区", "乡村" };
            Require(Enum.GetValues(typeof(SettlementKind)).Length == 6, "saved settlement enum changed");
            Require(SettlementCatalog.All.Count == 6, "catalog must include zero and five zones");
            for (int i = 0; i < names.Length; i++)
            {
                var rule = SettlementCatalog.All[i];
                Require((int)rule.kind == i && rule.name == names[i], "settlement index/name mismatch");
                Require(ReferenceEquals(rule, SettlementCatalog.Get((SettlementKind)i)), "catalog lookup changed");
            }
            Require(SettlementCatalog.Get((SettlementKind)(-1)) == null && SettlementCatalog.Get((SettlementKind)6) == null, "unknown settlement accepted");
        });
        Run("settlement field covers five continuous zones with deterministic signed coordinates", () =>
        {
            foreach (int seed in new[] { 73149, -73149, 0, int.MinValue, int.MaxValue })
            {
                var a = SettlementWorld(seed); var b = SettlementWorld(seed);
                var coordinates = new List<(int x, int y)>();
                for (int y = -3072; y <= 3072; y += 48)
                    for (int x = -3072; x <= 3072; x += 48) coordinates.Add((x, y));
                var sampled = coordinates.Select(c => SettlementGeneration.Sample(a, c.x, c.y)).ToArray();
                Require(sampled.Distinct().Count() == 5 && sampled.All(k => (int)k >= 1 && (int)k <= 5), "missing settlement zone for seed " + seed);
                for (int i = coordinates.Count - 1; i >= 0; i--)
                {
                    var c = coordinates[i];
                    Require(SettlementGeneration.Sample(b, c.x, c.y) == sampled[i], "sampling depends on traversal order");
                    Require(Math.Abs((int)sampled[i] - (int)SettlementGeneration.Sample(b, c.x + 1, c.y)) <= 1, "one-tile zoning salt-and-pepper");
                }
            }
            var world = SettlementWorld();
            int[] edges = { int.MinValue, -1048576, -513, -512, -33, -1, 0, 1, 32, 1048575, int.MaxValue };
            var signed = edges.SelectMany(x => edges.Select(y => SettlementGeneration.Sample(world, x, y))).ToArray();
            var replay = SettlementWorld();
            for (int i = edges.Length - 1; i >= 0; i--) for (int j = edges.Length - 1; j >= 0; j--)
                Require(SettlementGeneration.Sample(replay, edges[i], edges[j]) == signed[i * edges.Length + j], "signed/extreme sampling changed");
        });
        Run("settlement presets and density monotonically shift urban coverage", () =>
        {
            var wilderness = SettlementWorld(73149, WorldPreset.Wilderness);
            var balanced = SettlementWorld();
            var urban = SettlementWorld(73149, WorldPreset.Urban);
            var sparse = SettlementWorld(73149, WorldPreset.Balanced, 0);
            var dense = SettlementWorld(73149, WorldPreset.Balanced, 2);
            int wildCity = 0, balancedCity = 0, urbanCity = 0, denseCity = 0, seedChanges = 0;
            var otherSeed = SettlementWorld(-73149);
            for (int y = -2048; y < 2048; y += 32) for (int x = -2048; x < 2048; x += 32)
            {
                int w = (int)SettlementGeneration.Sample(wilderness, x, y), b = (int)SettlementGeneration.Sample(balanced, x, y);
                int u = (int)SettlementGeneration.Sample(urban, x, y), d = (int)SettlementGeneration.Sample(dense, x, y);
                Require(u <= b && b <= w && d <= b, "preset or density reverses zoning at a fixed coordinate");
                Require(SettlementGeneration.Sample(sparse, x, y) == SettlementKind.Rural, "zero density retained city/suburb zoning");
                if (w <= 2) wildCity++; if (b <= 2) balancedCity++; if (u <= 2) urbanCity++; if (d <= 2) denseCity++;
                if ((int)SettlementGeneration.Sample(otherSeed, x, y) != b) seedChanges++;
            }
            Require(wildCity < balancedCity && balancedCity < urbanCity && balancedCity < denseCity, "preset/density has no aggregate effect");
            Require(seedChanges > 1000, "world seed does not change settlement belts");
        });
        Run("settlement road density decreases across all five zones and keeps signed arterials", () =>
        {
            var world = SettlementWorld();
            var cells = new int[6]; var roads = new int[6];
            for (int y = -2047; y < 2048; y += 7) for (int x = -2047; x < 2048; x += 7)
            {
                int kind = (int)SettlementGeneration.Sample(world, x, y); cells[kind]++;
                if (SettlementGeneration.Street(world, x, y)) roads[kind]++;
            }
            for (int k = 1; k <= 5; k++)
            {
                Require(cells[k] > 500 && roads[k] > 0, "missing road sample class " + k);
                if (k > 1) Require((double)roads[k - 1] / cells[k - 1] > (double)roads[k] / cells[k], "road density not descending at class " + k);
            }
            foreach (int n in new[] { int.MinValue, -1048576, -513, -1, 0, 513, 1048575, int.MaxValue })
                Require(SettlementGeneration.Street(world, n, 32) && SettlementGeneration.Street(world, 33, n) &&
                    SettlementGeneration.Street(world, n, -512) && SettlementGeneration.Street(world, -512, n), "arterial disconnects in signed coordinates");
            var rural = SettlementWorld(73149, WorldPreset.Balanced, 0);
            Require(SettlementGeneration.Street(rural, -512, -17) && !SettlementGeneration.Street(rural, -511, -17), "negative road modulo/width incorrect");
        });
        Run("settlement site chances are bounded ordered and respond to generation settings", () =>
        {
            foreach (WorldPreset preset in Enum.GetValues(typeof(WorldPreset))) for (int density = 0; density <= 2; density++)
            {
                var world = SettlementWorld(73149, preset, density);
                double previous = 1;
                for (int k = 1; k <= 5; k++)
                {
                    double chance = SettlementGeneration.SiteChance(world, (SettlementKind)k);
                    Require(chance > 0 && chance < previous, "site chance is not bounded/strictly descending");
                    previous = chance;
                }
                Require(SettlementGeneration.SiteChance(world, SettlementKind.Unspecified) == 0, "unspecified has gen5 sites");
            }
            for (int k = 1; k <= 5; k++)
            {
                var kind = (SettlementKind)k;
                double balanced = SettlementGeneration.SiteChance(SettlementWorld(), kind);
                Require(SettlementGeneration.SiteChance(SettlementWorld(73149, WorldPreset.Wilderness), kind) < balanced &&
                    balanced < SettlementGeneration.SiteChance(SettlementWorld(73149, WorldPreset.Urban), kind), "preset has no site chance effect");
                Require(SettlementGeneration.SiteChance(SettlementWorld(73149, WorldPreset.Balanced, 0), kind) < balanced &&
                    balanced < SettlementGeneration.SiteChance(SettlementWorld(73149, WorldPreset.Balanced, 2), kind), "density has no site chance effect");
            }
        });
        Run("settlement building uses distinguish urban suburban rural and mixed industry", () =>
        {
            Func<SettlementKind, SiteKind, int> weight = (zone, site) => SettlementGeneration.BlueprintWeight(zone, site, BiomeKind.Meadow);
            Require(weight(SettlementKind.HighDensityCity, SiteKind.Apartment) > weight(SettlementKind.HighDensityCity, SiteKind.Residence) &&
                weight(SettlementKind.HighDensityCity, SiteKind.Hospital) > weight(SettlementKind.HighDensitySuburb, SiteKind.Hospital), "city lacks apartment/hospital preference");
            Require(weight(SettlementKind.HighDensitySuburb, SiteKind.Residence) > weight(SettlementKind.HighDensitySuburb, SiteKind.Apartment) &&
                weight(SettlementKind.LowDensitySuburb, SiteKind.Shop) > weight(SettlementKind.LowDensitySuburb, SiteKind.Supermarket), "suburbs lack homes/small retail preference");
            Require(weight(SettlementKind.Rural, SiteKind.Farmhouse) > weight(SettlementKind.Rural, SiteKind.Shop) &&
                weight(SettlementKind.Rural, SiteKind.Barn) > weight(SettlementKind.LowDensitySuburb, SiteKind.Barn) &&
                SettlementGeneration.BlueprintWeight(SettlementKind.Rural, SiteKind.HuntingCabin, BiomeKind.ConiferousForest) >
                    SettlementGeneration.BlueprintWeight(SettlementKind.HighDensitySuburb, SiteKind.HuntingCabin, BiomeKind.ConiferousForest), "rural lacks agriculture/cabins");
            Require(weight(SettlementKind.HighDensityCity, SiteKind.Farmhouse) == 0 && weight(SettlementKind.Rural, SiteKind.Apartment) == 0 &&
                weight(SettlementKind.Rural, SiteKind.Hospital) == 0, "incompatible building uses not filtered");
            for (int k = 1; k <= 5; k++) Require(weight((SettlementKind)k, SiteKind.Workshop) > 0, "industry cannot mix in class " + k);
            foreach (var plan in SiteBlueprintCatalog.All)
            {
                bool reachable = false;
                for (int k = 1; k <= 5; k++) for (int b = 0; b < WorldGeneration.BiomeCount; b++)
                {
                    int actual = SettlementGeneration.BlueprintWeight((SettlementKind)k, plan.kind, (BiomeKind)b);
                    Require(actual >= 0 && ((plan.biomeMask & (1 << b)) != 0 || actual == 0), "natural biome eligibility bypassed");
                    reachable |= actual > 0;
                }
                Require(reachable, "building permanently excluded from gen5: " + plan.kind);
            }
            Require(SettlementGeneration.BlueprintWeight(SettlementKind.Unspecified, SiteKind.Shop, BiomeKind.Meadow) == 0 &&
                SettlementGeneration.BlueprintWeight(SettlementKind.Rural, SiteKind.Shop, (BiomeKind)WorldGeneration.BiomeCount) == 0 &&
                SettlementGeneration.BlueprintWeight(SettlementKind.Rural, (SiteKind)(-1), BiomeKind.Meadow) == 0, "invalid weighted request accepted");
        });
        Run("settlement weighted blueprints are deterministic and honor all fifty biome zone combinations", () =>
        {
            var a = SettlementWorld(); var b = SettlementWorld();
            for (int k = 1; k <= 5; k++) for (int biome = 0; biome < WorldGeneration.BiomeCount; biome++)
            {
                var selected = new SiteKind[512];
                for (int i = 0; i < selected.Length; i++)
                {
                    var plan = SettlementGeneration.SelectBlueprint(a, -4096 + i * 16, 2048 - i * 7, (SettlementKind)k, (BiomeKind)biome);
                    Require(plan != null && (plan.biomeMask & (1 << biome)) != 0 &&
                        SettlementGeneration.BlueprintWeight((SettlementKind)k, plan.kind, (BiomeKind)biome) > 0, "weighted draw ignored biome/zone");
                    selected[i] = plan.kind;
                }
                for (int i = selected.Length - 1; i >= 0; i--)
                    Require(SettlementGeneration.SelectBlueprint(b, -4096 + i * 16, 2048 - i * 7, (SettlementKind)k, (BiomeKind)biome).kind == selected[i], "weighted draw depends on traversal order");
                Require(selected.Distinct().Count() >= 4, "weighted selector degenerates to a single building");
            }
            Require(SettlementGeneration.SelectBlueprint(a, 0, 0, SettlementKind.Unspecified, BiomeKind.Meadow) == null &&
                    SettlementGeneration.SelectBlueprint(a, 0, 0, SettlementKind.Rural, (BiomeKind)WorldGeneration.BiomeCount) == null, "empty weighted pool did not return null");
            int apartments = 0, residences = 0, hospitals = 0, ruralAgriculture = 0, ruralShops = 0;
            for (int i = 0; i < 8192; i++)
            {
                var city = SettlementGeneration.SelectBlueprint(a, -i * 17, i * 31, SettlementKind.HighDensityCity, BiomeKind.Meadow).kind;
                if (city == SiteKind.Apartment) apartments++; if (city == SiteKind.Residence) residences++; if (city == SiteKind.Hospital) hospitals++;
                var rural = SettlementGeneration.SelectBlueprint(a, -i * 17, i * 31, SettlementKind.Rural, BiomeKind.Meadow).kind;
                if (rural == SiteKind.Farmhouse || rural == SiteKind.Greenhouse || rural == SiteKind.Barn) ruralAgriculture++;
                if (rural == SiteKind.Shop) ruralShops++;
            }
            Require(apartments > residences * 3 && hospitals > residences * 2, "weighted city draws behave like a uniform pool");
            Require(ruralAgriculture > ruralShops * 5, "weighted rural draws ignore agriculture");
        });
        Run("settlement site roll matches descending probabilities without mutable state", () =>
        {
            var world = SettlementWorld(); var replay = SettlementWorld();
            var candidates = new int[6]; var sites = new int[6];
            for (int y = -4096; y < 4096; y += 32) for (int x = -4096; x < 4096; x += 32)
            {
                var kind = SettlementGeneration.Sample(world, x, y); candidates[(int)kind]++;
                bool placed = SettlementGeneration.ShouldPlaceSite(world, x, y);
                if (placed) sites[(int)kind]++;
                Require(placed == SettlementGeneration.ShouldPlaceSite(replay, x, y), "site roll uses mutable RNG");
            }
            double previous = 1;
            for (int k = 1; k <= 5; k++)
            {
                double observed = (double)sites[k] / candidates[k];
                Require(candidates[k] > 1000 && observed < previous &&
                    Math.Abs(observed - SettlementGeneration.SiteChance(world, (SettlementKind)k)) < .035, "observed site chance diverges in class " + k);
                previous = observed;
            }
            Require(world.chunks.records.Count == 0 && world.sites.Count == 0 && world.nodes.Count == 0 && world.buildings.Count == 0 &&
                world.nextId == ChunkStreaming.DynamicIdStart && world.chunks.revision == 0, "sampling changed world state");
        });
        Run("settlement gen3 gen4 terrain sites loot IDs and health match frozen baseline", () =>
        {
            Require(SettlementLegacyFingerprint(3) == "75EAD79707460D75FC70BBE1B613B3018774E20C4A87E4580C81FD0E5370F61A", "gen3 output changed");
            Require(SettlementLegacyFingerprint(4) == "1792E0FC687FF67344F070711819DA213C47E107E18CB8F504424014D6B9F53C", "gen4 output changed");
        });
        Run("settlement generated gen5 blueprints record all five zones and eligible natural biomes", () =>
        {
            const int size = ChunkStreaming.ChunkSize, center = size / 2;
            var counts = new int[6]; int biomesSeen = 0, streetCells = 0;
            for (int cy = -160; cy <= 160 && (counts.Skip(1).Min() < 6 || biomesSeen != 63); cy += 3)
                for (int cx = -160; cx <= 160 && (counts.Skip(1).Min() < 6 || biomesSeen != 63); cx += 3)
                {
                    var world = SettlementWorld();
                    int centerX = cx * size + center, centerY = cy * size + center;
                    var expected = SettlementGeneration.Sample(world, centerX, centerY);
                    if (counts[(int)expected] >= 6 && biomesSeen == 63) continue;
                    Require(ChunkTerrain.Generate(world, cx, cy), "gen5 chunk refused");
                    var chunk = world.chunks.records[0];
                    foreach (char biome in chunk.biomes) biomesSeen |= 1 << (biome - '0');
                    for (int localY = 0; localY < size; localY++) for (int localX = 0; localX < size; localX++)
                    {
                        if (!SettlementGeneration.Street(world, cx * size + localX, cy * size + localY)) continue;
                        streetCells++;
                        Require(chunk.ground[localX + localY * size] == '3', "generated ground lost a settlement street");
                    }
                    Require(world.nodes.Count <= ChunkTerrain.MaxNodesPerChunk && world.sites.Count <= 1, "gen5 exceeded staged chunk budget");
                    foreach (var site in world.sites)
                    {
                        Require(site.settlement == expected && site.settlement == SettlementGeneration.Sample(world, site.x, site.y), "site settlement metadata disagrees with center/anchor");
                        var plan = SiteBlueprintCatalog.Get(site.kind);
                        Require(SiteBlueprintCatalog.ValidMetadata(site), "gen5 plan metadata invalid");
                        var biome = (BiomeKind)(chunk.biomes[center + center * size] - '0');
                        Require(SettlementGeneration.ShouldPlaceSite(world, centerX, centerY) &&
                            SettlementGeneration.SelectBlueprint(world, centerX, centerY, expected, biome)?.kind == site.kind, "chunk bypassed settlement probability or weighted draw");
                        Require(SettlementGeneration.BlueprintWeight(site.settlement, site.kind, biome) > 0 &&
                            (plan.biomeMask & (1 << (WorldTiles.Biome(world, site.x, site.y) - '0'))) != 0, "generated site violates zone/biome pool");
                        Require(world.buildings.Any(b => b.kind == BuildKind.Door && b.x == site.doorX && b.y == site.doorY) &&
                            !world.nodes.Any(n => n.x == site.doorX && n.y == site.doorY), "site entrance blocked/missing");
                        counts[(int)expected]++;
                    }
                }
            Require(counts.Skip(1).All(n => n >= 6), "gen5 did not place sites in every settlement: " + string.Join(",", counts));
            Require(biomesSeen == 63, "natural biome coverage no longer includes all six biomes");
            Require(streetCells > 0, "fixture did not exercise settlement streets");
        });
        Run("settlement overlay leaves natural biome bytes identical across presets densities and generator versions", () =>
        {
            foreach (int seed in new[] { 11, -73149 })
                foreach (var c in new[] { (-99, -100), (100, 99), (-1, -1), (1023, -1024) })
                {
                    string expected = null;
                    foreach (int version in new[] { 3, 4, 5 })
                        foreach (WorldPreset preset in Enum.GetValues(typeof(WorldPreset))) for (int density = 0; density <= 2; density++)
                        {
                            var world = SettlementWorld(seed, preset, density, version);
                            Require(ChunkTerrain.Generate(world, c.Item1, c.Item2), "natural overlay fixture refused");
                            string actual = world.chunks.records[0].biomes;
                            if (expected == null) expected = actual;
                            Require(actual == expected, "preset/density/generator changed natural biome bytes");
                        }
                }
        });
        Run("settlement gen5 signed chunks have order independent geometry sites and resources", () =>
        {
            var chunks = new List<(int x, int y)>();
            for (int cy = -4; cy < 4; cy++) for (int cx = -4; cx < 4; cx++) chunks.Add((cx * 17, cy * 19));
            // Remain in the ground-world domain; virtual floor chunks are
            // independently reserved/validated by the floor suite.
            chunks.Add((-1024, -1024)); chunks.Add((1023, 1023));
            chunks.Add((-1024, 1023)); chunks.Add((1023, -1024));
            foreach (int seed in new[] { 73149, -73149 })
            {
                var a = SettlementWorld(seed); var b = SettlementWorld(seed);
                foreach (var c in chunks) Require(ChunkTerrain.Generate(a, c.x, c.y), "forward generation refused");
                for (int i = chunks.Count - 1; i >= 0; i--) Require(ChunkTerrain.Generate(b, chunks[i].x, chunks[i].y), "reverse generation refused");
                Require(a.sites.Count > 0 && SettlementGeometry(a) == SettlementGeometry(b), "chunk visitation changes gen5 geometry/resources");
            }
        });
    }

    static WorldState SettlementWorld(int seed = 73149, WorldPreset preset = WorldPreset.Balanced, int density = 1, int version = 5) => new WorldState
    {
        seed = seed, generatorVersion = version,
        generation = new WorldGenerationSettings { seed = seed, preset = preset, cityDensity = density, chunked = true },
        chunks = new ChunkWorldState { enabled = true }, nextId = ChunkStreaming.DynamicIdStart
    };

    // Golden data is captured from the 32-blueprint baseline before gen5 wiring.
    // Hash only fields present in gen3/gen4, so new save metadata is not mistaken
    // for a terrain regression. Includes entity IDs, placement, loot and health.
    static string SettlementLegacyFingerprint(int version)
    {
        var data = new StringBuilder();
        foreach (int seed in new[] { 73149, -73149 })
            foreach (WorldPreset preset in Enum.GetValues(typeof(WorldPreset))) for (int density = 0; density <= 2; density++)
            {
                var world = SettlementWorld(seed, preset, density, version);
                for (int cy = -7; cy <= 8; cy += 5) for (int cx = -6; cx <= 9; cx += 5)
                    Require(ChunkTerrain.Generate(world, cx, cy), "legacy fixture generation refused");
                data.Append(CultureInfo.InvariantCulture, $"{seed}/{(int)preset}/{density}/{world.nextId};");
                foreach (var chunk in world.chunks.records)
                    data.Append(CultureInfo.InvariantCulture, $"{chunk.x},{chunk.y}:{chunk.ground}:{chunk.biomes};");
                foreach (var site in world.sites)
                {
                    Require(site.settlement == SettlementKind.Unspecified, "legacy site gained settlement metadata");
                    data.Append(CultureInfo.InvariantCulture,
                        $"{site.id},{(int)site.kind},{site.x},{site.y},{site.width},{site.height},{site.doorX},{site.doorY},{site.layoutVersion},{site.rotation},{(int)site.district};");
                }
                foreach (var node in world.nodes)
                    data.Append(CultureInfo.InvariantCulture, $"{node.id},{(int)node.kind},{node.x},{node.y},{node.stock},{node.hp};");
                foreach (var building in world.buildings)
                    data.Append(CultureInfo.InvariantCulture,
                        $"{building.id},{(int)building.kind},{building.x},{building.y},{building.rotation},{building.footprintVersion},{building.hp};");
            }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data.ToString())));
    }

    static string SettlementGeometry(WorldState world) => System.Text.Json.JsonSerializer.Serialize(new
    {
        chunks = world.chunks.records.OrderBy(c => c.x).ThenBy(c => c.y).Select(c => new { c.x, c.y, c.ground, c.biomes }),
        sites = world.sites.OrderBy(s => s.x).ThenBy(s => s.y).Select(s => new
        { s.kind, s.x, s.y, s.width, s.height, s.doorX, s.doorY, s.layoutVersion, s.rotation, s.district, s.settlement }),
        nodes = world.nodes.OrderBy(n => n.x).ThenBy(n => n.y).Select(n => new { n.kind, n.x, n.y, n.stock, n.hp }),
        buildings = world.buildings.OrderBy(b => b.x).ThenBy(b => b.y).Select(b => new { b.kind, b.x, b.y, b.rotation, b.footprintVersion, b.hp })
    });
}
