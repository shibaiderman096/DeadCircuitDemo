using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public enum MachineModuleRole { Casing=0, Energy=1, Maintenance=2, Input=3, Process=4, Output=5, Controller=6, Exhaust=7 }
    public static class ModularMachineCatalog
    {
        public const int MaxModules=32, MaxSpan=12;
        static readonly string[] Names={ "机壳", "能源舱", "维护舱", "输入总线", "工艺核心", "输出总线", "控制器", "排气口" };
        public static bool IsModular(BuildKind kind) => kind==BuildKind.LargeSteamFurnace || kind==BuildKind.LargeAssemblyPlant || kind==BuildKind.LargeOreProcessor;
        public static bool ValidRole(int role) => role>=0 && role<Names.Length;
        public static int ModuleCount(BuildingState b) => b!=null && IsModular(b.kind) && b.modularModules!=null ? b.modularModules.Count : 0;
        static bool Valid(BuildingState b,int i) => i>=0 && i<ModuleCount(b) && b.modularModules[i]!=null;
        public static MachineModuleRole Role(BuildingState b,int i) => Valid(b,i) ? b.modularModules[i].role : MachineModuleRole.Casing;
        public static string RoleName(BuildingState b,int i) => Valid(b,i) ? RoleName(b.kind,Role(b,i)) : "";
        public static string RoleName(BuildKind kind,MachineModuleRole role) => !ValidRole((int)role) ? "" : role==MachineModuleRole.Exhaust && kind!=BuildKind.LargeSteamFurnace ? "通风口" : Names[(int)role];
        public static int ModuleX(BuildingState b,int i) => Valid(b,i) ? b.modularModules[i].x : throw new ArgumentOutOfRangeException(nameof(i));
        public static int ModuleY(BuildingState b,int i) => Valid(b,i) ? b.modularModules[i].y : throw new ArgumentOutOfRangeException(nameof(i));
        public static int ModuleRotation(BuildingState b,int i) => Valid(b,i) ? b.modularModules[i].rotation : 0;
        public static int ModuleAt(BuildingState b,int x,int y)
        {
            for(int i=0;i<ModuleCount(b);i++) if(Valid(b,i) && ModuleX(b,i)==x && ModuleY(b,i)==y) return i;
            return -1;
        }
        public static int RoleIndex(BuildingState b,MachineModuleRole role)
        {
            for(int i=0;i<ModuleCount(b);i++) if(Valid(b,i) && Role(b,i)==role) return i;
            return -1;
        }
        public static int PortDirection(BuildingState b,MachineModuleRole role)
        { int i=RoleIndex(b,role); return i<0 ? 0 : (ModuleRotation(b,i)+(role==MachineModuleRole.Energy || role==MachineModuleRole.Input ? 2 : 0))&3; }
        static int PortX(BuildingState b,MachineModuleRole role)
        { int i=RoleIndex(b,role); return i<0 ? b.x : ModuleX(b,i)+DemoCatalog.DirectionX(PortDirection(b,role)); }
        static int PortY(BuildingState b,MachineModuleRole role)
        { int i=RoleIndex(b,role); return i<0 ? b.y : ModuleY(b,i)+DemoCatalog.DirectionY(PortDirection(b,role)); }
        public static int EnergyInputX(BuildingState b) => PortX(b,MachineModuleRole.Energy);
        public static int EnergyInputY(BuildingState b) => PortY(b,MachineModuleRole.Energy);
        public static int OutputX(BuildingState b) => PortX(b,MachineModuleRole.Output);
        public static int OutputY(BuildingState b) => PortY(b,MachineModuleRole.Output);
        public static int OutputDirection(BuildingState b) => PortDirection(b,MachineModuleRole.Output);
        public static int ExhaustX(BuildingState b) => PortX(b,MachineModuleRole.Exhaust);
        public static int ExhaustY(BuildingState b) => PortY(b,MachineModuleRole.Exhaust);
        public static int Width(BuildingState b)
        { int max=b.x; for(int i=0;i<ModuleCount(b);i++) if(Valid(b,i)) max=Math.Max(max,ModuleX(b,i)); return max-b.x+1; }
        public static int Height(BuildingState b)
        { int max=b.y; for(int i=0;i<ModuleCount(b);i++) if(Valid(b,i)) max=Math.Max(max,ModuleY(b,i)); return max-b.y+1; }
        public static void RefreshBounds(BuildingState b)
        {
            if(ModuleCount(b)==0) return;
            int x=int.MaxValue,y=int.MaxValue;
            foreach(var m in b.modularModules) { x=Math.Min(x,m.x); y=Math.Min(y,m.y); } b.x=x; b.y=y;
        }
        public static string FormationIssue(BuildingState b)
        {
            if(b==null || !IsModular(b.kind) || ModuleCount(b)==0) return "缺控制器";
            if(ModuleCount(b)>MaxModules || Width(b)>MaxSpan || Height(b)>MaxSpan) return "模块数量或跨度超限";
            var counts=new int[8]; var cells=new HashSet<long>();
            for(int i=0;i<ModuleCount(b);i++)
            {
                var m=b.modularModules[i];
                if(m==null || !ValidRole((int)m.role) || m.rotation<0 || m.rotation>3) return "模块定义无效";
                if(!cells.Add(WorldTiles.Key(m.x,m.y))) return "模块重叠";
                counts[(int)m.role]++;
            }
            foreach(var role in new[] { MachineModuleRole.Controller,MachineModuleRole.Process,MachineModuleRole.Input,MachineModuleRole.Output,MachineModuleRole.Energy,MachineModuleRole.Maintenance,MachineModuleRole.Exhaust })
            {
                if(counts[(int)role]==0) return "缺"+RoleName(b.kind,role);
                if(counts[(int)role]!=1) return RoleName(b.kind,role)+"必须恰好一个";
            }
            if(counts[0]<2) return "缺机壳：至少两格";
            foreach(var role in new[] { MachineModuleRole.Input,MachineModuleRole.Output,MachineModuleRole.Energy,MachineModuleRole.Exhaust })
                if(cells.Contains(WorldTiles.Key(PortX(b,role),PortY(b,role)))) return RoleName(b.kind,role)+"接口朝内";
            var reached=new HashSet<long>(); var queue=new Queue<long>();
            long first=WorldTiles.Key(ModuleX(b,0),ModuleY(b,0)); reached.Add(first); queue.Enqueue(first);
            while(queue.Count>0)
            {
                long key=queue.Dequeue(); int x=WorldTiles.X(key),y=WorldTiles.Y(key);
                for(int d=0;d<4;d++)
                {
                    long n=WorldTiles.Key(x+DemoCatalog.DirectionX(d),y+DemoCatalog.DirectionY(d));
                    if(cells.Contains(n) && reached.Add(n)) queue.Enqueue(n);
                }
            }
            return reached.Count==ModuleCount(b) ? "" : "模块断连：需要四邻接连通";
        }
        public static int[] ModuleCost(BuildingState b,int i) => Valid(b,i) ? ModuleCost(b.kind,Role(b,i)) : new int[DemoCatalog.ItemCount];
        public static int[] ModuleCost(BuildKind kind,MachineModuleRole role)
        {
            if(!IsModular(kind) || !ValidRole((int)role)) return new int[DemoCatalog.ItemCount];
            bool steam=kind==BuildKind.LargeSteamFurnace;
            switch(role)
            {
                case MachineModuleRole.Casing: return DemoCatalog.Materials(ItemKind.SteelPlate,2,ItemKind.RefractoryBrick,2);
                case MachineModuleRole.Energy: return steam ? DemoCatalog.Materials(ItemKind.SteelPlate,1,ItemKind.PipeFitting,2) : DemoCatalog.Materials(ItemKind.SteelPlate,1,ItemKind.Cable,2);
                case MachineModuleRole.Maintenance: return DemoCatalog.Materials(ItemKind.IronPlate,1,ItemKind.Bearing,1);
                case MachineModuleRole.Input: case MachineModuleRole.Output: return DemoCatalog.Materials(ItemKind.IronPlate,2,ItemKind.Gear,1);
                case MachineModuleRole.Process: return steam ? DemoCatalog.Materials(ItemKind.RefractoryBrick,4,ItemKind.PipeFitting,2) : DemoCatalog.Materials(ItemKind.SteelPlate,2,ItemKind.Motor,2,ItemKind.Gear,2);
                case MachineModuleRole.Controller: return steam ? DemoCatalog.Materials(ItemKind.Bronze,2,ItemKind.Gear,1) : DemoCatalog.Materials(ItemKind.Circuit,2,ItemKind.Cable,2);
                default: return DemoCatalog.Materials(ItemKind.Brick,2,ItemKind.PipeFitting,1);
            }
        }
        // Minimum role-set quotation, not a reserved footprint or placement charge.
        public static int[] TotalCost(BuildKind kind)
        {
            var total=ModuleCost(kind,MachineModuleRole.Casing);
            for(int role=0;role<8;role++) { var cost=ModuleCost(kind,(MachineModuleRole)role); for(int i=0;i<total.Length;i++) total[i]+=cost[i]; }
            return total;
        }
        public static bool ModuleComplete(BuildingState b,int index)
        {
            if(!Valid(b,index)) return false;
            var paid=b.modularModules[index].paid; var cost=ModuleCost(b,index);
            if(paid==null || paid.Length!=cost.Length) return false;
            for(int i=0;i<cost.Length;i++) if(paid[i]!=cost[i]) return false;
            return true;
        }
        public static int CompletedModules(BuildingState b)
        { int count=0; for(int i=0;i<ModuleCount(b);i++) if(ModuleComplete(b,i)) count++; return count; }
        public static bool IsComplete(BuildingState b) => ModuleCount(b)>0 && FormationIssue(b).Length==0 && CompletedModules(b)==ModuleCount(b);
        public static int[] RequiredItems(BuildingState b)
        {
            var total=new int[DemoCatalog.ItemCount];
            for(int m=0;m<ModuleCount(b);m++)
            {
                var cost=ModuleCost(b,m); var paid=b.modularModules[m].paid;
                for(int i=0;i<total.Length;i++) total[i]+=Math.Max(0,cost[i]-(paid!=null && paid.Length==total.Length ? paid[i] : 0));
            }
            return total;
        }
        public static bool HasWrench(PlayerState p)
        {
            if(p==null || p.activeSlot!=1 || p.toolId<=0 || p.gear==null) return false;
            return p.gear.Exists(g=>g!=null && g.id==p.toolId && g.kind==GearKind.Wrench && g.durability>0);
        }
        public static bool ExhaustClear(WorldState s,BuildingState b)
        {
            if(s==null || b==null || RoleIndex(b,MachineModuleRole.Exhaust)<0) return false;
            if(b.kind!=BuildKind.LargeSteamFurnace) return true;
            int x=ExhaustX(b),y=ExhaustY(b);
            if(!WorldTiles.Inside(s,x,y) || WorldTiles.Ground(s,x,y)=='?') return false;
            foreach(var n in s.nodes) if(n.x==x && n.y==y && n.hp>0f && n.stock>0) return false;
            foreach(var other in s.buildings) if(other!=b && other.hp>0f && DemoCatalog.Contains(other,x,y)) return false;
            return ModuleAt(b,x,y)<0;
        }
        public static ItemKind MaintenanceItem(BuildKind kind) => kind==BuildKind.LargeSteamFurnace ? ItemKind.Bronze : ItemKind.Bearing;
        public static string Status(WorldState s,BuildingState b)
        {
            if(b==null || !IsModular(b.kind)) return "";
            string issue=FormationIssue(b);
            if(issue.Length>0) return "蓝图未形成："+issue;
            if(!IsComplete(b)) return "模块施工 "+CompletedModules(b)+"/"+ModuleCount(b);
            if(b.hp<WorldSimulator.BuildingMaxHealth(b.kind)) return "结构损坏：需手持扳手修复";
            if(s==null || !TechCatalog.IsUnlocked(s,b.kind)) return "研究未解锁";
            if(!ExhaustClear(s,b)) return "排气阻塞：请清空排气口外侧格";
            var recipe=TechCatalog.SelectedRecipe(b);
            if(recipe==null || !PrimitiveCatalog.IsRecipeUnlocked(s,recipe)) return "配方研究未解锁";
            int maintenance=(int)MaintenanceItem(b.kind);
            if(b.modularService==0 && b.items[maintenance]<=recipe.inputs[maintenance]) return "缺维护耗材："+DemoCatalog.ItemName(MaintenanceItem(b.kind));
            for(int i=0;i<DemoCatalog.ItemCount;i++) if(b.items[i]<recipe.inputs[i]) return "待输入："+DemoCatalog.ItemNames[i];
            for(int i=0;i<DemoCatalog.ItemCount;i++)
            {
                int reserved=0; foreach(var c in s.cargo) if(c.toId==b.id && c.item==i) reserved++;
                if(b.items[i]-recipe.inputs[i]+recipe.outputs[i]+reserved>WorldSimulator.BufferLimit) return "输出阻塞："+DemoCatalog.ItemNames[i];
            }
            if(!b.powered || b.powerRatio<=0f) return "能源不足：检查能源舱外侧接入";
            return b.powerRatio<1f ? "能源不足：按供能比例运行" : "运行中";
        }
    }
}
