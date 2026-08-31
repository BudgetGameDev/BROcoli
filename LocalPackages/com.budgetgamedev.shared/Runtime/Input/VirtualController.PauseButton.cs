using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace BudgetGameDev.Shared
{
    public partial class VirtualController
    {
        private void SetupPauseButton()
        {
            if (pauseButton != null)
            {
                // Connect to whatever pause screen this game provides
                IPauseController pauseMenu = PauseControllerLocator.Find();
                if (pauseMenu != null)
                {
                    pauseButton.onClick.RemoveAllListeners();
                    pauseButton.onClick.AddListener(() => pauseMenu.TogglePause());
                    Debug.Log(
                        "[VirtualController] Pause button connected to the game's pause screen"
                    );
                }
                else
                {
                    Debug.LogWarning(
                        "[VirtualController] No IPauseController in scene - pause button won't work"
                    );
                }
            }
        }

        private void SetupPauseButtonVisual()
        {
            if (pauseButton == null)
                return;

            Image buttonImage = pauseButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                if (cachedPauseButtonTexture == null)
                {
                    cachedPauseButtonTexture = CreatePauseIconTexture(
                        64,
                        pauseButtonColor,
                        pauseIconColor
                    );
                }
                Sprite pauseSprite = Sprite.Create(
                    cachedPauseButtonTexture,
                    new Rect(0, 0, 64, 64),
                    new Vector2(0.5f, 0.5f),
                    100f
                );
                buttonImage.sprite = pauseSprite;
                buttonImage.type = Image.Type.Simple;
                buttonImage.color = Color.white;
            }

            // Hide any text child (we use icon instead)
            TMPro.TMP_Text textComponent = pauseButton.GetComponentInChildren<TMPro.TMP_Text>();
            if (textComponent != null)
            {
                textComponent.gameObject.SetActive(false);
            }
            // Also check for legacy Text
            Text legacyText = pauseButton.GetComponentInChildren<Text>();
            if (legacyText != null)
            {
                legacyText.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Creates a circular pause button with pause icon (two vertical bars)
        /// </summary>
        private Texture2D CreatePauseIconTexture(int size, Color bgColor, Color iconColor)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            float center = size / 2f;
            float radius = center - 2f;

            // Pause icon dimensions (two vertical bars)
            float barWidth = size * 0.12f;
            float barHeight = size * 0.4f;
            float barSpacing = size * 0.12f; // Space between bars
            float barLeft1 = center - barSpacing - barWidth;
            float barRight1 = center - barSpacing;
            float barLeft2 = center + barSpacing;
            float barRight2 = center + barSpacing + barWidth;
            float barTop = center + barHeight / 2f;
            float barBottom = center - barHeight / 2f;

            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    // Check if inside circle
                    if (distance > radius + 1f)
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                    else if (distance > radius - 1f)
                    {
                        // Anti-aliased edge
                        float alpha = Mathf.Clamp01(radius + 1f - distance);
                        pixels[y * size + x] = new Color(
                            bgColor.r,
                            bgColor.g,
                            bgColor.b,
                            bgColor.a * alpha
                        );
                    }
                    else
                    {
                        // Inside circle - check if on pause icon bars
                        bool onBar1 =
                            x >= barLeft1 && x <= barRight1 && y >= barBottom && y <= barTop;
                        bool onBar2 =
                            x >= barLeft2 && x <= barRight2 && y >= barBottom && y <= barTop;

                        if (onBar1 || onBar2)
                        {
                            // Add slight anti-aliasing at bar edges
                            float edgeAA = 1f;
                            if (onBar1)
                            {
                                float distToEdge = Mathf.Min(
                                    x - barLeft1,
                                    barRight1 - x,
                                    y - barBottom,
                                    barTop - y
                                );
                                edgeAA = Mathf.Clamp01(distToEdge);
                            }
                            else
                            {
                                float distToEdge = Mathf.Min(
                                    x - barLeft2,
                                    barRight2 - x,
                                    y - barBottom,
                                    barTop - y
                                );
                                edgeAA = Mathf.Clamp01(distToEdge);
                            }
                            pixels[y * size + x] = Color.Lerp(bgColor, iconColor, edgeAA);
                        }
                        else
                        {
                            pixels[y * size + x] = bgColor;
                        }
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
