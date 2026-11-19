using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private readonly List<FarmPlotState> waterFarmOrder = new List<FarmPlotState>();

        private void RecordWaterProduction(BuildingState b, RecipeDefinition recipe)
        {
            if (!WaterCatalog.IsWaterDriven(State,b)) return;
            switch (recipe.id)
            {
                case 20:
                    State.waterCrushed = SaturatingAdd(State.waterCrushed,recipe.outputs[(int)ItemKind.CrushedOre]);
                    break;
                case 110:
                    State.waterSmelted = SaturatingAdd(State.waterSmelted,recipe.outputs[(int)ItemKind.Iron]);
                    break;
                case 113:
                    State.waterPressed = SaturatingAdd(State.waterPressed,recipe.outputs[(int)ItemKind.IronPlate]);
                    break;
                case 114:
                    State.waterGears = SaturatingAdd(State.waterGears,recipe.outputs[(int)ItemKind.Gear]);
                    break;
                case 115:
                    State.waterBearings = SaturatingAdd(State.waterBearings,recipe.outputs[(int)ItemKind.Bearing]);
                    break;
            }
        }

        // Tick integration follows RefreshPower; rainfall, evaporation and crop use remain in survival.
        private void UpdateWaterAgriculture()
        {
            var survival = State.survival;
            if (survival == null || !survival.enabled || survival.plots == null || survival.plots.Count == 0 ||
                !WaterCatalog.HasResearch(State,WaterTech.Agriculture)) return;
            waterFarmOrder.Clear();
            waterFarmOrder.AddRange(survival.plots);
            waterFarmOrder.Sort((a,b) => a.id.CompareTo(b.id));
            foreach (BuildingState pump in productionOrder)
            {
                if (pump.kind != BuildKind.IrrigationPump || pump.hp <= 0f || !pump.powered || !(pump.powerRatio > 0f) ||
                    !PrimitiveCatalog.IsBuildUnlocked(State,pump.kind) || !WaterCatalog.IsBuildUnlocked(State,pump.kind) || !NearWater(pump)) continue;
                float remaining = 8f * FixedStep * pump.powerRatio;
                bool waterDriven = WaterCatalog.IsWaterDriven(State,pump);
                int width = DemoCatalog.Width(pump), height = DemoCatalog.Height(pump);
                foreach (FarmPlotState plot in waterFarmOrder)
                {
                    if (remaining <= 0f) break;
                    if (!(plot.moisture < 65f) || BlockedAt(plot.x,plot.y)) continue;
                    // Match power coverage: measure the gap between occupied cell edges.
                    float dx = Math.Max(0f,Math.Max(pump.x-(plot.x+1f),plot.x-(pump.x+width)));
                    float dy = Math.Max(0f,Math.Max(pump.y-(plot.y+1f),plot.y-(pump.y+height)));
                    if (dx*dx+dy*dy > 16f) continue;
                    int targetX = Math.Max(pump.x,Math.Min(plot.x,pump.x+width-1));
                    int targetY = Math.Max(pump.y,Math.Min(plot.y,pump.y+height-1));
                    // Trace toward the pump so LineClear ignores its entire footprint, not a wall.
                    if (!LineClear(plot.x,plot.y,targetX,targetY,targetX,targetY)) continue;
                    float before = plot.moisture;
                    float after = Math.Min(65f,before+remaining);
                    float added = after-before;
                    if (!(added > 0f)) continue;
                    plot.moisture = after;
                    remaining = Math.Max(0f,remaining-added);
                    if (waterDriven) State.waterIrrigated = Math.Min(10000000f,State.waterIrrigated+added);
                }
            }
        }

        // Only the explicitly targeted water mill is eligible; manual cooking stays independent.
        private void Mill(PlayerState p, int x, int y)
        {
            if (p == null || p.interactCooldown > 0f || State.survival == null || !State.survival.enabled ||
                p.needs == null || p.needs.foods == null || p.needs.foods.Length != SurvivalCatalog.FoodCount) return;
            if (!SurvivalReach(p,x,y)) { Notice(p,"需要靠近并看见指定的磨坊。"); return; }
            BuildingState mill = BuildingAt(x,y);
            if (mill == null || mill.hp <= 0f || mill.kind != BuildKind.WaterMill)
            { Notice(p,"请瞄准水力磨坊。"); return; }
            string requirement = PrimitiveCatalog.BuildRequirement(State,mill.kind);
            if (requirement.Length == 0) requirement = WaterCatalog.BuildRequirement(State,mill.kind);
            if (requirement.Length != 0) { Notice(p,requirement); return; }
            if (!PrimitiveCatalog.IsCookingUnlocked(State)) { Notice(p,"需要研究：烹饪净水"); return; }
            if (!mill.powered || !(mill.powerRatio > 0f)) { Notice(p,"磨坊需要接入有动力的轴系。"); return; }
            int[] foods = p.needs.foods;
            if (foods[(int)FoodKind.Wheat] < 2) { Notice(p,"磨粉需要 2 份小麦。"); return; }
            if (foods[(int)FoodKind.Flour] > SurvivalCatalog.StackLimit-3) { Notice(p,"食物背包中的面粉容量不足。"); return; }
            foods[(int)FoodKind.Wheat] -= 2;
            foods[(int)FoodKind.Flour] += 3;
            if (WaterCatalog.IsWaterDriven(State,mill)) State.waterMilled = SaturatingAdd(State.waterMilled,3);
            p.interactCooldown = 2f;
            Notice(p,"动力磨粉：2 份小麦制成 3 份面粉。");
        }
    }
}
