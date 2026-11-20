using System;
using System.Globalization;

namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        // Observation only: search existing authority nodes and read stored tile
        // labels. Never sample an unknown chunk, generate terrain or grant oil.
        private void ProspectPetrochemical(PlayerState p)
        {
            if (p == null || !p.connected || !(p.hp > 0f) || FindPlayer(p.id) != p || p.interactCooldown > 0f) return;
            if (State.era < TechEra.MediumVoltage)
            { Notice(p, "原油勘探需要中压时代。"); return; }
            if (!PetrochemicalCatalog.HasResearch(State, PetrochemicalTech.Prospecting))
            { Notice(p, "请先研究原油勘探。"); return; }

            NodeState nearest = null;
            double nearestSquared = 48 * 48;
            foreach (var node in State.nodes)
            {
                if (node.kind != NodeKind.Oil || node.stock <= 0) continue;
                double dx = node.x - (double)p.x, dy = node.y - (double)p.y;
                double squared = dx * dx + dy * dy;
                if (!(squared <= nearestSquared)) continue;
                // A malformed authority node must not expose unknown terrain.
                char ground = WorldTiles.Ground(State, node.x, node.y);
                if (ground == '?' || ground == '2') continue;
                if (nearest != null && squared == nearestSquared &&
                    (node.y > nearest.y || node.y == nearest.y && node.x >= nearest.x)) continue;
                nearest = node; nearestSquared = squared;
            }
            p.interactCooldown = .5f;
            if (nearest == null)
            {
                Notice(p, "已生成区域内半径48格未发现有库存的油源；请继续探索荒漠（沙漠）、湿地、沼泽或河谷。群系倾向不保证每格有油。");
                return;
            }

            State.mvSurvey = 1;
            var biome = (BiomeKind)(WorldTiles.Biome(State, nearest.x, nearest.y) - '0');
            var rule = BiomeResourceCatalog.Get(biome);
            string biomeName = rule == null ? "未知群系" : WorldGenerationCatalog.Biomes[(int)biome].name;
            string tendency = rule != null && rule.oilPermille >= 18 ? "沉积油藏倾向较高" : "低概率小油藏区";
            Notice(p, "实测油源(" + nearest.x + "," + nearest.y + ")，" +
                PetrochemicalOilBearing(nearest.x - (double)p.x, nearest.y - (double)p.y) + "，距离" +
                Math.Sqrt(nearestSquared).ToString("0.0", CultureInfo.InvariantCulture) + "格；" +
                biomeName + "：" + tendency + "。剩余" + nearest.stock + "单位，需机器采抽。");
        }

        private static string PetrochemicalOilBearing(double dx, double dy)
        {
            if (Math.Abs(dx) < .01 && Math.Abs(dy) < .01) return "当前位置";
            // World +X is east and +Y is north, matching the rendered world.
            string[] directions = { "东", "东北", "北", "西北", "西", "西南", "南", "东南" };
            int sector = (int)Math.Floor(Math.Atan2(dy, dx) * 4 / Math.PI + 8.5) % 8;
            return directions[sector] + "方";
        }
    }
}
