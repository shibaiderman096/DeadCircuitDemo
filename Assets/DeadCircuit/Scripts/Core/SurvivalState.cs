using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    // Ordinals are v6 save/network contracts. Do not reorder. Industrial ItemKind stays unchanged.
    public enum WeatherKind { Clear, Overcast, Drizzle, Rain, Thunderstorm, Fog, Heatwave, ColdSnap, Snow, DustStorm }
    public enum FoodKind
    {
        Berries, Mushroom, Apple, Carrot, Potato, Corn, Wheat, Tomato, Pumpkin, Beans,
        RawMeat, RawPoultry, RawFish, Egg, RoastMeat, RoastPoultry, GrilledFish, BoiledEgg,
        BakedPotato, RoastedCorn, Bread, VegetableStew, MeatStew, FishSoup, FruitSalad,
        Flour, DriedMeat, CookedBeans, PumpkinSoup, HerbalTea, Herb
    }
    public enum CropKind { Carrot, Potato, Corn, Wheat, Tomato, Pumpkin, Beans, Herb }
    public enum CreatureKind { Hare, Deer, Boar, Chicken, Wolf, Bear, Fox, Goat, Cattle, Sheep, Duck, Pheasant }
    public enum CreatureTemperament { Timid, Docile, Defensive, Predator }

    [Serializable] public sealed class SurvivalWeatherState
    {
        // Each interpolant and both endpoints are saved: restore never redraws a phase or PRNG.
        public uint randomState = 2463534242u;
        public WeatherKind kind = WeatherKind.Clear, previousKind = WeatherKind.Clear;
        public int phaseTick, phaseDurationTicks = 3600, transitionTicks = 400;
        public float intensity = .3f, windX = .1f, windY, temperature = 20f, humidity = .5f, rainfall;
        public float fromIntensity = .3f, fromWindX = .1f, fromWindY, fromTemperature = 20f, fromHumidity = .5f, fromRainfall;
        public float targetIntensity = .3f, targetWindX = .1f, targetWindY, targetTemperature = 20f, targetHumidity = .5f, targetRainfall;
    }

    [Serializable] public sealed class SurvivalPlayerState
    {
        public bool initialized;
        public int[] foods = new int[SurvivalCatalog.FoodCount], seeds = new int[SurvivalCatalog.CropCount];
        // One water unit is one serving; collection, drinking, boiling and irrigation consume units.
        public int waterRaw, waterClean;
        public float thirst = 100f, wetness, bodyTemperature = 37f, infection, dryness, nutrition = 100f;
        // Derived and saved for HUD; main applies movement/hunger helpers to the existing player loop.
        public float movementMultiplier = 1f, hungerMultiplier = 1f;
    }

    [Serializable] public sealed class FarmPlotState
    {
        public int id, x, y, crop = -1;
        public float moisture = 25f, growth, health = 100f;
    }

    [Serializable] public sealed class CreatureState
    {
        public int id;
        public CreatureKind kind;
        public float x, y, hp, wanderX, wanderY, attackCooldown;
        public int decisionTicks, fleeTicks, meatLeft;
        // hp==0 is a finite carcass; butcher transfers meatLeft atomically then removes it.
    }

    [Serializable] public sealed class SurvivalWorldState
    {
        // Legacy worlds and arbitrary industrial/test fixtures never opt in implicitly.
        public bool enabled, initialized;
        public uint creatureRandomState = 1597334677u;
        public SurvivalWeatherState weather = new SurvivalWeatherState();
        public List<FarmPlotState> plots = new List<FarmPlotState>();
        public List<CreatureState> creatures = new List<CreatureState>();
    }
}
