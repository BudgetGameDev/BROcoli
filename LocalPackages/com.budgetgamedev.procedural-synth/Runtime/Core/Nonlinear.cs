using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace BudgetGameDev.Synth
{
    /// <summary>Odd, unity-small-signal-gain rational saturation, bounded to [-1, 1].</summary>
    public static class Saturation
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SoftClip(float input)
        {
            if (!math.isfinite(input)) return 0f;
            float x = math.clamp(input, -3f, 3f);
            float x2 = x * x;
            return x * (27f + x2) / (27f + 9f * x2);
        }

        // Derivative of x(27+x^2)/(27+9x^2), including its flat tails.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float Derivative(float x)
        {
            if (math.abs(x) >= 3f) return 0f;
            float x2 = x * x;
            float numerator = x2 - 9f;
            float denominator = 27f + 9f * x2;
            return 9f * numerator * numerator / (denominator * denominator);
        }
    }

    /// <summary>
    /// Four cascaded trapezoidal one-poles with nonlinear global negative feedback.
    /// Small-signal rolloff is 24 dB/octave. This ladder-inspired filter has one
    /// saturating feedback-input junction; its stages are linear. It is NOT a
    /// transistor/OTA circuit simulation and does not model component tolerances.
    ///
    /// Derivation: each stage y=G*u+(1-G)*state, G=tan(pi*fc/fs)/(1+tan(pi*fc/fs)).
    /// Cascade instantaneous response is y4=A*u+B. Solve
    /// u=SoftClip(input-k*(A*u+B)), k=4*resonance, using four fixed Newton steps.
    /// No artificial sample delay exists in this feedback route. The rational
    /// saturator and its exact derivative replace tanh; iterations are bounded.
    ///
    /// Cutoff <=0.2*fs means G<0.421 and k*A<0.120: the implicit equation is a
    /// contraction with a unique root, and Newton's denominator is >=1. Clamping
    /// each candidate to [-1,1] also bounds every stage's state: its update is a
    /// convex combination (1-2G)*state+2G*input. Abrupt cutoff changes remain bounded.
    /// No resonance loudness compensation: increasing feedback reduces DC gain.
    ///
    /// Reference for TPT/instantaneous response (implementation independently derived):
    /// Vadim Zavalishin, The Art of VA Filter Design, 2.1.0, chapters 3, 5 and 6.
    /// https://www.discodsp.net/VAFilterDesign_2.1.0.pdf
    /// Nonlinearity creates harmonics; caller must oversample and lowpass-decimate.
    /// </summary>
    public struct NonlinearFilter24
    {
        private float state1, state2, state3, state4;
        private float cachedCutoff, cachedSampleRate, gain;

        public void Reset()
        {
            state1 = state2 = state3 = state4 = 0f;
            cachedCutoff = cachedSampleRate = gain = 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Process(float input, float cutoffHz, float resonance, float sampleRate)
        {
            // Defend this standalone entry point too; normal voice inputs are already sanitized.
            if (!math.isfinite(input)) input = 0f;
            if (!math.isfinite(sampleRate)) sampleRate = 48000f;
            sampleRate = math.clamp(sampleRate, 8000f, 768000f);
            if (!math.isfinite(cutoffHz)) cutoffHz = 180f;
            cutoffHz = math.clamp(cutoffHz, 20f, math.min(18000f, sampleRate * 0.2f));
            if (!math.isfinite(resonance)) resonance = 0f;
            float k = 4f * math.clamp(resonance, 0f, 0.95f);
            input = math.clamp(input, -48f, 48f);

            // Static cutoff avoids transcendental work. Audio-rate cutoff computes a
            // fresh prewarp each internal sample (cost must be profiled in the voice).
            if (cutoffHz != cachedCutoff || sampleRate != cachedSampleRate)
            {
                float g = math.tan(math.PI * cutoffHz / sampleRate);
                gain = g / (1f + g);
                cachedCutoff = cutoffHz;
                cachedSampleRate = sampleRate;
            }
            float oneMinusGain = 1f - gain;
            float gain2 = gain * gain;
            float a = gain2 * gain2;
            float b = oneMinusGain * (state4 + gain * (state3 + gain * (state2 + gain * state1)));
            float u = Saturation.SoftClip(input - k * b);
            for (int iteration = 0; iteration < 4; iteration++)
            {
                float junction = input - k * (a * u + b);
                float residual = u - Saturation.SoftClip(junction);
                float derivative = 1f + k * a * Saturation.Derivative(junction);
                u = math.clamp(u - residual / derivative, -1f, 1f);
            }

            float y = Stage(u, ref state1, gain);
            y = Stage(y, ref state2, gain);
            y = Stage(y, ref state3, gain);
            return Stage(y, ref state4, gain);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Stage(float input, ref float state, float g)
        {
            float v = (input - state) * g;
            float y = state + v;
            state = y + v;
            // Avoid denormal tails on managed/non-Burst offline paths as well.
            if (math.abs(state) < 1e-20f) state = 0f;
            return y;
        }
    }

    /// <summary>Unity-gain-at-Nyquist first-order DC blocker, 8 Hz pole, output-rate use.</summary>
    public struct DcBlocker
    {
        private float previousInput, previousOutput, cachedSampleRate, pole;

        public void Reset()
        {
            previousInput = previousOutput = cachedSampleRate = pole = 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Process(float input, float sampleRate)
        {
            if (!math.isfinite(input)) input = 0f;
            if (!math.isfinite(sampleRate)) sampleRate = 48000f;
            sampleRate = math.clamp(sampleRate, 8000f, 768000f);
            if (sampleRate != cachedSampleRate)
            {
                pole = math.exp(-2f * math.PI * 8f / sampleRate);
                cachedSampleRate = sampleRate;
            }
            float result = (1f + pole) * 0.5f * (input - previousInput) + pole * previousOutput;
            previousInput = input;
            previousOutput = math.abs(result) < 1e-20f ? 0f : result;
            return previousOutput;
        }
    }
}
