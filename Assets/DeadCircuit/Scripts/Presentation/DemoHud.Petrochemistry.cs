using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class DemoHud
    {
        readonly List<PrimitiveResearchRow> petrochemicalResearchRows = new List<PrimitiveResearchRow>();
        TextMeshProUGUI petrochemicalHeading, petrochemicalGuide, petrochemicalProduction, petrochemicalRecipes, petrochemicalSurveyState;
        RectTransform petrochemicalSurveyRow, petrochemicalMachineControls, petrochemicalFluidButtons;
        TextMeshProUGUI petrochemicalMachineState;
        UiButton petrochemicalSurvey, petrochemicalFluidPrevious, petrochemicalFluidNext;
        float petrochemicalSurveySendAt;

        void MakePetrochemistryTechnology(RectTransform content)
        {
            petrochemicalHeading = FlowLabel("Petrochemical Research Heading", content, "MV石化工艺 · 勘探、采油、分馏与冷却循环", 768, 30, 18, Accent);
            petrochemicalGuide = FlowLabel("Petrochemical Startup Guide", content, "", 768, 112, 15, Ink);
            petrochemicalSurveyRow = FlowRow("Oil Prospecting Actions", content, 768, 132);
            petrochemicalSurveyState = Label("Oil Prospecting Result", petrochemicalSurveyRow, "", 8, 6, 562, 120, 15, Ink);
            petrochemicalSurveyState.enableAutoSizing = false;
            petrochemicalSurveyState.alignment = TextAlignmentOptions.TopLeft;
            petrochemicalSurvey = Button("Prospect Oil", petrochemicalSurveyRow, "勘探附近油源", 588, 12, 170, 64, ProspectOil);
            SetButtonArt(petrochemicalSurvey, "ui_research", 22);
            foreach (var definition in PetrochemicalCatalog.All)
            {
                int id = (int)definition.id;
                RectTransform root = FlowRow("Petrochemical Research Row " + id, content, 768, 164);
                var details = Label("Petrochemical Research Details " + id, root, "", 8, 6, 562, 152, 15, Ink);
                details.enableAutoSizing = false;
                details.alignment = TextAlignmentOptions.TopLeft;
                UiButton button = Button("Petrochemical Research " + id, root, "", 588, 12, 170, 64, () => ResearchPetrochemistry(id));
                SetButtonArt(button, "ui_research", 22);
                petrochemicalResearchRows.Add(new PrimitiveResearchRow { id = id, root = root, details = details, research = button });
            }
            petrochemicalProduction = FlowLabel("High Voltage Petrochemical Acceptance", content, "", 768, 180, 15, Ink);
            petrochemicalRecipes = FlowLabel("Petrochemical Recipe Guide", content, "", 768, 128, 15, Ink);
        }

        void RefreshPetrochemistryTechnology(WorldState world, PlayerState player)
        {
            bool visible = selectedEra == 4;
            petrochemicalHeading.gameObject.SetActive(visible);
            petrochemicalGuide.gameObject.SetActive(visible);
            petrochemicalSurveyRow.gameObject.SetActive(visible);
            petrochemicalProduction.gameObject.SetActive(visible);
            petrochemicalRecipes.gameObject.SetActive(visible);
            bool ready = bridge.IsRunning && player.connected && player.hp > 0f && player.interactCooldown <= 0f;
            foreach (var row in petrochemicalResearchRows)
            {
                row.root.gameObject.SetActive(visible);
                if (!visible) continue;
                var definition = PetrochemicalCatalog.Definition(row.id);
                bool known = PetrochemicalCatalog.HasResearch(world, row.id);
                bool can = !known && ready && PetrochemicalCatalog.CanResearch(world, player, row.id);
                string requirement = PetrochemicalCatalog.ResearchRequirement(world, player, row.id);
                string evidence = PetrochemicalCatalog.ResearchEvidence(world, definition.id);
                string state = known ? "已研究" : !bridge.IsRunning || !player.connected || player.hp <= 0f ? "角色尚未就绪" :
                    player.interactCooldown > 0f ? "交互冷却中" : world.era < TechEra.MediumVoltage ? "先进入MV中压时代" : can ? "可研究" : "条件未满足";
                string reason = known ? "全队已完成" : !ready ? state :
                    !string.IsNullOrEmpty(requirement) ? requirement : can ? "已满足，可研究" : "等待主机同步研究条件";
                Set(row.details, definition.name + " · " + state + "\n" + definition.description +
                    "\n前置：" + PetrochemicalPrerequisites(world, definition.prerequisites) +
                    "\n样品 / 扣料（随身持有 / 需求）：" + MaterialText(definition.cost, player.items, true) +
                    "\n真实生产：" + (string.IsNullOrEmpty(evidence) ? "已满足" : evidence) +
                    "\n" + (known || can ? "研究状态：" : "不可研究：") + reason);
                row.details.color = known ? Accent : Ink;
                ResizePetrochemicalRow(row.root, row.details, 164);
                row.research.button.interactable = can;
                Set(row.research.label, known ? "已研究" : can ? "研究" + definition.name : state + "\n查看左侧原因");
                Tint(row.research, known ? Selected : Raised);
            }
            if (!visible) return;

            bool entered = world.era >= TechEra.MediumVoltage;
            SetFlowText(technologyDescription, "MV中压石化：完成8项工艺研究；首批采油、分馏与基础反应使用LV电网，再用真实柴油启动MV扩产和冷却循环。", 56);
            string rules = "研究扣除研究者随身背包的真实样品，全队共享；需要2.4格内、视线畅通的工作台，不消耗角色技能点。";
            SetFlowText(researchMaterials, entered ? rules : "先完成LV验收并进入MV时代；晋级材料：" +
                MaterialText(TechCatalog.ResearchCost(TechEra.MediumVoltage), player.items, true) + "\n" + rules, 52);
            if (entered)
            {
                SetFlowText(researchRequirement, "逐项研究下列8种石化工艺。完成采油、管输、分馏、柴油、聚合、润滑、冷却回收与MV加工验收后，切换高压页晋级。", 48);
                researchRequirement.color = Accent;
                researchButton.button.interactable = false;
                Set(researchButton.label, "在节点中研究8项石化工艺");
            }
            SetFlowText(petrochemicalGuide, "启动顺序：勘探油源 → 采油与管输 → 分馏 → 柴油精炼 → 聚合、润滑 → 中压变电 → 冷却循环。" +
                "\n油源倾向沙漠、湿地、沼泽与河谷；勘探仅查询附近48格已生成的油源，未找到时继续探索、生成地形后再试。" +
                "\n首批设备接LV：采油泵20 kW、分馏装置40 kW、基础化学反应器40 kW、工艺水泵10 kW；分馏和基础化工需近水被动冷却。" +
                "\n管道与储罐只传所选液种；检查面板选液，存量与进出在途货物必须全空。箭头指向下游，E有货先取、空时从背包补20。" +
                "\n柴油发电机真实消耗柴油，研究中压变电后启用；化工厂接MV 80 kW扩产并产热冷却液，冷却塔回收热液。", 112);

            bool surveyed = PetrochemicalCatalog.HasResearch(world, PetrochemicalTech.Prospecting);
            petrochemicalSurvey.button.interactable = entered && surveyed && ready && Time.unscaledTime >= petrochemicalSurveySendAt;
            Set(petrochemicalSurvey.label, !entered ? "先进入MV时代" : !surveyed ? "先研究油源勘探" : !ready ? "角色或交互未就绪" : "勘探附近48格");
            Set(petrochemicalSurveyState, "油源勘探 · " + SteamProgress("成功勘探", world.mvSurvey, 1) +
                "\n范围：附近48格已生成油源；返回油源坐标及余量，以主机反馈为准。" +
                "\n群系倾向：沙漠 / 湿地 / 沼泽 / 河谷。" +
                "\n" + (player.noticeUntil > world.elapsed && !string.IsNullOrWhiteSpace(player.notice) ? player.notice : "研究勘探后点击右侧按钮；继续探索可发现新的油源。"));
            ResizePetrochemicalRow(petrochemicalSurveyRow, petrochemicalSurveyState, 132);

            int researched = 0;
            foreach (var definition in PetrochemicalCatalog.All)
                if (PetrochemicalCatalog.HasResearch(world, definition.id)) researched++;
            string highVoltage = PetrochemicalCatalog.HighVoltageRequirement(world);
            SetFlowText(petrochemicalProduction, "HV高压入口 · 全队真实生产验收" +
                "\n石化研究 " + researched + "/8 · 缺 " + (PetrochemicalCatalog.All.Length - researched) + " 项 · " + SteamProgress("勘探", world.mvSurvey, 1) +
                "\n" + SteamProgress("采油", world.mvExtracted, 12) + " · " + SteamProgress("管输到货", world.mvPiped, 8) +
                "\n" + SteamProgress("分馏", world.mvDistilled, 12) + " · " + SteamProgress("柴油", world.mvDiesel, 4) +
                "\n" + SteamProgress("聚合物", world.mvPolymer, 4) + " · " + SteamProgress("润滑油", world.mvLubricant, 2) +
                "\n" + SteamProgress("冷却回收", world.mvCooled, 4) + " · " + SteamProgress("MV加工", world.mvPowered, 4) +
                "\n只计真实生产与液体管输到货；背包持有、手工搬运、摆放设备和空转不算验收。" +
                "\n高压工艺前置：" + (string.IsNullOrEmpty(highVoltage) ? "已满足；切换高压页查看晋级扣料及其余条件。" : highVoltage), 180);
            petrochemicalProduction.color = string.IsNullOrEmpty(highVoltage) ? Accent : Ink;
            RefreshPetrochemicalRecipes(world);
        }

        static string PetrochemicalPrerequisites(WorldState world, int mask)
        {
            var names = new List<string>();
            foreach (var parent in PetrochemicalCatalog.All)
            {
                int id = (int)parent.id;
                if ((mask & (1 << id)) != 0)
                    names.Add(parent.name + (PetrochemicalCatalog.HasResearch(world, id) ? "（已研究）" : "（未研究）"));
            }
            return names.Count == 0 ? "LV阶段验收及进入MV中压时代" : string.Join("、", names);
        }

        static void ResizePetrochemicalRow(RectTransform root, TextMeshProUGUI details, float minimum)
        {
            float height = Mathf.Max(minimum, details.GetPreferredValues(details.text, 562, float.PositiveInfinity).y + 16);
            details.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height - 12);
            root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
        }

        static bool IsPetrochemicalRecipe(int id) => id == 70 || id == 71 || id == 91 || id >= 160 && id <= 165;

        void RefreshPetrochemicalRecipes(WorldState world)
        {
            var text = new StringBuilder(2048);
            text.Append("石化配方速查 · 在对应设备检查中切换；输入、产出均为真实物品");
            foreach (var recipe in TechCatalog.Recipes)
            {
                if (!IsPetrochemicalRecipe(recipe.id)) continue;
                string requirement = PetrochemicalCatalog.RecipeRequirement(world, recipe);
                text.Append("\n\n").Append(recipe.name).Append("（").Append(recipe.id).Append("） · ")
                    .Append(DemoCatalog.BuildName(recipe.building)).Append(" · ").Append(recipe.duration.ToString("0.#")).Append("秒")
                    .Append("\n设备内输入：").Append(MaterialText(recipe.inputs)).Append(" → 产出：").Append(MaterialText(recipe.outputs))
                    .Append("\n").Append(string.IsNullOrEmpty(requirement) ? "工艺已解锁；接通对应电压、供料并保证输出空间。" : requirement);
            }
            SetFlowText(petrochemicalRecipes, text.ToString(), 128);
        }

        void ResearchPetrochemistry(int id)
        {
            var world = bridge.World;
            var player = bridge.LocalPlayer;
            if (!technology.gameObject.activeSelf || selectedEra != 4 || !bridge.IsRunning || world == null || player == null ||
                !player.connected || player.hp <= 0f || player.interactCooldown > 0f ||
                PetrochemicalCatalog.HasResearch(world, id) || !PetrochemicalCatalog.CanResearch(world, player, id)) return;
            Send("mv", id);
        }

        void ProspectOil()
        {
            var world = bridge.World;
            var player = bridge.LocalPlayer;
            if (!technology.gameObject.activeSelf || selectedEra != 4 || !bridge.IsRunning || world == null || player == null ||
                !player.connected || player.hp <= 0f || player.interactCooldown > 0f || Time.unscaledTime < petrochemicalSurveySendAt ||
                world.era < TechEra.MediumVoltage || !PetrochemicalCatalog.HasResearch(world, PetrochemicalTech.Prospecting)) return;
            petrochemicalSurveySendAt = Time.unscaledTime + .4f;
            Send("prospect");
        }

        void MakePetrochemistryMachineControls(RectTransform content)
        {
            petrochemicalMachineControls = FlowRow("Petrochemical Machine Controls", content, 260, 120);
            var layout = petrochemicalMachineControls.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            petrochemicalMachineState = FlowLabel("Petrochemical Machine Configuration", petrochemicalMachineControls, "", 260, 76, 13, Ink);
            petrochemicalFluidButtons = FlowRow("Fluid Selection Buttons", petrochemicalMachineControls, 260, 38);
            petrochemicalFluidPrevious = Button("Previous Fluid", petrochemicalFluidButtons, "上一液种", 0, 0, 124, 34, () => CyclePetrochemicalFluid(-1));
            petrochemicalFluidNext = Button("Next Fluid", petrochemicalFluidButtons, "下一液种", 130, 0, 124, 34, () => CyclePetrochemicalFluid(1));
            petrochemicalMachineControls.gameObject.SetActive(false);
        }

        // Oil is handled here before the legacy resource inspector indexes its fixed name table.
        bool RefreshPetrochemistryMachineControls(WorldState world, PlayerState player, BuildingState building, NodeState node, bool inRange)
        {
            bool oil = building == null && node != null && node.kind == NodeKind.Oil;
            bool liquid = building != null && (building.kind == BuildKind.LiquidPipe || building.kind == BuildKind.LiquidTank);
            bool machine = building != null && (liquid || building.kind == BuildKind.OilPump || building.kind == BuildKind.DistillationUnit ||
                building.kind == BuildKind.ChemicalReactor || building.kind == BuildKind.ProcessWaterPump ||
                building.kind == BuildKind.DieselGenerator || building.kind == BuildKind.ChemicalPlant || building.kind == BuildKind.CoolingTower);
            petrochemicalMachineControls.gameObject.SetActive(machine);
            petrochemicalFluidButtons.gameObject.SetActive(liquid);
            if (oil)
            {
                Set(machineTitle, "油源  " + CellLocationText(world, node.x, node.y));
                machineTitle.color = Accent;
                machineRecipe.gameObject.SetActive(false);
                machineRecipeFlow.gameObject.SetActive(false);
                machineSafety.gameObject.SetActive(false);
                machineCluster.gameObject.SetActive(false);
                SetFlowText(machineInfo, "油源余量：" + node.stock + "\n所在区域：" + OutdoorRegionText(world, node.x, node.y));
                SetFlowText(machineContents, "油源不能用空手或镐开采。\n研究采油与管输，紧邻油源放置采油泵，接通LV 20 kW。");
                SetFlowText(machineHint, "原油由采油泵输出；液体管道 / 储罐选原油，箭头指向下游。\n沙漠、湿地、沼泽与河谷更容易找到油源；T → MV可勘探附近48格。");
                return true;
            }
            if (!machine) return false;
            if (liquid)
            {
                int selection = Mathf.Clamp(building.setting, 0, WorldSimulator.FluidCount - 1);
                ItemKind fluid = (ItemKind)WorldSimulator.FluidItem(selection);
                bool empty = true;
                if (building.items != null) foreach (int amount in building.items) if (amount > 0) { empty = false; break; }
                bool cargo = HasPetrochemicalCargo(world, building);
                bool reachable = inRange && SurvivalInReach(world, player, inspectedX, inspectedY) && MapExploration.IsVisible(world, player, inspectedX, inspectedY);
                bool unlocked = world.era >= TechEra.MediumVoltage && PetrochemicalCatalog.HasResearch(world, PetrochemicalTech.Extraction);
                bool ready = bridge.IsRunning && unlocked && reachable && player.connected && player.hp > 0f && player.interactCooldown <= 0f;
                string reason = !unlocked ? "先进入MV时代并研究抽油输液。" : !reachable ? "靠近2.4格内并保持视线畅通后可选液。" : !empty ? "设备有存量，须先取空才能改液种。" :
                    cargo ? "有进出在途货物；全部到货后才能改液种。" : !ready ? "角色或交互尚未就绪。" : "空存量且无进出货物，可以改液种。";
                petrochemicalFluidPrevious.button.interactable = petrochemicalFluidNext.button.interactable = ready && empty && !cargo;
                SetFlowText(petrochemicalMachineState, "所选液种：" + DemoCatalog.ItemName(fluid) + "（" + selection + " / 0–8）" +
                    "\n设备 " + Count(building.items, (int)fluid) + " · 背包 " + Count(player.items, (int)fluid) +
                    "\n沿箭头向下游输送所选液种；不同液体分管，不混线。" +
                    "\nE：有货先取所选液体；空时从背包补入最多20。" +
                    "\n" + reason, 76);
                petrochemicalMachineState.color = !empty || cargo ? Amber : Ink;
            }
            else
            {
                string requirement = PetrochemicalCatalog.BuildRequirement(world, building.kind);
                if (string.IsNullOrEmpty(requirement) && TechCatalog.RequiresWater(building.kind) && !TechCatalog.NearWater(world, building))
                    requirement = "需要靠近水源，当前取水 / 被动冷却条件未满足。";
                if (string.IsNullOrEmpty(requirement) && DemoCatalog.Demand(building.kind) > 0f && (!building.powered || building.powerRatio <= 0f))
                    requirement = "当前缺少对应电压的供电；请检查线路与电网余量。";
                SetFlowText(petrochemicalMachineState, PetrochemicalMachineGuide(building) +
                    (string.IsNullOrEmpty(requirement) ? "" : "\n停机条件：" + requirement), 76);
                petrochemicalMachineState.color = building.powerDemand > 0f && building.powerRatio < .99f ? Amber : Ink;
            }
            petrochemicalMachineControls.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight =
                petrochemicalMachineState.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight + (liquid ? 44 : 0);
            return false;
        }

        static bool HasPetrochemicalCargo(WorldState world, BuildingState building) =>
            world.cargo != null && world.cargo.Exists(c => c.fromId == building.id || c.toId == building.id);

        static string PetrochemicalMachineGuide(BuildingState building)
        {
            switch (building.kind)
            {
                case BuildKind.OilPump: return "采油泵 · LV 20 kW\n紧邻油源开采原油；无油源或油源耗尽会停产。原油管道接输出箭头方向。";
                case BuildKind.ProcessWaterPump: return "工艺水泵 · LV 10 kW\n靠近水源取工艺水；用所选液种为工艺水的管道送入基础反应器或冷却塔。";
                case BuildKind.DistillationUnit: return "分馏装置 · LV 40 kW，近水被动冷却\n161：原油4+燃料1→轻馏分2+化工原料1+重油1。为各液体分别配置输出管，任一产物堵塞都会影响生产。";
                case BuildKind.ChemicalReactor: return "基础化学反应器 · LV 40 kW，近水被动冷却\n70/71：聚合物与冷却液；160：化工原料；162/163：润滑油与燃料；165：柴油。检查配方并为不同液体分管供料、收货。";
                case BuildKind.ChemicalPlant: return "中压化工厂 · MV 80 kW\n164：化工原料4+冷却液1→聚合物4+热冷却液1。冷却液需持续供给，热液需输出回收；完成加工才计MV验收。";
                case BuildKind.CoolingTower: return "冷却塔 · 近水回收热液\n91：热冷却液2+工艺水1→冷却液2；连接热液、工艺水输入与冷却液输出，真实回收才计验收。";
                case BuildKind.DieselGenerator: return "柴油发电机 · 研究中压变电后启用\n真实消耗柴油，普通燃料不能替代。轻馏分2经配方165精炼为柴油2，再供给发电机。";
                default: return DemoCatalog.BuildName(building.kind);
            }
        }

        void CyclePetrochemicalFluid(int direction)
        {
            BuildingState building = InspectedBuilding();
            PlayerState player = bridge.LocalPlayer;
            WorldState world = bridge.World;
            if (!bridge.IsRunning || building == null || world == null || player == null || building.hp <= 0f ||
                building.kind != BuildKind.LiquidPipe && building.kind != BuildKind.LiquidTank ||
                world.era < TechEra.MediumVoltage || !PetrochemicalCatalog.HasResearch(world, PetrochemicalTech.Extraction) ||
                !player.connected || player.hp <= 0f || player.interactCooldown > 0f ||
                !SurvivalInReach(world, player, inspectedX, inspectedY) || !MapExploration.IsVisible(world, player, inspectedX, inspectedY) ||
                HasPetrochemicalCargo(world, building)) return;
            if (building.items != null) foreach (int amount in building.items) if (amount > 0) return;
            int selection = (Mathf.Clamp(building.setting, 0, WorldSimulator.FluidCount - 1) + direction + WorldSimulator.FluidCount) % WorldSimulator.FluidCount;
            SendInspected("configure", selection);
        }
    }
}
