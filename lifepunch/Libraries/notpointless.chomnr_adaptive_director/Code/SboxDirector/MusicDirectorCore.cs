#nullable enable annotations
namespace SboxDirector;

public sealed record MusicEnvelopeState(float Damage, float FastActivity, float SlowActivity, float DamageInflicted, float CommonSight, float MobPressure, float Action, float Threat, float Calm, float AmbientBasis);

public sealed record MusicParticipantState(
    string ParticipantId,
    float PreviousDamage,
    MusicEnvelopeState Envelopes,
    bool CombatActive,
    bool MobbedActive,
    bool BossPresent,
    bool HazardBurning,
    bool HazardAttacking,
    bool HazardRaging,
    bool WanderingHazardActive,
    bool DangerAtmosphereActive,
    bool ScenarioEnded,
    double AtmosphereReadyAt,
    double PositionalStingerReadyAt,
    double WanderingHazardReadyAt,
    double LargeAreaRevealReadyAt,
    IReadOnlyDictionary<string, double> SpecialAlertReadyAt,
    IReadOnlyDictionary<string, float> LastMixerLayers);

public sealed record MusicDirectorState(
    string ConfigurationVersion,
    bool HasTicked,
    double LastTickTime,
    long LastGameplayEventSequence,
    long NextDecisionId,
    RandomState Random,
    IReadOnlyList<MusicParticipantState> Participants);

public sealed class MusicDirectorCore
{
    readonly MusicDirectorConfiguration _configuration;
    DeterministicRandom _random;
    readonly Dictionary<string, ParticipantMemory> _participants = new(StringComparer.Ordinal);
    long _lastEventSequence;
    long _nextDecisionId = 1;
    double _lastTickTime;
    bool _hasTicked;

    public MusicDirectorCore(MusicDirectorConfiguration configuration)
    {
        _configuration = configuration; configuration.Validate(); _random = new(configuration.Seed);
    }

    public MusicDirectorCore(MusicDirectorConfiguration configuration, MusicDirectorState state)
    {
        _configuration = configuration; configuration.Validate();
        if (!string.Equals(configuration.Version, state.ConfigurationVersion, StringComparison.Ordinal)) throw new InvalidOperationException($"Music state version '{state.ConfigurationVersion}' does not match configuration version '{configuration.Version}'.");
        if (state.NextDecisionId <= 0 || state.Participants is null || state.Participants.Select(x => x.ParticipantId).Distinct(StringComparer.Ordinal).Count() != state.Participants.Count) throw new InvalidOperationException("Music Director state is incomplete or invalid.");
        _random = new(state.Random); _hasTicked = state.HasTicked; _lastTickTime = state.LastTickTime; _lastEventSequence = state.LastGameplayEventSequence; _nextDecisionId = state.NextDecisionId;
        foreach (var participant in state.Participants)
        {
            if (string.IsNullOrWhiteSpace(participant.ParticipantId) || participant.SpecialAlertReadyAt is null || participant.LastMixerLayers is null) throw new InvalidOperationException("Music participant state is incomplete.");
            _participants.Add(participant.ParticipantId, new ParticipantMemory(participant));
        }
    }

    public MusicDecisionBatch Tick(MusicWorldSnapshot world, IReadOnlyList<MusicGameplayEvent>? gameplayEvents = null)
    {
        if (_configuration.ValidateSnapshots) world.Validate();
        if (_hasTicked && world.Time < _lastTickTime) throw new InvalidOperationException("Music world time cannot move backwards.");
        var delta = _hasTicked ? world.Time - _lastTickTime : 0;
        _hasTicked = true; _lastTickTime = world.Time;
        var output = new MusicDecisionBatch { Time = world.Time };

        foreach (var input in world.Participants.OrderBy(x => x.Id, StringComparer.Ordinal)) if (!_participants.ContainsKey(input.Id)) _participants.Add(input.Id, new ParticipantMemory(input.Id, input.CumulativeDamage, _configuration.SpecialAlerts));
        ApplyGameplayEvents(world, gameplayEvents ?? Array.Empty<MusicGameplayEvent>(), output);
        foreach (var input in world.Participants.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            var state = _participants[input.Id];
            if (world.ScenarioEnded && !state.ScenarioEnded) output.Decisions.Add(NewDecision(MusicDecisionKind.General, input.Id, "scenario_ended", "world scenario ended"));
            state.ScenarioEnded = world.ScenarioEnded;
            UpdateParticipant(input, state, delta, world.Time, output);
        }
        return output;
    }

