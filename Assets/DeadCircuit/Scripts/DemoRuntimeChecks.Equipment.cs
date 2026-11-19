#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class DemoRuntimeChecks
    {
        int qaGearId, qaGearMagazine;
        int[] qaArmorIds;

        IEnumerator VerifyEquipment(GameBridge bridge)
        {
            // This opt-in fixture uses the separate QA save, with explicit material grants.
            PrepareQaPlot(bridge.World, true);
            var p = bridge.LocalPlayer;
            for (int i = 0; i < p.items.Length; i++) p.items[i] = 100;
            yield return BuildQa(bridge, BuildKind.Workbench, 30, 28);
            yield return new WaitForSecondsRealtime(.6f);
            Click("Open Inventory");
            yield return null;
            Click("Inventory Tab Craft");
            yield return new WaitForSecondsRealtime(.25f);
            Click("Gear Craft " + (int)GearKind.PrecisionRifle);
            yield return new WaitForSecondsRealtime(.65f);
            var rifle = p.gear.SingleOrDefault(g => g.kind == GearKind.PrecisionRifle);
            Require(rifle != null, "craft precision rifle through inventory UI");
            qaGearId = rifle.id;
            foreach (var kind in new[] { PartKind.Scope4x, PartKind.ExtendedMag, PartKind.Suppressor })
            {
                Click("Part Craft " + (int)kind);
                yield return new WaitForSecondsRealtime(.65f);
                Require(p.parts[(int)kind] > 0, "craft attachment through UI " + kind);
            }
            var armorKinds = new[] { GearKind.SteelHelmet, GearKind.ArmorVest, GearKind.WorkPants, GearKind.TacticalBoots };
            foreach (var kind in armorKinds)
            {
                Click("Gear Craft " + (int)kind);
                yield return new WaitForSecondsRealtime(.65f);
                Require(p.gear.Any(g => g.kind == kind), "craft armor through inventory UI " + kind);
            }
            Click("Inventory Tab Equipment");
            yield return new WaitForSecondsRealtime(.25f);
            Click("Gear Slot " + rifle.id);
            yield return new WaitForSecondsRealtime(.25f);
            Click("Equip Selected Gear");
            yield return new WaitForSecondsRealtime(.35f);
            Require(p.weaponId == rifle.id && p.activeSlot == 0, "equip selected rifle from inventory");
            foreach (var kind in new[] { PartKind.Scope4x, PartKind.ExtendedMag, PartKind.Suppressor })
            {
                Click("Attach Part " + (int)kind);
                yield return new WaitForSecondsRealtime(.35f);
            }
            Require(rifle.attachments[(int)AttachmentSlot.Optic] == (int)PartKind.Scope4x &&
                rifle.attachments[(int)AttachmentSlot.Magazine] == (int)PartKind.ExtendedMag, "inventory attachment commands persist on the chosen weapon");
            foreach (var kind in armorKinds)
            {
                var armor = p.gear.Single(g => g.kind == kind);
                Click("Gear Slot " + armor.id);
                yield return new WaitForSecondsRealtime(.2f);
                Click("Equip Selected Gear");
                yield return new WaitForSecondsRealtime(.3f);
                Require(p.armorIds[EquipmentCatalog.ArmorSlot(kind)] == armor.id && p.weaponId == rifle.id, "armor occupies its own slot without replacing the rifle");
            }
            Require(EquipmentCatalog.ArmorRating(p) > 0 && p.armorIds.All(id => id > 0), "all four armor slots contribute defense");
            yield return Capture("equipment-armor");
            Click("Unequip Selected Gear");
            yield return new WaitForSecondsRealtime(.3f);
            Require(p.armorIds[3] == 0, "unequip armor through inventory UI");
            Click("Equip Selected Gear");
            yield return new WaitForSecondsRealtime(.3f);
            qaArmorIds = (int[])p.armorIds.Clone();
            Click("Gear Slot " + rifle.id);
            yield return new WaitForSecondsRealtime(.25f);
            yield return Capture("equipment-precision");
            int reserve = p.items[(int)ItemKind.PrecisionAmmo];
            Click("Reload Gear");
            yield return new WaitForSecondsRealtime(.2f);
            Require(p.reloadRemaining > 0 && p.items[(int)ItemKind.PrecisionAmmo] == reserve, "reload starts without early reserve deduction");
            yield return Capture("equipment-reloading");
            yield return new WaitForSecondsRealtime(EquipmentCatalog.Stats(rifle).reload + .5f);
            Require(rifle.magazine > 0 && p.items[(int)ItemKind.PrecisionAmmo] < reserve, "native reload completes with matching rounds");
            Click("Close Inventory");
            // A clear shooting lane checks actual native combat, not just tooltip numbers.
            p.x = 30; p.y = 26;
            var target = new ZombieState { id = bridge.World.nextId++, x = 35, y = 26, hp = 500 };
            bridge.World.zombies.Add(target);
            yield return PointAtWorld(target.x, target.y);
            bridge.Send(new PlayerCommand { aimX = 1, aiming = true });
            yield return new WaitForSecondsRealtime(.1f);
            int magazine = rifle.magazine; float health = target.hp;
            bridge.Send(new PlayerCommand { action = "attack", aimX = target.x-p.x, aimY=target.y-p.y, aiming = true });
            Require(rifle.magazine == magazine - 1 && target.hp < health, "equipped precision rifle consumes magazine and damages target");
            qaGearMagazine = rifle.magazine;
            yield return Capture("precision-range");
            bridge.World.zombies.Clear();
            bridge.Send(new PlayerCommand { aimX = 1 });
        }

        void VerifyRestoredEquipment(PlayerState p)
        {
            var rifle = EquipmentCatalog.Find(p, qaGearId);
            Require(rifle != null && p.weaponId == qaGearId && rifle.magazine == qaGearMagazine &&
                rifle.attachments[(int)AttachmentSlot.Optic] == (int)PartKind.Scope4x &&
                rifle.attachments[(int)AttachmentSlot.Magazine] == (int)PartKind.ExtendedMag,
                "save/load preserves unique weapon, equipped slot, attachments and remaining magazine");
            Require(p.armorIds.SequenceEqual(qaArmorIds) && EquipmentCatalog.ArmorRating(p) > 0, "save/load preserves all four independent armor slots");
        }
    }
}
#endif
