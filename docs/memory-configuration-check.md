# Memory configuration checks

The overlay and system readiness report flag memory configured more than 5%
below the **same DIMM's firmware-reported capability**. A 6000 MT/s capability
with a 4800 MT/s configuration produces a yellow **SUBOPTIMAL CONFIGURATION**
notice, with both rates and the 20% transfer-rate shortfall. That percentage
is not a prediction of lost FPS. The report identifies the module and suggests
reviewing supported XMP/EXPO settings, kit compatibility and stability.

The helper reads module identity, `Speed` and `ConfiguredClockSpeed` through
[Windows physical-memory information](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-physicalmemory).
It does not add direct SPD/SMBus probing or bypass the sensor concurrency guard.
Comparisons use module IDs, not minimum rates from unrelated DIMMs. Missing,
stale or invalid values never become an optimization or health verdict.

Firmware capability is not guaranteed to be the advertised XMP/EXPO profile.
Some kits have a base JEDEC rate and a separate faster profile: for example,
[Kingston's DDR5-6000 certification report](https://media.kingston.com/pdfs/memory/self-certifications/KF560C30BBEAK2-32.pdf)
lists DDR5-4800 JEDEC operation and DDR5-6000 EXPO operation. If firmware exposes
only the base rate, this check cannot establish that the faster profile exists
or whether it is enabled. It reports that limitation rather than declaring the
configuration optimal or guessing the rated speed from a part number.

[Intel's XMP guidance](https://www.intel.com/content/www/us/en/support/articles/000094616/processors.html)
and [AMD's EXPO information](https://www.amd.com/en/products/processors/technologies/expo.html)
describe supported memory profiles. Profile compatibility depends on the kit,
processor and motherboard; module population can also limit a stable rate.
The game offers review guidance and makes no BIOS, voltage or clock changes.
This check neither diagnoses defective RAM nor replaces a memory stability test.
