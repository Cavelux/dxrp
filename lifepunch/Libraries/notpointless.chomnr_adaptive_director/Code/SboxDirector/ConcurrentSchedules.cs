#nullable enable annotations
namespace SboxDirector;

public sealed class ScheduleLane
{
    public string Id { get; init; } = "lane";
    public int Priority { get; init; }
    public IEncounterSchedule Schedule { get; init; } = null!;
    public Func<WorldSnapshot, bool>? Enabled { get; init; }
}

/// <summary>
/// Runs independent encounter lanes during one authoritative tick. This mirrors the proven separation
/// of survival lull, horde, special, and boss schedules without copying their unrecovered formulas.
/// </summary>
public sealed class ConcurrentEncounterSchedule : IEncounterSchedule, IPersistentEncounterSchedule, IEncounterScheduleFeedback
{
    readonly IReadOnlyList<ScheduleLane> _lanes;
    readonly HashSet<string> _served = new(StringComparer.Ordinal);
    readonly string _signature;
    ScheduleLane? _selected;
    double _servedTime = double.NaN;

    public ConcurrentEncounterSchedule(IEnumerable<ScheduleLane> lanes)
    {
        _lanes = lanes.OrderByDescending(x => x.Priority).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
        if (_lanes.Count == 0 || _lanes.Any(x => x.Schedule is null || string.IsNullOrWhiteSpace(x.Id) || x.Id.Contains(':'))) throw new ArgumentException("Concurrent schedule lanes require unique non-empty ids without colons and a schedule.");
        if (_lanes.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != _lanes.Count) throw new ArgumentException("Concurrent schedule lane ids must be unique.");
        _signature = string.Join("|", _lanes.Select(x => $"{x.Id.Length}:{x.Id}:{x.Priority}"));
    }

    public EncounterKind? SelectEncounter(WorldSnapshot world, TempoPhase tempo, float teamPressure, PopulationLedger population, DeterministicRandom random)
    {
        _selected = null;
        if (_servedTime != world.Time) { _servedTime = world.Time; _served.Clear(); }
        foreach (var lane in _lanes)
        {
            if (_served.Contains(lane.Id) || (lane.Enabled is not null && !lane.Enabled(world))) continue;
            var kind = lane.Schedule.SelectEncounter(world, tempo, teamPressure, population, random);
            if (kind is null) continue;
            _selected = lane; _served.Add(lane.Id); return kind;
        }
        return null;
    }

    public int RequestedCount(EncounterKind kind, WorldSnapshot world) => _selected?.Schedule.RequestedCount(kind, world) ?? 1;

    public void SelectionFinalized(bool requestCreated)
    {
        if (_selected is null) return;
        if (_selected.Schedule is IEncounterScheduleFeedback feedback) feedback.SelectionFinalized(requestCreated);
        if (!requestCreated) _served.Remove(_selected.Id);
        _selected = null;
    }

    public IReadOnlyDictionary<string, string> CaptureScheduleState()
    {
        var state = new Dictionary<string, string> { ["coordinator.signature"] = _signature, ["coordinator.time"] = _servedTime.ToString("R", CultureInfo.InvariantCulture), ["coordinator.served"] = string.Join("|", _served.OrderBy(x => x, StringComparer.Ordinal)) };
        foreach (var lane in _lanes)
            if (lane.Schedule is IPersistentEncounterSchedule persistent)
                foreach (var pair in persistent.CaptureScheduleState()) state[$"lane:{lane.Id}:{pair.Key}"] = pair.Value;
        return state;
    }

    public void RestoreScheduleState(IReadOnlyDictionary<string, string> state)
    {
        _served.Clear();
        if (!state.TryGetValue("coordinator.signature", out var signature) || !string.Equals(signature, _signature, StringComparison.Ordinal)) throw new InvalidOperationException("Concurrent schedule state does not match its configured lanes.");
        if (state.TryGetValue("coordinator.time", out var time) && double.TryParse(time, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) _servedTime = parsed;
        if (state.TryGetValue("coordinator.served", out var served)) foreach (var id in served.Split('|', StringSplitOptions.RemoveEmptyEntries)) _served.Add(id);
        foreach (var lane in _lanes)
        {
            if (lane.Schedule is not IPersistentEncounterSchedule persistent) continue;
            var prefix = $"lane:{lane.Id}:";
            persistent.RestoreScheduleState(state.Where(x => x.Key.StartsWith(prefix, StringComparison.Ordinal)).ToDictionary(x => x.Key[prefix.Length..], x => x.Value));
        }
    }
}
