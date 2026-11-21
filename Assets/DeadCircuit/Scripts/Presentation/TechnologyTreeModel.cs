using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public sealed class TechnologyTreeNode
    {
        public int key, era, researchId;
        public string name, description, command;
        public int[] cost;
        public int[] parents;
        public bool IsEra => researchId < 0;
    }

    // Presentation only: catalog checks remain authoritative and queries never send commands.
    public static class TechnologyTreeModel
    {
        public static readonly TechnologyTreeNode[] All;
        static readonly Dictionary<int, TechnologyTreeNode> NodesByKey;

        static TechnologyTreeModel()
        {
            All = CreateNodes();
            NodesByKey = new Dictionary<int, TechnologyTreeNode>(All.Length);
            foreach (var node in All) NodesByKey.Add(node.key, node);
        }

        public static TechnologyTreeNode Find(int key) => NodesByKey.TryGetValue(key, out var node) ? node : null;

        public static bool Known(WorldState world, TechnologyTreeNode node)
        {
            if (world == null || !Valid(node) || (int)world.era < node.era) return false;
            if (node.IsEra) return true;
            switch (node.era)
            {
                case 0: return PrimitiveCatalog.HasResearch(world, node.researchId);
                case 1: return WaterCatalog.HasResearch(world, node.researchId);
                case 2: return SteamCatalog.HasResearch(world, node.researchId);
                case 3: return LowVoltageCatalog.HasResearch(world, node.researchId);
                case 4: return PetrochemicalCatalog.HasResearch(world, node.researchId);
                case 5: return PrecisionCatalog.HasResearch(world, node.researchId);
                default: return false;
            }
        }

        public static string Requirement(WorldState world, PlayerState player, TechnologyTreeNode node)
        {
            if (world == null) return "世界尚未就绪。";
            if (!Valid(node)) return "无法识别这项研究。";
            if (Known(world, node)) return "";
            int requiredEra = node.IsEra ? node.era - 1 : node.era;
            if ((int)world.era < requiredEra)
                return "需要先进入：" + TechCatalog.EraName((TechEra)requiredEra) + "。";
            if (player == null || world.players == null || !world.players.Contains(player) ||
                !player.connected || !(player.hp > 0f) || player.items == null || player.items.Length != DemoCatalog.ItemCount)
                return "当前角色无法研究，请确认角色已连接且仍存活。";
            if (!(player.interactCooldown <= 0f)) return "操作尚未结束，请稍候。";
            if (!node.IsEra) return CatalogRequirement(world, player, node);

            string requirement = TechCatalog.ResearchRequirement(world, (TechEra)node.era);
            if (requirement.Length != 0) return requirement;
            int[] cost = TechCatalog.ResearchCost((TechEra)node.era);
            for (int i = 0; i < cost.Length; i++)
                if (player.items[i] < cost[i]) return "研究材料不足：" + DemoCatalog.ItemNames[i];
            return "";
        }

        public static bool CanResearch(WorldState world, PlayerState player, TechnologyTreeNode node) =>
            !Known(world, node) && Requirement(world, player, node).Length == 0 &&
            (!node.IsEra || TechCatalog.CanResearch(world, player, (TechEra)node.era));

        // Connections describe research knowledge, not inventory, proximity or production evidence.
        // Those live conditions are reported by Requirement and enforced by CanResearch.
        public static bool PrerequisitesMet(WorldState world, TechnologyTreeNode node)
        {
            if (world == null || !Valid(node) || (int)world.era < (node.IsEra ? node.era - 1 : node.era)) return false;
            foreach (int parent in Find(node.key).parents)
                if (!Known(world, Find(parent))) return false;
            return true;
        }

        static bool Valid(TechnologyTreeNode node)
        {
            if (node == null) return false;
            var definition = Find(node.key);
            return definition != null && node.era == definition.era && node.researchId == definition.researchId &&
                node.era >= 0 && node.era <= 6 && (node.researchId == -1 ||
                node.researchId >= 0 && node.researchId < ResearchCount(node.era));
        }

        static int ResearchCount(int era)
        {
            switch (era)
            {
                case 0: return PrimitiveCatalog.All.Length;
                case 1: return WaterCatalog.All.Length;
                case 2: return SteamCatalog.All.Length;
                case 3: return LowVoltageCatalog.All.Length;
                case 4: return PetrochemicalCatalog.All.Length;
                case 5: return PrecisionCatalog.All.Length;
                default: return 0;
            }
        }

        static string CatalogRequirement(WorldState world, PlayerState player, TechnologyTreeNode node)
        {
            switch (node.era)
            {
                case 0: return PrimitiveCatalog.ResearchRequirement(world, player, node.researchId);
                case 1: return WaterCatalog.ResearchRequirement(world, player, node.researchId);
                case 2: return SteamCatalog.ResearchRequirement(world, player, node.researchId);
                case 3: return LowVoltageCatalog.ResearchRequirement(world, player, node.researchId);
                case 4: return PetrochemicalCatalog.ResearchRequirement(world, player, node.researchId);
                case 5: return PrecisionCatalog.ResearchRequirement(world, player, node.researchId);
                default: return "无法识别这项研究。";
            }
        }

        static int Key(int era, int researchId) => era * 100 + researchId + 1;

        static void AddResearch(List<TechnologyTreeNode> nodes, int era, int id, string name, string description,
            int mask, int[] cost, string command)
        {
            var parents = new List<int>();
            for (int bit = 0; bit < ResearchCount(era); bit++)
                if ((mask & (1 << bit)) != 0) parents.Add(Key(era, bit));
            if (parents.Count == 0) parents.Add(era * 100);
            nodes.Add(new TechnologyTreeNode { key = Key(era, id), era = era, researchId = id, name = name,
                description = description, cost = (int[])cost.Clone(), command = command, parents = parents.ToArray() });
        }

        static void AddParents(List<TechnologyTreeNode> nodes, int key, params int[] keys)
        {
            var node = nodes.Find(value => value.key == key);
            var parents = new List<int>(node.parents);
            foreach (int parent in keys) if (!parents.Contains(parent)) parents.Add(parent);
            node.parents = parents.ToArray();
        }

        // Required crafts already connected through another required craft need no extra line.
        // Era acceptance still validates every required bit in the original catalog.
        static void AddEraParents(List<TechnologyTreeNode> nodes, int era, int requiredMask)
        {
            var required = nodes.FindAll(node => !node.IsEra && node.era == era - 1 &&
                (requiredMask & (1 << node.researchId)) != 0);
            var ancestors = new HashSet<int>();
            foreach (var node in required) CollectCraftAncestors(node);
            foreach (var node in required)
                if (!ancestors.Contains(node.key)) AddParents(nodes, era * 100, node.key);

            void CollectCraftAncestors(TechnologyTreeNode node)
            {
                foreach (int key in node.parents)
                    if (key % 100 != 0 && key / 100 == era - 1 && ancestors.Add(key))
                        CollectCraftAncestors(nodes.Find(parent => parent.key == key));
            }
        }

        static TechnologyTreeNode[] CreateNodes()
        {
            string[] descriptions =
            {
                "采集木石与黏土，研究工具、烧炭、制陶和冶金，准备水力建造。",
                "让岸边水车经传动轴驱动机械，加工矿石、板材和机械零件。",
                "炼焦、炼钢并加工耐压材料，组装模块锅炉，用管道输送蒸汽动力。",
                "建立低压电网，制造电路，让采矿、冶炼与装配自动运行。",
                "勘探油藏，建立采油、炼油和化工产线，用柴油与变压器扩展供电。",
                "提纯硅材，制造电子器件、控制模块和耐热合金，建立高压电网。",
                "加工核燃料，接通反应堆控制台、冷却循环与核废料专用仓。"
            };
            var nodes = new List<TechnologyTreeNode>();
            for (int era = 0; era <= 6; era++)
                nodes.Add(new TechnologyTreeNode { key = era * 100, era = era, researchId = -1,
                    name = TechCatalog.EraName((TechEra)era), description = descriptions[era], command = "research",
                    cost = TechCatalog.ResearchCost((TechEra)era), parents = era == 0 ? Array.Empty<int>() : new[] { (era - 1) * 100 } });
            foreach (var d in PrimitiveCatalog.All) AddResearch(nodes, 0, (int)d.id, d.name, d.description, d.prerequisites, d.cost, "primitive");
            foreach (var d in WaterCatalog.All) AddResearch(nodes, 1, (int)d.id, d.name, d.description, d.prerequisites, d.cost, "water");
            foreach (var d in SteamCatalog.All) AddResearch(nodes, 2, (int)d.id, d.name, d.description, d.prerequisites, d.cost, "steam");
            foreach (var d in LowVoltageCatalog.All) AddResearch(nodes, 3, (int)d.id, d.name, d.description, d.prerequisites, d.cost, "lv");
            foreach (var d in PetrochemicalCatalog.All) AddResearch(nodes, 4, (int)d.id, d.name, d.description, d.prerequisites, d.cost, "mv");
            foreach (var d in PrecisionCatalog.All) AddResearch(nodes, 5, (int)d.id, d.name, d.description, d.prerequisites, d.cost, "hv");

            // These masks mirror each catalog's era acceptance rule.
            AddEraParents(nodes, 1, (1 << (int)PrimitiveTech.Bronze) | (1 << (int)PrimitiveTech.Bloomery) |
                (1 << (int)PrimitiveTech.Construction));
            AddEraParents(nodes, 2, 127); // Agriculture is optional.
            AddEraParents(nodes, 3, 255);
            AddEraParents(nodes, 4, 255);
            AddEraParents(nodes, 5, 255);
            AddEraParents(nodes, 6, PrecisionCatalog.AllResearch);

            // Explicit cross-era craft checks. Era acceptance already reaches all other mandatory
            // ancestors, so it is not copied onto every craft as additional connections.
            AddParents(nodes, Key(1, (int)WaterTech.Crushing), Key(0, (int)PrimitiveTech.Bloomery));
            AddParents(nodes, Key(1, (int)WaterTech.Blast), Key(0, (int)PrimitiveTech.Bloomery), Key(0, (int)PrimitiveTech.CopperTin));
            AddParents(nodes, Key(1, (int)WaterTech.Pressing), Key(0, (int)PrimitiveTech.Bloomery));
            AddParents(nodes, Key(1, (int)WaterTech.Components), Key(0, (int)PrimitiveTech.Bronze));
            AddParents(nodes, Key(1, (int)WaterTech.Agriculture), Key(0, (int)PrimitiveTech.Cooking));
            AddParents(nodes, Key(2, (int)SteamTech.Coking), Key(0, (int)PrimitiveTech.Charcoal));
            AddParents(nodes, Key(2, (int)SteamTech.Refractories), Key(0, (int)PrimitiveTech.Ceramics));
            AddParents(nodes, Key(2, (int)SteamTech.PigIron), Key(1, (int)WaterTech.Blast), Key(1, (int)WaterTech.Crushing));
            AddParents(nodes, Key(2, (int)SteamTech.Machining), Key(1, (int)WaterTech.Pressing), Key(1, (int)WaterTech.Components));
            nodes.Sort((a, b) => a.key.CompareTo(b.key));
            return nodes.ToArray();
        }
    }
}
