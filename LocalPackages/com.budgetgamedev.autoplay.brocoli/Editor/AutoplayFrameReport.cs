using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Editor
{
    /// <summary>
    /// Reads the captured frames back and reports mean luminance in top, middle, and
    /// bottom screen bands. Lighting regressions do not raise exceptions -- a run
    /// that has gone completely black, or blown out to white, still passes every
    /// other check -- so the harness measures the picture rather than trusting it.
    /// </summary>
    internal static class AutoplayFrameReport
    {
        /// <summary>Frames sampled evenly across the run; enough to see a trend.</summary>
        private const int Samples = 8;

        internal static string Describe(string framesDir)
        {
            string[] frames = Directory.Exists(framesDir)
                ? Directory.GetFiles(framesDir, "*.png").OrderBy(path => path).ToArray()
                : Array.Empty<string>();
            if (frames.Length == 0)
                return $"[Autoplay] No frames were captured in {framesDir}.";

            var lines = new List<string>
            {
                "[Autoplay] Mean luminance by screen band (0 = black, 1 = white):",
                $"  {"frame", -20}{"top", 8}{"mid", 8}{"bottom", 8}",
            };
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                foreach (int index in SampleIndices(frames.Length))
                    lines.Add(DescribeFrame(frames[index], texture));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>Evenly spaced frame indices, de-duplicated for very short runs.</summary>
        internal static IEnumerable<int> SampleIndices(int count)
        {
            if (count <= 1)
                return new[] { 0 };

            var indices = new SortedSet<int>();
            for (int step = 0; step < Samples; step++)
                indices.Add(Mathf.RoundToInt(step * (count - 1) / (float)(Samples - 1)));
            return indices;
        }

        private static string DescribeFrame(string path, Texture2D texture)
        {
            string name = Path.GetFileName(path);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false))
                return $"  {name, -20}{"unreadable", 24}";

            Color32[] pixels = texture.GetPixels32();
            var invariant = CultureInfo.InvariantCulture;

            // Rows run bottom-up in Unity textures, so the last third is the top band.
            return $"  {name, -20}"
                + $"{BandLuminance(pixels, texture, 2f / 3f, 1f).ToString("0.000", invariant), 8}"
                + $"{BandLuminance(pixels, texture, 1f / 3f, 2f / 3f).ToString("0.000", invariant), 8}"
                + $"{BandLuminance(pixels, texture, 0f, 1f / 3f).ToString("0.000", invariant), 8}";
        }

        internal static float BandLuminance(
            IReadOnlyList<Color32> pixels,
            Texture2D texture,
            float from,
            float to
        )
        {
            int first = Mathf.Clamp(Mathf.RoundToInt(texture.height * from), 0, texture.height);
            int last = Mathf.Clamp(Mathf.RoundToInt(texture.height * to), first, texture.height);
            int start = first * texture.width;
            int end = last * texture.width;
            if (end <= start)
                return 0f;

            double total = 0d;
            for (int index = start; index < end && index < pixels.Count; index++)
                total += Luminance(pixels[index]);
            return (float)(total / (end - start));
        }

        private static double Luminance(Color32 pixel) =>
            (0.299d * pixel.r + 0.587d * pixel.g + 0.114d * pixel.b) / 255d;
    }
}
