using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        static void CopyExploration(WorldState source, WorldState target, HashSet<int> ids, ref int highest)
        {
            if (source.version < 5)
            {
                target.generatorVersion = 0;
                WorldGeneration.LegacyMetadata(target);
                return;
            }
            SaveCheck(source.generatorVersion >= 0 && source.generatorVersion <= WorldGeneration.CurrentGeneratorVersion, "generator version");
            SaveCheck(source.generation != null, "world settings");
            WorldGeneration.Validate(source.generation);
            SaveCheck(source.generation.seed == source.seed, "world settings seed");
            SaveCheck(string.IsNullOrEmpty(source.generation.seedText) || WorldSeed.Resolve(source.generation.seedText) == source.seed, "world seed text");
            SaveCheck(source.biomes != null && source.biomes.Length == source.width*source.height, "biome map length");
            for(int i=0;i<source.biomes.Length;i++) SaveCheck(source.ground[i]=='?' ? source.biomes[i]=='?' : source.biomes[i]>='0' && source.biomes[i]<='5', "biome kind");
            SaveCheck(source.sites != null && source.sites.Count <= (ChunkStreaming.Enabled(source) ? MaxSites : 64), "site list");
            SaveCheck(source.generation.chunked==ChunkStreaming.Enabled(source),"world chunk settings");
            var cells = new HashSet<long>();
            foreach (SiteState site in source.sites)
            {
                SaveCheck(site != null, "null site"); CheckEntityId(site.id, ids, ref highest);
                SaveCheck((int)site.kind >= 0 && (int)site.kind < WorldGenerationCatalog.Sites.Count, "site kind");
                SaveCheck(SiteBlueprintCatalog.ValidMetadata(site) && (site.layoutVersion == 0 || source.version >= 9 && source.generatorVersion >= 4), "site layout");
                SaveCheck((int)site.district >= 0 && (int)site.district <= 3, "site district");
                SaveCheck((int)site.settlement >= 0 && (int)site.settlement <= 5 &&
                    (site.settlement == SettlementKind.Unspecified || source.version >= 10 && source.generatorVersion >= 5), "site settlement");
                SaveCheck(site.parentSiteId == 0 && site.level == 0 || source.version >= 10 && source.generatorVersion >= 5 && ChunkStreaming.Enabled(source), "site floor version");
                SaveCheck(site.width >= 3 && site.width <= 12 && site.height >= 3 && site.height <= 12, "site size");
                SaveCheck(WorldTiles.Inside(source,site.x,site.y) && WorldTiles.Inside(source,site.x+site.width-1,site.y+site.height-1), "site bounds");
                if(!ChunkStreaming.Enabled(source)) SaveCheck(site.x>=1 && site.y>=1 && site.x+site.width<=63 && site.y+site.height<=63,"legacy site bounds");
                SaveCheck(site.doorX >= site.x && site.doorX < site.x + site.width && site.doorY >= site.y && site.doorY < site.y + site.height &&
                    (site.parentSiteId != 0 || site.doorX == site.x || site.doorX == site.x + site.width - 1 || site.doorY == site.y || site.doorY == site.y + site.height - 1), "site doorway");
                for (int y = site.y; y < site.y + site.height; y++) for (int x = site.x; x < site.x + site.width; x++)
                    SaveCheck(cells.Add(WorldTiles.Key(x,y)) && WorldTiles.Ground(source,x,y)!='?', "overlapping/unknown sites");
                target.sites.Add(new SiteState { id=site.id,kind=site.kind,district=site.district,settlement=site.settlement,parentSiteId=site.parentSiteId,level=site.level,x=site.x,y=site.y,width=site.width,height=site.height,doorX=site.doorX,doorY=site.doorY,layoutVersion=site.layoutVersion,rotation=site.rotation });
            }
            target.generatorVersion = source.generatorVersion;
            target.generation = WorldGeneration.CloneSettings(source.generation);
            target.biomes = source.biomes;
        }

        static void ValidateFloors(WorldState world, HashSet<long> occupied)
        {
            if (!FloorWorld.Enabled(world)) return;

            var byId = new Dictionary<int, SiteState>();
            foreach (var site in world.sites) byId.Add(site.id, site);
            var floorKeys = new HashSet<long>();
            var protectedCells = new HashSet<long>();
            foreach (var site in world.sites)
            {
                bool interior = site.parentSiteId != 0;
                if (!interior && site.layoutVersion == 0)
                {
                    SaveCheck(site.settlement == SettlementKind.Unspecified, "tutorial settlement metadata");
                    continue;
                }

                if (interior)
                {
                    SaveCheck(byId.TryGetValue(site.parentSiteId, out var root), "floor parent missing");
                    SaveCheck(root.parentSiteId == 0 && root.layoutVersion == SiteBlueprintCatalog.LayoutVersion &&
                        site.kind == root.kind && site.rotation == root.rotation && site.district == root.district &&
                        site.settlement == root.settlement && floorKeys.Add(WorldTiles.Key(root.id, site.level)), "floor identity");
                    SaveCheck(FloorWorld.StairGlyph(site, site.doorX, site.doorY) == (site.level > 0 ? '>' : '<'), "floor return stair");
                }
                else
                {
                    SaveCheck(site.settlement == SettlementGeneration.Sample(world, site.x, site.y), "site settlement does not match seed");
                    SaveCheck(!FloorWorld.IsInterior(world, site.x, site.y), "ground site inside floor chunk");
                }

                int stairs = 0;
                for (int y = site.y; y < site.y + site.height; y++)
                for (int x = site.x; x < site.x + site.width; x++)
                {
                    char stair = FloorWorld.StairGlyph(site, x, y);
                    if (stair != '<' && stair != '>') continue;
                    long key = WorldTiles.Key(x, y);
                    SaveCheck(protectedCells.Add(key) && !occupied.Contains(key) && WorldTiles.Ground(world, x, y) == '4', "obstructed floor stair");
                    stairs++;
                }
                int expectedStairs = (site.level < FloorWorld.MaxLevel(site.kind) ? 1 : 0) +
                    (site.level > FloorWorld.MinLevel(site.kind) ? 1 : 0);
                SaveCheck(stairs == expectedStairs, "missing floor stair");
            }

            // Layers are created one at a time from the adjacent floor. Their
            // signed chain must stay connected all the way back to the surface.
            foreach (var site in world.sites)
            {
                if (site.level > 1 || site.level < -1)
                    SaveCheck(floorKeys.Contains(WorldTiles.Key(site.parentSiteId, site.level - Math.Sign(site.level))), "disconnected floor chain");
            }

            var floorIds = new HashSet<int>();
            foreach (var chunk in world.chunks.records)
            {
                if (chunk.interiorSiteId == 0) continue;
                SaveCheck(FloorWorld.SlotOrdinal(chunk.x, chunk.y, out _) && byId.TryGetValue(chunk.interiorSiteId, out _) &&
                    floorIds.Add(chunk.interiorSiteId), "invalid interior chunk identity");
                var site = byId[chunk.interiorSiteId];
                int startX = chunk.x * ChunkStreaming.ChunkSize, startY = chunk.y * ChunkStreaming.ChunkSize;
                SaveCheck(site.parentSiteId > 0 && site.x == startX + 2 && site.y == startY + 2, "floor chunk footprint");
                var root = byId[site.parentSiteId];
                char biome = WorldTiles.Biome(world, root.doorX, root.doorY);
                for (int cell = 0; cell < 256; cell++)
                {
                    int x = cell % 16 - 2, y = cell / 16 - 2;
                    bool floor = x >= 0 && x < site.width && y >= 0 && y < site.height;
                    SaveCheck(chunk.ground[cell] == (floor ? '4' : '?') && chunk.biomes[cell] == (floor ? biome : '?'), "interior terrain footprint");
                }
            }
            SaveCheck(floorIds.Count == floorKeys.Count, "floor chunk missing");
        }
    }
}
