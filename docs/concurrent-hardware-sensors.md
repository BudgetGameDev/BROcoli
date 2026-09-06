# Concurrent hardware sensor probing

Reviewed 6 September 2026. Applies to the game's LibreHardwareMonitorLib 0.9.6
helper, Windows/NVIDIA performance counters and system readiness test.

## What can conflict?

Reading a sensor is sometimes a sequence: select a device, select a register or
bank, issue a command, then fetch its value. Two applications can interleave those
steps on shared embedded-controller (EC), Super I/O, PCI, SMBus/I²C or voltage
regulator interfaces. The result can be an incorrect reading, a delayed response
or system instability. Calling an application "read-only" means it does not ask
to change voltage, fan or clock settings; it does **not** mean every low-level
operation is a hardware read. Selecting a register can itself require a write.

Modern Windows is not a universal solution to this application/driver coordination
problem. A documented Windows or vendor telemetry API puts access behind that
driver's interface. Direct controller access needs additional coordination. Named
mutexes serialize cooperating applications only when they use the same locks and
hold them across the relevant transactions. They cannot exclude firmware or an
uncooperative kernel driver.

## Is Afterburner dangerous alongside another monitor?

Not automatically. In a [2024 answer about Afterburner's voltage-control warning](https://www.hwinfo.com/forum/threads/afterburner-voltage-control-conflict.9984/),
HWiNFO's author reported no known conflict and explained that HWiNFO directly
accesses GPU voltage regulators only on older GPUs. This is evidence about that
combination, not a guarantee for every GPU, monitoring library or tuning tool.

In a separate [EC concurrency discussion](https://www.hwinfo.com/forum/threads/ec-sensor-warning.8770/),
the author explained that applications must cooperate and synchronize, and that
conflicts typically manifest as invalid readings, instability, freezes or crashes.
He ruled out permanent damage in that EC discussion. We do not generalize that
assurance to every controller or voltage-control implementation. The historical
ASUS compatibility comments in that thread are not a current compatibility list.

## What the game does

* Before opening Libre Hardware Monitor (LHM), before enabling providers, and
  between hardware updates, check for known monitoring/tuning process names.
  These include Afterburner, HWiNFO, HWMonitor, Fan Control and common vendor
  utilities/services. Detection means a potential competitor, not proof that the
  application is currently reading voltages.
* If a known tool is detected, process enumeration fails, shared lock setup fails,
  or another game helper owns probing, withhold LHM readings for the remainder of
  that player session. Restart the game after resolving the cause to retry.
* Establish accessible handles for the global locks used by the pinned LHM version
  before opening providers. LHM then takes/releases its own transaction locks.
  This avoids its [unlocked fallback when a mutex cannot be opened](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LibreHardwareMonitorLib/Hardware/Mutexes.cs).
  A separate global lease prevents two game helpers probing concurrently.
* Keep Windows/NVIDIA performance counters, firmware memory configuration and
  Windows query-only SMART checks available when LHM is paused. These do not use
  LHM's direct controller probing. The SMART reader does not enable SMART, start
  self-tests or issue vendor-specific control commands.
* Explain the pause and detected process names in the Sensors report and its
  copied diagnostics. Missing sensor values remain unavailable, not "healthy".
  Administrator access does not bypass the guard. The game never closes another
  monitoring app, changes hardware controls, installs a driver or elevates itself.

## Limits and practical advice

Process detection is a heuristic, not a complete inventory of hardware clients:
unknown or renamed applications, kernel drivers and firmware may be invisible.
A program can start between a scan and a hardware transaction; shared locks help
only if it cooperates. The guard reduces conflict opportunities and fails closed
on detected problems; it cannot certify arbitrary combinations as safe.

If probing is paused, continue using the available counters or choose one hardware
monitor. Do not close a utility that is maintaining an essential fan curve just to
obtain another reading. Elevation provides access, not synchronization. If a
machine becomes unstable while probing, stop the game/extra monitor and retain the
diagnostic report; do not interpret implausible voltages as a confirmed hardware
fault. A readiness test with unavailable sensors is an incomplete assessment.

When upgrading LHM, re-audit its lock names and failure behavior and rerun the
helper guard tests. Do not assume a dependency update preserves these guarantees.

From the repository root, run `dotnet run --project
LocalPackages/com.budgetgamedev.shared/Native~/HardwareSensors.Tests -c Release`
for guard checks. After `python scripts/build-hardware-sensors.py`, run
`python LocalPackages/com.budgetgamedev.shared/Native~/HardwareSensors.Tests/integration.py`
for real helper-process tests. These simulate another monitor using an idle copy
of our test executable; they do not launch Afterburner or change hardware settings.
