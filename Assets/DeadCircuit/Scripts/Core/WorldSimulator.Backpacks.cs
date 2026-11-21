using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        private const int MaxDroppedBackpacks = 128;

        private DroppedBackpackState DroppedBackpackAt(int x, int y) =>
            State.droppedBackpacks.Find(drop => drop.x == x && drop.y == y);

        private void ChangeBackpack(PlayerState player, GearState next, DroppedBackpackState pickup = null)
        {
            GearState previous = InventoryWeight.EquippedPack(player);
            if (previous == next) return;
            AllocateBackpackContents(player);
            int x = RoundCell(player.x), y = RoundCell(player.y);
            int newGearCount = player.gear.Count - (previous == null ? 0 : 1 + (previous.backpackInventory?.gearIds.Count ?? 0)) +
                (pickup == null ? 0 : 1 + pickup.packedGear.Count);
            if (newGearCount > InventoryWeight.MaxSavedGear)
            { Notice(player, "请先整理装备再拾取背包。"); return; }

            if (previous != null)
            {
                if (pickup != null) { x = pickup.x; y = pickup.y; }
                else if (State.droppedBackpacks.Count >= MaxDroppedBackpacks || !TryBackpackDropCell(x, y, out x, out y))
                { Notice(player, "附近没有可放下背包的空地。"); return; }
            }

            if (pickup != null && !CanReceivePackContents(player, previous?.backpackInventory, pickup.gear.backpackInventory))
            { Notice(player, "请先整理随身库存再拾取背包。"); return; }
            if (pickup != null) State.droppedBackpacks.Remove(pickup);
            if (previous != null)
            {
                var drop = new DroppedBackpackState { x = x, y = y, gear = previous };
                if (previous.backpackInventory == null) previous.backpackInventory = new BackpackInventoryState();
                TransferBackpackContents(player, previous.backpackInventory, drop.packedGear, false);
                player.gear.Remove(previous);
                State.droppedBackpacks.Add(drop);
            }

            player.backpackId = next?.id ?? 0;
            if (next != null)
            {
                if (pickup != null)
                {
                    player.gear.Add(next);
                    TransferBackpackContents(player, next.backpackInventory, pickup.packedGear, true);
                }
                if (next.backpackInventory == null) next.backpackInventory = new BackpackInventoryState();
                Notice(player, "已背上" + EquipmentCatalog.GearNames[(int)next.kind] +
                    (previous == null ? "。" : "，原背包及包内物品已放在地上。"));
            }
            else Notice(player, "已放下背包，包内物品仍在原包中。");
            player.interactCooldown = .25f;
        }

        private bool TryBackpackDropCell(int x, int y, out int dropX, out int dropY)
        {
            for (int distance = 0; distance <= 2; distance++)
                for (int dy = -distance; dy <= distance; dy++) for (int dx = -distance; dx <= distance; dx++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) != distance) continue;
                    int cx = x + dx, cy = y + dy;
                    if (!Inside(cx, cy) || BlockedAt(cx, cy) || NodeAt(cx, cy) != null || BuildingAt(cx, cy) != null ||
                        FloorWorld.IsProtectedCell(State, cx, cy) || DroppedBackpackAt(cx, cy) != null || SurvivalPlotAt(cx, cy) != null ||
                        !LineClear(x, y, cx, cy, cx, cy)) continue;
                    dropX = cx; dropY = cy; return true;
                }
            dropX = x; dropY = y; return false;
        }

        private static void TransferBackpackContents(PlayerState player, BackpackInventoryState pack, List<GearState> contents, bool pickup)
        {
            if (pack == null) return;
            int sign = pickup ? 1 : -1;
            AddQuantities(player.items, pack.items, sign);
            AddQuantities(player.parts, pack.parts, sign);
            AddQuantities(player.needs.foods, pack.foods, sign);
            AddQuantities(player.needs.seeds, pack.seeds, sign);
            player.needs.waterRaw += sign * pack.waterRaw;
            player.needs.waterClean += sign * pack.waterClean;
            if (pickup) player.gear.AddRange(contents);
            else foreach (int id in pack.gearIds)
            {
                GearState item = EquipmentCatalog.Find(player, id);
                contents.Add(item);
                player.gear.Remove(item);
            }
        }

        private static bool CanReceivePackContents(PlayerState player, BackpackInventoryState old, BackpackInventoryState incoming)
        {
            if (incoming == null) return true;
            return CombinedCountsFit(player.items, old?.items, incoming.items) && CombinedCountsFit(player.parts, old?.parts, incoming.parts) &&
                CombinedCountsFit(player.needs.foods, old?.foods, incoming.foods) && CombinedCountsFit(player.needs.seeds, old?.seeds, incoming.seeds) &&
                player.needs.waterRaw - (old?.waterRaw ?? 0) <= InventoryLimit - incoming.waterRaw &&
                player.needs.waterClean - (old?.waterClean ?? 0) <= InventoryLimit - incoming.waterClean;
        }

        private static bool CombinedCountsFit(int[] owned, int[] outgoing, int[] incoming)
        {
            for (int i = 0; i < owned.Length; i++)
                if (owned[i] - (outgoing == null ? 0 : outgoing[i]) > InventoryLimit - incoming[i]) return false;
            return true;
        }

        private static void AddQuantities(int[] target, int[] source, int sign)
        {
            for (int i = 0; i < source.Length; i++) target[i] += sign * source[i];
        }

        private static void MoveBackpackItem(PlayerState player, PlayerCommand cmd, bool put)
        {
            BackpackInventoryState bag = InventoryWeight.EquippedPack(player)?.backpackInventory;
            if (bag == null) return;
            long packed = InventoryWeight.BackpackContentsGrams(bag, player.gear);
            long space = put ? InventoryWeight.CapacityGrams(player) - InventoryWeight.BaseCapacityGrams - packed :
                InventoryWeight.BaseCapacityGrams - (InventoryWeight.CarriedGrams(player) - packed);
            if (space <= 0) return;

            if (cmd.x == 6)
            {
                var gear = EquipmentCatalog.Find(player, cmd.arg);
                if (gear == null || EquipmentCatalog.IsBackpack(gear.kind) || InventoryWeight.IsEquipped(player, gear) ||
                    InventoryWeight.GearGrams(gear) > space) return;
                if (!put) bag.gearIds.Remove(gear.id);
                else if (!bag.gearIds.Contains(gear.id)) bag.gearIds.Add(gear.id);
                return;
            }
            int id = cmd.arg;
            switch (cmd.x)
            {
                case 0: MoveStack(player.items, bag.items, id, put, space, i => InventoryWeight.Grams((ItemKind)i)); break;
                case 1: MoveStack(player.parts, bag.parts, id, put, space, i => InventoryWeight.Grams((PartKind)i)); break;
                case 2: MoveStack(player.needs.foods, bag.foods, id, put, space, i => InventoryWeight.Grams((FoodKind)i)); break;
                case 3: MoveStack(player.needs.seeds, bag.seeds, id, put, space, i => InventoryWeight.Grams((CropKind)i)); break;
                case 4: bag.waterRaw += MoveAmount(player.needs.waterRaw, bag.waterRaw, put, space, InventoryWeight.WaterGrams); break;
                case 5: bag.waterClean += MoveAmount(player.needs.waterClean, bag.waterClean, put, space, InventoryWeight.WaterGrams); break;
            }
        }

        private static int MoveAmount(int total, int packed, bool put, long remaining, int grams) =>
            put ? (int)Math.Min(total - packed, remaining / grams) : -(int)Math.Min(packed, remaining / grams);

        private static void MoveStack(int[] total, int[] packed, int id, bool put, long space, Func<int, int> grams)
        {
            if (id < 0 || id >= total.Length) return;
            packed[id] += MoveAmount(total[id], packed[id], put, space, grams(id));
        }

        // Commands still operate on all holdings that the player can reach. Reduce recorded
        // pack counts after consumption, then route any excess personal load into worn storage.
        private static void AllocateBackpackContents(PlayerState player)
        {
            GearState equipped = InventoryWeight.EquippedPack(player);
            if (equipped == null) return;
            if (equipped.backpackInventory == null) equipped.backpackInventory = new BackpackInventoryState();
            BackpackInventoryState bag = equipped.backpackInventory;
            ClampPacked(player.items, bag.items); ClampPacked(player.parts, bag.parts);
            ClampPacked(player.needs.foods, bag.foods); ClampPacked(player.needs.seeds, bag.seeds);
            bag.waterRaw = Math.Min(bag.waterRaw, player.needs.waterRaw);
            bag.waterClean = Math.Min(bag.waterClean, player.needs.waterClean);
            bag.gearIds.RemoveAll(id => !CanPackGear(player, EquipmentCatalog.Find(player, id)));
            long packed = InventoryWeight.BackpackContentsGrams(bag, player.gear);
            long excess = InventoryWeight.CarriedGrams(player) - packed - InventoryWeight.BaseCapacityGrams;
            long space = InventoryWeight.CapacityGrams(player) - InventoryWeight.BaseCapacityGrams - packed;
            if (excess <= 0 || space <= 0) return;
            AllocateStacks(player.items, bag.items, i => InventoryWeight.Grams((ItemKind)i), ref excess, ref space);
            AllocateStacks(player.parts, bag.parts, i => InventoryWeight.Grams((PartKind)i), ref excess, ref space);
            AllocateStacks(player.needs.foods, bag.foods, i => InventoryWeight.Grams((FoodKind)i), ref excess, ref space);
            AllocateStacks(player.needs.seeds, bag.seeds, i => InventoryWeight.Grams((CropKind)i), ref excess, ref space);
            AllocateStack(player.needs.waterRaw, ref bag.waterRaw, InventoryWeight.WaterGrams, ref excess, ref space);
            AllocateStack(player.needs.waterClean, ref bag.waterClean, InventoryWeight.WaterGrams, ref excess, ref space);
            foreach (GearState item in player.gear)
            {
                if (excess <= 0 || space <= 0) break;
                if (!CanPackGear(player, item) || bag.gearIds.Contains(item.id)) continue;
                int grams = InventoryWeight.GearGrams(item);
                if (grams > space) continue;
                bag.gearIds.Add(item.id); space -= grams; excess -= grams;
            }
        }

        private static bool CanPackGear(PlayerState player, GearState gear) =>
            gear != null && !EquipmentCatalog.IsBackpack(gear.kind) && !InventoryWeight.IsEquipped(player, gear);

        private static void ClampPacked(int[] total, int[] packed)
        {
            for (int i = 0; i < total.Length; i++) packed[i] = Math.Min(total[i], packed[i]);
        }

        private static void AllocateStacks(int[] total, int[] packed, Func<int, int> grams, ref long excess, ref long space)
        {
            for (int i = 0; i < total.Length && excess > 0 && space > 0; i++)
                if (total[i] > packed[i]) AllocateStack(total[i], ref packed[i], grams(i), ref excess, ref space);
        }

        private static void AllocateStack(int total, ref int packed, int grams, ref long excess, ref long space)
        {
            if (excess <= 0 || space <= 0) return;
            int count = (int)Math.Min(total - packed, Math.Min(space / grams, (excess + grams - 1) / grams));
            packed += count; space -= (long)count * grams; excess -= (long)count * grams;
        }
    }
}
