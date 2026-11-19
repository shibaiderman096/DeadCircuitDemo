namespace DeadCircuit
{
    // Persisted order: skin, hair style/color, top style/color, pants, shoes, face.
    public static class AppearanceCatalog
    {
        public const int FieldCount = 8, DefaultCode = 0;
        private static readonly int[] Counts = { 6, 6, 8, 3, 8, 8, 6, 3 };
        private static readonly string[] Labels = { "肤色", "发型", "发色", "上衣款式", "上衣颜色", "裤子颜色", "鞋子颜色", "面容" };
        private static readonly string[][] Names =
        {
            new[] { "瓷白", "浅肤", "暖肤", "蜜色", "棕肤", "深肤" },
            new[] { "短发", "侧分", "刺发", "齐颈", "马尾", "光头" },
            new[] { "墨黑", "深棕", "栗棕", "金黄", "银白", "赤红", "靛蓝", "青绿" },
            new[] { "短袖", "夹克", "连帽衫" },
            new[] { "灰白", "炭黑", "砖红", "橙黄", "草绿", "湖蓝", "藏青", "紫罗" },
            new[] { "炭黑", "深灰", "灰蓝", "藏青", "卡其", "军绿", "棕褐", "浅灰" },
            new[] { "炭黑", "雪白", "棕褐", "深灰", "藏青", "暗红" },
            new[] { "自然", "坚毅", "微笑" }
        };

        public static int Count(int field) => field >= 0 && field < FieldCount ? Counts[field] : 0;

        public static int Get(int code, int field) => Count(field) > 0 && IsValid(code) ? (code >> (field * 3)) & 7 : 0;

        public static int With(int code, int field, int value)
        {
            if (!IsValid(code)) return DefaultCode;
            if (value < 0 || value >= Count(field)) return code;
            int shift = field * 3;
            return (code & ~(7 << shift)) | (value << shift);
        }

        public static string Label(int field) => Count(field) > 0 ? Labels[field] : "";

        public static string Name(int field, int value) => value >= 0 && value < Count(field) ? Names[field][value] : "";

        public static bool IsValid(int code)
        {
            if ((uint)code > 0xffffffu) return false;
            for (int field = 0; field < FieldCount; field++)
                if (((code >> (field * 3)) & 7) >= Counts[field]) return false;
            return true;
        }
    }
}