    void ApplyGameplayEvents(MusicWorldSnapshot world, IReadOnlyList<MusicGameplayEvent> events, MusicDecisionBatch output)
    {
        if (events.Select(x => x.Sequence).Distinct().Count() != events.Count) throw new ArgumentException("Music gameplay event sequences must be unique within a tick.");
        foreach (var input in events.OrderBy(x => x.Sequence))
        {
            if (input.Sequence <= _lastEventSequence) continue;
            if (!float.IsFinite(input.FadeSeconds) || input.FadeSeconds < 0) throw new ArgumentException("Music event fades must be finite and non-negative.");
            _lastEventSequence = input.Sequence;
            var targets = string.IsNullOrWhiteSpace(input.ParticipantId)
                ? world.Participants.Select(x => x.Id).OrderBy(x => x, StringComparer.Ordinal).ToArray()
                : new[] { input.ParticipantId };
            foreach (var participantId in targets)
            {
                var kind = input.Kind switch
                {
                    MusicTriggerKind.Play => MusicDecisionKind.Play,
                    MusicTriggerKind.StopEvent => MusicDecisionKind.StopEvent,
                    MusicTriggerKind.StopTrack => MusicDecisionKind.StopTrack,
                    MusicTriggerKind.StopAll => MusicDecisionKind.StopAll,
                    MusicTriggerKind.AmbientMob or MusicTriggerKind.MobSpawn or MusicTriggerKind.MobSpawnBehind or MusicTriggerKind.LargeAreaRevealed or MusicTriggerKind.LandmarkRevealed => MusicDecisionKind.Play,
                    _ => MusicDecisionKind.General
                };
                var target = input.Kind switch
                {
                    MusicTriggerKind.Reset => "reset",
                    MusicTriggerKind.ScenarioEnded => "scenario_ended",
                    MusicTriggerKind.ParticipantRestored => "participant_restored",
                    MusicTriggerKind.AmbientMob => _configuration.Cues.AmbientMobEvent,
                    MusicTriggerKind.MobSpawn => _configuration.Cues.MobSpawnEvent,
                    MusicTriggerKind.MobSpawnBehind => _configuration.Cues.MobSpawnBehindEvent,
                    MusicTriggerKind.LargeAreaRevealed => _configuration.Cues.LargeAreaRevealEvent,
                    _ => input.TargetId
                };
                if (input.Kind == MusicTriggerKind.LargeAreaRevealed && _participants.TryGetValue(participantId, out var revealState))
                {
                    if (revealState.LargeAreaRevealReadyAt > world.Time) continue;
                    revealState.LargeAreaRevealReadyAt = world.Time + _configuration.Dynamic.LargeAreaRevealCooldownSeconds;
                }
                if (kind == MusicDecisionKind.Play && string.IsNullOrWhiteSpace(target)) continue;
                output.Decisions.Add(NewDecision(kind, participantId, target, $"gameplay:{input.Kind}") with { FadeSeconds = input.FadeSeconds, EmitterId = input.EmitterId, Position = input.Position, Arguments = input.Arguments });
                if ((input.Kind is MusicTriggerKind.Reset or MusicTriggerKind.ParticipantRestored) && _participants.TryGetValue(participantId, out var state))
                {
                    state.ResetDynamic();
                    var current = world.Participants.FirstOrDefault(x => string.Equals(x.Id, participantId, StringComparison.Ordinal)); if (current is not null) state.PreviousDamage = current.CumulativeDamage;
                }
            }
        }
    }

