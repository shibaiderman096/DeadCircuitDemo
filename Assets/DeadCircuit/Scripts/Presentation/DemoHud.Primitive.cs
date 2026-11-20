using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class DemoHud
    {
        sealed class PrimitiveResearchRow
        {
            public int id;
            public RectTransform root;
            public TextMeshProUGUI details;
            public UiButton research;
        }

        readonly List<PrimitiveResearchRow> primitiveResearchRows = new List<PrimitiveResearchRow>();
        TextMeshProUGUI primitiveHeading;

        void MakePrimitiveTechnology(RectTransform content)
        {
            primitiveHeading = FlowLabel("Primitive Research Heading", content, "原始工艺 · 先研究，再建造并验证生产", 768, 30, 18, Accent);
            foreach (var definition in PrimitiveCatalog.All)
            {
                int id = (int)definition.id;
                RectTransform root = FlowRow("Primitive Research Row " + id, content, 768, 144);
                var details = Label("Primitive Research Details " + id, root, "", 8, 6, 562, 132, 15, Ink);
                details.enableAutoSizing = false;
                details.alignment = TextAlignmentOptions.TopLeft;
                UiButton button = Button("Primitive Research " + id, root, "", 588, 12, 170, 64, () => ResearchPrimitive(id));
                SetButtonArt(button, "ui_research", 22);
                primitiveResearchRows.Add(new PrimitiveResearchRow { id = id, root = root, details = details, research = button });
            }
            MakeWaterTechnology(content);
            MakeSteamTechnology(content);
            MakeLowVoltageTechnology(content);
            MakePetrochemistryTechnology(content);
            MakePrecisionTechnology(content);
        }

        void RefreshPrimitiveTechnology(WorldState world, PlayerState player)
        {
            bool visible = selectedEra == 0 && PrimitiveCatalog.Enabled(world);
            primitiveHeading.gameObject.SetActive(visible);
            foreach (var row in primitiveResearchRows)
            {
                row.root.gameObject.SetActive(visible);
                if (!visible) continue;
                var definition = PrimitiveCatalog.Definition(row.id);
                bool known = PrimitiveCatalog.HasResearch(world, row.id);
                bool ready = player.connected && player.hp > 0f && player.interactCooldown <= 0f;
                bool can = !known && ready && PrimitiveCatalog.CanResearch(world, player, row.id);
                string requirement = PrimitiveCatalog.ResearchRequirement(world, player, row.id);
                string state = known ? "已研究" : !player.connected || player.hp <= 0f ? "角色尚未就绪" :
                    player.interactCooldown > 0f ? "交互冷却中" : can ? "可研究" : "条件未满足";
                Set(row.details, definition.name + " · " + state + "\n" + definition.description +
                    "\n前置：" + PrimitivePrerequisites(world, definition.prerequisites) +
                    "\n材料：" + MaterialText(definition.cost, player.items, true) +
                    "\n工艺 / 生产验证：" + (known ? "已完成" : string.IsNullOrEmpty(requirement) ? "已满足" : requirement));
                row.details.color = known ? Accent : Ink;
                float height = Mathf.Max(144, row.details.GetPreferredValues(row.details.text, 562, float.PositiveInfinity).y + 16);
                row.details.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height - 12);
                row.root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
                row.research.button.interactable = can;
                Set(row.research.label, known ? "已研究" : can ? "研究" + definition.name : state);
                Tint(row.research, known ? Selected : Raised);
            }
            RefreshWaterTechnology(world, player);
            RefreshSteamTechnology(world, player);
            RefreshLowVoltageTechnology(world, player);
            RefreshPetrochemistryTechnology(world, player);
            RefreshPrecisionTechnology(world, player);
        }

        static string PrimitivePrerequisites(WorldState world, int mask)
        {
            var names = new List<string>();
            foreach (var definition in PrimitiveCatalog.All)
            {
                int id = (int)definition.id;
                if ((mask & (1 << id)) == 0) continue;
                names.Add(definition.name + (PrimitiveCatalog.HasResearch(world, id) ? "（已研究）" : "（未研究）"));
            }
            return names.Count == 0 ? "无" : string.Join("、", names);
        }

        void ResearchPrimitive(int id)
        {
            var world = bridge.World;
            var player = bridge.LocalPlayer;
            if (!technology.gameObject.activeSelf || selectedEra != 0 || world == null || player == null ||
                !player.connected || player.hp <= 0f || player.interactCooldown > 0f ||
                !PrimitiveCatalog.Enabled(world) || PrimitiveCatalog.HasResearch(world, id) ||
                !PrimitiveCatalog.CanResearch(world, player, id)) return;
            Send("primitive", id);
        }

        static int DefaultTechnologyEra(WorldState world) => PrimitiveCatalog.Enabled(world) && world.era == TechEra.Primitive
            ? 0 : WaterCatalog.Enabled(world) && world.era == TechEra.Water ? 1 :
                world.era == TechEra.Steam ? 2 : world.era == TechEra.LowVoltage ? 3 :
                world.era == TechEra.MediumVoltage ? 4 : world.era == TechEra.HighVoltage ? 5 : Mathf.Min((int)world.era + 1, 6);

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

        static string PrimitiveGoal(WorldState world, PlayerState player)
        {
            foreach (var definition in PrimitiveCatalog.All)
            {
                int id = (int)definition.id;
                if (definition.id == PrimitiveTech.Cooking) continue; // Optional survival branch, not a Water gate.
                if (PrimitiveCatalog.HasResearch(world, id)) continue;
                string requirement = PrimitiveCatalog.ResearchRequirement(world, player, id);
                return "按 T → 原始工艺：" + definition.name + (string.IsNullOrEmpty(requirement) ? " · 材料已齐，点击研究" : " · " + requirement);
            }
            string water = PrimitiveCatalog.WaterRequirement(world);
            return string.IsNullOrEmpty(water) ? null : "按 T 查看水力前置：" + water;
        }
    }
}
