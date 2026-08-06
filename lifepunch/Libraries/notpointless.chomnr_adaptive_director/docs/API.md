# Public API map

| Need | Type |
|---|---|
| Main deterministic coordinator | `DirectorCore` |
| Configuration | `DirectorConfiguration` and nested configuration types |
| Input from a game | `WorldSnapshot`, `ParticipantSnapshot`, `PopulationSnapshot`, `SpawnCandidate` |
| Output to a game | `DirectorDecisionBatch`, `SpawnRequest`, `DirectorEvent` |
| Confirm host execution | `SpawnRequestResult` |
| Existing manager integration | `DirectorHostRunner` |
| s&box component integration | `AdaptiveDirectorComponent` |
| Authoritative adaptive music | `MusicDirectorCore`, `MusicDirectorConfiguration`, `MusicWorldSnapshot` |
| Music commands and layers | `MusicDecisionBatch`, `MusicDecision`, `MusicMixerLayers` |
| Client/local track arbitration | `MusicRuntime`, `MusicRuntimeAction`, `MusicRuntimeConfiguration` |
| Authored music metadata | `MusicEventCatalog`, `MusicEventDefinition`, `MusicMasterFlags` |
| Combined local music bridge | `MusicDirectorHostRunner`, `IMusicAudioSink` |
| Optional s&box music component | `AdaptiveMusicDirectorComponent` |
| Music save/load | `MusicDirectorState`, `MusicRuntimeState`, `MusicStateCodec` |
| Round/no-round lifecycle | `RoundSessionFlow`, `ContinuousSessionFlow` |
| Custom enemy choice/rules | `IDirectorPolicy` |
| Custom spatial rules | `IPlacementPolicy` |
| Persistent mutable policies | `IPersistentDirectorPolicy`, `IPersistentPlacementPolicy` |
| Scheduling | `IEncounterSchedule`, `RuleBasedEncounterSchedule`, `MilestoneEncounterSchedule`, `CompositeEncounterSchedule` |
| Independent schedule lanes | `ConcurrentEncounterSchedule`, `ScheduleLane` |
| Encounter composition/retry | `IEncounterComposer`, `UniformEncounterComposer`, `WeightedEncounterComposer`, `RetryConfiguration` |
| Recovered spatial presets | `RecoveredDirectorDefaults`, `PlacementSelectionMode`, `HullVisibilitySamples` |
| Independent uniform class timers | `SpecialClassRotation`, `SpecialClassTimingConfiguration` |
| Crescendo waves | `WaveSequenceController` |
| Finale/objective stages | `ScenarioController` |
| Integrated stages and waves | `DirectorOrchestration` |
| Item/resource population | `ResourcePopulationController`, `IResourcePolicy` |
| Mode/count/music overrides | `IAdaptiveDirectorPolicy` |
| Population/timing overrides | `IAdaptivePopulationPolicy`, `IEncounterTimingPolicy` |
| Host callbacks | `IDirectorNotificationSink` |
| Save/load | `DirectorState`, `DirectorStateCodec` |
| Debug history | `DirectorTelemetryBuffer` |
| Player ownership lottery | `EncounterOwnershipAllocator` |
| Item/resource density budget | `ResourceBudget` |

Custom schedules that contain mutable state should implement `IPersistentEncounterSchedule`. If selecting an encounter changes tentative state, also implement `IEncounterScheduleFeedback` so failed placement or policy checks can roll it back.

Custom Director or placement policies that contain mutable deterministic state should also implement their corresponding persistence interface. Return a stable, versioned `PersistenceId` and string-keyed state. Restoration rejects missing or mismatched policy implementations instead of silently discarding saved state.

The same rule applies to mutable composers and resource policies through `IPersistentEncounterComposer` and `IPersistentResourcePolicy`. Give orchestration definitions a stable `DirectorOrchestrationConfiguration.PersistenceId`; the library also fingerprints stage, wave, seed, suppression, and encounter settings so a changed definition cannot silently consume incompatible state.

Music is intentionally a parallel subsystem rather than hidden inside
`DirectorCore`. This keeps authoritative world observation separate from client
audio state and allows one authority to emit a different decision stream for each
participant. Use direct `MusicGameplayEvent` values for mission/round/objective
hooks; their sequence IDs provide exact-once processing.
