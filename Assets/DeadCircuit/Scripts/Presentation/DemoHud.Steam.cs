using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class DemoHud
    {
        readonly List<PrimitiveResearchRow> steamResearchRows = new List<PrimitiveResearchRow>();
        TextMeshProUGUI steamHeading, steamGuide, steamProduction, steamResources, steamRecipes;

        void MakeSteamTechnology(RectTransform content)
        {
            steamHeading = FlowLabel("Steam Research Heading", content, "蒸汽工艺 · 冶炼钢铁，以蒸汽驱动机械", 768, 30, 18, Accent);
            steamGuide = FlowLabel("Steam Startup Guide", content, "", 768, 80, 15, Ink);
            foreach (var definition in SteamCatalog.All)
            {
                int id = (int)definition.id;
                RectTransform root = FlowRow("Steam Research Row " + id, content, 768, 164);
                var details = Label("Steam Research Details " + id, root, "", 8, 6, 562, 152, 15, Ink);
                details.enableAutoSizing = false;
                details.alignment = TextAlignmentOptions.TopLeft;
                UiButton button = Button("Steam Research " + id, root, "", 588, 12, 170, 64, () => ResearchSteam(id));
                steamResearchRows.Add(new PrimitiveResearchRow { id = id, root = root, details = details, research = button });
            }
            steamProduction = FlowLabel("Steam Production Acceptance", content, "", 768, 180, 15, Ink);
            steamResources = FlowLabel("Steam Resource Guide", content, "", 768, 128, 15, Ink);
            steamRecipes = FlowLabel("Steam Recipe Guide", content, "", 768, 128, 15, Ink);
        }

        void RefreshSteamTechnology(WorldState world, PlayerState player)
        {
            bool visible = selectedEra == 2;
            steamHeading.gameObject.SetActive(visible);
            steamGuide.gameObject.SetActive(visible);
            steamProduction.gameObject.SetActive(visible);
            steamResources.gameObject.SetActive(visible);
            steamRecipes.gameObject.SetActive(visible);
            foreach (var row in steamResearchRows)
            {
                row.root.gameObject.SetActive(visible);
                if (!visible) continue;
                var definition = SteamCatalog.Definition(row.id);
                bool known = SteamCatalog.HasResearch(world, row.id);
                bool ready = bridge.IsRunning && player.connected && player.hp > 0f && player.interactCooldown <= 0f;
                bool can = !known && ready && SteamCatalog.CanResearch(world, player, row.id);
                string requirement = SteamCatalog.ResearchRequirement(world, player, row.id);
                string state = known ? "已研究" : !bridge.IsRunning || !player.connected || player.hp <= 0f ? "角色尚未就绪" :
                    player.interactCooldown > 0f ? "交互冷却中" : world.era < TechEra.Steam ? "先进入蒸汽时代" : can ? "可研究" : "条件未满足";
                string reason = known ? "全队已完成" : !ready ? state :
                    !string.IsNullOrEmpty(requirement) ? requirement : can ? "已满足，可研究" : "正在同步研究条件，请稍候";
                Set(row.details, definition.name + " · " + state + "\n" + definition.description +
                    "\n前置研究：" + SteamPrerequisites(world, definition) +
                    "\n研究消耗（持有 / 需要）：" + MaterialText(definition.cost, player.items, true) +
                    "\n生产进度：" + SteamResearchEvidence(world, definition.id) +
                    "\n" + (known || can ? "研究状态：" : "不可研究：") + reason);
                row.details.color = known ? Accent : Ink;
                float height = Mathf.Max(164, row.details.GetPreferredValues(row.details.text, 562, float.PositiveInfinity).y + 16);
                row.details.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height - 12);
                row.root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
                row.research.button.interactable = can;
                Set(row.research.label, known ? "已研究" : can ? "研究" + definition.name : state + "\n查看条件");
                Tint(row.research, known ? Selected : Raised);
            }
            if (!visible) return;

            bool entered = world.era >= TechEra.Steam;
            string rules = "研究消耗背包材料，全队共享成果。需靠近2.4格内、视线畅通的工作台；不消耗角色技能点。";
            SetFlowText(researchMaterials, entered ? rules : "蒸汽时代研究消耗：" +
                MaterialText(TechCatalog.ResearchCost(TechEra.Steam), player.items, true) + "\n" + rules, 52);
            if (entered)
            {
                SetFlowText(researchRequirement, "完成下列8项工艺研究，解锁对应设备与配方。达到生产进度后，可在低压(LV)页研究下一时代。", 48);
                researchRequirement.color = Accent;
                researchButton.button.interactable = false;
                Set(researchButton.label, "在下方选择研究");
            }
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

        static string SteamPrerequisites(WorldState world, SteamTechnologyDefinition definition)
        {
            var names = new List<string>();
            foreach (var parent in SteamCatalog.All)
            {
                int id = (int)parent.id;
                if ((definition.prerequisites & (1 << id)) == 0) continue;
                names.Add(parent.name + (SteamCatalog.HasResearch(world, id) ? "（已研究）" : "（未研究）"));
            }
            switch (definition.id)
            {
                case SteamTech.Coking:
                    names.Add("原始炭化" + (PrimitiveCatalog.HasResearch(world, PrimitiveTech.Charcoal) ? "（已研究）" : "（未研究）"));
                    break;
                case SteamTech.Refractories:
                    names.Add("原始陶砖烧制" + (PrimitiveCatalog.HasResearch(world, PrimitiveTech.Ceramics) ? "（已研究）" : "（未研究）"));
                    break;
                case SteamTech.PigIron:
                    names.Add("水力鼓风冶炼" + (WaterCatalog.HasResearch(world, WaterTech.Blast) ? "（已研究）" : "（未研究）"));
                    names.Add("水力机械破碎" + (WaterCatalog.HasResearch(world, WaterTech.Crushing) ? "（已研究）" : "（未研究）"));
                    break;
                case SteamTech.Machining:
                    names.Add("水力锻造压板" + (WaterCatalog.HasResearch(world, WaterTech.Pressing) ? "（已研究）" : "（未研究）"));
                    names.Add("水力齿轮轴承" + (WaterCatalog.HasResearch(world, WaterTech.Components) ? "（已研究）" : "（未研究）"));
                    break;
            }
            return names.Count == 0 ? "无" : string.Join("、", names);
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
                default: return "本项需完成前置研究并备齐材料。研究后，使设备与传动轴、由模块锅炉供汽的蒸汽引擎同网，累计加工4件，满足低压(LV)时代的蒸汽加工条件。";
            }
        }

        static string SteamProgress(string name, int actual, int target) =>
            name + " " + actual + "/" + target + (actual >= target ? "（已满足）" : "（还需 " + (target - actual) + "）");

        void ResearchSteam(int id)
        {
            var world = bridge.World;
            var player = bridge.LocalPlayer;
            if (!technology.gameObject.activeSelf || selectedEra != 2 || !bridge.IsRunning || world == null || player == null ||
                !player.connected || player.hp <= 0f || player.interactCooldown > 0f ||
                SteamCatalog.HasResearch(world, id) || !SteamCatalog.CanResearch(world, player, id)) return;
            Send("steam", id);
        }
    }
}
