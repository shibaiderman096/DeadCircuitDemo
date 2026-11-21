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
            petrochemicalHeading = FlowLabel("Petrochemical Research Heading", content, "中压(MV)石化工艺 · 勘探、采油、分馏与冷却循环", 768, 30, 18, Accent);
            petrochemicalGuide = FlowLabel("Petrochemical Startup Guide", content, "", 768, 112, 15, Ink);
            petrochemicalSurveyRow = FlowRow("Oil Prospecting Actions", content, 768, 132);
            petrochemicalSurveyState = Label("Oil Prospecting Result", petrochemicalSurveyRow, "", 8, 6, 562, 120, 15, Ink);
            petrochemicalSurveyState.enableAutoSizing = false;
            petrochemicalSurveyState.alignment = TextAlignmentOptions.TopLeft;
            petrochemicalSurvey = Button("Prospect Oil", petrochemicalSurveyRow, "勘探油源", 588, 12, 170, 64, ProspectOil);
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
                    player.interactCooldown > 0f ? "交互冷却中" : world.era < TechEra.MediumVoltage ? "先进入中压(MV)时代" : can ? "可研究" : "条件未满足";
                string reason = known ? "全队已完成" : !ready ? state :
                    !string.IsNullOrEmpty(requirement) ? requirement : can ? "已满足，可研究" : "正在同步研究条件，请稍候";
                Set(row.details, definition.name + " · " + state + "\n" + definition.description +
                    "\n前置研究：" + PetrochemicalPrerequisites(world, definition.prerequisites) +
                    "\n研究消耗（持有 / 需要）：" + MaterialText(definition.cost, player.items, true) +
                    "\n生产进度：" + (string.IsNullOrEmpty(evidence) ? "已满足" : evidence) +
                    "\n" + (known || can ? "研究状态：" : "不可研究：") + reason);
                row.details.color = known ? Accent : Ink;
                ResizePetrochemicalRow(row.root, row.details, 164);
                row.research.button.interactable = can;
                Set(row.research.label, known ? "已研究" : can ? "研究" + definition.name : state + "\n查看条件");
                Tint(row.research, known ? Selected : Raised);
            }
            if (!visible) return;

            bool entered = world.era >= TechEra.MediumVoltage;
            SetFlowText(technologyDescription, "中压(MV)石化：完成8项工艺研究。先用低压(LV)电网采油、分馏与加工原料，再以柴油发电，发展中压(MV)生产线和冷却循环。", 56);
            string rules = "研究消耗背包材料，全队共享成果。需靠近2.4格内、视线畅通的工作台；不消耗角色技能点。";
            SetFlowText(researchMaterials, entered ? rules : "先完成低压(LV)研究与生产条件，再研究中压(MV)时代。时代研究消耗：" +
                MaterialText(TechCatalog.ResearchCost(TechEra.MediumVoltage), player.items, true) + "\n" + rules, 52);
            if (entered)
            {
                SetFlowText(researchRequirement, "完成下列8项石化研究，并达到采油、管输、分馏、柴油、聚合、润滑、冷却回收与中压(MV)加工进度后，可在高压(HV)页研究下一时代。", 48);
                researchRequirement.color = Accent;
                researchButton.button.interactable = false;
                Set(researchButton.label, "在下方选择研究");
            }
            SetFlowText(petrochemicalGuide, "起步：勘探油源 → 抽油输液 → 原油分馏 → 燃料精制 → 聚合、润滑 → 中压变电 → 冷却循环。" +
                "\n沙漠、湿地、沼泽与河谷更容易发现油源。勘探搜索48格内已生成区域的剩余油源；未发现时，继续探索后再试。" +
                "\n起步设备使用低压(LV)供电：抽油机20 kW、分馏装置40 kW、基础化学反应器40 kW、工艺取水泵10 kW。分馏装置与基础化学反应器需靠近水源冷却。" +
                "\n液体管道与储液罐仅输送选定液体。清空库存、等待进出液体全部送达后，可在设备详情中更改液体类型。箭头指向下游；按 E 取出液体，空置时从背包补入最多20单位。" +
                "\n研究中压变电后，柴油发电机消耗柴油发电。化工厂需中压(MV) 80 kW供电，生产聚合物并排出热冷却液；冷却塔将其回收为冷却液。", 112);

            bool surveyed = PetrochemicalCatalog.HasResearch(world, PetrochemicalTech.Prospecting);
            petrochemicalSurvey.button.interactable = entered && surveyed && ready && Time.unscaledTime >= petrochemicalSurveySendAt;
            Set(petrochemicalSurvey.label, !entered ? "先进入中压(MV)时代" : !surveyed ? "先研究石油勘探" : !ready ? "暂不能勘探" : "勘探48格内油源");
            Set(petrochemicalSurveyState, "油源勘探 · " + SteamProgress("成功勘探", world.mvSurvey, 1) +
                "\n勘探周围48格内已知区域的油藏，显示最近油藏的坐标、方位和储量。" +
                "\n常见区域：荒漠、湿地、沼泽、河谷。" +
                "\n" + (player.noticeUntil > world.elapsed && !string.IsNullOrWhiteSpace(player.notice) ? player.notice : "研究原油勘探后点击右侧按钮。若未发现油源，可继续探索后再试。"));
            ResizePetrochemicalRow(petrochemicalSurveyRow, petrochemicalSurveyState, 132);

            int researched = 0;
            foreach (var definition in PetrochemicalCatalog.All)
                if (PetrochemicalCatalog.HasResearch(world, definition.id)) researched++;
            string highVoltage = PetrochemicalCatalog.HighVoltageRequirement(world);
            SetFlowText(petrochemicalProduction, "高压(HV)时代条件 · 全队累计生产与运输进度" +
                "\n石化研究 " + researched + "/8 · 缺 " + (PetrochemicalCatalog.All.Length - researched) + " 项 · " + SteamProgress("勘探", world.mvSurvey, 1) +
                "\n" + SteamProgress("抽取原油", world.mvExtracted, 12) + " · " + SteamProgress("管输送达", world.mvPiped, 8) +
                "\n" + SteamProgress("分馏原油", world.mvDistilled, 12) + " · " + SteamProgress("柴油", world.mvDiesel, 4) +
                "\n" + SteamProgress("聚合物", world.mvPolymer, 4) + " · " + SteamProgress("润滑油", world.mvLubricant, 2) +
                "\n" + SteamProgress("冷却液回收", world.mvCooled, 4) + " · " + SteamProgress("中压(MV)聚合物产量", world.mvPowered, 4) +
                "\n生产进度累计已完成的产出；管输进度仅计液体经管道或储液罐送达。持有、手工搬运物品或设备空转均不计入。" +
                "\n高压(HV)研究条件：" + (string.IsNullOrEmpty(highVoltage) ? "生产条件已满足；前往高压(HV)页查看研究消耗与其余条件。" : highVoltage), 180);
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
            return names.Count == 0 ? "完成低压(LV)研究与生产条件，进入中压(MV)时代" : string.Join("、", names);
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
            text.Append("石化配方 · 在对应设备详情中切换配方");
            foreach (var recipe in TechCatalog.Recipes)
            {
                if (!IsPetrochemicalRecipe(recipe.id)) continue;
                string requirement = PetrochemicalCatalog.RecipeRequirement(world, recipe);
                text.Append("\n\n").Append(recipe.name).Append(" · ")
                    .Append(DemoCatalog.BuildName(recipe.building)).Append(" · ").Append(recipe.duration.ToString("0.#")).Append("秒")
                    .Append("\n消耗：").Append(MaterialText(recipe.inputs)).Append(" → 产出：").Append(MaterialText(recipe.outputs))
                    .Append("\n").Append(string.IsNullOrEmpty(requirement) ? "工艺已解锁；接通对应电压的电网、装入原料，并为所有产物留出空间。" : requirement);
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
            petrochemicalFluidPrevious = Button("Previous Fluid", petrochemicalFluidButtons, "上一种液体", 0, 0, 124, 34, () => CyclePetrochemicalFluid(-1));
            petrochemicalFluidNext = Button("Next Fluid", petrochemicalFluidButtons, "下一种液体", 130, 0, 124, 34, () => CyclePetrochemicalFluid(1));
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
                SetFlowText(machineContents, "油源无法徒手或用镐开采。\n研究抽油输液，在油源旁建造抽油机，接通低压(LV) 20 kW供电。");
                SetFlowText(machineHint, "抽油机输出原油。将液体管道、储液罐设为原油，箭头指向下游。\n沙漠、湿地、沼泽与河谷更容易发现油源。在 T → 中压(MV)中可勘探48格内的油源。");
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
                string reason = !unlocked ? "先进入中压(MV)时代并研究抽油输液。" : !reachable ? "请靠近2.4格内并保持视线畅通，再更改液体类型。" : !empty ? "设备中仍有液体，请先清空再更改类型。" :
                    cargo ? "液体正在运输，请等待全部送达后再更改类型。" : !ready ? "角色尚未就绪或交互冷却未结束，请稍候。" : "设备已清空且无在途液体，可以更改液体类型。";
                petrochemicalFluidPrevious.button.interactable = petrochemicalFluidNext.button.interactable = ready && empty && !cargo;
                SetFlowText(petrochemicalMachineState, "输送液体：" + DemoCatalog.ItemName(fluid) + "（" + selection + " / 0–8）" +
                    "\n设备×" + Count(building.items, (int)fluid) + " · 背包×" + Count(player.items, (int)fluid) +
                    "\n沿箭头输送选定液体；不同液体需使用独立管线。" +
                    "\n按 E 取出液体；空置时从背包补入最多20单位。" +
                    "\n" + reason, 76);
                petrochemicalMachineState.color = !empty || cargo ? Amber : Ink;
            }
            else
            {
                string requirement = PetrochemicalCatalog.BuildRequirement(world, building.kind);
                if (string.IsNullOrEmpty(requirement) && TechCatalog.RequiresWater(building.kind) && !TechCatalog.NearWater(world, building))
                    requirement = "距水源过远，无法取水或冷却；请将设备建在水源附近。";
                if (string.IsNullOrEmpty(requirement) && DemoCatalog.Demand(building.kind) > 0f && (!building.powered || building.powerRatio <= 0f))
                    requirement = "供电不足，请检查所需电压、线路连接与发电余量。";
                SetFlowText(petrochemicalMachineState, PetrochemicalMachineGuide(building) +
                    (string.IsNullOrEmpty(requirement) ? "" : "\n无法运行：" + requirement), 76);
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
                case BuildKind.OilPump: return "抽油机 · 低压(LV) 20 kW\n在油源旁开采原油；未邻接油源或油源耗尽时停产。原油管道接在输出箭头处。";
                case BuildKind.ProcessWaterPump: return "工艺取水泵 · 低压(LV) 10 kW\n靠近水源抽取工艺水。将管道设为工艺水，输送至基础化学反应器或冷却塔。";
                case BuildKind.DistillationUnit: return "分馏装置 · 低压(LV) 40 kW，需靠近水源冷却\n配方161：原油×4＋燃料×1 → 轻质馏分×2＋化工原料×1＋重质馏分×1。各液体需独立输出管线；任一产物无处存放都会停产。";
                case BuildKind.ChemicalReactor: return "基础化学反应器 · 低压(LV) 40 kW，需靠近水源冷却\n配方70/71：聚合物、冷却液；160：化工原料；162/163：润滑油、燃料；165：柴油。在设备详情中选择配方，并为不同液体设置独立的输入与输出管线。";
                case BuildKind.ChemicalPlant: return "化工厂 · 中压(MV) 80 kW\n配方164：化工原料×4＋冷却液×1 → 聚合物×4＋热冷却液×1。需持续供给冷却液、排出热冷却液；完成的聚合物产出计入中压(MV)加工进度。";
                case BuildKind.CoolingTower: return "冷却塔 · 中压(MV) 60 kW，需靠近水源\n配方91：热冷却液×2＋工艺水×1 → 冷却液×2。连接热冷却液、工艺水输入管与冷却液输出管；回收的冷却液计入生产进度。";
                case BuildKind.DieselGenerator: return "柴油发电机 · 研究中压变电后启用\n消耗柴油发电，普通燃料不能替代。配方165将轻质馏分×2精炼为柴油×2，再供给发电机。";
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
