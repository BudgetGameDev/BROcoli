using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsivePauseMenuLayout
    {
        private void BuildHdrCalibrationPresentation()
        {
            hdrCalibrationPanel = CreateRect("PauseHdrCalibrationPanel", card);
            hdrCalibrationStepLabel = CreateText(
                "PauseHdrCalibrationStep",
                hdrCalibrationPanel,
                string.Empty,
                15f,
                Primary,
                materialFont
            );
            hdrCalibrationInstructions = CreateText(
                "PauseHdrCalibrationInstructions",
                hdrCalibrationPanel,
                string.Empty,
                15f,
                OnSurfaceMuted,
                materialFont
            );
            hdrCalibrationInstructions.textWrappingMode = TextWrappingModes.Normal;
            hdrCalibrationInstructions.alignment = TextAlignmentOptions.Center;
            hdrCalibrationPreview = CreatePanel(
                "PauseHdrCalibrationPreview",
                hdrCalibrationPanel,
                Color.black
            );
            hdrPreviewBackground = hdrCalibrationPreview.GetComponent<Image>();
            hdrPreviewReference = CreatePanel("ReferencePatch", hdrCalibrationPreview, Color.white)
                .GetComponent<Image>();
            hdrPreviewMark = CreatePanel("CenterMark", hdrCalibrationPreview, Color.white)
                .GetComponent<Image>();
            CreatePauseHdrPreviewMaterials();
            hdrCalibrationValue = CreateText(
                "PauseHdrCalibrationValue",
                hdrCalibrationPanel,
                string.Empty,
                20f,
                OnSurface,
                materialFont
            );
            hdrCalibrationSlider = CreatePauseHdrCalibrationSlider();
            hdrCalibrationSlider.onValueChanged.AddListener(OnPauseHdrCalibrationSliderChanged);
            hdrCalibrationSystemButton = CreateButton(
                "PauseHdrSystemButton",
                hdrCalibrationPanel,
                "RESET TO SYSTEM DETECTED"
            );
            hdrCalibrationSystemButton.onClick.AddListener(ResetPauseHdrCalibrationToSystem);
            hdrCalibrationBackButton = CreateButton(
                "PauseHdrCalibrationBackButton",
                hdrCalibrationPanel,
                "CANCEL"
            );
            hdrCalibrationBackButton.onClick.AddListener(PreviousPauseHdrCalibrationStep);
            hdrCalibrationNextButton = CreateButton(
                "PauseHdrCalibrationNextButton",
                hdrCalibrationPanel,
                "NEXT"
            );
            hdrCalibrationNextButton.onClick.AddListener(NextPauseHdrCalibrationStep);
            hdrCalibrationSelectables = new Selectable[]
            {
                hdrCalibrationSlider,
                hdrCalibrationSystemButton,
                hdrCalibrationBackButton,
                hdrCalibrationNextButton,
            };
            foreach (
                Button button in new[]
                {
                    hdrCalibrationSystemButton,
                    hdrCalibrationBackButton,
                    hdrCalibrationNextButton,
                }
            )
                StyleButton(button, false, materialFont);
            RegisterPauseHdrCalibrationPointerSelection();
            hdrCalibrationPanel.gameObject.SetActive(false);
        }

        private Slider CreatePauseHdrCalibrationSlider()
        {
            RectTransform track = CreatePanel(
                "PauseHdrCalibrationTrack",
                hdrCalibrationPanel,
                Hex("#53645A")
            );
            track.GetComponent<Image>().raycastTarget = true;
            RectTransform fillArea = CreateRect("Fill Area", track);
            RectTransform fill = CreatePanel("Fill", fillArea, Primary);
            RectTransform handleArea = CreateRect("Handle Slide Area", track);
            RectTransform handle = CreatePanel("Handle", handleArea, OnSurface);
            handle.GetComponent<Image>().raycastTarget = true;
            Slider slider = track.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            return slider;
        }

        private void CreatePauseHdrPreviewMaterials()
        {
            Shader shader = Shader.Find("UI/Default");
            if (shader == null)
                return;
            Image[] images = { hdrPreviewBackground, hdrPreviewReference, hdrPreviewMark };
            hdrPreviewMaterials = new Material[images.Length];
            for (int index = 0; index < images.Length; index++)
            {
                Material material = new(shader)
                {
                    name = $"Pause HDR Calibration Preview {index}",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                images[index].color = Color.white;
                images[index].material = material;
                hdrPreviewMaterials[index] = material;
            }
        }

        private void DestroyPauseHdrCalibrationMaterials()
        {
            if (hdrPreviewMaterials == null)
                return;
            foreach (Material material in hdrPreviewMaterials)
            {
                if (material == null)
                    continue;
                if (Application.isPlaying)
                    Destroy(material);
                else
                    DestroyImmediate(material);
            }
            hdrPreviewMaterials = null;
        }

        private void RegisterPauseHdrCalibrationPointerSelection()
        {
            for (int index = 0; index < hdrCalibrationSelectables.Length; index++)
            {
                int captured = index;
                EventTrigger trigger = hdrCalibrationSelectables[index]
                    .gameObject.AddComponent<EventTrigger>();
                EventTrigger.Entry entry = new() { eventID = EventTriggerType.PointerEnter };
                entry.callback.AddListener(_ => selectedHdrCalibrationControl = captured);
                trigger.triggers.Add(entry);
            }
        }

        private void SelectPauseHdrCalibrationControl(int index, bool sound = true)
        {
            selectedHdrCalibrationControl =
                (index + hdrCalibrationSelectables.Length) % hdrCalibrationSelectables.Length;
            Select(hdrCalibrationSelectables[selectedHdrCalibrationControl]);
            if (sound)
                BudgetGameDev.Shared.ProceduralUIAudio.PlayHover();
        }
    }
}
