#nullable enable annotations
namespace SboxDirector;

public sealed class EncounterRule
{
    public string Id { get; init; } = "rule";
    public EncounterKind Kind { get; init; }
    public int Count { get; init; } = 1;
    public int Priority { get; init; }
    public double CooldownSeconds { get; init; } = 10;
    public float MinimumPressure { get; init; }
    public float MaximumPressure { get; init; } = 1f;
    public float Probability { get; init; } = 1f;
    public IReadOnlySet<TempoPhase> Phases { get; init; } = new HashSet<TempoPhase> { TempoPhase.BuildUp, TempoPhase.SustainPeak };
    public IReadOnlySet<string> Modes { get; init; } = new HashSet<string>();
    public Func<WorldSnapshot, bool>? AdditionalCondition { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || Count <= 0 || !double.IsFinite(CooldownSeconds) || CooldownSeconds < 0 || !float.IsFinite(MinimumPressure) || !float.IsFinite(MaximumPressure) || !float.IsFinite(Probability) || MinimumPressure < 0 || MaximumPressure > 1 || MaximumPressure < MinimumPressure || Probability is < 0 or > 1) throw new ArgumentException($"Invalid encounter rule '{Id}'.");
    }
}

public interface IEncounterTimingPolicy
{
    double AdjustCooldown(EncounterRule rule, double proposedSeconds, WorldSnapshot world, TempoPhase tempo, float teamPressure);
}

public sealed class DefaultEncounterTimingPolicy : IEncounterTimingPolicy
{
    public double AdjustCooldown(EncounterRule rule, double proposedSeconds, WorldSnapshot world, TempoPhase tempo, float teamPressure) => proposedSeconds;
}

public sealed class ModeEncounterTimingPolicy : IEncounterTimingPolicy
{
    readonly IReadOnlyDictionary<string, double> _modeMultipliers;
    readonly double _highPressureMultiplier;
    public ModeEncounterTimingPolicy(IReadOnlyDictionary<string, double>? modeMultipliers = null, double highPressureMultiplier = 1)
    {
        _modeMultipliers = new Dictionary<string, double>(modeMultipliers ?? new Dictionary<string, double>(), StringComparer.Ordinal); if (_modeMultipliers.Any(x => string.IsNullOrWhiteSpace(x.Key) || !double.IsFinite(x.Value) || x.Value < 0) || !double.IsFinite(highPressureMultiplier) || highPressureMultiplier < 0) throw new ArgumentException("Cooldown multipliers must be finite and non-negative."); _highPressureMultiplier = highPressureMultiplier;
    }
    public double AdjustCooldown(EncounterRule rule, double proposedSeconds, WorldSnapshot world, TempoPhase tempo, float teamPressure)
    {
        var mode = _modeMultipliers.TryGetValue(world.ModeId, out var multiplier) ? multiplier : 1; var pressure = 1 + (_highPressureMultiplier - 1) * Math.Clamp(teamPressure, 0, 1); return proposedSeconds * mode * pressure;
    }
}

public sealed class RuleBasedEncounterSchedule : IEncounterSchedule, IPersistentEncounterSchedule, IEncounterScheduleFeedback
{
    readonly IReadOnlyList<EncounterRule> _rules;
    readonly IEncounterTimingPolicy _timing;
    readonly Dictionary<string, double> _nextTimes = new();
    EncounterRule? _selected;
    double _selectedPreviousTime;

    public RuleBasedEncounterSchedule(IEnumerable<EncounterRule> rules, IEncounterTimingPolicy? timing = null)
    {
        _rules = rules.OrderByDescending(x => x.Priority).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
        if (_rules.Count == 0) throw new ArgumentException("At least one encounter rule is required.", nameof(rules));
        foreach (var rule in _rules) rule.Validate();
        if (_rules.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != _rules.Count) throw new ArgumentException("Encounter rule ids must be unique.");
        _timing = timing ?? new DefaultEncounterTimingPolicy();
    }

    public EncounterKind? SelectEncounter(WorldSnapshot world, TempoPhase tempo, float pressure, PopulationLedger population, DeterministicRandom random)
    {
        _selected = null;
        foreach (var rule in _rules)
        {
            if (_nextTimes.TryGetValue(rule.Id, out var next) && world.Time < next) continue;
            if (!rule.Phases.Contains(tempo) || pressure < rule.MinimumPressure || pressure > rule.MaximumPressure) continue;
            if (rule.Modes.Count > 0 && !rule.Modes.Contains(world.ModeId)) continue;
            if (rule.AdditionalCondition is not null && !rule.AdditionalCondition(world)) continue;
            if (population.Available(rule.Kind, world.Population) <= 0) continue;
            var cooldown = _timing.AdjustCooldown(rule, rule.CooldownSeconds, world, tempo, pressure); if (!double.IsFinite(cooldown) || cooldown < 0) throw new InvalidOperationException("Encounter timing policy returned an invalid cooldown.");
            if (random.NextFloat() > rule.Probability) { _nextTimes[rule.Id] = world.Time + cooldown; continue; }
            _selected = rule; _selectedPreviousTime = _nextTimes.TryGetValue(rule.Id, out var previous) ? previous : double.NegativeInfinity; _nextTimes[rule.Id] = world.Time + cooldown; return rule.Kind;
        }
        return null;
    }

