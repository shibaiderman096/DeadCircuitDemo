using System;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace DeadCircuit.EditorTools
{
    [InitializeOnLoad]
    public static class PackageInstaller
    {
        static AddAndRemoveRequest request;
        static double deadline;
        static PackageInstaller()
        {
            if (SessionState.GetBool("DeadCircuit.InstallActive", false)) EditorApplication.delayCall += ResumeAfterReload;
        }
        static void ResumeAfterReload()
        {
            if (UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.netcode.gameobjects") != null) Finish();
            else Install();
        }
        public static void Install()
        {
            if (UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.netcode.gameobjects") != null) { Finish(); return; }
            SessionState.SetBool("DeadCircuit.InstallActive", true);
            request = Client.AddAndRemove(new[] { "com.unity.netcode.gameobjects" });
            deadline = EditorApplication.timeSinceStartup + 600;
            EditorApplication.update += Poll;
        }
        static void Poll()
        {
            if (!request.IsCompleted && EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= Poll;
            if (request.Status != StatusCode.Success)
            {
                SessionState.SetBool("DeadCircuit.InstallActive", false);
                Debug.LogError("[DeadCircuit] Package installation failed: " + request.Error?.message);
                EditorApplication.Exit(2);
                return;
            }
            Debug.Log("[DeadCircuit] Packages installed: " + string.Join(",", request.Result.Select(p => p.name + "@" + p.version)));
            Finish();
        }
        static void Finish()
        {
            SessionState.SetBool("DeadCircuit.InstallActive", false);
            // Enable networking on the next compilation, after its assemblies exist.
            var target = UnityEditor.Build.NamedBuildTarget.Standalone;
            var symbols = PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            if (!symbols.Contains("DEADCIRCUIT_NETCODE")) symbols.Add("DEADCIRCUIT_NETCODE");
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }
    }
}
