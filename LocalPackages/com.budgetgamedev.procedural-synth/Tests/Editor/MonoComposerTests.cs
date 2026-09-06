using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace BudgetGameDev.Synth.Tests
{
    public class MonoComposerTests
    {
        private static List<SynthEvent> Collect(MonoComposer composer, long horizon, int capacity)
        {
            var events = new List<SynthEvent>();
            var buffer = new SynthEvent[capacity];
            for (int call = 0; call < 100000; call++)
            {
                int count = composer.Fill(horizon, buffer);
                for (int i = 0; i < count; i++) events.Add(buffer[i]);
                if (count == 0) return events;
            }
            throw new Exception("Composer failed to finish finite horizon.");
        }

        [Test]
        public void CapacityOneAndLargeBatchesProduceIdenticalSortedPairedEvents()
        {
            var a = new MonoComposer(48000, 73);
            var b = new MonoComposer(48000, 73);
            var state = new GameMusicState { Danger = .8f, MovementSpeed = .8f, PlayerHealth = .7f, NarrativeState = 3 };
            a.SetState(state); b.SetState(state);
            var expected = Collect(a, 48000 * 20, 128);
            var actual = Collect(b, 48000 * 20, 1);
            Assert.That(actual.Count, Is.EqualTo(expected.Count));
            long previous = -1;
            int heldNote = -1;
            for (int i = 0; i < actual.Count; i++)
            {
                Assert.That(actual[i].Sample, Is.EqualTo(expected[i].Sample).And.GreaterThanOrEqualTo(previous));
                Assert.That(actual[i].Type, Is.EqualTo(expected[i].Type));
                Assert.That(actual[i].Note, Is.EqualTo(expected[i].Note));
                Assert.That(actual[i].Value, Is.EqualTo(expected[i].Value));
                if (actual[i].Type == SynthEventType.NoteOn) { Assert.That(heldNote, Is.EqualTo(-1)); heldNote = actual[i].Note; }
                else { Assert.That(actual[i].Note, Is.EqualTo(heldNote)); heldNote = -1; }
                previous = actual[i].Sample;
            }
            Assert.That(actual.Count, Is.GreaterThan(100));
        }

        [Test]
        public void PhraseHasAccentsRestsAndScaleBoundedNotes()
        {
            var composer = new MonoComposer(48000, 9) { Tempo = 120, RootMidi = 48, Scale = MusicScale.Pentatonic };
            composer.SetState(new GameMusicState { Danger = 1, PlayerHealth = 1 });
            var events = Collect(composer, 96000 * 8 - 1, 256);
            int ons = 0, accents = 0, soft = 0;
            var scale = new HashSet<int> { 0, 3, 5, 7, 10 };
            foreach (var e in events)
            {
                Assert.That(e.Sample, Is.LessThan(96000 * 8));
                if (e.Type != SynthEventType.NoteOn) continue;
                ons++;
                Assert.That(e.Note, Is.InRange(48, 58));
                Assert.That(scale.Contains(e.Note - 48), Is.True);
                Assert.That(e.Sample % 96000, Is.Not.EqualTo(90000)); // last sixteenth is a phrase rest
                if (e.Value > .9f) accents++; else soft++;
            }
            Assert.That(ons, Is.GreaterThan(8).And.LessThan(128));
            Assert.That(accents, Is.GreaterThan(0));
            Assert.That(soft, Is.GreaterThan(0));
        }

        [Test]
        public void NarrativeNeedsStableBeatsAndCommitsWithRootAndScaleAtPhrase()
        {
            var composer = new MonoComposer(48000, 4) { Tempo = 120 };
            var buffer = new SynthEvent[128];
            composer.Fill(0, buffer);
            composer.SetState(new GameMusicState { NarrativeState = 7, PlayerHealth = 1 });
            composer.RootMidi = 48; composer.Scale = MusicScale.Dorian;
            composer.Fill(95999, buffer);
            Assert.That(composer.ActiveNarrativeState, Is.Zero);
            Assert.That(composer.ActiveRootMidi, Is.EqualTo(36));
            composer.Fill(96000, buffer);
            Assert.That(composer.ActiveNarrativeState, Is.EqualTo(7));
            Assert.That(composer.ActiveRootMidi, Is.EqualTo(48));
            Assert.That(composer.ActiveScale, Is.EqualTo(MusicScale.Dorian));

            composer.Fill(191999, buffer);
            composer.SetState(new GameMusicState { NarrativeState = 8, PlayerHealth = 1 });
            composer.Fill(192000, buffer); // first observation of 8, so retain 7
            Assert.That(composer.ActiveNarrativeState, Is.EqualTo(7));
            composer.Fill(288000, buffer);
            Assert.That(composer.ActiveNarrativeState, Is.EqualTo(8));
        }

        [Test]
        public void EmptyDestinationConsumesNothingAndTempoChangesAtBeat()
        {
            var composer = new MonoComposer(48000, 1) { Tempo = 120 };
            Assert.That(composer.Fill(100000, new SynthEvent[0]), Is.Zero);
            Assert.That(composer.NextStepIndex, Is.Zero);
            var buffer = new SynthEvent[128];
            composer.Fill(0, buffer);
            composer.Tempo = 60;
            composer.Fill(23999, buffer);
            Assert.That(composer.NextStepSample, Is.EqualTo(24000));
            composer.Fill(24000, buffer);
            Assert.That(composer.NextStepSample, Is.EqualTo(36000));
            composer.Tempo = float.NaN; composer.RootMidi = 300; composer.Scale = (MusicScale)100;
            Assert.That(composer.Tempo, Is.InRange(40, 200));
            Assert.That(composer.RootMidi, Is.EqualTo(60));
            Assert.That(composer.Scale, Is.EqualTo(MusicScale.Minor));
        }

        [Test]
        public void ExpressionIsSmoothedAndBounded()
        {
            var composer = new MonoComposer(48000, 1);
            var basis = SynthParameters.HeavyBass;
            composer.SetState(new GameMusicState { Danger = 1, EnemyProximity = 1, PlayerHealth = 0, Weather = 1 });
            var first = composer.AdaptPreset(basis, .01f);
            Assert.That(first.CutoffHz, Is.GreaterThan(basis.CutoffHz).And.LessThan(basis.CutoffHz * 16));
            Assert.That(first.WavetablePosition, Is.InRange(.01f, .1f));
            SynthParameters last = first;
            for (int i = 0; i < 200; i++) last = composer.AdaptPreset(basis, .01f);
            Assert.That(last.CutoffHz, Is.EqualTo(basis.CutoffHz * 16).Within(2));
            Assert.That(last.PreDrive, Is.InRange(1, 12));
            Assert.That(last.NoiseLevel, Is.InRange(0, 1));
            Assert.That(last.WavetablePosition, Is.InRange(.99f, 1));
            composer.SetState(new GameMusicState { Danger = float.NaN, EnemyProximity = float.PositiveInfinity, PlayerHealth = float.NegativeInfinity, Weather = 999 });
            last = composer.AdaptPreset(basis, float.NaN);
            Assert.That(float.IsNaN(last.CutoffHz) || float.IsInfinity(last.CutoffHz), Is.False);
        }

        [Test]
        public void DangerDensityHasHysteresisAndMovementShortensArticulation()
        {
            var low = new MonoComposer(48000, 19) { Tempo = 120 };
            var deadband = new MonoComposer(48000, 19) { Tempo = 120 };
            var high = new MonoComposer(48000, 19) { Tempo = 120 };
            low.SetState(new GameMusicState { Danger = 0, MovementSpeed = 0, PlayerHealth = 1 });
            deadband.SetState(new GameMusicState { Danger = .37f, MovementSpeed = .37f, PlayerHealth = 1 });
            high.SetState(new GameMusicState { Danger = 1, MovementSpeed = 1, PlayerHealth = 1 });
            var a = Collect(low, 96000 * 8 - 1, 128);
            var b = Collect(deadband, 96000 * 8 - 1, 128);
            var c = Collect(high, 96000 * 8 - 1, 128);
            Assert.That(b.Count, Is.EqualTo(a.Count));
            for (int i = 0; i < a.Count; i++)
            {
                Assert.That(b[i].Sample, Is.EqualTo(a[i].Sample));
                Assert.That(b[i].Note, Is.EqualTo(a[i].Note));
            }
            Assert.That(c.Count, Is.GreaterThan(a.Count));
            Assert.That(a[1].Sample - a[0].Sample, Is.EqualTo(5400));
            Assert.That(c[1].Sample - c[0].Sample, Is.EqualTo(2400));
        }
    }
}
