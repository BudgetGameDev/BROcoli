# Torch combustion audio and ember verification

Torch embers now use explicit combustion events rather than prewarmed cone
emission. Each event launches one to three immediately visible sparks from a
4.5 cm disk at the lower fuel anchor and triggers its procedural crackle in
the same call. Emission is bounded by the available particle capacity; a full
emitter cannot produce an unmatched crackle. Returning to a torch or resuming
gameplay does not replay a backlog of events.

Sparks use 0.9–1.65 m/s upward velocity, less than 0.25 m/s horizontal velocity,
0.65–0.9 gravity scaling, and 0.8–1.5 second lifetimes. Cone emission, prewarm,
delayed ignition visibility, and sideways noise are disabled. Static dungeon
collision and damped bounce remain enabled. Live testing found that generated
floor tiles have no physics collider. A bounded particle ground-contact pass
now uses the actual playable layout to provide a short damped bounce at y=0;
sparks outside the platform keep falling into the cliff void. It does not add
colliders or change gameplay physics. Total per-torch particle capacity
is 40, including at most 12 embers.

The fire sound is synthesized locally: a quiet filtered-noise fuel loop and
four short pop/sizzle variants, shared across torches. Three bounded sources
per torch provide one loop and two transient voices, with no per-event clip
or GameObject allocation. SFX mixer routing and pause settings apply. Gain is
based on player ground distance, compensating for the overhead camera's audio
listener: full near-field gain within 1 m, 25% at 6.5 m, silent at 12 m.
Out-of-range loops stop, and nearby torches use independent playback phases.

Compilation, formatting, and source-size checks pass. The focused Unity suite
passes **29/29**, including URP/HDRP flame rendering, actual emitted particle
positions and short trajectories, event synchronization, finite audio samples,
bounded waveform peaks, negligible DC, loop endpoints, and distance falloff.
Report: `build/verification/torch-audio-tests.json`.

`build/verification/torch-fire-nearfield.wav` is a six-second synthesized
preview produced from the actual C# synthesis at runtime near-field gains.
It is not a recording of the game's audio output. Signal measurements are in
`build/verification/torch-audio-synthesis-check.txt`.

Live validation in `build/verification/torch-ember-audio-live.json` confirms
three births within 4.3–4.7 cm of the fuel, exactly one callback and one audio
variant advance, 7–14 cm apex rises, and 8–13.5 cm maximum lateral travel.
All three particles reach the ground and rebound before fading within 0.22 s.
The bed plays at volume 0.13 nearby, stops at 13.8 m, and resumes on return.
A time-scale-only pause stops all voices and freezes the event timer;
disabling the effect clears particles and stops audio.

`build/verification/torch-ember-grounded-motion.mp4` is a silent gameplay clip
at the normal default camera, preserving 5.0052826 seconds across 36 frames.
Temporary player, camera, HDR, and pause settings were restored. The visible
Editor remains playing; pre-existing material/project-setting changes remain
byte-identical to the pre-verification snapshot.

Earlier release archives predate these changes and have not been rebuilt for
this scoped audio/ember refinement.

## Procedural dungeon ambience

The player owns one dungeon soundscape: a quiet stereo cavity-air loop and
sparse water drops and stone-settling echoes, with randomized spacing and pan.
It fades in over 2.5 seconds and uses the Ambience bus, with master/ambience
volume fallback when a scene is played directly without a configured mixer.
Three sources and shared cached clips bound memory and voice usage. Synthesis
and scheduling use local random generators, leaving gameplay RNG untouched.
The ambience clock runs in real time during accelerated gameplay and freezes
on pause. Death disables all three voices; player effects remain available.

Unity compilation and the focused dungeon suite pass **9/9**; the audio
regression suite passes **64/64**. Reports are
`build/verification/dungeon-ambience-tests.json` and
`build/verification/dungeon-ambience-audio-regressions.json`.
Formatting and the source-size gate pass.

`build/verification/dungeon-ambience-default-mix.wav` is a 15-second preview
generated from the actual C# synthesis at default runtime gains, including
the fade-in, a water drop, and a stone echo. It is not an in-game recording.
`build/verification/dungeon-ambience-synthesis-check.txt` records finite samples,
bounded peaks, negligible DC, smooth loop edges, and decaying detail tails.

Normal game selection exposed a pre-existing mixer initialization problem:
the launcher could create the shared audio settings host before a game had
selected its mixer. `Configure` now rebinds that existing host and schedules
source routing; scene pause cleanup is registered even when no mixer has been
selected yet. A regression covers selecting a mixer after host creation.

Live verification (`build/verification/dungeon-ambience-live.json`) confirms
one soundscape with three voices on Ambience and zero playing legacy loops.
The six authored camera loops retain their objects and clips, but are disabled
with play-on-awake off. The new fade progresses as intended; pause freezes
both sample positions and scheduling, resume continues playback, and the
death handler stops all ambience while preserving the defeat sound source.
`build/verification/dungeon-ambience-normal-launch-final.json` confirms the
existing host binds the selected mixer without recreation: an Ambience value
of 0.2 sets -13.9794 dB, and restoring 0.35 returns -9.118639 dB. The original
preference value and key existence were restored.
