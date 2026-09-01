using TMPro;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsiveMainMenuLayout
    {
        private void BuildHdrDetailsPresentation()
        {
            hdrDetailsPanel = CreateRect("HdrDetailsPanel", card);
            hdrDetailsTitle = CreateText(
                "HdrDetailsTitle",
                hdrDetailsPanel,
                "HDR OUTPUT",
                22f,
                OnSurface
            );
            hdrDetailsSubtitle = CreateText(
                "HdrDetailsSubtitle",
                hdrDetailsPanel,
                "DISPLAY DATA & ACTIVE TONE MAP",
                14f,
                Primary
            );
            hdrDetailsExplanation = CreateText(
                "HdrDetailsExplanation",
                hdrDetailsPanel,
                "EDID is the monitor's hardware report. The OS HDR profile includes calibration "
                    + "and the SDR-white preference used as paper white. EDID does not define paper white.",
                13f,
                OnSurfaceMuted
            );
            hdrDetailsExplanation.textWrappingMode = TextWrappingModes.Normal;
            hdrDetailsExplanation.alignment = TextAlignmentOptions.Center;
            hdrDetailsValues = CreateText(
                "HdrDetailsValues",
                hdrDetailsPanel,
                string.Empty,
                14f,
                OnSurface
            );
            hdrDetailsValues.textWrappingMode = TextWrappingModes.Normal;
            hdrDetailsValues.alignment = TextAlignmentOptions.TopLeft;
            hdrDetailsValues.richText = true;

            hdrDetailsProfileButton = CreateButton(
                "HdrDetailsProfileButton",
                hdrDetailsPanel,
                "RESET TO HDR PROFILE"
            );
            hdrDetailsProfileButton.onClick.AddListener(UseHdrProfileValues);
            hdrDetailsEdidButton = CreateButton(
                "HdrDetailsEdidButton",
                hdrDetailsPanel,
                "RESET TO EDID"
            );
            hdrDetailsEdidButton.onClick.AddListener(UseEdidValues);
            hdrDetailsCalibrateButton = CreateButton(
                "HdrDetailsCalibrateButton",
                hdrDetailsPanel,
                "CALIBRATE"
            );
            hdrDetailsCalibrateButton.onClick.AddListener(OpenHdrCalibration);
            hdrDetailsBackButton = CreateButton(
                "HdrDetailsBackButton",
                hdrDetailsPanel,
                "BACK"
            );
            hdrDetailsBackButton.onClick.AddListener(CloseHdrDetails);
            hdrDetailsActionButtons = new[]
            {
                hdrDetailsProfileButton,
                hdrDetailsEdidButton,
                hdrDetailsCalibrateButton,
                hdrDetailsBackButton,
            };
            hdrDetailsSelectables = new Selectable[]
            {
                hdrDetailsProfileButton,
                hdrDetailsEdidButton,
                hdrDetailsCalibrateButton,
                hdrDetailsBackButton,
            };
            RegisterHdrDetailsPointerSelection();
            hdrDetailsPanel.gameObject.SetActive(false);
        }
    }
}
