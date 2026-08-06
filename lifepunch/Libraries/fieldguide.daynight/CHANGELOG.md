# Changelog: Field Guide Day Night Kit

Display versions are `0.x.y` while the kit API settles. Bumped on every publish (minor per content
release, patch for hotfix, manual bumps only).

## v0.1.0 (unreleased)

First release. Extracted from World Builder (commit 864ba18) on 2026-07-21, then finished for publish
with a UI surface, a demo scene and a headless gate.

Added in the pre-publish pass (2026-07-31):

- `DayNightPanel`, the kit's dev tuning surface on the Field Kit UI system: a hero clock readout, a
  day-preserving time scrub, a day-length dial, four jump chips read off your own config, weather pins
  with a way back to the deterministic roll, run / pause, and a Copy config button that puts a
  paste-ready `DayNightConfig` block on your clipboard. Toggle with `N` or the `daynight_panel` convar.
  Optional: delete `Code/Ui/` and nothing else in the kit notices.
- The panel starts CLOSED unless `OpenOnStart` is set, and that property is the only thing that decides
  the boot state. s&box persists convars between sessions, so a `daynight_panel 1` left set weeks ago
  must never be able to open a tuning panel over a shipped game; the panel logs when it forces a stale
  value closed.
- The panel is honest on a client. Every clock setter is authority-only, so instead of letting a drag
  fail silently the card says the host owns the clock. The two CONFIG writes (day length, reset) are
  guarded in the panel itself, because config is authoring data and is not replicated: a client that
  changed its own day length would extrapolate at a different pace than the host.
- `Assets/scenes/daynight_demo.scene`, a runnable demo wired by `DayNightDemoBootstrap`. A lit shape
  cluster under a four-real-minute day, so a whole cycle plays out while you watch: the sun sweeps, the
  shadows swing a quarter turn, the grade warms into dusk and drops into a genuinely dark night. The
  shapes are chosen for their shadows rather than their looks. Everything in it ships with the engine.
- `DayNightHintCard`, the demo's on-screen key card, up from the first frame. It also prints the live
  sky weights, which is the only way to SEE the sky seam in a kit that deliberately ships no sky art:
  the four numbers hand off from one slot to the next as the clock runs.
- Demo content is inert by construction. The kit ships no scanned GameResource, so nothing can load
  itself into a consumer's game, and the demo UI is gated on a flag only the demo bootstrap sets.
- `tools/daynight_compile/`, a headless gate that compiles the real kit source against engine stubs and
  RUNS the self-test battery, plus `check_razor_code.py` for the C# inside the razor files. The maths
  types in the stubs are faithful implementations rather than empty shapes, which is what makes a green
  run mean something.

Fixed in the pre-publish pass (2026-07-31):

- `TimeMath.ComputeSliderHour` could return 24.000002, one float step past the end of the day. One
  in-game minute is not exactly representable in binary, so 1440 quantized steps overshoot; a full-right
  drag therefore handed `ComputeSetHour` a value past midnight, which tipped the day index over and
  re-rolled the day's weather. The result is now clamped after quantizing, and the self-test battery
  checks the range. Found by the new headless gate.

Added in the first extraction pass (2026-07-21):

- Host-authoritative day/night clock (`DayNightClock`): a thin `[Sync(FromHost)]` component that
  accumulates game-time on the host, extrapolates smoothly on clients, and carries the pause pin so a
  paused host never leaves clients free-running.
- Pure grading math (`SkyGrade`): sun rotation plus sun/sky/envmap colour grades for any game-hour and
  weather, anchor-exact at the reference hour, with a twilight blend and a non-uniform clock rate that
  makes the day run three times longer than the night in real time.
- Deterministic per-day weather (`WeatherRoll`): an FNV-1a hash of (seed, day) into Clear / Cloudy / Rain,
  so a host and every client agree without extra networking.
- The sky seam (`SkyWeights`): four normalized crossfade weights for any hour, so a consumer drives their
  own sky (the kit ships no shader or sky art).
- Pure time-set helpers (`TimeMath`): day-preserving time set and a minute-quantized slider mapping.
- Optional convenience driver (`DayNightDriver`) and optional cosmetic rain module (`RainStreaks`), both
  deletable, both decoupled from any game type through delegate seams.
- A pure self-test battery (`DayNightSelfTest`, console `fg_daynight_selftest`) covering weather
  determinism, anchor-exactness, twilight continuity, the clock rate, the 3:1 day:night ratio, the sky
  weight partition, and the time-set math.
