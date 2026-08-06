using SboxDirector;

namespace AdaptiveDirectorExamples;

/// <summary>Shows the authority/client boundary. Use your game's RPC or snapshot system for SendToOwningClient.</summary>
public sealed class NetworkedMusicAuthorityExample
{
    readonly MusicDirectorCore _director = new(RecoveredMusicDirectorDefaults.CreateConfiguration(ExampleMusicSetup.CreateCues(), ExampleMusicSetup.CreateSpecialAlerts(), seed: 9001));
    long _nextEventSequence = 1;

    public MusicDecisionBatch Tick(MusicWorldSnapshot world) => _director.Tick(world);

    public MusicDecisionBatch PlayScriptedCue(MusicWorldSnapshot world, string participantId, string eventId) => _director.Tick(world, new[]
    {
        new MusicGameplayEvent(_nextEventSequence++, MusicTriggerKind.Play, participantId, eventId)
    });

    // Replicate MusicDecision fields, not engine sound handles.
    public void TickAndSend(MusicWorldSnapshot world, Action<string, MusicDecisionBatch> sendToOwningClient)
    {
        var batch = Tick(world);
        foreach (var participant in batch.Decisions.Select(x => x.ParticipantId).Distinct(StringComparer.Ordinal))
        {
            var filtered = new MusicDecisionBatch { Time = batch.Time };
            filtered.Decisions.AddRange(batch.Decisions.Where(x => x.ParticipantId == participant));
            sendToOwningClient(participant, filtered);
        }
    }
}

public sealed class NetworkedMusicClientExample
{
    readonly MusicRuntime _runtime;
    readonly IMusicAudioSink _audio;

    public NetworkedMusicClientExample(string localParticipantId, IMusicAudioSink audio)
    {
        _runtime = new MusicRuntime(localParticipantId, ExampleMusicSetup.CreateCatalog()); _audio = audio;
    }

    public MusicRuntimeBatch Receive(MusicDecisionBatch replicatedBatch, bool playerDead, bool scenarioEnded)
    {
        _runtime.SetLifecycle(playerDead, scenarioEnded);
        var result = _runtime.Apply(replicatedBatch);
        foreach (var action in result.Actions) _audio.Apply(action);
        return result;
    }

    public MusicRuntimeBatch OnAudioFinished(long musicInstanceId, double time)
    {
        var result = _runtime.ReportCompleted(musicInstanceId, time);
        foreach (var action in result.Actions) _audio.Apply(action);
        return result;
    }
}