    void UpdateParticipant(MusicParticipantSnapshot input, ParticipantMemory state, double delta, double time, MusicDecisionBatch output)
    {
        var d = _configuration.Dynamic;
        var dt = (float)Math.Max(0, delta);
        var damageDelta = Math.Max(0, input.CumulativeDamage - state.PreviousDamage); state.PreviousDamage = input.CumulativeDamage;
        state.Damage = Advance(state.Damage, damageDelta > 0 ? 1 : 0, dt, d.DamageRisePerSecond, d.DamageDecaySeconds);
        state.FastActivity = Advance(state.FastActivity, input.WeaponActivity, dt, d.FastActivityRisePerSecond, d.FastActivityDecaySeconds);
        state.SlowActivity = Advance(state.SlowActivity, input.WeaponActivity, dt, d.SlowActivityRisePerSecond, d.SlowActivityDecaySeconds);
        state.DamageInflicted = Advance(state.DamageInflicted, input.InflictedDamage ? 1 : 0, dt, d.InflictedDamageRisePerSecond, d.InflictedDamageDecaySeconds);
        state.CommonSight = Advance(state.CommonSight, Clamp01(input.VisibleCommonThreats / d.CommonSightNormalizer), dt, float.PositiveInfinity, d.CommonSightDecaySeconds);

        var damageDriver = Math.Min(5 * input.VeryCloseCommonFactor, state.Damage);
        var damageDuck = RemapClamped(damageDriver, d.DamageDuckInputMinimum, d.DamageDuckInputMaximum, 0, d.DamageDuckOutputMaximum);
        var mobDriver = Math.Min(4 * input.VeryCloseCommonFactor, state.Damage);
        var mobTarget = mobDriver < d.MobDamageMinimum ? 0 : RemapClamped(mobDriver, d.MobDamageMinimum, d.MobDamageMaximum, d.MobOutputMinimum, d.MobOutputMaximum);
        if (input.IsIncapacitated) mobTarget = 0;
        state.MobPressure = Advance(state.MobPressure, mobTarget, dt, float.PositiveInfinity, d.MobPressureDecaySeconds);

        var rawAction = Math.Max(Math.Max(input.GlobalCombatActive || input.BossPresent ? 1 : 0, 5 * input.AttackingCommonFactor), state.FastActivity);
        rawAction = Clamp01(rawAction);
        var rawThreat = Clamp01(Math.Max(input.MobPressureActive || input.BossPresent || input.HazardPresent ? 1 : 0, 2 * input.AttackingCommonFactor));
        state.Action = Advance(state.Action, rawAction, dt, float.PositiveInfinity, d.ActionDecaySeconds);
        state.Threat = Advance(state.Threat, rawThreat, dt, float.PositiveInfinity, d.ThreatDecaySeconds);
        state.Calm = Advance(state.Calm, 1 - rawAction, dt, float.PositiveInfinity, d.CalmDecaySeconds);
        var ambientTarget = RemapClamped(state.Calm, d.AmbientInputMinimum, d.AmbientInputMaximum, 0, 1);
        state.AmbientBasis = Advance(state.AmbientBasis, ambientTarget, dt, float.PositiveInfinity, d.AmbientDecaySeconds);
        var ambient = state.AmbientBasis * (1 - input.HazardRage);
        var mobNetwork = state.MobPressure < .1f ? 0 : state.MobPressure;
        var secondary = Clamp01(2 * state.DamageInflicted);
        var close = RemapClamped(input.CloseCommonAction, 0, d.CloseActionMaximum, 0, 1);

        UpdateCombat(input, state, time, output);
        EmitLayer(state, output, input.Id, MusicMixerLayers.MobBeating, damageDuck, "damage/common pressure");
        EmitLayer(state, output, input.Id, MusicMixerLayers.MobRules, 1 - mobNetwork, "mob pressure");
        EmitLayer(state, output, input.Id, MusicMixerLayers.HazardRage, 1 - input.HazardRage, "hazard rage");
        EmitLayer(state, output, input.Id, MusicMixerLayers.Combat, state.CombatActive ? 1 : 0, "combat state");
        EmitLayer(state, output, input.Id, MusicMixerLayers.CombatSecondary, 1 - secondary, "damage inflicted");
        EmitLayer(state, output, input.Id, MusicMixerLayers.CombatClose, 1 - close, "close common action");
        EmitLayer(state, output, input.Id, MusicMixerLayers.Adrenaline, input.Adrenaline, "adrenaline");
        EmitLayer(state, output, input.Id, MusicMixerLayers.Checkpoint, input.InCheckpoint ? 1 : 0, "checkpoint state");
        EmitLayer(state, output, input.Id, MusicMixerLayers.Ambient, ambient, "calm and hazard rage");

        UpdateMobbed(input, state, mobNetwork, output);
        UpdateBoss(input, state, output);
        UpdateHazard(input, state, time, output);
        UpdateSpecialAlerts(input, state, time, ambient, output);
        UpdateAtmosphere(input, state, time, ambient, output);
        UpdatePositionalStinger(input, state, time, output);
    }

