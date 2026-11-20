using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DeadCircuit
{
    public sealed partial class GameBridge
    {
        string activeWorldId;
        WorldGenerationSettings pendingGeneration;
        WorldArchive archive;
        string qaSavePathOverride;
        public string QaOutputDirectory { get; private set; }
        public bool QaArgumentsValid { get; private set; } = true;
        public string QaArgumentError { get; private set; } = "";
        public List<WorldSaveInfo> Worlds { get; private set; } = new List<WorldSaveInfo>();
        WorldArchive Archive => archive ?? (archive = new WorldArchive(
            Path.Combine(Application.persistentDataPath, "Worlds"), s => JsonUtility.ToJson(s), json => JsonUtility.FromJson<WorldState>(json)));
        string LegacySavePath => Path.Combine(Application.persistentDataPath, "world-v1.json");
        public string QaSavePath => qaSavePathOverride ?? (qaRun ? string.Empty : Path.Combine(Application.persistentDataPath, "qa-world-v1.json"));
        void ConfigureQaSavePath()
        {
            if (!qaRun) return;
            string[] args = Environment.GetCommandLineArgs();
            QaArgumentsValid = TryResolveQaPaths(args, out string outputDirectory, out string savePath, out string error);
            QaOutputDirectory = outputDirectory;
            QaArgumentError = error;
            if (QaArgumentsValid) qaSavePathOverride = savePath;
        }

        internal static bool TryResolveQaPaths(string[] args, out string outputDirectory, out string savePath, out string error)
        {
            outputDirectory = Application.persistentDataPath;
            savePath = "";
            error = "";
            if (args == null || !Array.Exists(args, arg => arg == "--demo-qa"))
            {
                error = "缺少 --demo-qa";
                return false;
            }
            if (Array.Exists(args, arg => arg == "--demo-smoke-host" || arg == "--demo-smoke-client"))
            {
                error = "--demo-qa 不能和 smoke 参数同时使用";
                return false;
            }

            if (!TryReadArgument(args, "--demo-qa-dir", true, ref outputDirectory, out error) ||
                !TryReadArgument(args, "--demo-qa-save-path", true, ref savePath, out error))
                return false;

            try
            {
                outputDirectory = Path.GetFullPath(outputDirectory);
                savePath = Path.GetFullPath(savePath);
            }
            catch (Exception e)
            {
                error = "QA 路径无效：" + e.Message;
                return false;
            }

            string directoryPrefix = outputDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!savePath.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                error = "QA 存档必须位于 --demo-qa-dir 内";
                return false;
            }
            return true;
        }

        static bool TryReadArgument(string[] args, string name, bool required, ref string value, out string error)
        {
            error = "";
            int index = Array.IndexOf(args, name);
            if (index >= 0 && Array.IndexOf(args, name, index + 1) >= 0)
            {
                error = name + " 不能重复";
                return false;
            }
            if (index < 0)
            {
                if (required)
                {
                    error = "缺少 " + name + " 值";
                    return false;
                }
                return true;
            }
            if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]) || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                error = name + " 必须带非空值";
                return false;
            }
            value = args[index + 1];
            return true;
        }
        string ResolveSavePath() => smokeHost || smokeClient ? Path.Combine(Application.persistentDataPath, "smoke-world-v1.json") :
            qaRun ? QaSavePath : activeWorldId == null ? LegacySavePath : Archive.PathFor(activeWorldId);
        public void RefreshWorldList()
        {
            try
            {
                if (qaRun)
                {
                    Worlds = new List<WorldSaveInfo>();
                    string path = ResolveSavePath();
                    if (File.Exists(path))
                    {
                        WorldState saved = Archive.ReadPath(path);
                        Worlds.Add(new WorldSaveInfo { saveId = "qa", name = saved.generation.worldName, seed = saved.seed,
                            modified = File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm"), summary = "独立 QA 世界" });
                    }
                    return;
                }
                Worlds = Archive.List(LegacySavePath);
            }
            catch (Exception e) { Status = "读取世界列表失败：" + e.Message; Worlds = new List<WorldSaveInfo>(); }
        }
        public void CreateWorld(WorldGenerationSettings settings)
        {
            if (IsRunning || connecting || leaving) return;
            if (settings == null) { Status = "请填写世界设置。"; return; }
            string previous = activeWorldId;
            pendingGeneration = settings; activeWorldId = Guid.NewGuid().ToString("N");
            try
            {
                StartHost(settings.seed, false);
                if (IsHost && IsRunning) { SaveInternal(false); RefreshWorldList(); }
                else activeWorldId = previous;
            }
            finally { pendingGeneration = null; }
        }
        internal bool CanDeleteWorld => !IsRunning && !connecting && !leaving && !smokeHost && !smokeClient;

        internal void DeleteWorld(string saveId)
        {
            if (!CanDeleteWorld) throw new InvalidOperationException("请先退出当前游戏或连接。");
            if (!Worlds.Exists(world => world.saveId == saveId))
                throw new InvalidOperationException("所选存档已不在列表中，请刷新后重试。");

            if (qaRun)
            {
                if (saveId != "qa") throw new ArgumentException("QA 只能删除独立测试世界。", nameof(saveId));
                WorldArchive.DeletePath(ResolveSavePath());
            }
            else if (saveId == "legacy") WorldArchive.DeletePath(LegacySavePath);
            else Archive.Delete(saveId);
        }

        public void LoadWorld(string saveId)
        {
            if (IsRunning || connecting || leaving) return;
            try
            {
                if (qaRun)
                {
                    if (saveId != "qa") throw new ArgumentException("QA 只能读取独立测试世界。");
                    activeWorldId = null; StartHost(0, true); return;
                }
                if (saveId != "legacy") Archive.PathFor(saveId);
                activeWorldId = saveId == "legacy" ? null : saveId;
                StartHost(0, true);
                if (saveId == "legacy" && IsRunning && IsHost)
                {
                    // Continue as a separate world; the original legacy archive stays recoverable.
                    activeWorldId = Guid.NewGuid().ToString("N");
                    SaveInternal(false);
                    RefreshWorldList();
                }
            }
            catch (Exception e) { Status = "读取世界失败：" + e.Message; }
        }
    }
}
