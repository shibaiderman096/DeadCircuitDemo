using System;

namespace DeadCircuit
{
    // Snapshot-only helpers shared by the inspector and cell visuals. Never pays materials.
    internal static class ModularMachinePresentation
    {
        internal static int[] Remaining(BuildingState building, int module)
        {
            var required = ModularMachineCatalog.ModuleCost(building, module);
            for (int item = 0; item < required.Length; item++)
                required[item] = Math.Max(0, required[item] - Paid(building, module, item));
            return required;
        }

        static int Paid(BuildingState building, int module, int item)
        {
            var modules = building.modularModules;
            if (modules == null || module < 0 || module >= modules.Count || modules[module] == null) return 0;
            var paid = modules[module].paid;
            return paid != null && item >= 0 && item < paid.Length ? paid[item] : 0;
        }

        internal static float ConstructionRatio(BuildingState building, int module)
        {
            int total = 0, paid = 0;
            var cost = ModularMachineCatalog.ModuleCost(building, module);
            for (int item = 0; item < cost.Length; item++)
            {
                total += cost[item];
                paid += Math.Min(cost[item], Paid(building, module, item));
            }
            return total > 0 ? (float)paid / total : 0f;
        }

        internal static readonly MachineModuleRole[] PaletteRoles = { MachineModuleRole.Controller, MachineModuleRole.Casing,
            MachineModuleRole.Process, MachineModuleRole.Input, MachineModuleRole.Output, MachineModuleRole.Energy,
            MachineModuleRole.Maintenance, MachineModuleRole.Exhaust };

        internal static int RoleCount(BuildingState building, MachineModuleRole role)
        {
            int found = 0;
            if (building != null) for (int i = 0; i < ModularMachineCatalog.ModuleCount(building); i++)
                if (ModularMachineCatalog.Role(building, i) == role) found++;
            return found;
        }

        internal static int FindRole(BuildingState building, MachineModuleRole role)
        {
            if (building != null) for (int i = 0; i < ModularMachineCatalog.ModuleCount(building); i++)
                if (ModularMachineCatalog.Role(building, i) == role) return i;
            return -1;
        }

        internal static int CompletedModules(BuildingState building)
        {
            int count = 0;
            if (building != null) for (int i = 0; i < ModularMachineCatalog.ModuleCount(building); i++)
                if (ModularMachineCatalog.ModuleComplete(building, i)) count++;
            return count;
        }

        internal static int[] PlannedCost(BuildingState building)
        {
            var total = new int[DemoCatalog.ItemCount];
            if (building != null) for (int i = 0; i < ModularMachineCatalog.ModuleCount(building); i++)
            {
                var cost = ModularMachineCatalog.ModuleCost(building, i);
                for (int item = 0; item < total.Length; item++) total[item] += cost[item];
            }
            return total;
        }

        internal static int[] MinimumCost(BuildKind kind)
        {
            var total = new int[DemoCatalog.ItemCount];
            foreach (var role in PaletteRoles)
            {
                var cost = ModularMachineCatalog.ModuleCost(kind, role);
                int required = role == MachineModuleRole.Casing ? 2 : 1;
                for (int item = 0; item < total.Length; item++) total[item] += cost[item] * required;
            }
            return total;
        }

        internal static string RoleTitle(BuildKind kind, MachineModuleRole role)
        {
            switch (role)
            {
                case MachineModuleRole.Controller: return "控制器";
                case MachineModuleRole.Casing: return "机壳";
                case MachineModuleRole.Process: return kind == BuildKind.LargeSteamFurnace ? "炉腔 / 工艺" : "工艺核心";
                case MachineModuleRole.Input: return "输入总线";
                case MachineModuleRole.Output: return "输出总线";
                case MachineModuleRole.Energy: return "能源舱";
                case MachineModuleRole.Maintenance: return "维护舱";
                default: return kind == BuildKind.LargeSteamFurnace ? "排气口" : "通风口";
            }
        }

