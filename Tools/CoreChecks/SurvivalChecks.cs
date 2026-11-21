using System;
using System.Linq;
using System.Text.Json;
using DeadCircuit;

static partial class Checks
{
    // Main calls SurvivalChecks() from Program.Main after wiring the v6 seams.
    // Tests arrange host-owned save fixtures, then act only through constructors, Apply and Step.
    // Intentionally not executed while the parallel integration is incomplete.
    static WorldSimulator SurvivalFixture(out PlayerState player, Action<WorldState> arrange = null)
    {
        var world = Flat(era: TechEra.Primitive);
        WorldGeneration.LegacyMetadata(world);
        world.version = 6; world.ground = new string('0', 4096); world.biomes = new string('0', 4096);
        world.survival = new SurvivalWorldState { enabled = true, initialized = true };
        arrange?.Invoke(world);
        PrepareLegacySaveFixture(world, 6);
        var sim = new WorldSimulator(world);
        player = sim.AddPlayer("survival-check", "Survival");
        player.x = 8; player.y = 10;
        player.needs = new SurvivalPlayerState { initialized = true };
        return sim;
    }

    static void HoldSurvivalWeather(WorldState s, float temperature = 20, float rain = 0, float humidity = .5f)
    {
        s.survival.weather = new SurvivalWeatherState
        {
            kind = rain > 0 ? WeatherKind.Thunderstorm : WeatherKind.Clear,
            previousKind = rain > 0 ? WeatherKind.Thunderstorm : WeatherKind.Clear,
            phaseDurationTicks = 100000, transitionTicks = 400,
            temperature = temperature, fromTemperature = temperature, targetTemperature = temperature,
            rainfall = rain, fromRainfall = rain, targetRainfall = rain,
            humidity = humidity, fromHumidity = humidity, targetHumidity = humidity
        };
    }

    static string SurvivalDump(WorldState world) => JsonSerializer.Serialize(world.survival, Json);

    // Fill within the actual backpack budget while leaving an exact amount for the action under test.
    static void FillSurvivalLoad(PlayerState player, int freeGrams)
    {
        long fill = InventoryWeight.CapacityGrams - InventoryWeight.CarriedGrams(player) - freeGrams;
        int seedGrams = InventoryWeight.Grams(CropKind.Herb);
        Require(fill >= 0 && fill % seedGrams == 0, "cannot fill this fixture to the intended weight");
        player.needs.waterClean += (int)(fill / InventoryWeight.WaterGrams);
        player.needs.seeds[(int)CropKind.Herb] += (int)(fill % InventoryWeight.WaterGrams) / seedGrams;
    }

