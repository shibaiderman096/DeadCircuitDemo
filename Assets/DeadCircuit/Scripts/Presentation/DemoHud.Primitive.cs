using TMPro;
using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class DemoHud
    {
        static readonly GearKind[] PrimitiveToolKinds = { GearKind.StoneAxe, GearKind.StonePickaxe };
        readonly RectTransform[] primitiveToolRows = new RectTransform[PrimitiveToolKinds.Length];
        readonly TextMeshProUGUI[] primitiveToolDetails = new TextMeshProUGUI[PrimitiveToolKinds.Length];
        readonly UiButton[] primitiveCraftButtons = new UiButton[PrimitiveToolKinds.Length];
        readonly UiButton[] primitiveEquipButtons = new UiButton[PrimitiveToolKinds.Length];
        TextMeshProUGUI primitiveCraftHeading;

        void MakePrimitiveTechnology(RectTransform content)
        {
            primitiveCraftHeading = FlowLabel("Primitive Handcraft Heading", content,
                "随身制作 · 无需工作台或研究", 768, 30, 18, Accent);
            for (int i = 0; i < PrimitiveToolKinds.Length; i++)
            {
                GearKind kind = PrimitiveToolKinds[i];
                string name = EquipmentCatalog.GearNames[(int)kind];
                RectTransform row = primitiveToolRows[i] = FlowRow("Primitive Tool " + kind, content, 768, 76);
                primitiveCraftButtons[i] = Button("Craft " + kind, row, "制作" + name, 4, 8, 170, 56,
                    () => CraftPrimitiveTool(kind));
                SetButtonArt(primitiveCraftButtons[i], "gear_" + (int)kind, 24);
                primitiveEquipButtons[i] = Button("Equip " + kind, row, "装备" + name, 610, 8, 148, 56,
                    () => EquipPrimitiveTool(kind));
                var details = primitiveToolDetails[i] = Label("Primitive Tool Details", row, "", 188, 6, 414, 64, 14, Ink);
                details.enableAutoSizing = false;
                details.alignment = TextAlignmentOptions.TopLeft;
            }
        }

        void RefreshPrimitiveTechnology(WorldState world, PlayerState player)
        {
            bool visible = selectedEra == (int)TechEra.Primitive;
            primitiveCraftHeading.gameObject.SetActive(visible);
            GearState equipped = EquipmentCatalog.Equipped(player);
            for (int i = 0; i < PrimitiveToolKinds.Length; i++)
            {
                primitiveToolRows[i].gameObject.SetActive(visible);
                if (!visible) continue;
                GearKind kind = PrimitiveToolKinds[i];
                GearDefinition definition = EquipmentCatalog.Gear(kind);
                string requirement = EquipmentCatalog.CraftRequirement(world, player, kind);
                primitiveCraftButtons[i].button.interactable = bridge.IsRunning && requirement.Length == 0;

                bool active = equipped != null && equipped.kind == kind && equipped.durability > 0;
                GearState tool = active ? equipped : BestPrimitiveTool(player, kind);
                primitiveEquipButtons[i].button.interactable = bridge.IsRunning && player.connected && player.hp > 0 && tool != null && !active;
                Set(primitiveEquipButtons[i].label, active ? "已装备" : "装备" + definition.name);
                string use = kind == GearKind.StoneAxe ? "树木、补给箱" : "矿石、石材、黏土等矿床";
                string status = requirement.Length != 0 ? requirement : "可制作";
                if (tool != null) status += " · 工具耐久 " + tool.durability + "/" + definition.maxDurability;
                Set(primitiveToolDetails[i], MaterialText(definition.cost, player.items, true) +
                    "\n装备后可采集" + use + "\n" + status);
            }
        }

        void CraftPrimitiveTool(GearKind kind)
        {
            if (EquipmentCatalog.CanCraft(bridge.World, bridge.LocalPlayer, kind)) Send("gearcraft", (int)kind);
        }

        void EquipPrimitiveTool(GearKind kind)
        {
            var player = bridge.LocalPlayer;
            if (player == null || !player.connected) return;
            var tool = BestPrimitiveTool(player, kind);
            if (tool != null) Send("equip", tool.id);
        }

        static GearState BestPrimitiveTool(PlayerState player, GearKind kind)
        {
            GearState best = null;
            if (player.gear != null) foreach (var tool in player.gear)
                if (tool != null && tool.kind == kind && tool.durability > 0 && (best == null || tool.durability > best.durability))
                    best = tool;
            return best;
        }

        static string EraResearchRequirement(WorldState world, TechEra target)
        {
            string requirement = TechCatalog.ResearchRequirement(world, target);
            string process = target == TechEra.Water ? PrimitiveCatalog.WaterRequirement(world) :
                target == TechEra.Steam ? WaterCatalog.SteamRequirement(world) :
                target == TechEra.LowVoltage ? SteamCatalog.LowVoltageRequirement(world) :
                target == TechEra.MediumVoltage ? LowVoltageCatalog.MediumVoltageRequirement(world) :
                target == TechEra.HighVoltage ? PetrochemicalCatalog.HighVoltageRequirement(world) : "";
            return string.IsNullOrEmpty(process) || requirement.Contains(process) ? requirement :
                string.IsNullOrEmpty(requirement) ? process : requirement + "\n" + process;
        }

        static string PrimitiveHandcraftGoal(WorldState world, PlayerState player)
        {
            if (world.era != TechEra.Primitive || !PrimitiveCatalog.Enabled(world) || PrimitiveCatalog.HasResearch(world, (int)PrimitiveTech.Tools))
                return null;
            bool hasAxe = BestPrimitiveTool(player, GearKind.StoneAxe) != null;
            bool hasPickaxe = BestPrimitiveTool(player, GearKind.StonePickaxe) != null;
            if (hasAxe && hasPickaxe) return null;
            return "左键/E 或 Space 拾取树枝、石头、干草\nT → 原始 → 工艺与建造，制作并装备" +
                (!hasAxe && !hasPickaxe ? "石斧、石镐" : !hasAxe ? "石斧" : "石镐");
        }

        static string PrimitiveGoal(WorldState world, PlayerState player)
        {
            string handcraft = PrimitiveHandcraftGoal(world, player);
            if (handcraft != null) return handcraft;
            foreach (var definition in PrimitiveCatalog.All)
            {
                int id = (int)definition.id;
                if (definition.id == PrimitiveTech.Cooking) continue; // Optional survival branch, not a Water gate.
                if (PrimitiveCatalog.HasResearch(world, id)) continue;
                string requirement = PrimitiveCatalog.ResearchRequirement(world, player, id);
                return "按 T → 原始工艺：" + definition.name + (string.IsNullOrEmpty(requirement) ? " · 条件已满足，点击研究" : " · " + requirement);
            }
            string water = PrimitiveCatalog.WaterRequirement(world);
            return string.IsNullOrEmpty(water) ? null : "按 T 查看水力时代条件：" + water;
        }
    }
}
