using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class DemoHud
    {
        TextMeshProUGUI machineCluster;
        DemoWorldView roomWorldView;

        static string CellLocationText(WorldState world, int x, int y)
        {
            string floor = FloorWorld.Location(world, x, y);
            if (!string.IsNullOrEmpty(floor)) return floor;
            if (FloorWorld.IsInterior(world, x, y)) return "未知室内位置";
            return "(" + x + "," + y + ")";
        }

        static string OutdoorRegionText(WorldState world, int x, int y)
        {
            int biome = WorldTiles.Biome(world, x, y) - '0';
            var biomes = WorldGenerationCatalog.Biomes;
            string ecology = biome >= 0 && biome < biomes.Count ? biomes[biome].name : "";
            if (world.generatorVersion < 5) return ecology;
            string settlement = SettlementCatalog.Get(SettlementGeneration.Sample(world, x, y)).name;
            return ecology.Length == 0 ? settlement : settlement + " · " + ecology;
        }

        static string BiomeResourceHint(WorldState world, int x, int y)
        {
            if (world == null || world.generatorVersion < 7 || FloorWorld.IsInterior(world, x, y)) return "";
            char ground = WorldTiles.Ground(world, x, y);
            if (ground == '?' || ground == '4') return "";
            var resources = BiomeResourceCatalog.Get((BiomeKind)(WorldTiles.Biome(world, x, y) - '0'));
            return resources?.hint ?? "";
        }

        static string ResourceToolText(NodeKind kind)
        {
            if (EquipmentCatalog.UsesPickaxe(kind)) return "镐（提高采集效率）";
            switch (kind)
            {
                case NodeKind.Tree: return "斧（提高采集效率）";
                case NodeKind.Scrap: case NodeKind.Wall: return "扳手（提高采集效率）";
                default: return "手工即可";
            }
        }

        static string ResourceCollectionHint(WorldState world, PlayerState player, NodeState node)
        {
            string hint = "靠近并瞄准后按 E；空手也可采集。";
            switch (node.kind)
            {
                case NodeKind.Coal: hint += "\n煤层多见于针叶林与冻原，需在自然地貌中勘探。"; break;
                case NodeKind.Limestone: hint += "\n石灰石多见于河谷、荒漠与雪山，需在自然地貌中勘探。"; break;
                case NodeKind.FireClay: hint += "\n耐火黏土多见于湿地、沼泽与河谷沉积层，需在自然地貌中勘探。"; break;
            }
            if (EquipmentCatalog.UsesPickaxe(node.kind))
                hint += "\n蒸汽矿机可采集这类矿床，需紧邻矿床并接通蒸汽；LV采矿机目前采铁、铜、锡、铀。";
            if (node.kind == NodeKind.Tree && world.generatorVersion >= 1 &&
                world.generatorVersion <= WorldGeneration.CurrentGeneratorVersion &&
                world.survival != null && world.survival.enabled && player?.needs != null && player.needs.initialized)
            {
                string forage = "";
                switch (WorldTiles.Biome(world, node.x, node.y) - '0')
                {
                    case 0: case 1: forage = "苹果、浆果"; break;
                    case 2: case 4: case 5: forage = "蘑菇、浆果"; break;
                    case 3: forage = "少量浆果"; break;
                }
                if (forage.Length > 0) hint += "\n按 E 采尽树木可附带：" + forage + "。";
            }
            string resources = BiomeResourceHint(world, node.x, node.y);
            if (resources.Length > 0) hint += "\n本群系资源方向：" + resources;
            return hint + "\n资源有限，采尽后需继续探索。";
        }

        string RoomLocation(WorldState world, PlayerState player)
        {
            if (world == null || player == null) return "";
            int x = Mathf.FloorToInt(player.x + .5f), y = Mathf.FloorToInt(player.y + .5f);
            string floor = FloorWorld.Location(world, x, y);
            if (!string.IsNullOrEmpty(floor)) return "\n位置：" + floor;
            if (FloorWorld.IsInterior(world, x, y)) return "\n位置：未知室内位置";
            if (roomWorldView == null) roomWorldView = bridge.GetComponent<DemoWorldView>();
            var site = DemoWorldView.SiteAt(world, x, y);
            string siteLocation = site == null ? "" : WorldGenerationCatalog.Sites[(int)site.kind].name + " · ";
            var room = roomWorldView?.RoomAt(world, x, y);
            if (roomWorldView != null && room == null && world.buildings != null) foreach (var door in world.buildings)
            {
                if (door == null || door.hp <= 0 || door.kind != BuildKind.Door || !DemoCatalog.Contains(door, x, y)) continue;
                room = roomWorldView.RoomAt(world, x + 1, y) ?? roomWorldView.RoomAt(world, x - 1, y) ??
                    roomWorldView.RoomAt(world, x, y + 1) ?? roomWorldView.RoomAt(world, x, y - 1);
                break;
            }
            if (room == null)
            {
                string region = OutdoorRegionText(world, x, y);
                string resources = BiomeResourceHint(world, x, y).Replace('\n', ' ').Replace('\r', ' ');
                if (resources.Length > 30) resources = resources.Substring(0, 29) + "…";
                return "\n位置：" + siteLocation + "户外" + (region.Length == 0 ? "" : " · " + region) +
                    (resources.Length == 0 ? "" : "\n资源方向：" + resources);
            }
            string ground = FloorWorld.LevelName(0) + " · ";
            if (site != null) ground += "(" + (x - site.x) + "," + (y - site.y) + ") · ";
            return "\n位置：" + siteLocation + ground + "室内 " +
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
            if (building.kind == BuildKind.FilterSplitter)
                return "LV供电；在检查面板设置过滤物品：匹配物品送前方，余料送左侧。堵塞不混线，在途物品到货后才能改过滤。";
            if (building.kind == BuildKind.CircuitBreaker)
                return "保护所在LV连通电网；过载跳闸后同网停电，独立电网不受影响。先降载或增加发电，再在检查面板复位接通；也可手动断开。";
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
