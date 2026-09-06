# Rendered and total FPS

The performance overlay shows **rendered FPS** and **total FPS (including
generated frames)**. With active frame generation, rates use a rolling two-second
window of native counters and refresh four times per second. The 1% low, P99
and frame-time graph continue to describe the game's rendered-frame timing
over ten seconds; generated frames do not improve those statistics.

Rendered FPS uses successful real Presents. Total FPS uses Streamline's reported
frames. The SDK query and real Present occur at slightly different times, so
sampling boundaries can differ by a frame. These are presentation counters, not a
measurement of monitor scan-out. The counter never multiplies rendered FPS by
the requested 2×/3×/4× setting.

The overlay reads cached counters from the native bridge. It does not call
`slDLSSGGetState` itself: that API is not thread-safe and its frame count is
since the previous query, so another caller would disturb the presentation
thread's measurements. See the [Streamline 2.12 API declaration](https://github.com/NVIDIA-RTX/Streamline/blob/v2.12.0/include/sl_dlss_g.h).

Off, suspended, unsupported, missing/stale counters and errors are shown
explicitly. The orthographic main menu normally suspends FG; enter gameplay
or run the unsaved readiness benchmark to observe generated frames. Focus
changes, counter resets and accepted multiplier changes discard the old rate
window. Without an active native rate, rendered FPS falls back to Unity's
frame timing; missing total measurements display N/A.

## Latency and boost frequency

The overlay also displays measured **PC LATENCY (Reflex)**: simulation start to
GPU render end, using the same report as the NVIDIA diagnostics page. It is not
calculated from total FPS and is not divided by the FG multiplier. Higher total
FPS can coexist with high PC latency. Missing, failed or stale reports show N/A.
The 25/50 ms yellow/red thresholds are indicative, not hardware fault limits.

This is not complete input-to-photon latency: it excludes peripheral/input delay,
display scan-out and any later frame-generation presentation buffering. It cannot
establish every FG latency penalty. Compare measured runs with otherwise matching
settings; the game does not assume that FG always adds a fixed amount of latency.
See NVIDIA's [Reflex integration guide](https://github.com/NVIDIA-RTX/Streamline/blob/v2.12.0/docs/ProgrammingGuideReflex.md)
and [latency measurement explanation](https://developer.nvidia.com/blog/understanding-and-measuring-pc-latency/).

**CPU BOOST/PEAK** shows the fastest available core sensor, or a Windows estimate
computed per logical processor from Processor Frequency × % Processor Performance
/ 100. The percentage is not capped at 100, allowing boost to be represented.
This is an interval estimate, not an advertised maximum or guaranteed sustained
boost frequency. Aggregate core averages and the OS nominal frequency remain
distinct. Missing readings are unavailable; no hardware controls are changed.