    void UpdateCombat(MusicParticipantSnapshot input, ParticipantMemory state, double time, MusicDecisionBatch output)
    {
        var allowed = input.DynamicMusicAllowed && input.IsEligible && input.IsAlive && !input.IsIncapacitated && !input.BossPresent && !input.HazardAttacking;
        if (!state.CombatActive && allowed && input.VisibleCommonThreats > 0)
        {
            state.CombatActive = true;
            var intros = _configuration.Cues.CombatIntroEvents.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            if (intros.Length > 0) Play(output, input.Id, intros[_random.NextInt(intros.Length)], "combat started");
            PlayIfConfigured(output, input.Id, _configuration.Cues.CombatSecondaryEvent, "combat secondary started");
            PlayIfConfigured(output, input.Id, _configuration.Cues.CombatCloseEvent, "combat close layer started");
        }
        var stopSignal = Math.Max(state.CommonSight, state.SlowActivity);
        var canStop = !input.MobPressureActive && !input.IsIncapacitated && !input.BossPresent && !input.HazardAttacking && input.ActiveCommonThreats <= _configuration.Dynamic.CombatStopActiveCount && input.ScannedCommonThreats <= _configuration.Dynamic.CombatStopScannedCount;
        if (state.CombatActive && (!allowed || canStop && stopSignal < _configuration.Dynamic.CombatStopSignal))
        {
            state.CombatActive = false;
            foreach (var track in _configuration.Cues.CombatTrackIds.Where(x => !string.IsNullOrWhiteSpace(x))) output.Decisions.Add(NewDecision(MusicDecisionKind.StopTrack, input.Id, track, "combat ended") with { FadeSeconds = _configuration.CombatStopFadeSeconds });
        }
    }

    void UpdateMobbed(MusicParticipantSnapshot input, ParticipantMemory state, float mobNetwork, MusicDecisionBatch output)
    {
        var active = mobNetwork > 0;
        if (active && !state.MobbedActive) PlayIfConfigured(output, input.Id, _configuration.Cues.MobbedEvent, "mob pressure entered");
        else if (!active && state.MobbedActive && !string.IsNullOrWhiteSpace(_configuration.Cues.MobbedEvent)) output.Decisions.Add(NewDecision(MusicDecisionKind.StopEvent, input.Id, _configuration.Cues.MobbedEvent, "mob pressure cleared"));
        state.MobbedActive = active;
    }

    void UpdateBoss(MusicParticipantSnapshot input, ParticipantMemory state, MusicDecisionBatch output)
    {
        if (input.BossPresent && !state.BossPresent) PlayIfConfigured(output, input.Id, _configuration.Cues.BossApproachingEvent, "boss approaching");
        else if (!input.BossPresent && state.BossPresent && !string.IsNullOrWhiteSpace(_configuration.Cues.BossTrackId)) output.Decisions.Add(NewDecision(MusicDecisionKind.StopTrack, input.Id, _configuration.Cues.BossTrackId, "boss defeated") with { FadeSeconds = _configuration.BossStopFadeSeconds });
        state.BossPresent = input.BossPresent;
    }

