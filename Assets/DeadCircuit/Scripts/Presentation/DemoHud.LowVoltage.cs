using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class DemoHud
    {
        readonly List<PrimitiveResearchRow> lowVoltageResearchRows = new List<PrimitiveResearchRow>();
        TextMeshProUGUI lowVoltageHeading, lowVoltageGuide, lowVoltageProduction, lowVoltageRecipes;
        RectTransform lowVoltageMachineControls, lowVoltageFilterButtons, lowVoltageBreakerButtons;
        TextMeshProUGUI lowVoltageMachineState;
        UiButton lowVoltageFilterPrevious, lowVoltageFilterNext, lowVoltageDisconnect, lowVoltageReset;

        void MakeLowVoltageTechnology(RectTransform content)
        {
            lowVoltageHeading = FlowLabel("Low Voltage Research Heading", content, "低压(LV)工艺 · 从机械绕线到电动生产与电网保护", 768, 30, 18, Accent);
            lowVoltageGuide = FlowLabel("Low Voltage Startup Guide", content, "", 768, 80, 15, Ink);
            foreach (var definition in LowVoltageCatalog.All)
            {
                int id = (int)definition.id;
                RectTransform root = FlowRow("Low Voltage Research Row " + id, content, 768, 164);
                var details = Label("Low Voltage Research Details " + id, root, "", 8, 6, 562, 152, 15, Ink);
                details.enableAutoSizing = false;
                details.alignment = TextAlignmentOptions.TopLeft;
                UiButton button = Button("Low Voltage Research " + id, root, "", 588, 12, 170, 64, () => ResearchLowVoltage(id));
                SetButtonArt(button, "ui_research", 22);
                lowVoltageResearchRows.Add(new PrimitiveResearchRow { id = id, root = root, details = details, research = button });
            }
            lowVoltageProduction = FlowLabel("Low Voltage Production Acceptance", content, "", 768, 180, 15, Ink);
            lowVoltageRecipes = FlowLabel("Low Voltage Recipe Guide", content, "", 768, 128, 15, Ink);
        }

        void RefreshLowVoltageTechnology(WorldState world, PlayerState player)
        {
            bool visible = selectedEra == 3;
            lowVoltageHeading.gameObject.SetActive(visible);
            lowVoltageGuide.gameObject.SetActive(visible);
            lowVoltageProduction.gameObject.SetActive(visible);
            lowVoltageRecipes.gameObject.SetActive(visible);
            foreach (var row in lowVoltageResearchRows)
            {
                row.root.gameObject.SetActive(visible);
                if (!visible) continue;
                var definition = LowVoltageCatalog.Definition(row.id);
                bool known = LowVoltageCatalog.HasResearch(world, row.id);
                bool ready = bridge.IsRunning && player.connected && player.hp > 0f && player.interactCooldown <= 0f;
                bool can = !known && ready && LowVoltageCatalog.CanResearch(world, player, row.id);
                string requirement = LowVoltageCatalog.ResearchRequirement(world, player, row.id);
                string state = known ? "已研究" : !bridge.IsRunning || !player.connected || player.hp <= 0f ? "角色尚未就绪" :
                    player.interactCooldown > 0f ? "交互冷却中" : world.era < TechEra.LowVoltage ? "先进入低压(LV)时代" : can ? "可研究" : "条件未满足";
                string reason = known ? "全队已完成" : !ready ? state :
                    !string.IsNullOrEmpty(requirement) ? requirement : can ? "已满足，可研究" : "正在同步研究条件，请稍候";
                Set(row.details, definition.name + " · " + state + "\n" + definition.description +
                    "\n前置研究：" + LowVoltagePrerequisites(world, definition) +
                    "\n研究消耗（持有 / 需要）：" + MaterialText(definition.cost, player.items, true) +
                    "\n生产进度：" + LowVoltageResearchEvidence(world, definition.id) +
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

            bool entered = world.era >= TechEra.LowVoltage;
            string rules = "研究消耗背包材料，全队共享成果。需靠近2.4格内、视线畅通的工作台；不消耗角色技能点。";
            SetFlowText(researchMaterials, entered ? rules : "低压(LV)时代研究消耗：" +
                MaterialText(TechCatalog.ResearchCost(TechEra.LowVoltage), player.items, true) + "\n" + rules, 52);
            if (entered)
            {
                SetFlowText(researchRequirement, "完成下列8项低压研究，解锁对应设备与配方。达到生产进度后，可在中压(MV)页研究下一时代。", 48);
                researchRequirement.color = Accent;
                researchButton.button.interactable = false;
                Set(researchButton.label, "在下方选择研究");
            }
            SetFlowText(lowVoltageGuide, "起步：进入低压(LV)时代后，用陶窑烧制陶瓷绝缘件；用水车或蒸汽引擎经传动轴驱动机床，拉制铜线，无需供电。" +
                "\n累计生产铜线×4、陶瓷绝缘件×2后，研究发电配电与绝缘电缆。为机械绕线台接通传动轴，制作线圈；靠近工作台，按 B → 制作，手工制作绝缘电缆。" +
                "\n累计制作线圈×2、绝缘电缆×4后研究电动机，在工作台装配电机；累计装配电机×2后研究电动加工。" +
                "\n研究继电电路后，在工作台制作继电器，为电路装配机接通低压(LV)电网，制造电路。累计电动加工产出4件，其中电路至少2件，即可研究自动装配。" +
                "\n研究分流过滤与断路保护后，可在设备详情中设置过滤物品，或断开、复位断路器。匹配物品送往前方，其余送往左侧；断路器保护所在低压(LV)电网。", 80);

            int researched = 0;
            foreach (var definition in LowVoltageCatalog.All)
                if (LowVoltageCatalog.HasResearch(world, definition.id)) researched++;
            string mediumVoltage = LowVoltageCatalog.MediumVoltageRequirement(world);
            SetFlowText(lowVoltageProduction, "中压(MV)时代条件 · 全队累计生产与运输进度" +
                "\n低压研究 " + researched + "/" + LowVoltageCatalog.All.Length + " · 缺 " + (LowVoltageCatalog.All.Length - researched) + " 项" +
                "\n" + SteamProgress("铜线", world.lvWire, 12) + " · " + SteamProgress("陶瓷绝缘件", world.lvInsulators, 8) +
                "\n" + SteamProgress("绝缘电缆", world.lvCable, 8) + " · " + SteamProgress("线圈", world.lvCoils, 4) +
                "\n" + SteamProgress("电机", world.lvMotors, 2) + " · " + SteamProgress("继电器", world.lvRelays, 4) +
                "\n" + SteamProgress("电路", world.lvCircuits, 6) + " · " + SteamProgress("电动加工产出", world.lvElectric, 8) +
                "\n" + SteamProgress("自动装配产出", world.lvAutomated, 4) +
                "\n" + SteamProgress("过滤物品送达", world.lvSorted, 4) +
                "\n" + SteamProgress("断路保护电网内的加工产出", world.lvProtected, 4) +
                "\n生产进度累计已完成的产出；过滤进度仅计匹配物品送达。持有、手工搬运物品或设备空转均不计入；断路保护加工要求断路器与设备同网、保持闭合且未跳闸。" +
                "\n中压(MV)研究条件：" + (string.IsNullOrEmpty(mediumVoltage) ? "生产条件已满足；前往中压(MV)页查看研究消耗与其余条件。" : mediumVoltage), 180);
            lowVoltageProduction.color = string.IsNullOrEmpty(mediumVoltage) ? Accent : Ink;
            RefreshLowVoltageRecipes(world);
        }

        void RefreshLowVoltageRecipes(WorldState world)
        {
            var text = new StringBuilder(2048);
            text.Append("低压配方 · 工作台配方见 B → 制作，其余在对应设备详情中切换");
            foreach (var recipe in TechCatalog.Recipes)
            {
                if (recipe.era != TechEra.LowVoltage) continue;
                string requirement = PrimitiveCatalog.RecipeRequirement(world, recipe);
                if (string.IsNullOrEmpty(requirement)) requirement = LowVoltageCatalog.RecipeRequirement(world, recipe);
                bool manual = recipe.building == BuildKind.Workbench;
                text.Append("\n\n").Append(recipe.name).Append(" · ")
                    .Append(DemoCatalog.BuildName(recipe.building)).Append(" · ").Append(recipe.duration.ToString("0.#")).Append("秒")
                    .Append(manual ? "\n背包消耗：" : "\n设备消耗：").Append(MaterialText(recipe.inputs))
                    .Append(" → 产出：").Append(MaterialText(recipe.outputs))
                    .Append("\n").Append(!string.IsNullOrEmpty(requirement) ? requirement :
                        manual ? "工艺已解锁；携带材料靠近视线畅通的工作台，按 B → 制作。" : "工艺已解锁；供料并接通所需动力后生产。");
            }
            text.Append("\n\n在下方设备列表选择机械绕线台、电动破碎机、过滤分流器或低压断路器。");
            SetFlowText(lowVoltageRecipes, text.ToString(), 128);
        }

        static string LowVoltagePrerequisites(WorldState world, LowVoltageTechnologyDefinition definition)
        {
            var names = new List<string>();
            foreach (var parent in LowVoltageCatalog.All)
            {
                int id = (int)parent.id;
                if ((definition.prerequisites & (1 << id)) == 0) continue;
                names.Add(parent.name + (LowVoltageCatalog.HasResearch(world, id) ? "（已研究）" : "（未研究）"));
            }
            return names.Count == 0 ? "完成蒸汽研究与生产条件，进入低压(LV)时代" : string.Join("、", names);
        }

        static string LowVoltageResearchEvidence(WorldState world, LowVoltageTech id)
        {
            string steam = SteamCatalog.LowVoltageRequirement(world);
            string progress;
            switch (id)
            {
                case LowVoltageTech.Generation:
                case LowVoltageTech.Cabling:
                    progress = SteamProgress("铜线", world.lvWire, 4) + " · " + SteamProgress("陶瓷绝缘件", world.lvInsulators, 2); break;
                case LowVoltageTech.Motors:
                    progress = SteamProgress("线圈", world.lvCoils, 2) + " · " + SteamProgress("绝缘电缆", world.lvCable, 4); break;
                case LowVoltageTech.Processing:
                    progress = SteamProgress("电机", world.lvMotors, 2); break;
                case LowVoltageTech.Relays:
                    progress = SteamProgress("线圈", world.lvCoils, 1) + " · " + SteamProgress("绝缘电缆", world.lvCable, 2); break;
                case LowVoltageTech.Assembly:
                    progress = SteamProgress("电动加工产出", world.lvElectric, 4) + " · " + SteamProgress("电路", world.lvCircuits, 2); break;
                default:
                    progress = SteamProgress("电路", world.lvCircuits, 2); break;
            }
            return string.IsNullOrEmpty(steam) ? progress : "蒸汽研究与生产条件：" + steam + "；" + progress;
        }

        void ResearchLowVoltage(int id)
        {
            var world = bridge.World;
            var player = bridge.LocalPlayer;
            if (!technology.gameObject.activeSelf || selectedEra != 3 || !bridge.IsRunning || world == null || player == null ||
                !player.connected || player.hp <= 0f || player.interactCooldown > 0f ||
                LowVoltageCatalog.HasResearch(world, id) || !LowVoltageCatalog.CanResearch(world, player, id)) return;
            Send("lv", id);
        }

        void MakeLowVoltageMachineControls(RectTransform content)
        {
            lowVoltageMachineControls = FlowRow("Low Voltage Machine Controls", content, 260, 120);
            var layout = lowVoltageMachineControls.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            lowVoltageMachineState = FlowLabel("Low Voltage Machine Configuration", lowVoltageMachineControls, "", 260, 76, 13, Ink);
            lowVoltageFilterButtons = FlowRow("Low Voltage Filter Buttons", lowVoltageMachineControls, 260, 38);
            lowVoltageFilterPrevious = Button("Previous Filter Item", lowVoltageFilterButtons, "上一物品", 0, 0, 124, 34, () => CycleLowVoltageFilter(-1));
            lowVoltageFilterNext = Button("Next Filter Item", lowVoltageFilterButtons, "下一物品", 130, 0, 124, 34, () => CycleLowVoltageFilter(1));
            lowVoltageBreakerButtons = FlowRow("Low Voltage Breaker Buttons", lowVoltageMachineControls, 260, 38);
            lowVoltageDisconnect = Button("Disconnect Circuit Breaker", lowVoltageBreakerButtons, "手动断开", 0, 0, 124, 34, () => ConfigureLowVoltageMachine(BuildKind.CircuitBreaker, 0));
            lowVoltageReset = Button("Reset Circuit Breaker", lowVoltageBreakerButtons, "复位接通", 130, 0, 124, 34, () => ConfigureLowVoltageMachine(BuildKind.CircuitBreaker, 1));
            lowVoltageMachineControls.gameObject.SetActive(false);
        }

        void RefreshLowVoltageMachineControls(WorldState world, PlayerState player, BuildingState building, bool inRange)
        {
            bool filter = building != null && building.kind == BuildKind.FilterSplitter;
            bool breaker = building != null && building.kind == BuildKind.CircuitBreaker;
            lowVoltageMachineControls.gameObject.SetActive(filter || breaker);
            if (!filter && !breaker) return;
            lowVoltageFilterButtons.gameObject.SetActive(filter);
            lowVoltageBreakerButtons.gameObject.SetActive(breaker);
            bool ready = bridge.IsRunning && inRange && player.connected && player.hp > 0f && player.interactCooldown <= 0f &&
                LowVoltageCatalog.IsBuildUnlocked(world, building.kind);
            string access = !inRange ? "请靠近2.4格内并保持视线畅通，再更改设置。" : !ready ? "需先解锁对应工艺，待角色就绪且交互冷却结束后更改。" : "可在2.4格内、视线畅通时更改设置。";
            if (filter)
            {
                int item = Mathf.Clamp(building.setting, 0, DemoCatalog.ItemCount - 1);
                bool inTransit = world.cargo != null && world.cargo.Exists(c => c.fromId == building.id || c.toId == building.id);
                SetFlowText(lowVoltageMachineState, "过滤物品：" + DemoCatalog.ItemNames[item] + "（" + item + " / 0–" + (DemoCatalog.ItemCount - 1) + "）" +
                    "\n匹配物品送往前方，其余送往左侧。需要低压(LV)供电；出口堵塞时等待，不改道。" +
                    "\n" + (inTransit ? "物品正在运输，请等待全部送达后再更改过滤物品。" : access), 76);
                lowVoltageFilterPrevious.button.interactable = lowVoltageFilterNext.button.interactable = ready && !inTransit;
                lowVoltageMachineState.color = inTransit ? Amber : Ink;
            }
            else
            {
                string state = building.tripped ? "过载跳闸" : building.setting == 1 ? "手动断开" : "闭合接通";
                SetFlowText(lowVoltageMachineState, "断路器：" + state +
                    "\n保护所在低压(LV)电网。过载跳闸后，同网设备停电，其他独立电网不受影响。" +
                    "\n请先减少用电或增加发电，再复位接通；电网仍过载时无法复位。\n" + access, 76);
                lowVoltageDisconnect.button.interactable = ready && building.setting == 0;
                lowVoltageReset.button.interactable = ready && (building.setting != 0 || building.tripped);
                lowVoltageMachineState.color = building.tripped ? Danger : building.setting == 1 ? Amber : Ink;
            }
            lowVoltageMachineControls.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight =
                lowVoltageMachineState.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight + 44;
        }

        void CycleLowVoltageFilter(int direction)
        {
            BuildingState building = InspectedBuilding();
            if (building == null || building.kind != BuildKind.FilterSplitter) return;
            ConfigureLowVoltageMachine(BuildKind.FilterSplitter, (building.setting + direction + DemoCatalog.ItemCount) % DemoCatalog.ItemCount);
        }

        void ConfigureLowVoltageMachine(BuildKind kind, int arg)
        {
            BuildingState building = InspectedBuilding();
            PlayerState player = bridge.LocalPlayer;
            if (!machinePanel.gameObject.activeSelf || !bridge.IsRunning || building == null || building.kind != kind || player == null ||
                !player.connected || player.hp <= 0f || player.interactCooldown > 0f ||
                !LowVoltageCatalog.IsBuildUnlocked(bridge.World, kind)) return;
            if (kind == BuildKind.FilterSplitter && (arg < 0 || arg >= DemoCatalog.ItemCount ||
                bridge.World.cargo != null && bridge.World.cargo.Exists(c => c.fromId == building.id || c.toId == building.id))) return;
            if (kind == BuildKind.CircuitBreaker && arg != 0 && arg != 1) return;
            SendInspected("configure", arg);
        }
    }
}
