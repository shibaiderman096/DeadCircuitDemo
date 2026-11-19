using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private static void CopyChunks(WorldState source,WorldState target)
        {
            if(source.version<8 || !ChunkStreaming.Enabled(source))
            {
                SaveCheck(source.chunks==null || (!source.chunks.enabled && string.IsNullOrEmpty(source.chunks.generated) && (source.chunks.records==null || source.chunks.records.Count==0)),"legacy chunk metadata");
                return;
            }
            var chunks=source.chunks;
            SaveCheck(source.generatorVersion==3 || source.generatorVersion==4 && source.version>=9 || source.generatorVersion==5 && source.version>=10 || (source.generatorVersion==6 || source.generatorVersion==7) && source.version>=11,"chunk generator");
            SaveCheck(chunks.records!=null && chunks.records.Count>=16 && chunks.records.Count<=65536 && chunks.revision>=0 && string.IsNullOrEmpty(chunks.generated),"chunk metadata");
            var keys=new HashSet<long>();var result=new ChunkWorldState {enabled=true,revision=chunks.revision};
            foreach(var chunk in chunks.records)
            {
                SaveCheck(chunk!=null && chunk.x>=-65536 && chunk.x<65536 && chunk.y>=-65536 && chunk.y<65536 && WorldTiles.Inside(source,chunk.x*16,chunk.y*16) && WorldTiles.Inside(source,chunk.x*16+15,chunk.y*16+15),"chunk coordinates");
                SaveCheck(keys.Add(WorldTiles.Key(chunk.x,chunk.y)),"duplicate chunk");
                SaveCheck(chunk.ground!=null && chunk.ground.Length==256 && chunk.biomes!=null && chunk.biomes.Length==256,"chunk tile length");
                bool interior = chunk.interiorSiteId > 0;
                bool reserved = source.generatorVersion >= 5 && FloorWorld.IsReservedChunk(chunk.x, chunk.y);
                SaveCheck(chunk.interiorSiteId >= 0 && (!interior || source.version >= 10 && reserved), "interior chunk marker");
                SaveCheck(!reserved || interior, "natural chunk in interior reserve");
                for(int i=0;i<256;i++) SaveCheck(interior && chunk.ground[i]=='?' && chunk.biomes[i]=='?' ||
                    chunk.ground[i]>='0' && chunk.ground[i]<='4' && chunk.biomes[i]>='0' && chunk.biomes[i]<='5',"chunk tile kind");
                result.records.Add(new WorldChunkState {x=chunk.x,y=chunk.y,ground=chunk.ground,biomes=chunk.biomes,interiorSiteId=chunk.interiorSiteId});
            }
            // A normal save always contains its intact original tutorial footprint.
            for(int cy=0;cy<4;cy++) for(int cx=0;cx<4;cx++) SaveCheck(keys.Contains(WorldTiles.Key(cx,cy)),"missing initial chunk");
            target.chunks=result;
        }
        private static string CopyExplored(WorldState world,PlayerState player)
        {
            if(world.version<8) return "";
            string bits=player.explored;
            SaveCheck(bits!=null && (bits.Length==0 || bits.Length==(world.width*world.height+3)/4),"exploration length");
            foreach(char c in bits) SaveCheck(c>='0' && c<='9' || c>='a' && c<='f',"exploration bits");
            return bits;
        }
        private static List<ExploredChunkState> CopyExploredChunks(WorldState world,PlayerState player)
        {
            var copy=new List<ExploredChunkState>();if(world.version<8) return copy;
            SaveCheck(player.exploredChunks!=null && player.exploredChunks.Count<=65536,"explored chunk list");
            var keys=new HashSet<long>();
            foreach(var entry in player.exploredChunks)
            {
                SaveCheck(entry!=null && entry.x>=-65536 && entry.x<65536 && entry.y>=-65536 && entry.y<65536 && keys.Add(WorldTiles.Key(entry.x,entry.y)),"explored chunk coordinates");
                SaveCheck(ChunkStreaming.Enabled(world) && ChunkStreaming.IsGenerated(world,entry.x*16,entry.y*16),"explored unknown chunk");
                SaveCheck(entry.bits!=null && entry.bits.Length==64,"explored chunk length");
                foreach(char c in entry.bits) SaveCheck(c>='0' && c<='9' || c>='a' && c<='f',"explored chunk bits");
                copy.Add(new ExploredChunkState {x=entry.x,y=entry.y,bits=entry.bits});
            }
            return copy;
        }
    }
}
