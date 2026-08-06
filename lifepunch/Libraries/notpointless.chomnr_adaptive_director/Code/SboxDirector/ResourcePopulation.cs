#nullable enable annotations
namespace SboxDirector;

public sealed class ResourceCandidate
{
    public string Id { get; init; } = "";
    public string Category { get; init; } = "";
    public bool Reachable { get; init; } = true;
    public bool Visible { get; init; }
    public float AreaCapacity { get; init; } = 1;
    public string ClusterId { get; init; } = "";
    public IReadOnlySet<string> Tags { get; init; } = new HashSet<string>();
}

public sealed class ResourceWorldSnapshot
{
    public IReadOnlyDictionary<string, int> ExistingCounts { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<ResourceCandidate> Candidates { get; init; } = Array.Empty<ResourceCandidate>();
    public void Validate()
    {
        if (ExistingCounts.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Value < 0)) throw new ArgumentException("Resource counts require non-empty categories and non-negative counts.");
        if (Candidates.Any(x => string.IsNullOrWhiteSpace(x.Id) || string.IsNullOrWhiteSpace(x.Category) || !float.IsFinite(x.AreaCapacity) || x.AreaCapacity < 0 || x.Tags is null || x.Tags.Any(string.IsNullOrWhiteSpace))) throw new ArgumentException("Resource candidates require ids, categories, non-negative finite capacity, and valid tags.");
        if (Candidates.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != Candidates.Count) throw new ArgumentException("Resource candidate ids must be unique.");
    }
}

