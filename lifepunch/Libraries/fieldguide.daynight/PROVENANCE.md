# Provenance

Extracted from the World Builder game project (a first-party Field Guide s&box game). The game itself
stays closed; only this extracted kit code is MIT-licensed.

- Source repository: world_builder (private)
- Source commit: 864ba184d6639b239db6348c58d1fbe56fb16963 (2026-07-21)
- Extraction date: 2026-07-21
- License: MIT

## What came from where

- `SkyGrade.ComputeGrade` / `ApplyGradeTo` / `ClockRateScale` / `Smoothstep`: `Code/Game/WbDayNight.cs`,
  parameterized on `DayNightConfig` instead of the game's `Tuning` static.
- `WeatherRoll.For`: `WbDayNight.WeatherFor`, the FNV-1a hash ported byte-for-byte (offset basis, prime,
  and the `0x57139A2F` salt are unchanged), so the per-day roll is identical to the source game's.
- `SkyWeights.WeightsFor`: `Code/Game/WbSky.cs` `WeightsFor`, generalized to take the four anchor hours
  from config. This is the SEAM that replaces the game's four-sky blend shader (see the README).
- `DayNightClock`: the host-authoritative clock surface from `Code/Game/WorldSession.cs`
  (`NetTimeOfDay` + the `NetTimePaused` pause pin + `NetWeatherOverride`, the fixed-tick accumulate, and
  the client extrapolation with the pause hardening). Lifted out of `WorldSession` into a standalone
  component with its own `For(Scene)` resolver.
- `TimeMath.ComputeSetHour` / `ComputeSliderHour`: the pure time-set helpers from `WorldSession`.
- `DayNightSelfTest`: the day/night and weather cases from `Code/Game/PureTests.cs`
  (`DayWeatherDerivation`, `DayNightAnchorExact`, `DayNightTwilightContinuity`,
  `DayNightRateNightAndMidday`, `DayNightRateContinuousAtTwilight`, `DayNightRatioIs3x`,
  `ComputeSetHourTest`), re-pointed at the kit's config-driven math.
- `RainStreaks` (optional): the cosmetic rain FX from `WbDayNight`, decoupled from the game's player and
  possession types via a `Func<Vector3>` centre seam.

## Written for the kit, not extracted

These have no upstream counterpart. World Builder drives its day/night from the game's own dev panel and
its own scenes, neither of which is library material, so the kit's UI and demo were built here against
the Field Kit UI system (`docs/design/ui-system/daynight-kit.dc.html` in the libraries repo).

- `Code/Ui/DayNightPanel.razor` + `.razor.scss`: the dev tuning surface. New work.
- `Code/Demo/DayNightDemoBootstrap.cs`, `Code/Demo/DayNightHintCard.razor` + `.razor.scss`, and
  `Assets/scenes/daynight_demo.scene`: the demo scene and its hint card. New work.

## Deliberately NOT extracted

- `wb_sky_tod.shader` and the four equirectangular sky textures: World Builder flagship material with a
  first-touch shader-compile gotcha. The kit exposes `SkyWeights` as the seam instead and the consumer
  drives their own sky.
- `WbTimeBridge` (the editor-to-game command bridge for the `wb_time` MCP tool): editor-specific.
- `WbSkyDriver`: the game's shader-material driver, which only makes sense with the shader above.

## Backport policy

Backports are deliberate and release-shaped: when the upstream game lands a change worth shipping (a grade
tweak, a clock hardening), port it, bump the kit version, and record the new upstream commit above.
