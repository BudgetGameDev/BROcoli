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
        private static void ExerciseHdrDetails(ResponsiveMainMenuLayout layout)
        {
            InvokeHierarchy(layout, "CloseHdrDetails");
            if (!ResponsiveMainMenuLayout.SettingsOpen)
                InvokeHierarchy(layout, "OpenSettings");
            InvokeHierarchy(layout, "OpenHdrDetails");
            MethodInfo nativeCalibration = typeof(GameDisplaySettings)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Single(method =>
                    method.Name == "TryUseNativeDisplayCalibration"
                    && method.GetParameters().Length == 7
                );
            nativeCalibration.Invoke(
                null,
                new object[] { true, true, false, 1200f, 240f, 0.001f, 500f }
            );
            object metadata = Activator.CreateInstance(
                typeof(DisplayEdidMetadata),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[]
                {
                    "COVERAGE HDR",
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
            InvokeHierarchy(layout, "SyncHdrDetails");
            InvokeHierarchy(layout, "UseHdrProfileValues");
            InvokeHierarchy(layout, "UseEdidValues");

            Gamepad detailsGamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                InputSystem.QueueStateEvent(
                    detailsGamepad,
                    new GamepadState().WithButton(GamepadButton.DpadRight)
                );
                InputSystem.Update();
                InvokeHierarchy(layout, "UpdateHdrDetailsInput");
                InvokeHierarchy(layout, "Update");
            }
            finally
            {
                InputSystem.RemoveDevice(detailsGamepad);
            }
            SetHierarchyField(layout, "selectedHdrDetailsControl", 0);
            SetHierarchyField(layout, "lastHdrDetailsNavTime", -10f);
            layout.ProcessHdrDetailsInput(false, 0f, 1f, false, Time.unscaledTime);
            SetHierarchyField(layout, "lastHdrDetailsNavTime", -10f);
            layout.ProcessHdrDetailsInput(false, -1f, 0f, false, Time.unscaledTime + 1f);
            Selectable[] detailControls = GetHierarchyField<Selectable[]>(
                layout,
                "hdrDetailsSelectables"
            );
            int detailButton = Array.FindIndex(
                detailControls,
                selectable => selectable is Button button && button.interactable
            );
            if (detailButton >= 0)
            {
                SetHierarchyField(layout, "selectedHdrDetailsControl", detailButton);
                ResetMenuInputGate();
                layout.ProcessHdrDetailsInput(false, 0f, 0f, true, Time.unscaledTime + 1.5f);
            }
            InvokeHierarchy(layout, "OpenHdrCalibration");
            InvokeHierarchy(layout, "CloseHdrCalibration", false);
            Assert.That(ResponsiveMainMenuLayout.HdrDetailsOpen, Is.True);
            ResetMenuInputGate();
            layout.ProcessHdrDetailsInput(true, 0f, 0f, false, Time.unscaledTime + 2f);
            Assert.That(ResponsiveMainMenuLayout.HdrDetailsOpen, Is.False);

            InvokeHierarchy(layout, "OpenHdrDetails");
            InvokeHierarchy(layout, "CloseSettings");
        }

        private static void ExerciseHdrCalibration(ResponsiveMainMenuLayout layout)
        {
            if (!ResponsiveMainMenuLayout.SettingsOpen)
                InvokeHierarchy(layout, "OpenSettings");
            InvokeHierarchy(layout, "OpenHdrCalibration");

            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                InvokeHierarchy(layout, "UpdateHdrCalibrationInput");
                InvokeHierarchy(layout, "UpdateSettingsInput");
                InvokeHierarchy(layout, "Update");
            }
            finally
            {
                InputSystem.RemoveDevice(gamepad);
            }

            SetHierarchyField(layout, "suppressHdrCalibrationCallback", true);
            InvokeHierarchy(layout, "OnHdrCalibrationSliderChanged", 700f);
            SetHierarchyField(layout, "suppressHdrCalibrationCallback", false);

            foreach (
                ResponsiveMainMenuLayout.HdrCalibrationStep step in new[]
                {
                    ResponsiveMainMenuLayout.HdrCalibrationStep.PeakBrightness,
                    ResponsiveMainMenuLayout.HdrCalibrationStep.BlackLevel,
                }
            )
            {
                InvokeHierarchy(layout, "SetHdrCalibrationStep", step);
                float value = step switch
                {
                    ResponsiveMainMenuLayout.HdrCalibrationStep.PeakBrightness => 725f,
                    _ => 0.4f,
                };
                InvokeHierarchy(layout, "OnHdrCalibrationSliderChanged", value);
                SetHierarchyField(layout, "selectedHdrCalibrationControl", 0);
                SetHierarchyField(layout, "lastHdrCalibrationNavTime", -10f);
                layout.ProcessHdrCalibrationInput(false, 0f, 1f, false, Time.unscaledTime);
            }

            SetHierarchyField(layout, "selectedHdrCalibrationControl", 0);
            SetHierarchyField(layout, "lastHdrCalibrationNavTime", -10f);
            layout.ProcessHdrCalibrationInput(false, 1f, 0f, false, Time.unscaledTime);

            InvokeHierarchy(
                layout,
                "SetHdrCalibrationStep",
                ResponsiveMainMenuLayout.HdrCalibrationStep.BlackLevel
            );
            InvokeHierarchy(layout, "PreviousHdrCalibrationStep");
            InvokeHierarchy(layout, "NextHdrCalibrationStep");

            SetHierarchyField(layout, "selectedHdrCalibrationControl", 0);
            ResetMenuInputGate();
            layout.ProcessHdrCalibrationInput(false, 0f, 0f, true, Time.unscaledTime + 1f);

            InvokeHierarchy(layout, "OpenHdrCalibration");
            SetHierarchyField(layout, "selectedHdrCalibrationControl", 1);
            ResetMenuInputGate();
            layout.ProcessHdrCalibrationInput(false, 0f, 0f, true, Time.unscaledTime + 2f);

            InvokeHierarchy(layout, "CloseHdrCalibration", true);

            InvokeHierarchy(layout, "OpenHdrCalibration");
            ResetMenuInputGate();
            layout.ProcessHdrCalibrationInput(true, 0f, 0f, false, Time.unscaledTime + 3f);

            InvokeHierarchy(layout, "OpenHdrCalibration");
            InvokeHierarchy(layout, "CloseSettings");
            InvokeHierarchy(layout, "CloseHdrCalibration", true);

            object valueLabel = GetHierarchyField<object>(layout, "hdrCalibrationValue");
            SetHierarchyField(layout, "hdrCalibrationValue", null);
            InvokeHierarchy(layout, "SyncHdrCalibrationPreview");
            SetHierarchyField(layout, "hdrCalibrationValue", valueLabel);
            InvokeHierarchy(layout, "SetHdrPreviewLuminance", null, 1f);

            Material[] materials = GetHierarchyField<Material[]>(layout, "hdrPreviewMaterials");
            SetHierarchyField(layout, "hdrPreviewMaterials", null);
            InvokeHierarchy(layout, "DestroyHdrCalibrationMaterials");
            SetHierarchyField(layout, "hdrPreviewMaterials", materials);
            InvokeHierarchy(layout, "CreateHdrPreviewMaterials", (Shader)null);
            if (materials != null && materials.Length > 0)
                materials[0] = null;
            InvokeHierarchy(layout, "DestroyHdrCalibrationMaterials");

            if (!ResponsiveMainMenuLayout.SettingsOpen)
                InvokeHierarchy(layout, "OpenSettings");
            InvokeHierarchy(layout, "OpenHdrCalibration");
            InvokeHierarchy(layout, "OnDestroy");
        }

        private static object Invoke(object target, string method, params object[] arguments) =>
            target.GetType().GetMethod(method, PrivateInstance).Invoke(target, arguments);
    }
}
