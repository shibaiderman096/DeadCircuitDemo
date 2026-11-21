using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class DemoHud
    {
        RectTransform precisionAssemblerActions;
        TextMeshProUGUI precisionAssemblerState;
        UiButton precisionMode;
        bool PrecisionActionReady(PlayerState player) =>
            bridge.IsRunning && player != null && player.connected && player.hp > 0f && player.interactCooldown <= 0f;

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
                        outputs.Add(DemoCatalog.ItemName((ItemKind)i) + "×" + current + (automatic ? "/" + PrecisionCatalog.ControlledBuffer : ""));
                }
            }
            SetFlowText(precisionAssemblerState, "加工模式：" + (automatic ? "库存达限暂停" : "连续加工") +
                (outputs.Count == 0 ? "" : "\n成品库存：" + string.Join(" · ", outputs)) +
                (!unlocked ? "\n先研究自动控制，即可启用库存限制。" : bufferBlocked ? "\n已暂停：计入在途物料后，下一批产出将超过库存上限。请先取出成品。" : ""), 48);
            precisionAssemblerState.color = bufferBlocked ? Amber : Ink;
            precisionMode.button.interactable = CanConfigurePrecision(world, player, building);
            Set(precisionMode.label, !unlocked ? "自动控制未解锁" : automatic ? "连续加工" : "启用库存限制");
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
