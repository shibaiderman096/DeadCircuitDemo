using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class DemoHud
    {
        TextMeshProUGUI machineCluster;
        DemoWorldView roomWorldView;

        string RoomLocation(WorldState world, PlayerState player)
        {
            if (player == null) return "";
            if (roomWorldView == null) roomWorldView = bridge.GetComponent<DemoWorldView>();
            if (roomWorldView == null) return "";
            int x = Mathf.FloorToInt(player.x + .5f), y = Mathf.FloorToInt(player.y + .5f);
            string siteName = roomWorldView.SiteNameAt(world, x, y);
            string siteLocation = string.IsNullOrEmpty(siteName) ? "" : siteName + " · ";
            var room = roomWorldView.RoomAt(world, x, y);
            if (room == null && world.buildings != null) foreach (var door in world.buildings)
            {
                if (door == null || door.hp <= 0 || door.kind != BuildKind.Door || !DemoCatalog.Contains(door, x, y)) continue;
                room = roomWorldView.RoomAt(world, x + 1, y) ?? roomWorldView.RoomAt(world, x - 1, y) ??
                    roomWorldView.RoomAt(world, x, y + 1) ?? roomWorldView.RoomAt(world, x, y - 1);
                break;
            }
            return room == null ? "\n位置：" + siteLocation + "户外" : "\n位置：" + siteLocation + "室内 " +
                (room.cellKeys != null && room.cellKeys.Length > 0 ? room.cellKeys.Length : room.cells?.Length ?? 0) +
                " 格 · 出入口 " + (room.doorIds?.Length ?? 0);
        }

        static string InspectedMachineName(BuildingState building) =>
            building.kind == BuildKind.Boiler && !building.modularBoiler ? "旧式一体锅炉" : DemoCatalog.BuildName(building.kind);

        static string InspectedMachineDescription(BuildingState building)
        {
            if (building.kind == BuildKind.Boiler && !building.modularBoiler)
                return "旧式一体锅炉：保留自带燃烧及近水取水；E 补燃料。";
            if (building.kind == BuildKind.Door)
                return "E 开关门；打开后可通行。开门仍保留屋顶覆盖，默认剖面显示内部。";
            return DemoCatalog.BuildDescriptions[(int)building.kind];
        }

        void RefreshSteamCluster(WorldState world, BuildingState building)
        {
            bool legacy = building.kind == BuildKind.Boiler && !building.modularBoiler;
            bool module = building.kind == BuildKind.Boiler || building.kind == BuildKind.CombustionChamber ||
                building.kind == BuildKind.Chimney || building.kind == BuildKind.FeedwaterPump;
            bool show = legacy || module || building.clusterId > 0;
            machineCluster.gameObject.SetActive(show);
            if (!show) return;
            if (legacy)
            {
                SetFlowText(machineCluster, "旧式一体锅炉\n自带燃烧及取水，不要求新增部件。", 28);
                machineCluster.color = Muted;
                return;
            }
            SteamClusterState cluster = world.steamClusters?.Find(candidate => candidate != null && candidate.id == building.clusterId);
            if (cluster == null)
            {
                SetFlowText(machineCluster, "蒸汽集群：等待主机刷新\n将锅炉、燃烧室、给水泵和烟囱接触连接。", 28);
                machineCluster.color = Amber;
                return;
            }
            string missing = MissingClusterParts(cluster);
            SetFlowText(machineCluster, "蒸汽集群 #" + cluster.id + " · 部件 " + (cluster.buildingIds?.Length ?? 0) +
                "\n锅炉 " + cluster.boilers + " · 燃烧室 " + cluster.chambers +
                "\n给水泵 " + cluster.pumps + " · 烟囱 " + cluster.chimneys +
                "\n输出 " + cluster.output.ToString("0.#") + " 蒸汽功率" +
                "\n供热 / 给水 / 排汽：" + cluster.heatCapacity.ToString("0.#") + " / " +
                cluster.waterCapacity.ToString("0.#") + " / " + cluster.exhaustCapacity.ToString("0.#") +
                "\n缺件：" + missing +
                (string.IsNullOrWhiteSpace(cluster.status) ? "" : "\n状态：" + Clean(cluster.status)), 28);
            machineCluster.color = missing != "无" || cluster.output <= 0 ? Amber : Ink;
        }

        static string MissingClusterParts(SteamClusterState cluster)
        {
            var missing = new List<string>(4);
            if (cluster.boilers == 0) missing.Add("锅炉");
            if (cluster.chambers == 0) missing.Add("燃烧室");
            if (cluster.pumps == 0) missing.Add("给水泵");
            if (cluster.chimneys == 0) missing.Add("烟囱");
            return missing.Count == 0 ? "无" : string.Join("、", missing);
        }
    }
}
