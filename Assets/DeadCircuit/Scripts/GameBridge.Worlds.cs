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
        public List<WorldSaveInfo> Worlds { get; private set; } = new List<WorldSaveInfo>();
        WorldArchive Archive => archive ?? (archive = new WorldArchive(
            Path.Combine(Application.persistentDataPath, "Worlds"), s => JsonUtility.ToJson(s), json => JsonUtility.FromJson<WorldState>(json)));
        string LegacySavePath => Path.Combine(Application.persistentDataPath, "world-v1.json");
        string ResolveSavePath() => smokeHost || smokeClient ? Path.Combine(Application.persistentDataPath, "smoke-world-v1.json") :
            qaRun ? Path.Combine(Application.persistentDataPath, "qa-world-v1.json") : activeWorldId == null ? LegacySavePath : Archive.PathFor(activeWorldId);
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