    void UpdateHazard(MusicParticipantSnapshot input, ParticipantMemory state, double time, MusicDecisionBatch output)
    {
        TransitionEvent(input.Id, input.HazardBurning, ref state.HazardBurning, _configuration.Cues.HazardBurningEvent, _configuration.HazardStopFadeSeconds, "hazard burning", output);
        TransitionEvent(input.Id, input.HazardAttacking, ref state.HazardAttacking, _configuration.Cues.HazardAttackEvent, _configuration.HazardStopFadeSeconds, "hazard attacking", output);
        var raging = input.HazardRage > 0;
        if (raging && !state.HazardRaging && !string.IsNullOrWhiteSpace(_configuration.Cues.HazardRageEvent)) output.Decisions.Add(NewDecision(MusicDecisionKind.Play, input.Id, _configuration.Cues.HazardRageEvent, "hazard rage started") with { EmitterId = input.HazardEmitterId, Position = input.HazardPosition });
        else if (!raging && state.HazardRaging && !string.IsNullOrWhiteSpace(_configuration.Cues.HazardRageEvent)) output.Decisions.Add(NewDecision(MusicDecisionKind.StopEvent, input.Id, _configuration.Cues.HazardRageEvent, "hazard rage ended") with { FadeSeconds = _configuration.HazardRageStopFadeSeconds });
        state.HazardRaging = raging;

        var wandering = input.WanderingHazards.OrderBy(x => x.Anger).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
        if (wandering is null)
        {
            if (state.WanderingHazardActive && !string.IsNullOrWhiteSpace(_configuration.Cues.WanderingHazardTrackId)) output.Decisions.Add(NewDecision(MusicDecisionKind.StopTrack, input.Id, _configuration.Cues.WanderingHazardTrackId, "no wandering hazard") with { FadeSeconds = _configuration.HazardStopFadeSeconds });
            state.WanderingHazardActive = false;
        }
        else if (time >= state.WanderingHazardReadyAt)
        {
            var tier = wandering.Anger < .25f ? 0 : wandering.Anger < .5f ? 1 : wandering.Anger < .75f ? 2 : 3;
            output.Decisions.Add(NewDecision(MusicDecisionKind.Play, input.Id, _configuration.Cues.WanderingHazardEventsLowToHigh[tier], "wandering hazard anger tier") with { EmitterId = wandering.Id, Position = wandering.Position });
            state.WanderingHazardActive = true; state.WanderingHazardReadyAt = time + _configuration.Dynamic.WanderingHazardIntervalSeconds;
        }
    }

    void TransitionEvent(string participantId, bool active, ref bool previous, string eventId, float fade, string reason, MusicDecisionBatch output)
    {
        if (active && !previous) PlayIfConfigured(output, participantId, eventId, reason + " started");
        else if (!active && previous && !string.IsNullOrWhiteSpace(eventId)) output.Decisions.Add(NewDecision(MusicDecisionKind.StopEvent, participantId, eventId, reason + " ended") with { FadeSeconds = fade });
        previous = active;
    }

    void UpdateSpecialAlerts(MusicParticipantSnapshot input, ParticipantMemory state, double time, float ambient, MusicDecisionBatch output)
    {
        if (ambient <= _configuration.SpecialAlertMinimumAmbient) return;
        var profiles = _configuration.SpecialAlerts;
        foreach (var threat in input.SpecialThreats.OrderBy(x => x.ArchetypeId, StringComparer.Ordinal).ThenBy(x => x.EmitterId, StringComparer.Ordinal))
        {
            var index = -1;
            for (var i = 0; i < profiles.Count; i++) if (string.Equals(profiles[i].ArchetypeId, threat.ArchetypeId, StringComparison.Ordinal)) { index = i; break; }
            state.SpecialReadyAt.TryGetValue(threat.ArchetypeId, out var readyAt);
            if (index < 0 || threat.Distance > profiles[index].ScanMaximumDistance || readyAt > time) continue;
            var profile = profiles[index];
            var eventId = threat.Distance <= profile.CloseMaximumDistance ? profile.CloseEvent : threat.Distance >= profile.FarMinimumDistance ? profile.FarEvent : profile.MiddleEvent;
            output.Decisions.Add(NewDecision(MusicDecisionKind.Play, input.Id, eventId, $"special alert:{threat.ArchetypeId}") with { EmitterId = threat.EmitterId, Position = threat.Position });
            var min = threat.Distance <= profile.CloseMaximumDistance ? profile.RandomMultiplierMaximum : threat.Distance >= profile.FarMinimumDistance ? profile.RandomMultiplierMaximum * 2 : profile.RandomMultiplierMaximum;
            var max = threat.Distance <= profile.CloseMaximumDistance ? (int)MathF.Round(profile.RandomMultiplierMaximum * 1.4f) : threat.Distance >= profile.FarMinimumDistance ? (int)MathF.Round(profile.RandomMultiplierMaximum * 2.5f) : profile.RandomMultiplierMaximum * 2;
            var multiplier = min + _random.NextInt(max - min + 1);
            state.SpecialReadyAt[threat.ArchetypeId] = time + multiplier * profile.IntervalBeats * (60.0 / profile.Bpm);
            for (var step = 1; step < profiles.Count; step++)
            {
                var peer = profiles[(index + step) % profiles.Count].ArchetypeId;
                var floor = time + _configuration.SpecialPeerStaggerStartSeconds + (step - 1) * _configuration.SpecialPeerStaggerStepSeconds;
                state.SpecialReadyAt.TryGetValue(peer, out var peerReady); if (peerReady < floor) state.SpecialReadyAt[peer] = floor;
            }
            break;
        }
    }

