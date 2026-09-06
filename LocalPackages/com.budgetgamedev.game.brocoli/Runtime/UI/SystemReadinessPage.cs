using System;
using BudgetGameDev.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Games.Brocoli
{
    internal sealed class SystemReadinessPage : MonoBehaviour
    {
        private RectTransform card, backdrop, viewport, content;
        private TMP_Text title, report;
        private Button start, sensors, copy, back;
        private ScrollRect scroll;
        private Action cancel, close;
        private bool running;
        private int openedFrame, selected;
        private float nextNavigation;
        private string reportText;
        private string assessmentText;
        private bool showingSensors;
        private float nextSensors;

        internal void Build(TMP_FontAsset font, Action begin, Action cancelRun, Action closePage)
        {
            cancel = cancelRun;
            close = closePage;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();
            backdrop = CreatePanel("Backdrop", (RectTransform)transform, new Color(0, 0, 0, .8f));
            Stretch(backdrop);
            backdrop.GetComponent<Image>().raycastTarget = true;
            card = CreatePanel("SystemReadinessCard", (RectTransform)transform, Background);
            title = CreateText("Title", card, "SYSTEM READINESS", 28, OnSurface, font);
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 0;
            viewport = CreatePanel("ReportViewport", card, SurfaceVariant);
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            report = CreateText("Report", content, "", 20, OnSurface, font);
            report.fontStyle = FontStyles.Normal;
            report.characterSpacing = 0;
            report.alignment = TextAlignmentOptions.TopLeft;
            report.textWrappingMode = TextWrappingModes.Normal;
            report.overflowMode = TextOverflowModes.Overflow;
            report.richText = true;
            report.margin = new Vector4(18, 12, 18, 12);
            Stretch(report.rectTransform);
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35;
            start = Button("StartBenchmark", "RUN BENCHMARK", font, begin);
            sensors = Button("Sensors", "SENSORS", font, () =>
            {
                showingSensors = !showingSensors;
                sensors.GetComponentInChildren<TMP_Text>().text = showingSensors ? "ASSESSMENT" : "SENSORS";
                reportText = showingSensors ? HardwareSensorService.FormatReport() : assessmentText;
                report.text = reportText;
                Layout();
                scroll.verticalNormalizedPosition = 1;
            });
            copy = Button("CopyResults", "COPY RESULTS", font, () =>
                GUIUtility.systemCopyBuffer = System.Text.RegularExpressions.Regex.Replace(reportText ?? "", "<[^>]*>", ""));
            back = Button("Back", "BACK", font, () => { if (running) cancel(); else close(); });
        }

        private Button Button(string name, string label, TMP_FontAsset font, Action clicked)
        {
            var rect = CreatePanel(name, card, SurfaceVariant);
            rect.GetComponent<Image>().raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(() => clicked());
            var text = CreateText("Label", rect, label, 18, OnSurface, font);
            text.characterSpacing = 0;
            text.enableAutoSizing = true;
            text.fontSizeMin = 11;
            text.fontSizeMax = 18;
            Stretch(text.rectTransform);
            StyleButton(button, false, font);
            return button;
        }

        internal void Show(string value, bool progress, bool allowRun = true)
        {
            bool opening = !gameObject.activeSelf || running != progress;
            gameObject.SetActive(true);
            running = progress;
            reportText = value;
            assessmentText = value;
            showingSensors = false;
            sensors.GetComponentInChildren<TMP_Text>().text = "SENSORS";
            title.text = progress ? "UNSAVED BENCHMARK" : "SYSTEM READINESS";
            report.text = value;
            backdrop.gameObject.SetActive(!progress);
            start.gameObject.SetActive(!progress);
            sensors.gameObject.SetActive(!progress);
            start.interactable = allowRun;
            copy.gameObject.SetActive(!progress);
            back.GetComponentInChildren<TMP_Text>().text = progress ? "CANCEL" : "BACK";
            Layout();
            if (opening)
            {
                openedFrame = Time.frameCount;
                scroll.verticalNormalizedPosition = 1;
                selected = progress || !allowRun ? 3 : 0;
                Select();
            }
        }

        private void Layout()
        {
            var root = (RectTransform)transform;
            float width = Mathf.Min(running ? 660 : 1080, Mathf.Max(320, root.rect.width - 48));
            float height = running ? 190 : Mathf.Min(870, Mathf.Max(360, root.rect.height - 70));
            card.anchorMin = card.anchorMax = new Vector2(.5f, running ? 1 : .5f);
            card.pivot = new Vector2(.5f, running ? 1 : .5f);
            card.anchoredPosition = running ? new Vector2(0, -30) : Vector2.zero;
            card.sizeDelta = new Vector2(width, height);
            SetTopAnchored(title.rectTransform, 12, width - 30, 40);
            SetTopAnchored(viewport, 60, width - 30, height - 130);
            report.fontSize = running ? 18 : 20;
            float preferred = report.GetPreferredValues(report.text, width - 66, Mathf.Infinity).y + 30;
            content.sizeDelta = new Vector2(0, Mathf.Max(viewport.rect.height, preferred));
            var controls = new[] { start, sensors, copy, back };
            float buttonWidth = (width - 70) / 4;
            for (int i = 0; i < controls.Length; i++)
            {
                var rect = (RectTransform)controls[i].transform;
                SetTopAnchored(rect, height - 56, running ? 200 : buttonWidth, 42);
                rect.anchoredPosition += Vector2.right * (running ? 0 : (i - 1.5f) * (buttonWidth + 10));
            }
        }

        private void Select() => EventSystem.current?.SetSelectedGameObject(
            Control(selected).gameObject);

        private Button Control(int index) => index == 0 ? start : index == 1 ? sensors : index == 2 ? copy : back;

        private void Update()
        {
            if (showingSensors && Time.unscaledTime >= nextSensors)
            {
                nextSensors = Time.unscaledTime + 2;
                float offset = content.anchoredPosition.y;
                reportText = HardwareSensorService.FormatReport();
                report.text = reportText;
                Layout();
                content.anchoredPosition = new Vector2(0, Mathf.Clamp(offset, 0, Mathf.Max(0, content.rect.height - viewport.rect.height)));
            }
            if (Time.frameCount == openedFrame)
                return;
            var k = Keyboard.current;
            var g = Gamepad.current;
            if ((k?.escapeKey.wasPressedThisFrame == true || g?.buttonEast.wasPressedThisFrame == true)
                && MenuInputGate.TryConsumeCancel())
            {
                if (running) cancel(); else close();
                return;
            }
            float direction = g?.dpad.ReadValue().x ?? 0;
            if (k?.leftArrowKey.isPressed == true) direction = -1;
            if (k?.rightArrowKey.isPressed == true) direction = 1;
            if (!running && Mathf.Abs(direction) > .5f && Time.unscaledTime >= nextNavigation)
            {
                selected = (selected + (direction > 0 ? 1 : 3)) % 4;
                if (selected == 0 && !start.interactable) selected = direction > 0 ? 1 : 3;
                nextNavigation = Time.unscaledTime + .18f;
                Select();
            }
            float axis = g?.rightStick.ReadValue().y ?? 0;
            if (k?.pageUpKey.isPressed == true || k?.upArrowKey.isPressed == true) axis = 1;
            if (k?.pageDownKey.isPressed == true || k?.downArrowKey.isPressed == true) axis = -1;
            float travel = content.rect.height - viewport.rect.height;
            if (travel > 0)
                scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + axis * Time.unscaledDeltaTime * 500 / travel);
            if ((k?.enterKey.wasPressedThisFrame == true || g?.buttonSouth.wasPressedThisFrame == true)
                && MenuInputGate.TryConsumeSubmit())
            {
                var button = Control(selected);
                if (button.interactable) button.onClick.Invoke();
            }
        }
    }
}
