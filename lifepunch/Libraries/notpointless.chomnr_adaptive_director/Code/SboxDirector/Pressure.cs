namespace SboxDirector;

public readonly record struct PressureState(float Instantaneous, float Averaged, double LastUpdate, double HoldUntil);

public sealed class PressureTracker
{
    readonly PressureConfiguration _configuration;
    public float Instantaneous { get; private set; }
    public float Averaged { get; private set; }
    public double LastUpdate { get; private set; }
    public double HoldUntil { get; private set; }

    public PressureTracker(PressureConfiguration configuration, double startTime = 0)
    {
        _configuration = configuration; configuration.Validate(); LastUpdate = startTime;
    }

    public PressureTracker(PressureConfiguration configuration, PressureState state) : this(configuration, state.LastUpdate)
    {
        Instantaneous = Clamp01(state.Instantaneous); Averaged = state.Averaged; HoldUntil = state.HoldUntil;
    }

    public PressureState State => new(Instantaneous, Averaged, LastUpdate, HoldUntil);

    public void Add(PressureSeverity severity, double time)
    {
        Update(time);
        var index = (int)severity - 1;
        if (index is < 0 or >= 5) throw new ArgumentOutOfRangeException(nameof(severity));
        Instantaneous = Clamp01(Instantaneous + _configuration.Gain * _configuration.SeverityWeights[index]);
        HoldUntil = Math.Max(HoldUntil, time + _configuration.HoldSeconds);
        ApplyLock();
    }

    public void Update(double time)
    {
        if (time < LastUpdate) throw new InvalidOperationException("Pressure time cannot move backwards.");
        var dt = (float)(time - LastUpdate);
        var decayStart = Math.Max(LastUpdate, HoldUntil);
        var decaySeconds = (float)Math.Max(0, time - decayStart);
        if (decaySeconds > 0) Instantaneous = Math.Max(0f, Instantaneous - decaySeconds / _configuration.FullDecaySeconds);
        var delta = Instantaneous - Averaged;
        if (delta < 0.000001f) Averaged = Instantaneous;
        else Averaged += MathF.Pow(delta, dt / _configuration.AveragedFollowingSeconds);
        LastUpdate = time; ApplyLock();
    }

    void ApplyLock()
    {
        if (_configuration.LockValue < 0f) return;
        Instantaneous = _configuration.LockValue; Averaged = _configuration.LockValue;
    }
    static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
}

public sealed class TeamPressure
{
    readonly Dictionary<string, PressureTracker> _trackers = new();
    readonly PressureConfiguration _configuration;
    public TeamPressure(PressureConfiguration configuration) { _configuration = configuration; }
    public PressureTracker GetOrCreate(string id, double time)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Participant id is required.");
        if (!_trackers.TryGetValue(id, out var tracker)) _trackers[id] = tracker = new PressureTracker(_configuration, time);
        return tracker;
    }
    public void Apply(PressureEvent input, double time) => GetOrCreate(input.ParticipantId, time).Add(input.Severity, time);
    public float UpdateAndGetMaximum(WorldSnapshot world)
    {
        var max = 0f;
        foreach (var participant in world.Participants)
        {
            if (!participant.IsAlive || !participant.IsEligible) continue;
            var tracker = GetOrCreate(participant.Id, world.Time); tracker.Update(world.Time); max = Math.Max(max, tracker.Instantaneous);
        }
        return max;
    }
    public IReadOnlyDictionary<string, PressureState> Capture() => _trackers.ToDictionary(pair => pair.Key, pair => pair.Value.State);
    public void Restore(IReadOnlyDictionary<string, PressureState> states)
    {
        _trackers.Clear(); foreach (var pair in states) _trackers[pair.Key] = new PressureTracker(_configuration, pair.Value);
    }
    public bool Remove(string participantId) => _trackers.Remove(participantId);
}
