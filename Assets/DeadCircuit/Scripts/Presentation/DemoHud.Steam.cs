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
            steamHeading = FlowLabel("Steam Research Heading", content, "蒸汽工艺 · 热工、钢铁与真实轴系加工", 768, 30, 18, Accent);
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
                    !string.IsNullOrEmpty(requirement) ? requirement : can ? "已满足，可研究" : "等待主机同步研究条件";
                Set(row.details, definition.name + " · " + state + "\n" + definition.description +
                    "\n前置：" + SteamPrerequisites(world, definition) +
                    "\n样品 / 扣料（随身持有 / 需求）：" + MaterialText(definition.cost, player.items, true) +
                    "\n真实生产：" + SteamResearchEvidence(world, definition.id) +
                    "\n" + (known || can ? "研究状态：" : "不可研究：") + reason);
                row.details.color = known ? Accent : Ink;
                float height = Mathf.Max(164, row.details.GetPreferredValues(row.details.text, 562, float.PositiveInfinity).y + 16);
                row.details.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height - 12);
                row.root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
                row.research.button.interactable = can;
                Set(row.research.label, known ? "已研究" : can ? "研究" + definition.name : state + "\n查看左侧原因");
                Tint(row.research, known ? Selected : Raised);
            }
            if (!visible) return;

            bool entered = world.era >= TechEra.Steam;
            string rules = "材料从研究者随身库存扣除，全队共享科技；需近距可见工作台。科技与角色成长独立，不消耗角色技能点。";
            SetFlowText(researchMaterials, entered ? rules : "进入蒸汽时代的材料：" +
                MaterialText(TechCatalog.ResearchCost(TechEra.Steam), player.items, true) + "\n" + rules, 52);
            if (entered)
            {
                SetFlowText(researchRequirement, "逐项研究下列8种工艺，设备及配方还需对应节点。完成真实生产验收后，切换 LV低压页研究下一时代。", 48);
                researchRequirement.color = Accent;
                researchButton.button.interactable = false;
                Set(researchButton.label, "在节点中研究蒸汽工艺");
            }
            SetFlowText(steamGuide, "启动顺序：焦炉制焦＋耐火窑烧砖 → 水车经轴驱动炼铁高炉、精炼炉，生产首批生铁与钢。" +
                "\n研究机床加工后，既有机械压机压制钢板，首批机床仍接水力轴系；再加工耐压管件，建模块锅炉与蒸汽引擎。" +
                "\n锅炉、燃烧室、取水泵、烟囱接触组群；蒸汽管接引擎输入，传动轴接机械输出。", 80);

            int researched = 0;
            foreach (var definition in SteamCatalog.All)
                if (SteamCatalog.HasResearch(world, (int)definition.id)) researched++;
            string lowVoltage = SteamCatalog.LowVoltageRequirement(world);
            SetFlowText(steamProduction, "LV低压入口 · 全队真实生产验收" +
                "\n蒸汽研究 " + researched + "/" + SteamCatalog.All.Length + " · 缺 " + (SteamCatalog.All.Length - researched) + " 项" +
                "\n" + SteamProgress("焦炭", world.steamCoked, 6) + " · " + SteamProgress("耐火砖", world.steamRefractory, 8) +
                "\n" + SteamProgress("生铁", world.steamPigIron, 6) + " · " + SteamProgress("钢", world.steamSteel, 4) +
                "\n" + SteamProgress("钢板", world.steamPlates, 4) + " · " + SteamProgress("耐压管件", world.steamFittings, 4) +
                "\n" + SteamProgress("蒸汽引擎经轴驱动机械加工产出", world.steamDriven, 4) +
                "\n背包样品、搬运、空转及仅摆放锅炉不计生产；水车产出不能代替蒸汽引擎经轴加工验收。" +
                "\n低压工艺前置：" + (string.IsNullOrEmpty(lowVoltage) ? "已满足；进入 LV低压页查看晋级扣料及其余条件。" : lowVoltage), 180);
            steamProduction.color = string.IsNullOrEmpty(lowVoltage) ? Accent : Ink;
            SetFlowText(steamResources, "蒸汽原料勘探 · 群系影响露头分布" +
                "\n煤：针叶林、冻原较多，用于焦化与供热；首批焦化仍需木炭。" +
                "\n石灰石：沙漠、雪山、河谷较多，作为生铁冶炼与钢铁精炼的熔剂。" +
                "\n耐火黏土：湿地、沼泽、河谷较多，用于耐火炉衬；普通黏土、陶砖不能替代。" +
                "\n携带镐采集矿点；打开设备检查可看可见矿点的群系、余量与合适工具。", 128);
            RefreshSteamRecipes(world);
        }

        void RefreshSteamRecipes(WorldState world)
        {
            var text = new StringBuilder(1024);
            text.Append("蒸汽配方速查 · 在对应设备检查中切换配方");
            foreach (var recipe in TechCatalog.Recipes)
            {
                if (recipe.era != TechEra.Steam) continue;
                string requirement = PrimitiveCatalog.RecipeRequirement(world, recipe);
                if (string.IsNullOrEmpty(requirement)) requirement = SteamCatalog.RecipeRequirement(world, recipe);
                text.Append("\n\n").Append(recipe.name).Append(" · ").Append(DemoCatalog.BuildName(recipe.building))
                    .Append(" · ").Append(recipe.duration.ToString("0.#")).Append("秒")
                    .Append("\n设备内输入：").Append(MaterialText(recipe.inputs)).Append(" → 产出：").Append(MaterialText(recipe.outputs))
                    .Append("\n").Append(string.IsNullOrEmpty(requirement) ? "工艺已解锁，供料并接通所需动力后生产。" : requirement);
            }
            text.Append("\n\n下方本阶段设备可找到焦炉、耐火窑、炼铁高炉、精炼炉和机床，点击已解锁设备进入建造。");
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
                    return "水力阶段验收：" + (string.IsNullOrEmpty(water) ? "已满足" : water);
                case SteamTech.PigIron:
                    return SteamProgress("焦炭", world.steamCoked, 3) + " · " + SteamProgress("耐火砖", world.steamRefractory, 4);
                case SteamTech.Refining: return SteamProgress("生铁", world.steamPigIron, 3);
                case SteamTech.Machining: return SteamProgress("钢", world.steamSteel, 2);
                case SteamTech.Piping: return SteamProgress("钢板", world.steamPlates, 2);
                case SteamTech.Boiler:
                    return SteamProgress("钢板", world.steamPlates, 2) + " · " + SteamProgress("耐压管件", world.steamFittings, 2);
                default: return "本项凭前置研究及随身样品解锁；研究后需蒸汽引擎经轴实际加工4件，供 LV验收。";
            }
        }

        static string SteamProgress(string name, int actual, int target) =>
            name + " " + actual + "/" + target + (actual >= target ? "（已满足）" : "（缺 " + (target - actual) + "）");

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
