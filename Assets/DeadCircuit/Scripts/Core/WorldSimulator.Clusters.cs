using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private struct SteamClusterFootprint
        {
            public BuildingState building;
            public int id, x, y, width, height;
            public BuildKind kind;
            public bool modularBoiler;
        }

        private readonly List<SteamClusterFootprint> steamClusterLayout = new List<SteamClusterFootprint>();
        private readonly List<List<BuildingState>> steamClusterGroups = new List<List<BuildingState>>();

        private void RefreshSteamClusters(bool consume)
        {
            if (State.steamClusters == null) State.steamClusters = new List<SteamClusterState>();
            State.steamClusters.Clear();
            foreach (BuildingState b in State.buildings) b.clusterId = 0;

            // Compare snapshots, not mutable BuildingState values held by the old group.
            // Steady ticks scan the layout; adjacency/union work only follows structural changes.
            if (SteamClusterTopologyChanged()) RebuildSteamClusterTopology();
            foreach (List<BuildingState> members in steamClusterGroups) RefreshSteamCluster(members, consume);
        }

        private bool SteamClusterTopologyChanged()
        {
            int count = 0;
            foreach (PowerLayout layout in powerLayout)
            {
                BuildingState b = layout.building;
                if (b.hp <= 0f || !IsSteamClusterModule(b)) continue;
                if (count >= steamClusterLayout.Count) return true;
                SteamClusterFootprint old = steamClusterLayout[count++];
                if (old.building != b || old.id != b.id || old.kind != b.kind ||
                    old.x != b.x || old.y != b.y || old.width != layout.width || old.height != layout.height ||
                    old.modularBoiler != b.modularBoiler) return true;
            }
            return count != steamClusterLayout.Count;
        }

        private void RebuildSteamClusterTopology()
        {
            steamClusterLayout.Clear();
            steamClusterGroups.Clear();
            // RefreshPower has already rebuilt these rotated, live footprints. Only the
            // physical modules participate: transmission pipes and buildings cannot bridge.
            foreach (PowerLayout layout in powerLayout)
            {
                BuildingState b = layout.building;
                if (b.hp <= 0f || !IsSteamClusterModule(b)) continue;
                steamClusterLayout.Add(new SteamClusterFootprint
                {
                    building = b, id = b.id, kind = b.kind, x = b.x, y = b.y,
                    width = layout.width, height = layout.height, modularBoiler = b.modularBoiler
                });
            }
            steamClusterLayout.Sort((a, b) => a.id.CompareTo(b.id));
            var parents = new int[steamClusterLayout.Count];
            for (int i = 0; i < parents.Length; i++) parents[i] = i;
            for (int i = 0; i < steamClusterLayout.Count; i++) for (int j = 0; j < i; j++)
            {
                if (!SteamClusterTouching(steamClusterLayout[i], steamClusterLayout[j])) continue;
                int a = PowerRoot(parents, i), b = PowerRoot(parents, j);
                if (a != b) parents[Math.Max(a, b)] = Math.Min(a, b);
            }

            var byRoot = new Dictionary<int, List<BuildingState>>();
            for (int i = 0; i < steamClusterLayout.Count; i++)
            {
                int root = PowerRoot(parents, i);
                List<BuildingState> members;
                if (!byRoot.TryGetValue(root, out members))
                {
                    members = new List<BuildingState>();
                    byRoot.Add(root, members);
                    steamClusterGroups.Add(members);
                }
                members.Add(steamClusterLayout[i].building);
            }
            // Both groups and members retain ascending IDs independently of State order.
        }

        private static bool SteamClusterTouching(SteamClusterFootprint a, SteamClusterFootprint b) =>
            ((a.x + a.width == b.x || b.x + b.width == a.x) && a.y < b.y + b.height && b.y < a.y + a.height) ||
            ((a.y + a.height == b.y || b.y + b.height == a.y) && a.x < b.x + b.width && b.x < a.x + a.width);

        private static bool IsSteamClusterModule(BuildingState b) =>
            (b.kind == BuildKind.Boiler && b.modularBoiler) ||
            b.kind == BuildKind.CombustionChamber || b.kind == BuildKind.FeedwaterPump || b.kind == BuildKind.Chimney;

        private void RefreshSteamCluster(List<BuildingState> members, bool consume)
        {
            var cluster = new SteamClusterState { id = members[0].id, buildingIds = new int[members.Count] };
            int wetPumps = 0;
            for (int i = 0; i < members.Count; i++)
            {
                BuildingState b = members[i];
                cluster.buildingIds[i] = b.id;
                b.clusterId = cluster.id;
                switch (b.kind)
                {
                    case BuildKind.Boiler: cluster.boilers++; break;
                    case BuildKind.CombustionChamber: cluster.chambers++; break;
                    case BuildKind.FeedwaterPump:
                        cluster.pumps++;
                        if (NearWater(b)) wetPumps++;
                        break;
                    case BuildKind.Chimney: cluster.chimneys++; break;
                }
            }
            cluster.heatCapacity = cluster.chambers * 120f;
            cluster.waterCapacity = wetPumps * 120f;
            cluster.exhaustCapacity = cluster.chimneys * 120f;
            State.steamClusters.Add(cluster);

            var missing = new List<string>();
            if (cluster.boilers == 0) missing.Add("缺锅炉");
            if (cluster.chambers == 0) missing.Add("缺燃烧室");
            if (cluster.pumps == 0) missing.Add("缺给水泵");
            else if (wetPumps == 0) missing.Add("给水泵离水");
            if (cluster.chimneys == 0) missing.Add("缺烟囱");
            if (missing.Count > 0)
            {
                cluster.status = string.Join("；", missing);
                return;
            }

            int slots = Math.Min(cluster.boilers, Math.Min(wetPumps, cluster.chimneys));
            foreach (BuildingState b in members)
            {
                if (slots == 0) break;
                if (b.kind != BuildKind.CombustionChamber || BurnFuel(b, false, 120f, 20f) <= 0f) continue;
                cluster.output += BurnFuel(b, consume, 120f, 20f);
                // A final partial tick occupies one 120-unit slot. Starting another full
                // chamber to top it up would discard heat while charging a whole tick.
                slots--;
            }
            cluster.status = cluster.output > 0f ? "运行" : "无燃料";
            foreach (BuildingState b in members)
                if (b.kind == BuildKind.Boiler) b.powerGeneration = cluster.output / cluster.boilers;
        }
    }
}