        internal static bool CanErase(WorldState world, BuildingState building)
        {
            if (building == null || building.progress > 0f) return false;
            if (building.items != null) foreach (int amount in building.items) if (amount > 0) return false;
            if (world.cargo != null) foreach (var cargo in world.cargo) if (cargo.fromId == building.id || cargo.toId == building.id) return false;
            return true;
        }

        internal static bool ModuleRefundFits(PlayerState player, BuildingState building, int index)
        {
            if (player == null || building?.modularModules == null || index < 0 || index >= building.modularModules.Count) return false;
            var paid = building.modularModules[index]?.paid;
            return paid == null || InventoryWeight.CanFit(player, InventoryWeight.MaterialsGrams(paid));
        }

        internal static bool InReach(WorldState world, PlayerState player, int tx, int ty, BuildingState ignored)
        {
            if (world == null || player == null || !WorldTiles.Inside(world, tx, ty)) return false;
            float dx = tx - player.x, dy = ty - player.y;
            if (dx * dx + dy * dy > 2.4f * 2.4f) return false;
            int sx = (int)Math.Floor(player.x + .5f), sy = (int)Math.Floor(player.y + .5f);
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(dx * dx + dy * dy) / .16));
            for (int step = 1; step <= steps; step++)
            {
                int x = (int)Math.Floor(player.x + dx * step / steps + .5f), y = (int)Math.Floor(player.y + dy * step / steps + .5f);
                if (x == tx && y == ty || x == sx && y == sy || ignored != null && DemoCatalog.Contains(ignored, x, y)) continue;
                char ground = WorldTiles.Ground(world, x, y);
                if (!WorldTiles.Inside(world, x, y) || ground == '2' || ground == '?') return false;
                foreach (var node in world.nodes) if (node.x == x && node.y == y && node.hp > 0 && node.stock > 0) return false;
                foreach (var building in world.buildings)
                    if (building.hp > 0 && !DemoCatalog.Walkable(building) && DemoCatalog.Contains(building, x, y)) return false;
            }
            return true;
        }

        internal static int Incoming(WorldState world, BuildingState building, int item)
        {
            int count = 0;
            if (world.cargo != null) foreach (var cargo in world.cargo) if (cargo.toId == building.id && cargo.item == item) count++;
            return count;
        }

        internal static bool OutputHasSpace(WorldState world, BuildingState building, RecipeDefinition recipe)
        {
            for (int item = 0; item < DemoCatalog.ItemCount; item++)
                if (building.items[item] - recipe.inputs[item] + recipe.outputs[item] + Incoming(world, building, item) > WorldSimulator.BufferLimit) return false;
            return true;
        }

        internal static bool Processing(WorldState world, BuildingState building)
        {
            if (world == null || !ModularMachineCatalog.IsComplete(building) || building.hp < WorldSimulator.BuildingMaxHealth(building.kind) ||
                !building.powered || building.powerRatio <= 0 || !ModularMachineCatalog.ExhaustClear(world, building) || !TechCatalog.IsUnlocked(world, building.kind)) return false;
            RecipeDefinition recipe = TechCatalog.SelectedRecipe(building);
            if (recipe == null || !PrimitiveCatalog.IsRecipeUnlocked(world, recipe)) return false;
            int maintenance = (int)ModularMachineCatalog.MaintenanceItem(building.kind);
            if (building.items[maintenance] < recipe.inputs[maintenance] + (building.modularService == 0 ? 1 : 0)) return false;
            for (int item = 0; item < DemoCatalog.ItemCount; item++) if (building.items[item] < recipe.inputs[item]) return false;
            return OutputHasSpace(world, building, recipe);
        }

        internal static string Mark(MachineModuleRole role)
        {
            switch (role)
            {
                case MachineModuleRole.Controller: return "控";
                case MachineModuleRole.Energy: return "能";
                case MachineModuleRole.Maintenance: return "修";
                case MachineModuleRole.Input: return "入";
                case MachineModuleRole.Process: return "工";
                case MachineModuleRole.Output: return "出";
                case MachineModuleRole.Exhaust: return "排";
                default: return "壳";
            }
        }
    }
}
