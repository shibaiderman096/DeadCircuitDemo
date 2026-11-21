using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    // Read-only projection for presentation, not a save or a second authority.
    // Every list we filter is new. Referenced entity objects are never mutated.
    public static class WorldSnapshot
    {
        public static WorldState ForPlayer(WorldState source,string playerId)
        {
            if(!ChunkStreaming.Enabled(source))
            {
                if(!source.buildings.Exists(b=>ModularMachineCatalog.IsModular(b.kind))) return source;
                var detached=source.SnapshotCopy(); detached.buildings=source.buildings.ConvertAll(SnapshotBuilding); return detached;
            }
            PlayerState local=source.players.Find(p=>p.id==playerId);
            if(local==null) throw new ArgumentException("Unknown snapshot player.",nameof(playerId));
            int cx=WorldTiles.FloorDiv((int)Math.Floor(local.x+.5f),16),cy=WorldTiles.FloorDiv((int)Math.Floor(local.y+.5f),16);
            const int radius=4;
            Func<int,int,bool> nearby=(x,y)=>Math.Abs(WorldTiles.FloorDiv(x,16)-cx)<=radius && Math.Abs(WorldTiles.FloorDiv(y,16)-cy)<=radius;
            var result=source.SnapshotCopy();
            result.chunks=new ChunkWorldState { enabled=true,revision=source.chunks.revision };
            for(int y=cy-radius;y<=cy+radius;y++) for(int x=cx-radius;x<=cx+radius;x++)
                if(WorldTiles.TryChunk(source,x,y,out var chunk)) result.chunks.records.Add(chunk);
            result.nodes=source.nodes.FindAll(n=>nearby(n.x,n.y));
            result.buildings=source.buildings.FindAll(b=>ModularMachineCatalog.IsModular(b.kind)
                ? b.modularModules.Exists(m=>nearby(m.x,m.y))
                : WorldTiles.FloorDiv(b.x,16)<=cx+radius && WorldTiles.FloorDiv(b.x+DemoCatalog.Width(b)-1,16)>=cx-radius &&
                  WorldTiles.FloorDiv(b.y,16)<=cy+radius && WorldTiles.FloorDiv(b.y+DemoCatalog.Height(b)-1,16)>=cy-radius).ConvertAll(SnapshotBuilding);
            result.sites=source.sites.FindAll(s=>nearby(s.x,s.y));
            // A floor snapshot carries its ground identity for labels and stairs,
            // without sending that remote location's terrain or live entities.
            var rootIds=new HashSet<int>();
            foreach(var site in result.sites) if(site.parentSiteId>0) rootIds.Add(site.parentSiteId);
            foreach(var site in source.sites) if(rootIds.Contains(site.id) && !result.sites.Contains(site)) result.sites.Add(site);
            result.zombies=source.zombies.FindAll(z=>nearby((int)Math.Floor(z.x+.5f),(int)Math.Floor(z.y+.5f)));
            var ids=new HashSet<int>();foreach(var b in result.buildings) ids.Add(b.id);
            result.cargo=source.cargo.FindAll(c=>ids.Contains(c.fromId) && ids.Contains(c.toId));
            result.traces=source.traces.FindAll(t=>nearby((int)Math.Floor(t.x+.5f),(int)Math.Floor(t.y+.5f)));
            result.players=new List<PlayerState>();
            foreach(var player in source.players)
            {
                var copy=player.SnapshotCopy();copy.exploredChunks=new List<ExploredChunkState>();
                if(player.id==playerId) foreach(var fog in player.exploredChunks)
                    if(Math.Abs(fog.x-cx)<=radius && Math.Abs(fog.y-cy)<=radius) copy.exploredChunks.Add(fog);
                result.players.Add(copy);
            }
            if(source.survival!=null) result.survival=new SurvivalWorldState
            {
                enabled=source.survival.enabled,initialized=source.survival.initialized,weather=source.survival.weather,
                creatureRandomState=source.survival.creatureRandomState,
                creatures=source.survival.creatures.FindAll(c=>nearby((int)Math.Floor(c.x+.5f),(int)Math.Floor(c.y+.5f))),
                plots=source.survival.plots.FindAll(p=>nearby(p.x,p.y))
            };
            return result;
        }
        private static BuildingState SnapshotBuilding(BuildingState b)
        {
            if(!ModularMachineCatalog.IsModular(b.kind)) return b;
            var copy=b.SnapshotCopy(); copy.items=(int[])b.items.Clone(); copy.modularModules=b.modularModules.ConvertAll(m=>new MachineModuleState {role=m.role,x=m.x,y=m.y,rotation=m.rotation,paid=(int[])m.paid.Clone()}); return copy;
        }
    }
}
