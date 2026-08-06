#nullable enable annotations
namespace SboxDirector;

/// <summary>Constants recovered from a verified retail Director build. The names are generalized; hosts own navigation and trace execution.</summary>
public static class RecoveredDirectorDefaults
{
    public const int MaximumThreatAreas = 4;
    public const int MaximumBossRouteSteps = 100;
    public const int BossFallbackAttempts = 5;
    public const float ThreatSeparationMinimum = 5000f;
    public const float ThreatSeparationMaximum = 5000f;
    public const float ThreatCandidateRadius = 240f;
    public const float RegisteredThreatRadius = 1000f;
    public const float ThreatClearRadius = 500f;
    public const float IngressRange = 3000f;
    public const float VersusBossBuffer = 2200f;
    public const float OrdinaryInitialSpacingMinimumMultiplier = 0.8f;
    public const float OrdinaryInitialSpacingMaximumMultiplier = 1f;
    public const float OrdinaryLaterSpacingMinimumMultiplier = 1f;
    public const float OrdinaryLaterSpacingMaximumMultiplier = 1.2f;
    public const float VersusInitialDistanceMinimum = 3000f;
    public const float VersusInitialDistanceMaximum = 8000f;
    public const float VersusFirstMapInitialDistanceMinimum = 8000f;
    public const float VersusFirstMapInitialDistanceMaximum = 12000f;
    public const float MinimumAreaWidth = 24f;
    public const float MinimumAreaHeight = 24f;
    public const float MaximumRouteClimb = 66f;
    public const float MaximumRouteDrop = 240f;
    public const float BossFallbackAreaRadius = 120f;
    public const float DormantHazardHullWidth = 26f;
    public const float DormantHazardHullHeight = 68f;
    public const float BossHullWidth = 32f;
    public const float BossHullHeight = 72f;
    public const uint RouteExcludedAttributeMask = 0x00070000;
    public const uint ThreatRejectedAttributeBit = 0x00080000;
    public const uint StartingAreaRejectedAttributeBit = 0x00000040;
    public const uint BossFallbackExcludedAttributeMask = 0x00090882;
    public const uint VisibilityTraceMask = 0x02006041;

    public static PlacementProfile CreateThreatAreaProfile() => new()
    {
        SelectionMode = PlacementSelectionMode.NativeRandomTranspositionFirst,
        MinimumDistance = 0f,
        IdealDistance = 0f,
        MaximumDistance = float.MaxValue,
        MinimumThreatSeparation = 0f,
        MinimumAreaWidth = MinimumAreaWidth,
        MinimumAreaHeight = MinimumAreaHeight
    };

    public static PlacementProfile CreateRouteStepProfile() => new()
    {
        SelectionMode = PlacementSelectionMode.HighestProgressFirstTie,
        AllowVisible = true,
        MinimumDistance = 0f,
        IdealDistance = 0f,
        MaximumDistance = float.MaxValue,
        MinimumThreatSeparation = 0f
    };
}

public static class HullVisibilitySamples
{
    /// <summary>Returns the recovered lower-left, lower-right, upper-center, upper-left and upper-right sample pattern.</summary>
    public static IReadOnlyList<DirectorVector> CreateFivePointPattern(DirectorVector origin, DirectorVector observer, float hullWidth, float hullHeight)
    {
        if (!float.IsFinite(hullWidth) || !float.IsFinite(hullHeight) || hullWidth < 0f || hullHeight < 0f) throw new ArgumentOutOfRangeException(nameof(hullWidth));
        var dx = origin.X - observer.X;
        var dy = origin.Y - observer.Y;
        var length = MathF.Sqrt(dx * dx + dy * dy);
        var sideX = length > 0.00001f ? -dy / length : 1f;
        var sideY = length > 0.00001f ? dx / length : 0f;
        var halfWidth = hullWidth * 0.5f;
        var halfHeight = hullHeight * 0.5f;
        var left = new DirectorVector(origin.X + sideX * halfWidth, origin.Y + sideY * halfWidth, origin.Z);
        var right = new DirectorVector(origin.X - sideX * halfWidth, origin.Y - sideY * halfWidth, origin.Z);
        return new[]
        {
            left,
            right,
            new DirectorVector(origin.X, origin.Y, origin.Z + halfHeight),
            new DirectorVector(left.X, left.Y, origin.Z + hullHeight),
            new DirectorVector(right.X, right.Y, origin.Z + hullHeight)
        };
    }
}

