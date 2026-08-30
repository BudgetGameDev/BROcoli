using BudgetGameDev.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Non-pausing inventory and exploration map. It deliberately reads input
    /// without changing time scale, so movement and combat continue underneath.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class ExplorationOverlay : MonoBehaviour
    {
        internal enum Pane
        {
            Inventory,
            Map,
        }

        private const string LastPanePreference = "Brocoli.LastExplorationPane";
        private const float ControllerPanSpeed = 2.8f;
        private const float ControllerZoomSpeed = 1.25f;

        private static ExplorationOverlay instance;
        private RectTransform overlayRoot;
        private RectTransform safeArea;
        private RectTransform card;
        private RectTransform inventoryPanel;
        private RectTransform mapPanel;
        private TMP_Text title;
        private TMP_Text mapStatus;
        private TMP_Text footer;
        private DungeonMapGraphic mapGraphic;
        private Button touchInventoryButton;
        private Button touchMapButton;
        private VirtualController virtualController;
        private Pane activePane;
        private Rect lastSafeArea;
        private Vector2 lastRootSize;
        private float nextInventoryRefresh;

        public bool IsOpen => overlayRoot != null && overlayRoot.gameObject.activeSelf;
        internal Pane ActivePane => activePane;

        public static ExplorationOverlay EnsurePresent()
        {
            if (instance != null)
                return instance;

            Canvas canvas = ScreenCanvasLocator.Find();
            if (canvas == null)
                return null;

            ExplorationOverlay existing = canvas.GetComponent<ExplorationOverlay>();
            return existing != null
                ? existing
                : canvas.gameObject.AddComponent<ExplorationOverlay>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
            activePane = ReadRememberedPane();
            BuildInterface();
            ApplyResponsiveLayout(true);
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void Update()
        {
            ApplyResponsiveLayout(false);
            UpdateTouchButtonVisibility();
            HandleGlobalInput();

            if (!IsOpen)
                return;

            if (activePane == Pane.Map)
            {
                HandleMapControllerInput();
                mapGraphic?.RefreshFromDungeon();
                UpdateMapStatus();
            }
            else
            {
                HandleInventoryNavigationInput();
                if (Time.unscaledTime >= nextInventoryRefresh)
                {
                    nextInventoryRefresh = Time.unscaledTime + 0.2f;
                    UpdateInventory();
                }
            }
        }

        private void HandleGlobalInput()
        {
            if (Input.GetKeyDown(KeyCode.M))
            {
                TogglePane(Pane.Map);
                return;
            }

            if (Input.GetKeyDown(KeyCode.I))
            {
                TogglePane(Pane.Inventory);
                return;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null && gamepad.selectButton.wasPressedThisFrame)
            {
                if (IsOpen)
                    Close();
                else
                    Open(Pane.Inventory);
                return;
            }

            if (!IsOpen)
                return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            if (gamepad != null)
            {
                if (gamepad.leftShoulder.wasPressedThisFrame)
                    SwitchPane(-1);
                else if (gamepad.rightShoulder.wasPressedThisFrame)
                    SwitchPane(1);
            }
        }

        private void HandleMapControllerInput()
        {
            Gamepad gamepad = Gamepad.current;
            if (gamepad == null || mapGraphic == null)
                return;

            Vector2 pan = gamepad.rightStick.ReadValue();
            if (pan.sqrMagnitude > 0.04f)
                mapGraphic.Pan(pan * (ControllerPanSpeed * Time.unscaledDeltaTime));

            float zoom = gamepad.rightTrigger.ReadValue() - gamepad.leftTrigger.ReadValue();
            if (Mathf.Abs(zoom) > 0.08f)
                mapGraphic.ZoomBy(zoom * ControllerZoomSpeed * Time.unscaledDeltaTime);
        }

        private void TogglePane(Pane pane)
        {
            if (IsOpen && activePane == pane)
                Close();
            else
                Open(pane);
        }

        private void Open(Pane pane)
        {
            if (overlayRoot == null || Time.timeScale <= 0f)
                return;

            overlayRoot.gameObject.SetActive(true);
            overlayRoot.SetAsLastSibling();
            ShowPane(pane);

            // The inventory's full-screen surfaces do not block raycasts, so the
            // movement controller remains usable below it. Only the two compact
            // overlay toggles need to render above the presentation.
            if (touchInventoryButton != null)
                touchInventoryButton.transform.SetAsLastSibling();
            if (touchMapButton != null)
                touchMapButton.transform.SetAsLastSibling();
        }

        public void Close()
        {
            if (overlayRoot != null)
                overlayRoot.gameObject.SetActive(false);
            ResetInventoryNavigationRepeat();
        }

        private void SwitchPane(int direction)
        {
            Pane next = direction < 0 ? PreviousPane(activePane) : NextPane(activePane);
            ShowPane(next);
        }

        private void ShowPane(Pane pane)
        {
            activePane = pane;
            ApplyResponsiveLayout(true);
            PlayerPrefs.SetInt(LastPanePreference, (int)pane);
            inventoryPanel?.gameObject.SetActive(pane == Pane.Inventory);
            mapPanel?.gameObject.SetActive(pane == Pane.Map);

            if (title != null)
                title.text = pane == Pane.Map ? "EXPLORATION MAP" : "INVENTORY";
            if (footer != null)
            {
                footer.text =
                    pane == Pane.Map
                        ? "M / SELECT  CLOSE     LB / RB  SWITCH     DRAG / RIGHT STICK  PAN     SCROLL / TRIGGERS  ZOOM"
                        : "I / SELECT  CLOSE     ARROWS / D-PAD  NAVIGATE     WASD / LEFT STICK  MOVE";
            }

            if (pane == Pane.Map)
            {
                mapGraphic?.FocusPlayer();
                mapGraphic?.RefreshFromDungeon(true);
                UpdateMapStatus();
            }
            else
            {
                EnsureInventorySelection();
                UpdateInventory();
            }
        }

        private static Pane ReadRememberedPane()
        {
            int value = PlayerPrefs.GetInt(LastPanePreference, (int)Pane.Inventory);
            return value == (int)Pane.Map ? Pane.Map : Pane.Inventory;
        }

        internal static Pane NextPane(Pane pane) =>
            pane == Pane.Inventory ? Pane.Map : Pane.Inventory;

        internal static Pane PreviousPane(Pane pane) => NextPane(pane);
    }
}
