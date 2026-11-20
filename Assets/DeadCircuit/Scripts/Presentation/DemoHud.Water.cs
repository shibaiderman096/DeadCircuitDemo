using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class DemoHud
    {
        readonly List<PrimitiveResearchRow> waterResearchRows = new List<PrimitiveResearchRow>();
        TextMeshProUGUI waterHeading, waterProduction, waterMillDetails;
        RectTransform waterMillRow;
        UiButton waterMillAction;
        float waterMillSendAt;

        void MakeWaterTechnology(RectTransform content)
        {
            waterHeading = FlowLabel("Water Research Heading", content, "水力工艺 · 用水车与传动轴驱动生产", 768, 30, 18, Accent);
            foreach (var definition in WaterCatalog.All)
            {
                int id = (int)definition.id;
                RectTransform root = FlowRow("Water Research Row " + id, content, 768, 144);
                var details = Label("Water Research Details " + id, root, "", 8, 6, 562, 132, 15, Ink);
                details.enableAutoSizing = false;
                details.alignment = TextAlignmentOptions.TopLeft;
                UiButton button = Button("Water Research " + id, root, "", 588, 12, 170, 64, () => ResearchWater(id));
                SetButtonArt(button, "ui_research", 22);
                waterResearchRows.Add(new PrimitiveResearchRow { id = id, root = root, details = details, research = button });
            }
            waterProduction = FlowLabel("Water Production Acceptance", content, "", 768, 128, 15, Ink);
            waterMillRow = FlowRow("Water Mill Actions", content, 768, 132);
            waterMillDetails = Label("Water Mill Details", waterMillRow, "", 8, 6, 562, 120, 15, Ink);
            waterMillDetails.enableAutoSizing = false;
            waterMillDetails.alignment = TextAlignmentOptions.TopLeft;
            waterMillAction = Button("Water Mill Action", waterMillRow, "磨粉", 588, 12, 170, 64, RequestWaterMill);
            SetButtonArt(waterMillAction, "ui_craft", 22);
        }

        void RefreshWaterTechnology(WorldState world, PlayerState player)
        {
            bool visible = selectedEra == 1 && WaterCatalog.Enabled(world);
            waterHeading.gameObject.SetActive(visible);
            waterProduction.gameObject.SetActive(visible);
            bool millVisible = selectedEra == 1 && (WaterCatalog.Enabled(world) || world.era >= TechEra.Water);
            waterMillRow.gameObject.SetActive(millVisible);
            foreach (var row in waterResearchRows)
            {
                row.root.gameObject.SetActive(visible);
                if (!visible) continue;
                var definition = WaterCatalog.Definition(row.id);
                bool known = WaterCatalog.HasResearch(world, row.id);
                bool ready = bridge.IsRunning && player.connected && player.hp > 0f && player.interactCooldown <= 0f;
                bool can = !known && ready && WaterCatalog.CanResearch(world, player, row.id);
                string requirement = WaterCatalog.ResearchRequirement(world, player, row.id);
                string state = known ? "已研究" : !bridge.IsRunning || !player.connected || player.hp <= 0f ? "角色尚未就绪" :
                    player.interactCooldown > 0f ? "交互冷却中" : can ? "可研究" : "条件未满足";
                Set(row.details, definition.name + " · " + state + "\n" + definition.description +
                    "\n前置研究：" + WaterPrerequisites(world, definition.prerequisites) +
                    "\n研究消耗（持有 / 需要）：" + MaterialText(definition.cost, player.items, true) +
                    "\n研究条件：" + (known ? "已完成" : string.IsNullOrEmpty(requirement) ? "已满足" : requirement));
                row.details.color = known ? Accent : Ink;
                float height = Mathf.Max(144, row.details.GetPreferredValues(row.details.text, 562, float.PositiveInfinity).y + 16);
                row.details.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height - 12);
                row.root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
                row.research.button.interactable = can;
                Set(row.research.label, known ? "已研究" : can ? "研究" + definition.name : state);
                Tint(row.research, known ? Selected : Raised);
            }
            if (millVisible) RefreshWaterMill(world, player);
            if (!visible) return;

            bool entered = world.era >= TechEra.Water;
            SetFlowText(researchMaterials, entered
                ? "逐项研究水力工艺，材料从背包扣除。需靠近2.4格内、视线畅通的工作台；灌溉磨粉为可选研究。"
                : "进入水力时代后可研究下方工艺。时代研究消耗：" + MaterialText(TechCatalog.ResearchCost(TechEra.Water), player.items, true), 52);
            if (entered)
            {
                SetFlowText(researchRequirement, "设备与配方需先完成对应研究。由水车经传动轴驱动生产，达到下方进度后可研究蒸汽时代。", 48);
                researchRequirement.color = Accent;
                researchButton.button.interactable = false;
                Set(researchButton.label, "在下方选择研究");
            }
            string steam = WaterCatalog.SteamRequirement(world);
            SetFlowText(waterProduction, "水力生产进度 · 仅计入与供能水车、传动轴同网的生产和运输" +
                "\n碎铁矿 " + world.waterCrushed + "/4 · 鼓风炉炼铁 " + world.waterSmelted + "/6 · 铁板 " + world.waterPressed + "/4" +
                "\n齿轮 " + world.waterGears + "/2 · 轴承 " + world.waterBearings + "/2 · 机械装卸器送达 " + world.waterTransported + "/4" +
                "\n可选农业：面粉产量 " + world.waterMilled + " · 累计灌溉量 " + world.waterIrrigated.ToString("0.#") +
                "\n蒸汽时代条件：" + (string.IsNullOrEmpty(steam) ? "水力研究与生产条件已满足" : steam), 128);
        }

        void RefreshWaterMill(WorldState world, PlayerState player)
        {
            bool millReady = WaterMillPermit(world, player, out int x, out int y, out string reason);
            var needs = player.needs;
            Set(waterMillDetails, "动力磨粉 · 小麦×2 → 面粉×3" +
                "\n随身小麦×" + Count(needs?.foods, (int)FoodKind.Wheat) + " · 面粉×" + Count(needs?.foods, (int)FoodKind.Flour) +
                "\n" + (millReady ? "水力磨坊（" + x + ", " + y + "）机械动力已接通，可以磨粉。" : reason) +
                "\n水车或蒸汽引擎均可驱动；仅与供能水车、传动轴同网的产出计入水力进度。" +
                "\n也可靠近工作台，在 J → 烹饪中手工磨粉。");
            waterMillDetails.color = millReady ? Ink : Muted;
            float millHeight = Mathf.Max(132, waterMillDetails.GetPreferredValues(waterMillDetails.text, 562, float.PositiveInfinity).y + 16);
            waterMillDetails.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, millHeight - 12);
            waterMillRow.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = millHeight;
            waterMillAction.button.interactable = millReady;
            Set(waterMillAction.label, millReady ? "磨粉" : "暂不能磨粉");
        }

        static string WaterPrerequisites(WorldState world, int mask)
        {
            var names = new List<string>();
            foreach (var definition in WaterCatalog.All)
            {
                int id = (int)definition.id;
                if ((mask & (1 << id)) == 0) continue;
                names.Add(definition.name + (WaterCatalog.HasResearch(world, id) ? "（已研究）" : "（未研究）"));
            }
            return names.Count == 0 ? "无" : string.Join("、", names);
        }

        void ResearchWater(int id)
        {
            var world = bridge.World;
            var player = bridge.LocalPlayer;
            if (!technology.gameObject.activeSelf || selectedEra != 1 || !bridge.IsRunning || world == null || player == null ||
                !player.connected || player.hp <= 0f || player.interactCooldown > 0f ||
                !WaterCatalog.Enabled(world) || WaterCatalog.HasResearch(world, id) ||
                !WaterCatalog.CanResearch(world, player, id)) return;
            Send("water", id);
        }

        void RequestWaterMill()
        {
            if (!technology.gameObject.activeSelf || selectedEra != 1) return;
            if (!WaterMillPermit(bridge.World, bridge.LocalPlayer, out int x, out int y, out string reason))
            { Notify(reason, true); RefreshAll(); return; }
            localFeedback = "";
            Send("mill", 0, x, y);
            waterMillSendAt = Time.unscaledTime + .4f;
            if (!CurrentLocalFeedback()) Notify("已请求磨粉，请等待结果。");
            RefreshAll();
        }

        bool WaterMillPermit(WorldState world, PlayerState player, out int x, out int y, out string reason)
        {
            x = y = 0;
            reason = "";
            if (!bridge.IsRunning || world == null || player == null || !player.connected || player.hp <= 0f)
                reason = "角色尚未就绪。";
            else if (!PrimitiveCatalog.IsBuildUnlocked(world, BuildKind.WaterMill)) reason = PrimitiveCatalog.BuildRequirement(world, BuildKind.WaterMill);
            else if (!WaterCatalog.IsBuildUnlocked(world, BuildKind.WaterMill)) reason = WaterCatalog.BuildRequirement(world, BuildKind.WaterMill);
            else if (!PrimitiveCatalog.IsCookingUnlocked(world)) reason = "先研究原始工艺“烹饪净水”。";
            else if (world.survival == null || !world.survival.enabled) reason = "此世界未开启生存模式，无法加工食材。";
            else if (player.needs == null || !player.needs.initialized) reason = "正在同步食材库存，请稍候。";
            else if (player.interactCooldown > 0f || Time.unscaledTime < waterMillSendAt) reason = "交互冷却中。";
            else if (Count(player.needs.foods, (int)FoodKind.Wheat) < 2) reason = "小麦不足，需要小麦×2。";
            else if (Count(player.needs.foods, (int)FoodKind.Flour) > SurvivalCatalog.StackLimit - 3) reason = "面粉库存空间不足，请腾出3份容量。";
            if (reason.Length != 0) return false;
            float nearest = float.PositiveInfinity;
            foreach (var building in world.buildings)
            {
                if (building == null || building.kind != BuildKind.WaterMill || building.hp <= 0f ||
                    !building.powered || !(building.powerRatio > 0f)) continue;
                for (int cy = building.y; cy < building.y + DemoCatalog.Height(building); cy++)
                    for (int cx = building.x; cx < building.x + DemoCatalog.Width(building); cx++)
                    {
                        float dx = cx - player.x, dy = cy - player.y, distance = dx * dx + dy * dy;
                        if (distance >= nearest || !MapExploration.IsVisible(world, player, cx, cy) || !SurvivalInReach(world, player, cx, cy)) continue;
                        nearest = distance; x = cx; y = cy;
                    }
            }
            if (!float.IsPositiveInfinity(nearest)) return true;
            reason = "请靠近2.4格内、视线畅通且已接通机械动力的水力磨坊。";
            return false;
        }
    }
}
