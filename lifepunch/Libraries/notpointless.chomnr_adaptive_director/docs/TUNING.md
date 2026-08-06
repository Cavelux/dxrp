# Tuning and diagnostics

Start with long cooldowns and low population limits. First validate candidate availability, then enemy counts, then pressure inputs, and only then shorten tempo or scheduling intervals.

Pressure defaults reproduce the recovered coefficient surface: gain `0.25`, five-second hold, thirty-second linear decay, and weights `0.05/0.20/0.50/1.00/max`. These are useful starting values, not universal balance.

Attach `DirectorTelemetryBuffer` during development. Log phase-change events, request reasons, results, and candidate rejection counts in your host. Use a fixed seed and saved `WorldSnapshot` sequence for reproducible balance regressions.

Bosses and dormant hazards are generalized encounter kinds. Use a `MilestoneEncounterSchedule` for progress windows and your `IDirectorPolicy` to select the actual archetype. The Director never assumes a Tank, Witch, monster, vehicle, or specific NPC class.

Weighted composition and weighted placement remain clean-room tuning choices for games that need them. The recovered L4D2 paths instead choose uniformly among due-and-eligible special classes, shuffle threat candidates before taking the first valid one, and choose route neighbors by greatest flow with first-enumerated ties. Use `RecoveredDirectorDefaults`, `UniformEncounterComposer`, and `SpecialClassRotation` when that behavior is the desired baseline. Retry attempts/delay and resource targets remain library policy.

## Music tuning

Start with `RecoveredMusicDirectorDefaults` and replace every cue ID with an event
from your own catalog. Tune observation normalization before changing envelopes:
common-threat counts, attacking/close/very-close factors, weapon activity, hazard
rage, and cumulative damage must be consistent across ticks.

The most audible controls are:

- activity and damage rise/decay rates;
- combat stop active/scanned counts and the `0.01` quiet-signal threshold;
- common sight, action, threat, calm, and ambient decay durations;
- damage-duck, mob-pressure, close-action, and ambient remap ranges;
- special alert distance bands, BPM, beats, random multiplier, and peer stagger;
- event priority, track block/duck/stop lists, fades, delay, timing tags, and
  autoqueue.

Keep catalog IDs and track IDs stable across saves. When changing stateful music
behavior incompatibly, change `MusicDirectorConfiguration.Version` or
`MusicRuntimeConfiguration.Version` and migrate old saves deliberately. Use a fixed
seed and recorded `MusicWorldSnapshot`/`MusicGameplayEvent` sequence for listening
tests; identical inputs must produce identical decisions before the audio adapter.
