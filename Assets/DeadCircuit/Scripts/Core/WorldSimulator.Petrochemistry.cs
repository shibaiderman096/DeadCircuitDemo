using System;

namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        public const int FluidCount = 9;
        private static readonly ItemKind[] PetrochemicalFluids =
        {
            ItemKind.CrudeOil, ItemKind.LightFraction, ItemKind.ChemicalFeedstock,
            ItemKind.HeavyFraction, ItemKind.Diesel, ItemKind.Lubricant,
            ItemKind.ProcessWater, ItemKind.HotCoolant, ItemKind.Coolant
        };

        public static int FluidItem(int setting) => setting >= 0 && setting < FluidCount
            ? (int)PetrochemicalFluids[setting] : -1;

        private static bool BlocksFluidLogistics(BuildKind kind) => kind == BuildKind.Belt ||
            kind == BuildKind.Chest || kind == BuildKind.FilterSplitter || kind == BuildKind.MechanicalHopper;

        private static int CargoOutputDirection(BuildingState from,int item) =>
            from.kind==BuildKind.Reactor && item==(int)ItemKind.HotCoolant ? (from.rotation+3)&3 : from.rotation;
        private static int CargoOutputX(BuildingState from,int item)
        {
            int direction=CargoOutputDirection(from,item);
            return direction==0 ? from.x+DemoCatalog.Width(from) : direction==2 ? from.x-1 : from.x+(DemoCatalog.Width(from)-1)/2;
        }
        private static int CargoOutputY(BuildingState from,int item)
        {
            int direction=CargoOutputDirection(from,item);
            return direction==1 ? from.y+DemoCatalog.Height(from) : direction==3 ? from.y-1 : from.y+(DemoCatalog.Height(from)-1)/2;
        }

        // Compose this with ElectricalCargoRouteValid so live routes and save validation agree.
        // Ordinary solids retain the existing route rules; liquid storage never carries solids.
        public static bool PetrochemicalCargoRouteValid(BuildingState from, BuildingState to, int item)
        {
            if(from == null || to == null || from == to || item < 0 || item >= DemoCatalog.ItemCount) return false;
            if(item == (int)ItemKind.NuclearWaste)
                return from.kind == BuildKind.Reactor && to.kind == BuildKind.NuclearWasteVault &&
                    DemoCatalog.Contains(to,DemoCatalog.OutputX(from),DemoCatalog.OutputY(from));
            if(from.kind==BuildKind.Reactor && item!=(int)ItemKind.HotCoolant) return false;
            bool sourceStorage = PetrochemicalCatalog.IsLiquidStorage(from.kind);
            bool targetStorage = PetrochemicalCatalog.IsLiquidStorage(to.kind);
            if(!PetrochemicalCatalog.IsFluid(item)) return !sourceStorage && !targetStorage;
            if(BlocksFluidLogistics(from.kind) || BlocksFluidLogistics(to.kind)) return false;
            if(sourceStorage && FluidItem(from.setting) != item) return false;
            if(targetStorage && FluidItem(to.setting) != item) return false;
            if(!DemoCatalog.Contains(to,CargoOutputX(from,item),CargoOutputY(from,item))) return false;
            if(!sourceStorage)
            {
                RecipeDefinition recipe = TechCatalog.SelectedRecipe(from);
                if(!IsOutput(from.kind,item) || (recipe != null && recipe.inputs[item] > 0)) return false;
            }
            if(targetStorage) return true;
            if(to.kind == BuildKind.DieselGenerator) return item == (int)ItemKind.Diesel;
            if(to.kind == BuildKind.Reactor) return item == (int)ItemKind.Coolant;
            RecipeDefinition input = TechCatalog.SelectedRecipe(to);
            return input != null && input.building == to.kind && to.kind != BuildKind.Workbench && input.inputs[item] > 0;
        }

        // All liquid flights reserve a source slot, including pump and multi-output machine
        // flights: a downstream disappearance must not lose fluid after a new batch completes.
        private static bool ReservesProductionCargoReturn(BuildingState b, int item) =>
            ReservesCargoReturn(b) || PetrochemicalCatalog.IsFluid(item) || (b.kind==BuildKind.Reactor && item==(int)ItemKind.NuclearWaste);

        private bool InteractPetrochemical(PlayerState p, BuildingState b)
        {
            if(!PetrochemicalCatalog.IsLiquidStorage(b.kind)) return false;
            int item = FluidItem(b.setting);
            if(item < 0) { Notice(p,"未选择液体，请先为管道或储液罐设置液体类型。"); return true; }
            bool empty = TotalItems(b.items) == 0;
            int moved = 0;
            if(empty) moved = TransferIntoBuilding(p.items,b,item,20);
            else for(int i=0;i<DemoCatalog.ItemCount;i++)
                moved += Transfer(b.items,p.items,i,BufferLimit,InventoryLimit);
            Notice(p,moved > 0 ? "已转移 " + moved + " 单位。" :
                "管道与储液罐只能装入所选液体。按 E 取出，空置时可装入。");
            return true;
        }

        private bool ConfigurePetrochemical(PlayerState p, BuildingState b, int arg)
        {
            if(!PetrochemicalCatalog.IsLiquidStorage(b.kind)) return false;
            if(arg < 0 || arg >= FluidCount) { Notice(p,"请选择有效液体类型。"); return true; }
            if(!PetrochemicalCatalog.IsBuildUnlocked(State,b.kind)) { Notice(p,"尚未解锁液体输送。"); return true; }
            if(TotalItems(b.items) != 0) { Notice(p,"请先清空管道或储液罐，再更改液体类型。"); return true; }
            foreach(var c in State.cargo)
                if(c.fromId == b.id || c.toId == b.id)
                { Notice(p,"仍有液体正在运输，请等待送达后再更改设置。"); return true; }
            b.setting = arg;
            Notice(p,"输送液体：" + DemoCatalog.ItemNames[FluidItem(arg)]);
            return true;
        }

        // Finite extraction uses only the one-cell rim of the rotated footprint, including
        // diagonal neighbours. Lowest ID wins when several oil deposits touch the pump.
        private NodeState NearbyPetroleum(BuildingState b)
        {
            NodeState nearest = null;
            int width = DemoCatalog.Width(b), height = DemoCatalog.Height(b);
            for(int y=b.y-1;y<=b.y+height;y++) for(int x=b.x-1;x<=b.x+width;x++)
            {
                if(DemoCatalog.Contains(b,x,y)) continue;
                NodeState n = NodeAt(x,y);
                if(n != null && n.kind == NodeKind.Oil && n.stock > 0 && n.hp > 0 &&
                    (nearest == null || n.id < nearest.id)) nearest = n;
            }
            return nearest;
        }

        // True means the building is a pump, including when it is blocked or depleted.
        private bool PumpPetrochemical(BuildingState b)
        {
            bool oil = b.kind == BuildKind.OilPump;
            if(!oil && b.kind != BuildKind.ProcessWaterPump) return false;
            if(!PetrochemicalCatalog.IsBuildUnlocked(State,b.kind) || !b.powered || b.powerRatio <= 0) return true;
            NodeState deposit = oil ? NearbyPetroleum(b) : null;
            if(oil ? deposit == null : !NearWater(b)) return true;
            int item = oil ? (int)ItemKind.CrudeOil : (int)ItemKind.ProcessWater;
            int amount = oil ? 1 : 2;
            if(!OutputRoom(b,item,amount)) return true;
            b.progress = Math.Min(2f,b.progress + FixedStep * b.powerRatio);
            if(b.progress + .00001f < 2f) return true;
            if(oil)
            {
                deposit.stock--;
                State.mvExtracted = SaturatingAdd(State.mvExtracted,1);
                State.gathered = SaturatingAdd(State.gathered,1);
                if(deposit.stock == 0) RemoveNode(deposit);
            }
            b.items[item] += amount;
            RecordProduced(item,amount);
            b.progress = 0f;
            return true;
        }

        private void RecordPetrochemicalProduction(BuildingState b, RecipeDefinition recipe)
        {
            switch(recipe.id)
            {
                case 161: State.mvDistilled = SaturatingAdd(State.mvDistilled,4); break;
                case 165: State.mvDiesel = SaturatingAdd(State.mvDiesel,recipe.outputs[(int)ItemKind.Diesel]); break;
                case 70: case 164: State.mvPolymer = SaturatingAdd(State.mvPolymer,recipe.outputs[(int)ItemKind.Polymer]); break;
                case 162: State.mvLubricant = SaturatingAdd(State.mvLubricant,recipe.outputs[(int)ItemKind.Lubricant]); break;
                case 91: State.mvCooled = SaturatingAdd(State.mvCooled,recipe.outputs[(int)ItemKind.Coolant]); break;
            }
            if(b.kind == BuildKind.ChemicalPlant && TechCatalog.EnergyTier(b) == 3 && b.powered && b.powerRatio > 0)
                State.mvPowered = SaturatingAdd(State.mvPowered,recipe.outputs[(int)ItemKind.Polymer]);
        }

        private void RecordPetrochemicalDelivery(BuildingState from, BuildingState to, int item)
        {
            if(PetrochemicalCatalog.IsFluid(item) &&
                (PetrochemicalCatalog.IsLiquidStorage(from.kind) || PetrochemicalCatalog.IsLiquidStorage(to.kind)))
                State.mvPiped = SaturatingAdd(State.mvPiped,1);
        }
    }
}
