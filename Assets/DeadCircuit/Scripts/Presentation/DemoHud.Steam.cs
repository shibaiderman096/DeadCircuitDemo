using System.Text;
using TMPro;
using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class DemoHud
    {
        TextMeshProUGUI steamHeading, steamGuide, steamProduction, steamResources, steamRecipes;

        void MakeSteamTechnology(RectTransform content)
        {
            steamHeading = FlowLabel("Steam Research Heading", content, "蒸汽工艺 · 冶炼钢铁，以蒸汽驱动机械", 768, 30, 18, Accent);
            steamGuide = FlowLabel("Steam Startup Guide", content, "", 768, 80, 15, Ink);
            steamProduction = FlowLabel("Steam Production Acceptance", content, "", 768, 180, 15, Ink);
            steamResources = FlowLabel("Steam Resource Guide", content, "", 768, 128, 15, Ink);
            steamRecipes = FlowLabel("Steam Recipe Guide", content, "", 768, 128, 15, Ink);
        }

        void RefreshSteamTechnology(WorldState world)
        {
            bool visible = selectedEra == 2;
            steamHeading.gameObject.SetActive(visible);
            steamGuide.gameObject.SetActive(visible);
            steamProduction.gameObject.SetActive(visible);
            steamResources.gameObject.SetActive(visible);
            steamRecipes.gameObject.SetActive(visible);
            if (!visible) return;
            SetFlowText(steamGuide, "起步：用焦炉制焦、耐火窑烧制耐火砖，再用水车经传动轴驱动炼铁高炉与精炼炉，生产生铁和钢。" +
                "\n研究机床加工后，用机械压机压制钢板，为机床接通水车与传动轴；加工耐压管件后，建造模块锅炉与蒸汽引擎。" +
                "\n锅炉、燃烧室、取水泵和烟囱需沿边相连。蒸汽管连接蒸汽引擎输入端，传动轴连接机械输出端。", 80);

            int researched = 0;
            foreach (var definition in SteamCatalog.All)
                if (SteamCatalog.HasResearch(world, (int)definition.id)) researched++;
            string lowVoltage = SteamCatalog.LowVoltageRequirement(world);
            SetFlowText(steamProduction, "低压(LV)时代条件 · 全队累计生产进度" +
                "\n蒸汽研究 " + researched + "/" + SteamCatalog.All.Length + " · 缺 " + (SteamCatalog.All.Length - researched) + " 项" +
                "\n" + SteamProgress("焦炭", world.steamCoked, 6) + " · " + SteamProgress("耐火砖", world.steamRefractory, 8) +
                "\n" + SteamProgress("生铁", world.steamPigIron, 6) + " · " + SteamProgress("钢", world.steamSteel, 4) +
                "\n" + SteamProgress("钢板", world.steamPlates, 4) + " · " + SteamProgress("耐压管件", world.steamFittings, 4) +
                "\n" + SteamProgress("蒸汽引擎经传动轴驱动的加工产出", world.steamDriven, 4) +
                "\n生产进度仅累计完成的产出，持有、搬运物品或设备空转均不计入。蒸汽加工需由模块锅炉供汽，设备与蒸汽引擎、传动轴同网；仅用水车驱动不计入。" +
                "\n低压(LV)研究条件：" + (string.IsNullOrEmpty(lowVoltage) ? "生产条件已满足；前往低压(LV)页查看研究消耗与其余条件。" : lowVoltage), 180);
            steamProduction.color = string.IsNullOrEmpty(lowVoltage) ? Accent : Ink;
            SetFlowText(steamResources, "蒸汽原料 · 在不同群系寻找矿点" +
                "\n煤：针叶林、冻原较多，用于焦化与供热；制焦时还需消耗木炭。" +
                "\n石灰石：荒漠、雪山、河谷较多，作为生铁冶炼与钢铁精炼的熔剂。" +
                "\n耐火黏土：湿地、沼泽、河谷较多，用于制作耐火砖；普通黏土、陶砖不能替代。" +
                "\n携带镐开采矿点。在设备详情中查看已发现矿点的群系、余量与适用工具。", 128);
            RefreshSteamRecipes(world);
        }

        void RefreshSteamRecipes(WorldState world)
        {
            var text = new StringBuilder(1024);
            text.Append("蒸汽配方 · 在对应设备详情中切换配方");
            foreach (var recipe in TechCatalog.Recipes)
            {
                if (recipe.era != TechEra.Steam) continue;
                string requirement = PrimitiveCatalog.RecipeRequirement(world, recipe);
                if (string.IsNullOrEmpty(requirement)) requirement = SteamCatalog.RecipeRequirement(world, recipe);
                text.Append("\n\n").Append(recipe.name).Append(" · ").Append(DemoCatalog.BuildName(recipe.building))
                    .Append(" · ").Append(recipe.duration.ToString("0.#")).Append("秒")
                    .Append("\n消耗：").Append(MaterialText(recipe.inputs)).Append(" → 产出：").Append(MaterialText(recipe.outputs))
                    .Append("\n").Append(string.IsNullOrEmpty(requirement) ? "工艺已解锁，供料并接通所需动力后生产。" : requirement);
            }
            text.Append("\n\n在下方设备列表选择焦炉、耐火窑、炼铁高炉、精炼炉或机床。已解锁的设备可点击建造。");
            SetFlowText(steamRecipes, text.ToString(), 128);
        }

        static string SteamResearchEvidence(WorldState world, SteamTech id)
        {
            switch (id)
            {
                case SteamTech.Coking:
                case SteamTech.Refractories:
                    string water = WaterCatalog.SteamRequirement(world);
                    return "水力研究与生产条件：" + (string.IsNullOrEmpty(water) ? "已满足" : water);
                case SteamTech.PigIron:
                    return SteamProgress("焦炭", world.steamCoked, 3) + " · " + SteamProgress("耐火砖", world.steamRefractory, 4);
                case SteamTech.Refining: return SteamProgress("生铁", world.steamPigIron, 3);
                case SteamTech.Machining: return SteamProgress("钢", world.steamSteel, 2);
                case SteamTech.Piping: return SteamProgress("钢板", world.steamPlates, 2);
                case SteamTech.Boiler:
                    return SteamProgress("钢板", world.steamPlates, 2) + " · " + SteamProgress("耐压管件", world.steamFittings, 2);
                default: return "完成前置研究并备齐材料；设备接入传动轴与锅炉供汽的蒸汽引擎同网，累计加工4件即可进入低压。";
            }
        }

        static string SteamProgress(string name, int actual, int target) =>
            name + " " + actual + "/" + target + (actual >= target ? "（已满足）" : "（还需 " + (target - actual) + "）");

    }
}
