using SboxDirector;

namespace AdaptiveDirectorExamples;

public sealed record ExampleSaveDocument(string DirectorJson, string MusicAuthorityJson, string MusicRuntimeJson);

/// <summary>Persists encounter direction, authoritative music, and client/local track queues together.</summary>
public sealed class SaveRestoreEverythingExample
{
    readonly DirectorConfiguration _directorConfiguration = new() { Version = "save-example-v1", Seed = 42 };
    readonly MusicDirectorConfiguration _musicConfiguration = RecoveredMusicDirectorDefaults.CreateConfiguration(ExampleMusicSetup.CreateCues(), ExampleMusicSetup.CreateSpecialAlerts(), seed: 42);
    DirectorCore _director;
    MusicDirectorCore _music;
    MusicRuntime _runtime;

    public SaveRestoreEverythingExample(string participantId)
    {
        ParticipantId = participantId; _director = new DirectorCore(_directorConfiguration); _music = new MusicDirectorCore(_musicConfiguration); _runtime = new MusicRuntime(participantId, ExampleMusicSetup.CreateCatalog());
    }

    public string ParticipantId { get; }
    public DirectorDecisionBatch TickDirector(WorldSnapshot world) => _director.Tick(world);
    public void ApplySpawnResult(SpawnRequestResult result) => _director.ApplyResult(result);
    public void ReportPressure(PressureEvent pressure, double time) => _director.ApplyPressure(pressure, time);
    public MusicRuntimeBatch TickMusic(MusicWorldSnapshot world)
    {
        var decisions = _music.Tick(world); return _runtime.Apply(decisions);
    }

    public ExampleSaveDocument Save() => new(
        DirectorStateCodec.ToJson(_director.CaptureState()),
        MusicStateCodec.DirectorToJson(_music.CaptureState()),
        MusicStateCodec.RuntimeToJson(_runtime.CaptureState()));

    public void Load(ExampleSaveDocument save)
    {
        var directorState = DirectorStateCodec.FromJson(save.DirectorJson);
        var musicState = MusicStateCodec.DirectorFromJson(save.MusicAuthorityJson);
        var runtimeState = MusicStateCodec.RuntimeFromJson(save.MusicRuntimeJson);
        _director = new DirectorCore(_directorConfiguration, directorState);
        _music = new MusicDirectorCore(_musicConfiguration, musicState);
        _runtime = new MusicRuntime(ParticipantId, ExampleMusicSetup.CreateCatalog(), runtimeState);
    }
}
