using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Keyboard and controller navigation for the main menu. The menu drives its
    /// own selection so the d-pad walks the entries in the order they are drawn.
    /// </summary>
    public partial class MainMenu
    {
        private const float NavigationRepeatDelay = 0.35f;
        private const float NavigationRepeatInterval = 0.14f;

        private Button[] navigationOrder = Array.Empty<Button>();
        private int navigationDirection;
        private float nextNavigationTime;
        private bool suppressedNavigationEvents;

        /// <summary>
        /// Stops the EventSystem from raising its own Move/Submit events. This menu
        /// reads the gamepad directly, and the input module would otherwise deliver
        /// the very same press a second time - to whichever button the panel that
        /// just opened had selected, skipping the panel entirely.
        /// </summary>
        private void SuppressEventSystemNavigation()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || !eventSystem.sendNavigationEvents)
                return;

            eventSystem.sendNavigationEvents = false;
            suppressedNavigationEvents = true;
        }

        private void RestoreEventSystemNavigation()
        {
            if (suppressedNavigationEvents && EventSystem.current != null)
                EventSystem.current.sendNavigationEvents = true;

            suppressedNavigationEvents = false;
        }

        /// <summary>
        /// Declares the order the menu entries appear in on screen. The responsive
        /// layout builds and re-parents them in an order of its own, so neither the
        /// hierarchy nor the transform positions (which are still settling when the
        /// menu first scans) describe what the player is looking at.
        /// </summary>
        public void SetNavigationOrder(Button[] ordered)
        {
            navigationOrder = ordered ?? Array.Empty<Button>();
        }

        /// <summary>
        /// Rebuilds the navigable list over the buttons that are visible right now.
        /// </summary>
        /// <param name="rescan">Re-collect the buttons, after a panel toggled.</param>
        /// <param name="preferred">Button to start on, when it is navigable.</param>
        public void SetupControllerNavigation(bool rescan = false, Button preferred = null)
        {
            if (rescan || menuButtons == null || menuButtons.Length == 0)
            {
                menuButtons = GetComponentsInChildren<Button>(true);
            }

            ClearSelectionVisuals();

            var navigable = new List<Button>();
            foreach (Button button in menuButtons)
            {
                if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
                    continue;

                navigable.Add(button);
                RegisterButtonVisual(button);
            }

            navigable.Sort(CompareByMenuOrder);

            menuButtons = navigable.ToArray();
            selectedIndex = -1;
            navigationDirection = 0;

            int start = preferred != null ? Array.IndexOf(menuButtons, preferred) : 0;
            SelectButton(Mathf.Max(0, start), false);
        }

        private int CompareByMenuOrder(Button first, Button second)
        {
            int firstRank = RankOf(first);
            int secondRank = RankOf(second);
            return firstRank != secondRank
                ? firstRank.CompareTo(secondRank)
                : CompareByScreenPosition(first, second);
        }

        private int RankOf(Button button)
        {
            int rank = Array.IndexOf(navigationOrder, button);
            return rank < 0 ? int.MaxValue : rank;
        }

        /// <summary>Top-to-bottom, then left-to-right; fallback for undeclared buttons.</summary>
        private static int CompareByScreenPosition(Button first, Button second)
        {
            RectTransform a = first.GetComponent<RectTransform>();
            RectTransform b = second.GetComponent<RectTransform>();
            Vector3 firstPosition = a.position;
            Vector3 secondPosition = b.position;
            return !Mathf.Approximately(firstPosition.y, secondPosition.y)
                ? secondPosition.y.CompareTo(firstPosition.y)
                : firstPosition.x.CompareTo(secondPosition.x);
        }

        private void HandleMenuInput()
        {
            if (
                ResponsiveMainMenuLayout.ModalOpen
                || menuButtons == null
                || menuButtons.Length == 0
            )
            {
                navigationDirection = 0;
                return;
            }

            HandleNavigation(ReadNavigationAxis());
            HandleSubmit();
        }

        /// <summary>Vertical menu axis from keyboard, d-pad or left stick; up is positive.</summary>
        private static float ReadNavigationAxis()
        {
            return ResolveNavigationAxis(
                Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W),
                Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S),
                Gamepad.current?.dpad.ReadValue(),
                Gamepad.current?.leftStick.ReadValue()
            );
        }

        internal static float ResolveNavigationAxis(
            bool up,
            bool down,
            Vector2? dpad,
            Vector2? leftStick
        )
        {
            float vertical =
                up ? 1f
                : down ? -1f
                : 0f;

            if (dpad.HasValue)
            {
                Vector2 axis = dpad.Value;
                if (axis.sqrMagnitude < 0.25f)
                    axis = leftStick.GetValueOrDefault();
                if (Mathf.Abs(axis.y) > 0.5f)
                    vertical = Mathf.Sign(axis.y);
            }

            return vertical;
        }

        private void HandleNavigation(float vertical)
        {
            if (Mathf.Abs(vertical) < 0.5f)
            {
                navigationDirection = 0;
                return;
            }

            int direction = vertical > 0f ? -1 : 1;
            if (direction != navigationDirection)
            {
                // A fresh press always steps immediately, so a short d-pad tap is
                // never swallowed by the auto-repeat cooldown of an earlier one.
                navigationDirection = direction;
                nextNavigationTime = Time.unscaledTime + NavigationRepeatDelay;
            }
            else if (Time.unscaledTime < nextNavigationTime)
            {
                return;
            }
            else
            {
                nextNavigationTime = Time.unscaledTime + NavigationRepeatInterval;
            }

            int count = menuButtons.Length;
            SelectButton(((selectedIndex + direction) % count + count) % count);
        }

        private void HandleSubmit()
        {
            bool pressed =
                Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.Space)
                || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);

            HandleSubmit(pressed);
        }

        internal void HandleSubmit(bool pressed)
        {
            if (!pressed || selectedIndex < 0 || selectedIndex >= menuButtons.Length)
                return;

            SubmitSelected();
        }

        internal void SubmitSelected()
        {
            Button button = menuButtons[selectedIndex];
            if (button == null || !button.interactable || !MenuInputGate.TryConsumeSubmit())
                return;

            button.onClick.Invoke();
        }
    }
}
