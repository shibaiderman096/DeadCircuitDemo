using System;

namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private static void GiveStartingGear(WorldState world, PlayerState p, bool migrated)
        {
            if (!migrated && PrimitiveCatalog.Enabled(world)) return;
            GearKind[] kinds = migrated ? new[] { GearKind.StoneAxe, GearKind.StonePickaxe, GearKind.Knife, GearKind.Pistol } :
                new[] { GearKind.StoneAxe, GearKind.StonePickaxe, GearKind.Knife };
            SaveCheck(world.nextId > 0 && world.nextId < int.MaxValue - kinds.Length - 2, "starter gear id space");
            foreach (GearKind kind in kinds)
            {
                var gear = new GearState { id=world.nextId++, kind=kind, loadedAmmo=EquipmentCatalog.Gear(kind).ammo };
                p.gear.Add(gear);
                if (kind == GearKind.StoneAxe) p.toolId=gear.id;
                if (kind == GearKind.Knife || kind == GearKind.Pistol) p.weaponId=gear.id;
            }
            p.activeSlot=0; p.selectedAmmo=ItemKind.Ammo;
        }

        private static void CancelReload(PlayerState p)
        { p.reloadRemaining=0f; p.reloadingGearId=0; }

        private void UpdateEquipment(PlayerState p)
        {
            p.recoil=Math.Max(0f,p.recoil-FixedStep*(p.aiming ? .3f : .23f)*(1f+p.attributes[3]*.04f));
            if (!p.connected || p.hp<=0f) { CancelReload(p); p.aiming=false; p.moving=false; return; }
            if (p.reloadingGearId==0) return;
            GearState g=EquipmentCatalog.Equipped(p);
            if(g==null || g.id!=p.reloadingGearId || !EquipmentCatalog.IsGun(g.kind) || g.durability<=0 ||
                !EquipmentCatalog.AmmoCompatible(g.kind,p.selectedAmmo) || g.magazine>0 && g.loadedAmmo!=p.selectedAmmo)
            { CancelReload(p); return; }
            p.reloadRemaining=Math.Max(0f,p.reloadRemaining-FixedStep);
            if(p.reloadRemaining>.00001f) return;
            // No ammunition is reserved or removed at start. Recheck current stock exactly once
            // on completion, so cancellation, save/reload, and competing commands cannot mint ammo.
            int amount=Math.Min(EquipmentCatalog.Stats(g).magazine-g.magazine,p.items[(int)p.selectedAmmo]);
            if(amount>0) { p.items[(int)p.selectedAmmo]-=amount; g.magazine+=amount; g.loadedAmmo=p.selectedAmmo; }
            CancelReload(p);
            Notice(p,amount>0 ? "装填完成："+g.magazine+" 发" : "装填取消：储备弹药已用完。");
        }

        private void CraftGear(PlayerState p,int arg)
        {
            if(p.interactCooldown>0f || arg<0 || arg>=EquipmentCatalog.GearNames.Length) return;
            GearKind kind=(GearKind)arg;
            string requirement=EquipmentCatalog.CraftRequirement(State,p,kind);
            if(requirement.Length>0)
            {
                Notice(p,kind==GearKind.RadiationSuit ? "制作失败："+requirement :
                    "制作失败：请靠近工作台，并确认已解锁科技、材料充足且装备栏未满（最多 24 件）。");
                return;
            }
            GearDefinition d=EquipmentCatalog.Gear(kind);
            int id=AllocateId(); Pay(p.items,d.cost);
            p.gear.Add(new GearState { id=id,kind=kind,loadedAmmo=d.ammo,durability=d.maxDurability });
            p.interactCooldown=.4f; Notice(p,"已制作："+d.name+"；枪械需装备并装填。");
        }

        private void CraftPart(PlayerState p,int arg)
        {
            if(p.interactCooldown>0f || arg<0 || arg>=EquipmentCatalog.PartNames.Length) return;
            PartKind kind=(PartKind)arg;
            if(!EquipmentCatalog.CanCraftPart(State,p,kind)) { Notice(p,"配件制作失败：请检查工作台、科技、材料及配件库存。"); return; }
            PartDefinition d=EquipmentCatalog.Part(kind); Pay(p.items,d.cost); p.parts[arg]++;
            p.interactCooldown=.4f; Notice(p,"已制作："+d.name);
        }

        private void EquipGear(PlayerState p,int id)
        {
            GearState g=EquipmentCatalog.Find(p,id);
            if(g==null) return;
            int armorSlot=EquipmentCatalog.ArmorSlot(g.kind);
            if(armorSlot>=0)
            { p.armorIds[armorSlot]=g.id; Notice(p,"已穿戴："+EquipmentCatalog.GearNames[(int)g.kind]); return; }
            if(EquipmentCatalog.Equipped(p)!=g) CancelReload(p);
            bool tool=EquipmentCatalog.Gear(g.kind).tool;
            if(tool) p.toolId=g.id; else p.weaponId=g.id;
            p.activeSlot=tool ? 1 : 0; p.aiming=false;
            if(EquipmentCatalog.IsGun(g.kind)) p.selectedAmmo=g.magazine>0 ? g.loadedAmmo : EquipmentCatalog.AmmoCompatible(g.kind,p.selectedAmmo) ? p.selectedAmmo : EquipmentCatalog.Gear(g.kind).ammo;
            Notice(p,"已装备："+EquipmentCatalog.GearNames[(int)g.kind]);
        }

        private void SwitchGear(PlayerState p,int slot)
        {
            if(slot<0 || slot>2) return;
            if(p.activeSlot!=slot) CancelReload(p);
            p.activeSlot=slot; p.aiming=false;
            GearState g=EquipmentCatalog.Equipped(p);
            if(g!=null && EquipmentCatalog.IsGun(g.kind)) p.selectedAmmo=g.magazine>0 ? g.loadedAmmo : EquipmentCatalog.AmmoCompatible(g.kind,p.selectedAmmo) ? p.selectedAmmo : EquipmentCatalog.Gear(g.kind).ammo;
        }

        private void UnequipGear(PlayerState p,int id)
        {
            GearState g=EquipmentCatalog.Find(p,id);
            if(g==null) return;
            if(p.reloadingGearId==id) CancelReload(p);
            if(EquipmentCatalog.Equipped(p)==g) { p.activeSlot=2; p.aiming=false; }
            if(p.weaponId==id) p.weaponId=0;
            if(p.toolId==id) p.toolId=0;
            for(int i=0;i<p.armorIds.Length;i++) if(p.armorIds[i]==id) p.armorIds[i]=0;
            Notice(p,"已卸下："+EquipmentCatalog.GearNames[(int)g.kind]);
        }

        private void StartReload(PlayerState p)
        {
            if(p.interactCooldown>0f || p.reloadingGearId!=0) return;
            GearState g=EquipmentCatalog.Equipped(p);
            if(g==null || !EquipmentCatalog.IsGun(g.kind) || g.durability<=0) return;
            if(!EquipmentCatalog.AmmoCompatible(g.kind,p.selectedAmmo) || g.magazine>0 && g.loadedAmmo!=p.selectedAmmo)
            { Notice(p,"弹种不兼容；更换弹种前请先卸弹。"); return; }
            if(g.magazine>=EquipmentCatalog.Stats(g).magazine || p.items[(int)p.selectedAmmo]<=0)
            { Notice(p,"弹匣已满或缺少"+EquipmentCatalog.AmmoName(p.selectedAmmo)); return; }
            p.reloadingGearId=g.id; p.reloadRemaining=EquipmentCatalog.Stats(g).reload; p.interactCooldown=.15f;
            Notice(p,"正在装填"+EquipmentCatalog.AmmoName(p.selectedAmmo)+"；切换装备会取消。");
        }

        private void SelectAmmo(PlayerState p,int arg)
        {
            ItemKind ammo=(ItemKind)arg;
            if(!EquipmentCatalog.IsAmmo(ammo) || ammo==p.selectedAmmo) return;
            GearState g=EquipmentCatalog.Equipped(p);
            if(g==null || !EquipmentCatalog.IsGun(g.kind)) g=EquipmentCatalog.Find(p,p.weaponId);
            if(g!=null && EquipmentCatalog.IsGun(g.kind))
            {
                if(!EquipmentCatalog.AmmoCompatible(g.kind,ammo)) { Notice(p,"该枪械不兼容此弹种。"); return; }
                if(g.magazine>0) { Notice(p,"请先卸弹，再更换弹种。"); return; }
            }
            CancelReload(p); p.selectedAmmo=ammo; Notice(p,"已选择："+EquipmentCatalog.AmmoName(ammo));
        }

        private void UnloadGear(PlayerState p,int id)
        {
            if(p.interactCooldown>0f) return;
            GearState g=EquipmentCatalog.Find(p,id);
            if(g==null || !EquipmentCatalog.IsGun(g.kind)) return;
            if(p.items[(int)g.loadedAmmo]>InventoryLimit-g.magazine) { Notice(p,"弹药储备已满，无法完整卸弹。"); return; }
            if(p.reloadingGearId==id) CancelReload(p);
            p.items[(int)g.loadedAmmo]+=g.magazine; g.magazine=0;
            p.interactCooldown=.2f; Notice(p,"弹匣已卸空，弹药全额退回储备。");
        }

        private void AttachPart(PlayerState p,int id,int arg)
        {
            if(p.interactCooldown>0f || arg<0 || arg>=EquipmentCatalog.PartNames.Length) return;
            GearState g=EquipmentCatalog.Find(p,id); PartKind kind=(PartKind)arg; PartDefinition d=EquipmentCatalog.Part(kind);
            if(g==null || !EquipmentCatalog.PartCompatible(g.kind,kind) || p.parts[arg]<=0 || !EquipmentCatalog.HasWorkbench(State,p))
            { Notice(p,"安装失败：请靠近工作台，并确认已解锁科技、拥有对应配件且配件适用。"); return; }
            int slot=(int)d.slot;
            if(g.attachments[slot]!=-1) { Notice(p,"此槽已有配件，请先卸下。"); return; }
            if(p.reloadingGearId==id) CancelReload(p);
            p.parts[arg]--; g.attachments[slot]=arg; p.interactCooldown=.25f; Notice(p,"已安装："+d.name);
        }

        private void DetachPart(PlayerState p,int id,int slot)
        {
            if(p.interactCooldown>0f || slot<0 || slot>=EquipmentCatalog.SlotNames.Length) return;
            GearState g=EquipmentCatalog.Find(p,id);
            if(g==null || g.attachments[slot]<0 || !EquipmentCatalog.HasWorkbench(State,p)) return;
            int part=g.attachments[slot];
            if(p.parts[part]>=InventoryLimit) { Notice(p,"配件库存已满。"); return; }
            if(part==(int)PartKind.ExtendedMag && g.magazine>EquipmentCatalog.Gear(g.kind).magazine)
            { Notice(p,"扩容弹匣仍有超额弹药，请先卸弹。"); return; }
            if(p.reloadingGearId==id) CancelReload(p);
            g.attachments[slot]=-1; p.parts[part]++; p.interactCooldown=.25f; Notice(p,"配件已退回库存。");
        }

        private void RepairGear(PlayerState p,int id)
        {
            if(p.interactCooldown>0f) return;
            GearState g=EquipmentCatalog.Find(p,id);
            if(g==null || g.durability>=EquipmentCatalog.Gear(g.kind).maxDurability || !EquipmentCatalog.HasWorkbench(State,p)) return;
            bool stone=g.kind==GearKind.StoneAxe || g.kind==GearKind.StonePickaxe;
            int[] cost=stone ? DemoCatalog.Materials(ItemKind.Wood,1,ItemKind.Stone,1) : DemoCatalog.Materials(ItemKind.Iron,2,ItemKind.Scrap,1);
            if(!CanPay(p.items,cost)) { Notice(p,stone ? "修理需1木材、1石材。" : "修理需2铁锭、1废料。"); return; }
            if(p.reloadingGearId==id) CancelReload(p);
            Pay(p.items,cost); g.durability=Math.Min(EquipmentCatalog.Gear(g.kind).maxDurability,g.durability+25); p.interactCooldown=.4f; Notice(p,"已修理装备。");
        }

        private void SalvageGear(PlayerState p,int id)
        {
            if(p.interactCooldown>0f || !EquipmentCatalog.HasWorkbench(State,p)) return;
            GearState g=EquipmentCatalog.Find(p,id);
            if(g==null) return;
            int[] returned=EquipmentCatalog.Gear(g.kind).cost;
            for(int i=0;i<returned.Length;i++) returned[i]/=2;
            if(g.magazine>0) returned[(int)g.loadedAmmo]+=g.magazine;
            var returnedParts=new int[EquipmentCatalog.PartNames.Length];
            foreach(int part in g.attachments) if(part>=0) returnedParts[part]++;
            for(int i=0;i<returned.Length;i++) if(p.items[i]>InventoryLimit-returned[i])
            { Notice(p,"背包空间不足：无法收回全部材料与弹药。"); return; }
            for(int i=0;i<returnedParts.Length;i++) if(p.parts[i]>InventoryLimit-returnedParts[i])
            { Notice(p,"配件库存已满，无法收回全部配件。"); return; }
            for(int i=0;i<returned.Length;i++) p.items[i]+=returned[i];
            for(int i=0;i<returnedParts.Length;i++) p.parts[i]+=returnedParts[i];
            if(p.reloadingGearId==id) CancelReload(p);
            if(EquipmentCatalog.Equipped(p)==g) { p.activeSlot=2; p.aiming=false; }
            if(p.weaponId==id) p.weaponId=0;
            if(p.toolId==id) p.toolId=0;
            for(int i=0;i<p.armorIds.Length;i++) if(p.armorIds[i]==id) p.armorIds[i]=0;
            p.gear.Remove(g); p.interactCooldown=.4f; Notice(p,"装备已回收。已返还一半制作材料、全部配件与弹匣中的弹药。");
        }

        private static GearState EquippedWrench(PlayerState p)
        {
            GearState g=EquipmentCatalog.Equipped(p);
            return g!=null && g.kind==GearKind.Wrench && g.durability>0 ? g : null;
        }

        private static int HarvestBonus(GearState g,NodeKind node)
        {
            if(g==null || g.durability<=0) return 0;
            if(node==NodeKind.Tree) return g.kind==GearKind.StoneAxe ? 2 : g.kind==GearKind.SteelAxe ? 5 : 0;
            if(EquipmentCatalog.UsesPickaxe(node))
                return g.kind==GearKind.StonePickaxe ? 2 : g.kind==GearKind.SteelPickaxe ? 5 : 0;
            return g.kind==GearKind.Wrench && (node==NodeKind.Scrap || node==NodeKind.Wall) ? 3 : 0;
        }
    }
}
