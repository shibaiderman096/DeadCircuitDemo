using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace DeadCircuit.EditorTools
{
    [InitializeOnLoad]
    public static class DemoProject
    {
        const string ScenePath = "Assets/DeadCircuit/Scenes/DeadCircuitDemo.unity";
        static double resourceDeadline;
        static DemoProject()
        {
            if (SessionState.GetBool("DeadCircuit.ImportUi", false))
            {
                resourceDeadline = EditorApplication.timeSinceStartup + 120;
                EditorApplication.update += WaitForUiResources;
            }
        }
        public static void ImportUiResources()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null) { EditorApplication.Exit(0); return; }
            SessionState.SetBool("DeadCircuit.ImportUi", true);
            resourceDeadline = EditorApplication.timeSinceStartup + 120;
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            EditorApplication.update += WaitForUiResources;
        }
        static void WaitForUiResources()
        {
            if (EditorApplication.isUpdating || EditorApplication.isCompiling) return;
            if (Resources.Load<TMP_Settings>("TMP Settings") != null)
            {
                EditorApplication.update -= WaitForUiResources;
                SessionState.SetBool("DeadCircuit.ImportUi", false);
                AssetDatabase.SaveAssets();
                Debug.Log("DEMO_UI_RESOURCES_PASS");
                EditorApplication.Exit(0);
            }
            else if (EditorApplication.timeSinceStartup > resourceDeadline)
            {
                EditorApplication.update -= WaitForUiResources;
                SessionState.SetBool("DeadCircuit.ImportUi", false);
                Debug.LogError("TMP essential resources import timed out.");
                EditorApplication.Exit(3);
            }
        }
        public static void Prepare()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ImportTmpResources();
            EnsureFont();
            ImportArt();
            Directory.CreateDirectory("Assets/DeadCircuit/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("DeadCircuit / World Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 9.75f;
            camera.transform.position = new Vector3(32, 35, -10);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            // Build analysis must see the camera and effects used by runtime-created objects.
            const string atmospherePath = "Assets/DeadCircuit/Resources/Art/Atmosphere.asset";
            var atmosphere = AssetDatabase.LoadAssetAtPath<VolumeProfile>(atmospherePath);
            if (atmosphere == null)
            {
                atmosphere = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(atmosphere, atmospherePath);
                var color = atmosphere.Add<ColorAdjustments>(true);
                color.postExposure.Override(.06f); color.contrast.Override(3f); color.saturation.Override(-3f);
                var vignette = atmosphere.Add<Vignette>(true);
                vignette.intensity.Override(.12f); vignette.smoothness.Override(.65f);
                vignette.color.Override(new Color(.035f, .065f, .065f));
                var bloom = atmosphere.Add<Bloom>(true);
                bloom.intensity.Override(.10f); bloom.threshold.Override(1.1f); bloom.scatter.Override(.4f);
                foreach (var component in atmosphere.components) AssetDatabase.AddObjectToAsset(component, atmosphere);
                EditorUtility.SetDirty(atmosphere);
            }
            var global = new GameObject("DeadCircuit / Atmosphere").AddComponent<Volume>();
            global.isGlobal = true;
            global.sharedProfile = atmosphere;
            new GameObject("DeadCircuit / Bootstrap").AddComponent<GameBridge>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "DeadCircuit";
            PlayerSettings.productName = "Dead Circuit - 废土回路";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            if (GraphicsSettings.TryGetRenderPipelineSettings<URPShaderStrippingSetting>(out var stripping))
            {
                // Profiles are instantiated at runtime; retain their post-process resources.
                stripping.stripUnusedPostProcessingVariants = false;
                EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Settings/UniversalRenderPipelineGlobalSettings.asset"));
            }
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.deadcircuit.demo");
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
            AssetDatabase.SaveAssets();
            Debug.Log("DEMO_PREPARE_PASS scene=" + ScenePath + " quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()]);
        }

        static void ImportTmpResources()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
                throw new InvalidOperationException("Run DemoProject.ImportUiResources without -quit first; TMP package import is asynchronous.");
        }

        static void EnsureFont()
        {
            const string path = "Assets/DeadCircuit/Resources/Fonts/DemoChinese SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/DeadCircuit/Resources/Fonts/DemoChinese.otf");
                if (source == null) throw new InvalidOperationException("Bundled Chinese font is missing.");
                var font = TMP_FontAsset.CreateFontAsset(source, 48, 5, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                if (font == null || font.material == null) throw new InvalidOperationException("Chinese font generation failed.");
                font.name = "DemoChinese SDF";
                font.isMultiAtlasTexturesEnabled = true;
                AssetDatabase.CreateAsset(font, path);
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
                // Seed game-specific glyphs, avoiding the first-frame font population spike.
                string words = string.Join("", DemoCatalog.ItemNames.Concat(DemoCatalog.BuildNames).Concat(DemoCatalog.AttributeNames).Concat(DemoCatalog.SkillNames));
                font.TryAddCharacters(words + "废土回路生存探索联机创建世界加载存档主客户端地址随机种子开始返回技能属性点升级生命体力饥饿玩家电力产线攻击制造弹药发电燃料木材矿脉工程战斗防守幸存者0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz .:/→↑←↓+-%()，。；：、", out _);
                EditorUtility.SetDirty(font);
            }
        }

        static void ImportArt()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/DeadCircuit/Resources/Art" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 32;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            string materialPath = "Assets/DeadCircuit/Resources/Art/WorldMaterial.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) throw new InvalidOperationException("URP 2D unlit shader unavailable.");
                AssetDatabase.CreateAsset(new Material(shader), materialPath);
            }
        }

        public static void BuildWindows()
        {
            Prepare();
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Windows/DeadCircuitDemo.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            Debug.Log("DEMO_BUILD_" + report.summary.result.ToString().ToUpperInvariant() + " bytes=" + report.summary.totalSize);
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Windows build failed: " + report.summary.result);
        }
    }
}
