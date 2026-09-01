using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsiveMainMenuLayout
    {
        private void BuildHdrCalibrationPresentation()
        {
            hdrCalibrationPanel = CreateRect("HdrCalibrationPanel", card);
            hdrCalibrationTitle = CreateText(
                "HdrCalibrationTitle",
                hdrCalibrationPanel,
                "HDR CALIBRATION",
                22f,
                OnSurface
            );
            hdrCalibrationStepLabel = CreateText(
                "HdrCalibrationStep",
                hdrCalibrationPanel,
                string.Empty,
                15f,
                Primary
            );
            hdrCalibrationInstructions = CreateText(
                "HdrCalibrationInstructions",
                hdrCalibrationPanel,
                string.Empty,
                15f,
                OnSurfaceMuted
            );
            hdrCalibrationInstructions.textWrappingMode = TextWrappingModes.Normal;
            hdrCalibrationInstructions.alignment = TextAlignmentOptions.Center;

            hdrCalibrationPreview = CreatePanel(
                "HdrCalibrationPreview",
                hdrCalibrationPanel,
                Color.black
            );
            hdrPreviewBackground = hdrCalibrationPreview.GetComponent<Image>();
            hdrPreviewReference = CreatePanel("ReferencePatch", hdrCalibrationPreview, Color.white)
                .GetComponent<Image>();
            hdrPreviewMark = CreatePanel("CenterMark", hdrCalibrationPreview, Color.white)
                .GetComponent<Image>();
            CreateHdrPreviewMaterials();

            hdrCalibrationValue = CreateText(
                "HdrCalibrationValue",
                hdrCalibrationPanel,
                string.Empty,
                20f,
                OnSurface
            );
            hdrCalibrationSlider = CreateHdrCalibrationSlider();
            hdrCalibrationSlider.onValueChanged.AddListener(OnHdrCalibrationSliderChanged);

            hdrCalibrationBackButton = CreateButton(
                "HdrCalibrationBackButton",
                hdrCalibrationPanel,
                "CANCEL"
            );
            hdrCalibrationBackButton.onClick.AddListener(PreviousHdrCalibrationStep);
            hdrCalibrationNextButton = CreateButton(
                "HdrCalibrationNextButton",
                hdrCalibrationPanel,
                "NEXT"
            );
            hdrCalibrationNextButton.onClick.AddListener(NextHdrCalibrationStep);
            hdrCalibrationActionButtons = new[]
            {
                hdrCalibrationBackButton,
                hdrCalibrationNextButton,
            };
            hdrCalibrationSelectables = new Selectable[]
            {
                hdrCalibrationSlider,
                hdrCalibrationBackButton,
                hdrCalibrationNextButton,
            };
            RegisterHdrCalibrationPointerSelection();
            hdrCalibrationPanel.gameObject.SetActive(false);
        }

        private Slider CreateHdrCalibrationSlider()
        {
            RectTransform track = CreatePanel(
                "HdrCalibrationTrack",
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

        private void CreateHdrPreviewMaterials()
        {
            CreateHdrPreviewMaterials(Shader.Find("UI/Default"));
        }

        private void CreateHdrPreviewMaterials(Shader shader)
        {
            if (shader == null)
                return;

            Image[] images = { hdrPreviewBackground, hdrPreviewReference, hdrPreviewMark };
            hdrPreviewMaterials = new Material[images.Length];
            for (int index = 0; index < images.Length; index++)
            {
                Material material = new(shader)
                {
                    name = $"HDR Calibration Preview {index}",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                images[index].color = Color.white;
                images[index].material = material;
                hdrPreviewMaterials[index] = material;
            }
        }

        private void DestroyHdrCalibrationMaterials()
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
        }
    }
}
