using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using static BudgetGameDev.Shared.MenuTheme;

namespace BudgetGameDev.Shared
{
    public sealed partial class NvidiaSettingsPage
    {
        public void Layout(float width, float top, float bottom, bool compact, bool narrow)
        {
            SetCenteredRect(panel, width, Mathf.Max(120, top - bottom), (top + bottom) * 0.5f);
            float height = panel.rect.height;
            float gap = compact ? 6 : 10;
            float headingHeight = compact ? 24 : 30;
            float controlHeight = compact ? 42 : 50;
            float footerHeight = compact ? 32 : 42;
            SetTopAnchored(heading.rectTransform, 0, width, headingHeight);
            float buttonWidth = (width - 2 * gap) / 3;
            for (int i = 0; i < 3; i++)
            {
                var rect = (RectTransform)controls[i].transform;
                SetTopAnchored(rect, headingHeight + gap, buttonWidth, controlHeight);
                rect.anchoredPosition += Vector2.right * (i - 1) * (buttonWidth + gap);
                rect = (RectTransform)controls[i + 3].transform;
                SetTopAnchored(rect, height - footerHeight, buttonWidth, footerHeight);
                rect.anchoredPosition += Vector2.right * (i - 1) * (buttonWidth + gap);
            }
            float inset = headingHeight + gap + controlHeight + gap;
            SetTopAnchored(
                viewport,
                inset,
                width,
                Mathf.Max(20, height - inset - footerHeight - gap)
            );
            report.fontSize = narrow ? 12 : 14;
            ResizeContent();
        }

        private void ResizeContent()
        {
            if (viewport == null || report == null)
                return;
            float offset = content.anchoredPosition.y;
            float height =
                report
                    .GetPreferredValues(
                        report.text,
                        Mathf.Max(30, viewport.rect.width),
                        Mathf.Infinity
                    )
                    .y + 20;
            content.sizeDelta = new Vector2(0, Mathf.Max(viewport.rect.height, height));
            content.anchoredPosition = new Vector2(
                0,
                Mathf.Clamp(offset, 0, Mathf.Max(0, height - viewport.rect.height))
            );
        }

        private void SelectControl(int index)
        {
            for (int i = 0; i < controls.Length; i++)
            {
                int next = (index + i + controls.Length) % controls.Length;
                if (!controls[next].interactable)
                    continue;
                selection = next;
                EventSystem.current?.SetSelectedGameObject(controls[next].gameObject);
                return;
            }
        }

        private void HandleInput()
        {
            if (Time.frameCount == openedFrame)
                return;
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            bool cancel =
                keyboard?.escapeKey.wasPressedThisFrame == true
                || gamepad?.buttonEast.wasPressedThisFrame == true;
            if (cancel && consumeCancel())
            {
                Close();
                return;
            }
            Vector2 axis = gamepad?.dpad.ReadValue() ?? Vector2.zero;
            if (axis.sqrMagnitude < 0.25f)
                axis = gamepad?.leftStick.ReadValue() ?? Vector2.zero;
            if (keyboard?.leftArrowKey.isPressed == true)
                axis.x = -1;
            if (keyboard?.rightArrowKey.isPressed == true)
                axis.x = 1;
            if (keyboard?.upArrowKey.isPressed == true)
                axis.y = 1;
            if (keyboard?.downArrowKey.isPressed == true)
                axis.y = -1;
            if (Time.unscaledTime >= nextNavigation && axis.sqrMagnitude > 0.25f)
            {
                int delta =
                    Mathf.Abs(axis.x) > 0.5f ? (axis.x > 0 ? 1 : -1) : (axis.y > 0 ? -3 : 3);
                SelectControl(selection + delta);
                nextNavigation = Time.unscaledTime + 0.18f;
            }
            float scrollAxis = gamepad?.rightStick.ReadValue().y ?? 0;
            if (keyboard?.pageUpKey.isPressed == true)
                scrollAxis = 1;
            if (keyboard?.pageDownKey.isPressed == true)
                scrollAxis = -1;
            float travel = content.rect.height - viewport.rect.height;
            if (travel > 0)
                scroll.verticalNormalizedPosition = Mathf.Clamp01(
                    scroll.verticalNormalizedPosition
                        + scrollAxis * Time.unscaledDeltaTime * 500 / travel
                );
            bool submit =
                keyboard?.enterKey.wasPressedThisFrame == true
                || keyboard?.spaceKey.wasPressedThisFrame == true
                || gamepad?.buttonSouth.wasPressedThisFrame == true;
            if (submit && controls[selection].interactable && consumeSubmit())
                controls[selection].onClick.Invoke();
        }
    }
}
