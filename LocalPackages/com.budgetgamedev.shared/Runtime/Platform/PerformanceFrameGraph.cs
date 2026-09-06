using UnityEngine;
using UnityEngine.UI;

namespace BudgetGameDev.Shared
{
    /// <summary>Ten seconds of peak frame times in 50 ms bins, scrolling at up to 60 Hz.</summary>
    internal sealed class PerformanceFrameGraph : Graphic
    {
        private readonly float[] samples = new float[256];
        private readonly double[] times = new double[256];
        private int count,
            next;
        private double clock,
            binStart,
            redrawAt;
        private float peak,
            scale = 16.67f;
        internal float ScaleMilliseconds => scale;

        internal void AddFrame(double now, float milliseconds, bool visible)
        {
            double elapsed = clock > 0 ? now - clock : 0;
            clock = now;
            if (binStart == 0)
                binStart = now;
            peak = Mathf.Max(peak, milliseconds);
            if (now - binStart >= .05)
            {
                samples[next] = peak;
                times[next] = now;
                next = (next + 1) % samples.Length;
                count = Mathf.Min(count + 1, samples.Length);
                binStart = now;
                peak = 0;
            }
            float maximum = Mathf.Max(16.67f, peak);
            for (int i = 0; i < count; i++)
                if (clock - times[i] <= 10)
                    maximum = Mathf.Max(maximum, samples[i]);
            // Expand immediately for a hitch; ease downward so the axis never jumps around.
            float target = 16.67f;
            while (target < maximum)
                target *= 2;
            scale =
                target > scale ? target : Mathf.Lerp(scale, target, 1 - Mathf.Exp(-(float)elapsed));
            if (!visible || now < redrawAt)
                return;
            redrawAt = now + 1d / 60;
            SetVerticesDirty();
        }

        internal void Clear()
        {
            count = next = 0;
            peak = 0;
            clock = binStart = redrawAt = 0;
            scale = 16.67f;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            var grid = new Color(.6f, .8f, .7f, .18f);
            for (int i = 0; i <= 2; i++)
            {
                float y = rect.yMin + rect.height * i / 2;
                Line(mesh, new Vector2(rect.xMin, y), new Vector2(rect.xMax, y), grid, .6f);
            }
            Vector2 previous = default;
            bool havePrevious = false;
            for (int i = 0; i < count; i++)
            {
                int index = (next - count + i + samples.Length) % samples.Length;
                double age = clock - times[index];
                if (age > 10)
                    continue;
                var point = new Vector2(
                    rect.xMax - (float)(age / 10) * rect.width,
                    rect.yMin + Mathf.Clamp01(samples[index] / scale) * rect.height
                );
                if (havePrevious)
                {
                    ColorUtility.TryParseHtmlString(
                        PerformanceTint.FrameColor(samples[index]), out Color tint);
                    Quad(
                        mesh,
                        new Vector2(previous.x, rect.yMin),
                        previous,
                        point,
                        new Vector2(point.x, rect.yMin),
                        new Color(tint.r, tint.g, tint.b, .13f)
                    );
                    Line(mesh, previous, point, tint, 1.6f);
                }
                previous = point;
                havePrevious = true;
            }
        }

        private static void Line(VertexHelper mesh, Vector2 a, Vector2 b, Color tint, float width)
        {
            Vector2 delta = b - a;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * width * .5f;
            Quad(mesh, a - normal, a + normal, b + normal, b - normal, tint);
        }

        private static void Quad(
            VertexHelper mesh,
            Vector2 a,
            Vector2 b,
            Vector2 c,
            Vector2 d,
            Color tint
        )
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(a, tint, Vector2.zero);
            mesh.AddVert(b, tint, Vector2.zero);
            mesh.AddVert(c, tint, Vector2.zero);
            mesh.AddVert(d, tint, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
