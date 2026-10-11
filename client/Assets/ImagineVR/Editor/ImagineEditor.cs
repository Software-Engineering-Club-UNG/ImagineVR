using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace ImagineVR.EditorTools
{
    /// <summary>
    /// One-click project setup. Everything a fresh clone needs (render pipeline, XR, Android
    /// settings, font, scene) is created from here, so the project is reproducible on any machine.
    /// Each step only creates what is missing; running it again is harmless.
    /// </summary>
    public static class Setup
    {
        const string Root = "Assets/ImagineVR";
        const string ScenePath = Root + "/Scenes/Main.unity";
        const string FontSource = Root + "/Fonts/JetBrainsMono-Regular.ttf";
        const string FontAsset = Root + "/Resources/Fonts/ImagineMono.asset";
        const string SettingsAsset = Root + "/Resources/ImagineSettings.asset";
        const string PipelineAsset = Root + "/Settings/ImaginePipeline.asset";
        const string RendererAsset = Root + "/Settings/ImagineRenderer.asset";
        const string TmpEssentials = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";

        [MenuItem("ImagineVR/Setup Project", priority = 0)]
        public static void SetupProject()
        {
            Step1();
            Step2();
            EditorUtility.DisplayDialog("ImagineVR", "Project setup finished. Open Assets/ImagineVR/Scenes/Main and press Play.", "OK");
        }

        /// <summary>Batch entry point: settings, render pipeline, XR, text resources.</summary>
        public static void Step1()
        {
            EnsureFolders();
            ImportTextResources();
            ConfigurePlayer();
            ConfigurePipeline();
            ConfigureXr();
            AssetDatabase.SaveAssets();
            Debug.Log("ImagineVR setup: step 1 done");
        }

        /// <summary>Batch entry point: font, settings asset, scene, catalog. Run after step 1 has been imported.</summary>
        public static void Step2()
        {
            EnsureFolders();
            CreateFont();
            CreateSettings();
            CreateScene();
            Catalog.Export();
            AssetDatabase.SaveAssets();
            Debug.Log("ImagineVR setup: step 2 done");
        }

        static void EnsureFolders()
        {
            foreach (string folder in new[] { "Scenes", "Settings", "Resources", "Resources/Fonts", "Fonts" })
            {
                string path = Root + "/" + folder;
                if (!AssetDatabase.IsValidFolder(path))
                {
                    Directory.CreateDirectory(path);
                    AssetDatabase.Refresh();
                }
            }
        }

        /// <summary>
        /// Batch entry point: imports the TextMesh Pro resources and exits when the import has
        /// really finished (it is asynchronous, so a plain -quit run would leave too early).
        /// Run without -quit.
        /// </summary>
        public static void ImportText()
        {
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro")) { Debug.Log("ImagineVR setup: text resources already present"); EditorApplication.Exit(0); return; }
            AssetDatabase.importPackageCompleted += name => { Debug.Log("ImagineVR setup: imported " + name); EditorApplication.Exit(0); };
            AssetDatabase.importPackageFailed += (name, error) => { Debug.LogError("ImagineVR setup: import failed: " + error); EditorApplication.Exit(1); };
            AssetDatabase.importPackageCancelled += name => EditorApplication.Exit(1);
            AssetDatabase.ImportPackage(TmpEssentials, false);
        }

        static void ImportTextResources()
        {
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro")) return;
            if (!File.Exists(TmpEssentials)) { Debug.LogWarning("ImagineVR setup: TextMesh Pro resources package not found"); return; }
            AssetDatabase.ImportPackage(TmpEssentials, false);
            AssetDatabase.Refresh();
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Hacklanta";
            PlayerSettings.productName = "ImagineVR";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.hacklanta.imaginevr");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            // Lets the headset talk to a server on the local network over plain http while developing.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            PlayerSettings.stripEngineCode = false;

            // Input System only (1). Read by the editor on its next start.
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets.Length > 0)
            {
                var so = new SerializedObject(assets[0]);
                SerializedProperty handler = so.FindProperty("activeInputHandler");
                if (handler != null && handler.intValue != 1)
                {
                    handler.intValue = 1;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        static void ConfigurePipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAsset);
            if (pipeline == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererAsset);
                ResourceReloader.ReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelineAsset);
            }
            // Tuned for Quest: no shadows, no HDR, no extra textures, 4x MSAA.
            var so = new SerializedObject(pipeline);
            SetBool(so, "m_SupportsHDR", false);
            SetInt(so, "m_MSAA", 4);
            SetBool(so, "m_MainLightShadowsSupported", false);
            SetBool(so, "m_AdditionalLightShadowsSupported", false);
            SetInt(so, "m_AdditionalLightsRenderingMode", 0);
            SetBool(so, "m_RequireDepthTexture", false);
            SetBool(so, "m_RequireOpaqueTexture", false);
            SetBool(so, "m_UseSRPBatcher", true);
            SetBool(so, "m_SupportsDynamicBatching", false);
            SetFloat(so, "m_RenderScale", 1f);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
                QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
                QualitySettings.vSyncCount = 0;
                QualitySettings.antiAliasing = 4;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        static void SetBool(SerializedObject so, string name, bool value) { SerializedProperty p = so.FindProperty(name); if (p != null) p.boolValue = value; }
        static void SetInt(SerializedObject so, string name, int value) { SerializedProperty p = so.FindProperty(name); if (p != null) p.intValue = value; }
        static void SetFloat(SerializedObject so, string name, float value) { SerializedProperty p = so.FindProperty(name); if (p != null) p.floatValue = value; }

        static void ConfigureXr()
        {
            MethodInfo getOrCreate = typeof(XRGeneralSettingsPerBuildTarget).GetMethod("GetOrCreate", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            var perTarget = getOrCreate != null ? getOrCreate.Invoke(null, null) as XRGeneralSettingsPerBuildTarget : null;
            if (perTarget == null) { Debug.LogError("ImagineVR setup: could not create XR settings"); return; }

            const BuildTargetGroup android = BuildTargetGroup.Android;
            if (!perTarget.HasSettingsForBuildTarget(android)) perTarget.CreateDefaultSettingsForBuildTarget(android);
            if (!perTarget.HasManagerSettingsForBuildTarget(android)) perTarget.CreateDefaultManagerSettingsForBuildTarget(android);
            XRGeneralSettings general = perTarget.SettingsForBuildTarget(android);
            general.InitManagerOnStart = true;
            if (!XRPackageMetadataStore.AssignLoader(general.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", android))
                Debug.LogWarning("ImagineVR setup: OpenXR loader was not assigned for Android (it may already be)");
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(perTarget);

            FeatureHelpers.RefreshFeatures(android);
            OpenXRSettings openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(android);
            if (openxr == null) { Debug.LogError("ImagineVR setup: no OpenXR settings for Android"); return; }
            foreach (OpenXRFeature feature in openxr.GetFeatures())
            {
                if (feature == null) continue;
                bool wanted = feature is MetaQuestFeature || feature is OculusTouchControllerProfile ||
                              feature is MetaQuestTouchPlusControllerProfile || feature is MetaQuestTouchProControllerProfile ||
                              feature is FoveatedRenderingFeature || feature is QuestFeature;
                if (wanted && !feature.enabled)
                {
                    feature.enabled = true;
                    EditorUtility.SetDirty(feature);
                }
            }
            openxr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            EditorUtility.SetDirty(openxr);
        }

        /// <summary>Every character the terminal draws, baked into the font atlas ahead of time.</summary>
        public const string TerminalCharacters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            " ¡£©«°±·»¿ÀÁÄÇÈÉÍÑÓÖ×ÚÜß" +
            "àáâäçèéêëíîïñóôö÷ùúûü" +
            "–—‘’“”•…‹›€" +
            "←↑→↓─│┌┐└┘╭╮╯╰" +
            "▁▂▃▄▅▆▇█░▒▓" +
            "■□▲▶▼◀○●★☆✓✗✦✱✻⎿∗" +
            "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏";

        static void CreateFont()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAsset) != null) return;
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontSource);
            if (font == null) { Debug.LogWarning("ImagineVR setup: no font at " + FontSource + ", the terminal will use the default font"); return; }
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 60, 6, GlyphRenderMode.SDFAA, 2048, 1024, AtlasPopulationMode.Dynamic, true);
            if (asset == null) { Debug.LogError("ImagineVR setup: could not create the font asset"); return; }
            AssetDatabase.CreateAsset(asset, FontAsset);
            asset.material.name = "ImagineMono Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            asset.atlasTextures[0].name = "ImagineMono Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            asset.TryAddCharacters(TerminalCharacters, out string missing);
            if (!string.IsNullOrEmpty(missing))
            {
                var codes = new StringBuilder();
                foreach (char c in missing) codes.Append("U+").Append(((int)c).ToString("X4")).Append(' ');
                Debug.Log("ImagineVR setup: font has no glyph for " + codes + "(the terminal falls back to plainer characters)");
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        static void CreateSettings()
        {
            if (AssetDatabase.LoadAssetAtPath<ImagineSettings>(SettingsAsset) != null) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<ImagineSettings>(), SettingsAsset);
        }

        static void CreateScene()
        {
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("ImagineVR").AddComponent<ImagineApp>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        public static void OpenMainScene()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }

    /// <summary>Writes shared/catalog.json: the vocabulary the server gives to the AI.</summary>
    public static class Catalog
    {
        [MenuItem("ImagineVR/Export Catalog", priority = 20)]
        public static void Export()
        {
            Library.EnsureLoaded();
            var root = new Dictionary<string, object>
            {
                ["language_version"] = 2,
                ["catalog_version"] = "",
                ["limits"] = new Dictionary<string, object>
                {
                    ["max_spawned"] = ObjectSystem.MaxSpawned,
                    ["max_scatter_count"] = ObjectSystem.MaxScatterCount,
                    ["max_scatter_total"] = ObjectSystem.MaxScatterTotal,
                    ["max_physics"] = PropSystem.MaxPhysics,
                    ["safe_radius_m"] = (int)World.SafeRadius,
                    ["world_radius_m"] = (int)World.Radius,
                },
            };

            var skies = new List<object>();
            foreach (SkyPreset p in Library.SkyPresets) skies.Add(p.ToCatalog());
            root["sky_presets"] = skies;

            var terrains = new List<object>();
            foreach (TerrainType t in Library.TerrainTypes)
            {
                Dictionary<string, object> d = t.ToCatalog();
                d["material"] = t.Material;
                if (!float.IsNaN(t.WaterLevel)) d["water"] = true;
                terrains.Add(d);
            }
            root["terrain_types"] = terrains;

            var materials = new List<object>();
            foreach (GroundMaterial m in Library.GroundMaterials) materials.Add(m.ToCatalog());
            root["materials"] = materials;

            var assets = new List<object>();
            var primitives = new List<object>();
            foreach (AssetDef a in Library.AssetList)
            {
                if (a.Primitive) { primitives.Add(a.Id); continue; }
                Dictionary<string, object> d = a.ToCatalog();
                d["category"] = a.Category;
                d["size_m"] = a.Size;
                if (a.Holdable) d["holdable"] = true;
                if (a.Prop) d["prop"] = true;
                assets.Add(d);
            }
            root["assets"] = assets;
            root["primitives"] = primitives;

            var motions = new List<object>();
            foreach (string m in Library.Motions) motions.Add(m);
            root["motions"] = motions;

            var weather = new List<object>();
            foreach (WeatherDef w in Library.WeatherList) weather.Add(w.ToCatalog());
            root["weather"] = weather;

            var ambience = new List<object>();
            foreach (AmbienceDef a in Library.AmbienceList) ambience.Add(a.ToCatalog());
            root["ambience"] = ambience;

            // The version only changes when the content does.
            string body = MiniJson.Serialize(root);
            string hash;
            using (SHA1 sha = SHA1.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(body));
                hash = BitConverter.ToString(digest, 0, 4).Replace("-", "").ToLowerInvariant();
            }
            string version = "1." + assets.Count + "." + hash;
            root["catalog_version"] = version;

            string shared = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "shared"));
            Directory.CreateDirectory(shared);
            string file = Path.Combine(shared, "catalog.json");
            File.WriteAllText(file, MiniJson.Serialize(root, true) + "\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(Application.dataPath, "ImagineVR", "Resources", "CatalogVersion.txt"), version + "\n", new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log("ImagineVR: exported catalog " + version + " (" + assets.Count + " assets, " +
                      skies.Count + " skies) to " + file);
        }

        /// <summary>Batch helper: builds every asset mesh once and reports triangle counts, to catch broken recipes.</summary>
        public static void CheckAssets()
        {
            Library.EnsureLoaded();
            int total = 0, worst = 0;
            string worstId = "";
            var sb = new StringBuilder();
            foreach (AssetDef a in Library.AssetList)
            {
                try
                {
                    Mesh mesh = a.Mesh;
                    int tris = 0;
                    for (int s = 0; s < mesh.subMeshCount; s++) tris += (int)(mesh.GetIndexCount(s) / 3);
                    total += tris;
                    if (tris > worst) { worst = tris; worstId = a.Id; }
                    Bounds b = mesh.bounds;
                    sb.AppendLine(string.Format("{0,-22} {1,5} tris  size {2:0.00} x {3:0.00} x {4:0.00}  minY {5:0.00}", a.Id, tris, b.size.x, b.size.y, b.size.z, b.min.y));
                }
                catch (Exception e)
                {
                    Debug.LogError("ImagineVR: asset " + a.Id + " failed to build: " + e);
                }
            }
            Debug.Log("ImagineVR asset check: " + Library.AssetList.Count + " assets, " + total + " triangles in total, heaviest " + worstId + " (" + worst + ")\n" + sb);
        }
    }

    /// <summary>Renders worlds to PNG files from the command line. See scripts/unity.ps1.</summary>
    public static class Snapshot
    {
        static string Arg(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
            return fallback;
        }

        /// <summary>
        /// -executeMethod ImagineVR.EditorTools.Snapshot.Run -snapScripts a.ndjson;b.ndjson -snapOut dir
        ///   [-snapTimes 2,7] [-snapYaws 0,120,240] [-snapPitch -4] [-snapWelcome 1] [-snapBare 1]
        /// </summary>
        public static void Run()
        {
            SessionState.SetBool("ImagineSnap.Active", true);
            SessionState.SetString("ImagineSnap.Scripts", Arg("-snapScripts", ""));
            SessionState.SetString("ImagineSnap.Out", Arg("-snapOut", "Snapshots"));
            SessionState.SetString("ImagineSnap.Times", Arg("-snapTimes", "7"));
            SessionState.SetString("ImagineSnap.Yaws", Arg("-snapYaws", "0"));
            SessionState.SetFloat("ImagineSnap.Pitch", float.Parse(Arg("-snapPitch", "-4"), System.Globalization.CultureInfo.InvariantCulture));
            SessionState.SetBool("ImagineSnap.Welcome", Arg("-snapWelcome", "0") == "1");
            SessionState.SetString("ImagineSnap.Sheet", Arg("-snapSheet", ""));
            SessionState.SetBool("ImagineSnap.Bare", Arg("-snapBare", "0") == "1");
            SessionState.SetBool("ImagineSnap.Probe", Arg("-snapProbe", "0") == "1");
            Setup.OpenMainScene();
            EditorApplication.EnterPlaymode();
        }
    }

    public static class Builder
    {
        /// <summary>A real Windows player for shader/collision snapshot checks without a headset.</summary>
        public static void BuildWindowsCheck()
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "ImagineVRCheck.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/ImagineVR/Scenes/Main.unity" },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            var summary = BuildPipeline.BuildPlayer(options).summary;
            Debug.Log("ImagineVR Windows check: " + summary.result + ", " + summary.totalErrors + " errors -> " + output);
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }
        [MenuItem("ImagineVR/Build Quest APK", priority = 40)]
        public static void BuildAndroid()
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "ImagineVR.apk"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            EnableQuestExtras();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/ImagineVR/Scenes/Main.unity" },
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
            UnityEditor.Build.Reporting.BuildSummary summary = report.summary;
            Debug.Log("ImagineVR build: " + summary.result + ", " + (summary.totalSize / (1024 * 1024)) + " MB, " +
                      summary.totalErrors + " errors, " + summary.totalWarnings + " warnings -> " + output);
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>
        /// A player for this Mac, to check a real build without the headset: it goes through the same
        /// shader stripping as the headset build, which the editor does not. Run what it makes with
        ///   Builds/ImagineVRCheck.app/Contents/MacOS/ImagineVR -batchmode -snapOut dir -snapScripts "a.ndjson;b.ndjson" -snapProbe 1
        /// (the same -snap arguments as the editor snapshot). Start Unity with -buildTarget StandaloneOSX.
        /// </summary>
        public static void BuildMacCheck()
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "ImagineVRCheck.app"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            // macOS will not package an app that uses the microphone without saying why
            if (string.IsNullOrEmpty(PlayerSettings.macOS.microphoneUsageDescription))
                PlayerSettings.macOS.microphoneUsageDescription = "ImagineVR listens while you hold the talk button.";
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/ImagineVR/Scenes/Main.unity" },
                locationPathName = output,
                target = BuildTarget.StandaloneOSX,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            UnityEditor.Build.Reporting.BuildSummary summary = BuildPipeline.BuildPlayer(options).summary;
            Debug.Log("ImagineVR check build: " + summary.result + ", " + summary.totalErrors + " errors -> " + output);
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>The 90 Hz and passthrough feature is ours, so nothing switches it on unless we do.</summary>
        static void EnableQuestExtras()
        {
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            OpenXRSettings openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            QuestFeature feature = openxr != null ? openxr.GetFeature<QuestFeature>() : null;
            if (feature == null) { Debug.LogWarning("ImagineVR build: Quest extras feature not found; building without 90 Hz and passthrough"); return; }
            if (!feature.enabled)
            {
                feature.enabled = true;
                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(openxr);
                AssetDatabase.SaveAssets();
            }
            Debug.Log("ImagineVR build: Quest extras enabled (" + QuestFeature.Extensions + ")");
        }
    }

    /// <summary>Tells the headset the app may show the real room behind itself (see QuestFeature).</summary>
    public sealed class QuestFeatureBuildHooks : OpenXRFeatureBuildHooks
    {
        public override int callbackOrder => 2;
        public override Type featureType => typeof(QuestFeature);

        protected override void OnPreprocessBuildExt(UnityEditor.Build.Reporting.BuildReport report) { }
        protected override void OnPostGenerateGradleAndroidProjectExt(string path) { }
        protected override void OnPostprocessBuildExt(UnityEditor.Build.Reporting.BuildReport report) { }

        protected override Unity.XR.Management.AndroidManifest.Editor.ManifestRequirement ProvideManifestRequirementExt()
        {
            return new Unity.XR.Management.AndroidManifest.Editor.ManifestRequirement
            {
                SupportedXRLoaders = new HashSet<Type> { typeof(OpenXRLoader) },
                NewElements = new List<Unity.XR.Management.AndroidManifest.Editor.ManifestElement>
                {
                    new Unity.XR.Management.AndroidManifest.Editor.ManifestElement
                    {
                        ElementPath = new List<string> { "manifest", "uses-feature" },
                        Attributes = new Dictionary<string, string> { { "name", "com.oculus.feature.PASSTHROUGH" }, { "required", "false" } },
                    },
                },
            };
        }
    }

    /// <summary>Plays an ImagineScript file into the running scene. Needs Play mode.</summary>
    public sealed class ReplayWindow : EditorWindow
    {
        string[] _files = new string[0];
        Vector2 _scroll;
        string _typed = "";

        [MenuItem("ImagineVR/Replay Script...", priority = 21)]
        static void Open()
        {
            GetWindow<ReplayWindow>("Replay Script").Refresh();
        }

        static string ExamplesDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "shared"));

        void Refresh()
        {
            _files = Directory.Exists(ExamplesDir) ? Directory.GetFiles(ExamplesDir, "*.ndjson") : new string[0];
        }

        void OnEnable() { Refresh(); }

        void OnGUI()
        {
            bool playing = Application.isPlaying && ImagineApp.Instance != null;
            if (!playing) EditorGUILayout.HelpBox("Press Play first. Scripts are played into the running world.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!playing))
            {
                EditorGUILayout.LabelField("Scripts in shared/", EditorStyles.boldLabel);
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                foreach (string file in _files)
                {
                    if (GUILayout.Button(Path.GetFileNameWithoutExtension(file)))
                    {
                        ImagineApp.Instance.Conductor.SubmitScript(File.ReadAllText(file), Path.GetFileNameWithoutExtension(file).Replace('-', ' '));
                    }
                }
                EditorGUILayout.EndScrollView();
                EditorGUILayout.Space();
                if (GUILayout.Button("Choose a file..."))
                {
                    string picked = EditorUtility.OpenFilePanel("ImagineScript file", ExamplesDir, "ndjson,txt");
                    if (!string.IsNullOrEmpty(picked)) ImagineApp.Instance.Conductor.SubmitScript(File.ReadAllText(picked), Path.GetFileNameWithoutExtension(picked));
                }
                EditorGUILayout.LabelField("Or paste statements, one per line", EditorStyles.boldLabel);
                _typed = EditorGUILayout.TextArea(_typed, GUILayout.MinHeight(80));
                if (GUILayout.Button("Play pasted statements") && _typed.Trim().Length > 0) ImagineApp.Instance.Conductor.SubmitScript(_typed, "pasted script");
                if (GUILayout.Button("Back to the dark room")) ImagineApp.Instance.BackToDark();
            }
            if (GUILayout.Button("Refresh list")) Refresh();
        }
    }
}
