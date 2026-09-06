using System;
using BudgetGameDev.Shared.Rendering;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Shared
{
    /// <summary>Reusable uGUI settings page for any host menu, independent of HDRP assemblies.</summary>
    public sealed partial class NvidiaSettingsPage : MonoBehaviour
    {
        private RectTransform panel,
            viewport,
            content;
        private TMP_Text heading,
            report;
        private ScrollRect scroll;
        private Button dlssButton,
            framesButton,
            reflexButton,
            resetButton,
            copyButton,
            backButton;
        private Button[] controls;
        private Action closed;
        private Func<bool> consumeCancel,
            consumeSubmit;
        private int openedFrame;
        private NvidiaRendering.Snapshot snapshot;
        private float nextRefresh,
            copiedUntil;
        private int selection;
        private float nextNavigation;
        private string reportText;
        public bool IsOpen => gameObject.activeSelf;

        public static NvidiaSettingsPage Create(
            RectTransform parent,
            TMP_FontAsset font,
            Action onClosed,
            Func<bool> consumeCancel = null,
            Func<bool> consumeSubmit = null
        )
        {
            var rect = CreateRect("NvidiaSettingsPage", parent);
            var page = rect.gameObject.AddComponent<NvidiaSettingsPage>();
            page.panel = rect;
            page.closed = onClosed;
            page.consumeCancel = consumeCancel ?? (() => true);
            page.consumeSubmit = consumeSubmit ?? (() => true);
            page.Build(font);
            rect.gameObject.SetActive(false);
            return page;
        }

        public static Button CreateMenuButton(
            RectTransform parent,
            TMP_FontAsset font,
            Action onClick
        ) => MakeButton("NvidiaSettingsButton", parent, "NVIDIA", font, onClick);

        private void Build(TMP_FontAsset font)
        {
            heading = CreateText(
                "NvidiaTitle",
                panel,
                "DLSS · FRAME GEN · REFLEX",
                18,
                OnSurface,
                font
            );
            heading.characterSpacing = 0;
            heading.enableAutoSizing = true;
            heading.fontSizeMin = 10;
            heading.fontSizeMax = 18;
            dlssButton = MakeButton(
                "DlssControl",
                panel,
                "DLSS",
                font,
                () => NvidiaRendering.Backend?.SetDlss(!snapshot.DlssRequested)
            );
            framesButton = MakeButton(
                "FrameGenControl",
                panel,
                "FRAME GEN",
                font,
                () =>
                {
                    int maximum = Mathf.Min(3, snapshot.MaximumGeneratedFrames);
                    NvidiaRendering.Backend?.SetFrames(
                        snapshot.GeneratedFrames >= maximum ? 0 : snapshot.GeneratedFrames + 1
                    );
                }
            );
            reflexButton = MakeButton(
                "ReflexControl",
                panel,
                "REFLEX",
                font,
                () => NvidiaRendering.Backend?.SetReflex((snapshot.Reflex + 1) % 3)
            );
            viewport = CreatePanel("NvidiaDiagnosticsViewport", panel, Background);
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            report = CreateText("NvidiaDiagnosticsText", content, "", 14, OnSurface, font);
            report.alignment = TextAlignmentOptions.TopLeft;
            report.fontStyle = FontStyles.Normal;
            report.characterSpacing = 0;
            report.textWrappingMode = TextWrappingModes.Normal;
            report.overflowMode = TextOverflowModes.Overflow;
            report.richText = false;
            report.margin = new Vector4(10, 8, 10, 8);
            Stretch(report.rectTransform);
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;
            resetButton = MakeButton(
                "NvidiaReset",
                panel,
                "DEFAULTS",
                font,
                () => NvidiaRendering.Backend?.Reset()
            );
            copyButton = MakeButton(
                "NvidiaCopy",
                panel,
                "COPY DEBUG",
                font,
                () =>
                {
                    GUIUtility.systemCopyBuffer = reportText ?? "";
                    copiedUntil = Time.unscaledTime + 2;
                }
            );
            backButton = MakeButton("NvidiaBack", panel, "BACK", font, Close);
            controls = new[]
            {
                dlssButton,
                framesButton,
                reflexButton,
                resetButton,
                copyButton,
                backButton,
            };
            foreach (var button in new[] { dlssButton, framesButton, reflexButton, resetButton })
                button.onClick.AddListener(Refresh);
            for (int i = 0; i < controls.Length; i++)
            {
                int index = i;
                var trigger = controls[i].gameObject.AddComponent<EventTrigger>();
                var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                entry.callback.AddListener(_ => selection = index);
                trigger.triggers.Add(entry);
            }
        }

        private static Button MakeButton(
            string name,
            RectTransform parent,
            string label,
            TMP_FontAsset font,
            Action action
        )
        {
            var rect = CreatePanel(name, parent, Color.white);
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var text = CreateText("Label", rect, label, 12, OnSurface, font);
            StyleButton(button, false, font);
            text.characterSpacing = 0;
            text.margin = new Vector4(4, 0, 4, 0);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.enableAutoSizing = true;
            text.fontSizeMin = 9;
            text.fontSizeMax = 14;
            button.onClick.AddListener(() =>
            {
                ProceduralUIAudio.PlaySelect();
                action();
            });
            return button;
        }

        public void Open()
        {
            openedFrame = Time.frameCount;
            gameObject.SetActive(true);
            Refresh();
            scroll.verticalNormalizedPosition = 1;
            SelectControl(0);
        }

        public void Dismiss()
        {
            NvidiaRendering.Backend?.ReleaseDiagnostics();
            gameObject.SetActive(false);
        }

        public void Close()
        {
            Dismiss();
            closed?.Invoke();
        }

        private void OnDisable() => NvidiaRendering.Backend?.ReleaseDiagnostics();

        private void Update()
        {
            if (Time.unscaledTime >= nextRefresh)
                Refresh();
            HandleInput();
        }

        private void Refresh()
        {
            nextRefresh = Time.unscaledTime + 0.25f;
            snapshot = NvidiaRendering.Capture();
            reportText = snapshot.Report;
            report.text = reportText;
            SetLabel(dlssButton, snapshot.DlssRequested ? "DLSS\nQUALITY / K" : "DLSS\nOFF");
            SetLabel(
                framesButton,
                "FRAME GEN\n"
                    + (
                        snapshot.GeneratedFrames == 0
                            ? "OFF"
                            : $"{snapshot.GeneratedFrames + 1}x REQUESTED"
                    )
            );
            SetLabel(
                reflexButton,
                "REFLEX\n"
                    + (
                        snapshot.Reflex == 0
                            ? (snapshot.GeneratedFrames > 0 ? "ON (FOR FG)" : "OFF")
                        : snapshot.Reflex == 1 ? "ON"
                        : "ON + BOOST"
                    )
            );
            SetLabel(copyButton, Time.unscaledTime < copiedUntil ? "COPIED" : "COPY DEBUG");
            dlssButton.interactable = snapshot.CanSetDlss;
            framesButton.interactable = snapshot.CanSetFrames;
            reflexButton.interactable = snapshot.CanSetReflex;
            resetButton.interactable = NvidiaRendering.Backend != null;
            ResizeContent();
        }

        private static void SetLabel(Button button, string value) =>
            button.GetComponentInChildren<TMP_Text>().text = value;
    }
}
