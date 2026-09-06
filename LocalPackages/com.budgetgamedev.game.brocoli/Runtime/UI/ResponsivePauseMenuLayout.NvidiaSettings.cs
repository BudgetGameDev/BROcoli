using BudgetGameDev.Shared;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsivePauseMenuLayout
    {
        private NvidiaSettingsPage nvidiaPage;
        private Button nvidiaSettingsButton;

        private void BuildNvidiaSettingsPresentation()
        {
            nvidiaSettingsButton = NvidiaSettingsPage.CreateMenuButton(
                settingsPanel,
                materialFont,
                () =>
                {
                    settingsPanel.gameObject.SetActive(false);
                    title.text = "NVIDIA";
                    footer.text = "ESC  ·  B  TO SETTINGS";
                    nvidiaPage.Open();
                    ApplyResponsiveLayout(true);
                }
            );
            nvidiaPage = NvidiaSettingsPage.Create(
                card,
                materialFont,
                () =>
                {
                    if (!SettingsOpen)
                        return;
                    settingsPanel.gameObject.SetActive(true);
                    title.text = "SETTINGS";
                    footer.text = "ESC  ·  B  TO PAUSE MENU";
                    SelectSetting(
                        System.Array.IndexOf(settingsSelectables, nvidiaSettingsButton),
                        false
                    );
                    ApplyResponsiveLayout(true);
                },
                MenuInputGate.TryConsumeCancel,
                MenuInputGate.TryConsumeSubmit
            );
        }
    }
}
