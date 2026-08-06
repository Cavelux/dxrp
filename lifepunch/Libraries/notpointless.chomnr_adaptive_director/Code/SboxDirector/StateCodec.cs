using System.Text.Json;

namespace SboxDirector;

public static class DirectorStateCodec
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static string ToJson(DirectorState state) => JsonSerializer.Serialize(state, Options);
    public static DirectorState FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("State JSON is required.", nameof(json));
        var state = JsonSerializer.Deserialize<DirectorState>(json, Options);
        var incompleteRequests = state.IssuedRequests is null || state.RetryQueue is null || state.IssuedRequests.Any(x => x.Composition is null) || state.RetryQueue.Any(x => x.Composition is null);
        var duplicateRequests = state.IssuedRequests is not null && state.IssuedRequests.Select(x => x.RequestId).Distinct().Count() != state.IssuedRequests.Count;
        var invalidResources = state.Resources is { } resources && (string.IsNullOrWhiteSpace(resources.ConfigurationSignature) || resources.Pending is null || resources.Consumed is null || resources.Policy.Values is null || resources.Pending.Any(x => string.IsNullOrWhiteSpace(x.Category) || string.IsNullOrWhiteSpace(x.CandidateId)) || resources.Consumed.Any(x => string.IsNullOrWhiteSpace(x.Category) || string.IsNullOrWhiteSpace(x.CandidateId)));
        var invalidOrchestration = state.Orchestration is { } orchestration && string.IsNullOrWhiteSpace(orchestration.ConfigurationId);
        if (string.IsNullOrWhiteSpace(state.ConfigurationVersion) || state.Pressure is null || state.Population.Pending is null || state.Schedule is null || state.Policy.Values is null || state.PlacementPolicy.Values is null || state.Composer.Values is null || incompleteRequests || duplicateRequests || invalidResources || invalidOrchestration) throw new InvalidOperationException("The Director state document is incomplete or invalid.");
        return state;
    }
}

public sealed class DirectorTelemetryBuffer
{
    readonly int _capacity;
    readonly Queue<DirectorDiagnostic> _items = new();
    public DirectorTelemetryBuffer(DirectorCore core, int capacity = 256)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity; core.Diagnostic += Record;
    }
    void Record(DirectorDiagnostic diagnostic) { _items.Enqueue(diagnostic); while (_items.Count > _capacity) _items.Dequeue(); }
    public IReadOnlyList<DirectorDiagnostic> Snapshot() => _items.ToArray();
    public void Clear() => _items.Clear();
}
