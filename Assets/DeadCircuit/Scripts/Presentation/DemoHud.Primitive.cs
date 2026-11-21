namespace DeadCircuit
{
    public sealed partial class DemoHud
    {
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
                return "按 T → 原始工艺：" + definition.name + (string.IsNullOrEmpty(requirement) ? " · 条件已满足，点击研究" : " · " + requirement);
            }
            string water = PrimitiveCatalog.WaterRequirement(world);
            return string.IsNullOrEmpty(water) ? null : "按 T 查看水力时代条件：" + water;
        }
    }
}
