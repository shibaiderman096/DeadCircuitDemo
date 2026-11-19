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
                SaveCheck((int)site.district >= 0 && (int)site.district <= 3, "site district");
                SaveCheck(site.width >= 3 && site.width <= 12 && site.height >= 3 && site.height <= 12, "site size");
                SaveCheck(WorldTiles.Inside(source,site.x,site.y) && WorldTiles.Inside(source,site.x+site.width-1,site.y+site.height-1), "site bounds");
                if(!ChunkStreaming.Enabled(source)) SaveCheck(site.x>=1 && site.y>=1 && site.x+site.width<=63 && site.y+site.height<=63,"legacy site bounds");
                SaveCheck(site.doorX >= site.x && site.doorX < site.x + site.width && site.doorY >= site.y && site.doorY < site.y + site.height &&
                    (site.doorX == site.x || site.doorX == site.x + site.width - 1 || site.doorY == site.y || site.doorY == site.y + site.height - 1), "site doorway");
                for (int y = site.y; y < site.y + site.height; y++) for (int x = site.x; x < site.x + site.width; x++)
                    SaveCheck(cells.Add(WorldTiles.Key(x,y)) && WorldTiles.Ground(source,x,y)!='?', "overlapping/unknown sites");
                target.sites.Add(new SiteState { id=site.id,kind=site.kind,district=site.district,x=site.x,y=site.y,width=site.width,height=site.height,doorX=site.doorX,doorY=site.doorY });
            }
            target.generatorVersion = source.generatorVersion;
            target.generation = WorldGeneration.CloneSettings(source.generation);
            target.biomes = source.biomes;
        }
    }
}
