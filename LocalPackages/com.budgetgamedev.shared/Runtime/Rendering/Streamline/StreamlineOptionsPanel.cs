using UnityEngine;
using UnityEngine.InputSystem;

namespace BudgetGameDev.Shared.Rendering
{
    /// <summary>Reusable native rendering controls. Hosts may also bind StreamlineSettings to their own menu.</summary>
    public sealed class StreamlineOptionsPanel : MonoBehaviour
    {
        internal static bool Visible { get; private set; }
        private Rect window = new Rect(24, 24, 380, 230);

        private void Update()
        {
            if (Keyboard.current?.f10Key.wasPressedThisFrame == true)
                SetVisible(!Visible);
        }

        public void SetVisible(bool visible)
        {
            Visible = visible;
            // IMGUI is composed outside HDRP's overlay buffer; do not interpolate
            // this settings window. Gameplay uses the regular HDRP UI alpha input.
            GetComponent<StreamlineRuntime>()?.ApplyOptions();
        }

        private void OnGUI()
        {
            if (Visible)
                window = GUILayout.Window(
                    GetEntityId().GetHashCode(),
                    window,
                    DrawWindow,
                    "NVIDIA Rendering"
                );
        }

        private void DrawWindow(int id)
        {
            StreamlineNative.TryGetStatus(out var status);
            GUILayout.Label("DLSS Super Resolution default: Quality / Preset K");
            GUILayout.Space(8);
            GUILayout.Label("DLSS Frame Generation");
            GUI.enabled = status.frameGenerationAvailable != 0 && status.swapchainHooked != 0;
            GUILayout.BeginHorizontal();
            if (
                GUILayout.Toggle(StreamlineSettings.GeneratedFrames == 0, "Off", "Button")
                && StreamlineSettings.GeneratedFrames != 0
            )
                StreamlineSettings.GeneratedFrames = 0;
            for (int frames = 1; frames <= 3; frames++)
            {
                bool available = GUI.enabled;
                GUI.enabled = available && status.maxGeneratedFrames >= frames;
                if (
                    GUILayout.Toggle(
                        StreamlineSettings.GeneratedFrames == frames,
                        $"{frames + 1}x",
                        "Button"
                    )
                    && StreamlineSettings.GeneratedFrames != frames
                )
                    StreamlineSettings.GeneratedFrames = frames;
                GUI.enabled = available;
            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            if (status.frameGenerationAvailable == 0)
                GUILayout.Label("Frame Generation unavailable on this system.");
            GUILayout.Label("NVIDIA Reflex Low Latency");
            GUI.enabled = status.reflexAvailable != 0;
            int selected = GUILayout.SelectionGrid(
                (int)StreamlineSettings.EffectiveReflex,
                new[] { "Off", "On", "On + Boost" },
                3
            );
            if (selected != (int)StreamlineSettings.EffectiveReflex)
            {
                if (selected == 0)
                    StreamlineSettings.GeneratedFrames = 0;
                StreamlineSettings.Reflex = (StreamlineSettings.ReflexMode)selected;
            }
            GUI.enabled = true;
            GUILayout.Space(8);
            if (GUILayout.Button("Restore defaults"))
                StreamlineSettings.ResetDefaults();
            if (GUILayout.Button("Close (F10)"))
                SetVisible(false);
            GUI.DragWindow();
        }

        private void OnDestroy() => Visible = false;
    }
}
