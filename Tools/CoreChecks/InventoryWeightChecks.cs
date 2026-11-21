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
            player.items[(int)ItemKind.Wood] = 240; // 240 × 500 g leaves no room for even a branch.
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
            player.items[(int)ItemKind.Wood] = 236; // 118 kg leaves room for 3 stones (1.8 kg).
            var chest = Add(sim.State, BuildKind.Chest, 11, 10);
            chest.items[(int)ItemKind.Stone] = 5;
            sim.Apply(Command(player, "interact", 11, 10));
            Require(player.items[(int)ItemKind.Stone] == 3 && chest.items[(int)ItemKind.Stone] == 2,
                "transfer did not preserve material that exceeded the remaining shared weight");
            Advance(sim, .5f);
            sim.Apply(Command(player, "storeitem", arg: (int)ItemKind.Wood));
            Require(chest.items[(int)ItemKind.Wood] == 200 && player.items[(int)ItemKind.Wood] == 36,
                "store did not keep the overflow in the backpack");
            Advance(sim, .5f);
            player.x = 30;
            sim.Apply(Command(player, "storeitem", arg: (int)ItemKind.Stone));
            Require(chest.items[(int)ItemKind.Stone] == 2, "stored an item out of range");
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
