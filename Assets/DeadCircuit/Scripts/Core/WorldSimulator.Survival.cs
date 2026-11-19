using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public sealed partial class WorldSimulator
    {
        /* MAIN INTEGRATION (all methods except DamageCreature are private partial-class members):
         * WorldState.survival = new SurvivalWorldState(); PlayerState.needs = new SurvivalPlayerState();
         * New generation ONLY: InitializeSurvivalWorld(world, true) after terrain/sites/IDs.
         * CopyAndValidate: CopyNeeds(p, clone, source.version) per player, then
         * CopySurvival(source, target, entityIds, ref highestId) BEFORE final nextId validation.
         * Never initialize/reseed on restore or on v1-v5 migration.
         * AddPlayer: InitializeSurvivalPlayer(p, true) after registering the host-owned player.
         * Tick: UpdateSurvival() once after UpdatePlayer. Apply: if (ApplySurvival(p,cmd)) return;
         * Keep existing host connection/death/rate guards; run ApplySurvival before switch.
         * UpdatePlayer: speed *= SurvivalMovementMultiplier(p); hungerRate *= SurvivalHungerMultiplier(p).
         * Respawn: ResetSurvivalAfterRespawn(p) after successful spawn; retains every inventory.
         * Build footprint: reject SurvivalOccupiesCell(x,y); plots never block walking.
         * Combat: include live creatures in nearest-hit/occlusion selection, then call DamageCreature.
         * Program.Main: SurvivalChecks(). No game/GUI/build is needed for this source-only delivery.
         */

        private static void InitializeSurvivalWorld(WorldState world, bool newGeneratedWorld)
        {
            if (world == null || !newGeneratedWorld || world.generatorVersion < 1 || world.generatorVersion > WorldGeneration.CurrentGeneratorVersion) return;
            if (world.survival != null && world.survival.initialized) return;
            var survival = new SurvivalWorldState { enabled = true, initialized = true };
            survival.weather.randomState = SurvivalSeed(world.seed, 0xA341316Cu);
            survival.creatureRandomState = SurvivalSeed(world.seed, 0xC8013EA4u);
            world.survival = survival;
            SeedSurvivalCreatures(world);
        }

        private void InitializeSurvivalPlayer(PlayerState p, bool grantStartingSupplies = false)
        {
            if (p == null || State.survival == null || !State.survival.enabled) return;
            if (p.needs == null) p.needs = new SurvivalPlayerState();
            if (p.needs.initialized) return;
            p.needs.initialized = true;
            if (!grantStartingSupplies) return;
            p.needs.waterClean = Math.Min(SurvivalCatalog.StackLimit, p.needs.waterClean + 2);
            p.needs.foods[(int)FoodKind.Berries] = Math.Min(SurvivalCatalog.StackLimit, p.needs.foods[(int)FoodKind.Berries] + 3);
            for (int i = 0; i < SurvivalCatalog.CropCount; i++)
                p.needs.seeds[i] = Math.Min(SurvivalCatalog.StackLimit, p.needs.seeds[i] + 2);
        }

        private void ResetSurvivalAfterRespawn(PlayerState p)
        {
            if (State.survival == null || !State.survival.enabled || p == null || p.needs == null) return;
            // Rescue restores viability but gives no water, seeds or food. Existing hunger rule stays in main.
            var n = p.needs; n.thirst = Math.Max(50f, n.thirst); n.bodyTemperature = 37f;
            n.wetness = n.dryness = n.infection = 0f; n.nutrition = Math.Max(35f, n.nutrition);
            n.movementMultiplier = n.hungerMultiplier = 1f;
        }

        private float SurvivalMovementMultiplier(PlayerState p) => State.survival != null && State.survival.enabled && p.needs != null ? p.needs.movementMultiplier : 1f;
        private float SurvivalHungerMultiplier(PlayerState p) => State.survival != null && State.survival.enabled && p.needs != null ? p.needs.hungerMultiplier : 1f;

        private bool ApplySurvival(PlayerState p, PlayerCommand cmd)
        {
            if (cmd == null) return false;
            switch (cmd.action)
            {
                case "eatfood": case "drink": case "collectwater": case "boilwater": case "cook":
                case "till": case "sow": case "watercrop": case "harvest": case "butcher": break;
                default: return false;
            }
            // A known rejected action is handled. No industrial fallback can accidentally spend twice.
            if (State.survival == null || !State.survival.enabled || p == null ||
                FindPlayer(p.id) != p || cmd.playerId != p.id || !p.connected || p.hp <= 0 || p.needs == null ||
                p.interactCooldown > 0 || State.era < TechEra.Primitive) return true;
            RefreshSpatial();
            switch (cmd.action)
            {
                case "eatfood": ConsumeSurvivalFood(p, cmd.arg); break;
                case "drink": DrinkSurvivalWater(p, cmd.arg); break;
                case "collectwater":
                    if (cmd.arg == 0 && SurvivalReach(p, cmd.x, cmd.y) && State.ground[Cell(cmd.x, cmd.y)] == '2' && p.needs.waterRaw < SurvivalCatalog.StackLimit)
                    { p.needs.waterRaw++; SurvivalActionDone(p, "收集一份原水；饮用前请煮沸。"); }
                    break;
                case "boilwater": BoilSurvivalWater(p, cmd); break;
                case "cook": CookSurvivalFood(p, cmd); break;
                case "till": case "sow": case "watercrop": case "harvest": ApplySurvivalFarming(p, cmd); break;
                case "butcher": ButcherSurvivalCreature(p, cmd.arg); break;
            }
            return true;
        }

        private void ConsumeSurvivalFood(PlayerState p, int arg)
        {
            var food = SurvivalCatalog.Food(arg); var n = p.needs;
            if (food == null || !food.edible || n.foods[arg] <= 0) return;
            // Medicines/hot food remain useful while fed; reject a completely wasted serving.
            if (p.hunger >= 99 && n.thirst >= 99 && n.nutrition >= 99 &&
                (food.infection >= 0 || n.infection <= 0) && (food.healing <= 0 || p.hp >= PlayerMaxHealth(p))) return;
            n.foods[arg]--; p.hunger = Clamp(p.hunger + food.hunger, 0, 100);
            n.thirst = Clamp(n.thirst + food.hydration, 0, 100);
            n.nutrition = Clamp(n.nutrition + food.nutrition, 0, 100);
            n.bodyTemperature = Clamp(n.bodyTemperature + food.warmth, 25, 45);
            n.infection = Clamp(n.infection + food.infection, 0, 100);
            p.hp = Math.Min(PlayerMaxHealth(p), p.hp + food.healing);
            SurvivalActionDone(p, "已食用：" + food.name);
        }

        private void DrinkSurvivalWater(PlayerState p, int arg)
        {
            var n = p.needs;
            if (arg < 0 || arg > 1 || n.thirst >= 99f || (arg == 0 ? n.waterRaw : n.waterClean) <= 0) return;
            if (arg == 0) { n.waterRaw--; n.infection = Math.Min(100f, n.infection + 25f); } else n.waterClean--;
            n.thirst = Math.Min(100f, n.thirst + (arg == 0 ? 30f : 40f));
            SurvivalActionDone(p, arg == 0 ? "饮用原水，有感染风险。" : "饮用净水。");
        }

        private void BoilSurvivalWater(PlayerState p, PlayerCommand cmd)
        {
            var n = p.needs;
            if (cmd.arg != 0 || n.waterRaw < 1 || n.waterClean >= SurvivalCatalog.StackLimit ||
                p.items[(int)ItemKind.Fuel] < 1 || !SurvivalStation(p, cmd.x, cmd.y, BuildKind.StoneFurnace)) return;
            n.waterRaw--; n.waterClean++; p.items[(int)ItemKind.Fuel]--;
            SurvivalActionDone(p, "一份原水煮成一份净水，消耗一份燃料。");
        }

        private void CookSurvivalFood(PlayerState p, PlayerCommand cmd)
        {
            var recipe = SurvivalCatalog.Recipe(cmd.arg); var n = p.needs;
            if (recipe == null || !SurvivalStation(p, cmd.x, cmd.y, recipe.station) ||
                n.waterClean < recipe.cleanWater || p.items[(int)ItemKind.Fuel] < recipe.fuel) return;
            for (int i = 0; i < SurvivalCatalog.FoodCount; i++)
            {
                int after = n.foods[i] - recipe.foods[i] + ((int)recipe.output == i ? recipe.quantity : 0);
                if (n.foods[i] < recipe.foods[i] || after > SurvivalCatalog.StackLimit) return;
            }
            // All gates precede every write; synchronous host execution makes this atomic.
            for (int i = 0; i < SurvivalCatalog.FoodCount; i++) n.foods[i] -= recipe.foods[i];
            n.foods[(int)recipe.output] += recipe.quantity; n.waterClean -= recipe.cleanWater;
            p.items[(int)ItemKind.Fuel] -= recipe.fuel;
            SurvivalActionDone(p, "已处理：" + recipe.name);
        }

        private void SurvivalActionDone(PlayerState p, string text) { p.interactCooldown = .4f; Notice(p, text); }
        private bool SurvivalReach(PlayerState p, int x, int y) => Inside(x, y) &&
            DistSq(p.x, p.y, x, y) <= Reach * Reach && LineClear(p.x, p.y, x, y, x, y);
        private bool SurvivalStation(PlayerState p, int x, int y, BuildKind kind)
        {
            if (!SurvivalReach(p, x, y)) return false;
            var b = BuildingAt(x, y);
            return b != null && b.hp > 0 && b.kind == kind && TechCatalog.IsUnlocked(State, kind);
        }

        private static uint SurvivalSeed(int seed, uint salt)
        {
            uint value = unchecked((uint)seed ^ salt);
            value ^= value >> 16; value = unchecked(value * 2246822519u); value ^= value >> 13;
            return value == 0 ? salt : value;
        }
        private static uint SurvivalRandom(ref uint state)
        {
            // Xorshift32 uses serialized bits only; System.Random and wall time are never consulted.
            state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state;
        }
        private static float SurvivalUnit(ref uint state) => (SurvivalRandom(ref state) & 0xFFFFFFu) / 16777216f;
        private static float SurvivalBlend(float a, float b, float t) => a + (b - a) * t;

        private void UpdateSurvival()
        {
            if (State.survival == null || !State.survival.enabled) return;
            UpdateSurvivalWeather();
            foreach (var p in State.players) if (p.connected && p.hp > 0 && p.needs != null) UpdateSurvivalNeeds(p);
            UpdateSurvivalFarming();
            UpdateSurvivalCreatures();
        }

        private void UpdateSurvivalWeather()
        {
            var w = State.survival.weather;
            if (w.phaseTick >= w.phaseDurationTicks) BeginSurvivalWeatherPhase(w);
            w.phaseTick++;
            float t = Math.Min(1f, w.phaseTick / (float)w.transitionTicks); t = t * t * (3 - 2 * t);
            w.intensity = SurvivalBlend(w.fromIntensity, w.targetIntensity, t);
            w.windX = SurvivalBlend(w.fromWindX, w.targetWindX, t); w.windY = SurvivalBlend(w.fromWindY, w.targetWindY, t);
            w.temperature = SurvivalBlend(w.fromTemperature, w.targetTemperature, t);
            w.humidity = SurvivalBlend(w.fromHumidity, w.targetHumidity, t); w.rainfall = SurvivalBlend(w.fromRainfall, w.targetRainfall, t);
        }

        private static void BeginSurvivalWeatherPhase(SurvivalWeatherState w)
        {
            w.previousKind = w.kind;
            int pick = (int)(SurvivalRandom(ref w.randomState) % 100);
            // Neighbouring phases favour related conditions, while every kind remains reachable.
            if (pick < 45 && (w.kind == WeatherKind.Drizzle || w.kind == WeatherKind.Rain || w.kind == WeatherKind.Thunderstorm))
                w.kind = pick < 15 ? WeatherKind.Overcast : pick < 30 ? WeatherKind.Rain : WeatherKind.Drizzle;
            else if (pick < 45 && (w.kind == WeatherKind.ColdSnap || w.kind == WeatherKind.Snow))
                w.kind = pick < 25 ? WeatherKind.Snow : WeatherKind.Overcast;
            else w.kind = (WeatherKind)(SurvivalRandom(ref w.randomState) % SurvivalCatalog.WeatherCount);
            w.fromIntensity = w.intensity; w.fromWindX = w.windX; w.fromWindY = w.windY;
            w.fromTemperature = w.temperature; w.fromHumidity = w.humidity; w.fromRainfall = w.rainfall;
            w.phaseTick = 0; w.transitionTicks = 400 + (int)(SurvivalRandom(ref w.randomState) % 201);
            w.phaseDurationTicks = 2400 + (int)(SurvivalRandom(ref w.randomState) % 2401);
            w.targetIntensity = .35f + .65f * SurvivalUnit(ref w.randomState);
            float angle = SurvivalUnit(ref w.randomState) * 6.2831853f;
            float wind = w.kind == WeatherKind.DustStorm || w.kind == WeatherKind.Thunderstorm ? 1.8f : .7f;
            w.targetWindX = (float)Math.Cos(angle) * wind * w.targetIntensity;
            w.targetWindY = (float)Math.Sin(angle) * wind * w.targetIntensity;
            float temperature = 20f, humidity = .45f, rainfall = 0;
            switch (w.kind)
            {
                case WeatherKind.Overcast: temperature = 17; humidity = .65f; break;
                case WeatherKind.Drizzle: temperature = 16; humidity = .82f; rainfall = .25f; break;
                case WeatherKind.Rain: temperature = 13; humidity = .94f; rainfall = .7f; break;
                case WeatherKind.Thunderstorm: temperature = 12; humidity = 1; rainfall = 1; break;
                case WeatherKind.Fog: temperature = 14; humidity = .98f; break;
                case WeatherKind.Heatwave: temperature = 43; humidity = .15f; break;
                case WeatherKind.ColdSnap: temperature = -17; humidity = .4f; break;
                case WeatherKind.Snow: temperature = -5; humidity = .85f; rainfall = .08f; break;
                case WeatherKind.DustStorm: temperature = 32; humidity = .08f; break;
            }
            w.targetTemperature = temperature + (SurvivalUnit(ref w.randomState) * 4 - 2);
            w.targetHumidity = humidity; w.targetRainfall = rainfall * w.targetIntensity;
        }

        private float SurvivalExposure(float x, float y)
        {
            int cx = RoundCell(x), cy = RoundCell(y);
            // A real enclosure grants shelter; a floor tile or old location marker alone does not.
            return Inside(cx,cy) && Interiors!=null && Interiors.At(cx,cy)!=null ? .1f : 1f;
        }

        private float SurvivalAmbientTemperature(float x, float y)
        {
            int cell = Cell(RoundCell(x), RoundCell(y));
            int biome = State.biomes != null && cell >= 0 && cell < State.biomes.Length ? State.biomes[cell] - '0' : 0;
            float offset = biome == 3 ? 8 : biome == 5 ? -13 : biome == 2 ? -4 : biome == 4 ? -1 : 0;
            float cycle = (float)Math.Sin(State.tick * .05 * (Math.PI * 2) / State.dayLength) * 3;
            return State.survival.weather.temperature + offset + cycle;
        }

        private FarmPlotState SurvivalPlotAt(int x, int y)
        {
            foreach (var plot in State.survival.plots) if (plot.x == x && plot.y == y) return plot;
            return null;
        }

        private bool SurvivalOccupiesCell(int x, int y)
        {
            if (State.survival == null || !State.survival.enabled) return false;
            if (SurvivalPlotAt(x, y) != null) return true;
            foreach (var c in State.survival.creatures) if (Math.Abs(c.x - x) < .8f && Math.Abs(c.y - y) < .8f) return true;
            return false;
        }

        private void ApplySurvivalFarming(PlayerState p, PlayerCommand cmd)
        {
            if (!SurvivalReach(p, cmd.x, cmd.y)) return;
            var plot = SurvivalPlotAt(cmd.x, cmd.y); var n = p.needs;
            if (cmd.action == "till")
            {
                char terrain = State.ground[Cell(cmd.x, cmd.y)];
                if (cmd.arg != 0 || plot != null || State.survival.plots.Count >= SurvivalCatalog.MaxPlots ||
                    (terrain != '0' && terrain != '1') || NodeAt(cmd.x, cmd.y) != null || BuildingAt(cmd.x, cmd.y) != null ||
                    NearSpawn(cmd.x, cmd.y) || OccupiedByActor(cmd.x, cmd.y) || SurvivalOccupiesCell(cmd.x, cmd.y) ||
                    p.items[(int)ItemKind.Wood] < 1 || State.nextId >= int.MaxValue - 2) return;
                int id = AllocateId();
                p.items[(int)ItemKind.Wood]--;
                State.survival.plots.Add(new FarmPlotState { id = id, x = cmd.x, y = cmd.y });
                SurvivalActionDone(p, "已开垦一格农田，消耗一份木材制作简易农具。"); return;
            }
            if (plot == null || BuildingAt(plot.x, plot.y) != null || NodeAt(plot.x, plot.y) != null) return;
            if (cmd.action == "sow")
            {
                var crop = SurvivalCatalog.Crop(cmd.arg);
                if (crop == null || (plot.crop >= 0 && plot.health > 0) || n.seeds[cmd.arg] < 1) return;
                n.seeds[cmd.arg]--; plot.crop = cmd.arg; plot.growth = 0; plot.health = 100;
                SurvivalActionDone(p, "已播种：" + crop.name);
            }
            else if (cmd.action == "watercrop")
            {
                if (cmd.arg < 0 || cmd.arg > 1 || plot.moisture >= 99 || (cmd.arg == 0 ? n.waterRaw : n.waterClean) < 1) return;
                if (cmd.arg == 0) n.waterRaw--; else n.waterClean--;
                plot.moisture = Math.Min(100, plot.moisture + 40);
                SurvivalActionDone(p, "已浇水，消耗一份水。");
            }
            else if (cmd.action == "harvest")
            {
                var crop = SurvivalCatalog.Crop(plot.crop);
                if (cmd.arg != 0 || crop == null || plot.growth < 1 || plot.health <= 0 ||
                    n.foods[(int)crop.food] > SurvivalCatalog.StackLimit - crop.harvestCount ||
                    n.seeds[plot.crop] > SurvivalCatalog.StackLimit - crop.seedYield) return;
                n.foods[(int)crop.food] += crop.harvestCount; n.seeds[plot.crop] += crop.seedYield;
                plot.crop = -1; plot.growth = 0; plot.health = 100;
                SurvivalActionDone(p, "收获食材与可再播种的种子。");
            }
        }

        private void UpdateSurvivalFarming()
        {
            var w = State.survival.weather;
            foreach (var plot in State.survival.plots)
            {
                var crop = SurvivalCatalog.Crop(plot.crop);
                float temperature = SurvivalAmbientTemperature(plot.x, plot.y);
                float rain = w.rainfall * SurvivalExposure(plot.x, plot.y);
                float evaporation = .012f + Math.Max(0, temperature) * .0015f * (1 - w.humidity) +
                    (Math.Abs(w.windX) + Math.Abs(w.windY)) * .008f;
                float use = crop != null && plot.health > 0 && plot.growth < 1 ? crop.waterPerSecond : 0;
                plot.moisture = Clamp(plot.moisture + (rain * 1.8f - evaporation - use) * FixedStep, 0, 100);
                if (crop == null || plot.health <= 0) continue;
                bool suitable = temperature >= crop.minTemperature && temperature <= crop.maxTemperature;
                if (plot.moisture <= 2 || temperature < crop.minTemperature - 8 || temperature > crop.maxTemperature + 10)
                    plot.health = Math.Max(0, plot.health - .7f * FixedStep);
                if (!suitable || plot.moisture < 12 || plot.health <= 0 || plot.growth >= 1) continue;
                float fertility = plot.moisture > 85 ? .8f : 1f;
                plot.growth = Math.Min(1, plot.growth + fertility * FixedStep / crop.growSeconds);
            }
        }

        private static bool SurvivalWorldCellFree(WorldState world, int x, int y, bool allowBelts = false)
        {
            if (x < 0 || x >= world.width || y < 0 || y >= world.height || world.ground[x + y * world.width] == '2') return false;
            foreach (var node in world.nodes) if (node.x == x && node.y == y) return false;
            foreach (var building in world.buildings) if ((!allowBelts || building.kind != BuildKind.Belt) && DemoCatalog.Contains(building, x, y)) return false;
            return true;
        }

        private static bool SurvivalCreaturePositionFree(WorldState world, float x, float y)
        {
            for (int cy = RoundCell(y - .24f); cy <= RoundCell(y + .24f); cy++)
                for (int cx = RoundCell(x - .24f); cx <= RoundCell(x + .24f); cx++)
                {
                    if (SurvivalWorldCellFree(world, cx, cy, true)) continue;
                    float dx = Math.Max(0, Math.Abs(x - cx) - .5f), dy = Math.Max(0, Math.Abs(y - cy) - .5f);
                    if (dx * dx + dy * dy < .24f * .24f - .00001f) return false;
                }
            return true;
        }

        private static void SeedSurvivalCreatures(WorldState world)
        {
            var s = world.survival;
            for (int slot = 0; slot < SurvivalCatalog.MaxCreatures; slot++)
            {
                var definition = SurvivalCatalog.Creatures[slot % SurvivalCatalog.CreatureCount];
                for (int attempt = 0; attempt < 512; attempt++)
                {
                    int x = 2 + (int)(SurvivalRandom(ref s.creatureRandomState) % 60);
                    int y = 2 + (int)(SurvivalRandom(ref s.creatureRandomState) % 60);
                    int cell = x + y * world.width;
                    int biome = world.biomes != null && world.biomes.Length == world.width * world.height ? world.biomes[cell] - '0' : 0;
                    char ground = world.ground[cell];
                    if (biome < 0 || biome > 5 || (definition.biomeMask & (1 << biome)) == 0 || (ground != '0' && ground != '1') ||
                        !SurvivalWorldCellFree(world, x, y) || DistSq(x, y, world.width / 2, world.height / 2 + 3) < 144) continue;
                    bool occupied = false;
                    foreach (var c in s.creatures) if (DistSq(c.x, c.y, x, y) < 2.25f) { occupied = true; break; }
                    foreach (var p in world.players) if (p.hp > 0 && DistSq(p.x, p.y, x, y) < 144) { occupied = true; break; }
                    if (occupied) continue;
                    if (world.nextId >= int.MaxValue - 2) throw new InvalidOperationException("Survival entity IDs exhausted.");
                    s.creatures.Add(new CreatureState { id = world.nextId++, kind = definition.kind, x = x, y = y, hp = definition.health });
                    break;
                }
            }
        }

        /// <summary>Host hit-resolution hook, not a client command. Caller resolves ray/cone/range first.
        /// Returns false for absent/dead targets, invalid damage or a foreign/dead/disconnected owner.
        /// A null owner is reserved for host environmental/predator damage. Carcasses never pay twice.</summary>
        public bool DamageCreature(int id, float damage, PlayerState attacker = null)
        {
            if (State.survival == null || !State.survival.enabled || !Finite(damage) || damage <= 0 ||
                (attacker != null && (FindPlayer(attacker.id) != attacker || !attacker.connected || attacker.hp <= 0))) return false;
            foreach (var creature in State.survival.creatures)
            {
                if (creature.id != id || creature.hp <= 0) continue;
                creature.hp = Math.Max(0, creature.hp - damage); creature.fleeTicks = 240;
                if (creature.hp <= 0)
                {
                    creature.meatLeft = SurvivalCatalog.Creatures[(int)creature.kind].meat;
                    creature.wanderX = creature.wanderY = 0; creature.attackCooldown = 0;
                    creature.decisionTicks = creature.fleeTicks = 0;
                }
                return true;
            }
            return false;
        }

        private void ButcherSurvivalCreature(PlayerState p, int id)
        {
            var creatures = State.survival.creatures;
            for (int i = 0; i < creatures.Count; i++)
            {
                var c = creatures[i];
                if (c.id != id || c.hp > 0 || c.meatLeft <= 0 || DistSq(p.x, p.y, c.x, c.y) > Reach * Reach ||
                    !LineClear(p.x, p.y, c.x, c.y, RoundCell(c.x), RoundCell(c.y))) continue;
                var definition = SurvivalCatalog.Creatures[(int)c.kind];
                if (p.needs.foods[(int)definition.food] > SurvivalCatalog.StackLimit - c.meatLeft) return;
                p.needs.foods[(int)definition.food] += c.meatLeft; c.meatLeft = 0; creatures.RemoveAt(i);
                SurvivalActionDone(p, "已采集：" + definition.name + "的肉。"); return;
            }
        }

        private static bool SurvivalSmallPrey(CreatureKind kind) => kind == CreatureKind.Hare || kind == CreatureKind.Chicken || kind == CreatureKind.Duck || kind == CreatureKind.Pheasant;

        private void UpdateSurvivalCreatures()
        {
            var s = State.survival;
            foreach (var c in s.creatures)
            {
                if (c.hp <= 0) continue;
                c.attackCooldown = Math.Max(0, c.attackCooldown - FixedStep);
                if (c.fleeTicks > 0) c.fleeTicks--;
                if (c.decisionTicks > 0) c.decisionTicks--;
                var definition = SurvivalCatalog.Creatures[(int)c.kind];
                PlayerState player = null; float playerDistance = float.MaxValue;
                foreach (var p in State.players)
                {
                    if (!p.connected || p.hp <= 0) continue;
                    float distance = DistSq(c.x, c.y, p.x, p.y);
                    if (distance < playerDistance && distance < 144 && LineClear(c.x, c.y, p.x, p.y, RoundCell(p.x), RoundCell(p.y)))
                    { player = p; playerDistance = distance; }
                }
                float dx = c.wanderX, dy = c.wanderY, speed = definition.speed * .3f;
                bool chasingPlayer = player != null && definition.damage > 0 && !definition.huntsSmallPrey &&
                    (playerDistance < definition.aggroRange * definition.aggroRange || (c.fleeTicks > 0 && definition.temperament == CreatureTemperament.Defensive));
                CreatureState prey = null, threat = null;
                float preyDistance = 64, threatDistance = definition.fearRange * definition.fearRange;
                foreach (var other in s.creatures)
                {
                    if (other == c || other.hp <= 0) continue;
                    var otherDefinition = SurvivalCatalog.Creatures[(int)other.kind];
                    float distance = DistSq(c.x, c.y, other.x, other.y);
                    if (distance > 64 || !LineClear(c.x, c.y, other.x, other.y, RoundCell(other.x), RoundCell(other.y))) continue;
                    if (definition.temperament == CreatureTemperament.Predator && otherDefinition.temperament != CreatureTemperament.Predator &&
                        (!definition.huntsSmallPrey || SurvivalSmallPrey(other.kind)) && distance < preyDistance)
                    { prey = other; preyDistance = distance; }
                    if (definition.temperament != CreatureTemperament.Predator && otherDefinition.temperament == CreatureTemperament.Predator && distance < threatDistance)
                    { threat = other; threatDistance = distance; }
                }
                bool fleeing = !chasingPlayer && (threat != null || (player != null &&
                    (playerDistance < definition.fearRange * definition.fearRange || c.fleeTicks > 0)));
                if (chasingPlayer && (prey == null || playerDistance <= preyDistance))
                {
                    dx = player.x - c.x; dy = player.y - c.y; speed = definition.speed;
                    if (playerDistance <= 1.05f * 1.05f && c.attackCooldown <= 0)
                    {
                        DamagePlayer(player, definition.damage, true); c.attackCooldown = 1.2f;
                        AddTrace(c.x, c.y, player.x, player.y, true);
                    }
                }
                else if (fleeing)
                {
                    bool fleePlayer = player != null && (threat == null || playerDistance < threatDistance);
                    dx = c.x - (fleePlayer ? player.x : threat.x); dy = c.y - (fleePlayer ? player.y : threat.y); speed = definition.speed;
                }
                else if (prey != null)
                {
                    dx = prey.x - c.x; dy = prey.y - c.y; speed = definition.speed;
                    if (preyDistance <= .95f * .95f && c.attackCooldown <= 0)
                    { DamageCreature(prey.id, definition.damage); c.attackCooldown = 1.2f; }
                }
                else if (c.decisionTicks <= 0)
                {
                    float angle = SurvivalUnit(ref s.creatureRandomState) * 6.2831853f;
                    c.wanderX = (float)Math.Cos(angle); c.wanderY = (float)Math.Sin(angle);
                    c.decisionTicks = 30 + (int)(SurvivalRandom(ref s.creatureRandomState) % 91);
                    dx = c.wanderX; dy = c.wanderY;
                }
                if (fleeing || chasingPlayer || prey != null) Normalize(ref dx, ref dy, true);
                float oldX = c.x, oldY = c.y;
                MoveCircle(ref c.x, ref c.y, dx * speed * FixedStep, dy * speed * FixedStep, .24f);
                if (DistSq(oldX, oldY, c.x, c.y) < .000001f)
                {
                    // A short perpendicular sidestep lets an escaping/chasing animal clear corners.
                    MoveCircle(ref c.x, ref c.y, -dy * speed * FixedStep * .5f, dx * speed * FixedStep * .5f, .24f);
                    c.decisionTicks = 0;
                }
            }
        }

        private void UpdateSurvivalNeeds(PlayerState p)
        {
            var n = p.needs; var w = State.survival.weather;
            float exposure = SurvivalExposure(p.x, p.y), temperature = SurvivalAmbientTemperature(p.x, p.y);
            float wind = (float)Math.Sqrt(w.windX * w.windX + w.windY * w.windY);
            float heat = Math.Max(0, temperature - 28) / 12;
            float activity = p.moving ? 1.6f : 1f;
            float drain = .12f * activity * (1 + heat + n.infection * .006f);
            n.thirst = Math.Max(0, n.thirst - drain * FixedStep);
            // Rain replenishes thirst slowly (mouth collection) but cannot create clean bottled water.
            n.thirst = Math.Min(100, n.thirst + w.rainfall * exposure * .6f * FixedStep);
            n.wetness = Clamp(n.wetness + (w.rainfall * exposure * 10 - (1 - w.humidity) * (1 + heat + wind)) * FixedStep, 0, 100);
            n.dryness = Clamp(n.dryness + ((1 - w.humidity) * Math.Max(0, temperature - 24) * .035f * exposure - w.rainfall * exposure * 4 - (n.thirst > 40 ? .04f : 0)) * FixedStep, 0, 100);
            float shelterTemperature = SurvivalBlend(temperature, 20, 1 - exposure);
            float equilibrium = 37 + (shelterTemperature - 20) * .13f - n.wetness * .018f * (1 + wind * exposure);
            n.bodyTemperature = Clamp(n.bodyTemperature + (equilibrium - n.bodyTemperature) * .018f * FixedStep, 25, 45);
            n.nutrition = Math.Max(0, n.nutrition - .012f * activity * FixedStep);
            // Severe infection persists without treatment; minor contamination clears slowly.
            n.infection = Clamp(n.infection + (n.infection >= 50 ? .012f : -.008f) * FixedStep, 0, 100);
            bool cold = n.bodyTemperature < 35, hot = n.bodyTemperature > 39;
            n.movementMultiplier = (n.thirst < 15 ? .7f : 1f) * (cold || hot ? .75f : 1f) * (n.infection > 35 ? .8f : 1f) * (n.dryness > 65 ? .9f : 1f);
            n.hungerMultiplier = (cold ? 1.6f : 1f) * (n.infection > 35 ? 1.3f : 1f);
            if (n.thirst < 15 || cold || hot || n.infection > 35 || n.dryness > 65)
                p.stamina = Math.Max(0, p.stamina - 18f * FixedStep);
            float damage = (n.thirst <= 0 ? 3.5f : 0) + (cold ? 1.8f : 0) + (hot ? 2.2f : 0) +
                (n.infection > 35 ? 1f + n.infection * .02f : 0) + (n.dryness > 85 ? .8f : 0) + (n.nutrition <= 0 ? .65f : 0);
            if (damage > 0) DamagePlayer(p, damage * FixedStep, false);
        }

        // Called from main's existing validator after the normal world/player clones exist.
        // IDs are registered into the SAME set as nodes, buildings, cargo, gear and gen1 sites.
        private static void CopySurvival(WorldState source, WorldState target, HashSet<int> ids, ref int highest)
        {
            target.survival = new SurvivalWorldState();
            if (source.version < 6) return; // migration is inert: no animals and no supplies
            var s = source.survival;
            SaveCheck(s != null && s.weather != null && s.plots != null && s.creatures != null, "survival graph");
            SaveCheck(!s.enabled || s.initialized, "survival initialization");
            SaveCheck(s.creatureRandomState != 0, "survival wildlife PRNG");
            SaveCheck(s.plots.Count <= SurvivalCatalog.MaxPlots && s.creatures.Count <= SurvivalCatalog.MaxCreatures, "survival entity limits");
            var copy = new SurvivalWorldState { enabled = s.enabled, initialized = s.initialized,
                creatureRandomState = s.creatureRandomState, weather = CopySurvivalWeather(s.weather) };
            var cells = new HashSet<int>();
            foreach (var plot in s.plots)
            {
                SaveCheck(plot != null, "null farm plot");
                CheckEntityId(plot.id, ids, ref highest); CheckCell(plot.x, plot.y);
                int cell = plot.x + plot.y * source.width; char ground = source.ground[cell];
                SaveCheck(cells.Add(cell) && (ground == '0' || ground == '1') && SurvivalWorldCellFree(source, plot.x, plot.y), "farm terrain/occupancy");
                SaveCheck(plot.crop >= -1 && plot.crop < SurvivalCatalog.CropCount, "farm crop kind");
                SaveRange(plot.moisture, 0, 100, "farm moisture"); SaveRange(plot.growth, 0, 1, "farm growth"); SaveRange(plot.health, 0, 100, "farm health");
                SaveCheck(plot.crop >= 0 || (plot.growth == 0 && plot.health == 100), "empty farm crop state");
                copy.plots.Add(new FarmPlotState { id = plot.id, x = plot.x, y = plot.y, crop = plot.crop,
                    moisture = plot.moisture, growth = plot.growth, health = plot.health });
            }
            foreach (var c in s.creatures)
            {
                SaveCheck(c != null, "null wildlife"); CheckEntityId(c.id, ids, ref highest);
                var definition = SurvivalCatalog.Creature((int)c.kind); SaveCheck(definition != null, "wildlife species");
                CheckPosition(c.x, c.y); SaveCheck(SurvivalCreaturePositionFree(source, c.x, c.y), "wildlife in obstruction");
                SaveRange(c.hp, 0, definition.health, "wildlife hp");
                SaveRange(c.wanderX, -1, 1, "wildlife heading x"); SaveRange(c.wanderY, -1, 1, "wildlife heading y");
                SaveCheck(c.wanderX * c.wanderX + c.wanderY * c.wanderY <= 1.001f, "wildlife heading length");
                SaveRange(c.attackCooldown, 0, 1.201f, "wildlife attack cooldown");
                SaveCheck(c.decisionTicks >= 0 && c.decisionTicks <= 120 && c.fleeTicks >= 0 && c.fleeTicks <= 240, "wildlife decision clock");
                SaveCheck(c.hp > 0 ? c.meatLeft == 0 : c.meatLeft > 0 && c.meatLeft <= definition.meat, "wildlife carcass inventory");
                SaveCheck(c.hp > 0 || (c.wanderX == 0 && c.wanderY == 0 && c.attackCooldown == 0 && c.decisionTicks == 0 && c.fleeTicks == 0), "moving carcass");
                copy.creatures.Add(new CreatureState { id = c.id, kind = c.kind, x = c.x, y = c.y, hp = c.hp,
                    wanderX = c.wanderX, wanderY = c.wanderY, attackCooldown = c.attackCooldown,
                    decisionTicks = c.decisionTicks, fleeTicks = c.fleeTicks, meatLeft = c.meatLeft });
            }
            SaveCheck(source.nextId > highest, "nextId does not cover survival entities");
            foreach (var p in source.players) SaveCheck(!s.enabled || (p.needs != null && p.needs.initialized), "active survival player initialization");
            target.survival = copy;
        }

        private static SurvivalWeatherState CopySurvivalWeather(SurvivalWeatherState w)
        {
            SaveCheck(w.randomState != 0 && (int)w.kind >= 0 && (int)w.kind < SurvivalCatalog.WeatherCount &&
                (int)w.previousKind >= 0 && (int)w.previousKind < SurvivalCatalog.WeatherCount, "weather kind/PRNG");
            SaveCheck(w.phaseDurationTicks >= 1 && w.phaseDurationTicks <= 100000 && w.transitionTicks >= 1 &&
                w.transitionTicks <= w.phaseDurationTicks && w.phaseTick >= 0 && w.phaseTick <= w.phaseDurationTicks, "weather phase clock");
            CheckSurvivalWeatherSample(w.intensity, w.windX, w.windY, w.temperature, w.humidity, w.rainfall);
            CheckSurvivalWeatherSample(w.fromIntensity, w.fromWindX, w.fromWindY, w.fromTemperature, w.fromHumidity, w.fromRainfall);
            CheckSurvivalWeatherSample(w.targetIntensity, w.targetWindX, w.targetWindY, w.targetTemperature, w.targetHumidity, w.targetRainfall);
            return new SurvivalWeatherState { randomState = w.randomState, kind = w.kind, previousKind = w.previousKind,
                phaseTick = w.phaseTick, phaseDurationTicks = w.phaseDurationTicks, transitionTicks = w.transitionTicks,
                intensity = w.intensity, windX = w.windX, windY = w.windY, temperature = w.temperature, humidity = w.humidity, rainfall = w.rainfall,
                fromIntensity = w.fromIntensity, fromWindX = w.fromWindX, fromWindY = w.fromWindY, fromTemperature = w.fromTemperature, fromHumidity = w.fromHumidity, fromRainfall = w.fromRainfall,
                targetIntensity = w.targetIntensity, targetWindX = w.targetWindX, targetWindY = w.targetWindY, targetTemperature = w.targetTemperature, targetHumidity = w.targetHumidity, targetRainfall = w.targetRainfall };
        }

        private static void CheckSurvivalWeatherSample(float intensity, float windX, float windY, float temperature, float humidity, float rainfall)
        {
            SaveRange(intensity, 0, 1, "weather intensity"); SaveRange(windX, -2, 2, "weather wind x"); SaveRange(windY, -2, 2, "weather wind y");
            SaveRange(temperature, -80, 80, "weather temperature"); SaveRange(humidity, 0, 1, "weather humidity"); SaveRange(rainfall, 0, 1, "weather rainfall");
        }

        private static void CopyNeeds(PlayerState source, PlayerState target, int sourceVersion)
        {
            target.needs = new SurvivalPlayerState();
            if (sourceVersion < 6) return;
            var n = source.needs; SaveCheck(n != null, "player needs");
            SaveCheck(n.foods != null && n.foods.Length == SurvivalCatalog.FoodCount &&
                n.seeds != null && n.seeds.Length == SurvivalCatalog.CropCount, "survival inventory length");
            foreach (int value in n.foods) SaveCheck(value >= 0 && value <= SurvivalCatalog.StackLimit, "food inventory");
            foreach (int value in n.seeds) SaveCheck(value >= 0 && value <= SurvivalCatalog.StackLimit, "seed inventory");
            SaveCheck(n.waterRaw >= 0 && n.waterRaw <= SurvivalCatalog.StackLimit && n.waterClean >= 0 && n.waterClean <= SurvivalCatalog.StackLimit, "water inventory");
            SaveRange(n.thirst, 0, 100, "thirst"); SaveRange(n.wetness, 0, 100, "wetness"); SaveRange(n.bodyTemperature, 25, 45, "body temperature");
            SaveRange(n.infection, 0, 100, "infection"); SaveRange(n.dryness, 0, 100, "dryness"); SaveRange(n.nutrition, 0, 100, "nutrition");
            SaveRange(n.movementMultiplier, .1f, 1, "survival movement multiplier"); SaveRange(n.hungerMultiplier, 1, 3, "survival hunger multiplier");
            target.needs = new SurvivalPlayerState { initialized = n.initialized, foods = (int[])n.foods.Clone(), seeds = (int[])n.seeds.Clone(),
                waterRaw = n.waterRaw, waterClean = n.waterClean, thirst = n.thirst, wetness = n.wetness, bodyTemperature = n.bodyTemperature,
                infection = n.infection, dryness = n.dryness, nutrition = n.nutrition, movementMultiplier = n.movementMultiplier, hungerMultiplier = n.hungerMultiplier };
        }
    }
}
