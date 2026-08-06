#nullable enable annotations
namespace SboxDirector;

public sealed class MusicCueConfiguration
{
    public IReadOnlyList<string> CombatIntroEvents { get; init; } = new[] { "combat.intro" };
    public string CombatSecondaryEvent { get; init; } = "combat.secondary";
    public string CombatCloseEvent { get; init; } = "combat.close";
    public IReadOnlyList<string> CombatTrackIds { get; init; } = new[] { "combat", "combat_secondary", "combat_close" };
    public string MobbedEvent { get; init; } = "combat.mobbed";
    public string BossApproachingEvent { get; init; } = "boss.approaching";
    public string BossTrackId { get; init; } = "boss";
    public string HazardBurningEvent { get; init; } = "hazard.burning";
    public string HazardAttackEvent { get; init; } = "hazard.attack";
    public string HazardRageEvent { get; init; } = "hazard.rage";
    public string HazardTrackId { get; init; } = "hazard";
    public string SafeAtmosphereEvent { get; init; } = "atmosphere.safe";
    public string DangerAtmosphereEvent { get; init; } = "atmosphere.danger";
    public string PositionalStingerEvent { get; init; } = "ambient.positional_stinger";
    public string AmbientMobEvent { get; init; } = "mob.ambient";
    public string MobSpawnEvent { get; init; } = "mob.spawn";
    public string MobSpawnBehindEvent { get; init; } = "mob.spawn_behind";
    public string LargeAreaRevealEvent { get; init; } = "world.large_area_reveal";
    public IReadOnlyList<string> WanderingHazardEventsLowToHigh { get; init; } = new[] { "hazard.wandering.tier4", "hazard.wandering.tier3", "hazard.wandering.tier2", "hazard.wandering.tier1" };
    public string WanderingHazardTrackId { get; init; } = "hazard_wandering";
}

public sealed class DynamicMusicConfiguration
{
    public double UpdateIntervalSeconds { get; init; } = 0.1;
    public float DamageRisePerSecond { get; init; } = 1f;
    public float DamageDecaySeconds { get; init; } = 10f;
    public float FastActivityRisePerSecond { get; init; } = 4f;
    public float FastActivityDecaySeconds { get; init; } = 1f;
    public float SlowActivityRisePerSecond { get; init; } = 3f;
    public float SlowActivityDecaySeconds { get; init; } = 3f;
    public float InflictedDamageRisePerSecond { get; init; } = .2f;
    public float InflictedDamageDecaySeconds { get; init; } = 4f;
    public float CommonSightNormalizer { get; init; } = 2f;
    public float CommonSightDecaySeconds { get; init; } = 8f;
    public float MobPressureDecaySeconds { get; init; } = 15f;
    public float ActionDecaySeconds { get; init; } = 10f;
    public float ThreatDecaySeconds { get; init; } = 6f;
    public float CalmDecaySeconds { get; init; } = 15f;
    public float AmbientDecaySeconds { get; init; } = 7f;
    public float DamageDuckInputMinimum { get; init; } = .5f;
    public float DamageDuckInputMaximum { get; init; } = 1f;
    public float DamageDuckOutputMaximum { get; init; } = .37f;
    public float MobDamageMinimum { get; init; } = .6f;
    public float MobDamageMaximum { get; init; } = 1f;
    public float MobOutputMinimum { get; init; } = .5f;
    public float MobOutputMaximum { get; init; } = 1f;
    public float CloseActionMaximum { get; init; } = .42f;
    public float AmbientInputMinimum { get; init; } = .5f;
    public float AmbientInputMaximum { get; init; } = .8f;
    public int CombatStopActiveCount { get; init; } = 8;
    public int CombatStopScannedCount { get; init; } = 3;
    public float CombatStopSignal { get; init; } = .01f;
    public float PositionalStingerAmbientMinimum { get; init; } = .2f;
    public float PositionalStingerBpm { get; init; } = 90f;
    public int PositionalStingerBeats { get; init; } = 16;
    public int PositionalStingerRandomMultiplierMaximum { get; init; } = 3;
    public double WanderingHazardIntervalSeconds { get; init; } = 2.5;
    public double LargeAreaRevealCooldownSeconds { get; init; } = 60;
}

public sealed class SpecialMusicAlertConfiguration
{
    public string ArchetypeId { get; init; } = "";
    public string CloseEvent { get; init; } = "";
    public string MiddleEvent { get; init; } = "";
    public string FarEvent { get; init; } = "";
    public float CloseMaximumDistance { get; init; } = 1200f;
    public float FarMinimumDistance { get; init; } = 1800f;
    public float ScanMaximumDistance { get; init; } = 2400f;
    public float Bpm { get; init; } = 80f;
    public int IntervalBeats { get; init; } = 5;
    public int RandomMultiplierMaximum { get; init; } = 5;
}

