namespace SboxDirector;

public sealed class PressureConfiguration
{
    public float Gain { get; init; } = 0.25f;
    public float HoldSeconds { get; init; } = 5f;
    public float FullDecaySeconds { get; init; } = 30f;
    public float AveragedFollowingSeconds { get; init; } = 20f;
    public float LockValue { get; init; } = -1f;
    public IReadOnlyList<float> SeverityWeights { get; init; } = new[] { 0.05f, 0.20f, 0.50f, 1.00f, 999999.875f };

    public void Validate()
    {
        if (!float.IsFinite(Gain) || Gain < 0f) throw new ArgumentOutOfRangeException(nameof(Gain));
        if (!float.IsFinite(HoldSeconds) || HoldSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(HoldSeconds));
        if (!float.IsFinite(FullDecaySeconds) || FullDecaySeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(FullDecaySeconds));
        if (!float.IsFinite(AveragedFollowingSeconds) || AveragedFollowingSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(AveragedFollowingSeconds));
        if (SeverityWeights.Count != 5 || SeverityWeights.Any(x => !float.IsFinite(x) || x < 0f))
            throw new ArgumentException("Exactly five non-negative severity weights are required.");
    }
}

public sealed class TempoConfiguration
{
    public double BuildUpMinimumSeconds { get; init; } = 15;
    public double SustainPeakSeconds { get; init; } = 10;
    public double RelaxMinimumSeconds { get; init; } = 20;
    public double RelaxMaximumSeconds { get; init; } = 45;
    public float PeakThreshold { get; init; } = 0.9f;
    public float RelaxThreshold { get; init; } = 0.9f;
    public float RelaxMaximumProgressTravel { get; init; } = 0.15f;

    public void Validate()
    {
        if (!double.IsFinite(BuildUpMinimumSeconds) || !double.IsFinite(SustainPeakSeconds) || !double.IsFinite(RelaxMinimumSeconds) || !double.IsFinite(RelaxMaximumSeconds) || BuildUpMinimumSeconds < 0 || SustainPeakSeconds < 0 || RelaxMinimumSeconds < 0) throw new ArgumentOutOfRangeException("Tempo durations must be finite and non-negative.");
        if (RelaxMaximumSeconds < RelaxMinimumSeconds) throw new ArgumentException("RelaxMaximumSeconds must be >= RelaxMinimumSeconds.");
        if (!float.IsFinite(PeakThreshold) || !float.IsFinite(RelaxThreshold) || !float.IsFinite(RelaxMaximumProgressTravel) || PeakThreshold is < 0f or > 1f || RelaxThreshold is < 0f or > 1f || RelaxMaximumProgressTravel < 0f) throw new ArgumentOutOfRangeException("Tempo thresholds and travel must be finite and non-negative; thresholds must be in [0,1].");
    }
}

public sealed class PlacementProfile
{
    public PlacementSelectionMode SelectionMode { get; init; } = PlacementSelectionMode.WeightedScore;
    public float MinimumDistance { get; init; } = 500f;
    public float MaximumDistance { get; init; } = 2500f;
    public float IdealDistance { get; init; } = 1200f;
    public float MinimumThreatSeparation { get; init; } = 500f;
    public bool AllowVisible { get; init; }
    public float DistanceWeight { get; init; } = 1f;
    public float CapacityWeight { get; init; } = 0.25f;
    public float SeparationWeight { get; init; } = 0.25f;
    public float BehindProgressWeight { get; init; }
    public float MinimumPathDistance { get; init; }
    public float MinimumIngressDistance { get; init; }
    public float MinimumClearRadius { get; init; }
    public float MinimumAreaWidth { get; init; }
    public float MinimumAreaHeight { get; init; }
    public float PathDistanceWeight { get; init; }
    public float IngressWeight { get; init; }
    public float ClearRadiusWeight { get; init; }
    public IReadOnlySet<AdvanceRelation> AllowedRelations { get; init; } = new HashSet<AdvanceRelation>();
    public PlacementFallbackMode FallbackMode { get; init; } = PlacementFallbackMode.None;
    public IReadOnlySet<string> RequiredTags { get; init; } = new HashSet<string>();
    public IReadOnlySet<string> ExcludedTags { get; init; } = new HashSet<string>();
    public void Validate()
    {
        var measurements = new[] { MinimumDistance, MaximumDistance, IdealDistance, MinimumThreatSeparation, DistanceWeight, CapacityWeight, SeparationWeight, BehindProgressWeight, MinimumPathDistance, MinimumIngressDistance, MinimumClearRadius, MinimumAreaWidth, MinimumAreaHeight, PathDistanceWeight, IngressWeight, ClearRadiusWeight };
        if (measurements.Any(x => !float.IsFinite(x)) || MinimumDistance < 0 || MaximumDistance < MinimumDistance || IdealDistance < MinimumDistance || IdealDistance > MaximumDistance || MinimumThreatSeparation < 0 || MinimumPathDistance < 0 || MinimumIngressDistance < 0 || MinimumClearRadius < 0 || MinimumAreaWidth < 0 || MinimumAreaHeight < 0) throw new ArgumentException("Invalid placement distance or area profile.");
        if (RequiredTags.Overlaps(ExcludedTags)) throw new ArgumentException("Placement tags cannot be both required and excluded.");
    }
}

