using System;
using System.Text.Json;
using DeadCircuit;

static partial class Checks
{
    static WorldSimulator SeasonFixture(float day, out PlayerState player, Action<WorldState> arrange = null)
    {
        return SurvivalFixture(out player, world =>
        {
            world.elapsed = day * world.dayLength;
            world.tick = (long)Math.Round(world.elapsed / WorldSimulator.FixedStep);
            world.nextWaveAt = world.elapsed + 180;
            HoldSurvivalWeather(world);
            arrange?.Invoke(world);
        });
    }

    // Main calls SeasonChecks(). All actions use the public calendar, Step and save/restore seams.
    static void SeasonChecks()
    {
        Run("season calendar follows host elapsed and day length across years", () =>
        {
            var world = new WorldState { dayLength = 120, tick = 987654321, seed = 71 };
            var days = new[] { 0f, 6f, 7f, 14f, 21f, 28f, 35f, 2800f };
            var seasons = new[] { SeasonKind.Spring, SeasonKind.Spring, SeasonKind.Summer, SeasonKind.Autumn,
                SeasonKind.Winter, SeasonKind.Spring, SeasonKind.Summer, SeasonKind.Spring };
            var years = new[] { 1, 1, 1, 1, 1, 2, 2, 101 };
            for (int i = 0; i < days.Length; i++)
            {
                world.elapsed = days[i] * 120;
                string before = Dump(world);
                var sample = SeasonCalendar.Sample(world);
                Require(sample.season == seasons[i] && sample.year == years[i], "wrong season/year on day " + days[i]);
                Require(sample.daysPerSeason == 7 && sample.dayOfSeason == (i == 1 ? 7 : 1), "season days are not one based");
                Require(sample.progress >= 0 && sample.progress <= 1, "season progress escaped its range");
                Require(Dump(world) == before, "calendar sampling mutated host state");
            }
            world.elapsed = 210;
            var fractional = SeasonCalendar.Sample(world);
            Require(fractional.dayOfSeason == 2, "fractional day rounded into another day");
            Near(fractional.progress, .25f, "fractional season progress");
            Require(SeasonCalendar.Name(SeasonKind.Spring) == "春季" && SeasonCalendar.Name(SeasonKind.Summer) == "夏季" &&
                SeasonCalendar.Name(SeasonKind.Autumn) == "秋季" && SeasonCalendar.Name(SeasonKind.Winter) == "冬季" &&
                fractional.Name == "春季", "calendar Chinese names");
        });
        Run("season temperature warms summer freezes winter and is continuous at every boundary", () =>
        {
            var world = new WorldState { dayLength = 240, elapsed = 2520 };
            Near(SeasonCalendar.Sample(world).temperatureOffset, 12, "midsummer temperature offset");
            world.elapsed = 5880;
            Near(SeasonCalendar.Sample(world).temperatureOffset, -26, "midwinter temperature offset");
            for (int halfSeason = 1; halfSeason <= 16; halfSeason++)
            {
                float boundary = halfSeason * 840f;
                world.elapsed = boundary - .01f; float before = SeasonCalendar.Sample(world).temperatureOffset;
                world.elapsed = boundary + .01f; float after = SeasonCalendar.Sample(world).temperatureOffset;
                Require(Math.Abs(before - after) < .01f, "season temperature jumped across a season/year boundary");
            }
            world.elapsed = 0;
            Near(SeasonCalendar.Sample(world).temperatureOffset, 0, "new spring preserves starting climate");
        });
        Run("season weather weights favour summer storms and winter cold without forbidden draws", () =>
        {
            var counts = new int[4, SurvivalCatalog.WeatherCount];
            for (int season = 0; season < 4; season++)
            {
                var sim = SeasonFixture(3.5f + season * 7, out var player);
                sim.SetConnected(player.id, false);
                for (int phase = 0; phase < 256; phase++)
                {
                    var weather = sim.State.survival.weather; weather.phaseTick = weather.phaseDurationTicks;
                    sim.Step(WorldSimulator.FixedStep);
                    Require(season != 1 || weather.kind != WeatherKind.Snow, "summer drew snow");
                    Require(season != 3 || weather.kind != WeatherKind.Heatwave, "winter drew a heatwave");
                    counts[season, (int)weather.kind]++;
                }
            }
            Require(counts[1, (int)WeatherKind.Heatwave] > counts[0, (int)WeatherKind.Heatwave] &&
                counts[1, (int)WeatherKind.Thunderstorm] > counts[0, (int)WeatherKind.Thunderstorm], "summer did not favour heat/storms");
            Require(counts[3, (int)WeatherKind.Snow] > counts[2, (int)WeatherKind.Snow] &&
                counts[3, (int)WeatherKind.ColdSnap] > counts[2, (int)WeatherKind.ColdSnap], "winter did not favour snow/cold snaps");
        });
        Run("season climate changes actual body temperature crop growth frost health and water use", () =>
        {
            Action<WorldState> crop = world =>
            {
                HoldSurvivalWeather(world, 20, 0, .2f);
                world.survival.plots.Add(new FarmPlotState { id = world.nextId++, x = 10, y = 10,
                    crop = (int)CropKind.Corn, moisture = 80 });
            };
            var spring = SeasonFixture(3.5f, out var mild, crop);
            var summer = SeasonFixture(10.5f, out var hot, crop);
            var winter = SeasonFixture(24.5f, out var cold, crop);
            Advance(spring, 20); Advance(summer, 20); Advance(winter, 20);
            Require(hot.needs.bodyTemperature > cold.needs.bodyTemperature + 1, "season temperature never reached player physiology");
            Require(hot.needs.thirst < cold.needs.thirst - .3f, "summer heat did not increase actual thirst");
            var growing = spring.State.survival.plots[0]; var frozen = winter.State.survival.plots[0];
            Require(growing.growth > .09f && frozen.growth == 0, "winter crop dormancy is cosmetic");
            Require(growing.health == 100 && frozen.health < 90, "winter frost never damaged the crop");
            Require(summer.State.survival.plots[0].moisture < frozen.moisture - .3f, "summer evaporation did not consume soil water");
        });
        Run("season save crosses into summer without redrawing saved snow or diverging on resume", () =>
        {
            var sim = SeasonFixture(0, out var player, world =>
            {
                world.elapsed = 1679.5f; world.tick = 33590; world.nextWaveAt = world.elapsed + 180;
                var w = world.survival.weather;
                w.kind = w.previousKind = WeatherKind.Snow; w.phaseTick = 200;
                w.phaseDurationTicks = 3600; w.transitionTicks = 400;
                w.fromTemperature = -5; w.targetTemperature = -12; w.temperature = -8.5f;
                world.survival.plots.Add(new FarmPlotState { id = world.nextId++, x = 10, y = 10,
                    crop = (int)CropKind.Wheat, moisture = 80 });
            });
            sim.SetConnected(player.id, false);
            var source = Saved(sim.State); string before = Dump(source);
            var restored = new WorldSimulator(source);
            Require(SurvivalDump(restored.State) == SurvivalDump(source), "restore changed saved weather/crop state");
            uint random = source.survival.weather.randomState;
            Advance(sim, 1); Advance(restored, 1);
            Require(SeasonCalendar.Sample(restored.State).season == SeasonKind.Summer, "fixture never crossed into summer");
            Require(restored.State.survival.weather.kind == WeatherKind.Snow &&
                restored.State.survival.weather.phaseTick == 220 && restored.State.survival.weather.randomState == random,
                "season boundary invalidated/redrew the in-progress saved weather");
            bool drewNewPhase = false;
            for (int tick = 0; tick < 7000; tick++)
            {
                sim.Step(WorldSimulator.FixedStep); restored.Step(WorldSimulator.FixedStep);
                if (restored.State.survival.weather.phaseTick == 1)
                {
                    drewNewPhase = true;
                    Require(restored.State.survival.weather.kind != WeatherKind.Snow, "new summer phase reused forbidden snow");
                }
                if (tick % 97 == 0)
                    Require(SurvivalDump(sim.State) == SurvivalDump(restored.State), "seasonal save continuation diverged");
            }
            Require(drewNewPhase && SurvivalDump(sim.State) == SurvivalDump(restored.State), "did not verify a new seasonal weather phase");
            Require(Dump(source) == before, "simulation or calendar aliased the source save");
        });
        Run("season preserves crop moisture gates and rain irrigation", () =>
        {
            foreach (float day in new[] { 3.5f, 10.5f })
            {
                var sim = SeasonFixture(day, out var player, world =>
                {
                    HoldSurvivalWeather(world, 14, 0, .5f);
                    world.survival.plots.Add(new FarmPlotState { id = world.nextId++, x = 10, y = 10,
                        crop = (int)CropKind.Wheat, moisture = 0 });
                    world.survival.plots.Add(new FarmPlotState { id = world.nextId++, x = 12, y = 10,
                        crop = (int)CropKind.Wheat, moisture = 80 });
                });
                sim.SetConnected(player.id, false); Advance(sim, 5);
                var dry = sim.State.survival.plots[0]; var wet = sim.State.survival.plots[1];
                Require(dry.growth == 0 && dry.health < 100 && wet.growth > 0, "season bypassed the actual water gate");
                HoldSurvivalWeather(sim.State, 14, 1, 1); Advance(sim, 20);
                Require(dry.moisture > 12 && dry.growth > 0, "season stopped rain from irrigating a viable crop");
            }
        });
        Run("season leaves disabled survival industrial fixtures inert even in winter", () =>
        {
            var world = Flat(elapsed: 5880); WorldGeneration.LegacyMetadata(world);
            world.version = WorldSimulator.SaveVersion;
            var sim = new WorldSimulator(world); var player = sim.AddPlayer("season-disabled", "Fixture");
            player.needs.thirst = 0; player.needs.bodyTemperature = 34;
            string before = SurvivalDump(sim.State); string needs = JsonSerializer.Serialize(player.needs, Json);
            float hp = player.hp;
            Require(SeasonCalendar.Sample(sim.State).season == SeasonKind.Winter, "disabled fixture is not in winter");
            Advance(sim, 5);
            Require(SurvivalDump(sim.State) == before && JsonSerializer.Serialize(player.needs, Json) == needs && player.hp == hp,
                "winter changed a disabled legacy industrial fixture");
        });
    }
}
