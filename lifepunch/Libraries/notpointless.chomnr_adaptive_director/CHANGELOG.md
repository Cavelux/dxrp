# Changelog

## Unreleased

- Removed the in-repository playable demo. Demo development now lives in its own standalone s&box project so this repository remains focused on the reusable library.

## 1.4.0 - 2026-07-31

- Added a playable s&box demo on Facepunch's `facepunch.flatgrass` map.
- Added first-person movement, a hitscan demo blaster, animated citizen common/special/boss encounters, player damage, nearby-death pressure reporting, and a live telemetry HUD.
- Added runtime switching between continuous and round-based integration, plus optional music-intensity display and deterministic reset controls.
- Added `demo/run_demo.ps1`, including an optional sbox-mcp mount that waits for the HTTP listener before reporting readiness.
- Verified the demo in the real s&box editor: clean compiler gate, zero runtime errors, and observed live common, special, and boss objects created from Director requests.

## 1.3.1 - 2026-07-31

- Added a compiled example index covering manual integration, custom policies,
  combined round/music use, networked music, catalog arbitration, and complete
  save/restore.
- Added centralized pressure-reporting examples for recovered damage bands, ledge
  danger, first/later incapacitation, guarded revival, nearby threat deaths, custom
  danger events, authority handling, and diagnostics.
- Added a reusable example music catalog demonstrating priority, blocking, ducking,
  stop lists, automatic queues, split events, timing tags, lifecycle flags, special
  alerts, bosses, hazards, and positional cues.

## 1.3.0 - 2026-07-31

- Added a generalized deterministic Music Director recovered from the verified
  server/client control path.
- Added per-participant damage, activity, common-pressure, action, threat, calm,
  ambient, combat, adrenaline, and dormant-hazard mixer layers.
- Added configurable combat, mobbed, boss, dormant-hazard, special-alert,
  atmosphere, positional-stinger, scenario, and exact-once gameplay music rules.
- Added client/local event-catalog arbitration with four priorities, block/duck/stop
  lists, fades, delayed engagement, timing tags, autoqueue, and lifecycle flags.
- Added full authority/runtime state capture, JSON restoration, host runner, audio
  sink, optional s&box component, and a compilable music integration example.
- Extended deterministic tests and the real s&box editor gate to execute the music
  policy, runtime, catalog, and persistence path.

## 1.2.0 - 2026-07-31

- Added exact recovered placement modes: first-valid, greatest-progress with first tie, and native full-range random-transposition shuffle.
- Added area width/height eligibility and recovered boss/hazard placement constants and profiles.
- Added the recovered five-point hull visibility sample generator.
- Added uniform encounter composition and persistent independent special-class timers with recovered defaults.
- Corrected the evidence boundary: weighted placement and composition remain optional clean-room policies, not claimed L4D2 compatibility behavior.

## 1.1.0 - 2026-07-31

- Added independent concurrent encounter lanes matching the recovered survival scheduler decomposition.
- Added deterministic weighted encounter composition and persistent failed/partial/timeout retry queues.
- Added route relation, path distance, ingress, clearance and evidence-backed placement fallback support.
- Added resource density, conversion, avoidance, pickup and revisit-state orchestration.
- Integrated scenario stages and panic-wave sequences with the main Director decisions and persistence.
- Added adaptive mode/music/count callbacks and host decision/event notifications.
- Added configurable mode/pressure cooldown scaling and adaptive population-limit policy.
- Added persistence identity for composers, resource policies/rules and orchestration definitions.
- Made scenario and wave progression wait for confirmed host results; orchestration retries remain active while ordinary scheduling is suppressed.
- Added component-to-RVA evidence traceability and a 500-tick advanced deterministic simulation.

## 1.0.0 - 2026-07-31

- Added deterministic pressure, tempo, population, placement, scheduling, scenario, and wave controllers.
- Added generalized ambient, wave, special, boss, dormant-hazard, objective, and custom encounter kinds.
- Added rule, milestone, and composite schedules with persistent state and failed-selection rollback.
- Added complete Director state capture, JSON persistence, restoration, request expiry, and cancellation.
- Added state capture and identity-checked restoration contracts for custom Director and placement policies.
- Added round-based and continuous lifecycle adapters, authoritative host runner, and optional s&box component bridge.
- Added placement-policy, Director-policy, schedule, ownership-allocation, resource-budget, and diagnostics extension points.
- Added compilable integration examples, architecture/API/tuning documentation, 1,000-tick replay testing, and the real s&box editor gate.
