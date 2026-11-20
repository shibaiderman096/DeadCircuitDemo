using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private struct ItemTransfer
        {
            public BuildingState from, to;
            public int item, x, y, direction;
        }

        private sealed class PowerLayout
        {
            public BuildingState building;
            public int id, x, y, width, height, tier, setting;
            public BuildKind kind;
            public bool unlocked;
            public PowerNetworkState network;
            public PowerNetworkState inputNetwork;
        }

        private readonly List<BuildingState> productionOrder = new List<BuildingState>();
        private readonly List<PowerLayout> powerLayout = new List<PowerLayout>();
        private readonly List<PowerNetworkState> powerComponents = new List<PowerNetworkState>();
        private readonly Dictionary<int, BuildingState> cargoBuildings = new Dictionary<int, BuildingState>();
        private readonly Dictionary<int, int[]> cargoIncoming = new Dictionary<int, int[]>();
        private readonly Dictionary<int, int> cargoReturnItems = new Dictionary<int, int>();
        private readonly List<int[]> cargoReservationPool = new List<int[]>();
        private readonly HashSet<int> cargoBusySources = new HashSet<int>();
        private readonly List<ItemTransfer> cargoPlans = new List<ItemTransfer>();
        private int cargoReservationCount;

        private void Interact(PlayerState p, int x, int y, int arg)
        {
            if (TryFloorInteraction(p, x, y)) return;
            BuildingState b = BuildingAt(x, y);
            if (b == null)
            {
                NodeState n = NodeAt(x, y);
                if (n == null) { Notice(p, "靠近树、矿、废料堆或补给箱，瞄准后按 E。"); return; }
                int item = n.kind == NodeKind.Crate ? SiteCrateItem(n) : NodeItem(n.kind);
                GearState tool=EquipmentCatalog.Equipped(p);
                int bonus=HarvestBonus(tool,n.kind);
                int amount = Math.Min(n.stock, Math.Min(InventoryLimit - p.items[item], 3 + p.attributes[0] / 3 + (p.skills[2] >= 1 ? p.skills[2] : 0) + bonus));
                if (amount <= 0) { Notice(p, "背包已满。"); return; }
                n.stock -= amount; p.items[item] += amount;
                if(tool!=null && EquipmentCatalog.Gear(tool.kind).tool && tool.durability>0) tool.durability--;
                State.gathered = SaturatingAdd(State.gathered, amount);
                GainXp(p, amount * 2);
                p.interactCooldown = 0.4f * (1f - p.attributes[0] * 0.025f);
                if(bonus>0) p.interactCooldown*=bonus>=5 ? .5f : .75f;
                string forage = "";
                if (n.stock == 0) { forage = AwardForageLoot(p, n); RemoveNode(n); }
                Notice(p, "采集 " + DemoCatalog.ItemNames[item] + " +" + amount + forage);
                return;
            }
            int moved = 0;
            if (InteractElectrical(p, b, arg)) return;
            if(b.kind==BuildKind.Door) { SetDoor(p,b,1-b.setting); return; }
            if (b.kind != BuildKind.Chest && b.kind != BuildKind.MechanicalHopper && b.kind != BuildKind.Belt && b.kind != BuildKind.Wall && b.kind != BuildKind.Turret)
            {
                RecipeDefinition recipe=TechCatalog.SelectedRecipe(b);
                // Reconfiguration never deletes old buffers: unload all possible outputs first.
                for(int i=0;i<DemoCatalog.ItemCount;i++) if(IsOutput(b.kind,i))
                    moved += Transfer(b.items,p.items,i,BufferLimit,InventoryLimit);
                if(b.kind==BuildKind.CombustionChamber)
                {
                    foreach(ItemKind fuel in new[] { ItemKind.Coal, ItemKind.Coke, ItemKind.Fuel })
                    {
                        int added=TransferIntoBuilding(p.items,b,(int)fuel,8);
                        moved+=added;
                        if(added>0) break;
                    }
                }
                else if(TechCatalog.IsFuelBurner(b.kind) && !(b.kind==BuildKind.Boiler && b.modularBoiler)) moved += TransferIntoBuilding(p.items,b,FuelItem(b),8);
                if(b.kind==BuildKind.Reactor)
                {
                    moved += TransferIntoBuilding(p.items,b,(int)ItemKind.NuclearFuel,4);
                    moved += TransferIntoBuilding(p.items,b,(int)ItemKind.Coolant,20);
                }
                if(recipe!=null && recipe.building==b.kind)
                    for(int i=0;i<DemoCatalog.ItemCount;i++) if(recipe.inputs[i]>0) moved += TransferIntoBuilding(p.items,b,i,20);
                Notice(p,moved>0 ? "设备交互完成（转移 "+moved+" 件）。" : DemoCatalog.BuildDescriptions[(int)b.kind]); return;
            }
            switch (b.kind)
            {
                case BuildKind.Generator:
                    moved = TransferIntoBuilding(p.items, b, (int)ItemKind.Fuel, 8);
                    Notice(p, moved > 0 ? "添加燃料 " + moved + "（120 kW，每份运行20秒；节点7格连线，供电覆盖占地外4格）。" : "需携带燃料；3木材可制作2燃料。"); return;
                case BuildKind.Miner:
                    moved = Transfer(b.items, p.items, (int)ItemKind.Ore, BufferLimit, InventoryLimit); break;
                case BuildKind.Smelter:
                    moved += TransferIntoBuilding(p.items, b, (int)ItemKind.Ore, 20);
                    moved += Transfer(b.items, p.items, (int)ItemKind.Iron, BufferLimit, InventoryLimit); break;
                case BuildKind.Assembler:
                    moved += TransferIntoBuilding(p.items, b, (int)ItemKind.Iron, 20);
                    moved += Transfer(b.items, p.items, (int)ItemKind.Ammo, BufferLimit, InventoryLimit); break;
                case BuildKind.Turret:
                    moved = TransferIntoBuilding(p.items, b, (int)ItemKind.Ammo, 40);
                    Notice(p, moved > 0 ? "装填炮塔弹药 " + moved : "炮塔需供电及弹药；E装填，弹药也可从相邻朝向它的箱/带自动输入。"); return;
                case BuildKind.Chest:
                    for (int i = 0; i < DemoCatalog.ItemCount; i++) moved += Transfer(b.items, p.items, i, BufferLimit, InventoryLimit); break;
                case BuildKind.MechanicalHopper:
                    for (int i = 0; i < DemoCatalog.ItemCount; i++) moved += Transfer(b.items, p.items, i, BufferLimit, InventoryLimit);
                    if (moved == 0 && arg >= 0 && arg < DemoCatalog.ItemCount)
                        moved = TransferIntoBuilding(p.items, b, arg, 20);
                    break;
                case BuildKind.Belt:
                    for (int i = 0; i < DemoCatalog.ItemCount; i++) moved += Transfer(b.items, p.items, i, 2, InventoryLimit);
                    if (moved == 0 && TotalItems(b.items) == 0 && arg >= 0 && arg < DemoCatalog.ItemCount)
                        moved = TransferIntoBuilding(p.items, b, arg, 1);
                    break;
                case BuildKind.Wall: Notice(p, "墙体可阻挡丧尸；使用修理操作消耗1木材恢复耐久。"); return;
                case BuildKind.PowerPole: Notice(p, "电线杆连接7格内的供电节点，为占地边缘4格内的用电设备供电。"); return;
                case BuildKind.SolarPanel: Notice(p, "太阳能板白昼最高40 kW，夜间停机；连接电线杆可扩展供电网络。"); return;
            }
            Notice(p, moved > 0 ? "设备交互完成（转移 " + moved + " 件）。" : "暂无可转移物资；检查输入、供电与背包余量。");
        }

        private static int Transfer(int[] from, int[] to, int item, int maximum, int limit)
        {
            int moved = Math.Max(0, Math.Min(maximum, Math.Min(from[item], limit - to[item])));
            from[item] -= moved; to[item] += moved; return moved;
        }
        private int TransferIntoBuilding(int[] from, BuildingState to, int item, int maximum)
        {
            int reserved = 0, totalReserved = 0, returning = 0;
            foreach (CargoState c in State.cargo)
            {
                if (c.toId == to.id)
                {
                    totalReserved++;
                    if (c.item == item) reserved++;
                }
                if (ReservesCargoReturn(to) && c.fromId == to.id && c.item == item) returning++;
            }
            if (to.kind == BuildKind.Belt) maximum = Math.Min(maximum, 2 - TotalItems(to.items) - totalReserved);
            return Transfer(from, to.items, item, maximum, BufferLimit - reserved - returning);
        }
        private static int TotalItems(int[] items) { int result = 0; for (int i = 0; i < items.Length; i++) result += items[i]; return result; }
        private static int SaturatingAdd(int current, int amount) { return current > int.MaxValue - amount ? int.MaxValue : current + amount; }

        private bool NearWater(BuildingState b) => TechCatalog.NearWater(State,b);
        private void RefreshPower(bool consume)
        {
            productionOrder.Clear();
            foreach(BuildingState b in State.buildings)
            {
                b.networkId=0; b.powerRatio=b.powerDemand=b.powerGeneration=0f; b.powered=false;
                if(b.hp>0f) productionOrder.Add(b);
            }
            productionOrder.Sort(CompareProductionIds);
            bool changed=productionOrder.Count!=powerLayout.Count;
            for(int i=0;!changed && i<productionOrder.Count;i++)
            {
                BuildingState b=productionOrder[i]; PowerLayout old=powerLayout[i];
                changed=old.building!=b || old.id!=b.id || old.kind!=b.kind || old.x!=b.x || old.y!=b.y || old.setting!=b.setting || old.width!=DemoCatalog.Width(b) || old.height!=DemoCatalog.Height(b) || old.unlocked!=WaterPowerUnlocked(b);
            }
            if(changed) RebuildPowerLayout();
            State.powerNetworks.Clear();
            foreach(var n in powerComponents) { n.generation=n.demand=n.ratio=0f; State.powerNetworks.Add(n); }
            RefreshSteamClusters(consume);
            float sunlight=(float)Math.Max(0.0,Math.Cos((State.elapsed%State.dayLength)/State.dayLength*Math.PI*2.0));
            if(sunlight<.000001f) sunlight=0f;
            foreach(var l in powerLayout)
            {
                if(!l.unlocked) continue;
                BuildingState b=l.building;
                b.powerDemand=b.kind==BuildKind.Transformer ? 200f : DemoCatalog.Demand(b.kind);
                switch(b.kind)
                {
                    case BuildKind.Generator: b.powerGeneration=BurnFuel(b,consume,120f,20f); break;
                    case BuildKind.DieselGenerator: b.powerGeneration=BurnFuel(b,consume,240f,20f); break;
                    case BuildKind.HighGenerator: b.powerGeneration=BurnFuel(b,consume,600f,10f); break;
                    case BuildKind.SolarPanel: b.powerGeneration=40f*sunlight; break;
                    case BuildKind.Waterwheel: b.powerGeneration=NearWater(b) ? 40f : 0f; break;
                    case BuildKind.Boiler:
                        if(!b.modularBoiler) b.powerGeneration=NearWater(b) ? BurnFuel(b,consume,120f,20f) : 0f;
                        break;
                    case BuildKind.Reactor: b.powerGeneration=ReactorOutput(b,consume); break;
                }
                if(l.network!=null)
                {
                    b.networkId=l.network.id; l.network.generation+=b.powerGeneration;
                    if(!Converter(b.kind)) l.network.demand+=b.powerDemand;
                }
                if(l.inputNetwork!=null) l.inputNetwork.demand+=b.powerDemand;
            }
            // Only downward conversions; input demand is included before output allocation.
            ResolveTier(1);
            foreach(var l in powerLayout) if(l.kind==BuildKind.SteamEngine) ConvertPower(l,.8f);
            ResolveTier(0);
            foreach(var l in powerLayout) if(l.kind==BuildKind.SteamTurbine) ConvertPower(l,.8f);
            ResolveTier(4);
            foreach(var l in powerLayout) if(l.kind==BuildKind.Transformer && l.setting==1) ConvertPower(l,.9f);
            ResolveTier(3);
            foreach(var l in powerLayout) if(l.kind==BuildKind.Transformer && l.setting==0) ConvertPower(l,.9f);
            ResolveTier(2);
            ApplyElectricalProtection(consume);
            foreach(var l in powerLayout)
            {
                if(!l.unlocked) continue;
                BuildingState b=l.building;
                b.powerRatio=Converter(b.kind) ? (l.inputNetwork==null ? 0f : l.inputNetwork.ratio) : l.tier<0 ? 1f : l.network==null ? 0f : l.network.ratio;
                b.powered=b.powerRatio>0f;
            }
        }
        private void ResolveTier(int tier)
        {
            foreach(var n in powerComponents) if(n.tier==tier) n.ratio=n.demand>0f ? Clamp(n.generation/n.demand,0f,1f) : n.generation>0f ? 1f : 0f;
        }
        private static bool Converter(BuildKind kind) => kind==BuildKind.Transformer || kind==BuildKind.SteamTurbine || kind==BuildKind.SteamEngine;
        private static void ConvertPower(PowerLayout l,float efficiency)
        {
            l.building.powerGeneration=l.inputNetwork==null ? 0f : l.building.powerDemand*l.inputNetwork.ratio*efficiency;
            if(l.network!=null) l.network.generation+=l.building.powerGeneration;
        }
        private int FuelItem(BuildingState b)
        {
            if(b.kind==BuildKind.CombustionChamber)
            {
                if(b.items[(int)ItemKind.Coal]>0) return (int)ItemKind.Coal;
                if(b.items[(int)ItemKind.Coke]>0) return (int)ItemKind.Coke;
            }
            return b.kind==BuildKind.StoneFurnace && PrimitiveCatalog.Enabled(State) ? (int)ItemKind.Charcoal : (int)ItemKind.Fuel;
        }
        private float BurnFuel(BuildingState b,bool consume,float output,float duration)
        {
            const float tolerance=.0001f;
            int fuelItem=FuelItem(b);
            float seconds=b.fuel<tolerance ? 0f : b.fuel;
            if(seconds+tolerance<FixedStep && b.items[fuelItem]>0)
            {
                if(consume) b.items[fuelItem]--;
                seconds+=duration;
            }
            float generation=output*(seconds+tolerance>=FixedStep ? 1f : Clamp(seconds/FixedStep,0f,1f));
            if(consume) { b.fuel=Math.Max(0f,seconds-FixedStep); if(b.fuel<tolerance) b.fuel=0f; }
            return generation;
        }
        private float ReactorOutput(BuildingState b,bool consume)
        {
            // Latch the hot input before passive cooling can hide an exact-limit trip.
            bool tripped=b.tripped || b.temperature>=900f;
            float temperature=Math.Max(20f,b.temperature-FixedStep*(tripped ? 18f : 4f));
            if(tripped && temperature<=300f) tripped=false;
            float coolant=b.progress, fuel=b.fuel;
            bool canRun=!tripped && OutputRoom(b,(int)ItemKind.NuclearWaste,1) && (fuel>.0001f || b.items[(int)ItemKind.NuclearFuel]>0) && (coolant>.0001f || b.items[(int)ItemKind.Coolant]>0);
            if(canRun)
            {
                // One can supplies one second of cooling and removes that second's
                // 45 degrees of reaction heat; passive loss provides stable headroom.
                if(coolant<FixedStep-.0001f && b.items[(int)ItemKind.Coolant]>0) { if(consume) b.items[(int)ItemKind.Coolant]--; coolant+=1f; temperature=Math.Max(20f,temperature-45f); }
                if(fuel<FixedStep-.0001f && b.items[(int)ItemKind.NuclearFuel]>0)
                {
                    if(consume) { b.items[(int)ItemKind.NuclearFuel]--; b.items[(int)ItemKind.NuclearWaste]++; RecordProduced((int)ItemKind.NuclearWaste,1); }
                    fuel+=20f;
                }
                float fraction=Clamp(Math.Min(coolant,fuel)/FixedStep,0f,1f);
                temperature+=45f*FixedStep*fraction;
                if(temperature>=900f) { tripped=true; canRun=false; }
                if(consume) { b.progress=Math.Max(0f,coolant-FixedStep*fraction); b.fuel=Math.Max(0f,fuel-FixedStep*fraction); }
                if(consume) { b.temperature=temperature; b.tripped=tripped; }
                return canRun ? 1200f*fraction : 0f;
            }
            if(consume) { b.temperature=temperature; b.tripped=tripped; }
            return canRun ? 1200f : 0f;
        }
        private static int OutputTier(BuildingState b) => TechCatalog.EnergyTier(b);
        private bool WaterPowerUnlocked(BuildingState b)
        {
            if(!LowVoltageCatalog.IsBuildUnlocked(State,b.kind)) return false;
            if(!SteamCatalog.IsBuildUnlocked(State,b.kind)) return false;
            if(!WaterCatalog.Enabled(State)) return true;
            switch(b.kind)
            {
                case BuildKind.Waterwheel: case BuildKind.Shaft: case BuildKind.MechanicalCrusher:
                case BuildKind.BlastFurnace: case BuildKind.MechanicalPress: case BuildKind.MechanicalWorkshop:
                case BuildKind.WaterMill: case BuildKind.IrrigationPump: case BuildKind.MechanicalHopper:
                    return WaterCatalog.IsBuildUnlocked(State,b.kind);
                default: return true;
            }
        }
        private void RebuildPowerLayout()
        {
            powerLayout.Clear(); powerComponents.Clear();
            var nodes=new List<int>(); int[] parents=new int[productionOrder.Count];
            for(int i=0;i<productionOrder.Count;i++)
            {
                BuildingState b=productionOrder[i];
                bool unlocked=WaterPowerUnlocked(b);
                powerLayout.Add(new PowerLayout { building=b,id=b.id,x=b.x,y=b.y,kind=b.kind,setting=b.setting,tier=OutputTier(b),width=DemoCatalog.Width(b),height=DemoCatalog.Height(b),unlocked=unlocked });
                parents[i]=i; if(unlocked && DemoCatalog.IsPowerNode(b.kind)) nodes.Add(i);
            }
            for(int i=0;i<nodes.Count;i++) for(int j=0;j<i;j++)
            {
                var a=powerLayout[nodes[i]]; var b=powerLayout[nodes[j]];
                if(a.tier!=b.tier) continue;
                bool linked=a.tier<2 ? Touching(a,b) : DistSq(DemoCatalog.CenterX(a.building),DemoCatalog.CenterY(a.building),DemoCatalog.CenterX(b.building),DemoCatalog.CenterY(b.building))<=DemoCatalog.LinkRange*DemoCatalog.LinkRange;
                if(!linked) continue;
                int ra=PowerRoot(parents,nodes[i]),rb=PowerRoot(parents,nodes[j]);
                if(ra!=rb) parents[Math.Max(ra,rb)]=Math.Min(ra,rb);
            }
            var networks=new Dictionary<int,PowerNetworkState>();
            foreach(int index in nodes)
            {
                int root=PowerRoot(parents,index); PowerNetworkState n;
                if(!networks.TryGetValue(root,out n)) { n=new PowerNetworkState { id=productionOrder[root].id,tier=powerLayout[index].tier }; networks.Add(root,n); powerComponents.Add(n); }
                n.nodes++; powerLayout[index].network=n;
            }
            foreach(var l in powerLayout)
            {
                if(!l.unlocked) continue;
                if(Converter(l.kind))
                {
                    int tier=l.kind==BuildKind.SteamTurbine || l.kind==BuildKind.SteamEngine ? 1 : l.tier+1;
                    l.inputNetwork=NearestNetwork(l,nodes,tier);
                    if(l.inputNetwork!=null) l.inputNetwork.consumers++;
                }
                else if(DemoCatalog.Demand(l.kind)>0f)
                {
                    l.network=NearestNetwork(l,nodes,l.tier);
                    if(l.network!=null) l.network.consumers++;
                }
            }
        }
        private PowerNetworkState NearestNetwork(PowerLayout consumer,List<int> nodes,int tier)
        {
            float nearest=float.MaxValue; int nearestId=int.MaxValue; PowerNetworkState result=null;
            foreach(int index in nodes)
            {
                var node=powerLayout[index]; if(node==consumer || node.tier!=tier) continue;
                float distance=PowerFootprintDistanceSquared(consumer,node);
                if((tier<2 ? !Touching(consumer,node) : distance>DemoCatalog.SupplyRadius*DemoCatalog.SupplyRadius) || distance>nearest || (distance==nearest && node.id>=nearestId)) continue;
                nearest=distance; nearestId=node.id; result=node.network;
            }
            return result;
        }
        private static bool Touching(PowerLayout a,PowerLayout b)
        {
            bool xTouch=a.x+a.width==b.x || b.x+b.width==a.x;
            bool yTouch=a.y+a.height==b.y || b.y+b.height==a.y;
            return (xTouch && a.y<b.y+b.height && b.y<a.y+a.height) || (yTouch && a.x<b.x+b.width && b.x<a.x+a.width);
        }

        private static int PowerRoot(int[] parents, int index)
        {
            while (parents[index] != index)
            {
                parents[index] = parents[parents[index]]; index = parents[index];
            }
            return index;
        }

        private static float PowerFootprintDistanceSquared(PowerLayout a, PowerLayout b)
        {
            // Cells extend half a unit around their integer centre, so adjacent footprints touch.
            float dx = Math.Max(0, Math.Max(a.x - (b.x + b.width), b.x - (a.x + a.width)));
            float dy = Math.Max(0, Math.Max(a.y - (b.y + b.height), b.y - (a.y + a.height)));
            return dx * dx + dy * dy;
        }

        private static int CompareProductionIds(BuildingState a, BuildingState b) { return a.id.CompareTo(b.id); }
        private static int CompareCargoIds(CargoState a, CargoState b) { return a.id.CompareTo(b.id); }

        private void UpdateProduction()
        {
            cargoIncoming.Clear(); cargoReservationCount=0;
            foreach(var c in State.cargo) ChangeCargoReservation(c.toId,c.item,1);
            foreach(BuildingState b in productionOrder)
            {
                if(b.hp<=0f || !b.powered) continue;
                if(b.kind==BuildKind.Miner || b.kind==BuildKind.SteamMiner)
                {
                    NodeState ore=NearbyOre(b);
                    if(ore==null) continue;
                    int item=NodeItem(ore.kind);
                    int[] reserved=Incoming(b.id);
                    if(b.items[item]+(reserved==null ? 0 : reserved[item])>=BufferLimit) continue;
                    b.progress=Math.Min(3f,b.progress+FixedStep*engineeringRate*b.powerRatio);
                    if(b.progress+.00001f<3f) continue;
                    ore.stock--; b.items[item]++; b.progress=0f;
                    State.gathered=SaturatingAdd(State.gathered,1);
                    if(ore.stock==0) RemoveNode(ore);
                    continue;
                }
                RecipeDefinition r=TechCatalog.SelectedRecipe(b);
                if(r==null || r.building!=b.kind || b.kind==BuildKind.Workbench || r.era>State.era) continue;
                if(!PrimitiveCatalog.IsBuildUnlocked(State,b.kind) || !PrimitiveCatalog.IsRecipeUnlocked(State,r)) continue;
                if(!WaterCatalog.IsBuildUnlocked(State,b.kind) || !WaterCatalog.IsRecipeUnlocked(State,r)) continue;
                if(!LowVoltageCatalog.IsBuildUnlocked(State,b.kind) || !LowVoltageCatalog.IsRecipeUnlocked(State,r)) continue;
                if((b.kind==BuildKind.CoolingTower || r.id==71) && !NearWater(b)) continue;
                if(!CanRunRecipe(b.items,r,BufferLimit,Incoming(b.id))) continue;
                float ratio=b.powerRatio;
                if(b.kind==BuildKind.StoneFurnace)
                {
                    ratio=BurnFuel(b,true,1f,20f); b.powerRatio=ratio; b.powered=ratio>0f;
                    if(ratio<=0f) continue;
                }
                b.progress=Math.Min(r.duration,b.progress+FixedStep*engineeringRate*ratio);
                if(b.progress+.00001f<r.duration) continue;
                Pay(b.items,r.inputs);
                for(int i=0;i<DemoCatalog.ItemCount;i++)
                {
                    b.items[i]+=r.outputs[i]; RecordProduced(i,r.outputs[i]);
                    State.manufactured=SaturatingAdd(State.manufactured,r.outputs[i]);
                }
                RecordWaterProduction(b,r);
                RecordSteamProduction(b,r);
                RecordLowVoltageProduction(b,r);
                b.progress=0f;
                if(b.kind==BuildKind.Assembler) foreach(PlayerState p in State.players)
                    if(p.connected && p.hp>0f && DistSq(p.x,p.y,b.x,b.y)<=196f) GainXp(p,2);
            }
            TransferLogistics();
        }
        private void RecordProduced(int item,int amount)
        {
            if(amount>0) State.produced[item]=SaturatingAdd(State.produced[item],amount);
        }
        private static bool CanRunRecipe(int[] inventory,RecipeDefinition r,int limit,int[] reserved)
        {
            for(int i=0;i<DemoCatalog.ItemCount;i++)
                if(inventory[i]<r.inputs[i] || inventory[i]-r.inputs[i]+r.outputs[i]+(reserved==null ? 0 : reserved[i])>limit) return false;
            return true;
        }
        // Snapshot all live reservations once, before any machine changes inventory.
        private int[] Incoming(int id)
        {
            int[] result; cargoIncoming.TryGetValue(id,out result);
            return result;
        }
        private bool OutputRoom(BuildingState b,int item,int count)
        {
            int incoming=0; foreach(var c in State.cargo) if(c.toId==b.id && c.item==item) incoming++;
            return b.items[item]+incoming<=BufferLimit-count;
        }
        private static int NodeItem(NodeKind kind)
        {
            switch(kind)
            {
                case NodeKind.Tree: return (int)ItemKind.Wood;
                case NodeKind.Ore: return (int)ItemKind.Ore;
                case NodeKind.CopperOre: return (int)ItemKind.CopperOre;
                case NodeKind.TinOre: return (int)ItemKind.TinOre;
                case NodeKind.UraniumOre: return (int)ItemKind.UraniumOre;
                case NodeKind.Stone: return (int)ItemKind.Stone;
                case NodeKind.Clay: return (int)ItemKind.Clay;
                case NodeKind.Coal: return (int)ItemKind.Coal;
                case NodeKind.Limestone: return (int)ItemKind.Limestone;
                case NodeKind.FireClay: return (int)ItemKind.FireClay;
                case NodeKind.Crate: return (int)ItemKind.Food;
                default: return (int)ItemKind.Scrap;
            }
        }
        private static bool IsOutput(BuildKind kind,int item)
        {
            if(kind==BuildKind.Miner) return item==(int)ItemKind.Ore || item==(int)ItemKind.CopperOre || item==(int)ItemKind.TinOre || item==(int)ItemKind.UraniumOre;
            if(kind==BuildKind.SteamMiner) return TechCatalog.Stores(kind,item);
            if(kind==BuildKind.Reactor) return item==(int)ItemKind.NuclearWaste;
            foreach(var r in TechCatalog.Recipes) if(r.building==kind && kind!=BuildKind.Workbench && r.outputs[item]>0) return true;
            return false;
        }

        private void TransferLogistics()
        {
            cargoBuildings.Clear(); cargoIncoming.Clear(); cargoReturnItems.Clear(); cargoBusySources.Clear(); cargoPlans.Clear();
            cargoReservationCount = 0;
            foreach (BuildingState b in productionOrder) cargoBuildings.Add(b.id, b);
            State.cargo.Sort(CompareCargoIds);
            foreach (CargoState c in State.cargo)
            {
                cargoBusySources.Add(c.fromId);
                if (cargoBuildings.ContainsKey(c.toId)) ChangeCargoReservation(c.toId, c.item, 1);
                BuildingState source;
                if (cargoBuildings.TryGetValue(c.fromId,out source) && ReservesCargoReturn(source))
                    cargoReturnItems[c.fromId] = c.item;
            }
            // Capture exportable inventory BEFORE any arrival or orphan return. Even a restored
            // packet with progress == 1 cannot be forwarded by an otherwise empty belt this tick.
            foreach (BuildingState from in productionOrder)
            {
                if (cargoBusySources.Contains(from.id) || CargoExportRate(from) <= 0f) continue;
                if (from.kind == BuildKind.FilterSplitter)
                {
                    ItemTransfer electricalPlan;
                    if (TryPlanElectricalCargo(from, out electricalPlan)) cargoPlans.Add(electricalPlan);
                    continue;
                }
                BuildingState to = BuildingAt(DemoCatalog.OutputX(from), DemoCatalog.OutputY(from));
                if (to == null || to == from || to.hp <= 0f) continue;
                // Side loading is allowed, but the front of a belt is exclusively an output.
                if (to.kind == BuildKind.Belt && ((from.rotation - to.rotation) & 3) == 2) continue;
                int item = CargoExportItem(from, to);
                if (item >= 0) cargoPlans.Add(new ItemTransfer { from = from, to = to, item = item,
                    x = DemoCatalog.OutputX(from), y = DemoCatalog.OutputY(from), direction = from.rotation });
            }
            int kept = 0, oldCount = State.cargo.Count;
            for (int i = 0; i < oldCount; i++)
            {
                CargoState c = State.cargo[i];
                BuildingState from, to;
                bool sourceLives = cargoBuildings.TryGetValue(c.fromId, out from);
                bool targetLives = cargoBuildings.TryGetValue(c.toId, out to);
                if (!sourceLives || !targetLives)
                {
                    if (targetLives) ChangeCargoReservation(c.toId, c.item, -1);
                    // Normal destruction/dismantling is handled by the world code. If an endpoint
                    // disappeared independently, refund only into real available source capacity.
                    cargoReturnItems.Remove(c.fromId);
                    if (sourceLives && HasCargoRoom(from, c.item)) from.items[c.item]++;
                    continue;
                }
                float rate = CargoExportRate(from);
                if (rate <= 0f) { State.cargo[kept++] = c; continue; }
                c.progress = Math.Min(1f, c.progress + FixedStep * rate / c.duration);
                if (c.progress >= 1f)
                {
                    ChangeCargoReservation(c.toId, c.item, -1);
                    if (HasCargoRoom(to, c.item))
                    {
                        to.items[c.item]++;
                        cargoReturnItems.Remove(c.fromId);
                        if (from.kind == BuildKind.MechanicalHopper && WaterCatalog.IsWaterDriven(State,from))
                            State.waterTransported = SaturatingAdd(State.waterTransported,1);
                        if (from.kind == BuildKind.FilterSplitter && c.item == from.setting)
                            State.lvSorted = SaturatingAdd(State.lvSorted,1);
                        continue;
                    }
                    // Unexpected external filling must block the packet, never discard it.
                    ChangeCargoReservation(c.toId, c.item, 1);
                }
                State.cargo[kept++] = c;
            }
            if (kept < oldCount) State.cargo.RemoveRange(kept, oldCount - kept);
            foreach (ItemTransfer plan in cargoPlans)
            {
                if (State.cargo.Count >= MaxCargo || State.nextId >= int.MaxValue - 1) break;
                if (plan.from.items[plan.item] <= 0 || CargoExportRate(plan.from) <= 0f || !HasCargoRoom(plan.to, plan.item)) continue;
                int x = plan.x, y = plan.y;
                float fromX = x - DirectionX[plan.direction], fromY = y - DirectionY[plan.direction];
                var c = new CargoState { id = AllocateId(), fromId = plan.from.id, toId = plan.to.id, item = plan.item,
                    fromX = fromX, fromY = fromY, toX = x, toY = y, progress = 0f,
                    // Keep a bounded duration in saves; only live progress depends on power ratio.
                    duration = (float)Math.Sqrt(DistSq(fromX, fromY, x, y)) / (DemoCatalog.CargoSpeed * (plan.from.kind == BuildKind.MechanicalHopper ? 2f : 1f)) };
                plan.from.items[plan.item]--;
                // Reserve the vacated slot until delivery so an orphan can always return normally.
                if (ReservesCargoReturn(plan.from)) cargoReturnItems[plan.from.id] = plan.item;
                ChangeCargoReservation(plan.to.id, plan.item, 1);
                State.cargo.Add(c);
            }
        }

        private float CargoExportRate(BuildingState from)
        {
            if(WaterCatalog.Enabled(State) && !WaterCatalog.IsBuildUnlocked(State,from.kind)) return 0f;
            if(from.kind==BuildKind.FilterSplitter)
                return LowVoltageCatalog.HasResearch(State,LowVoltageTech.Sorting) && LowVoltageCatalog.IsBuildUnlocked(State,from.kind) && from.powered
                    ? from.powerRatio : 0f;
            if(from.kind!=BuildKind.MechanicalHopper) return 1f;
            return from.powered && from.powerRatio>0f && PrimitiveCatalog.IsBuildUnlocked(State,from.kind) && WaterCatalog.IsBuildUnlocked(State,from.kind)
                ? from.powerRatio : 0f;
        }

        private int CargoExportItem(BuildingState from,BuildingState to)
        {
            for(int i=0;i<DemoCatalog.ItemCount;i++)
                if(from.items[i]>0 && (from.kind==BuildKind.Chest || from.kind==BuildKind.MechanicalHopper || from.kind==BuildKind.Belt || from.kind==BuildKind.FilterSplitter || CanExportOutput(from,i)) && Accepts(to,i)) return i;
            return -1;
        }
        private bool CanExportOutput(BuildingState b,int item)
        {
            RecipeDefinition r=TechCatalog.SelectedRecipe(b);
            if(WaterCatalog.Enabled(State) && r!=null && !WaterCatalog.IsRecipeUnlocked(State,r)) return false;
            return IsOutput(b.kind,item) && (r==null || r.inputs[item]==0);
        }

        private void ChangeCargoReservation(int id, int item, int delta)
        {
            int[] reserved;
            if (!cargoIncoming.TryGetValue(id, out reserved))
            {
                if (cargoReservationCount == cargoReservationPool.Count) cargoReservationPool.Add(new int[DemoCatalog.ItemCount]);
                reserved = cargoReservationPool[cargoReservationCount++];
                Array.Clear(reserved, 0, reserved.Length); cargoIncoming.Add(id, reserved);
            }
            reserved[item] += delta;
        }

        private bool HasCargoRoom(BuildingState b, int item)
        {
            int[] reserved; cargoIncoming.TryGetValue(b.id, out reserved);
            int returningItem;
            int returning = ReservesCargoReturn(b) && cargoReturnItems.TryGetValue(b.id,out returningItem) && returningItem == item ? 1 : 0;
            if (b.items[item] + (reserved == null ? 0 : reserved[item]) + returning >= BufferLimit) return false;
            return b.kind != BuildKind.Belt || TotalItems(b.items) + (reserved == null ? 0 : TotalItems(reserved)) < 2;
        }
        private bool Accepts(BuildingState b,int item)
        {
            if(b.kind==BuildKind.Boiler && b.modularBoiler) return false;
            if(b.kind==BuildKind.Chest || b.kind==BuildKind.MechanicalHopper || b.kind==BuildKind.Belt || b.kind==BuildKind.FilterSplitter) return true;
            if(b.kind==BuildKind.CombustionChamber) return item==(int)ItemKind.Coal || item==(int)ItemKind.Coke || item==(int)ItemKind.Fuel;
            if(TechCatalog.IsFuelBurner(b.kind) && item==FuelItem(b)) return true;
            if(b.kind==BuildKind.Turret) return item==(int)ItemKind.Ammo;
            if(b.kind==BuildKind.Reactor) return item==(int)ItemKind.NuclearFuel || item==(int)ItemKind.Coolant;
            RecipeDefinition r=TechCatalog.SelectedRecipe(b);
            return r!=null && r.building==b.kind && b.kind!=BuildKind.Workbench && r.inputs[item]>0;
        }
        private static bool Accepts(BuildKind kind,int item)
        {
            if(kind==BuildKind.Chest || kind==BuildKind.MechanicalHopper || kind==BuildKind.Belt || kind==BuildKind.FilterSplitter) return true;
            if(kind==BuildKind.CombustionChamber) return item==(int)ItemKind.Coal || item==(int)ItemKind.Coke || item==(int)ItemKind.Fuel;
            if(TechCatalog.IsFuelBurner(kind) && item==(int)ItemKind.Fuel) return true;
            if(kind==BuildKind.StoneFurnace && item==(int)ItemKind.Charcoal) return true;
            if(kind==BuildKind.Turret) return item==(int)ItemKind.Ammo;
            if(kind==BuildKind.Reactor) return item==(int)ItemKind.NuclearFuel || item==(int)ItemKind.Coolant;
            foreach(var r in TechCatalog.Recipes) if(r.building==kind && kind!=BuildKind.Workbench && r.inputs[item]>0) return true;
            return false;
        }
    }
}
