using System;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace BudgetGameDev.Shared.Rendering
{
    internal static class StreamlineDiagnosticsReport
    {
        internal static bool Fresh(ulong now, ulong tick) =>
            tick != 0 && now >= tick && now - tick <= 1500;

        internal static string FrameGenerationState(
            StreamlineNative.Status s,
            StreamlineNative.Diagnostics d
        )
        {
            if (s.initialized == 0)
                return "STREAMLINE INITIALIZATION FAILED";
            if (s.frameGenerationAvailable == 0)
                return "FRAME GENERATION UNSUPPORTED";
            if (s.swapchainHooked == 0)
                return "SWAPCHAIN NOT ATTACHED";
            if (!Fresh(d.snapshotTick, d.presentTick))
                return "NO RECENT SUCCESSFUL PRESENT";
            if (d.fgStateResult != 0 || s.frameGenerationStatus != 0)
                return "FRAME GENERATION ERROR";
            if (s.generatedFrames == 0)
                return "FRAME GENERATION OFF / SUSPENDED";
            if (d.tagMask != 7)
                return "INCOMPLETE PIPELINE INPUTS";
            return Fresh(d.snapshotTick, d.generatedTick)
                ? "EXTRA PRESENTS OBSERVED BY STREAMLINE"
                : "FG CONFIGURED; EXTRA PRESENTS NOT OBSERVED";
        }

        internal static string Mode(uint value) =>
            value == 0 ? "Off"
            : value == 1 ? "On"
            : value == 2 ? "On + Boost"
            : "Not reported";

        internal static string Multiplier(uint value) =>
            value == 0 ? "Off" : $"{value + 1}x ({value} generated per rendered frame)";

        private static string Yes(uint value) => value != 0 ? "Yes" : "No";

        private static string Age(ulong now, ulong then) =>
            then == 0 || now < then ? "never" : $"{now - then} ms ago";

        internal static string Build(
            NvidiaRendering.Snapshot requested,
            StreamlineNative.Status s,
            StreamlineNative.Diagnostics d,
            bool native,
            bool telemetry,
            string sr,
            string log
        )
        {
            var text = new StringBuilder();
            text.AppendLine($"LIVE DIAGNOSTICS • {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            text.AppendLine(
                "Refreshes 4 times/second. Scroll: wheel / right stick / PgUp-Dn. Copy includes all diagnostics and recent native logs."
            );
            text.AppendLine($"\n{requested.Summary}\n");
            text.AppendLine("DLSS SUPER RESOLUTION");
            text.AppendLine(
                $"Requested: {(requested.DlssRequested ? "On • Quality • Preset K" : "Off")}"
            );
            text.AppendLine(
                $"Pipeline: {GraphicsSettings.currentRenderPipeline?.name ?? "none"}; backend: NVIDIA Streamline"
            );
            text.AppendLine(sr);
            text.AppendLine(
                $"\nFRAME GENERATION\nRequested: {Multiplier((uint)requested.GeneratedFrames)}"
            );
            text.AppendLine(
                $"Main view: {StreamlineRuntime.ViewCamera?.name ?? "none eligible"}; capture enabled: {StreamlineRuntime.CaptureEnabled}"
            );
            text.AppendLine(
                "Only one fullscreen perspective output camera is supported. Orthographic menus can suspend FG."
            );
            if (native)
            {
                text.AppendLine(
                    $"slInit succeeded: {Yes(s.initialized)}; FG available: {Yes(s.frameGenerationAvailable)}; proxy attached: {Yes(s.swapchainHooked)}"
                );
                text.AppendLine(
                    $"Accepted option: {Multiplier(s.generatedFrames)}; device maximum: {(s.maxGeneratedFrames == 0 ? "not reported" : (s.maxGeneratedFrames + 1) + "x")}"
                );
                text.AppendLine(
                    $"Feature support: {Result(s.featureSupportResult)}; requirements: {Result(s.requirementsResult)}"
                );
                text.AppendLine(
                    "Support checks cover the actual adapter, driver, OS and HAGS; individual OS/HAGS state is not exposed separately."
                );
                text.AppendLine(
                    $"DLSS-G status: {FgStatus(s.frameGenerationStatus)}; last bridge error (sticky): {Result(s.lastError)}"
                );
            }
            else
                text.AppendLine(
                    "Native bridge unavailable. No accepted configuration or execution evidence."
                );
            if (telemetry)
            {
                text.AppendLine(
                    $"DLSS-G state query: {Result(d.fgStateResult)}; last SDK count: {d.actualPresentedLast}"
                );
                text.AppendLine(
                    $"Total frames reported by SDK: {d.slPresentedFrames}; state samples: {d.slStateSamples}"
                );
                text.AppendLine(
                    $"Successful real Presents: {d.presentedFrames}; latest: {Age(d.snapshotTick, d.presentTick)}; HRESULT: 0x{d.presentResult:X8}"
                );
                text.AppendLine(
                    $"Extra-present evidence last seen: {Age(d.snapshotTick, d.generatedTick)}"
                );
                text.AppendLine(
                    "SDK counts include real + generated presents. They do not measure monitor scan-out or image quality."
                );
                text.AppendLine(
                    $"\nFRAME INPUTS & SYNCHRONIZATION\nLast presented input mask: 0x{d.tagMask:X} / 0x7 required"
                );
                text.AppendLine(
                    $"Depth + motion + constants: {((d.tagMask & 1) != 0)}; HUD-less color: {((d.tagMask & 2) != 0)}; UI alpha: {((d.tagMask & 4) != 0)}"
                );
                text.AppendLine(
                    $"Render: {d.renderWidth} x {d.renderHeight}; output: {d.outputWidth} x {d.outputHeight}; complete tagged frames: {d.completeTags}"
                );
                text.AppendLine(
                    $"Latest token IDs — simulation: {d.simulationId}; submission: {d.submissionId}; Present: {d.presentId}"
                );
                text.AppendLine(
                    "IDs can differ while frames are in flight. Present markers and FG use the same submitted token."
                );
                text.AppendLine(
                    $"Simulation frames: {d.simulatedFrames}; successful PCL markers: {d.markers}; latest marker result: {Result(d.markerResult)}"
                );
            }
            text.AppendLine(
                $"\nREFLEX LOW LATENCY\nRequested: {Mode((uint)requested.Reflex)}; effective request: {StreamlineSettings.EffectiveReflex}"
            );
            if (telemetry)
            {
                text.AppendLine(
                    $"Available: {Yes(s.reflexAvailable)}; accepted mode: {Mode(d.activeReflex)}; PCL window bound: {Yes(d.pclWindowBound)}"
                );
                text.AppendLine(
                    $"Sleep calls: {d.sleepCalls}; successful: {d.sleepSuccesses}; latest: {Result(d.sleepResult)}"
                );
                text.AppendLine(
                    $"State query: {Result(d.reflexStateResult)}; valid latency data: {Yes(d.latencyValid)}"
                );
                bool fresh = d.latencyValid != 0 && Fresh(d.snapshotTick, d.reportTick);
                text.AppendLine(
                    $"Timing evidence: {(fresh ? "FRESH DRIVER REPORTS" : "NOT OBSERVED / STALE")}; latest: {Age(d.snapshotTick, d.reportTick)}"
                );
                text.AppendLine(
                    $"Report frame: {d.reflexReportFrame}; distinct report updates: {d.reflexReportUpdates}"
                );
                if (d.latencyValid != 0)
                    text.AppendLine(
                        $"{(fresh ? "Current" : "Last known")} latency — PC: {d.pcLatencyUs / 1000f:0.00} ms; simulation: {d.simulationLatencyUs / 1000f:0.00} ms; submission: {d.renderLatencyUs / 1000f:0.00} ms; GPU: {d.gpuLatencyUs / 1000f:0.00} ms"
                    );
                text.AppendLine(
                    "PC latency is simulation start to GPU render end; it is not click-to-photon latency. Reports do not prove a reduction versus Reflex Off."
                );
            }
            else
                text.AppendLine(
                    "Timing, sleep and marker telemetry unavailable. No working-state claim."
                );
            text.AppendLine(
                $"\nENVIRONMENT\nPlatform: {Application.platform}; Unity: {Application.unityVersion}; Streamline SDK: 2.12.0"
            );
            text.AppendLine(
                $"Graphics API: {SystemInfo.graphicsDeviceType}; GPU: {SystemInfo.graphicsDeviceName}"
            );
            text.AppendLine(
                $"Vendor: {SystemInfo.graphicsDeviceVendor} (0x{SystemInfo.graphicsDeviceVendorID:X}); device: 0x{SystemInfo.graphicsDeviceID:X}"
            );
            text.AppendLine(
                $"Driver/API: {SystemInfo.graphicsDeviceVersion}; VRAM: {SystemInfo.graphicsMemorySize} MB"
            );
            text.AppendLine($"Output: {Screen.width} x {Screen.height}; {DisplayState()}");
            text.AppendLine(
                $"Focus: {Application.isFocused}; F10 overlay visible: {StreamlineOptionsPanel.Visible}; time scale: {Time.timeScale}"
            );
            text.AppendLine(
                $"\nNATIVE LOG • last 64 messages • warnings/errors: {s.integrationWarnings}"
            );
            text.AppendLine(
                "Full files: %LOCALAPPDATA%\\BudgetGameDev\\Streamline\\<executable-name>"
            );
            text.AppendLine(string.IsNullOrEmpty(log) ? "No native messages recorded." : log);
            return text.ToString();
        }

        private static string DisplayState()
        {
            try
            {
                var output = HDROutputSettings.main;
                if (output != null && output.available)
                    return $"HDR active: {output.active}; format: {output.graphicsFormat}";
            }
            catch (InvalidOperationException)
            {
                // A display disconnect / HDR toggle can invalidate the availability check.
            }
            return "HDR active: False; format: SDR / unavailable";
        }

        internal static string FgStatus(uint flags)
        {
            if (flags == 0)
                return "0x0 (OK when queried)";
            var reasons = new StringBuilder($"0x{flags:X}");
            if ((flags & 1) != 0)
                reasons.Append("; output resolution too low");
            if ((flags & 2) != 0)
                reasons.Append("; Reflex not detected");
            if ((flags & 4) != 0)
                reasons.Append("; HDR format unsupported");
            if ((flags & 8) != 0)
                reasons.Append("; invalid camera constants");
            if ((flags & 16) != 0)
                reasons.Append("; GetCurrentBackBufferIndex missing");
            if ((flags & ~31u) != 0)
                reasons.Append("; unknown/reserved status bits");
            return reasons.ToString();
        }

        private static readonly string[] Results =
        {
            "OK",
            "IO error",
            "Driver out of date",
            "OS out of date",
            "HAGS disabled",
            "Device not created",
            "No supported adapter",
            "Adapter unsupported",
            "No plugins",
            "Vulkan error",
            "DXGI error",
            "D3D error",
            "NRD error",
            "NVAPI error",
            "Reflex error",
            "NGX failed",
            "JSON error",
            "Missing proxy",
            "Missing resource state",
            "Invalid integration",
            "Missing input",
            "Not initialized",
            "Compute failed",
            "slInit not called",
            "Exception handler",
            "Invalid parameter",
            "Missing constants",
            "Duplicated constants",
            "Missing/invalid API",
            "Common constants missing",
            "Unsupported interface",
            "Feature missing",
            "Feature unsupported",
            "Feature hooks missing",
            "Feature load failed",
            "Feature priority error",
            "Feature dependency missing",
            "Feature manager state error",
            "Invalid state",
            "Out of VRAM warning",
        };

        internal static string Result(uint code) =>
            code == uint.MaxValue
                ? "Not queried"
                : $"{code} ({(code < Results.Length ? Results[code] : "Unknown result")})";
    }
}
