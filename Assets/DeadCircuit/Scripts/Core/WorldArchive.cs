using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DeadCircuit
{
    [Serializable] public sealed class WorldSaveInfo
    {
        public string saveId, name, modified, summary;
        public int seed;
    }

    // Serialization stays at the boundary: Unity uses JsonUtility, core checks use System.Text.Json.
    public sealed class WorldArchive
    {
        public const int MaxBytes = 4 * 1024 * 1024, MaxWorlds = 64;
        readonly string root;
        readonly Func<WorldState, string> encode;
        readonly Func<string, WorldState> decode;
        public int UnreadableCount { get; private set; }
        public WorldArchive(string directory, Func<WorldState, string> serialize, Func<string, WorldState> deserialize)
        {
            root = Path.GetFullPath(directory ?? throw new ArgumentNullException(nameof(directory)));
            encode = serialize ?? throw new ArgumentNullException(nameof(serialize));
            decode = deserialize ?? throw new ArgumentNullException(nameof(deserialize));
        }
        public string PathFor(string id)
        {
            if (id == null || id.Length != 32 || !Guid.TryParseExact(id, "N", out _))
                throw new ArgumentException("无效的世界标识。", nameof(id));
            return Path.Combine(root, id.ToLowerInvariant() + ".json");
        }
        public WorldState Read(string id) => ReadPath(PathFor(id));
        public WorldState ReadPath(string path)
        {
            // ReadPath is also used for the one explicitly named legacy save by the bridge.
            var file = new FileInfo(path);
            if (!file.Exists || file.Length <= 0 || file.Length > MaxBytes) throw new InvalidDataException("存档不存在、为空或超过4MB。");
            WorldState state;
            try { state = decode(File.ReadAllText(path, Encoding.UTF8)); }
            catch (Exception e) { throw new InvalidDataException("存档格式无效。", e); }
            return new WorldSimulator(state).State;
        }
        public void Write(string id, WorldState state)
        {
            string path = PathFor(id);
            if (!File.Exists(path) && Directory.Exists(root) && Directory.GetFiles(root, "*.json").Length >= MaxWorlds)
                throw new InvalidOperationException("本地世界已达到64个，请使用已有世界。");
            // Reject a corrupt state before touching the previous good save.
            var checkedState = new WorldSimulator(state).State;
            byte[] bytes = new UTF8Encoding(false).GetBytes(encode(checkedState));
            if (bytes.Length == 0 || bytes.Length > MaxBytes) throw new InvalidDataException("存档超过4MB。");
            Directory.CreateDirectory(root);
            string pending = path + ".tmp";
            using (var stream = new FileStream(pending, FileMode.Create, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(path)) File.Replace(pending, path, path + ".bak");
            else File.Move(pending, path);
        }
        public List<WorldSaveInfo> List(string legacyPath = null)
        {
            var entries = new List<Tuple<DateTime, WorldSaveInfo>>();
            UnreadableCount = 0;
            if (Directory.Exists(root)) foreach (string path in Directory.GetFiles(root, "*.json"))
            {
                string id = Path.GetFileNameWithoutExtension(path);
                if (id.Length != 32 || !Guid.TryParseExact(id, "N", out _)) continue;
                Add(entries, id, path);
                if (entries.Count >= MaxWorlds) break;
            }
            if (legacyPath != null && File.Exists(legacyPath)) Add(entries, "legacy", legacyPath);
            entries.Sort((a,b) => b.Item1.CompareTo(a.Item1));
            var result = new List<WorldSaveInfo>();
            foreach (var entry in entries) result.Add(entry.Item2);
            return result;
        }
        void Add(List<Tuple<DateTime, WorldSaveInfo>> entries, string id, string path)
        {
            try
            {
                WorldState s = ReadPath(path); DateTime time = File.GetLastWriteTimeUtc(path);
                entries.Add(Tuple.Create(time, new WorldSaveInfo
                {
                    saveId = id, name = s.generation == null ? "旧世界" : s.generation.worldName, seed = s.seed,
                    modified = time.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                    summary = "种子 " + s.seed + " · 第" + (1 + (int)(s.elapsed / s.dayLength)) + "天 · " + TechCatalog.EraName(s.era) + " · " + s.players.Count + "名角色"
                }));
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException || e is InvalidOperationException || e is FormatException)
            { UnreadableCount++; }
        }
    }
}
