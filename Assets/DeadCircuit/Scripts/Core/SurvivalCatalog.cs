using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public sealed class SurvivalFoodDefinition
    {
        public readonly FoodKind kind;
        public readonly string name;
        public readonly float hunger, hydration, nutrition, warmth, infection, healing;
        public readonly bool edible;
        internal SurvivalFoodDefinition(FoodKind kind, string name, float hunger, float hydration, float nutrition,
            float warmth = 0, float infection = 0, float healing = 0, bool edible = true)
        { this.kind = kind; this.name = name; this.hunger = hunger; this.hydration = hydration; this.nutrition = nutrition;
            this.warmth = warmth; this.infection = infection; this.healing = healing; this.edible = edible; }
    }

    public sealed class SurvivalRecipeDefinition
    {
        public readonly int id;
        public readonly string name;
        public readonly BuildKind station;
        public readonly int[] foods;
        public readonly FoodKind output;
        public readonly int quantity, cleanWater, fuel;
        internal SurvivalRecipeDefinition(int id, string name, BuildKind station, FoodKind output, int quantity,
            int cleanWater, int fuel, params FoodKind[] ingredients)
        {
            this.id = id; this.name = name; this.station = station; this.output = output; this.quantity = quantity;
            this.cleanWater = cleanWater; this.fuel = fuel; foods = new int[SurvivalCatalog.FoodCount];
            foreach (var ingredient in ingredients) foods[(int)ingredient]++;
        }
    }

    public sealed class SurvivalCropDefinition
    {
        public readonly CropKind kind;
        public readonly string name;
        public readonly FoodKind food;
        public readonly float growSeconds, waterPerSecond, minTemperature, maxTemperature;
        public readonly int harvestCount, seedYield;
        internal SurvivalCropDefinition(CropKind kind, string name, FoodKind food, float growSeconds, float waterPerSecond,
            float minTemperature, float maxTemperature, int harvestCount, int seedYield)
        { this.kind = kind; this.name = name; this.food = food; this.growSeconds = growSeconds; this.waterPerSecond = waterPerSecond;
            this.minTemperature = minTemperature; this.maxTemperature = maxTemperature; this.harvestCount = harvestCount; this.seedYield = seedYield; }
    }

    public sealed class SurvivalCreatureDefinition
    {
        public readonly CreatureKind kind;
        public readonly string name;
        public readonly CreatureTemperament temperament;
        public readonly float health, speed, fearRange, damage, aggroRange;
        public readonly int biomeMask, meat;
        public readonly FoodKind food;
        public readonly bool huntsSmallPrey;
        internal SurvivalCreatureDefinition(CreatureKind kind, string name, CreatureTemperament temperament, float health,
            float speed, float fearRange, float damage, float aggroRange, int biomeMask, int meat, FoodKind food, bool huntsSmallPrey = false)
        { this.kind = kind; this.name = name; this.temperament = temperament; this.health = health; this.speed = speed;
            this.fearRange = fearRange; this.damage = damage; this.aggroRange = aggroRange; this.biomeMask = biomeMask;
            this.meat = meat; this.food = food; this.huntsSmallPrey = huntsSmallPrey; }
    }

    public static class SurvivalCatalog
    {
        public const int FoodCount = 31, CropCount = 8, CreatureCount = 12, WeatherCount = 10;
        public const int MaxPlots = 128, MaxCreatures = 32;
        public static readonly IReadOnlyList<string> WeatherNames = Array.AsReadOnly(new[]
            { "晴", "阴", "毛雨", "雨", "雷暴", "雾", "热浪", "寒潮", "雪", "沙尘" });
        public static readonly IReadOnlyList<SurvivalFoodDefinition> Foods = Array.AsReadOnly(new[]
        {
            new SurvivalFoodDefinition(FoodKind.Berries, "浆果", 8, 6, 5),
            new SurvivalFoodDefinition(FoodKind.Mushroom, "野蘑菇", 6, 2, 3, infection: 18),
            new SurvivalFoodDefinition(FoodKind.Apple, "苹果", 12, 10, 8),
            new SurvivalFoodDefinition(FoodKind.Carrot, "胡萝卜", 10, 4, 9),
            new SurvivalFoodDefinition(FoodKind.Potato, "生土豆", 9, 1, 4, infection: 8),
            new SurvivalFoodDefinition(FoodKind.Corn, "玉米", 14, 2, 7),
            new SurvivalFoodDefinition(FoodKind.Wheat, "小麦", 0, 0, 0, edible: false),
            new SurvivalFoodDefinition(FoodKind.Tomato, "番茄", 8, 14, 8),
            new SurvivalFoodDefinition(FoodKind.Pumpkin, "南瓜", 18, 5, 10),
            new SurvivalFoodDefinition(FoodKind.Beans, "生豆", 12, -2, 7, infection: 15),
            new SurvivalFoodDefinition(FoodKind.RawMeat, "生红肉", 20, -3, 8, infection: 35),
            new SurvivalFoodDefinition(FoodKind.RawPoultry, "生禽肉", 17, -2, 7, infection: 45),
            new SurvivalFoodDefinition(FoodKind.RawFish, "生鱼", 16, 2, 9, infection: 30),
            new SurvivalFoodDefinition(FoodKind.Egg, "生蛋", 9, 1, 9, infection: 10),
            new SurvivalFoodDefinition(FoodKind.RoastMeat, "烤肉", 34, -5, 20, .15f, healing: 2),
            new SurvivalFoodDefinition(FoodKind.RoastPoultry, "烤禽肉", 30, -3, 19, .12f, healing: 2),
            new SurvivalFoodDefinition(FoodKind.GrilledFish, "烤鱼", 28, -1, 22, .1f, healing: 3),
            new SurvivalFoodDefinition(FoodKind.BoiledEgg, "水煮蛋", 18, 2, 15, .05f),
            new SurvivalFoodDefinition(FoodKind.BakedPotato, "烤土豆", 25, 0, 12, .12f),
            new SurvivalFoodDefinition(FoodKind.RoastedCorn, "烤玉米", 24, -1, 13, .08f),
            new SurvivalFoodDefinition(FoodKind.Bread, "面包", 38, -8, 16),
            new SurvivalFoodDefinition(FoodKind.VegetableStew, "蔬菜炖汤", 30, 28, 28, .35f, healing: 4),
            new SurvivalFoodDefinition(FoodKind.MeatStew, "肉炖汤", 48, 22, 32, .4f, healing: 5),
            new SurvivalFoodDefinition(FoodKind.FishSoup, "鱼汤", 35, 32, 30, .3f, healing: 4),
            new SurvivalFoodDefinition(FoodKind.FruitSalad, "水果沙拉", 26, 22, 24, healing: 2),
            new SurvivalFoodDefinition(FoodKind.Flour, "面粉", 0, 0, 0, edible: false),
            new SurvivalFoodDefinition(FoodKind.DriedMeat, "肉干", 42, -12, 18),
            new SurvivalFoodDefinition(FoodKind.CookedBeans, "煮豆", 29, 8, 23, .1f),
            new SurvivalFoodDefinition(FoodKind.PumpkinSoup, "南瓜汤", 32, 30, 25, .3f, healing: 3),
            new SurvivalFoodDefinition(FoodKind.HerbalTea, "药草茶", 2, 25, 5, .25f, infection: -25, healing: 1),
            new SurvivalFoodDefinition(FoodKind.Herb, "生药草", 0, 0, 0, edible: false)
        });
        public static readonly IReadOnlyList<SurvivalRecipeDefinition> Recipes = Array.AsReadOnly(new[]
        {
            new SurvivalRecipeDefinition(0, "烤肉", BuildKind.StoneFurnace, FoodKind.RoastMeat, 1, 0, 1, FoodKind.RawMeat),
            new SurvivalRecipeDefinition(1, "烤禽肉", BuildKind.StoneFurnace, FoodKind.RoastPoultry, 1, 0, 1, FoodKind.RawPoultry),
            new SurvivalRecipeDefinition(2, "烤鱼", BuildKind.StoneFurnace, FoodKind.GrilledFish, 1, 0, 1, FoodKind.RawFish),
            new SurvivalRecipeDefinition(3, "水煮蛋", BuildKind.StoneFurnace, FoodKind.BoiledEgg, 2, 1, 1, FoodKind.Egg, FoodKind.Egg),
            new SurvivalRecipeDefinition(4, "烤土豆", BuildKind.StoneFurnace, FoodKind.BakedPotato, 1, 0, 1, FoodKind.Potato),
            new SurvivalRecipeDefinition(5, "烤玉米", BuildKind.StoneFurnace, FoodKind.RoastedCorn, 1, 0, 1, FoodKind.Corn),
            new SurvivalRecipeDefinition(6, "磨面粉", BuildKind.Workbench, FoodKind.Flour, 1, 0, 0, FoodKind.Wheat, FoodKind.Wheat),
            new SurvivalRecipeDefinition(7, "烤面包", BuildKind.StoneFurnace, FoodKind.Bread, 1, 1, 1, FoodKind.Flour, FoodKind.Flour),
            new SurvivalRecipeDefinition(8, "蔬菜炖汤", BuildKind.StoneFurnace, FoodKind.VegetableStew, 2, 2, 1, FoodKind.Carrot, FoodKind.Potato, FoodKind.Tomato),
            new SurvivalRecipeDefinition(9, "肉炖汤", BuildKind.StoneFurnace, FoodKind.MeatStew, 2, 2, 1, FoodKind.RawMeat, FoodKind.Carrot, FoodKind.Potato),
            new SurvivalRecipeDefinition(10, "鱼汤", BuildKind.StoneFurnace, FoodKind.FishSoup, 2, 2, 1, FoodKind.RawFish, FoodKind.Tomato),
            new SurvivalRecipeDefinition(11, "水果沙拉", BuildKind.Workbench, FoodKind.FruitSalad, 1, 0, 0, FoodKind.Berries, FoodKind.Apple),
            new SurvivalRecipeDefinition(12, "烘肉干", BuildKind.StoneFurnace, FoodKind.DriedMeat, 1, 0, 1, FoodKind.RawMeat, FoodKind.RawMeat),
            new SurvivalRecipeDefinition(13, "煮豆", BuildKind.StoneFurnace, FoodKind.CookedBeans, 1, 1, 1, FoodKind.Beans, FoodKind.Beans),
            new SurvivalRecipeDefinition(14, "南瓜汤", BuildKind.StoneFurnace, FoodKind.PumpkinSoup, 2, 2, 1, FoodKind.Pumpkin, FoodKind.Pumpkin),
            new SurvivalRecipeDefinition(15, "煮药草茶", BuildKind.StoneFurnace, FoodKind.HerbalTea, 1, 1, 1, FoodKind.Herb)
        });
        public static readonly IReadOnlyList<SurvivalCropDefinition> Crops = Array.AsReadOnly(new[]
        {
            new SurvivalCropDefinition(CropKind.Carrot, "胡萝卜", FoodKind.Carrot, 120, .08f, 4, 30, 3, 2),
            new SurvivalCropDefinition(CropKind.Potato, "土豆", FoodKind.Potato, 150, .07f, 2, 29, 4, 2),
            new SurvivalCropDefinition(CropKind.Corn, "玉米", FoodKind.Corn, 180, .13f, 12, 36, 4, 3),
            new SurvivalCropDefinition(CropKind.Wheat, "小麦", FoodKind.Wheat, 140, .06f, 5, 32, 6, 3),
            new SurvivalCropDefinition(CropKind.Tomato, "番茄", FoodKind.Tomato, 130, .12f, 10, 33, 4, 2),
            new SurvivalCropDefinition(CropKind.Pumpkin, "南瓜", FoodKind.Pumpkin, 210, .15f, 9, 34, 3, 2),
            new SurvivalCropDefinition(CropKind.Beans, "豆", FoodKind.Beans, 160, .09f, 8, 32, 5, 3),
            new SurvivalCropDefinition(CropKind.Herb, "药草", FoodKind.Herb, 110, .05f, 3, 28, 3, 2)
        });
        // Masks use the stable ordinals from WorldGeneration.BiomeKind.
        public static readonly IReadOnlyList<SurvivalCreatureDefinition> Creatures = Array.AsReadOnly(new[]
        {
            new SurvivalCreatureDefinition(CreatureKind.Hare, "野兔", CreatureTemperament.Timid, 18, 3.9f, 7, 0, 0, 39 | 1 << 8 | 1 << 9, 1, FoodKind.RawMeat),
            new SurvivalCreatureDefinition(CreatureKind.Deer, "鹿", CreatureTemperament.Timid, 65, 3.6f, 8, 0, 0, 7 | 1 << 8 | 1 << 9, 5, FoodKind.RawMeat),
            new SurvivalCreatureDefinition(CreatureKind.Boar, "野猪", CreatureTemperament.Defensive, 95, 2.5f, 4, 14, 2.8f, 19 | 1 << 6 | 1 << 9, 5, FoodKind.RawMeat),
            new SurvivalCreatureDefinition(CreatureKind.Chicken, "野鸡", CreatureTemperament.Timid, 14, 2.2f, 5, 0, 0, 3 | 1 << 8 | 1 << 9, 2, FoodKind.RawPoultry),
            new SurvivalCreatureDefinition(CreatureKind.Wolf, "狼", CreatureTemperament.Predator, 72, 3.1f, 0, 12, 8, 38 | 1 << 7 | 1 << 8 | 1 << 9, 3, FoodKind.RawMeat),
            new SurvivalCreatureDefinition(CreatureKind.Bear, "熊", CreatureTemperament.Predator, 240, 2.1f, 0, 25, 5, 6 | 1 << 6 | 1 << 7 | 1 << 9, 10, FoodKind.RawMeat),
            new SurvivalCreatureDefinition(CreatureKind.Fox, "狐", CreatureTemperament.Predator, 32, 3.4f, 7, 6, 0, 47 | 1 << 6 | 1 << 8 | 1 << 9, 2, FoodKind.RawMeat, true),
            new SurvivalCreatureDefinition(CreatureKind.Goat, "山羊", CreatureTemperament.Timid, 52, 3.0f, 6, 0, 0, 45 | 1 << 7, 4, FoodKind.RawMeat),
            new SurvivalCreatureDefinition(CreatureKind.Cattle, "野牛", CreatureTemperament.Defensive, 180, 1.9f, 3, 19, 2.0f, 17 | 1 << 6 | 1 << 9, 9, FoodKind.RawMeat),
            new SurvivalCreatureDefinition(CreatureKind.Sheep, "羊", CreatureTemperament.Docile, 48, 2.3f, 4, 0, 0, 37 | 1 << 7, 4, FoodKind.RawMeat),
            new SurvivalCreatureDefinition(CreatureKind.Duck, "野鸭", CreatureTemperament.Timid, 16, 2.6f, 5, 0, 0, 16 | 1 << 6 | 1 << 9, 2, FoodKind.RawPoultry),
            new SurvivalCreatureDefinition(CreatureKind.Pheasant, "雉", CreatureTemperament.Timid, 22, 2.9f, 6, 0, 0, 11 | 1 << 8, 2, FoodKind.RawPoultry)
        });

        public static SurvivalFoodDefinition Food(int id) => id >= 0 && id < FoodCount ? Foods[id] : null;
        public static SurvivalRecipeDefinition Recipe(int id) => id >= 0 && id < Recipes.Count ? Recipes[id] : null;
        public static SurvivalCropDefinition Crop(int id) => id >= 0 && id < CropCount ? Crops[id] : null;
        public static SurvivalCreatureDefinition Creature(int id) => id >= 0 && id < CreatureCount ? Creatures[id] : null;
    }
}
