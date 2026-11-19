using System;

namespace DeadCircuit
{
    /// <summary>Derived host calendar; years and days are one based. Never persisted or randomized.</summary>
    public readonly struct SeasonSample
    {
        public readonly SeasonKind season;
        public readonly int year, dayOfSeason, daysPerSeason;
        public readonly float progress, temperatureOffset;
        public string Name => SeasonCalendar.Name(season);

        internal SeasonSample(SeasonKind season, int year, int dayOfSeason, float progress, float temperatureOffset)
        {
            this.season = season; this.year = year; this.dayOfSeason = dayOfSeason;
            daysPerSeason = SeasonCalendar.DaysPerSeason; this.progress = progress;
            this.temperatureOffset = temperatureOffset;
        }
    }

    public static class SeasonCalendar
    {
        public const int DaysPerSeason = 7;
        // Start/midpoint of spring, summer, autumn and winter. Smoothstep gives matching
        // zero slopes at each anchor, including the winter-to-spring/year wrap.
        private static readonly float[] TemperatureAnchors = { 0, 4, 8, 12, 0, -6, -14, -26 };

        public static SeasonSample Sample(WorldState world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (float.IsNaN(world.elapsed) || float.IsInfinity(world.elapsed) || world.elapsed < 0 ||
                float.IsNaN(world.dayLength) || float.IsInfinity(world.dayLength) || world.dayLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(world), "Calendar requires finite host time and a positive day length.");
            double days = (double)world.elapsed / world.dayLength;
            double yearIndex = Math.Floor(days / (DaysPerSeason * 4));
            if (yearIndex >= int.MaxValue) throw new ArgumentOutOfRangeException(nameof(world), "Calendar year exceeds supported range.");
            double dayOfYear = days % (DaysPerSeason * 4);
            int seasonIndex = (int)(dayOfYear / DaysPerSeason);
            double dayInSeason = dayOfYear - seasonIndex * DaysPerSeason;
            return new SeasonSample((SeasonKind)seasonIndex, (int)yearIndex + 1,
                (int)Math.Floor(dayInSeason) + 1, (float)(dayInSeason / DaysPerSeason), TemperatureOffset(dayOfYear));
        }

        private static float TemperatureOffset(double dayOfYear)
        {
            double halfSeason = dayOfYear / (DaysPerSeason * .5);
            int anchor = (int)halfSeason;
            double t = halfSeason - anchor; t = t * t * (3 - 2 * t);
            float from = TemperatureAnchors[anchor], to = TemperatureAnchors[(anchor + 1) % TemperatureAnchors.Length];
            return from + (to - from) * (float)t;
        }

        public static string Name(SeasonKind season)
        {
            switch (season)
            {
                case SeasonKind.Spring: return "春季";
                case SeasonKind.Summer: return "夏季";
                case SeasonKind.Autumn: return "秋季";
                case SeasonKind.Winter: return "冬季";
                default: throw new ArgumentOutOfRangeException(nameof(season));
            }
        }
    }
}
