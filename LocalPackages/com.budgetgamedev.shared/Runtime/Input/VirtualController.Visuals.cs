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
        private void Start()
        {
            bool isMobile = IsMobilePlatform();

            // Always setup pause button (it handles its own visibility)
            SetupPauseButton();
            SetupPauseButtonVisual();

            // Clear cached textures to ensure colors are current (important after code changes)
            cachedRingTexture = null;
            cachedHandleTexture = null;
            cachedPauseButtonTexture = null;

            if (isMobile)
            {
                SetupActionButton();
                SetupJoystickVisuals();
                wasPortrait = Screen.height > Screen.width;
                lastSafeArea = Screen.safeArea;
                UpdateLayoutForOrientation();
            }
        }

        private void SetupJoystickVisuals()
        {
            // Create and apply ring sprite for background
            if (joystickBackground != null)
            {
                Image bgImage = joystickBackground.GetComponent<Image>();
                if (bgImage != null)
                {
                    if (cachedRingTexture == null)
                    {
                        cachedRingTexture = CreateRingTexture(
                            128,
                            ringThickness,
                            ringColor,
                            fillColor
                        );
                    }
                    Sprite ringSprite = Sprite.Create(
                        cachedRingTexture,
                        new Rect(0, 0, 128, 128),
                        new Vector2(0.5f, 0.5f),
                        100f
                    );
                    bgImage.sprite = ringSprite;
                    bgImage.type = Image.Type.Simple;
                    bgImage.color = Color.white; // Use white to show texture colors as-is
                }
            }

            // Create and apply circle sprite for handle - make it visually distinct
            if (joystickHandle != null)
            {
                Image handleImage = joystickHandle.GetComponent<Image>();
                if (handleImage != null)
                {
                    if (cachedHandleTexture == null)
                    {
                        cachedHandleTexture = CreateCircleTexture(
                            64,
                            handleColor,
                            handleBorderColor
                        );
                    }
                    Sprite handleSprite = Sprite.Create(
                        cachedHandleTexture,
                        new Rect(0, 0, 64, 64),
                        new Vector2(0.5f, 0.5f),
                        100f
                    );
                    handleImage.sprite = handleSprite;
                    handleImage.type = Image.Type.Simple;
                    handleImage.color = Color.white; // Use white to show texture colors as-is

                    // Ensure handle is rendered on top of background
                    handleImage.raycastTarget = false;
                }
            }
        }

        private Texture2D CreateRingTexture(int size, float thickness, Color ringCol, Color fillCol)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            float center = size / 2f;
            float outerRadius = center - 2f;
            float innerRadius = outerRadius - thickness;

            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    if (distance > outerRadius + 1f)
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                    else if (distance > outerRadius - 1f)
                    {
                        float alpha = Mathf.Clamp01(outerRadius + 1f - distance);
                        pixels[y * size + x] = new Color(
                            ringCol.r,
                            ringCol.g,
                            ringCol.b,
                            ringCol.a * alpha
                        );
                    }
                    else if (distance > innerRadius + 1f)
                    {
                        pixels[y * size + x] = ringCol;
                    }
                    else if (distance > innerRadius - 1f)
                    {
                        float t = Mathf.Clamp01(distance - innerRadius + 1f);
                        pixels[y * size + x] = Color.Lerp(fillCol, ringCol, t);
                    }
                    else
                    {
                        pixels[y * size + x] = fillCol;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private Texture2D CreateCircleTexture(int size, Color col, Color borderCol)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            float center = size / 2f;
            float radius = center - 2f;
            float borderWidth = 3f; // Border thickness in pixels
            float innerRadius = radius - borderWidth;

            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    if (distance > radius + 1f)
                    {
                        // Outside circle - transparent
                        pixels[y * size + x] = Color.clear;
                    }
                    else if (distance > radius - 1f)
                    {
                        // Outer edge anti-aliasing
                        float alpha = Mathf.Clamp01(radius + 1f - distance);
                        pixels[y * size + x] = new Color(
                            borderCol.r,
                            borderCol.g,
                            borderCol.b,
                            borderCol.a * alpha
                        );
                    }
                    else if (distance > innerRadius)
                    {
                        // Border region
                        pixels[y * size + x] = borderCol;
                    }
                    else if (distance > innerRadius - 1f)
                    {
                        // Inner edge anti-aliasing (border to fill transition)
                        float t = Mathf.Clamp01(innerRadius - distance + 1f);
                        pixels[y * size + x] = Color.Lerp(borderCol, col, t);
                    }
                    else
                    {
                        // Inner fill
                        pixels[y * size + x] = col;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
