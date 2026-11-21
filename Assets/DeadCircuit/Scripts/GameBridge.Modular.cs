using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class GameBridge
    {
        public MachineModuleRole ModularRoleSelection { get; set; } = MachineModuleRole.Controller;
        public int ModularBlueprintId { get; private set; }
        public bool ModularEraseMode { get; set; }
        public BuildingState ActiveModularBlueprint => World?.buildings?.Find(b => b.id == ModularBlueprintId && ModularMachineCatalog.IsModular(b.kind));
        bool pendingModularController;
        int pendingModularX, pendingModularY;
        BuildKind pendingModularKind;
        long pendingModularTick, lastModularPaintTick = -100;
        int lastModularPaintX = int.MinValue, lastModularPaintY = int.MinValue;

        public void SelectModularBuilding(BuildKind kind)
        {
            if (!ModularMachineCatalog.IsModular(kind) || !ModularMachineCatalog.HasWrench(LocalPlayer))
            { Status = "请先在工具槽装备并手持耐久正常的扳手。"; return; }
            CancelPlayerControl(); BuildSelection = kind; BuildRotation = 0;
            ModularBlueprintId = 0; ModularRoleSelection = MachineModuleRole.Controller;
            ModularEraseMode = pendingModularController = false;
            lastModularPaintX = lastModularPaintY = int.MinValue; lastModularPaintTick = -100;
        }

        public void EditModularBlueprint(BuildingState building)
        {
            if (building == null || !ModularMachineCatalog.IsModular(building.kind) || !ModularMachineCatalog.HasWrench(LocalPlayer)) return;
            CancelPlayerControl(); BuildSelection = building.kind; BuildRotation = 0;
            ModularBlueprintId = building.id; ModularRoleSelection = MachineModuleRole.Casing;
            ModularEraseMode = pendingModularController = false;
            lastModularPaintX = lastModularPaintY = int.MinValue; lastModularPaintTick = -100;
        }

        public void SelectModularRole(MachineModuleRole role)
        {
            if ((int)role < 0 || (int)role > (int)MachineModuleRole.Exhaust) return;
            ModularRoleSelection = role; ModularEraseMode = false;
        }

        void RefreshModularDrawing()
        {
            if (!BuildSelection.HasValue || !ModularMachineCatalog.IsModular(BuildSelection.Value))
            { ModularBlueprintId = 0; ModularEraseMode = pendingModularController = false; return; }
            if (!ModularMachineCatalog.HasWrench(LocalPlayer))
            { BuildSelection = null; ModularBlueprintId = 0; ModularEraseMode = pendingModularController = false; return; }
            if (pendingModularController) ResolveModularController();
            if (ModularBlueprintId != 0 && ActiveModularBlueprint == null)
            { ModularBlueprintId = 0; ModularRoleSelection = MachineModuleRole.Controller; ModularEraseMode = false; }
        }

        void ResolveModularController()
        {
            if (World == null) return;
            foreach (var building in World.buildings)
            {
                if (building.kind != pendingModularKind) continue;
                int cell = ModularMachineCatalog.ModuleAt(building, pendingModularX, pendingModularY);
                if (cell < 0 || ModularMachineCatalog.Role(building, cell) != MachineModuleRole.Controller) continue;
                ModularBlueprintId = building.id; ModularRoleSelection = MachineModuleRole.Casing;
                pendingModularController = false; return;
            }
            if (IsHost || World.tick - pendingModularTick > 60) pendingModularController = false;
        }

        bool CanPaintNextModularCell()
        {
            if (!BuildSelection.HasValue || !ModularMachineCatalog.IsModular(BuildSelection.Value) ||
                pendingModularController || ModularBlueprintId == 0 || World == null || World.tick - lastModularPaintTick < 5) return false;
            Vector2 point = MouseWorld;
            int x = Mathf.FloorToInt(point.x + .5f), y = Mathf.FloorToInt(point.y + .5f);
            return x != lastModularPaintX || y != lastModularPaintY;
        }

        void DrawModularCell(int x, int y)
        {
            RefreshModularDrawing();
            if (!BuildSelection.HasValue || pendingModularController || !ModularMachineCatalog.HasWrench(LocalPlayer)) return;
            lastModularPaintX = x; lastModularPaintY = y; lastModularPaintTick = World.tick;
            if (ModularBlueprintId == 0)
            {
                if (ModularEraseMode || ModularRoleSelection != MachineModuleRole.Controller) return;
                pendingModularController = true; pendingModularX = x; pendingModularY = y;
                pendingModularKind = BuildSelection.Value; pendingModularTick = World.tick;
                Send(new PlayerCommand { action = "build", arg = (int)BuildSelection.Value, x = x, y = y, rotation = BuildRotation });
                ResolveModularController();
                return;
            }
            Send(new PlayerCommand { action = ModularEraseMode ? "moduleerase" : "moduleplan", buildingId = ModularBlueprintId,
                arg = (int)ModularRoleSelection, x = x, y = y, rotation = BuildRotation });
        }
    }
}