public sealed class MusicDirectorConfiguration
{
    public string Version { get; init; } = "music-1.0";
    public int Seed { get; init; } = 1;
    public bool ValidateSnapshots { get; init; } = true;
    public MusicCueConfiguration Cues { get; init; } = new();
    public DynamicMusicConfiguration Dynamic { get; init; } = new();
    public IReadOnlyList<SpecialMusicAlertConfiguration> SpecialAlerts { get; init; } = Array.Empty<SpecialMusicAlertConfiguration>();
    public float SpecialAlertMinimumAmbient { get; init; } = .1f;
    public float SpecialPeerStaggerStartSeconds { get; init; } = 1.5f;
    public float SpecialPeerStaggerStepSeconds { get; init; } = .1f;
    public float BossStopFadeSeconds { get; init; } = 2f;
    public float HazardStopFadeSeconds { get; init; } = 2f;
    public float HazardRageStopFadeSeconds { get; init; } = 1f;
    public float CombatStopFadeSeconds { get; init; } = 2f;
    public double AtmosphereRetrySeconds { get; init; } = 45;
    public double AtmosphereTransitionSeconds { get; init; } = 3;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Version)) throw new ArgumentException("A music configuration version is required.");
        var d = Dynamic;
        var values = new[] { d.UpdateIntervalSeconds, d.DamageRisePerSecond, d.DamageDecaySeconds, d.FastActivityRisePerSecond, d.FastActivityDecaySeconds, d.SlowActivityRisePerSecond, d.SlowActivityDecaySeconds, d.InflictedDamageRisePerSecond, d.InflictedDamageDecaySeconds, d.CommonSightNormalizer, d.CommonSightDecaySeconds, d.MobPressureDecaySeconds, d.ActionDecaySeconds, d.ThreatDecaySeconds, d.CalmDecaySeconds, d.AmbientDecaySeconds, d.PositionalStingerBpm, d.WanderingHazardIntervalSeconds, d.LargeAreaRevealCooldownSeconds };
        if (values.Any(x => !double.IsFinite(x) || x <= 0)) throw new ArgumentException("Dynamic music rates and durations must be finite and positive.");
        var ranges = new[] { d.DamageDuckInputMinimum, d.DamageDuckInputMaximum, d.DamageDuckOutputMaximum, d.MobDamageMinimum, d.MobDamageMaximum, d.MobOutputMinimum, d.MobOutputMaximum, d.CloseActionMaximum, d.AmbientInputMinimum, d.AmbientInputMaximum, d.CombatStopSignal, d.PositionalStingerAmbientMinimum };
        if (ranges.Any(x => !float.IsFinite(x) || x < 0) || d.DamageDuckInputMaximum <= d.DamageDuckInputMinimum || d.MobDamageMaximum <= d.MobDamageMinimum || d.AmbientInputMaximum <= d.AmbientInputMinimum || d.CloseActionMaximum <= 0 || d.DamageDuckOutputMaximum > 1 || d.MobOutputMinimum > d.MobOutputMaximum || d.MobOutputMaximum > 1 || d.PositionalStingerAmbientMinimum > 1) throw new ArgumentException("Dynamic music remap ranges are invalid.");
        if (d.CombatStopActiveCount < 0 || d.CombatStopScannedCount < 0 || d.PositionalStingerBeats <= 0 || d.PositionalStingerRandomMultiplierMaximum <= 0) throw new ArgumentException("Dynamic music counts must be non-negative and beat controls positive.");
        if (SpecialAlerts.Select(x => x.ArchetypeId).Distinct(StringComparer.Ordinal).Count() != SpecialAlerts.Count || SpecialAlerts.Any(x => string.IsNullOrWhiteSpace(x.ArchetypeId) || string.IsNullOrWhiteSpace(x.CloseEvent) || string.IsNullOrWhiteSpace(x.MiddleEvent) || string.IsNullOrWhiteSpace(x.FarEvent) || !float.IsFinite(x.CloseMaximumDistance) || !float.IsFinite(x.FarMinimumDistance) || !float.IsFinite(x.ScanMaximumDistance) || x.CloseMaximumDistance < 0 || x.FarMinimumDistance < x.CloseMaximumDistance || x.ScanMaximumDistance < x.FarMinimumDistance || x.Bpm <= 0 || x.IntervalBeats <= 0 || x.RandomMultiplierMaximum <= 0)) throw new ArgumentException("Special music alert definitions are invalid or duplicated.");
        var fades = new[] { BossStopFadeSeconds, HazardStopFadeSeconds, HazardRageStopFadeSeconds, CombatStopFadeSeconds, SpecialPeerStaggerStartSeconds, SpecialPeerStaggerStepSeconds };
        if (!float.IsFinite(SpecialAlertMinimumAmbient) || SpecialAlertMinimumAmbient is < 0 or > 1 || fades.Any(x => !float.IsFinite(x) || x < 0) || !double.IsFinite(AtmosphereRetrySeconds) || !double.IsFinite(AtmosphereTransitionSeconds) || AtmosphereRetrySeconds < 0 || AtmosphereTransitionSeconds < 0) throw new ArgumentException("Music thresholds and timers are invalid.");
        if (Cues.CombatIntroEvents.Any(string.IsNullOrWhiteSpace) || Cues.CombatTrackIds.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Configured combat music ids cannot be empty.");
        if (Cues.WanderingHazardEventsLowToHigh.Count != 4 || Cues.WanderingHazardEventsLowToHigh.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Exactly four wandering-hazard tier events are required.");
    }
}

/// <summary>Recovered numeric behavior packaged without any game audio or entity names.</summary>
public static class RecoveredMusicDirectorDefaults
{
    public static MusicDirectorConfiguration CreateConfiguration(MusicCueConfiguration? cues = null, IReadOnlyList<SpecialMusicAlertConfiguration>? specialAlerts = null, int seed = 1) => new()
    {
        Version = "recovered-music-2026.06",
        Seed = seed,
        Cues = cues ?? new MusicCueConfiguration(),
        SpecialAlerts = specialAlerts ?? Array.Empty<SpecialMusicAlertConfiguration>()
    };
}
