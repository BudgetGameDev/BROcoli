using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BudgetGameDev.Shared
{
    public static partial class AcesToneScale
    {
        // Pre-transposed ACES matrices, matching the ones URP's ACES.hlsl renders with.
        private static readonly Matrix4x4 SRgbToAp1 = FromRows(
            new Vector3(0.61319f, 0.33951f, 0.04737f),
            new Vector3(0.07021f, 0.91634f, 0.01345f),
            new Vector3(0.02062f, 0.10957f, 0.86961f)
        );

        private static readonly Matrix4x4 Ap1ToAp0 = FromRows(
            new Vector3(0.6954522414f, 0.1406786965f, 0.1638690622f),
            new Vector3(0.0447945634f, 0.8596711185f, 0.0955343182f),
            new Vector3(-0.0055258826f, 0.0040252103f, 1.0015006723f)
        );

        private static readonly Matrix4x4 Ap0ToAp1 = FromRows(
            new Vector3(1.4514393161f, -0.2365107469f, -0.2149285693f),
            new Vector3(-0.0765537734f, 1.1762296998f, -0.0996759264f),
            new Vector3(0.0083161484f, -0.0060324498f, 0.9977163014f)
        );

        private static readonly Matrix4x4 Ap1ToXyz = FromRows(
            new Vector3(0.6624541811f, 0.1340042065f, 0.1561876870f),
            new Vector3(0.2722287168f, 0.6740817658f, 0.0536895174f),
            new Vector3(-0.0055746495f, 0.0040607335f, 1.0103391003f)
        );

        private static readonly Matrix4x4 XyzToRec709 = FromRows(
            new Vector3(3.2409699419f, -1.5373831776f, -0.4986107603f),
            new Vector3(-0.9692436363f, 1.8759675015f, 0.0415550574f),
            new Vector3(0.0556300797f, -0.2039769589f, 1.0569715142f)
        );

        private static readonly Matrix4x4 D60ToD65 = FromRows(
            new Vector3(0.98722400f, -0.00611327f, 0.0159533f),
            new Vector3(-0.00759836f, 1.00186000f, 0.0053302f),
            new Vector3(0.00307257f, -0.00509595f, 1.0816800f)
        );

        private static readonly Vector3 Ap1Luminance = new(0.272229f, 0.674082f, 0.0536895f);

        private const float RrtGlowGain = 0.05f;
        private const float RrtGlowMid = 0.08f;
        private const float RrtRedScale = 0.82f;
        private const float RrtRedPivot = 0.03f;
        private const float RrtRedWidth = 135f;
        private const float RrtSaturationFactor = 0.96f;

        /// <summary>
        /// The reference rendering transform: scene-referred ACES2065-1 to output-referred OCES.
        /// </summary>
        private static Vector3 ReferenceRenderingTransform(Vector3 aces)
        {
            float saturation = RgbToSaturation(aces);
            float glow = GlowForward(
                RgbToYc(aces),
                RrtGlowGain * SigmoidShaper((saturation - 0.4f) / 0.2f),
                RrtGlowMid
            );
            aces *= 1f + glow;

            float centeredHue = CenterHue(RgbToHue(aces), 0f);
            float hueWeight = Mathf.SmoothStep(
                0f,
                1f,
                1f - Mathf.Abs(2f * centeredHue / RrtRedWidth)
            );
            hueWeight *= hueWeight;
            aces.x += hueWeight * saturation * (RrtRedPivot - aces.x) * (1f - RrtRedScale);

            Vector3 rgbPre = Positive(Transform(Ap0ToAp1, Positive(aces)));
            float luminance = Vector3.Dot(rgbPre, Ap1Luminance);
            rgbPre =
                new Vector3(luminance, luminance, luminance)
                + RrtSaturationFactor * (rgbPre - new Vector3(luminance, luminance, luminance));

            Vector3 rgbPost = new(ToneScale(rgbPre.x), ToneScale(rgbPre.y), ToneScale(rgbPre.z));
            return Transform(Ap1ToAp0, rgbPost);
        }

        /// <summary>The RRT tone scale, a five-knot B-spline over log luminance.</summary>
        private static float ToneScale(float x)
        {
            const float MinX = 0.18f * (1f / 32768f); // 0.18 * 2^-15
            const float MidX = 0.18f;
            const float MaxX = 0.18f * 262144f; // 0.18 * 2^18
            float[] low = ToneScaleLowCoefficients;
            float[] high = ToneScaleHighCoefficients;

            float logX = Mathf.Log10(Mathf.Max(x, 1e-10f));
            if (logX <= Mathf.Log10(MinX))
                return 0.0001f;
            if (logX >= Mathf.Log10(MaxX))
                return 10000f;
            return logX < Mathf.Log10(MidX)
                ? EvaluateSpline(low, 3, logX, Mathf.Log10(MinX), Mathf.Log10(MidX))
                : EvaluateSpline(high, 3, logX, Mathf.Log10(MidX), Mathf.Log10(MaxX));
        }

        private static readonly float[] ToneScaleLowCoefficients =
        {
            -4f,
            -4f,
            -3.1573765773f,
            -0.4852499958f,
            1.8477324706f,
            1.8477324706f,
        };

        private static readonly float[] ToneScaleHighCoefficients =
        {
            -0.7185482425f,
            2.0810307172f,
            3.6681241237f,
            4f,
            4f,
            4f,
        };

        /// <summary>Evaluates one half of a segmented B-spline and returns the linear result.</summary>
        private static float EvaluateSpline(
            float[] coefficients,
            int spans,
            float logX,
            float logStart,
            float logEnd
        )
        {
            float knot = spans * (logX - logStart) / (logEnd - logStart);
            int index = Mathf.Clamp((int)knot, 0, spans - 1);
            float t = knot - index;
            float c0 = coefficients[index];
            float c1 = coefficients[index + 1];
            float c2 = coefficients[index + 2];
            float logY =
                (t * t * (0.5f * c0 - c1 + 0.5f * c2)) + (t * (c1 - c0)) + (0.5f * (c0 + c1));
            return Mathf.Pow(10f, logY);
        }

        private static float RgbToSaturation(Vector3 rgb)
        {
            const float Tiny = 1e-4f;
            float minimum = Mathf.Min(rgb.x, Mathf.Min(rgb.y, rgb.z));
            float maximum = Mathf.Max(rgb.x, Mathf.Max(rgb.y, rgb.z));
            return (Mathf.Max(maximum, Tiny) - Mathf.Max(minimum, Tiny))
                / Mathf.Max(maximum, 1e-2f);
        }

        private static float RgbToYc(Vector3 rgb)
        {
            const float YcRadiusWeight = 1.75f;
            float chroma = Mathf.Sqrt(
                Mathf.Max(
                    (rgb.z * (rgb.z - rgb.y))
                        + (rgb.y * (rgb.y - rgb.x))
                        + (rgb.x * (rgb.x - rgb.z)),
                    0f
                )
            );
            return (rgb.z + rgb.y + rgb.x + (YcRadiusWeight * chroma)) / 3f;
        }

        private static float RgbToHue(Vector3 rgb)
        {
            if (Mathf.Approximately(rgb.x, rgb.y) && Mathf.Approximately(rgb.y, rgb.z))
                return 0f;

            float hue =
                Mathf.Rad2Deg
                * Mathf.Atan2(Mathf.Sqrt(3f) * (rgb.y - rgb.z), (2f * rgb.x) - rgb.y - rgb.z);
            return hue < 0f ? hue + 360f : hue;
        }

        private static float CenterHue(float hue, float centerHue)
        {
            float centered = hue - centerHue;
            if (centered < -180f)
                return centered + 360f;
            return centered > 180f ? centered - 360f : centered;
        }

        private static float SigmoidShaper(float x)
        {
            float t = Mathf.Max(1f - Mathf.Abs(x / 2f), 0f);
            return (1f + (Mathf.Sign(x) * (1f - (t * t)))) / 2f;
        }

        private static float GlowForward(float yc, float glowGain, float glowMid)
        {
            if (yc <= 2f / 3f * glowMid)
                return glowGain;
            if (yc >= 2f * glowMid)
                return 0f;
            return glowGain * ((glowMid / yc) - 0.5f);
        }

        /// <summary>
        /// The HDR output device transform: a nine-knot B-spline over log luminance mapping OCES
        /// to display nits. All three presets share their dark and mid tones and differ only in
        /// how far the shoulder reaches.
        /// </summary>
        private static float OutputDeviceTransform(float oces, HDRACESPreset preset)
        {
            float[] high;
            float maxX;
            float maxNits;
            float slopeHigh;
            switch (preset)
            {
                case HDRACESPreset.ACES2000Nits:
                    high = OutputHighCoefficients2000;
                    maxX = OutputMaxOces2000;
                    maxNits = 2000f;
                    slopeHigh = 0.12f;
                    break;
                case HDRACESPreset.ACES4000Nits:
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

        private static Vector3 Positive(Vector3 value) =>
            new(Mathf.Max(value.x, 0f), Mathf.Max(value.y, 0f), Mathf.Max(value.z, 0f));

        private static Vector3 Transform(Matrix4x4 matrix, Vector3 value) =>
            matrix.MultiplyVector(value);

        private static Matrix4x4 FromRows(Vector3 row0, Vector3 row1, Vector3 row2)
        {
            Matrix4x4 matrix = Matrix4x4.identity;
            matrix.SetRow(0, new Vector4(row0.x, row0.y, row0.z, 0f));
            matrix.SetRow(1, new Vector4(row1.x, row1.y, row1.z, 0f));
            matrix.SetRow(2, new Vector4(row2.x, row2.y, row2.z, 0f));
            return matrix;
        }
    }
}