    void UpdateAtmosphere(MusicParticipantSnapshot input, ParticipantMemory state, double time, float ambient, MusicDecisionBatch output)
    {
        var danger = state.Threat > 0;
        if (danger && !state.DangerAtmosphereActive && time >= state.AtmosphereReadyAt)
        {
            PlayIfConfigured(output, input.Id, _configuration.Cues.DangerAtmosphereEvent, "danger atmosphere"); state.DangerAtmosphereActive = true; state.AtmosphereReadyAt = time + _configuration.AtmosphereRetrySeconds;
        }
        else if (!danger && state.DangerAtmosphereActive && ambient <= 0)
        {
            if (!string.IsNullOrWhiteSpace(_configuration.Cues.DangerAtmosphereEvent)) output.Decisions.Add(NewDecision(MusicDecisionKind.StopEvent, input.Id, _configuration.Cues.DangerAtmosphereEvent, "danger atmosphere cleared"));
            state.DangerAtmosphereActive = false; state.AtmosphereReadyAt = time + _configuration.AtmosphereTransitionSeconds;
        }
        else if (!danger && !state.DangerAtmosphereActive && ambient > 0 && time >= state.AtmosphereReadyAt)
        {
            PlayIfConfigured(output, input.Id, _configuration.Cues.SafeAtmosphereEvent, "safe atmosphere"); state.AtmosphereReadyAt = time + _configuration.AtmosphereRetrySeconds + 10;
        }
    }

    void UpdatePositionalStinger(MusicParticipantSnapshot input, ParticipantMemory state, double time, MusicDecisionBatch output)
    {
        if (state.AmbientBasis <= _configuration.Dynamic.PositionalStingerAmbientMinimum || time < state.PositionalStingerReadyAt || string.IsNullOrWhiteSpace(_configuration.Cues.PositionalStingerEvent)) return;
        var candidates = input.PositionalCandidates.Where(x => x.Eligible).OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(); if (candidates.Length == 0) return;
        var selected = candidates[_random.NextInt(candidates.Length)];
        output.Decisions.Add(NewDecision(MusicDecisionKind.Play, input.Id, _configuration.Cues.PositionalStingerEvent, "positional stinger") with { EmitterId = selected.Id, Position = selected.Position, StartOffsetSeconds = .1f });
        var multiplier = 1 + _random.NextInt(_configuration.Dynamic.PositionalStingerRandomMultiplierMaximum);
        state.PositionalStingerReadyAt = time + multiplier * _configuration.Dynamic.PositionalStingerBeats * (60.0 / _configuration.Dynamic.PositionalStingerBpm);
    }

    void EmitLayer(ParticipantMemory state, MusicDecisionBatch output, string participantId, string layer, float value, string reason)
    {
        value = Clamp01(value);
        if (state.LastLayers.TryGetValue(layer, out var previous) && MathF.Abs(previous - value) <= .000001f) return;
        state.LastLayers[layer] = value;
        output.Decisions.Add(NewDecision(MusicDecisionKind.SetMixerLayer, participantId, "", reason) with { LayerId = layer, Value = value });
    }

    void PlayIfConfigured(MusicDecisionBatch output, string participantId, string eventId, string reason) { if (!string.IsNullOrWhiteSpace(eventId)) Play(output, participantId, eventId, reason); }
    void Play(MusicDecisionBatch output, string participantId, string eventId, string reason) => output.Decisions.Add(NewDecision(MusicDecisionKind.Play, participantId, eventId, reason));
    MusicDecision NewDecision(MusicDecisionKind kind, string participantId, string target, string reason) => new(_nextDecisionId++, kind, participantId, target, reason);

    static float Advance(float current, float target, float delta, float risePerSecond, float decaySeconds)
    {
        target = Clamp01(target);
        if (target >= current) return float.IsPositiveInfinity(risePerSecond) ? target : Math.Min(target, current + risePerSecond * delta);
        return Math.Max(target, current - delta / decaySeconds);
    }
    static float RemapClamped(float value, float inputMin, float inputMax, float outputMin, float outputMax) => inputMax <= inputMin ? outputMax : outputMin + Clamp01((value - inputMin) / (inputMax - inputMin)) * (outputMax - outputMin);
    static float Clamp01(float value) => Math.Clamp(value, 0, 1);