    public int RequestedCount(EncounterKind kind, WorldSnapshot world) => _selected?.Kind == kind ? _selected.Count : 1;
    public IReadOnlyDictionary<string, string> CaptureScheduleState() => _nextTimes.ToDictionary(x => x.Key, x => x.Value.ToString("R", CultureInfo.InvariantCulture));
    public void RestoreScheduleState(IReadOnlyDictionary<string, string> state)
    {
        _nextTimes.Clear(); foreach (var pair in state) if (double.TryParse(pair.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) _nextTimes[pair.Key] = value;
    }
    public void SelectionFinalized(bool requestCreated) { if (_selected is not null && !requestCreated) { if (double.IsNegativeInfinity(_selectedPreviousTime)) _nextTimes.Remove(_selected.Id); else _nextTimes[_selected.Id] = _selectedPreviousTime; } _selected = null; }
}

public sealed class MilestoneEncounterSchedule : IEncounterSchedule, IPersistentEncounterSchedule, IEncounterScheduleFeedback
{
    readonly string _id; readonly EncounterKind _kind; readonly float _minimumProgress; readonly float _maximumProgress; readonly int _count;
    readonly HashSet<string> _completedScopes = new(); string? _selectedScope;
    public MilestoneEncounterSchedule(string id, EncounterKind kind, float minimumProgress, float maximumProgress = 1f, int count = 1)
    {
        if (string.IsNullOrWhiteSpace(id) || minimumProgress < 0 || maximumProgress < minimumProgress || count <= 0) throw new ArgumentException("Invalid milestone schedule.");
        _id = id; _kind = kind; _minimumProgress = minimumProgress; _maximumProgress = maximumProgress; _count = count;
    }
    public EncounterKind? SelectEncounter(WorldSnapshot world, TempoPhase tempo, float teamPressure, PopulationLedger population, DeterministicRandom random)
    {
        var scope = string.IsNullOrWhiteSpace(world.RoundId) ? world.ModeId : world.ModeId + ":" + world.RoundId;
        if (_completedScopes.Contains(scope) || world.TeamProgress < _minimumProgress || world.TeamProgress > _maximumProgress || population.Available(_kind, world.Population) <= 0) return null;
        _selectedScope = scope; return _kind;
    }
    public int RequestedCount(EncounterKind kind, WorldSnapshot world) => kind == _kind && _selectedScope is not null ? _count : 1;
    public IReadOnlyDictionary<string, string> CaptureScheduleState() => new Dictionary<string, string> { ["id"] = _id, ["completed"] = string.Join("|", _completedScopes.OrderBy(x => x, StringComparer.Ordinal)) };
    public void RestoreScheduleState(IReadOnlyDictionary<string, string> state) { _completedScopes.Clear(); if (state.TryGetValue("completed", out var value)) foreach (var scope in value.Split('|', StringSplitOptions.RemoveEmptyEntries)) _completedScopes.Add(scope); }
    public void SelectionFinalized(bool requestCreated) { if (requestCreated && _selectedScope is not null) _completedScopes.Add(_selectedScope); _selectedScope = null; }
}

public sealed class CompositeEncounterSchedule : IEncounterSchedule, IPersistentEncounterSchedule, IEncounterScheduleFeedback
{
    readonly IReadOnlyList<IEncounterSchedule> _schedules; IEncounterSchedule? _selected;
    public CompositeEncounterSchedule(params IEncounterSchedule[] schedules) { _schedules = schedules; if (schedules.Length == 0) throw new ArgumentException("At least one schedule is required."); }
    public EncounterKind? SelectEncounter(WorldSnapshot world, TempoPhase tempo, float teamPressure, PopulationLedger population, DeterministicRandom random)
    {
        _selected = null; foreach (var schedule in _schedules) { var kind = schedule.SelectEncounter(world, tempo, teamPressure, population, random); if (kind is not null) { _selected = schedule; return kind; } }
        return null;
    }
    public int RequestedCount(EncounterKind kind, WorldSnapshot world) => _selected?.RequestedCount(kind, world) ?? 1;
    public IReadOnlyDictionary<string, string> CaptureScheduleState()
    {
        var output = new Dictionary<string, string>(); for (var i = 0; i < _schedules.Count; i++) if (_schedules[i] is IPersistentEncounterSchedule persistent) foreach (var pair in persistent.CaptureScheduleState()) output[$"{i}:{pair.Key}"] = pair.Value; return output;
    }
    public void RestoreScheduleState(IReadOnlyDictionary<string, string> state)
    {
        for (var i = 0; i < _schedules.Count; i++) if (_schedules[i] is IPersistentEncounterSchedule persistent) persistent.RestoreScheduleState(state.Where(x => x.Key.StartsWith(i + ":", StringComparison.Ordinal)).ToDictionary(x => x.Key[(x.Key.IndexOf(':') + 1)..], x => x.Value));
    }
    public void SelectionFinalized(bool requestCreated) { if (_selected is IEncounterScheduleFeedback feedback) feedback.SelectionFinalized(requestCreated); _selected = null; }
}
