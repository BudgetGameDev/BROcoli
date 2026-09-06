using System;

namespace BudgetGameDev.Games.Brocoli
{
    internal sealed partial class ProceduralDungeonAmbience
    {
        internal const int SampleRate = 22050;
        internal const int BedSeconds = 12;
        internal const int DetailVariants = 6;

        internal static float[] SynthesizeBed()
        {
            int frames = SampleRate * BedSeconds;
            var samples = new float[frames * 2];
            var random = new Random(723901);
            double air = 0,
                cavity = 0,
                dc = 0,
                sideAir = 0,
                sideLow = 0;
            for (int frame = -4096; frame < frames; frame++)
            {
                double noise = random.NextDouble() * 2 - 1;
                double side = random.NextDouble() * 2 - 1;
                air += 0.12 * (noise - air);
                cavity += 0.023 * (noise - cavity);
                dc += 0.004 * (noise - dc);
                sideAir += 0.07 * (side - sideAir);
                sideLow += 0.009 * (side - sideLow);
                if (frame < 0)
                    continue;
                double phase = 2 * Math.PI * frame / frames;
                double breath = 0.8 + 0.12 * Math.Sin(phase) + 0.08 * Math.Sin(phase * 3);
                double centre = ((cavity - dc) * 1.5 + (air - cavity) * 0.16) * breath;
                double width = (sideAir - sideLow) * 0.16;
                samples[frame * 2] = (float)(centre + width);
                samples[frame * 2 + 1] = (float)(centre - width);
            }
            Finish(samples, 2, SampleRate / 25, 0.42f);
            return samples;
        }

        internal static float[] SynthesizeDetail(int variant)
        {
            variant = Math.Abs(variant % DetailVariants);
            bool water = variant % 2 == 0;
            var random = new Random(31799 + variant * 613);
            var excitation = new float[SampleRate * 3];
            double low = 0,
                rumble = 0;
            for (int frame = 0; frame < SampleRate; frame++)
            {
                double time = frame / (double)SampleRate;
                double noise = random.NextDouble() * 2 - 1;
                low += (water ? 0.3 : 0.075) * (noise - low);
                rumble += 0.012 * (noise - rumble);
                if (water)
                {
                    double frequency = 1100 + variant * 90;
                    double phase = 2 * Math.PI * (frequency * time - 1400 * time * time);
                    double shape = (1 - Math.Exp(-time * 600)) * Math.Exp(-time * 42);
                    excitation[frame] = (float)((Math.Sin(phase) * 0.6 + low * 0.22) * shape);
                }
                else
                {
                    double shape =
                        StonePulse(time, 0.02)
                        + StonePulse(time, 0.19) * 0.55
                        + StonePulse(time, 0.37) * 0.24;
                    excitation[frame] = (float)((low - rumble) * shape);
                }
            }
            var samples = new float[excitation.Length];
            int first = (int)(SampleRate * (0.137 + variant * 0.003));
            int second = (int)(SampleRate * 0.223);
            double softened = 0;
            for (int frame = 0; frame < samples.Length; frame++)
            {
                double reflected = frame >= first ? samples[frame - first] * 0.48 : 0;
                if (frame >= second)
                    reflected += samples[frame - second] * 0.27;
                softened += 0.3 * (reflected - softened);
                samples[frame] = (float)(excitation[frame] * 0.7 + softened);
            }
            Finish(samples, 1, SampleRate / 100, water ? 0.48f : 0.34f);
            return samples;
        }

        private static double StonePulse(double time, double start)
        {
            double age = time - start;
            return age < 0 ? 0 : (1 - Math.Exp(-age * 45)) * Math.Exp(-age * 14);
        }

        private static void Finish(float[] samples, int channels, int fadeFrames, float peak)
        {
            int frames = samples.Length / channels;
            for (int channel = 0; channel < channels; channel++)
            {
                double sum = 0,
                    weight = 0;
                for (int frame = 0; frame < frames; frame++)
                {
                    double edge = Edge(frame, frames, fadeFrames);
                    int index = frame * channels + channel;
                    samples[index] *= (float)edge;
                    sum += samples[index];
                    weight += edge;
                }
                double correction = sum / weight;
                for (int frame = 0; frame < frames; frame++)
                    samples[frame * channels + channel] -= (float)(
                        correction * Edge(frame, frames, fadeFrames)
                    );
            }
            float maximum = 0;
            foreach (float sample in samples)
                maximum = Math.Max(maximum, Math.Abs(sample));
            float scale = maximum > 0 ? peak / maximum : 1;
            for (int index = 0; index < samples.Length; index++)
                samples[index] *= scale;
        }

        private static double Edge(int frame, int length, int fadeFrames)
        {
            double ramp = Math.Min(1, Math.Min(frame, length - frame - 1) / (double)fadeFrames);
            return 0.5 - 0.5 * Math.Cos(Math.PI * ramp);
        }
    }
}