    public MusicDirectorState CaptureState() => new(
        _configuration.Version, _hasTicked, _lastTickTime, _lastEventSequence, _nextDecisionId, _random.State,
        _participants.Values.OrderBy(x => x.Id, StringComparer.Ordinal).Select(x => x.Capture()).ToArray());

    public bool RemoveParticipant(string participantId) => _participants.Remove(participantId);
    public void Reset()
    {
        _participants.Clear(); _lastEventSequence = 0; _nextDecisionId = 1; _lastTickTime = 0; _hasTicked = false; _random = new(_configuration.Seed);
    }

    sealed class ParticipantMemory
    {
        public readonly string Id;
        public float PreviousDamage, Damage, FastActivity, SlowActivity, DamageInflicted, CommonSight, MobPressure, Action, Threat, Calm, AmbientBasis;
        public bool CombatActive, MobbedActive, BossPresent, HazardBurning, HazardAttacking, HazardRaging, WanderingHazardActive, DangerAtmosphereActive, ScenarioEnded;
        public double AtmosphereReadyAt, PositionalStingerReadyAt, WanderingHazardReadyAt, LargeAreaRevealReadyAt;
        public readonly Dictionary<string, double> SpecialReadyAt = new(StringComparer.Ordinal);
        public readonly Dictionary<string, float> LastLayers = new(StringComparer.Ordinal);
        public ParticipantMemory(string id, float previousDamage, IReadOnlyList<SpecialMusicAlertConfiguration> alerts) { Id = id; PreviousDamage = previousDamage; foreach (var alert in alerts) SpecialReadyAt[alert.ArchetypeId] = 0; }
        public ParticipantMemory(MusicParticipantState state)
        {
            Id = state.ParticipantId; PreviousDamage = state.PreviousDamage;
            (Damage, FastActivity, SlowActivity, DamageInflicted, CommonSight, MobPressure, Action, Threat, Calm, AmbientBasis) = state.Envelopes;
            CombatActive = state.CombatActive; MobbedActive = state.MobbedActive; BossPresent = state.BossPresent; HazardBurning = state.HazardBurning; HazardAttacking = state.HazardAttacking; HazardRaging = state.HazardRaging; WanderingHazardActive = state.WanderingHazardActive; DangerAtmosphereActive = state.DangerAtmosphereActive; ScenarioEnded = state.ScenarioEnded; AtmosphereReadyAt = state.AtmosphereReadyAt; PositionalStingerReadyAt = state.PositionalStingerReadyAt; WanderingHazardReadyAt = state.WanderingHazardReadyAt; LargeAreaRevealReadyAt = state.LargeAreaRevealReadyAt;
            foreach (var pair in state.SpecialAlertReadyAt) SpecialReadyAt[pair.Key] = pair.Value; foreach (var pair in state.LastMixerLayers) LastLayers[pair.Key] = pair.Value;
        }
        public MusicParticipantState Capture() => new(Id, PreviousDamage, new(Damage, FastActivity, SlowActivity, DamageInflicted, CommonSight, MobPressure, Action, Threat, Calm, AmbientBasis), CombatActive, MobbedActive, BossPresent, HazardBurning, HazardAttacking, HazardRaging, WanderingHazardActive, DangerAtmosphereActive, ScenarioEnded, AtmosphereReadyAt, PositionalStingerReadyAt, WanderingHazardReadyAt, LargeAreaRevealReadyAt, new Dictionary<string, double>(SpecialReadyAt), new Dictionary<string, float>(LastLayers));
        public void ResetDynamic()
        {
            Damage = FastActivity = SlowActivity = DamageInflicted = CommonSight = MobPressure = Action = Threat = Calm = AmbientBasis = 0; CombatActive = MobbedActive = BossPresent = HazardBurning = HazardAttacking = HazardRaging = WanderingHazardActive = DangerAtmosphereActive = ScenarioEnded = false; AtmosphereReadyAt = PositionalStingerReadyAt = WanderingHazardReadyAt = LargeAreaRevealReadyAt = 0; LastLayers.Clear(); foreach (var key in SpecialReadyAt.Keys.ToArray()) SpecialReadyAt[key] = 0;
        }
    }
}
