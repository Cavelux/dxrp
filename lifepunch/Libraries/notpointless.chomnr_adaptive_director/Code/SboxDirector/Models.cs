#nullable enable annotations
namespace SboxDirector;

public enum TempoPhase { BuildUp, SustainPeak, PeakFade, Relax }
public enum PressureSeverity { Minor = 1, Moderate = 2, Major = 3, Critical = 4, Maximum = 5 }
public enum EncounterKind { Ambient, CommonWave, Special, Boss, DormantHazard, Objective, Custom }
public enum RequestResultKind { Succeeded, PartiallySucceeded, Rejected, Deferred, Failed }
public enum WaveState { Inactive, InitialDelay, IssueWave, WaitForClear, Pause, Done }
public enum ScenarioStatus { Inactive, Running, Complete, Failed }
public enum AdvanceRelation { Unknown, Ahead, Behind, Lateral, ObjectiveAdjacent }
public enum PlacementFallbackMode { None, IgnoreRouteConstraints, AnyReachableHidden }
public enum PlacementSelectionMode { WeightedScore, FirstEligible, HighestProgressFirstTie, NativeRandomTranspositionFirst }

public readonly record struct DirectorVector(float X, float Y, float Z)
{
    public static float DistanceSquared(DirectorVector a, DirectorVector b)
    {
        var x = a.X - b.X; var y = a.Y - b.Y; var z = a.Z - b.Z;
        return x * x + y * y + z * z;
    }
}

public sealed class ParticipantSnapshot
{
    public string Id { get; init; } = "";
    public bool IsAlive { get; init; } = true;
    public bool IsEligible { get; init; } = true;
    public bool IsInCombat { get; init; }
    public DirectorVector Position { get; init; }
    public float Progress { get; init; }
}

public sealed class PopulationSnapshot
{
    readonly Dictionary<EncounterKind, int> _counts = new();
    public PopulationSnapshot() { }
    public PopulationSnapshot(IReadOnlyDictionary<EncounterKind, int> counts) { foreach (var pair in counts) _counts[pair.Key] = Math.Max(0, pair.Value); }
    public int this[EncounterKind kind] => _counts.TryGetValue(kind, out var value) ? value : 0;
    public IReadOnlyDictionary<EncounterKind, int> Counts => _counts;
}

public sealed class SpawnCandidate
{
    public string Id { get; init; } = "";
    public DirectorVector Position { get; init; }
    public bool Reachable { get; init; }
    public bool Spawnable { get; init; }
    public bool Visible { get; init; }
    public float NearestParticipantDistance { get; init; }
    public float Progress { get; init; }
    public float AreaCapacity { get; init; } = 1f;
    public float ThreatSeparation { get; init; }
    public float RelativeProgress { get; init; }
    public float PathDistance { get; init; }
    public float IngressDistance { get; init; }
    public float ClearRadius { get; init; }
    public float AreaWidth { get; init; }
    public float AreaHeight { get; init; }
    public AdvanceRelation AdvanceRelation { get; init; }
    public IReadOnlySet<string> Tags { get; init; } = new HashSet<string>();
}

public sealed class WorldSnapshot
{
    public double Time { get; init; }
    public IReadOnlyList<ParticipantSnapshot> Participants { get; init; } = Array.Empty<ParticipantSnapshot>();
    public PopulationSnapshot Population { get; init; } = new();
    public IReadOnlyList<SpawnCandidate> SpawnCandidates { get; init; } = Array.Empty<SpawnCandidate>();
    public bool CombatActive { get; init; }
    public float TeamProgress { get; init; }
    public string ModeId { get; init; } = "continuous";
    public string? RoundId { get; init; }
    public ResourceWorldSnapshot Resources { get; init; } = new();

    public void Validate()
    {
        if (double.IsNaN(Time) || double.IsInfinity(Time)) throw new ArgumentException("World time must be finite.");
        if (float.IsNaN(TeamProgress) || float.IsInfinity(TeamProgress)) throw new ArgumentException("Team progress must be finite.");
        if (Participants.Any(x => string.IsNullOrWhiteSpace(x.Id)) || Participants.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != Participants.Count) throw new ArgumentException("Participant ids must be non-empty and unique.");
        if (SpawnCandidates.Any(x => string.IsNullOrWhiteSpace(x.Id)) || SpawnCandidates.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != SpawnCandidates.Count) throw new ArgumentException("Spawn candidate ids must be non-empty and unique.");
        if (SpawnCandidates.Any(x => !float.IsFinite(x.NearestParticipantDistance) || !float.IsFinite(x.Progress) || !float.IsFinite(x.RelativeProgress) || !float.IsFinite(x.AreaCapacity) || !float.IsFinite(x.ThreatSeparation) || !float.IsFinite(x.PathDistance) || !float.IsFinite(x.IngressDistance) || !float.IsFinite(x.ClearRadius) || !float.IsFinite(x.AreaWidth) || !float.IsFinite(x.AreaHeight) || x.NearestParticipantDistance < 0 || x.AreaCapacity < 0 || x.ThreatSeparation < 0 || x.PathDistance < 0 || x.IngressDistance < 0 || x.ClearRadius < 0 || x.AreaWidth < 0 || x.AreaHeight < 0)) throw new ArgumentException("Spawn candidate distances, dimensions, and capacity must be finite and non-negative.");
        Resources.Validate();
    }
}

public sealed record EncounterMember(string Archetype, int Count);

public sealed record SpawnRequest(
    long RequestId,
    EncounterKind Kind,
    int RequestedCount,
    string CandidateId,
    string Archetype,
    string Reason)
{
    public IReadOnlyList<EncounterMember> Composition { get; init; } = Array.Empty<EncounterMember>();
    public int Attempt { get; init; } = 1;
    public bool Equals(SpawnRequest? other) => other is not null && RequestId == other.RequestId && Kind == other.Kind && RequestedCount == other.RequestedCount && CandidateId == other.CandidateId && Archetype == other.Archetype && Reason == other.Reason && Attempt == other.Attempt && Composition.SequenceEqual(other.Composition);
    public override int GetHashCode()
    {
        var hash = HashCode.Combine(RequestId, Kind, RequestedCount, CandidateId, Archetype, Reason, Attempt); foreach (var member in Composition) hash = HashCode.Combine(hash, member); return hash;
    }
}

public sealed record SpawnRequestResult(
    long RequestId,
    RequestResultKind Result,
    int SpawnedCount,
    string? Detail = null);

public sealed record DirectorEvent(string Name, string Reason, double Time);

public sealed class DirectorDecisionBatch
{
    public double Time { get; init; }
    public TempoPhase Tempo { get; init; }
    public float TeamPressure { get; init; }
    public float MusicIntensity { get; init; }
    public List<SpawnRequest> SpawnRequests { get; } = new();
    public List<ResourceSpawnRequest> ResourceRequests { get; } = new();
    public List<DirectorEvent> Events { get; } = new();
}

public sealed record PressureEvent(string ParticipantId, PressureSeverity Severity, string Reason);

public sealed record DirectorDiagnostic(double Time, string Category, string Message);
