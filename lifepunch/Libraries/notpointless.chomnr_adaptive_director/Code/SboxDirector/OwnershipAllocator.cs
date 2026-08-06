#nullable enable annotations
namespace SboxDirector;

public sealed record OwnershipCandidate(string Id, float Weight = 1f, bool Eligible = true);

public sealed class EncounterOwnershipAllocator
{
    public string? Select(IReadOnlyList<OwnershipCandidate> candidates, DeterministicRandom random)
    {
        var eligible = candidates.Where(x => x.Eligible && x.Weight > 0 && !string.IsNullOrWhiteSpace(x.Id)).ToArray();
        if (eligible.Length == 0) return null;
        var total = eligible.Sum(x => x.Weight); var roll = random.NextFloat() * total;
        foreach (var candidate in eligible) { roll -= candidate.Weight; if (roll <= 0) return candidate.Id; }
        return eligible[^1].Id;
    }
}

public sealed class ResourceBudget
{
    readonly Dictionary<string, float> _remaining;
    public ResourceBudget(IReadOnlyDictionary<string, float> budgets) { _remaining = budgets.ToDictionary(x => x.Key, x => Math.Max(0, x.Value)); }
    public float Remaining(string category) => _remaining.TryGetValue(category, out var value) ? value : 0;
    public bool TrySpend(string category, float cost) { if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost)); if (Remaining(category) < cost) return false; _remaining[category] -= cost; return true; }
    public IReadOnlyDictionary<string, float> Capture() => new Dictionary<string, float>(_remaining);
}
