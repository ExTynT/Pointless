using System;
using System.IO;
using Pointless.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Pointless.Editor
{
    public static class Setup
    {
        const string ScenePath = "Assets/Game/Game.unity";
        const string MaterialPath = "Assets/Game/View/Shapes.mat";
        const string PipelinePath = "Assets/Settings/Mobile_RPAsset.asset";
        const string ApkPath = "Builds/Pointless.apk";
        const string IconPath = "Assets/Game/Icon.png";
        const string AudioPath = "ProjectSettings/AudioManager.asset";
        const int IconSize = 512;
        const int LowLatencyBuffer = 256;

        static readonly string[] leftovers =
        {
            "Assets/Settings/PC_RPAsset.asset",
            "Assets/Settings/PC_Renderer.asset",
            "Assets/Settings/SampleSceneProfile.asset"
        };

        [MenuItem("Pointless/Set Up Project")]
        public static void Run()
        {
            Player();
            Icon();
            Pipeline();
            Scene();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Pointless/Build Android")]
        public static void Build()
        {
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Android build {report.summary.result}: {report.summary.totalErrors} errors");
            Debug.Log($"Android build {report.summary.totalSize / 1024} KB at {report.summary.outputPath}");
        }

        static void Player()
        {
            PlayerSettings.companyName = "ExTynT";
            PlayerSettings.productName = "Pointless";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.extynt.pointless");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.accelerometerFrequency = 0;
            PlayerSettings.gcIncremental = true;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.High);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;

            var audio = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(AudioPath)[0]);
            Set(audio, "m_DSPBufferSize", LowLatencyBuffer);
            audio.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Icon()
        {
            float side = IconSize * 0.3f, rise = side * 0.8660254f, half = side * 0.435f;
            float middle = IconSize * 0.5f, apex = middle + rise;
            var pixels = new Color32[IconSize * IconSize];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Theme.Back;

            for (int y = 0; y < 2; y++)
                for (int x = -y; x <= y; x++)
                {
                    bool up = Lattice.IsUp(x, y);
                    int tint = Lattice.Index(x, y);
                    float centerX = middle + x * side * 0.5f;
                    float centerY = apex - (y + (up ? 2f / 3f : 1f / 3f)) * rise;
                    for (int row = 0; row < IconSize; row++)
                        for (int column = 0; column < IconSize; column++)
                        {
                            float localX = (column + 0.5f - centerX) / half, localY = (row + 0.5f - centerY) / half;
                            float cover = Mathf.Clamp01(0.5f - Edge(localX, up ? localY : -localY, 0.2f) * half);
                            if (cover <= 0f) continue;
                            Color32 shade = Theme.Mix(Theme.Low[tint], Theme.Top[tint], Mathf.Clamp01(localY * 0.5f + 0.5f));
                            pixels[row * IconSize + column] = Theme.Mix(pixels[row * IconSize + column], shade, cover);
                        }
                }

            var image = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            image.SetPixels32(pixels);
            File.WriteAllBytes(IconPath, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(IconPath);
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        }

        static float Edge(float x, float y, float corner)
        {
            const float root = 1.7320508f;
            float reach = 1f - corner * root;
            x = Mathf.Abs(x) - reach;
            y += reach / root;
            if (x + root * y > 0f) (x, y) = ((x - root * y) * 0.5f, (-root * x - y) * 0.5f);
            x -= Mathf.Clamp(x, -2f * reach, 0f);
            return -Mathf.Sqrt(x * x + y * y) * Mathf.Sign(y) - corner;
        }

        static void Pipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = 1;
            pipeline.renderScale = 1f;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            pipeline.shadowDistance = 0f;

            var fields = new SerializedObject(pipeline);
            Set(fields, "m_MainLightRenderingMode", 0);
            Set(fields, "m_MainLightShadowsSupported", false);
            Set(fields, "m_AdditionalLightsRenderingMode", 0);
            Set(fields, "m_AdditionalLightShadowsSupported", false);
            Set(fields, "m_SupportsLightCookies", false);
            Set(fields, "m_ReflectionProbeBlending", false);
            Set(fields, "m_ReflectionProbeBoxProjection", false);
            fields.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int level = 0; level < QualitySettings.names.Length; level++)
            {
                QualitySettings.SetQualityLevel(level, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);

            foreach (string path in leftovers)
                if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
        }

        static void Scene()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Pointless/Shapes"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var eye = new GameObject("Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            eye.transform.position = new Vector3(0f, 0f, -10f);
            var camera = eye.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9f;
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 20f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Theme.Back;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;
            UniversalAdditionalCameraData extra = camera.GetUniversalAdditionalCameraData();
            extra.renderPostProcessing = false;
            extra.renderShadows = false;
            extra.requiresDepthTexture = false;
            extra.requiresColorTexture = false;
            extra.antialiasing = AntialiasingMode.None;
            extra.dithering = false;
            extra.stopNaN = false;

            var board = new GameObject("Game", typeof(MeshFilter), typeof(MeshRenderer), typeof(GameView));
            var renderer = board.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;

            var fields = new SerializedObject(board.GetComponent<GameView>());
            fields.FindProperty("view").objectReferenceValue = camera;
            fields.FindProperty("surface").objectReferenceValue = board.GetComponent<MeshFilter>();
            fields.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void Set(SerializedObject fields, string name, int value)
        {
            SerializedProperty field = fields.FindProperty(name);
            if (field != null) field.intValue = value;
        }

        static void Set(SerializedObject fields, string name, bool value)
        {
            SerializedProperty field = fields.FindProperty(name);
            if (field != null) field.boolValue = value;
        }
    }
}
