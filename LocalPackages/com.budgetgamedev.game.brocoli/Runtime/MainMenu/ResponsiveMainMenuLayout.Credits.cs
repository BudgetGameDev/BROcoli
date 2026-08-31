using BudgetGameDev.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class ResponsiveMainMenuLayout
    {
        internal const string CreditsResourcePath = "Brocoli/Credits";

        private Button creditsButton;
        private Button backCreditsButton;
        private Button[] creditsActionButtons;
        private RectTransform creditsPanel;
        private RectTransform creditsViewport;
        private TMP_Text creditsTitle;
        private TMP_Text creditsBody;
        private ScrollRect creditsScroll;

        public static bool CreditsOpen { get; private set; }
        public static bool ModalOpen => SettingsOpen || CreditsOpen || SavesOpen;

        private void BuildCreditsPresentation()
        {
            creditsButton = CreateButton("CreditsButton", card, "CREDITS");
            creditsButton.onClick.AddListener(OpenCredits);

            creditsPanel = CreateRect("CreditsPanel", card);
            creditsTitle = CreateText(
                "CreditsTitle",
                creditsPanel,
                "CREDITS & LICENSES",
                22f,
                OnSurface
            );

            creditsViewport = CreatePanel("CreditsViewport", creditsPanel, SurfaceVariant);
            creditsViewport.GetComponent<Image>().raycastTarget = true;
            creditsViewport.gameObject.AddComponent<RectMask2D>();
            creditsScroll = creditsViewport.gameObject.AddComponent<ScrollRect>();
            creditsScroll.viewport = creditsViewport;
            creditsScroll.horizontal = false;
            creditsScroll.vertical = true;
            creditsScroll.movementType = ScrollRect.MovementType.Clamped;
            creditsScroll.scrollSensitivity = 42f;

            TextAsset source = Resources.Load<TextAsset>(CreditsResourcePath);
            string text =
                source != null
                    ? source.text.Trim()
                    : "Credits could not be loaded. Please report this installation error.";
            creditsBody = CreateText("CreditsBody", creditsViewport, text, 15f, OnSurfaceMuted);
            creditsBody.alignment = TextAlignmentOptions.TopLeft;
            creditsBody.fontStyle = FontStyles.Normal;
            creditsBody.characterSpacing = 0f;
            creditsBody.lineSpacing = 5f;
            creditsBody.textWrappingMode = TextWrappingModes.Normal;
            creditsBody.overflowMode = TextOverflowModes.Overflow;
            creditsBody.margin = new Vector4(18f, 14f, 18f, 14f);
            creditsScroll.content = creditsBody.rectTransform;

            backCreditsButton = CreateButton("BackFromCreditsButton", creditsPanel, "BACK");
            backCreditsButton.onClick.AddListener(CloseCredits);
            creditsActionButtons = new[] { backCreditsButton };
            creditsPanel.gameObject.SetActive(false);
        }

        private void OpenCredits()
        {
            ProceduralUIAudio.PlaySelect();
            mainButtonsWereActive = new bool[mainButtons.Length];
            for (int i = 0; i < mainButtons.Length; i++)
            {
                if (mainButtons[i] == null)
                    continue;

                mainButtonsWereActive[i] = mainButtons[i].gameObject.activeSelf;
                mainButtons[i].gameObject.SetActive(false);
            }

            CreditsOpen = true;
            creditsPanel.gameObject.SetActive(true);
            ApplyResponsiveLayout(true);
            Canvas.ForceUpdateCanvases();
            creditsScroll.verticalNormalizedPosition = 1f;
            EventSystem.current?.SetSelectedGameObject(backCreditsButton.gameObject);
        }

        private void CloseCredits()
        {
            ProceduralUIAudio.PlaySelect();
            CreditsOpen = false;
            creditsPanel.gameObject.SetActive(false);
            if (mainButtonsWereActive != null)
            {
                for (int i = 0; i < mainButtons.Length; i++)
                    if (mainButtons[i] != null)
                        mainButtons[i].gameObject.SetActive(mainButtonsWereActive[i]);
            }

            GetComponent<MainMenu>()?.SetupControllerNavigation(true, creditsButton);
            ApplyResponsiveLayout(true);
        }

        private void UpdateCreditsInput()
        {
            bool cancel =
                Input.GetKeyDown(KeyCode.Escape)
                || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            bool home = Input.GetKeyDown(KeyCode.Home);
            bool end = Input.GetKeyDown(KeyCode.End);
            float vertical = ReadCreditsScrollAxis();
            bool submit =
                Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.Space)
                || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
            ProcessCreditsInput(
                cancel && MenuInputGate.TryConsumeCancel(),
                home,
                end,
                vertical,
                submit && MenuInputGate.TryConsumeSubmit()
            );
        }

        internal void ProcessCreditsInput(
            bool cancel,
            bool home,
            bool end,
            float vertical,
            bool submit
        )
        {
            if (cancel)
            {
                CloseCredits();
                return;
            }

            if (home)
                creditsScroll.verticalNormalizedPosition = 1f;
            else if (end)
                creditsScroll.verticalNormalizedPosition = 0f;

            if (Mathf.Abs(vertical) > 0.15f)
            {
                creditsScroll.verticalNormalizedPosition = Mathf.Clamp01(
                    creditsScroll.verticalNormalizedPosition
                        + vertical * Time.unscaledDeltaTime * 0.9f
                );
            }

            if (submit)
                CloseCredits();
        }

        private static float ReadCreditsScrollAxis()
        {
            Vector2? rightStick = Gamepad.current?.rightStick.ReadValue();
            Vector2? dpad = Gamepad.current?.dpad.ReadValue();
            return ResolveCreditsScrollAxis(
                Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W),
                Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S),
                Input.GetKey(KeyCode.PageUp),
                Input.GetKey(KeyCode.PageDown),
                rightStick,
                dpad
            );
        }

        internal static float ResolveCreditsScrollAxis(
            bool up,
            bool down,
            bool pageUp,
            bool pageDown,
            Vector2? rightStick,
            Vector2? dpad
        )
        {
            float vertical =
                up ? 1f
                : down ? -1f
                : 0f;
            if (pageUp)
                vertical = 3f;
            else if (pageDown)
                vertical = -3f;

            if (!rightStick.HasValue)
                return vertical;

            Vector2 axis = rightStick.Value;
            if (axis.sqrMagnitude < 0.04f)
                axis = dpad.GetValueOrDefault();
            return Mathf.Abs(axis.y) > 0.15f ? axis.y : vertical;
        }
    }
}
