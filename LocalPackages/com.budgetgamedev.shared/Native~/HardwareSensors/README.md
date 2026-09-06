# Hardware sensor reader

Read-only LibreHardwareMonitorLib 0.9.6 sidecar, built for Windows x64 with a
self-contained .NET 8 runtime. It inherits the game's security token, opens
sensor providers, updates all discovered hardware/subhardware, and writes
newline-delimited JSON to its parent through stdout. It does not change fan
controls, install drivers, request elevation or send telemetry over a network.

Build from the repository root: `python scripts/build-hardware-sensors.py`.
This publishes locked dependencies and includes the redistribution licenses and notices.
Use `--once` for a single discovery snapshot, or `--parent <pid>` for continuous
two-second updates that stop when the owning player exits.

Libre Hardware Monitor source and MPL-2.0 license:
https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/v0.9.6
Some low-level CPU, DIMM and motherboard readings require administrator access
and an installed compatible PawnIO driver. Elevation alone is not sufficient.
PawnIO: https://github.com/namazso/PawnIO
Missing sensor values are not interpreted as permission failures unless the
provider actually reports access denied. No voltage, clock or fan-control changes
are requested. Low-level reads can still write index/bank/command registers.

Before opening LHM and between provider probes, a conservative process-name scan
checks for known monitoring/tuning tools (including Afterburner, HWiNFO, Fan Control
and vendor utilities/services). A match or scan failure pauses LHM for the rest of
the player session. Shared global LHM lock handles must be accessible, and only
one game helper may own probing across Windows sessions. We retain LHM's own
transaction locks; we do not hold bus locks around provider calls. Lock failure
never falls back to unlocked probing. Administrator access does not bypass this.
Windows/NVIDIA counters, firmware memory configuration and query-only SMART remain
available. No other process is stopped or modified. Restart the game to retry.
Process names are only a heuristic: unknown/renamed tools, drivers and tools starting
during a transaction cannot be ruled out. The guard reduces conflicts, not proves
universal safety. See docs/concurrent-hardware-sensors.md in the game repository.

Every ten seconds the helper also attempts Windows NVMe SMART/Health log queries
and storage failure prediction (ATA SMART summary where supported). Query-only
handles are used; SMART is never enabled/modified and no self-test is started.
Volume extents identify game/Windows physical drives; USB/RAID and inaccessible
volumes may remain unmapped. `--smart-once` prints one drive health snapshot.
Raw NVMe/vendor bytes are retained in the JSON. NVMe counters preserve 128 bits;
vendor-specific ATA attributes are not interpreted using universal thresholds.
Set BROCOLI_SENSOR_GAME_DIRECTORY to the game data path when probing separately.

Startup also reads Win32_PhysicalMemory firmware configuration for DDR modules.
Configured transfer rate (MT/s), capability and derived clock (MT/s / 2 in MHz)
are explicitly labeled as configuration, not live clock measurements.
Module labels include the firmware manufacturer and part number where reported.
Firmware capability is not a verified advertised XMP/EXPO profile. The readiness
check compares each module with its own configured rate and reports this limit;
it does not infer profile contents or enabled state from a part number.
