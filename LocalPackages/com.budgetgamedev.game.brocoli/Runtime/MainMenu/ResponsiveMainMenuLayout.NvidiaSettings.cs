using BudgetGameDev.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsiveMainMenuLayout
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
