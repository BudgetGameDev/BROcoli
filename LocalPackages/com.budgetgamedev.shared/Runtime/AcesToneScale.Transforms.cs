using UnityEngine;
using UnityEngine.Rendering;

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

        private const float AcesCcMidGray = 0.4135884f;

        /// <summary>
        /// URP's contrast, which is applied to the graded scene in ACEScc log space before the
        /// tone map, pivoting on middle grey. Modelled here so content authored against a display
        /// luminance can be pre-compensated for it: pass 1 / contrast to undo it.
        /// </summary>
        public static Vector3 ApplyContrast(Vector3 sceneColor, float contrast)
        {
            if (Mathf.Approximately(contrast, 1f))
                return sceneColor;

            Vector3 aces = Transform(Ap1ToAp0, Transform(SRgbToAp1, Positive(sceneColor)));
            Vector3 graded = new(
                AcesCcToLinear(
                    ((LinearToAcesCc(aces.x) - AcesCcMidGray) * contrast) + AcesCcMidGray
                ),
                AcesCcToLinear(
                    ((LinearToAcesCc(aces.y) - AcesCcMidGray) * contrast) + AcesCcMidGray
                ),
                AcesCcToLinear(
                    ((LinearToAcesCc(aces.z) - AcesCcMidGray) * contrast) + AcesCcMidGray
                )
            );
            return Positive(Transform(Ap1ToSRgb, Transform(Ap0ToAp1, graded)));
        }

        private static float LinearToAcesCc(float value)
        {
            const float Tiny = 1f / 65536f; // 2^-16
            if (value <= 0f)
                return (Mathf.Log(Tiny, 2f) + 9.72f) / 17.52f;
            if (value < 1f / 32768f) // 2^-15
                return (Mathf.Log(Tiny + (value * 0.5f), 2f) + 9.72f) / 17.52f;
            return (Mathf.Log(value, 2f) + 9.72f) / 17.52f;
        }

        private static float AcesCcToLinear(float value)
        {
            const float Tiny = 1f / 65536f;
            if (value < (9.72f - 15f) / 17.52f)
                return (Mathf.Pow(2f, (value * 17.52f) - 9.72f) * 2f) - Tiny;
            return Mathf.Pow(2f, (value * 17.52f) - 9.72f);
        }

        private static readonly Matrix4x4 Ap1ToSRgb = FromRows(
            new Vector3(1.70505f, -0.62179f, -0.08326f),
            new Vector3(-0.13026f, 1.14080f, -0.01055f),
            new Vector3(-0.02400f, -0.12897f, 1.15297f)
        );

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
