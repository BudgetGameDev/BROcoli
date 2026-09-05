using System;
using System.Linq;
using System.Reflection;
using BudgetGameDev.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExercisePauseHdrInput(ResponsivePauseMenuLayout layout)
        {
            float now = Time.unscaledTime;
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.up });
                InputSystem.Update();
                layout.HandleSettingsInput(null, gamepad);
            }
            finally
            {
                InputSystem.RemoveDevice(gamepad);
            }

            SetHierarchyField(layout, "lastSettingsNavTime", -10f);
            layout.ProcessPauseSettingsInput(false, 1f, 0f, false, now);
            SetHierarchyField(layout, "lastSettingsNavTime", -10f);
            SetHierarchyField(layout, "selectedSetting", 0);
            layout.ProcessPauseSettingsInput(false, 0f, 1f, false, now + 1f);

            Selectable[] settings = GetHierarchyField<Selectable[]>(layout, "settingsSelectables");
            Button hdrToggle = GetHierarchyField<Button>(layout, "hdrToggleButton");
            int toggleIndex = System.Array.IndexOf(settings, hdrToggle);
            if (toggleIndex >= 0)
            {
                SetHierarchyField(layout, "selectedSetting", toggleIndex);
                SetHierarchyField(layout, "lastSettingsNavTime", -10f);
                layout.ProcessPauseSettingsInput(false, 0f, 1f, false, now + 2f);
            }
            int settingsButton = System.Array.FindIndex(settings, item => item is Button);
            if (settingsButton >= 0)
            {
                SetHierarchyField(layout, "selectedSetting", settingsButton);
                ResetMenuInputGate();
                layout.ProcessPauseSettingsInput(false, 0f, 0f, true, now + 3f);
            }
            if (!layout.SettingsOpen)
                InvokeHierarchy(layout, "ShowSettings", (System.Action)null);

            InvokeHierarchy(layout, "ShowHdrDetails");
            Gamepad detailsGamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                InputSystem.QueueStateEvent(
                    detailsGamepad,
                    new GamepadState { leftStick = Vector2.right }
                );
                InputSystem.Update();
                layout.HandleSettingsInput(null, detailsGamepad);
            }
            finally
            {
                InputSystem.RemoveDevice(detailsGamepad);
            }
            SetHierarchyField(layout, "lastHdrDetailNavTime", -10f);
            layout.ProcessPauseHdrDetailsInput(false, 1f, 1f, false, now + 4f);
            Selectable[] details = GetHierarchyField<Selectable[]>(layout, "hdrDetailSelectables");
            int detailButton = System.Array.FindIndex(
                details,
                item => item is Button button && button.interactable
            );
            if (detailButton >= 0)
            {
                SetHierarchyField(layout, "selectedHdrDetail", detailButton);
                ResetMenuInputGate();
                layout.ProcessPauseHdrDetailsInput(false, 0f, 0f, true, now + 5f);
            }
            if (!layout.HdrDetailsOpen)
                InvokeHierarchy(layout, "ShowHdrDetails");
            ResetMenuInputGate();
            layout.ProcessPauseHdrDetailsInput(true, 0f, 0f, false, now + 6f);

            if (!layout.HdrDetailsOpen)
                InvokeHierarchy(layout, "ShowHdrDetails");
            InvokeHierarchy(layout, "OpenHdrCalibration");
            Gamepad calibrationGamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                InputSystem.QueueStateEvent(
                    calibrationGamepad,
                    new GamepadState().WithButton(GamepadButton.DpadDown)
                );
                InputSystem.Update();
                layout.HandleSettingsInput(null, calibrationGamepad);
            }
            finally
            {
                InputSystem.RemoveDevice(calibrationGamepad);
            }
            SetHierarchyField(layout, "lastHdrCalibrationNavTime", -10f);
            layout.ProcessPauseHdrCalibrationInput(false, 1f, 0f, false, now + 7f);
            Slider slider = GetHierarchyField<Slider>(layout, "hdrCalibrationSlider");
            Selectable[] calibration = GetHierarchyField<Selectable[]>(
                layout,
                "hdrCalibrationSelectables"
            );
            int sliderIndex = System.Array.IndexOf(calibration, slider);
            SetHierarchyField(layout, "selectedHdrCalibrationControl", sliderIndex);
            SetHierarchyField(layout, "lastHdrCalibrationNavTime", -10f);
            layout.ProcessPauseHdrCalibrationInput(false, 0f, 1f, false, now + 8f);
            ResetMenuInputGate();
            layout.ProcessPauseHdrCalibrationInput(false, 0f, 0f, true, now + 9f);
            SetHierarchyField(layout, "selectedHdrCalibrationControl", sliderIndex);
            SetHierarchyField(layout, "lastHdrCalibrationNavTime", -10f);
            layout.ProcessPauseHdrCalibrationInput(false, 0f, -1f, false, now + 10f);

            int calibrationButton = System.Array.FindIndex(calibration, item => item is Button);
            SetHierarchyField(layout, "selectedHdrCalibrationControl", calibrationButton);
            ResetMenuInputGate();
            layout.ProcessPauseHdrCalibrationInput(false, 0f, 0f, true, now + 11f);
            if (!layout.HdrCalibrationOpen)
                InvokeHierarchy(layout, "OpenHdrCalibration");
            ResetMenuInputGate();
            layout.ProcessPauseHdrCalibrationInput(true, 0f, 0f, false, now + 12f);

            if (!layout.SettingsOpen)
                InvokeHierarchy(layout, "ShowSettings", (Action)null);
            ResetMenuInputGate();
            layout.ProcessPauseSettingsInput(true, 0f, 0f, false, now + 13f);

            InvokeHierarchy(layout, "ShowSettings", (Action)null);
            InvokeHierarchy(layout, "ShowHdrDetails");
            MethodInfo nativeCalibration = typeof(GameDisplaySettings)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Single(method =>
                    method.Name == "TryUseNativeDisplayCalibration"
                    && method.GetParameters().Length == 7
                );
            nativeCalibration.Invoke(
                null,
                new object[] { true, true, false, 1000f, 220f, 0.002f, 400f }
            );
            object metadata = Activator.CreateInstance(
                typeof(DisplayEdidMetadata),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[]
                {
                    "PAUSE COVERAGE HDR",
                    true,
                    true,
                    0.002f,
                    true,
                    1100f,
                    true,
                    400f,
                    "coverage",
                },
                null
            );
            SetHierarchyField(layout, "hdrEdidMetadata", metadata);
            InvokeHierarchy(layout, "UseHdrEdid");
            Assert.That(
                (string)InvokeHierarchy(layout, "ResolvePauseHdrSource"),
                Is.EqualTo("EDID METADATA")
            );

            InvokeHierarchy(layout, "OpenHdrCalibration");
            object blackLevelStep = Enum.ToObject(
                GetHierarchyField<object>(layout, "hdrCalibrationStep").GetType(),
                1
            );
            InvokeHierarchy(layout, "SetPauseHdrCalibrationStep", blackLevelStep);
            InvokeHierarchy(layout, "PreviousPauseHdrCalibrationStep");
            InvokeHierarchy(layout, "SetPauseHdrCalibrationStep", blackLevelStep);
            InvokeHierarchy(layout, "NextPauseHdrCalibrationStep");

            InvokeHierarchy(layout, "OpenHdrCalibration");
            InvokeHierarchy(layout, "HideSettings");
            SetHierarchyField(layout, "<HdrCalibrationOpen>k__BackingField", true);
            GameDisplaySettings.BeginHdrCalibrationPreview();
            InvokeHierarchy(layout, "OnDestroy");

            RectTransform savedDetails = GetHierarchyField<RectTransform>(
                layout,
                "hdrDetailsPanel"
            );
            RectTransform savedCalibration = GetHierarchyField<RectTransform>(
                layout,
                "hdrCalibrationPanel"
            );
            RectTransform savedSettings = GetHierarchyField<RectTransform>(layout, "settingsPanel");
            SetHierarchyField(layout, "hdrDetailsPanel", null);
            InvokeHierarchy(layout, "LayoutPauseHdrDetails", 500f, 100f, -100f, false, false);
            SetHierarchyField(layout, "hdrCalibrationPanel", null);
            InvokeHierarchy(layout, "LayoutPauseHdrCalibration", 500f, 100f, -100f, false, false);
            SetHierarchyField(layout, "settingsPanel", null);
            InvokeHierarchy(layout, "LayoutSettingsPanels", 500f, 100f, -100f, false, false);
            SetHierarchyField(layout, "hdrDetailsPanel", savedDetails);
            SetHierarchyField(layout, "hdrCalibrationPanel", savedCalibration);
            SetHierarchyField(layout, "settingsPanel", savedSettings);

            Material[] pauseMaterials = GetHierarchyField<Material[]>(
                layout,
                "hdrPreviewMaterials"
            );
            InvokeHierarchy(layout, "CreatePauseHdrPreviewMaterials", (Shader)null);
            SetHierarchyField(layout, "hdrPreviewMaterials", null);
            InvokeHierarchy(layout, "DestroyPauseHdrCalibrationMaterials");
            SetHierarchyField(layout, "hdrPreviewMaterials", new Material[] { null });
            InvokeHierarchy(layout, "DestroyPauseHdrCalibrationMaterials");
            SetHierarchyField(layout, "hdrPreviewMaterials", pauseMaterials);
            SetHierarchyField(layout, "<HdrCalibrationOpen>k__BackingField", false);
            InvokeHierarchy(layout, "ShowSettings", (Action)null);
        }
    }
}
