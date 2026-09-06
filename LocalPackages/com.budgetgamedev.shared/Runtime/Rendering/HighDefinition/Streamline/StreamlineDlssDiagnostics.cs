using System.Text;

namespace BudgetGameDev.Shared.Rendering.HighDefinition
{
    internal sealed class StreamlineDlssDiagnostics
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private UnityEngine.NVIDIA.GraphicsDeviceDebugView view;
        private UnityEngine.NVIDIA.GraphicsDevice owner;
#endif

        internal string Read(out bool supported)
        {
            supported = false;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var device = UnityEngine.NVIDIA.GraphicsDevice.device;
            if (device == null)
                return "NVIDIA module: no graphics device. SR execution not observed.";
            supported = device.IsFeatureAvailable(UnityEngine.NVIDIA.GraphicsDeviceFeature.DLSS);
            if (owner != device)
            {
                Release();
                owner = device;
            }
            if (view == null)
                view = device.CreateDebugView();
            if (view == null)
                return "NVIDIA debug view unavailable; SR execution cannot be verified.";
            device.UpdateDebugView(view);
            var text = new StringBuilder();
            text.AppendLine(
                $"NVIDIA module: 0x{view.deviceVersion:X}; NGX: {(view.ngxVersion >> 18) & 0x3FF}.{(view.ngxVersion >> 7) & 0x7F}.{view.ngxVersion & 0x7F}"
            );
            text.AppendLine($"DLSS supported: {supported}");
            int count = 0;
            foreach (var feature in view.dlssFeatureInfosSpan)
            {
                if (!feature.validFeature)
                    continue;
                ++count;
                text.AppendLine($"Feature slot {feature.featureSlot}: VALID NATIVE FEATURE");
                text.AppendLine(
                    $"  Reported input: {feature.execData.subrectWidth} x {feature.execData.subrectHeight}; output: {feature.initData.outputRTWidth} x {feature.initData.outputRTHeight}"
                );
                text.AppendLine(
                    $"  Init quality: {feature.initData.quality}; Quality preset: {feature.initData.presetQualityMode}"
                );
            }
            if (count == 0)
                text.AppendLine("No valid native DLSS feature; SR execution NOT OBSERVED.");
            text.Append(
                "Feature validity and execution parameters are native SDK evidence, not a GPU completion counter. NVIDIA App overrides are not exposed here."
            );
            return text.ToString();
#else
            return "Native DLSS execution data is available in the Windows player only.";
#endif
        }

        internal void Release()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (owner != null && view != null && UnityEngine.NVIDIA.GraphicsDevice.device == owner)
                owner.DeleteDebugView(view);
            view = null;
            owner = null;
#endif
        }
    }
}
