using UnityEngine;

namespace BudgetGameDev.Shared
{
    public sealed partial class GameCursor
    {
        /// <summary>
        /// A pointer's look: which image the hardware cursor is drawn from, and what colour
        /// that image is recoloured to. Where it points is not part of this, because it is
        /// measured from the image itself.
        /// </summary>
        public readonly struct PointerArt
        {
            /// <summary>A <c>Resources</c> path, namespaced by the owning game.</summary>
            public readonly string PointerResource;

            /// <summary>
            /// What the source image's lit pixels are recoloured to. A hardware cursor cannot
            /// be tinted as it is drawn, so this is baked into a copy of the texture at load.
            /// </summary>
            public readonly Color Tint;

            public PointerArt(string pointerResource, Color tint)
            {
                PointerResource = pointerResource;
                Tint = tint;
            }

            public bool IsEmpty => string.IsNullOrEmpty(PointerResource);
        }

        /// <summary>
        /// Cursor art is antialiased, so its outermost pixels are nearly transparent and belong
        /// to the edge rather than to the point. This is the alpha at which a pixel counts as
        /// drawn.
        /// </summary>
        internal const float PointAlphaThreshold = 0.47f;

        private static PointerArt art;
        private static Texture2D tintedPointer;

        /// <summary>
        /// Chooses the pointer's look and applies it. Swapping in different art is this call
        /// and nothing else: the hotspot comes from the new image, so a cursor replaced with a
        /// differently shaped one still clicks where it points.
        /// </summary>
        public static void SetArt(PointerArt pointerArt)
        {
            art = pointerArt;
            DisposeTintedPointer();
            instance?.ApplyHardwarePointer();
        }

        public static PointerArt Art => art;

        private static void ResetArt()
        {
            art = default;
            DisposeTintedPointer();
        }

        private static void DisposeTintedPointer()
        {
            if (tintedPointer == null)
                return;

            Destroy(tintedPointer);
            tintedPointer = null;
        }

        private void ApplyHardwarePointer()
        {
            Texture2D pointer = art.IsEmpty ? null : GetTintedPointer();
            if (pointer == null)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                return;
            }

            Cursor.SetCursor(pointer, MeasureHotspot(pointer), CursorMode.Auto);
        }

        private static Texture2D GetTintedPointer()
        {
            if (tintedPointer != null)
                return tintedPointer;

            Texture2D source = Resources.Load<Texture2D>(art.PointerResource);
            if (source == null)
            {
                Debug.LogWarning($"GameCursor: no pointer texture at {art.PointerResource}");
                return null;
            }

            tintedPointer = Tint(source, art.Tint);
            return tintedPointer;
        }

        /// <summary>
        /// Where a cursor points, in pixels from the image's top-left corner, which is what
        /// Unity wants and where a click will land.
        ///
        /// It is measured rather than authored so that replacing the art is the whole job of
        /// changing the pointer. The point is taken as the solid pixel nearest the top-left
        /// corner, not the corner of the drawing's bounding box: a diagonal arrow reaches the
        /// box's left edge and its top edge at two different pixels, and neither of them is
        /// the tip. This assumes a cursor that points up and left, as pointers conventionally
        /// do; a crosshair or a resize handle would need its own answer.
        /// </summary>
        internal static Vector2 MeasureHotspot(Texture2D pointer)
        {
            if (pointer == null)
                return Vector2.zero;

            Color32[] pixels = pointer.GetPixels32();
            int width = pointer.width;
            int height = pointer.height;
            byte threshold = (byte)Mathf.RoundToInt(PointAlphaThreshold * 255f);

            int bestX = 0;
            int bestY = 0;
            long bestDistance = long.MaxValue;

            for (int row = 0; row < height; row++)
            {
                // Texture rows run bottom-up while a hotspot is measured from the top.
                int y = height - 1 - row;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[(row * width) + x].a < threshold)
                        continue;

                    long distance = ((long)x * x) + ((long)y * y);
                    if (distance >= bestDistance)
                        continue;

                    bestDistance = distance;
                    bestX = x;
                    bestY = y;
                }
            }

            return bestDistance == long.MaxValue ? Vector2.zero : new Vector2(bestX, bestY);
        }

        /// <summary>
        /// Recolours a pointer image. The source art is drawn in greys, so multiplying keeps
        /// its shading and its dark outline while moving the lit part onto the wanted colour;
        /// alpha is left exactly as drawn, because that is the pointer's shape.
        /// </summary>
        internal static Texture2D Tint(Texture2D source, Color tint)
        {
            Texture2D tinted = new(source.width, source.height, TextureFormat.RGBA32, false)
            {
                name = source.name + " (Tinted)",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            Color[] pixels = source.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                pixels[i] = new Color(
                    pixel.r * tint.r,
                    pixel.g * tint.g,
                    pixel.b * tint.b,
                    pixel.a
                );
            }

            tinted.SetPixels(pixels);
            tinted.Apply(false, false);
            return tinted;
        }
    }
}
