using System;

namespace DeadCircuit
{
    public static class InventoryWeight
    {
        public const int CapacityGrams = 120000, WaterGrams = 500;
        // This guard also allows former inventories to be reduced by net-lighter crafts.
        internal const int MaxSavedGear = 512;

        public static int Grams(ItemKind item) => item switch
        {
            ItemKind.Wood => 500,
            ItemKind.Stone or ItemKind.Limestone or ItemKind.FireClay => 600,
            ItemKind.Ore or ItemKind.CopperOre or ItemKind.TinOre => 650,
            ItemKind.UraniumOre => 800,
            ItemKind.Iron or ItemKind.Copper or ItemKind.Bronze or ItemKind.Steel or ItemKind.Tin or ItemKind.PigIron or
                ItemKind.IronPlate or ItemKind.SteelPlate or ItemKind.HeatAlloy => 950,
            ItemKind.Scrap or ItemKind.Gear or ItemKind.Bearing or ItemKind.PipeFitting => 450,
            ItemKind.Circuit or ItemKind.AdvancedCircuit or ItemKind.Relay or ItemKind.PrecisionPart or ItemKind.ControlModule => 180,
            ItemKind.Polymer or ItemKind.Cable or ItemKind.CopperWire or ItemKind.Insulator => 160,
            ItemKind.Coil => 350,
            ItemKind.Motor => 1400,
            ItemKind.Clay or ItemKind.Brick or ItemKind.RefractoryBrick => 550,
            ItemKind.CrushedOre or ItemKind.Quartz or ItemKind.IndustrialSilicon or ItemKind.PureSilicon => 400,
            ItemKind.Wafer or ItemKind.Semiconductor => 40,
            ItemKind.Coal or ItemKind.Coke => 300,
            ItemKind.Fuel or ItemKind.Food or ItemKind.CrudeOil or ItemKind.LightFraction or ItemKind.ChemicalFeedstock or
                ItemKind.HeavyFraction or ItemKind.Diesel or ItemKind.Lubricant or ItemKind.ProcessWater or
                ItemKind.HotCoolant or ItemKind.Coolant => 500,
            ItemKind.NuclearFuel or ItemKind.NuclearWaste or ItemKind.UraniumConcentrate => 1100,
            ItemKind.Ammo => 15,
            ItemKind.RifleAmmo => 20,
            ItemKind.Shell or ItemKind.PrecisionAmmo or ItemKind.ArmorPiercingAmmo => 35,
            ItemKind.Charcoal => 120,
            ItemKind.Branch => 80,
            ItemKind.LooseStone => 250,
            ItemKind.DryGrass => 30,
            _ => throw new ArgumentOutOfRangeException(nameof(item))
        };

        public static int Grams(GearKind gear) => gear switch
        {
            GearKind.StoneAxe => 1200, GearKind.StonePickaxe => 1600,
            GearKind.SteelAxe => 2100, GearKind.SteelPickaxe => 2700, GearKind.Wrench => 1300,
            GearKind.Knife => 500, GearKind.Pistol => 900, GearKind.Shotgun => 3200,
            GearKind.AssaultRifle => 3500, GearKind.PrecisionRifle => 5400,
            GearKind.ClothHood => 500, GearKind.LeatherJacket => 1800, GearKind.WorkPants => 750, GearKind.WorkBoots => 1400,
            GearKind.SteelHelmet => 1900, GearKind.ArmorVest => 5900, GearKind.TacticalHelmet => 1500,
            GearKind.TacticalVest => 6400, GearKind.TacticalBoots => 1650, GearKind.RadiationSuit => 4200,
            _ => throw new ArgumentOutOfRangeException(nameof(gear))
        };

        public static int Grams(PartKind part) => part switch
        {
            PartKind.RedDot => 130, PartKind.Scope4x => 450, PartKind.Suppressor => 370, PartKind.MuzzleBrake => 90,
            PartKind.ExtendedMag => 210, PartKind.ErgonomicGrip => 150, PartKind.MatchBarrel => 700,
            _ => throw new ArgumentOutOfRangeException(nameof(part))
        };