public sealed class ResourceRule
{
    public string Category { get; init; } = "resource";
    public int TargetCount { get; init; } = 1;
    public int MaximumRequestsPerTick { get; init; } = 1;
    public bool AllowVisible { get; init; }
    public int MaximumPerCluster { get; init; } = int.MaxValue;
    public IReadOnlySet<string> RequiredTags { get; init; } = new HashSet<string>();
    public void Validate() { if (string.IsNullOrWhiteSpace(Category) || TargetCount < 0 || MaximumRequestsPerTick <= 0 || MaximumPerCluster <= 0 || RequiredTags is null || RequiredTags.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Invalid resource rule."); }
}

public interface IResourcePolicy
{
    bool AllowResourceSpawn(string category, ResourceCandidate candidate, WorldSnapshot world);
    string ConvertResource(string category, ResourceCandidate candidate, WorldSnapshot world);
    bool ShouldAvoidResource(string archetype, ResourceCandidate candidate, WorldSnapshot world);
    bool CanPickupObject(string archetype, string participantId, WorldSnapshot world);
}

public interface IPersistentResourcePolicy
{
    string PersistenceId { get; }
    IReadOnlyDictionary<string, string> CaptureResourcePolicyState();
    void RestoreResourcePolicyState(IReadOnlyDictionary<string, string> state);
}

public sealed class DefaultResourcePolicy : IResourcePolicy
{
    public bool AllowResourceSpawn(string category, ResourceCandidate candidate, WorldSnapshot world) => true;
    public string ConvertResource(string category, ResourceCandidate candidate, WorldSnapshot world) => category;
    public bool ShouldAvoidResource(string archetype, ResourceCandidate candidate, WorldSnapshot world) => false;
    public bool CanPickupObject(string archetype, string participantId, WorldSnapshot world) => true;
}

public sealed record ResourceSpawnRequest(long RequestId, string Category, string Archetype, string CandidateId, string Reason);
public sealed record ResourceSpawnResult(long RequestId, RequestResultKind Result, string? Detail = null);
public readonly record struct PendingResourceState(long RequestId, string Category, string CandidateId, string ClusterId);
public readonly record struct ResourcePopulationState(string ConfigurationSignature, long NextRequestId, RandomState Random, IReadOnlyList<PendingResourceState> Pending, IReadOnlyList<PendingResourceState> Consumed, ExtensionState Policy);

/// <summary>Generalized density/conversion/revisit controller backed by recovered item-population responsibilities.</summary>
public sealed class ResourcePopulationController
{
    readonly IReadOnlyList<ResourceRule> _rules;
    readonly IResourcePolicy _policy;
    DeterministicRandom _random;
    readonly Dictionary<long, PendingResourceState> _pending = new();
    readonly Dictionary<string, PendingResourceState> _consumed = new(StringComparer.Ordinal);
    readonly string _configurationSignature;
    long _nextRequestId = 1;

    public ResourcePopulationController(IEnumerable<ResourceRule> rules, int seed = 1, IResourcePolicy? policy = null)
    {
        _rules = rules.OrderBy(x => x.Category, StringComparer.Ordinal).ToArray(); foreach (var rule in _rules) rule.Validate();
        if (_rules.Select(x => x.Category).Distinct(StringComparer.Ordinal).Count() != _rules.Count) throw new ArgumentException("Resource rule categories must be unique.");
        _policy = policy ?? new DefaultResourcePolicy();
        _random = new DeterministicRandom(seed);
        _configurationSignature = string.Join(";", _rules.Select(x => $"{Encode(x.Category)}:{x.TargetCount}:{x.MaximumRequestsPerTick}:{x.AllowVisible}:{x.MaximumPerCluster}:{string.Join(",", x.RequiredTags.OrderBy(tag => tag, StringComparer.Ordinal).Select(Encode))}"));
    }

    public ResourcePopulationController(IEnumerable<ResourceRule> rules, ResourcePopulationState state, IResourcePolicy? policy = null) : this(rules, 1, policy)
    {
        RestoreState(state);
    }

    public IReadOnlyList<ResourceSpawnRequest> Tick(ResourceWorldSnapshot resources, WorldSnapshot world)
    {
        var output = new List<ResourceSpawnRequest>();
        foreach (var rule in _rules)
        {
            var existing = resources.ExistingCounts.TryGetValue(rule.Category, out var count) ? count : 0;
            var reserved = _pending.Values.Count(x => x.Category == rule.Category);
            var needed = Math.Min(rule.MaximumRequestsPerTick, Math.Max(0, rule.TargetCount - existing - reserved));
            for (var requestIndex = 0; requestIndex < needed; requestIndex++)
            {
                var candidates = resources.Candidates.Where(x => x.Category == rule.Category && x.Reachable && (rule.AllowVisible || !x.Visible) && !_consumed.ContainsKey(x.Id) && !_pending.Values.Any(p => p.CandidateId == x.Id) && ClusterAvailable(x, rule) && rule.RequiredTags.All(x.Tags.Contains) && _policy.AllowResourceSpawn(rule.Category, x, world)).ToArray();
                if (candidates.Length == 0) break;
                var bestCapacity = candidates.Max(x => x.AreaCapacity); var best = candidates.Where(x => Math.Abs(x.AreaCapacity - bestCapacity) < .00001f).ToArray(); var candidate = best[_random.NextInt(best.Length)];
                var archetype = _policy.ConvertResource(rule.Category, candidate, world); if (string.IsNullOrWhiteSpace(archetype) || _policy.ShouldAvoidResource(archetype, candidate, world)) { _consumed[candidate.Id] = new(0, rule.Category, candidate.Id, candidate.ClusterId); requestIndex--; continue; }
                var id = _nextRequestId++; _pending[id] = new(id, rule.Category, candidate.Id, candidate.ClusterId); output.Add(new(id, rule.Category, archetype, candidate.Id, $"target={rule.TargetCount}; existing={existing}; reserved={reserved}; cluster={candidate.ClusterId}")); reserved++;
            }
        }
        return output;
    }

    public void ApplyResult(ResourceSpawnResult result)
    {
        if (result.Result == RequestResultKind.Deferred) return;
        if (!_pending.Remove(result.RequestId, out var pending)) return;
        if (result.Result is RequestResultKind.Succeeded or RequestResultKind.PartiallySucceeded) _consumed[pending.CandidateId] = pending;
    }

    public void ResetRevisitState() => _consumed.Clear();
    public void CancelPending() => _pending.Clear();
    public bool CanPickup(string archetype, string participantId, WorldSnapshot world) => _policy.CanPickupObject(archetype, participantId, world);
    public ResourcePopulationState CaptureState() => new(_configurationSignature, _nextRequestId, _random.State, _pending.Values.OrderBy(x => x.RequestId).ToArray(), _consumed.Values.OrderBy(x => x.CandidateId, StringComparer.Ordinal).ToArray(), CapturePolicyState());
    public void RestoreState(ResourcePopulationState state)
    {
        if (!string.Equals(state.ConfigurationSignature, _configurationSignature, StringComparison.Ordinal)) throw new InvalidOperationException("Resource population state does not match the configured rules.");
        _nextRequestId = state.NextRequestId; _random = new DeterministicRandom(state.Random); _pending.Clear(); _consumed.Clear();
        foreach (var item in state.Pending) _pending[item.RequestId] = item; foreach (var item in state.Consumed) _consumed[item.CandidateId] = item;
        if (state.Policy.HasState)
        {
            if (_policy is not IPersistentResourcePolicy persistent || persistent.PersistenceId != state.Policy.PersistenceId) throw new InvalidOperationException($"Resource policy state '{state.Policy.PersistenceId}' requires the matching {nameof(IPersistentResourcePolicy)} implementation.");
            persistent.RestoreResourcePolicyState(state.Policy.Values);
        }
    }
    ExtensionState CapturePolicyState() => _policy is IPersistentResourcePolicy persistent ? new(string.IsNullOrWhiteSpace(persistent.PersistenceId) ? throw new InvalidOperationException("Persistent resource policy requires an id.") : persistent.PersistenceId, persistent.CaptureResourcePolicyState() ?? new Dictionary<string, string>()) : ExtensionState.Empty;
    static string Encode(string value) => $"{value.Length}:{value}";
    bool ClusterAvailable(ResourceCandidate candidate, ResourceRule rule)
    {
        if (string.IsNullOrWhiteSpace(candidate.ClusterId) || rule.MaximumPerCluster == int.MaxValue) return true;
        var pending = _pending.Values.Count(x => x.Category == rule.Category && x.ClusterId == candidate.ClusterId);
        var consumed = _consumed.Values.Count(x => x.Category == rule.Category && x.ClusterId == candidate.ClusterId);
        return pending + consumed < rule.MaximumPerCluster;
    }
}
