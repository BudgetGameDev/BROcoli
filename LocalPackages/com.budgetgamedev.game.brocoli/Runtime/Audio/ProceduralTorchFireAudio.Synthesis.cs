using System;

namespace BudgetGameDev.Games.Brocoli
{
    internal sealed partial class ProceduralTorchFireAudio
    {
        internal const int SampleRate = 22050;
        internal const int BedSeconds = 4;
        internal const int CrackleVariants = 4;

        internal static float[] SynthesizeBed()
        {
            var random = new Random(78193);
            var samples = new float[SampleRate * BedSeconds];
            double warm = 0,
                bright = 0,
                rumble = 0;
            for (int sample = -1024; sample < samples.Length; sample++)
            {
                double noise = random.NextDouble() * 2 - 1;
                warm += 0.066 * (noise - warm);
                bright += 0.32 * (noise - bright);
                rumble += 0.018 * (noise - rumble);
                if (sample < 0)
                    continue;
                double phase = 2 * Math.PI * sample / samples.Length;
                double breath = 0.75 + 0.15 * Math.Sin(phase * 3) + 0.1 * Math.Sin(phase * 5);
                samples[sample] = (float)((warm - rumble + (bright - warm) * 0.16) * breath);
            }
            Finish(samples, SampleRate / 40, 0.46f);
            return samples;
        }

        internal static float[] SynthesizeCrackle(int variant)
        {
            variant = Math.Abs(variant % CrackleVariants);
            var random = new Random(5197 + variant * 173);
            var samples = new float[(int)(SampleRate * (0.18 + variant * 0.025))];
            double low = 0,
                high = 0;
            for (int sample = 0; sample < samples.Length; sample++)
            {
                double time = sample / (double)SampleRate;
                double noise = random.NextDouble() * 2 - 1;
                low += 0.42 * (noise - low);
                high += 0.04 * (noise - high);
                double pop = Pulse(time, 0.007, 75) + 0.32 * Pulse(time, 0.037, 95);
                double sizzle = Math.Sin(Math.PI * sample / (samples.Length - 1));
                sizzle = sizzle * sizzle * Math.Exp(-time * 18) * 0.15;
                double body = Math.Sin(2 * Math.PI * (680 + variant * 70) * time) * pop * 0.09;
                samples[sample] = (float)((low - high) * (pop + sizzle) + body);
            }
            Finish(samples, SampleRate / 500, 0.55f);
            return samples;
        }

        private static double Pulse(double time, double start, double decay)
        {
            double age = time - start;
            return age <= 0 ? 0 : (1 - Math.Exp(-age * 1200)) * Math.Exp(-age * decay);
        }

        private static void Finish(float[] samples, int fadeSamples, float peak)
        {
            double sum = 0,
                weight = 0;
            for (int index = 0; index < samples.Length; index++)
            {
                double edge = Edge(index, samples.Length, fadeSamples);
                samples[index] *= (float)edge;
                sum += samples[index];
                weight += edge;
            }
            double correction = sum / weight;
            float greatest = 0;
            for (int index = 0; index < samples.Length; index++)
            {
                samples[index] -= (float)(correction * Edge(index, samples.Length, fadeSamples));
                greatest = Math.Max(greatest, Math.Abs(samples[index]));
            }
            float scale = greatest > 0 ? peak / greatest : 1;
            for (int index = 0; index < samples.Length; index++)
                samples[index] *= scale;
        }

        private static double Edge(int index, int length, int fadeSamples)
        {
            double ramp = Math.Min(1, Math.Min(index, length - 1 - index) / (double)fadeSamples);
            return 0.5 - 0.5 * Math.Cos(Math.PI * ramp);
        }
    }
}
