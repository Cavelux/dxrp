using SboxDirector;

namespace AdaptiveDirectorExamples;

public sealed record CombinedRoundResult(DirectorRunResult Director, MusicHostRunResult? Music);

/// <summary>One round manager using encounter direction and optional adaptive music together.</summary>
public sealed class RoundDirectorAndMusicExample
{
    readonly RoundSessionFlow _rounds = new();
    readonly DirectorHostRunner _directorRunner;
    readonly MusicDirectorHostRunner? _musicRunner;
    readonly Func<MusicWorldSnapshot>? _captureMusic;
    readonly List<MusicGameplayEvent> _musicEvents = new();
    long _nextMusicEvent = 1;

    public RoundDirectorAndMusicExample(
        Func<WorldSnapshot> captureWorld,
        Func<SpawnRequest, SpawnRequestResult> spawn,
        bool useAdaptiveMusic = false,
        string localParticipantId = "local",
        Func<MusicWorldSnapshot>? captureMusic = null,
        IMusicAudioSink? audio = null)
    {
        var schedule = new CompositeEncounterSchedule(
            new MilestoneEncounterSchedule("round-boss", EncounterKind.Boss, .7f, .9f),
            new RuleBasedEncounterSchedule(new[]
            {
                new EncounterRule { Id = "waves", Kind = EncounterKind.CommonWave, Count = 10, Priority = 10, CooldownSeconds = 12 },
                new EncounterRule { Id = "specials", Kind = EncounterKind.Special, Count = 1, Priority = 20, CooldownSeconds = 18, Probability = .5f }
            }));
        var director = new DirectorCore(new DirectorConfiguration { Version = "combined-round-v1", Seed = 555 }, schedule: schedule);
        _directorRunner = new DirectorHostRunner(director, _rounds, new DelegateWorldSource(captureWorld), new DelegateSpawnExecutor(spawn));

        if (useAdaptiveMusic)
        {
            _captureMusic = captureMusic ?? throw new ArgumentNullException(nameof(captureMusic));
            var music = new MusicDirectorCore(RecoveredMusicDirectorDefaults.CreateConfiguration(ExampleMusicSetup.CreateCues(), ExampleMusicSetup.CreateSpecialAlerts(), seed: 555));
            var runtime = new MusicRuntime(localParticipantId, ExampleMusicSetup.CreateCatalog());
            _musicRunner = new MusicDirectorHostRunner(music, runtime, audio ?? throw new ArgumentNullException(nameof(audio)));
        }
    }

    public void BeginRound(string roundId)
    {
        _rounds.BeginRound(roundId);
        QueueMusic(MusicTriggerKind.Reset);
    }

    public CombinedRoundResult FixedUpdate()
    {
        var director = _directorRunner.Update();
        MusicHostRunResult? music = null;
        if (_musicRunner is not null && _captureMusic is not null)
        {
            music = _musicRunner.Update(_captureMusic(), _musicEvents.ToArray()); _musicEvents.Clear();
        }
        return new(director, music);
    }

    public void PlayRoundStageCue(string participantId, string eventId) => QueueMusic(MusicTriggerKind.Play, participantId, eventId);

    public MusicHostRunResult? EndRound()
    {
        _directorRunner.Stop(); _rounds.EndRound();
        if (_musicRunner is null || _captureMusic is null) return null;
        QueueMusic(MusicTriggerKind.StopAll); QueueMusic(MusicTriggerKind.ScenarioEnded);
        var captured = _captureMusic();
        var world = new MusicWorldSnapshot { Time = captured.Time, Participants = captured.Participants, ScenarioEnded = true };
        var result = _musicRunner.Update(world, _musicEvents.ToArray()); _musicEvents.Clear(); return result;
    }

    public void ReportPressure(string playerId, PressureSeverity severity, double time) => _directorRunner.ReportPressure(new PressureEvent(playerId, severity, "round gameplay"), time);

    void QueueMusic(MusicTriggerKind kind, string participantId = "", string targetId = "")
    {
        if (_musicRunner is not null) _musicEvents.Add(new MusicGameplayEvent(_nextMusicEvent++, kind, participantId, targetId));
    }
}