public sealed class PopulationConfiguration
{
    public IReadOnlyDictionary<EncounterKind, int> Limits { get; init; } = new Dictionary<EncounterKind, int>
    {
        [EncounterKind.Ambient] = 30,
        [EncounterKind.CommonWave] = 30,
        [EncounterKind.Special] = 4,
        [EncounterKind.Boss] = 1,
        [EncounterKind.DormantHazard] = 1,
        [EncounterKind.Objective] = 8,
        [EncounterKind.Custom] = 16
    };
}

public sealed class DirectorConfiguration
{
    public string Version { get; init; } = "1.2";
    public PressureConfiguration Pressure { get; init; } = new();
    public TempoConfiguration Tempo { get; init; } = new();
    public PopulationConfiguration Population { get; init; } = new();
    public IReadOnlyDictionary<EncounterKind, PlacementProfile> Placement { get; init; } = DefaultPlacement();
    public int Seed { get; init; } = 1;
    public double RequestTimeoutSeconds { get; init; } = 15;
    public int MaximumRequestsPerTick { get; init; } = 1;
    public bool ValidateWorldSnapshots { get; init; } = true;
    public RetryConfiguration Retry { get; init; } = new();

    public void Validate()
    {
        Pressure.Validate(); Tempo.Validate();
        if (string.IsNullOrWhiteSpace(Version)) throw new ArgumentException("A configuration version is required.", nameof(Version));
        if (RequestTimeoutSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(RequestTimeoutSeconds));
        if (MaximumRequestsPerTick <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumRequestsPerTick));
        Retry.Validate();
        foreach (var pair in Population.Limits) if (pair.Value < 0) throw new ArgumentOutOfRangeException($"Negative population limit for {pair.Key}.");
        foreach (var profile in Placement.Values) profile.Validate();
    }

    static IReadOnlyDictionary<EncounterKind, PlacementProfile> DefaultPlacement() => new Dictionary<EncounterKind, PlacementProfile>
    {
        [EncounterKind.Ambient] = new() { MinimumDistance = 300f, IdealDistance = 800f, MaximumDistance = 1800f },
        [EncounterKind.CommonWave] = new(),
        [EncounterKind.Special] = new() { MinimumDistance = 600f, IdealDistance = 1400f },
        [EncounterKind.Boss] = new() { MinimumDistance = 900f, IdealDistance = 1800f, MinimumThreatSeparation = 1200f },
        [EncounterKind.DormantHazard] = new() { MinimumDistance = 700f, IdealDistance = 1500f, MinimumThreatSeparation = 1000f },
        [EncounterKind.Objective] = new() { AllowVisible = true, MinimumDistance = 0f, MaximumDistance = 5000f },
        [EncounterKind.Custom] = new()
    };
}

public sealed class RetryConfiguration
{
    public int MaximumAttempts { get; init; } = 3;
    public double DelaySeconds { get; init; } = 1;
    public bool RetryRejected { get; init; } = true;
    public bool RetryFailed { get; init; } = true;
    public bool RetryPartialRemainder { get; init; } = true;
    public void Validate()
    {
        if (MaximumAttempts <= 0 || !double.IsFinite(DelaySeconds) || DelaySeconds < 0) throw new ArgumentException("Invalid request retry configuration.");
    }
}