    static void SurvivalChecks()
    {
        Run("survival consumes actual food once and retains industrial ration", () =>
        {
            var sim = SurvivalFixture(out var p);
            p.hunger = 30; p.needs.thirst = 20;
            p.needs.foods[(int)FoodKind.Apple] = 1;
            int ration = p.items[(int)ItemKind.Food];
            sim.Apply(Command(p, "eatfood", arg: (int)FoodKind.Apple));
            Near(p.hunger, 42, "apple calories"); Near(p.needs.thirst, 30, "apple hydration");
            Require(p.needs.foods[(int)FoodKind.Apple] == 0, "apple not consumed");
            Advance(sim, .5f); float hunger = p.hunger;
            sim.Apply(Command(p, "eatfood", arg: (int)FoodKind.Apple));
            Near(p.hunger, hunger, "empty food inventory duplicated apple");
            Require(p.items[(int)ItemKind.Food] == ration, "new food touched industrial enum inventory");
            sim.Apply(Command(p, "eat"));
            Require(p.items[(int)ItemKind.Food] == ration - 1, "legacy ration stopped working");
        });
        Run("survival collects boils and drinks finite water at a stone furnace", () =>
        {
            var sim = SurvivalFixture(out var p, s =>
            {
                var ground = s.ground.ToCharArray(); ground[8 + 12 * 64] = '2'; s.ground = new string(ground);
                Add(s, BuildKind.StoneFurnace, 10, 10);
            });
            p.items[(int)ItemKind.Fuel] = 1; p.needs.thirst = 10;
            FillSurvivalLoad(p, 500);
            int cleanBefore = p.needs.waterClean;
            sim.Apply(Command(p, "collectwater", 8, 12));
            Require(p.needs.waterRaw == 1, "collection missed reachable water"); Advance(sim, .5f);
            sim.Apply(Command(p, "boilwater", 10, 10));
            Require(p.needs.waterRaw == 0 && p.needs.waterClean == cleanBefore + 1 && p.items[(int)ItemKind.Fuel] == 0,
                "a full backpack blocked the weight-reducing water exchange");
            Advance(sim, .5f); sim.Apply(Command(p, "boilwater", 10, 10));
            Require(p.needs.waterClean == cleanBefore + 1, "empty boiling duplicated water");
            sim.Apply(Command(p, "drink", arg: 1));
            Require(p.needs.waterClean == cleanBefore && p.needs.thirst > 49 && p.needs.infection == 0, "clean drinking did not consume safely");
        });
        Run("survival shore collection needs room for one water unit", () =>
        {
            var sim = SurvivalFixture(out var p, s =>
            {
                var ground = s.ground.ToCharArray(); ground[8 + 12 * 64] = '2'; s.ground = new string(ground);
            });
            FillSurvivalLoad(p, 0);
            int cleanBefore = p.needs.waterClean;
            sim.Apply(Command(p, "collectwater", 8, 12));
            Require(p.needs.waterRaw == 0 && p.needs.waterClean == cleanBefore && p.interactCooldown == 0,
                "full backpack accepted water or spent the interaction");
            p.needs.thirst = 10;
            sim.Apply(Command(p, "drink", arg: 1)); Advance(sim, .5f);
            sim.Apply(Command(p, "collectwater", 8, 12));
            Require(p.needs.waterRaw == 1 && p.needs.waterClean == cleanBefore - 1, "shore did not supply water once room was freed");
        });
        Run("survival cooking rejects missing fuel range and occlusion atomically", () =>
        {
            foreach (string blocked in new[] { "fuel", "range", "wall" })
            {
                var sim = SurvivalFixture(out var p, s =>
                {
                    Add(s, BuildKind.StoneFurnace, 10, 10);
                    if (blocked == "wall") Add(s, BuildKind.Wall, 9, 10);
                });
                p.needs.foods[(int)FoodKind.RawMeat] = 2; p.items[(int)ItemKind.Fuel] = blocked == "fuel" ? 0 : 2;
                if (blocked == "range") p.x = 3;
                string needs = JsonSerializer.Serialize(p.needs, Json); int fuel = p.items[(int)ItemKind.Fuel];
                sim.Apply(Command(p, "cook", 10, 10, 0));
                Require(JsonSerializer.Serialize(p.needs, Json) == needs && p.items[(int)ItemKind.Fuel] == fuel, "rejected cooking spent resources: " + blocked);
            }
            var good = SurvivalFixture(out var actor, s => Add(s, BuildKind.StoneFurnace, 10, 10));
            actor.needs.foods[(int)FoodKind.RawMeat] = 1; actor.items[(int)ItemKind.Fuel] = 1;
            FillSurvivalLoad(actor, 0);
            good.Apply(Command(actor, "cook", 10, 10, 0));
            Require(actor.needs.foods[(int)FoodKind.RawMeat] == 0 && actor.needs.foods[(int)FoodKind.RoastMeat] == 1 && actor.items[(int)ItemKind.Fuel] == 0, "valid cooking not connected");
        });
        Run("survival cooking checks the net weight of a complete recipe", () =>
        {
            // Two wheat weigh 240 g and one flour weighs 250 g.
            foreach (int freeGrams in new[] { 0, 10 })
            {
                var sim = SurvivalFixture(out var p, s => Add(s, BuildKind.Workbench, 10, 10));
                p.needs.foods[(int)FoodKind.Wheat] = 2;
                FillSurvivalLoad(p, freeGrams);
                sim.Apply(Command(p, "cook", 10, 10, 6));
                bool fits = freeGrams == 10;
                Require(p.needs.foods[(int)FoodKind.Wheat] == (fits ? 0 : 2) &&
                    p.needs.foods[(int)FoodKind.Flour] == (fits ? 1 : 0), "manual milling did not check the 10 g net gain atomically");
            }
        });
        Run("survival refuses disconnected dead unknown and invalid commands", () =>
        {
            var sim = SurvivalFixture(out var p); p.needs.foods[(int)FoodKind.Apple] = 5; p.hunger = 10;
            p.connected = false; sim.Apply(Command(p, "eatfood", arg: 2)); p.connected = true;
            p.hp = 0; sim.Apply(Command(p, "eatfood", arg: 2)); p.hp = 100;
            sim.Apply(new PlayerCommand { playerId = "forged", action = "eatfood", arg = 2 });
            sim.Apply(Command(p, "eatfood", arg: -1)); sim.Apply(Command(p, "eatfood", arg: int.MaxValue));
            sim.Apply(Command(p, "collectwater", int.MaxValue, int.MinValue));
            Require(p.needs.foods[(int)FoodKind.Apple] == 5 && p.needs.waterRaw == 0 && p.hunger == 10, "unauthorised/invalid command changed inventory");
        });
        Run("survival dehydration and infection damage while dead and offline needs freeze", () =>
        {
            var sim = SurvivalFixture(out var p); p.needs.thirst = 0; Advance(sim, 2);
            Require(p.hp < 95 && p.needs.movementMultiplier < 1, "dehydration is cosmetic");
            p.needs.thirst = 80; p.needs.infection = 60; float hp = p.hp; Advance(sim, 2);
            Require(p.hp < hp && p.needs.hungerMultiplier > 1, "infection has no effect");
            p.connected = false; string needs = JsonSerializer.Serialize(p.needs, Json); Advance(sim, 2);
            Require(JsonSerializer.Serialize(p.needs, Json) == needs, "offline needs changed");
            p.connected = true; p.hp = 0; needs = JsonSerializer.Serialize(p.needs, Json); Advance(sim, 2);
            Require(JsonSerializer.Serialize(p.needs, Json) == needs, "dead needs changed");
        });
        Run("survival weather save mid transition retains all future weather and PRNG", () =>
        {
            var sim = SurvivalFixture(out var p);
            sim.State.survival.weather.phaseTick = sim.State.survival.weather.phaseDurationTicks;
            sim.Step(.05f); Advance(sim, 4.15f);
            var source = Saved(sim.State); var restored = new WorldSimulator(source);
            string snapshot = SurvivalDump(source);
            sim.SetConnected(p.id, false); restored.SetConnected(p.id, false);
            for (int tick = 0; tick < 11000; tick++)
            {
                sim.Step(.05f); restored.Step(.05f);
                if (tick % 97 == 0) Require(SurvivalDump(sim.State) == SurvivalDump(restored.State), "reload changed weather trajectory");
            }
            Require(SurvivalDump(sim.State) == SurvivalDump(restored.State), "weather diverged after multiple phase draws");
            Require(SurvivalDump(source) == snapshot, "restored survival aliases caller graph");
        });
        Run("survival next weather transition has no instantaneous temperature or rain jump", () =>
        {
            var sim = SurvivalFixture(out var p); var w = sim.State.survival.weather;
            HoldSurvivalWeather(sim.State, 43, 0, .15f); w = sim.State.survival.weather;
            w.phaseTick = w.phaseDurationTicks; sim.Step(.05f);
            Require(Math.Abs(w.temperature - 43) < .01f && Math.Abs(w.rainfall) < .001f, "weather changed discontinuously at phase boundary");
            Require(w.phaseTick == 1 && w.randomState != 2463534242u, "weather phase did not advance");
        });
        Run("survival storm replenishes water and moisture while site interiors shelter", () =>
        {
            var sim = SurvivalFixture(out var outdoors, s =>
            {
                HoldSurvivalWeather(s, 15, 1, 1);
                s.survival.plots.Add(new FarmPlotState { id = s.nextId++, x = 10, y = 10, moisture = 5 });
                var floor = s.ground.ToCharArray();
                for (int y = 20; y < 25; y++) for (int x = 20; x < 25; x++) floor[x + y * 64] = '4';
                s.ground = new string(floor);
                s.sites.Add(new SiteState { id = s.nextId++, x = 20, y = 20, width = 5, height = 5, doorX = 22, doorY = 20 });
                for (int y = 20; y < 25; y++) for (int x = 20; x < 25; x++)
                    if (x == 20 || x == 24 || y == 20 || y == 24) Add(s, BuildKind.Wall, x, y);
            });
            var indoors = sim.AddPlayer("shelter", "Shelter"); indoors.x = indoors.y = 22;
            outdoors.needs.thirst = indoors.needs.thirst = 10;
            Advance(sim, 10);
            Require(outdoors.needs.thirst > 14 && outdoors.needs.wetness > 90, "storm did not actually replenish/wet the player");
            Require(indoors.needs.wetness < 20 && indoors.needs.wetness < outdoors.needs.wetness * .2f, "physical enclosure did not shelter");
            Require(sim.State.survival.plots[0].moisture > 20, "rain did not irrigate farm soil");
            Require(outdoors.needs.waterClean == 0, "rain created clean bottled water");
        });
        Run("survival farming tills sows waters grows harvests and recycles seeds", () =>
        {
            var sim = SurvivalFixture(out var p, s => HoldSurvivalWeather(s));
            p.needs.seeds[(int)CropKind.Carrot] = 1; p.needs.waterRaw = 1;
            int wood = p.items[(int)ItemKind.Wood];
            sim.Apply(Command(p, "till", 10, 10)); Advance(sim, .5f);
            Require(sim.State.survival.plots.Count == 1 && p.items[(int)ItemKind.Wood] == wood - 1, "till did not create paid soil");
            sim.Apply(Command(p, "sow", 10, 10, (int)CropKind.Carrot)); Advance(sim, .5f);
            Require(p.needs.seeds[(int)CropKind.Carrot] == 0, "sowing did not spend seed");
            sim.Apply(Command(p, "watercrop", 10, 10, 0));
            Require(p.needs.waterRaw == 0, "irrigation created water");
            sim.SetConnected(p.id, false); Advance(sim, 125); sim.SetConnected(p.id, true);
            sim.Apply(Command(p, "harvest", 10, 10));
            Require(p.needs.foods[(int)FoodKind.Carrot] == 3 && p.needs.seeds[(int)CropKind.Carrot] == 2, "carrot harvest/seed loop failed");
            Require(sim.State.survival.plots[0].crop == -1, "harvest did not empty the crop");
            Advance(sim, .5f); sim.Apply(Command(p, "harvest", 10, 10));
            Require(p.needs.foods[(int)FoodKind.Carrot] == 3, "harvest replay duplicated produce");
            sim.Apply(Command(p, "sow", 10, 10, (int)CropKind.Carrot));
            Require(p.needs.seeds[(int)CropKind.Carrot] == 1, "harvested seed is not reusable");
        });
        Run("survival harvest preserves a ripe crop until food and seeds both fit", () =>
        {
            var sim = SurvivalFixture(out var p, s => s.survival.plots.Add(new FarmPlotState
                { id = s.nextId++, x = 10, y = 10, crop = (int)CropKind.Carrot, growth = 1, moisture = 60 }));
            p.needs.seeds[(int)CropKind.Carrot] = 999;
            FillSurvivalLoad(p, 540); // Three carrots fit. Their two seeds need another 20 g.
            var plot = sim.State.survival.plots.Single();
            sim.Apply(Command(p, "harvest", 10, 10));
            Require(plot.crop == (int)CropKind.Carrot && plot.growth == 1 && p.interactCooldown == 0 &&
                p.needs.foods[(int)FoodKind.Carrot] == 0 && p.needs.seeds[(int)CropKind.Carrot] == 999,
                "rejected harvest removed the crop or granted a partial batch");
            p.needs.thirst = 10;
            sim.Apply(Command(p, "drink", arg: 1)); Advance(sim, .5f);
            sim.Apply(Command(p, "harvest", 10, 10));
            Require(plot.crop == -1 && p.needs.foods[(int)FoodKind.Carrot] == 3 && p.needs.seeds[(int)CropKind.Carrot] == 1001,
                "harvest failed after freeing room or still capped seeds at 999");
            var loaded = new WorldSimulator(Saved(sim.State));
            Require(loaded.State.players.Single().needs.seeds[(int)CropKind.Carrot] == 1001,
                "valid seed stack over 999 did not survive save and restore");
        });
        Run("survival dry and freezing crops cannot grow for free", () =>
        {
            foreach (bool freezing in new[] { false, true })
            {
                var sim = SurvivalFixture(out var p, s =>
                {
                    HoldSurvivalWeather(s, freezing ? -20 : 20, 0, .1f);
                    s.survival.plots.Add(new FarmPlotState { id = s.nextId++, x = 10, y = 10, crop = (int)CropKind.Carrot, moisture = freezing ? 80 : 0 });
                });
                sim.SetConnected(p.id, false); Advance(sim, 20);
                var plot = sim.State.survival.plots[0];
                Require(plot.growth == 0 && plot.health < 90, "dry/frozen crop has no actual penalty");
            }
        });
        Run("survival wildlife flees hunts and attacks through public fixed ticks", () =>
        {
            var timid = SurvivalFixture(out var p, s => s.survival.creatures.Add(new CreatureState
                { id = s.nextId++, kind = CreatureKind.Hare, hp = 18, x = 10, y = 10 }));
            Advance(timid, 1); Require(timid.State.survival.creatures[0].x > 12, "hare did not flee visible player");
            var aggressive = SurvivalFixture(out var actor, s => s.survival.creatures.Add(new CreatureState
                { id = s.nextId++, kind = CreatureKind.Wolf, hp = 72, x = 8.7f, y = 10 }));
            Advance(aggressive, 6); Require(actor.hp < 100, "wolf never attacked after spawn protection");
            var hunt = SurvivalFixture(out var observer, s =>
            {
                s.survival.creatures.Add(new CreatureState { id = s.nextId++, kind = CreatureKind.Fox, hp = 32, x = 30, y = 30 });
                s.survival.creatures.Add(new CreatureState { id = s.nextId++, kind = CreatureKind.Hare, hp = 18, x = 30.2f, y = 30 });
            });
            hunt.SetConnected(observer.id, false); Advance(hunt, .05f);
            Require(hunt.State.survival.creatures.Single(c => c.kind == CreatureKind.Hare).hp == 12, "fox predation is cosmetic");
        });
        Run("survival melee kills wildlife and finite carcass pays meat once", () =>
        {
            var sim = SurvivalFixture(out var p, s => s.survival.creatures.Add(new CreatureState
                { id = s.nextId++, kind = CreatureKind.Chicken, hp = 14, x = 9, y = 10 }));
            int id = sim.State.survival.creatures[0].id;
            p.activeSlot = 0; p.weaponId = 0; p.toolId = 0;
            sim.Apply(new PlayerCommand { playerId = p.id, action = "attack", aimX = 1, aimY = 0 });
            var corpse = sim.State.survival.creatures.Single();
            Require(corpse.hp == 0, "main melee has not connected DamageCreature");
            FillSurvivalLoad(p, 250); // Room for one serving, but the corpse has two.
            sim.Apply(Command(p, "butcher", arg: id));
            Require(sim.State.survival.creatures.Contains(corpse) && corpse.meatLeft == 2 &&
                p.needs.foods[(int)FoodKind.RawPoultry] == 0 && p.interactCooldown == 0, "failed butchery consumed or partially looted a carcass");
            p.needs.thirst = 10;
            sim.Apply(Command(p, "drink", arg: 1)); Advance(sim, .5f);
            sim.Apply(Command(p, "butcher", arg: id));
            Require(sim.State.survival.creatures.Count == 0 && p.needs.foods[(int)FoodKind.RawPoultry] == 2, "dead chicken did not yield finite meat");
            Advance(sim, .5f); sim.Apply(Command(p, "butcher", arg: id));
            Require(p.needs.foods[(int)FoodKind.RawPoultry] == 2, "carcass replay duplicated meat");
        });
        Run("survival zombie kill grants XP when the scrap drop cannot fit", () =>
        {
            foreach (int freeGrams in new[] { 440, 450 })
            {
                var sim = SurvivalFixture(out var p, s => s.zombies.Add(new ZombieState
                    { id = s.nextId++, hp = 1, x = 9, y = 10 }));
                p.activeSlot = 0; p.weaponId = p.toolId = 0;
                FillSurvivalLoad(p, freeGrams);
                int scrapBefore = p.items[(int)ItemKind.Scrap], xpBefore = p.xp;
                sim.Apply(new PlayerCommand { playerId = p.id, action = "attack", aimX = 1, aimY = 0 });
                Require(sim.State.zombies.Count == 0 && sim.State.kills == 1 && p.xp == xpBefore + 12,
                    "carried weight affected the kill or experience reward");
                Require(p.items[(int)ItemKind.Scrap] == scrapBefore + (freeGrams == 450 ? 1 : 0),
                    "kill reward did not respect the remaining weight");
            }
        });
        Run("survival world save rejects corrupt graphs and duplicate shared entity ids", () =>
        {
            var sim = SurvivalFixture(out var p, s =>
            {
                s.survival.plots.Add(new FarmPlotState { id = s.nextId++, x = 10, y = 10 });
                s.survival.creatures.Add(new CreatureState { id = s.nextId++, kind = CreatureKind.Deer, hp = 65, x = 20, y = 20 });
            });
            foreach (Action<WorldState> corrupt in new Action<WorldState>[]
            {
                s => s.survival = null, s => s.survival.weather.randomState = 0,
                s => s.survival.weather.temperature = float.NaN, s => s.survival.weather.phaseTick = -1,
                s => s.survival.creatures[0].id = s.survival.plots[0].id,
                s => s.survival.creatures[0].id = s.players[0].gear[0].id,
                s => s.survival.creatures[0].hp = -1, s => s.survival.creatures[0].kind = (CreatureKind)99,
                s => s.survival.creatures.Add(null), s => s.survival.plots[0].crop = 99,
                s => s.players[0].needs.foods = new int[1], s => s.players[0].needs.seeds[0] = -1,
                s => s.players[0].needs.waterClean = WorldSimulator.InventoryLimit + 1, s => s.players[0].needs.thirst = float.PositiveInfinity,
                s => s.players[0].needs.bodyTemperature = 0, s => s.nextId = s.survival.creatures[0].id
            })
            {
                var save = Saved(sim.State); corrupt(save); Reject(save, "corrupt survival save accepted");
            }
            var source = Saved(sim.State); string before = Dump(source); var copy = new WorldSimulator(source);
            copy.State.players[0].needs.foods[0]++; copy.State.players[0].needs.seeds[0]++;
            copy.State.survival.plots[0].moisture = 99; copy.State.survival.creatures[0].hp = 1;
            copy.State.survival.weather.temperature = 40;
            Require(Dump(source) == before, "nested survival state was not deeply copied");
        });
        Run("survival legacy migration and disabled fixtures cannot invent supplies or animals", () =>
        {
            var world = Flat(); WorldGeneration.LegacyMetadata(world); world.version = 5;
            PrepareLegacySaveFixture(world, 5);
            var sim = new WorldSimulator(world); var p = sim.AddPlayer("legacy-survival", "Legacy");
            Advance(sim, 2);
            Require(!sim.State.survival.enabled && sim.State.survival.creatures.Count == 0 && p.needs.foods.All(v => v == 0) &&
                p.needs.seeds.All(v => v == 0) && p.needs.waterRaw == 0 && p.needs.waterClean == 0, "migration granted new survival resources");
            p.needs.thirst = 0; float hp = p.hp; Advance(sim, 2);
            Require(p.hp == hp && p.needs.thirst == 0, "disabled survival damaged an industrial fixture");
        });
        Run("survival new generation enables bounded wildlife while restore never repopulates", () =>
        {
            var sim = new WorldSimulator(73149);
            Require(sim.State.version == WorldSimulator.SaveVersion && sim.State.generatorVersion == WorldGeneration.CurrentGeneratorVersion && sim.State.survival.enabled, "new generation survival initialization missing");
            Require(sim.State.survival.creatures.Count > 0 && sim.State.survival.creatures.Count <= 32, "new world wildlife cap/initialization");
            foreach (var c in sim.State.survival.creatures)
            {
                int biome = sim.State.biomes[(int)c.x + (int)c.y * 64] - '0';
                Require((SurvivalCatalog.Creatures[(int)c.kind].biomeMask & (1 << biome)) != 0, "wildlife ignored its biome");
            }
            var save = Saved(sim.State); save.survival.creatures.Clear();
            var loaded = new WorldSimulator(save); Advance(loaded, 1);
            Require(loaded.State.survival.creatures.Count == 0, "restore silently reseeded dead wildlife");
        });
        Run("survival farm authority rejects terrain obstruction replay and 129th plot", () =>
        {
            foreach (string blocked in new[] { "road", "floor", "water", "wall", "far", "full" })
            {
                var sim = SurvivalFixture(out var p, s =>
                {
                    var ground = s.ground.ToCharArray();
                    if (blocked == "road") ground[10 + 10 * 64] = '3';
                    if (blocked == "floor") ground[10 + 10 * 64] = '4';
                    if (blocked == "water") ground[10 + 10 * 64] = '2';
                    s.ground = new string(ground);
                    if (blocked == "wall") Add(s, BuildKind.Wall, 9, 10);
                    if (blocked == "full") for (int i = 0; i < 128; i++)
                        s.survival.plots.Add(new FarmPlotState { id = s.nextId++, x = 20 + i % 16, y = 40 + i / 16 });
                });
                if (blocked == "far") p.x = 3;
                int count = sim.State.survival.plots.Count, wood = p.items[(int)ItemKind.Wood];
                sim.Apply(Command(p, "till", 10, 10));
                Require(sim.State.survival.plots.Count == count && p.items[(int)ItemKind.Wood] == wood, "illegal till spent or created resources: " + blocked);
            }
            var valid = SurvivalFixture(out var actor); valid.Apply(Command(actor, "till", 10, 10)); Advance(valid, .5f);
            int remainingWood = actor.items[(int)ItemKind.Wood]; valid.Apply(Command(actor, "till", 10, 10));
            Require(valid.State.survival.plots.Count == 1 && actor.items[(int)ItemKind.Wood] == remainingWood, "till replay duplicated soil/cost");
            actor.needs.seeds[0] = 1; valid.Apply(Command(actor, "sow", 10, 10, 0)); Advance(valid, .5f);
            valid.Apply(Command(actor, "sow", 10, 10, 0));
            Require(actor.needs.seeds[0] == 0 && valid.State.survival.plots[0].crop == 0, "sowing replay changed planted crop");
        });
        Run("survival hunger cold heat and dryness apply actual health and stamina effects", () =>
        {
            foreach (string status in new[] { "cold", "hot", "dry" })
            {
                var sim = SurvivalFixture(out var p); p.hunger = 50;
                if (status == "cold") p.needs.bodyTemperature = 33;
                if (status == "hot") p.needs.bodyTemperature = 41;
                if (status == "dry") p.needs.dryness = 95;
                Advance(sim, 2);
                Require(p.hp < 99 && p.stamina < 100 && p.needs.movementMultiplier < 1, "survival status is cosmetic: " + status);
            }
            var food = SurvivalFixture(out var actor); actor.needs.waterRaw = 1; actor.needs.thirst = 20;
            food.Apply(Command(actor, "drink", arg: 0));
            Require(actor.needs.waterRaw == 0 && actor.needs.infection == 25, "raw water has no contamination cost");
        });
        Run("survival respawn restores viable needs without duplicating inventories", () =>
        {
            var sim = SurvivalFixture(out var p); p.hp = 0; p.needs.thirst = 0; p.needs.infection = 80;
            p.needs.wetness = 90; p.needs.dryness = 95; p.needs.bodyTemperature = 30;
            p.needs.foods[0] = 7; p.needs.seeds[0] = 3; p.needs.waterRaw = 2; p.needs.waterClean = 1;
            Advance(sim, 5.1f); sim.Apply(Command(p, "respawn"));
            Require(p.hp > 0 && p.needs.thirst >= 50 && p.needs.bodyTemperature == 37 && p.needs.infection == 0, "main respawn has not reset survival needs");
            Require(p.needs.foods[0] == 7 && p.needs.seeds[0] == 3 && p.needs.waterRaw == 2 && p.needs.waterClean == 1, "rescue duplicated supplies");
        });
    }
}
