#nullable enable annotations
namespace SboxDirector;

public interface IEncounterComposer
{
    IReadOnlyList<EncounterMember> Compose(EncounterKind kind, int requestedCount, WorldSnapshot world, IDirectorPolicy policy, DeterministicRandom random);
}

public interface IPersistentEncounterComposer
{
    string PersistenceId { get; }
    IReadOnlyDictionary<string, string> CaptureComposerState();
    void RestoreComposerState(IReadOnlyDictionary<string, string> state);
}

public sealed class DefaultEncounterComposer : IEncounterComposer
{
    public IReadOnlyList<EncounterMember> Compose(EncounterKind kind, int requestedCount, WorldSnapshot world, IDirectorPolicy policy, DeterministicRandom random)
        => new[] { new EncounterMember(policy.SelectArchetype(kind, world, random), requestedCount) };
}

public sealed record WeightedArchetype(string Archetype, float Weight = 1f);

/// <summary>Configurable clean-room weighted composition policy. This is optional and is not the recovered retail special-class algorithm.</summary>
public sealed class WeightedEncounterComposer : IEncounterComposer
{
    readonly IReadOnlyDictionary<EncounterKind, IReadOnlyList<WeightedArchetype>> _tables;
    public WeightedEncounterComposer(IReadOnlyDictionary<EncounterKind, IReadOnlyList<WeightedArchetype>> tables)
    {
        if (tables is null) throw new ArgumentNullException(nameof(tables));
        var copy = new Dictionary<EncounterKind, IReadOnlyList<WeightedArchetype>>();
        foreach (var pair in tables)
        {
            var entries = pair.Value?.ToArray() ?? throw new ArgumentException("Composition tables cannot be null.", nameof(tables));
            if (entries.Any(x => string.IsNullOrWhiteSpace(x.Archetype) || !float.IsFinite(x.Weight) || x.Weight < 0) || (entries.Length > 0 && entries.All(x => x.Weight == 0))) throw new ArgumentException($"Composition table for {pair.Key} contains invalid archetypes or weights.", nameof(tables));
            copy[pair.Key] = entries;
        }
        _tables = copy;
    }
    public IReadOnlyList<EncounterMember> Compose(EncounterKind kind, int requestedCount, WorldSnapshot world, IDirectorPolicy policy, DeterministicRandom random)
    {
        if (!_tables.TryGetValue(kind, out var table) || table.Count == 0) return new DefaultEncounterComposer().Compose(kind, requestedCount, world, policy, random);
        var eligible = table.Where(x => x.Weight > 0 && !string.IsNullOrWhiteSpace(x.Archetype)).ToArray();
        if (eligible.Length == 0) throw new InvalidOperationException($"Composition table for {kind} has no positive weighted archetypes.");
        var counts = new Dictionary<string, int>(StringComparer.Ordinal); var total = eligible.Sum(x => x.Weight);
        for (var index = 0; index < requestedCount; index++)
        {
            var roll = random.NextFloat() * total; var selected = eligible[^1].Archetype;
            foreach (var option in eligible) { roll -= option.Weight; if (roll <= 0) { selected = option.Archetype; break; } }
            counts[selected] = counts.TryGetValue(selected, out var count) ? count + 1 : 1;
        }
        return counts.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => new EncounterMember(x.Key, x.Value)).ToArray();
    }
}

/// <summary>Selects every member uniformly from the supplied eligible archetypes.</summary>
public sealed class UniformEncounterComposer : IEncounterComposer
{
    readonly IReadOnlyDictionary<EncounterKind, IReadOnlyList<string>> _tables;
    public UniformEncounterComposer(IReadOnlyDictionary<EncounterKind, IReadOnlyList<string>> tables)
    {
        if (tables is null) throw new ArgumentNullException(nameof(tables));
        var copy = new Dictionary<EncounterKind, IReadOnlyList<string>>();
        foreach (var pair in tables)
        {
            var entries = pair.Value?.ToArray() ?? throw new ArgumentException("Composition tables cannot be null.", nameof(tables));
            if (entries.Length == 0 || entries.Any(string.IsNullOrWhiteSpace) || entries.Distinct(StringComparer.Ordinal).Count() != entries.Length) throw new ArgumentException($"Uniform composition table for {pair.Key} must contain unique non-empty archetypes.", nameof(tables));
            copy[pair.Key] = entries;
        }
        _tables = copy;
    }
    public IReadOnlyList<EncounterMember> Compose(EncounterKind kind, int requestedCount, WorldSnapshot world, IDirectorPolicy policy, DeterministicRandom random)
    {
        if (!_tables.TryGetValue(kind, out var table)) return new DefaultEncounterComposer().Compose(kind, requestedCount, world, policy, random);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < requestedCount; index++)
        {
            var archetype = table[random.NextInt(table.Count)];
            counts[archetype] = counts.TryGetValue(archetype, out var count) ? count + 1 : 1;
        }
        return counts.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => new EncounterMember(x.Key, x.Value)).ToArray();
    }
}

public readonly record struct IssuedRequestState(long RequestId, EncounterKind Kind, int RequestedCount, int Attempt, IReadOnlyList<EncounterMember> Composition, string? OrchestrationSource = null);
public readonly record struct RetryRequestState(EncounterKind Kind, int RequestedCount, int Attempt, double NotBefore, IReadOnlyList<EncounterMember> Composition, string Reason, string? OrchestrationSource = null);
