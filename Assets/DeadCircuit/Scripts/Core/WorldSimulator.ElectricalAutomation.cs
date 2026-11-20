namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private const float ElectricalOverloadTolerance = .0001f;

        private static bool ReservesCargoReturn(BuildingState b) =>
            b.kind == BuildKind.MechanicalHopper || b.kind == BuildKind.FilterSplitter;

        // These are physical routes, shared by dispatch and current-save validation.
        // Destination inventory compatibility remains the caller's responsibility.
        public static bool ElectricalCargoRouteValid(BuildingState from, BuildingState to, int item)
        {
            if (from == null || to == null || item < 0 || item >= DemoCatalog.ItemCount) return false;
            if (from.kind != BuildKind.FilterSplitter)
                return DemoCatalog.Contains(to, DemoCatalog.OutputX(from), DemoCatalog.OutputY(from));
            if (from.setting < 0 || from.setting >= DemoCatalog.ItemCount) return false;
            int direction = item == from.setting ? from.rotation : (from.rotation + 1) & 3;
            return DemoCatalog.Contains(to, from.x + DirectionX[direction], from.y + DirectionY[direction]);
        }

        private bool TryPlanElectricalCargo(BuildingState from, out ItemTransfer plan)
        {
            plan = default(ItemTransfer);
            for (int item = 0; item < DemoCatalog.ItemCount; item++)
            {
                if (from.items[item] <= 0) continue;
                int direction = item == from.setting ? from.rotation : (from.rotation + 1) & 3;
                int x = from.x + DirectionX[direction], y = from.y + DirectionY[direction];
                BuildingState to = BuildingAt(x, y);
                if (to == null || to == from || to.hp <= 0f || !ElectricalCargoRouteValid(from, to, item)) continue;
                if (to.kind == BuildKind.Belt && ((direction - to.rotation) & 3) == 2) continue;
                if (!Accepts(to, item) || !HasCargoRoom(to, item)) continue;
                plan = new ItemTransfer { from = from, to = to, item = item, x = x, y = y, direction = direction };
                return true;
            }
            return false;
        }

        private bool ConfigureElectrical(PlayerState p, BuildingState b, int arg)
        {
            if (b == null || (b.kind != BuildKind.FilterSplitter && b.kind != BuildKind.CircuitBreaker)) return false;
            if (!TechCatalog.IsUnlocked(State, b.kind) || !LowVoltageCatalog.IsBuildUnlocked(State, b.kind))
            {
                Notice(p, "尚未解锁此电气设备。");
                return true;
            }
            if (b.kind == BuildKind.FilterSplitter)
            {
                if (arg < 0 || arg >= DemoCatalog.ItemCount) return true;
                foreach (CargoState c in State.cargo)
                    if (c.fromId == b.id || c.toId == b.id)
                    {
                        Notice(p, "分流器仍有在途物品；到货后再更改过滤物品。");
                        return true;
                    }
                b.setting = arg;
                Notice(p, "分流：" + DemoCatalog.ItemNames[arg] + "走正前方，其余走左侧。");
                return true;
            }

            // Command 0 opens manually (setting 1); command 1 closes/resets (setting 0).
            if (arg != 0 && arg != 1) return true;
            if (arg == 0)
            {
                b.setting = 1;
                RefreshPower(false);
                Notice(p, "断路器已手动断开，同网LV设备停止供电。");
                return true;
            }
            RefreshPower(false);
            foreach (PowerNetworkState network in State.powerNetworks)
                if (network.id == b.networkId && network.tier == 2 && ElectricalOverloaded(network))
                {
                    Notice(p, "过载仍在：请先减少同网负载或增加发电，再复位。");
                    return true;
                }
            b.setting = 0;
            b.tripped = false;
            RefreshPower(false);
            Notice(p, "断路器已复位接通。");
            return true;
        }

        private static bool ElectricalOverloaded(PowerNetworkState network) =>
            network.generation > 0f && network.demand > network.generation + ElectricalOverloadTolerance;

        private void ApplyElectricalProtection(bool consume)
        {
            foreach (PowerLayout layout in powerLayout)
            {
                PowerNetworkState network = layout.network;
                BuildingState breaker = layout.building;
                if (layout.kind != BuildKind.CircuitBreaker || !layout.unlocked || network == null || network.tier != 2) continue;
                if (consume && ElectricalOverloaded(network)) breaker.tripped = true;
                // Cut the actual allocation before UpdateProduction/UpdateTurrets see it.
                // Keep raw generation/demand visible so a still-overloaded reset is refused.
                if (breaker.setting != 0 || breaker.tripped) network.ratio = 0f;
            }
        }

        private bool IsProtectedLowVoltage(BuildingState machine)
        {
            if (machine == null || machine.hp <= 0f || !machine.powered || machine.powerRatio <= 0f ||
                machine.networkId <= 0 || TechCatalog.EnergyTier(machine) != 2 ||
                !LowVoltageCatalog.HasResearch(State, LowVoltageTech.Generation)) return false;
            bool protectedNetwork = false;
            foreach (PowerLayout layout in powerLayout)
            {
                PowerNetworkState network = layout.network;
                BuildingState breaker = layout.building;
                if (layout.kind != BuildKind.CircuitBreaker || !layout.unlocked || network == null ||
                    network.id != machine.networkId || network.tier != 2 || network.generation <= 0f) continue;
                if (breaker.setting != 0 || breaker.tripped) return false;
                protectedNetwork = true;
            }
            return protectedNetwork;
        }

        private bool InteractElectrical(PlayerState p, BuildingState b, int arg)
        {
            if (b.kind == BuildKind.CircuitBreaker)
            {
                Notice(p, b.tripped ? "断路器过载跳闸；减载后用配置操作复位。" :
                    b.setting != 0 ? "断路器手动断开；用配置操作接通。" :
                    !b.powered ? "断路器已接通，但同网无有效供电。" : "断路器已接通；保护所在LV电网。");
                return true;
            }
            if (b.kind != BuildKind.FilterSplitter) return false;
            int moved = 0;
            for (int item = 0; item < DemoCatalog.ItemCount; item++)
                moved += Transfer(b.items, p.items, item, BufferLimit, InventoryLimit);
            if (moved == 0 && arg >= 0 && arg < DemoCatalog.ItemCount)
                moved = TransferIntoBuilding(p.items, b, arg, 20);
            Notice(p, moved > 0 ? "分流器转移 " + moved + " 件。" :
                "分流器：匹配物品走正前方，其余走左侧；需要LV供电。");
            return true;
        }
    }
}
