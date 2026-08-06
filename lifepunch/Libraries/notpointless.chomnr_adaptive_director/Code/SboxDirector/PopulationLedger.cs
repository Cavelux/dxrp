namespace SboxDirector;

public sealed class PopulationLedger
{
    readonly PopulationConfiguration _configuration;
    readonly Dictionary<EncounterKind, int> _reserved = new();
    readonly Dictionary<long, PendingReservation> _pending = new();
    public PopulationLedger(PopulationConfiguration configuration) { _configuration = configuration; }

    public int Available(EncounterKind kind, PopulationSnapshot population)
        => Available(kind, population, ConfiguredLimit(kind));
    public int ConfiguredLimit(EncounterKind kind) => _configuration.Limits.TryGetValue(kind, out var configured) ? configured : 0;
    public int Available(EncounterKind kind, PopulationSnapshot population, int limit)
    {
        return Math.Max(0, limit - population[kind] - (_reserved.TryGetValue(kind, out var value) ? value : 0));
    }
    public bool TryReserve(long requestId, EncounterKind kind, int count, PopulationSnapshot population, double expiresAt = double.PositiveInfinity)
        => TryReserveWithLimit(requestId, kind, count, population, ConfiguredLimit(kind), expiresAt);
    public bool TryReserveWithLimit(long requestId, EncounterKind kind, int count, PopulationSnapshot population, int limit, double expiresAt = double.PositiveInfinity)
    {
        if (count <= 0 || limit < 0 || _pending.ContainsKey(requestId) || Available(kind, population, limit) < count) return false;
        _pending[requestId] = new(requestId, kind, count, expiresAt); _reserved[kind] = (_reserved.TryGetValue(kind, out var value) ? value : 0) + count; return true;
    }
    public void Resolve(SpawnRequestResult result)
    {
        if (result.Result == RequestResultKind.Deferred) return;
        if (!_pending.Remove(result.RequestId, out var pending)) return;
        _reserved[pending.Kind] = Math.Max(0, _reserved[pending.Kind] - pending.Count);
    }
    public IReadOnlyList<long> CancelAll()
    {
        var ids = _pending.Keys.OrderBy(x => x).ToArray(); _pending.Clear(); _reserved.Clear(); return ids;
    }
    public IReadOnlyList<long> Expire(double time)
    {
        var expired = _pending.Values.Where(x => x.ExpiresAt <= time).Select(x => x.RequestId).ToArray();
        foreach (var id in expired) Resolve(new SpawnRequestResult(id, RequestResultKind.Failed, 0, "request timeout"));
        return expired;
    }
    public PopulationLedgerState Capture() => new(_pending.Values.OrderBy(x => x.RequestId).ToArray());
    public void Restore(PopulationLedgerState state)
    {
        _pending.Clear(); _reserved.Clear();
        foreach (var item in state.Pending) { _pending[item.RequestId] = item; _reserved[item.Kind] = (_reserved.TryGetValue(item.Kind, out var count) ? count : 0) + item.Count; }
    }
    public IReadOnlyDictionary<EncounterKind, int> Reservations => _reserved;
    public IReadOnlyList<PendingReservation> Pending => _pending.Values.OrderBy(x => x.RequestId).ToArray();
}

public readonly record struct PendingReservation(long RequestId, EncounterKind Kind, int Count, double ExpiresAt);
public readonly record struct PopulationLedgerState(IReadOnlyList<PendingReservation> Pending);
