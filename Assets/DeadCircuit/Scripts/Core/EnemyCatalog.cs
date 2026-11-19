using System;
using System.Collections.Generic;

namespace DeadCircuit
{
    public sealed class EnemyStats
    {
        public readonly int variant, unlockWave;
        public readonly string name, description;
        public readonly float hp, speed, damage, attackInterval, structureDamage, structureInterval, armor;
        public readonly float specialRange, specialDamage, specialInterval;
        internal EnemyStats(int variant, string name, string description, int unlockWave, float hp, float speed,
            float damage, float attackInterval, float structureDamage, float structureInterval, float armor = 0f,
            float specialRange = 0f, float specialDamage = 0f, float specialInterval = 0f)
        {
            this.variant = variant; this.name = name; this.description = description; this.unlockWave = unlockWave;
            this.hp = hp; this.speed = speed; this.damage = damage; this.attackInterval = attackInterval;
            this.structureDamage = structureDamage; this.structureInterval = structureInterval; this.armor = armor;
            this.specialRange = specialRange; this.specialDamage = specialDamage; this.specialInterval = specialInterval;
        }
    }

    public static class EnemyCatalog
    {
        public const int Count = 12;
        public static readonly string[] Names = { "普通丧尸", "快跑丧尸", "暴徒丧尸", "爬行丧尸", "喷吐丧尸", "装甲丧尸",
            "燃烧丧尸", "尖啸丧尸", "防化感染者", "冻伤丧尸", "自爆丧尸", "夜行精英" };
        public static readonly IReadOnlyList<EnemyStats> All = Array.AsReadOnly(new[]
        {
            new EnemyStats(0, Names[0], "稳定追击并拆除障碍；保留原始规则。", 1, 45, 1.15f, 10, 1.05f, 14, .8f),
            new EnemyStats(1, Names[1], "快速追击，近战间隔更短。", 2, 32, 1.8f, 10, .8f, 14, .8f),
            new EnemyStats(2, Names[2], "缓慢高血量；重击与拆墙伤害更高。", 3, 110, .85f, 18, 1.05f, 26, .8f),
            new EnemyStats(3, Names[3], "低伏慢行，近身撕咬额外削减16体力，妨碍冲刺。", 2, 38, .65f, 7, .7f, 8, 1.1f),
            new EnemyStats(4, Names[4], "6.5格内有视线才停步喷吐；遮挡时绕行或拆障。", 3, 70, .95f, 6, 1.8f, 10, 1.2f, 0, 6.5f, 12, 2.4f),
            new EnemyStats(5, Names[5], "装甲使普通伤害减半；穿甲弹可无视该装甲。", 4, 145, .75f, 13, 1.3f, 24, 1.2f, 100),
            new EnemyStats(6, Names[6], "近身每1.5秒灼烧可见玩家和设施；可由生存模块累计升温。", 4, 60, 1.25f, 8, 1.2f, 14, 1, 0, 1.8f, 5, 1.5f),
            new EnemyStats(7, Names[7], "看到10格内玩家时尖啸，唤醒14格内同伴8秒。", 3, 58, 1, 6, 1.5f, 10, 1.2f, 0, 10, 0, 4.5f),
            new EnemyStats(8, Names[8], "2.25格内每2秒毒性脉冲，损耗血量和体力；可累计感染。", 5, 90, 1, 9, 1.35f, 18, 1, 0, 2.25f, 4, 2),
            new EnemyStats(9, Names[9], "3格内寒息削减24体力；可由生存模块累计失温。", 5, 80, .9f, 9, 1.4f, 16, 1.1f, 0, 3, 4, 2),
            new EnemyStats(10, Names[10], "靠近1.4格主动自爆，3格范围伤及玩家、设施和资源；击杀可阻止爆炸。", 6, 52, 1.3f, 0, 1, 0, 1, 0, 1.4f, 42, 1),
            new EnemyStats(11, Names[11], "白天畏光，7格外停止追击；夜间疾奔并加快重击。", 7, 100, 1.1f, 11, 1.3f, 20, 1, 0, 7, 0, 0)
        });

        public static EnemyStats Stats(int variant)
        {
            if (variant < 0 || variant >= Count) throw new ArgumentOutOfRangeException(nameof(variant));
            return All[variant];
        }

        // Match the existing solar day: noon at phase 0, darkness over the middle half.
        public static bool IsNight(float elapsed, float dayLength)
        {
            if (float.IsNaN(elapsed) || float.IsInfinity(elapsed) || float.IsNaN(dayLength) || float.IsInfinity(dayLength) || elapsed < 0 || dayLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(dayLength));
            float phase = elapsed % dayLength / dayLength; return phase >= .25f && phase < .75f;
        }
        public static float Speed(int variant, bool night) => variant == 11 && night ? 2.6f : Stats(variant).speed;
        public static float Damage(int variant, bool night) => variant == 11 && night ? 23f : Stats(variant).damage;
        public static float AttackInterval(int variant, bool night) => variant == 11 && night ? .65f : Stats(variant).attackInterval;
        public static float IncomingDamageMultiplier(int variant, bool armorPiercing) => armorPiercing ? 1f : 100f / (100f + Stats(variant).armor);

        // Pure deterministic weighted selection. The caller keeps legacy modulo selection
        // for gen0 and wave 1; new types unlock gradually, never in the initial six.
        public static int SelectVariant(int wave, BiomeKind biome, DistrictKind district, bool night, uint roll)
        {
            int total = 0;
            for (int i = 0; i < Count; i++) total += Weight(i, wave, biome, district, night);
            if (total == 0) return 0;
            int choice = (int)(roll % (uint)total);
            for (int i = 0; i < Count; i++) { choice -= Weight(i, wave, biome, district, night); if (choice < 0) return i; }
            return 0;
        }

        private static int Weight(int variant, int wave, BiomeKind biome, DistrictKind district, bool night)
        {
            if (wave < All[variant].unlockWave) return 0;
            int weight = variant == 0 ? 50 : variant == 1 ? 28 : variant == 2 ? 16 : variant == 3 || variant == 4 ? 8 : variant == 5 ? 7 : variant == 6 || variant == 9 ? 6 : variant == 7 || variant == 8 ? 5 : variant == 10 ? 4 : 3;
            if (variant == 3 && (biome == BiomeKind.DeciduousForest || biome == BiomeKind.ConiferousForest)) weight += 8;
            if ((variant == 4 || variant == 8) && biome == BiomeKind.Wetland) weight += 12;
            if (variant == 6 && biome == BiomeKind.Desert) weight += 12;
            if (variant == 9 && biome == BiomeKind.Tundra) weight += 16;
            if (district == DistrictKind.Industrial && (variant == 5 || variant == 10)) weight += 12;
            if (district == DistrictKind.Urban && (variant == 7 || variant == 8)) weight += 10;
            if (variant == 11) weight = night ? weight + (district == DistrictKind.Urban ? 12 : 6) : 1;
            return weight;
        }
    }
}
