using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class DemoHud
    {
        readonly List<(int id, UiButton button)> precisionResearchButtons = new List<(int, UiButton)>();
        RectTransform precisionTechnology, precisionAssemblerActions;
        TextMeshProUGUI precisionHeading, precisionDetails, precisionNuclear, precisionAssemblerState;
        UiButton precisionMode;
        int selectedPrecision = (int)PrecisionTech.Silicon;

        void MakePrecisionTechnology(RectTransform content)
        {
            precisionTechnology = PrecisionColumn("Precision Technology", content, 768);
            precisionHeading = FlowLabel("Precision Heading", precisionTechnology, "", 768, 28, 17, Accent);
            RectTransform row = null;
            for (int i = 0; i < PrecisionCatalog.All.Length; i++)
            {
                if (i % 4 == 0) row = FlowRow("Precision Research Row " + i / 4, precisionTechnology, 768, 42);
                int id = (int)PrecisionCatalog.All[i].id;
                UiButton button = Button("Precision Node " + id, row, "", i % 4 * 192, 0, 184, 42, () =>
                {
                    selectedPrecision = id;
                    RefreshAll();
                });
                precisionResearchButtons.Add((id, button));
            }
            precisionDetails = FlowLabel("Selected Precision Research", precisionTechnology, "", 768, 102, 15, Ink);
            precisionNuclear = FlowLabel("Nuclear Acceptance", precisionTechnology, "", 768, 50, 15, Ink);
            RectTransform entry = FlowRow("Nuclear Entry Row", precisionTechnology, 768, 38);
            Button("Browse Nuclear Era", entry, "查看核时代", 0, 0, 180, 38, () => BrowseEra((int)TechEra.Nuclear));
            precisionTechnology.gameObject.SetActive(false);
        }

        void RefreshPrecisionTechnology(WorldState world, PlayerState player)
        {
            bool visible = selectedEra == (int)TechEra.HighVoltage;
            precisionTechnology.gameObject.SetActive(visible);
            if (!visible) return;

            bool entered = world.era >= TechEra.HighVoltage;
            bool ready = PrecisionActionReady(player);
            var definition = PrecisionCatalog.Definition(selectedPrecision);
            bool known = PrecisionCatalog.HasResearch(world, selectedPrecision);
            string requirement = known || !entered ? "" : PrecisionCatalog.ResearchRequirement(world, player, selectedPrecision);
            bool canResearch = entered && !known && ready && string.IsNullOrEmpty(requirement);
            int completed = 0;
            foreach (var node in precisionResearchButtons)
            {
                bool unlocked = PrecisionCatalog.HasResearch(world, node.id);
                if (unlocked) completed++;
                bool selected = node.id == selectedPrecision;
                Set(node.button.label, PrecisionCatalog.Definition(node.id).name + "\n" + (unlocked ? "已研究" : entered ? "未研究" : "待进入高压"));
                Tint(node.button, selected ? Selected : Raised);
                node.button.label.color = selected || unlocked ? Accent : Ink;
            }

            SetFlowText(technologyDescription, "HV高压精密工业：从石英、硅材和晶圆到半导体、高级电路与控制模块；耐热合金和自动控制支撑高压电网。", 56);
            if (entered)
            {
                SetFlowText(researchMaterials, "选中：" + definition.name + " · 样品：" + MaterialText(definition.cost, player.items, true), 28);
                string status = known ? "全队已完成" : !string.IsNullOrEmpty(requirement) ? requirement :
                    canResearch ? "条件已满足，可研究。" : player.interactCooldown > 0f ? "交互冷却中。" : "角色尚未就绪。";
                SetFlowText(researchRequirement, status, 30);
                researchRequirement.color = known || canResearch ? Accent : Amber;
                researchButton.button.interactable = canResearch;
                Set(researchButton.label, known ? "该节点已研究" : canResearch ? "研究" + definition.name : "节点研究条件未满足");
            }

            SetFlowText(precisionHeading, "精密工业研究 · " + completed + "/" + PrecisionCatalog.All.Length, 28);
            string evidence = PrecisionCatalog.ResearchEvidence(world, definition.id);
            SetFlowText(precisionDetails, definition.name + " · " + (known ? "已研究" : "未研究") + "\n" + definition.description +
                "\n前置研究：" + PrecisionPrerequisites(world, definition.prerequisites) +
                (entered ? "" : "\n随身样品：" + MaterialText(definition.cost, player.items, true)) +
                "\n生产证据：" + (string.IsNullOrEmpty(evidence) ? "无缺口" : evidence), 102);

            string nuclear = world.era >= TechEra.Nuclear ? "已进入核时代。" : !entered ? "先完成MV验收并进入HV高压时代。" :
                EraResearchRequirement(world, TechEra.Nuclear);
            SetFlowText(precisionNuclear, "核时代入口" +
                "\n高压控制模块 " + world.hvPowered + "/4 · 控制模块总产 " + Count(world.produced, (int)ItemKind.ControlModule) +
                "/4 · 耐热合金 " + Count(world.produced, (int)ItemKind.HeatAlloy) + "/6" +
                "\n精密装配机：" + (HasBuilding(world, BuildKind.PrecisionAssembler) ? "已建成且未损毁" : "未建成或已损毁") +
                "\n晋级扣料：" + MaterialText(TechCatalog.ResearchCost(TechEra.Nuclear), player.items, true) +
                "\n" + (string.IsNullOrEmpty(nuclear) ? "验收已通过；在核时代页晋级。" : nuclear), 50);
            precisionNuclear.color = entered && (world.era >= TechEra.Nuclear || string.IsNullOrEmpty(nuclear)) ? Accent : Ink;
        }

        static string PrecisionPrerequisites(WorldState world, int mask)
        {
            var names = new List<string>();
            foreach (var parent in PrecisionCatalog.All)
                if ((mask & (1 << (int)parent.id)) != 0)
                    names.Add(parent.name + (PrecisionCatalog.HasResearch(world, parent.id) ? "（已研究）" : "（未研究）"));
            return names.Count == 0 ? "进入HV高压时代" : string.Join("、", names);
        }

        bool PrecisionActionReady(PlayerState player) =>
            bridge.IsRunning && player != null && player.connected && player.hp > 0f && player.interactCooldown <= 0f;

        void ResearchPrecision(WorldState world, PlayerState player)
        {
            if (!PrecisionActionReady(player) || PrecisionCatalog.HasResearch(world, selectedPrecision) ||
                !string.IsNullOrEmpty(PrecisionCatalog.ResearchRequirement(world, player, selectedPrecision))) return;
            Send("hv", selectedPrecision);
        }

        RectTransform PrecisionColumn(string name, RectTransform content, float width)
        {
            RectTransform column = NewRect(name, content);
            Place(column, 0, 0, width, 1);
            var layout = column.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return column;
        }

        void MakePrecisionMachineControls(RectTransform content)
        {
            precisionAssemblerActions = PrecisionColumn("Precision Assembler Controls", content, 260);
            precisionAssemblerState = FlowLabel("Precision Assembler State", precisionAssemblerActions, "", 260, 48, 13, Ink);
            RectTransform row = FlowRow("Precision Mode Row", precisionAssemblerActions, 260, 34);
            precisionMode = Button("Toggle Precision Buffer Mode", row, "", 0, 0, 254, 34, TogglePrecisionMode);
            precisionAssemblerActions.gameObject.SetActive(false);
        }

        void RefreshPrecisionMachineControls(WorldState world, PlayerState player, BuildingState building)
        {
            bool assembler = building != null && building.kind == BuildKind.PrecisionAssembler;
            precisionAssemblerActions.gameObject.SetActive(assembler);
            if (!assembler) return;

            bool automatic = building.setting == 1;
            bool unlocked = PrecisionCatalog.HasResearch(world, PrecisionTech.Control);
            RecipeDefinition recipe = MachineRecipe(building);
            var outputs = new List<string>();
            bool bufferBlocked = false;
            if (recipe != null)
            {
                int[] reserved = automatic ? PrecisionReservations(world, building.id) : null;
                for (int i = 0; i < recipe.outputs.Length; i++)
                {
                    int current = Count(building.items, i);
                    if (automatic && recipe.outputs[i] > 0 && current - recipe.inputs[i] + recipe.outputs[i] + reserved[i] > PrecisionCatalog.ControlledBuffer)
                        bufferBlocked = true;
                    if (recipe.outputs[i] > 0)
                        outputs.Add(DemoCatalog.ItemName((ItemKind)i) + " " + current + (automatic ? "/" + PrecisionCatalog.ControlledBuffer : ""));
                }
            }
            SetFlowText(precisionAssemblerState, "加工模式：" + (automatic ? "成品缓冲自动暂停" : "连续加工") +
                (outputs.Count == 0 ? "" : "\n当前配方产物：" + string.Join(" · ", outputs)) +
                (!unlocked ? "\n需研究自动控制。" : bufferBlocked ? "\n缓存暂停：计入在途物料后，下一批会超出上限。" : ""), 48);
            precisionAssemblerState.color = bufferBlocked ? Amber : Ink;
            precisionMode.button.interactable = CanConfigurePrecision(world, player, building);
            Set(precisionMode.label, !unlocked ? "自动控制未解锁" : automatic ? "切换：连续加工" : "切换：成品缓冲自动暂停");
        }

        static int[] PrecisionReservations(WorldState world, int buildingId)
        {
            var reserved = new int[DemoCatalog.ItemCount];
            if (world.cargo != null)
                foreach (var cargo in world.cargo)
                    if (cargo.toId == buildingId || (cargo.fromId == buildingId && PetrochemicalCatalog.IsFluid(cargo.item)))
                        reserved[cargo.item]++;
            return reserved;
        }

        static bool CanSwitchTransformer(WorldState world, BuildingState building) =>
            building.setting == 1 || PrecisionCatalog.HasResearch(world, PrecisionTech.Grid);

        bool CanConfigurePrecision(WorldState world, PlayerState player, BuildingState building) =>
            world != null && building != null && building.kind == BuildKind.PrecisionAssembler && building.hp > 0f &&
            world.era >= TechEra.HighVoltage && PrecisionCatalog.HasResearch(world, PrecisionTech.Control) && PrecisionActionReady(player) &&
            SurvivalInReach(world, player, inspectedX, inspectedY) && MapExploration.IsVisible(world, player, inspectedX, inspectedY);

        void TogglePrecisionMode()
        {
            BuildingState building = InspectedBuilding();
            if (!machinePanel.gameObject.activeSelf || !CanConfigurePrecision(bridge.World, bridge.LocalPlayer, building)) return;
            SendInspected("configure", building.setting == 1 ? 0 : 1);
        }
    }
}