        public static int Grams(FoodKind food) => food switch
        {
            FoodKind.Berries or FoodKind.Mushroom or FoodKind.Egg or FoodKind.BoiledEgg => 70,
            FoodKind.Apple or FoodKind.Carrot or FoodKind.Potato or FoodKind.Corn or FoodKind.Tomato or
                FoodKind.BakedPotato or FoodKind.RoastedCorn or FoodKind.FruitSalad => 180,
            FoodKind.Wheat or FoodKind.Beans or FoodKind.DriedMeat => 120,
            FoodKind.Pumpkin => 1200,
            FoodKind.RawMeat or FoodKind.RawPoultry or FoodKind.RawFish or FoodKind.RoastMeat or FoodKind.RoastPoultry or
                FoodKind.GrilledFish or FoodKind.Bread or FoodKind.Flour or FoodKind.CookedBeans => 250,
            FoodKind.VegetableStew or FoodKind.MeatStew or FoodKind.FishSoup or FoodKind.PumpkinSoup or FoodKind.HerbalTea => 400,
            FoodKind.Herb => 30,
            _ => throw new ArgumentOutOfRangeException(nameof(food))
        };

        public static int Grams(CropKind crop) => crop switch
        {
            CropKind.Potato => 60, CropKind.Corn or CropKind.Pumpkin or CropKind.Beans => 20,
            CropKind.Carrot or CropKind.Wheat or CropKind.Tomato or CropKind.Herb => 10,
            _ => throw new ArgumentOutOfRangeException(nameof(crop))
        };

        public static bool IsEquipped(PlayerState player, GearState gear)
        {
            if (player == null || gear == null || gear.id == 0) return false;
            if (gear.id == player.weaponId || gear.id == player.toolId) return true;
            foreach (int id in player.armorIds) if (id == gear.id) return true;
            return false;
        }

        public static int GearGrams(GearState gear)
        {
            if (gear == null) return 0;
            int grams = Grams(gear.kind) + (gear.magazine > 0 ? gear.magazine * Grams(gear.loadedAmmo) : 0);
            foreach (int part in gear.attachments) if (part >= 0) grams += Grams((PartKind)part);
            return grams;
        }

        public static long MaterialsGrams(int[] amounts)
        {
            long grams = 0;
            for (int i = 0; i < amounts.Length; i++)
                if (amounts[i] != 0) grams += (long)amounts[i] * Grams((ItemKind)i);
            return grams;
        }

        public static long CarriedGrams(PlayerState player)
        {
            if (player == null) return 0;
            long grams = MaterialsGrams(player.items);
            for (int i = 0; i < player.parts.Length; i++)
                if (player.parts[i] != 0) grams += (long)player.parts[i] * Grams((PartKind)i);
            foreach (var gear in player.gear)
                if (!IsEquipped(player, gear)) grams += GearGrams(gear);
            if (player.needs != null)
            {
                var needs = player.needs;
                grams += ((long)needs.waterRaw + needs.waterClean) * WaterGrams;
                for (int i = 0; i < needs.foods.Length; i++)
                    if (needs.foods[i] != 0) grams += (long)needs.foods[i] * Grams((FoodKind)i);
                for (int i = 0; i < needs.seeds.Length; i++)
                    if (needs.seeds[i] != 0) grams += (long)needs.seeds[i] * Grams((CropKind)i);
            }
            return grams;
        }

        public static long CookingDeltaGrams(SurvivalRecipeDefinition recipe)
        {
            long grams = (long)recipe.quantity * Grams(recipe.output) - (long)recipe.cleanWater * WaterGrams -
                (long)recipe.fuel * Grams(ItemKind.Fuel);
            for (int i = 0; i < recipe.foods.Length; i++)
                if (recipe.foods[i] > 0) grams -= (long)recipe.foods[i] * Grams((FoodKind)i);
            return grams;
        }

        public static int MillingDeltaGrams => 3 * Grams(FoodKind.Flour) - 2 * Grams(FoodKind.Wheat);

        public static bool CanFit(PlayerState player, long deltaGrams) =>
            deltaGrams <= 0 || deltaGrams <= CapacityGrams - CarriedGrams(player);

        public static bool CanExchange(PlayerState player, int[] costs, int[] outputs) =>
            CanFit(player, MaterialsGrams(outputs) - MaterialsGrams(costs));

        public static int Allowance(PlayerState player, int unitGrams) =>
            (int)(Math.Max(0, CapacityGrams - CarriedGrams(player)) / unitGrams);
    }
}
