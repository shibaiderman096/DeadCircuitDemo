using DeadCircuit;

static partial class Checks
{
    static void InventoryWeightChecks()
    {
        Run("inventory weight ground pickup respects other materials at exact capacity", () =>
        {
            var world = Flat(era: TechEra.Primitive);
            world.primitiveVersion = 1;
            var sim = new WorldSimulator(world);
            var player = sim.AddPlayer("load-check", "Load Check");
            player.x = 10; player.y = 10;
            Array.Clear(player.items);
            player.items[(int)ItemKind.Wood] = 100; // 100 × 500 g leaves no room for even a branch.
            var pickup = new NodeState { id = sim.State.nextId++, x = 11, y = 10, kind = NodeKind.Branch, hp = 1, stock = 1 };
            sim.State.nodes.Add(pickup);
            sim.Apply(Command(player, "interact", 11, 10));
            Require(player.items[(int)ItemKind.Branch] == 0 && sim.State.nodes.Contains(pickup), "another stack did not constrain the pickup");
            player.items[(int)ItemKind.Wood]--;
            Advance(sim, .6f);
            sim.Apply(Command(player, "interact", 11, 10));
            Require(player.items[(int)ItemKind.Branch] == 1 && !sim.State.nodes.Contains(pickup), "freed weight did not allow pickup");
        });
        Run("inventory weight chest transfer stops at the total cap and can put a stack back", () =>
        {
            var world = Flat(era: TechEra.Primitive);
            world.primitiveVersion = 1;
            var sim = new WorldSimulator(world);
            var player = sim.AddPlayer("transfer-check", "Transfer Check");
            player.x = 10; player.y = 10;
            Array.Clear(player.items);
            player.items[(int)ItemKind.Wood] = 96; // 48 kg leaves room for 3 stones (1.8 kg).
            var chest = Add(sim.State, BuildKind.Chest, 11, 10);
            chest.items[(int)ItemKind.Stone] = 5;
            sim.Apply(Command(player, "interact", 11, 10));
            Require(player.items[(int)ItemKind.Stone] == 3 && chest.items[(int)ItemKind.Stone] == 2,
                "transfer did not preserve material that exceeded the remaining shared weight");
            Advance(sim, .5f);
            chest.items[(int)ItemKind.Wood] = 110;
            sim.Apply(Command(player, "storeitem", arg: (int)ItemKind.Wood));
            Require(chest.items[(int)ItemKind.Wood] == 200 && player.items[(int)ItemKind.Wood] == 6,
                "store did not keep the overflow in the backpack");
            Advance(sim, .5f);
            player.x = 30;
            sim.Apply(Command(player, "storeitem", arg: (int)ItemKind.Stone));
            Require(chest.items[(int)ItemKind.Stone] == 2, "stored an item out of range");
        });
        Run("backpack tier craft wear pack drop restore and retrieve conserve each possession", () =>
        {
            var world = Flat(era: TechEra.Primitive);
            world.primitiveVersion = 1;
            var sim = new WorldSimulator(world);
            var p = sim.AddPlayer("pack-owner", "Owner");
            var partner = sim.AddPlayer("pack-picker", "Partner");
            p.x = 10; p.y = 10; partner.x = 11; partner.y = 11;
            p.items[(int)ItemKind.DryGrass] = 1;
            Require(InventoryWeight.CapacityGrams(p) == 50000, "unworn capacity");
            sim.Apply(Command(p, "gearcraft", arg: (int)GearKind.GrassBackpack));
            GearState pack = p.gear.Single();
            Require(p.items[(int)ItemKind.DryGrass] == 0 && p.items[(int)ItemKind.Wood] == 9, "grass pack recipe was not paid");
            sim.Apply(Command(p, "equip", arg: pack.id));
            Require(p.backpackId == pack.id && p.weaponId == 0 && p.armorIds.All(id => id == 0) &&
                InventoryWeight.CapacityGrams(p) == 100000, "backpack changed other slots or capacity is wrong");
            sim.Apply(Command(p, "packput", x: 0, arg: (int)ItemKind.Wood));
            Require(pack.backpackInventory.items[(int)ItemKind.Wood] == 9, "put did not move the stack into the pack");
            sim.Apply(Command(p, "packtake", x: 0, arg: (int)ItemKind.Wood));
            Require(pack.backpackInventory.items[(int)ItemKind.Wood] == 0 && p.items[(int)ItemKind.Wood] == 9, "take duplicated or lost a stack");
            sim.Apply(Command(p, "packput", x: 0, arg: (int)ItemKind.Wood));
            Advance(sim, .6f);
            sim.Apply(Command(p, "craft", arg: 0));
            Require(pack.backpackInventory.items[(int)ItemKind.Wood] == 6, "crafting did not deduct packed materials");
            var axe = new GearState { id = sim.State.nextId++, kind = GearKind.StoneAxe, durability = 42 };
            p.gear.Add(axe);
            sim.Apply(Command(p, "packput", x: 6, arg: axe.id));
            sim.Apply(Command(p, "unequip", arg: pack.id));
            var dropped = sim.State.droppedBackpacks.Single();
            Require(p.backpackId == 0 && p.items[(int)ItemKind.Wood] == 0 && p.gear.Count == 0 &&
                p.items[(int)ItemKind.Fuel] == 2 && dropped.gear.id == pack.id && dropped.packedGear.Single() == axe,
                "unequipping lost, copied, or dropped the wrong possessions");
            var restored = new WorldSimulator(Saved(sim.State));
            var other = restored.State.players.Single(a => a.id == partner.id);
            int wood = other.items[(int)ItemKind.Wood];
            restored.Apply(Command(other, "primary", dropped.x, dropped.y));
            Advance(restored, 1);
            Require(other.backpackId == pack.id && other.items[(int)ItemKind.Wood] == wood + 6 &&
                other.gear.Single(g => g.id == axe.id).durability == 42 && restored.State.droppedBackpacks.Count == 0,
                "load and pickup did not recover the same container and equipment");
            restored.Apply(Command(other, "interact", dropped.x, dropped.y));
            Require(other.items[(int)ItemKind.Wood] == wood + 6, "repeated pickup duplicated contents");
        });
        Run("backpack tiers exchange physical packs and autosort incoming weight", () =>
        {
            var world = Flat(); world.primitiveVersion = 0;
            Add(world, BuildKind.Workbench, 10, 10);
            var sim = new WorldSimulator(world);
            var p = sim.AddPlayer("bag-tier", "Carrier"); p.x = 8; p.y = 10;
            Array.Clear(p.items);
            foreach (var (kind, cap) in new[] { (GearKind.ReinforcedBackpack, 120000), (GearKind.EngineerBackpack, 160000) })
            {
                p.items = EquipmentCatalog.Gear(kind).cost;
                Advance(sim, .6f);
                sim.Apply(Command(p, "gearcraft", arg: (int)kind));
                var next = p.gear.Single(g => g.kind == kind);
                sim.Apply(Command(p, "equip", arg: next.id));
                Require(InventoryWeight.CapacityGrams(p) == cap, "workbench tier did not grant its capacity");
            }
            var previous = sim.State.droppedBackpacks.Single();
            Require(previous.gear.kind == GearKind.ReinforcedBackpack, "swap did not leave the old pack in the world");
            var chest = Add(sim.State, BuildKind.Chest, 8, 11);
            chest.items[(int)ItemKind.Wood] = 130;
            Advance(sim, .6f);
            sim.Apply(Command(p, "interact", 8, 11));
            var current = InventoryWeight.EquippedPack(p);
            Require(p.items[(int)ItemKind.Wood] == 130 && current.backpackInventory.items[(int)ItemKind.Wood] > 0,
                "taken inventory beyond personal carrying weight did not enter the worn pack");
            int personal = p.items[(int)ItemKind.Wood] - current.backpackInventory.items[(int)ItemKind.Wood];
            Advance(sim, .6f);
            sim.Apply(Command(p, "interact", previous.x, previous.y));
            Require(p.backpackId == previous.gear.id && p.items[(int)ItemKind.Wood] == personal &&
                sim.State.droppedBackpacks.Single().gear == current && current.backpackInventory.items[(int)ItemKind.Wood] == 130 - personal,
                "swapping packs moved the previous pack contents to the new container");
            var corrupt = Saved(sim.State);
            corrupt.droppedBackpacks[0].gear.backpackInventory.items[(int)ItemKind.Wood] = 1000;
            Reject(corrupt, "overweight ground pack accepted");
        });
        Run("inventory weight gives every collectible a positive distinct-by-purpose unit", () =>
        {
            Require(Enum.GetValues<ItemKind>().All(i => InventoryWeight.Grams(i) >= 10) &&
                Enum.GetValues<GearKind>().All(i => InventoryWeight.Grams(i) >= 500) &&
                Enum.GetValues<PartKind>().All(i => InventoryWeight.Grams(i) > 0) &&
                Enum.GetValues<FoodKind>().All(i => InventoryWeight.Grams(i) > 0) &&
                Enum.GetValues<CropKind>().All(i => InventoryWeight.Grams(i) >= 10), "a collectible has no usable unit weight");
            Require(InventoryWeight.Grams(ItemKind.Ore) > InventoryWeight.Grams(ItemKind.Branch) &&
                InventoryWeight.Grams(GearKind.ArmorVest) > InventoryWeight.Grams(GearKind.ClothHood), "heavy materials or wearables were misclassified");
        });
    }
}