public sealed class SpecialClassTimingConfiguration
{
    public double InitialDelayMinimum { get; init; } = 30;
    public double InitialDelayMaximum { get; init; } = 60;
    public double InitialDelayMaximumExtra { get; init; } = 180;
    public double RespawnInterval { get; init; } = 45;
    public double IneligibleRetryDelay { get; init; } = 20;
    public double FailedSpawnRetryDelay { get; init; } = 5;
    public double SuccessfulClassHold { get; init; } = 999;
    public void Validate()
    {
        var values = new[] { InitialDelayMinimum, InitialDelayMaximum, InitialDelayMaximumExtra, RespawnInterval, IneligibleRetryDelay, FailedSpawnRetryDelay, SuccessfulClassHold };
        if (values.Any(x => !double.IsFinite(x) || x < 0) || InitialDelayMaximum < InitialDelayMinimum || InitialDelayMaximumExtra < InitialDelayMaximum) throw new ArgumentException("Invalid special-class timing configuration.");
    }
}

/// <summary>Persistent independent class timers with uniform due-and-eligible selection.</summary>
public sealed class SpecialClassRotation
{
    readonly string[] _archetypes;
    readonly Dictionary<string, double> _nextTimes = new(StringComparer.Ordinal);
    readonly SpecialClassTimingConfiguration _configuration;

    public SpecialClassRotation(IEnumerable<string> archetypes, SpecialClassTimingConfiguration? configuration = null)
    {
        _archetypes = archetypes?.ToArray() ?? throw new ArgumentNullException(nameof(archetypes));
        if (_archetypes.Length == 0 || _archetypes.Any(string.IsNullOrWhiteSpace) || _archetypes.Distinct(StringComparer.Ordinal).Count() != _archetypes.Length) throw new ArgumentException("Archetypes must be unique and non-empty.", nameof(archetypes));
        _configuration = configuration ?? new SpecialClassTimingConfiguration();
        _configuration.Validate();
    }

    public void Initialize(double now, DeterministicRandom random, bool useMaximumExtra = false)
    {
        if (!double.IsFinite(now)) throw new ArgumentOutOfRangeException(nameof(now));
        var maximum = useMaximumExtra ? _configuration.InitialDelayMaximumExtra : _configuration.InitialDelayMaximum;
        foreach (var archetype in _archetypes) _nextTimes[archetype] = now + _configuration.InitialDelayMinimum + random.NextFloat() * (maximum - _configuration.InitialDelayMinimum);
    }

    public string? SelectDue(double now, Func<string, bool> isEligible, DeterministicRandom random)
    {
        if (!double.IsFinite(now)) throw new ArgumentOutOfRangeException(nameof(now));
        if (isEligible is null) throw new ArgumentNullException(nameof(isEligible));
        var choices = new List<string>();
        foreach (var archetype in _archetypes)
        {
            if (!_nextTimes.TryGetValue(archetype, out var next) || now < next) continue;
            if (isEligible(archetype)) choices.Add(archetype);
            else _nextTimes[archetype] = now + _configuration.IneligibleRetryDelay;
        }
        return choices.Count == 0 ? null : choices[random.NextInt(choices.Count)];
    }

    public void ReportSpawnFailure(string archetype, double now) => SetNext(archetype, now, _configuration.FailedSpawnRetryDelay);
    public void ReportSpawnSuccess(string archetype, double now) => SetNext(archetype, now, _configuration.SuccessfulClassHold);
    public void ReleaseAfterLifecycle(string archetype, double now) => SetNext(archetype, now, _configuration.RespawnInterval);
    public IReadOnlyDictionary<string, double> CaptureState() => new Dictionary<string, double>(_nextTimes, StringComparer.Ordinal);
    public void RestoreState(IReadOnlyDictionary<string, double> state)
    {
        if (state.Count != _archetypes.Length || _archetypes.Any(x => !state.TryGetValue(x, out var time) || !double.IsFinite(time))) throw new InvalidOperationException("Special-class rotation state does not match its archetypes.");
        _nextTimes.Clear(); foreach (var pair in state) _nextTimes[pair.Key] = pair.Value;
    }
    void SetNext(string archetype, double now, double delay)
    {
        if (!_archetypes.Contains(archetype, StringComparer.Ordinal)) throw new ArgumentException("Unknown archetype.", nameof(archetype));
        if (!double.IsFinite(now)) throw new ArgumentOutOfRangeException(nameof(now));
        _nextTimes[archetype] = now + delay;
    }
}
