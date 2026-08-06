# L4D2 evidence traceability

This library is a clean-room behavioral implementation. It contains no Valve source or decompiled source reproduction. Evidence comes from the legally installed June 30, 2026 x86 `server.dll` identified in the [research repository](https://github.com/zeljkovranjes/l4d2-director-system-research), plus sanitized local runtime traces.

“Proven” below means the shipped binary, registered configuration, or a runtime trace establishes the behavior. It does not mean the original C++ source is available.

| Generalized implementation | L4D2 evidence | Confidence | Clean-room boundary |
|---|---|---|---|
| `PressureTracker`, `TeamPressure` | intensity methods/call sites and runtime validation documented in `INTENSITY_RECOVERY.md`; averaged formula matched 209 native updates | Proven | Public types and storage format are original |
| `TempoController` | four-state control flow and intensity consumers in `tempo_state_machine_intensity.json` | Proven state order and inputs | Public configuration/API are original |
| `WaveSequenceController` | panic machine RVA `0x0028C400`; jump table `0x0028C754`; live `4→0→1→2` trace | Proven states, counters, `[1,2]` setup and 10-second captured timer | General configuration and host request protocol are original |
| `ConcurrentEncounterSchedule` | survival scheduler RVA `0x0028EE00` independently handles lull, horde, special intervals and boss stages | Strong structural evidence | Lane priorities and serialization keys are original |
| `DirectorOrchestration` | finale state machine RVA `0x0028B7C0`, stage callbacks RVA `0x0028A2B0`, panic controller | Strong structural evidence | General stage definitions and suppression switch are original |
| `UniformEncounterComposer`, `SpecialClassRotation` | special selector RVA `0x0026F910` | Uniform due-and-eligible class draw and `30–60/180/45/20/5/999` timer behavior proven | Host archetype names and lifecycle integration are original |
| `WeightedEncounterComposer` | no native-equivalence claim | Optional library feature | All weights are host configuration |
| Retry queue and failed-result handling | explicit custom-finale boss creation failure path RVA `0x002881A0`; pending-mob logic RVA `0x00287710` | Failure/recovery responsibility proven | Default three attempts and one-second delay are clean-room defaults |
| `PlacementSelector`, `RecoveredDirectorDefaults`, `HullVisibilitySamples` | boss/hazard selector `0x00274BC0`, visibility `0x0028F4C0/0x0028F6D0`, route step `0x002EAEB0`, threat generator `0x0035F370` | Exact first-valid shuffle, flow argmax/first tie, dimensions, trace pattern and fallback constants recovered | Host nav enumeration, traces and optional weighted mode are original |
| `EncounterOwnershipAllocator` | single eligible offer RVA `0x002609D0`; weighted ticket lottery RVA `0x0026C520` | Strong control-flow evidence | Generic candidate contract and RNG are original |
| `ResourcePopulationController` | cleanup `0x00280310`, conversion `0x00280750`, category report `0x002812D0`, density/revisit `0x00283C40`, clearing `0x00284640` | Responsibilities proven | Density target algorithm and category names are original/configurable |
| `IResourcePolicy` | callbacks `AllowWeaponSpawn 0x00279D20`, `ConvertWeaponSpawn 0x00279E80`, `ShouldAvoidItem 0x0027A1A0`, `CanPickupObject 0x0027A250` | Named callbacks proven | General resource terminology and signatures are original |
| `IAdaptiveDirectorPolicy` and notifications | boss music `0x0027A0F0`, mob-with-boss `0x00289F30`, stage/music `0x0028A2B0`, option loader `0x002758B0` | Named callbacks and override surface proven | Count/music formulas are host policy |
| `MusicDirectorCore`, `RecoveredMusicDirectorDefaults` | server `Music` constructor `0x002B0030`, input scan `0x002B0CE0`, dynamic core `0x002B1710`, update `0x002B3C10`, envelope helper `0x002ABC50` | Continuous equations, default constants, combat/mob/boss/hazard/special/atmosphere transitions and all 85 virtual callbacks recovered | Public state, generalized cue IDs and host observations are original |
| Semantic music decisions | server play `0x002AB830`, stop `0x002AABD0`, general `0x002AAD20`; client discriminator `0x0024B470` | Five command meanings and replicated layer fields proven | Library records replace the game-specific bit protocol |
| `MusicRuntime`, `MusicEventCatalog` | client mixer `0x0024A630`, play `0x0024ABF0`, stop `0x0024A970`, metadata `0x0024FFD0`, queue `0x00250390`, track engagement `0x002507A0` | Priority ordinal, block/duck/stop lists, fades, delay, tags, autoqueue and lifecycle gates recovered | Deterministic public API, adapter actions, persistence and same-track tie policy are clean-room engineering |
| JSON persistence, xorshift RNG, diagnostics and s&box adapter | no claim of native equivalence | Library engineering | Entirely original |

## Machine-readable proof

The most relevant reproducible artifacts are:

- `research/analysis/panic_machine_disassembly.json`
- `research/analysis/survival_scheduler_disassembly.json`
- `research/analysis/finale_machine_disassembly.json`
- `research/analysis/threat_placement_disassembly.json`
- `research/analysis/placement_and_specials_recovery.json`
- `research/analysis/tank_offer_disassembly.json`
- `research/analysis/director_global_xrefs.json`
- `research/analysis/music_director_server.json`
- `research/analysis/music_director_client.json`
- `research/analysis/music_sound_events.json`
- `research/oracle/panic_high_frequency_trace.jsonl`
- `research/analysis/intensity_averaged_validation.json`

See [FUNCTION_CATALOG.md](https://github.com/zeljkovranjes/l4d2-director-system-research/blob/main/research/FUNCTION_CATALOG.md) for the complete RVA index and [COVERAGE_AUDIT.md](https://github.com/zeljkovranjes/l4d2-director-system-research/blob/main/research/COVERAGE_AUDIT.md) for irreducible limits.

The readable music evidence and clean-room contract are
[MUSIC_DIRECTOR_RECOVERY.md](https://github.com/zeljkovranjes/l4d2-director-system-research/blob/main/research/MUSIC_DIRECTOR_RECOVERY.md)
and
[MUSIC_IMPLEMENTATION_SPEC.md](https://github.com/zeljkovranjes/l4d2-director-system-research/blob/main/research/MUSIC_IMPLEMENTATION_SPEC.md).
