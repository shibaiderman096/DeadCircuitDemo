using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DeadCircuit
{
    [Serializable] public sealed class ExploredChunkState
    {
        public int x,y;
        public string bits="";
    }
    public static class MapExploration
    {
        const string Hex="0123456789abcdef";
        sealed class FogIndex
        {
            public List<ExploredChunkState> source;
            public int count;
            public readonly Dictionary<long,ExploredChunkState> entries=new Dictionary<long,ExploredChunkState>();
        }
        static readonly ConditionalWeakTable<PlayerState,FogIndex> indices=new ConditionalWeakTable<PlayerState,FogIndex>();
        public static int Radius(WorldState world)
        {
            if(world==null) return 0;
            int radius=EnemyCatalog.IsNight(world.elapsed,world.dayLength) ? 8 : 12;
            if(world.survival!=null && world.survival.enabled && world.survival.weather!=null)
            {
                WeatherKind kind=world.survival.weather.kind;
                if(kind==WeatherKind.Fog || kind==WeatherKind.DustStorm) radius=6;
                else if(kind==WeatherKind.Thunderstorm || kind==WeatherKind.Snow) radius=Math.Min(radius,9);
            }
            return radius;
        }
        public static bool IsVisible(WorldState world,PlayerState player,int x,int y)
        {
            if(player==null || !player.connected || player.hp<=0 || !ChunkStreaming.IsGenerated(world,x,y) || WorldTiles.Ground(world,x,y)=='?') return false;
            float dx=x-player.x,dy=y-player.y; int r=Radius(world);
            return dx*dx+dy*dy<=r*r;
        }
        static ExploredChunkState Find(PlayerState p,int cx,int cy)
        {
            if(p.exploredChunks==null) return null;
            var index=indices.GetValue(p,_=>new FogIndex());
            if(!ReferenceEquals(index.source,p.exploredChunks) || index.count>p.exploredChunks.Count)
            {
                index.entries.Clear();index.source=p.exploredChunks;index.count=0;
            }
            while(index.count<p.exploredChunks.Count)
            {
                var entry=p.exploredChunks[index.count++];
                index.entries[WorldTiles.Key(entry.x,entry.y)]=entry;
            }
            ExploredChunkState found;return index.entries.TryGetValue(WorldTiles.Key(cx,cy),out found) ? found : null;
        }
        public static bool IsExplored(WorldState world,PlayerState player,int x,int y)
        {
            if(player==null || !ChunkStreaming.IsGenerated(world,x,y) || WorldTiles.Ground(world,x,y)=='?') return false;
            string bits;int cell;
            if(ChunkStreaming.Enabled(world))
            {
                int cx=WorldTiles.FloorDiv(x,16),cy=WorldTiles.FloorDiv(y,16);
                var entry=Find(player,cx,cy);if(entry==null) return false;
                bits=entry.bits;cell=x-cx*16+(y-cy*16)*16;
            }
            else { bits=player.explored;cell=x+y*world.width; }
            int index=cell/4;
            return bits!=null && index<bits.Length && Hex.IndexOf(bits[index])>=0 && (Hex.IndexOf(bits[index])&(1<<(cell%4)))!=0;
        }
        public static void Reveal(WorldState world,PlayerState player)
        {
            if(player==null || !player.connected || player.hp<=0) return;
            int radius=Radius(world),px=(int)Math.Floor(player.x+.5f),py=(int)Math.Floor(player.y+.5f);
            // Each changed chunk is cloned once per reveal, not once per tile.
            var changed=new Dictionary<long,char[]>();
            char[] legacy=null;
            if(!ChunkStreaming.Enabled(world) && string.IsNullOrEmpty(player.explored)) legacy=new string('0',(world.width*world.height+3)/4).ToCharArray();
            for(int y=py-radius;y<=py+radius;y++) for(int x=px-radius;x<=px+radius;x++)
            {
                if(!IsVisible(world,player,x,y)) continue;
                if(ChunkStreaming.Enabled(world))
                {
                    int cx=WorldTiles.FloorDiv(x,16),cy=WorldTiles.FloorDiv(y,16),cell=x-cx*16+(y-cy*16)*16;
                    long key=WorldTiles.Key(cx,cy);char[] bits;
                    if(!changed.TryGetValue(key,out bits))
                    {
                        var entry=Find(player,cx,cy);string existing=entry==null ? new string('0',64) : entry.bits;
                        int current=Hex.IndexOf(existing[cell/4]);if((current&(1<<(cell%4)))!=0) continue;
                        bits=existing.ToCharArray();changed.Add(key,bits);
                    }
                    bits[cell/4]=Hex[Hex.IndexOf(bits[cell/4])|(1<<(cell%4))];
                }
                else
                {
                    int cell=x+y*world.width,index=cell/4,mask=1<<(cell%4);
                    int value=Hex.IndexOf(legacy==null ? player.explored[index] : legacy[index]);
                    if((value&mask)!=0) continue;
                    if(legacy==null) legacy=player.explored.ToCharArray();
                    legacy[index]=Hex[value|mask];
                }
            }
            if(legacy!=null) player.explored=new string(legacy);
            if(player.exploredChunks==null) player.exploredChunks=new List<ExploredChunkState>();
            foreach(var pair in changed)
            {
                int cx=WorldTiles.X(pair.Key),cy=WorldTiles.Y(pair.Key);var entry=Find(player,cx,cy);
                if(entry==null) { entry=new ExploredChunkState {x=cx,y=cy};player.exploredChunks.Add(entry); }
                entry.bits=new string(pair.Value);
            }
        }
    }
}
