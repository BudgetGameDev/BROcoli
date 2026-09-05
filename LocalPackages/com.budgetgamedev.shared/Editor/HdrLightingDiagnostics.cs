using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BudgetGameDev.Shared.Editor
{
    /// <summary>Reads the live rendering state without interpreting an SDR screenshot as HDR.</summary>
    public static class HdrLightingDiagnostics
    {
        [MenuItem("Tools/BROcoli/Rendering/Log HDR Lighting Diagnostics")]
        public static void LogReport()
        {
            string report = BuildReport();
            string path = Path.GetFullPath("Temp/BrocoliHdrLighting.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, report);
            Debug.Log(report + "\nSaved: " + path);
        }

        public static string BuildReport()
        {
            var output = HDROutputSettings.main;
            var report = new StringBuilder();
            report.AppendLine(
                "BROcoli live HDR lighting diagnostics (" + DateTime.UtcNow.ToString("O") + ")"
            );
            report.AppendLine("Pipeline: " + GraphicsSettings.currentRenderPipeline?.name);
            report.AppendLine(
                "GPU: " + SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType
            );
            report.AppendLine($"Native HDR available={output.available}, active={output.active}");
            // Unity throws for display metadata in a player with no HDR display attached.
            if (output.available)
            {
                report.AppendLine(
                    $"Output format={output.graphicsFormat}, gamut={output.displayColorGamut}"
                );
                report.AppendLine(
                    $"Display reports min={output.minToneMapLuminance}, peak={output.maxToneMapLuminance}, fullFrame={output.maxFullFrameToneMapLuminance}, paperWhite={output.paperWhiteNits} nits"
                );
            }
            report.AppendLine(
                $"Calibration peak={GameDisplaySettings.PeakBrightnessNits}, paperWhite={GameDisplaySettings.PaperWhiteNits}, black={GameDisplaySettings.BlackLevelNits} nits; preset={GameDisplaySettings.HdrToneMapPreset}"
            );
            report.AppendLine(
                $"Requested compact highlight={GameDisplaySettings.PeakBrightnessNits * GameDisplaySettings.HighlightOvershoot} nits before display clipping; contrast={GameDisplaySettings.HdrContrastLift}, saturation={GameDisplaySettings.HdrSaturationLift}"
            );
            if (!output.active)
                report.AppendLine(
                    "Native HDR is inactive. Values above paper white in an SDR debug view do not validate display nits or 10-bit output."
                );

            foreach (Camera camera in Camera.allCameras)
            {
                report.AppendLine(
                    $"Camera {camera.name}: HDR rendering={camera.allowHDR}, target={(camera.targetTexture == null ? "display" : camera.targetTexture.graphicsFormat.ToString())}"
                );
                var hdType = Type.GetType(
                    "UnityEngine.Rendering.HighDefinition.HDCamera, Unity.RenderPipelines.HighDefinition.Runtime"
                );
                var cached =
                    hdType
                        ?.GetMethod("GetHDCameras", BindingFlags.Static | BindingFlags.NonPublic)
                        ?.Invoke(null, null) as System.Collections.IEnumerable;
                object hdCamera = null;
                if (cached != null)
                    foreach (object entry in cached)
                        if (hdType.GetField("camera").GetValue(entry) as Camera == camera)
                            hdCamera = entry;
                if (hdCamera != null)
                {
                    report.AppendLine("Resolved HDRP camera stack:");
                    AppendStack(
                        report,
                        hdType.GetProperty("volumeStack").GetValue(hdCamera) as VolumeStack
                    );
                    object frame = hdType.GetProperty("frameSettings").GetValue(hdCamera);
                    MethodInfo enabled = frame.GetType().GetMethod("IsEnabled");
                    Type fieldType = enabled.GetParameters()[0].ParameterType;
                    foreach (
                        string field in new[]
                        {
                            "Postprocess",
                            "CustomPostProcess",
                            "ExposureControl",
                        }
                    )
                        report.AppendLine(
                            $"  Camera frame setting {field}={enabled.Invoke(frame, new[] { Enum.Parse(fieldType, field) })}"
                        );
                }
                else
                {
                    report.AppendLine(
                        "VolumeManager current stack (last rendered camera; may be Scene view):"
                    );
                    AppendStack(report, VolumeManager.instance.stack);
                }
            }
            return report.ToString();
        }

        private static void AppendStack(StringBuilder report, VolumeStack stack)
        {
            // Each pipeline owns its volume types; inspect them without adding a pipeline
            // dependency to the shared editor assembly. These are the resolved stack values.
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (
                    string name in new[]
                    {
                        "Exposure",
                        "Tonemapping",
                        "ColorAdjustments",
                        "Bloom",
                        "ImpressionistBloom",
                    }
                )
                {
                    Type type =
                        assembly.GetType("UnityEngine.Rendering.HighDefinition." + name)
                        ?? assembly.GetType("UnityEngine.Rendering.Universal." + name)
                        ?? assembly.GetType(
                            "BudgetGameDev.Shared.Rendering.HighDefinition." + name
                        );
                    if (type == null || !typeof(VolumeComponent).IsAssignableFrom(type))
                        continue;
                    VolumeComponent component = stack?.GetComponent(type);
                    if (component == null)
                        continue;
                    report.AppendLine(type.FullName + " active=" + component.active);
                    if (component is IPostProcessComponent effect)
                        report.AppendLine("  IsActive=" + effect.IsActive());
                    foreach (
                        FieldInfo field in type.GetFields(
                            BindingFlags.Public | BindingFlags.Instance
                        )
                    )
                    {
                        if (field.GetValue(component) is not VolumeParameter parameter)
                            continue;
                        object value = parameter
                            .GetType()
                            .GetProperty("value")
                            ?.GetValue(parameter);
                        report.AppendLine(
                            $"  {field.Name}={value} (override={parameter.overrideState})"
                        );
                    }
                }
            }
        }

        [MenuItem("Tools/BROcoli/Rendering/Show Values Above Paper White")]
        public static void ShowValuesAbovePaperWhite()
        {
            EditorApplication.ExecuteMenuItem("Window/Analysis/Rendering Debugger");
            foreach (var panel in DebugManager.instance.panels)
                SetHdrDebug(panel, "ValuesAbovePaperWhite");
            LogReport();
        }

        [MenuItem("Tools/BROcoli/Rendering/Hide HDR Debug Overlay")]
        public static void HideHdrDebugOverlay()
        {
            foreach (var panel in DebugManager.instance.panels)
                SetHdrDebug(panel, "None");
        }

        private static void SetHdrDebug(DebugUI.IContainer container, string mode)
        {
            foreach (DebugUI.Widget widget in container.children)
            {
                // URP labels this "HDR Debug Mode"; HDRP labels it "DebugMode" inside
                // Rendering / HDR Output. Match the distinctive enum, not its UI caption.
                if (
                    widget is DebugUI.EnumField field
                    && field.enumNames != null
                    && Array.Exists(
                        field.enumNames,
                        name => name.text.Replace(" ", "") == "ValuesAbovePaperWhite"
                    )
                )
                {
                    int index = Array.FindIndex(
                        field.enumNames,
                        name =>
                            name.text.Replace(" ", "")
                                .Equals(mode, StringComparison.OrdinalIgnoreCase)
                    );
                    if (index >= 0)
                        field.SetValue(field.enumValues[index]);
                }
                if (widget is DebugUI.IContainer child)
                    SetHdrDebug(child, mode);
            }
        }
    }
}
