# Getting started

## 1. Add the library

Place this repository in your project's `Libraries/local.adaptive_director` directory, or add it through your normal s&box library workflow.

## 2. Build a snapshot

On the authoritative simulation, create one stable participant id per player, current population counts, and candidate locations computed by your navigation system. Distances use your game's world units; tune placement profiles accordingly. Populate `AreaWidth`, `AreaHeight`, and `Progress` when using the recovered placement presets. Candidate list order matters for first-tie compatibility. `TeamProgress` is a normalized or consistently scaled value chosen by your game.

## 3. Execute requests

Call `DirectorCore.Tick`, spawn through your own entity code, and always return a `SpawnRequestResult`. Never treat a request as an already-spawned enemy. The reservation prevents concurrent requests from exceeding limits.

## 4. Report pressure

Map your game's meaningful danger events to `Minor`, `Moderate`, `Major`, `Critical`, or `Maximum`. Do this centrally so balance can be changed without touching the Director.

The compilable [pressure-reporting example](../examples/PressureReportingExample.cs)
shows recovered damage bands, ledge danger, first/later incapacitation, guarded
revival, nearby threat-death ranges, custom events, host-runner reporting, and a
bounded diagnostic buffer. Report events once from authority using monotonic game
time; do not send both a predicted client event and its server confirmation.

## 5. Choose lifecycle style

- Existing manager: construct `DirectorHostRunner` with delegates or implementations of the world/spawn interfaces.
- s&box component: derive from `AdaptiveDirectorComponent` and implement its two abstract translation methods.
- Round game: use `RoundSessionFlow`, call `BeginRound`, and call `EndRound` to cancel pending requests.
- Continuous game: use `ContinuousSessionFlow` and persist `CaptureState()` when saving.

If your custom `IDirectorPolicy` or `IPlacementPolicy` changes internal counters, cooldowns, or rolls, implement `IPersistentDirectorPolicy` or `IPersistentPlacementPolicy`. The Director will then include that extension state in the same JSON document and verify its identity during load.

See the compilable [round example](../examples/RoundBasedExample.cs) and [continuous example](../examples/ContinuousExample.cs).

The [advanced example](../examples/AdvancedDirectorExample.cs) shows concurrent horde/special schedules, weighted composition, resource density and integrated scenario stages. All navigation facts are still supplied by your game through candidates.

The complete [example index](../examples/README.md) includes focused integrations
for custom policies, optional round music, client/server music separation, catalog
features, and combined save/restore.

## Adaptive music

1. Create a `MusicDirectorCore` from `RecoveredMusicDirectorDefaults` or a custom
   `MusicDirectorConfiguration`.
2. Build a `MusicWorldSnapshot` for each listener/player. All measurements are
   normalized except counts, distances, and cumulative damage.
3. Register your own audio event IDs in `MusicEventCatalog`. Event definitions own
   track priority, block/duck/stop lists, fades, delays, timing tags, and autoqueue.
4. Apply the authority's `MusicDecisionBatch` to that participant's `MusicRuntime`.
5. Translate each `MusicRuntimeAction` to s&box sound/mixer operations.

Use `MusicDirectorHostRunner` when authority and audio run in the same process. In a
network game, serialize or map the semantic decision batch through your normal RPC
layer and apply it on the owning client. Do not replicate audio handles.

Scenario and round managers send exact-once `MusicGameplayEvent` records. Use
`Play`, `StopEvent`, `StopTrack`, `StopAll`, `Reset`, `ScenarioEnded`, and
`ParticipantRestored` without changing the music core. Save both
`MusicDirectorState` and `MusicRuntimeState` if the game needs seamless restoration.

See the [compilable example](../examples/MusicDirectorExample.cs).
