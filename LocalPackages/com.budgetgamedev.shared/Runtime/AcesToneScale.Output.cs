using UnityEngine;
using UnityEngine.Rendering;

namespace BudgetGameDev.Shared
{
    public static partial class AcesToneScale
    {
        // Knot positions, in OCES, of the output device transforms. ACES defines them as the tone
        // scale of 0.18 at -12, 0 and +10/+11/+12 stops; they are written out so this file does
        // not depend on static initialisation order. AcesToneScaleTests re-derives them.
        private const float OutputMinOces = 0.000141798689f;
        private const float OutputMidOces = 4.8f;
        private const float OutputMaxOces1000 = 4505.0795f;
        private const float OutputMaxOces2000 = 5771.8607f;
        private const float OutputMaxOces4000 = 6824.3621f;

        private static readonly float[] OutputLowCoefficients =
        {
            -4.9706219331f,
            -3.0293780669f,
            -2.1262f,
            -1.5105f,
            -1.0578f,
            -0.4668f,
            0.11938f,
            0.7088134201f,
            1.2911865799f,
            1.2911865799f,
        };

        private static readonly float[] OutputHighCoefficients1000 =
        {
            0.8089132070f,
            1.1910867930f,
            1.5683f,
            1.9483f,
            2.3083f,
            2.6384f,
            2.8595f,
            2.9872608805f,
            3.0127391195f,
            3.0127391195f,
        };

        private static readonly float[] OutputHighCoefficients2000 =
        {
            0.8019952042f,
            1.1980047958f,
            1.5943f,
            1.9973f,
            2.3783f,
            2.7684f,
            3.0515f,
            3.2746293562f,
            3.3274306351f,
            3.3274306351f,
        };

        private static readonly float[] OutputHighCoefficients4000 =
        {
            0.7973186613f,
            1.2026813387f,
            1.6093f,
            2.0108f,
            2.4148f,
            2.8179f,
            3.1725f,
            3.5344995451f,
            3.6696204376f,
            3.6696204376f,
        };

        /// <summary>
        /// The HDR output device transform: a nine-knot B-spline over log luminance mapping OCES
        /// to display nits. All three presets share their dark and mid tones and differ only in
        /// how far the shoulder reaches.
        /// </summary>
        private static float OutputDeviceTransform(float oces, HDRRangeReduction preset)
        {
            float[] high;
            float maxX;
            float maxNits;
            float slopeHigh;
            switch (preset)
            {
                case HDRRangeReduction.ACES2000Nits:
                    high = OutputHighCoefficients2000;
                    maxX = OutputMaxOces2000;
                    maxNits = 2000f;
                    slopeHigh = 0.12f;
                    break;
                case HDRRangeReduction.ACES4000Nits:
                    high = OutputHighCoefficients4000;
                    maxX = OutputMaxOces4000;
                    maxNits = 4000f;
                    slopeHigh = 0.3f;
                    break;
                default:
                    high = OutputHighCoefficients1000;
                    maxX = OutputMaxOces1000;
                    maxNits = 1000f;
                    slopeHigh = 0.06f;
                    break;
            }

            float logX = Mathf.Log10(Mathf.Max(oces, 1e-4f));
            float logMin = Mathf.Log10(OutputMinOces);
            float logMid = Mathf.Log10(OutputMidOces);
            float logMax = Mathf.Log10(maxX);
            if (logX <= logMin)
            {
                const float SlopeLow = 3f;
                return Mathf.Pow(
                    10f,
                    (logX * SlopeLow) + (Mathf.Log10(0.0001f) - (SlopeLow * logMin))
                );
            }
            if (logX >= logMax)
                return Mathf.Pow(
                    10f,
                    (logX * slopeHigh) + (Mathf.Log10(maxNits) - (slopeHigh * logMax))
                );
            return logX < logMid
                ? EvaluateSpline(OutputLowCoefficients, 7, logX, logMin, logMid)
                : EvaluateSpline(high, 7, logX, logMid, logMax);
        }
    }
}
