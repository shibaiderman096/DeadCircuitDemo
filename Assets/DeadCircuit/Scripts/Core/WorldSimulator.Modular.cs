using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private static int ModuleFloor(WorldState world,int x,int y)
        {
            return ChunkStreaming.Enabled(world) && WorldTiles.TryChunk(world,WorldTiles.FloorDiv(x,16),WorldTiles.FloorDiv(y,16),out var chunk) ? chunk.interiorSiteId : 0;
        }
        private bool ModularEditable(BuildingState b) => b.progress==0f && TotalItems(b.items)==0 && !State.cargo.Exists(c=>c.fromId==b.id || c.toId==b.id);
        private void EditModularPlan(PlayerState p,PlayerCommand cmd,BuildingState b)
        {
            if(b==null || !ModularMachineCatalog.IsModular(b.kind) || !TechCatalog.IsUnlocked(State,b.kind)) return;
            if(!ModularMachineCatalog.HasWrench(p)) { Notice(p,"编辑模块需要手持耐久正常的扳手。"); return; }
            if(cmd.action=="moduleunload")
            {
                int module=ModularMachineCatalog.ModuleAt(b,cmd.x,cmd.y);
                if(module<0 || ModularMachineCatalog.Role(b,module)!=MachineModuleRole.Controller) { Notice(p,"请对准控制器执行停机清空。"); return; }
                if(State.cargo.Exists(c=>c.fromId==b.id || c.toId==b.id)) { Notice(p,"仍有在途物品，送达后才能停机清空。"); return; }
                for(int i=0;i<b.items.Length;i++) if(b.items[i]>InventoryLimit-p.items[i]) { Notice(p,"背包空间不足，停机清空未执行。"); return; }
                for(int i=0;i<b.items.Length;i++) { p.items[i]+=b.items[i]; b.items[i]=0; }
                b.progress=0f; RefreshPower(false); Notice(p,"已停机清空：取消本批加工并取回全部库存，已消耗维护不返还。"); return;
            }
            if(!ModularEditable(b)) { Notice(p,"编辑前须清空整机库存、生产进度及在途物品。"); return; }
            if(ModuleFloor(State,cmd.x,cmd.y)!=ModuleFloor(State,b.x,b.y) || ModuleFloor(State,RoundCell(p.x),RoundCell(p.y))!=ModuleFloor(State,b.x,b.y))
            { Notice(p,"模块与施工者须处于同一楼层。"); return; }
            if(cmd.action=="moduleerase")
            {
                int index=ModularMachineCatalog.ModuleAt(b,cmd.x,cmd.y);
                if(index<0) return;
                var m=b.modularModules[index];
                if(m.role==MachineModuleRole.Controller && b.modularModules.Count>1) { Notice(p,"请先擦除其他模块，再擦除控制器。"); return; }
                for(int i=0;i<m.paid.Length;i++) if(m.paid[i]>InventoryLimit-p.items[i]) { Notice(p,"背包空间不足，无法完整退还本格建材。"); return; }
                for(int i=0;i<m.paid.Length;i++) p.items[i]+=m.paid[i];
                b.modularModules.RemoveAt(index);
                if(b.modularModules.Count==0) State.buildings.Remove(b);
                else ModularMachineCatalog.RefreshBounds(b);
                Notice(p,"已擦除模块并精确退还已付建材。");
            }
            else
            {
                if(!ModularMachineCatalog.ValidRole(cmd.arg) || cmd.rotation<0 || cmd.rotation>3) return;
                var role=(MachineModuleRole)cmd.arg;
                if(b.modularModules.Count>=ModularMachineCatalog.MaxModules) { Notice(p,"每台最多32个模块。"); return; }
                if(role!=MachineModuleRole.Casing && ModularMachineCatalog.RoleIndex(b,role)>=0) { Notice(p,"该职责模块只能放置一个。"); return; }
                int maxX=b.x+DemoCatalog.Width(b)-1,maxY=b.y+DemoCatalog.Height(b)-1;
                if((long)Math.Max(maxX,cmd.x)-Math.Min(b.x,cmd.x)>=ModularMachineCatalog.MaxSpan ||
                    (long)Math.Max(maxY,cmd.y)-Math.Min(b.y,cmd.y)>=ModularMachineCatalog.MaxSpan) { Notice(p,"模块跨度不能超过12×12。"); return; }
                if(BlockedAt(cmd.x,cmd.y) || BuildingAt(cmd.x,cmd.y)!=null || FloorWorld.IsProtectedCell(State,cmd.x,cmd.y) ||
                    SurvivalOccupiesCell(cmd.x,cmd.y) || NearSpawn(cmd.x,cmd.y) || OccupiedByActor(cmd.x,cmd.y))
                { Notice(p,"此格有障碍、人物或楼梯，不能规划模块。"); return; }
                b.modularModules.Add(new MachineModuleState {role=role,x=cmd.x,y=cmd.y,rotation=cmd.rotation});
                ModularMachineCatalog.RefreshBounds(b);
                Notice(p,"已放置模块蓝图："+ModularMachineCatalog.RoleName(b.kind,role));
            }
            powerLayout.Clear(); RefreshSpatial(); RefreshPower(false); flowRevision=-1;
        }

        private void InteractModular(PlayerState p,BuildingState b,int x,int y)
        {
            int module=ModularMachineCatalog.ModuleAt(b,x,y);
            if(module<0) return;
            if(!ModularMachineCatalog.IsComplete(b))
            {
                if(!ModularMachineCatalog.HasWrench(p)) { Notice(p,"施工需要手持耐久正常的扳手。"); return; }
                int[] cost=ModularMachineCatalog.ModuleCost(b,module); int moved=0;
                for(int i=0;i<cost.Length;i++)
                {
                    int take=Math.Min(p.items[i],cost[i]-b.modularModules[module].paid[i]);
                    if(take<=0) continue;
                    b.modularModules[module].paid[i]+=take; p.items[i]-=take; moved+=take;
                }
                Notice(p,(moved>0 ? "已投入建材；" : "此格无可投入建材；")+ModularMachineCatalog.Status(State,b));
                RefreshPower(false); return;
            }
            int transferred=0; RecipeDefinition r=TechCatalog.SelectedRecipe(b);
            switch(ModularMachineCatalog.Role(b,module))
            {
                case MachineModuleRole.Input:
                    if(r!=null) for(int i=0;i<DemoCatalog.ItemCount;i++) if(r.inputs[i]>0) transferred+=TransferIntoBuilding(p.items,b,i,20);
                    break;
                case MachineModuleRole.Output:
                    // Preserve buffers from an old recipe, but never take selected recipe inputs.
                    for(int i=0;i<DemoCatalog.ItemCount;i++) if(IsOutput(b.kind,i) && (r==null || r.inputs[i]==0))
                        transferred+=Transfer(b.items,p.items,i,BufferLimit,InventoryLimit);
                    break;
                case MachineModuleRole.Maintenance:
                    if(!ModularMachineCatalog.HasWrench(p)) { Notice(p,"维护需要手持耐久正常的扳手。"); return; }
                    if(b.hp<BuildingMaxHealth(b.kind)) { RepairModular(p,b); return; }
                    transferred=TransferIntoBuilding(p.items,b,(int)ModularMachineCatalog.MaintenanceItem(b.kind),8);
                    break;
            }
            Notice(p,transferred>0 ? "已转移 "+transferred+" 件；"+ModularMachineCatalog.Status(State,b) : ModularMachineCatalog.RoleName(b,module)+"："+ModularMachineCatalog.Status(State,b));
            RefreshPower(false);
        }

        private void RepairModular(PlayerState p,BuildingState b)
        {
            if(!ModularMachineCatalog.HasWrench(p)) { Notice(p,"维护修复需要手持耐久正常的扳手。"); return; }
            if(b.hp>=BuildingMaxHealth(b.kind)) return;
            const int amount=1; int material=(int)ItemKind.SteelPlate;
            if(p.items[material]<amount) { Notice(p,"结构修复需要钢板×1。"); return; }
            p.items[material]-=amount; b.hp=BuildingMaxHealth(b.kind);
            Notice(p,"已用钢板修复整机结构。"); RefreshPower(false);
        }

        private void DismantleModular(PlayerState p,BuildingState b)
        {
            if(!ModularMachineCatalog.HasWrench(p)) { Notice(p,"拆除蓝图需要手持耐久正常的扳手。"); return; }
            var returned=(int[])b.items.Clone();
            foreach(var module in b.modularModules) for(int i=0;i<returned.Length;i++) returned[i]+=module.paid[i];
            foreach(var c in State.cargo) if(c.fromId==b.id || c.toId==b.id) returned[c.item]++;
            for(int i=0;i<returned.Length;i++) if(returned[i]>InventoryLimit-p.items[i])
            { Notice(p,"背包空间不足，无法完整收回建材、库存及在途物品。"); return; }
            for(int i=0;i<returned.Length;i++) p.items[i]+=returned[i];
            State.cargo.RemoveAll(c=>c.fromId==b.id || c.toId==b.id); State.buildings.Remove(b);
            RefreshSpatial(); RefreshPower(false); flowRevision=-1;
            Notice(p,"已精确收回已付建材、库存及在途物品；已消耗维护不返还。");
        }

        // Shared by topology allocation and production. Incoming output reservations are counted
        // before requesting energy, so waiting machines never load the grid.
        private bool ModularReadyForPower(BuildingState b)
        {
            if(!ModularMachineCatalog.IsComplete(b) || b.hp<BuildingMaxHealth(b.kind) || !TechCatalog.IsUnlocked(State,b.kind)) return false;
            if(!ModularMachineCatalog.ExhaustClear(State,b)) return false;
            var r=TechCatalog.SelectedRecipe(b);
            if(r==null || r.building!=b.kind || !PrimitiveCatalog.IsRecipeUnlocked(State,r)) return false;
            int maintenance=(int)ModularMachineCatalog.MaintenanceItem(b.kind);
            if(b.items[maintenance]<r.inputs[maintenance]+(b.modularService==0 ? 1 : 0)) return false;
            for(int i=0;i<DemoCatalog.ItemCount;i++)
            {
                int incoming=0; foreach(var c in State.cargo) if(c.toId==b.id && c.item==i) incoming++;
                if(b.items[i]<r.inputs[i] || b.items[i]-r.inputs[i]+r.outputs[i]+incoming>BufferLimit) return false;
            }
            return true;
        }
        private static bool ModularPowerPort(PowerLayout consumer,PowerLayout node)
        {
            var b=consumer.building;
            return DemoCatalog.Contains(node.building,ModularMachineCatalog.EnergyInputX(b),ModularMachineCatalog.EnergyInputY(b));
        }
        private bool UpdateModularProduction(BuildingState b)
        {
            if(!ModularMachineCatalog.IsModular(b.kind)) return false;
            if(!b.powered || b.powerRatio<=0f || !ModularReadyForPower(b)) return true;
            var r=TechCatalog.SelectedRecipe(b);
            // Rated throughput is two ordinary machines at twice the ordinary demand.
            // Engineering must not increase output at the same rated energy.
            b.progress=Math.Min(r.duration,b.progress+FixedStep*b.powerRatio);
            if(b.progress+.00001f<r.duration) return true;
            if(b.modularService==0) { b.items[(int)ModularMachineCatalog.MaintenanceItem(b.kind)]--; b.modularService=8; }
            b.modularService--; Pay(b.items,r.inputs);
            for(int i=0;i<DemoCatalog.ItemCount;i++) { b.items[i]+=r.outputs[i]; RecordProduced(i,r.outputs[i]); State.manufactured=SaturatingAdd(State.manufactured,r.outputs[i]); }
            RecordSteamProduction(b,r); RecordLowVoltageProduction(b,r); b.progress=0f; return true;
        }

        // Port authority is shared by live cargo planning, delivery and save validation.
        private static bool ModularCargoRouteValid(BuildingState from,BuildingState to,int item)
        {
            if(!ModularMachineCatalog.IsModular(from.kind) && !ModularMachineCatalog.IsModular(to.kind)) return true;
            if(ModularMachineCatalog.IsModular(from.kind) && !ModularMachineCatalog.IsComplete(from)) return false;
            if(ModularMachineCatalog.IsModular(from.kind))
            {
                var output=TechCatalog.SelectedRecipe(from);
                if(!IsOutput(from.kind,item) || (output!=null && output.inputs[item]>0)) return false;
            }
            if(!ModularMachineCatalog.IsModular(to.kind)) return true;
            if(!ModularMachineCatalog.IsComplete(to)) return false;
            int direction=from.kind==BuildKind.FilterSplitter && item!=from.setting ? (from.rotation+1)&3 : CargoOutputDirection(from,item);
            int x=from.kind==BuildKind.FilterSplitter ? from.x+DirectionX[direction] : CargoOutputX(from,item);
            int y=from.kind==BuildKind.FilterSplitter ? from.y+DirectionY[direction] : CargoOutputY(from,item);
            var r=TechCatalog.SelectedRecipe(to);
            return x==ModularMachineCatalog.ModuleX(to,ModularMachineCatalog.RoleIndex(to,MachineModuleRole.Input)) &&
                y==ModularMachineCatalog.ModuleY(to,ModularMachineCatalog.RoleIndex(to,MachineModuleRole.Input)) &&
                direction==ModularMachineCatalog.ModuleRotation(to,ModularMachineCatalog.RoleIndex(to,MachineModuleRole.Input)) && r!=null && r.inputs[item]>0;
        }

        private static List<MachineModuleState> ValidateModularState(WorldState world,BuildingState b)
        {
            var modules=new List<MachineModuleState>();
            if(!ModularMachineCatalog.IsModular(b.kind))
            {
                SaveCheck((b.modularModules==null || b.modularModules.Count==0) && b.modularService==0,"module state in ordinary building");
                return modules;
            }
            SaveCheck(world.version==SaveVersion && b.footprintVersion==1,"modular machine requires current format and physical footprint");
            SaveCheck(TechCatalog.IsUnlocked(world,b.kind),"unresearched modular machine");
            SaveCheck(b.modularModules!=null && b.modularModules.Count>=1 && b.modularModules.Count<=ModularMachineCatalog.MaxModules,"module list length");
            int minX=int.MaxValue,minY=int.MaxValue,maxX=int.MinValue,maxY=int.MinValue,controllers=0;
            var roles=new HashSet<MachineModuleRole>(); var cells=new HashSet<long>();
            foreach(var m in b.modularModules)
            {
                SaveCheck(m!=null && ModularMachineCatalog.ValidRole((int)m.role) && m.rotation>=0 && m.rotation<=3,"module definition");
                CheckCell(m.x,m.y,world);
                SaveCheck(cells.Add(WorldTiles.Key(m.x,m.y)),"duplicate module cell");
                SaveCheck(m.role==MachineModuleRole.Casing || roles.Add(m.role),"duplicate functional module");
                if(m.role==MachineModuleRole.Controller) controllers++;
                minX=Math.Min(minX,m.x); minY=Math.Min(minY,m.y); maxX=Math.Max(maxX,m.x); maxY=Math.Max(maxY,m.y);
                var cost=ModularMachineCatalog.ModuleCost(b.kind,m.role);
                SaveCheck(m.paid!=null && m.paid.Length==DemoCatalog.ItemCount,"module payment array length");
                for(int i=0;i<cost.Length;i++) SaveCheck(m.paid[i]>=0 && m.paid[i]<=cost[i],"module payment outside exact cost");
                SaveCheck(ModuleFloor(world,m.x,m.y)==ModuleFloor(world,b.x,b.y),"machine modules cross floors");
                modules.Add(new MachineModuleState {role=m.role,x=m.x,y=m.y,rotation=m.rotation,paid=(int[])m.paid.Clone()});
            }
            SaveCheck(controllers==1 && minX==b.x && minY==b.y && (long)maxX-minX<ModularMachineCatalog.MaxSpan && (long)maxY-minY<ModularMachineCatalog.MaxSpan,"module controller or bounds");
            SaveCheck(b.modularService>=0 && b.modularService<8,"module maintenance reserve");
            if(!ModularMachineCatalog.IsComplete(b))
            {
                SaveCheck(b.progress==0f && b.fuel==0f,"unfinished machine runtime state");
                SaveCheck(b.items!=null && b.items.Length==DemoCatalog.ItemCount,"unfinished inventory length");
                foreach(int n in b.items) SaveCheck(n==0,"unfinished machine has usable inventory");
            }
            if(b.progress>0f)
            {
                var r=TechCatalog.SelectedRecipe(b);
                SaveCheck(r!=null && r.building==b.kind && b.items!=null && b.items.Length==DemoCatalog.ItemCount,"modular progress recipe");
                for(int i=0;i<DemoCatalog.ItemCount;i++) SaveCheck(b.items[i]>=r.inputs[i],"modular progress without inputs");
                int maintenance=(int)ModularMachineCatalog.MaintenanceItem(b.kind);
                SaveCheck(b.modularService>0 || b.items[maintenance]>r.inputs[maintenance],"modular progress without maintenance");
            }
            return modules;
        }
    }
}
