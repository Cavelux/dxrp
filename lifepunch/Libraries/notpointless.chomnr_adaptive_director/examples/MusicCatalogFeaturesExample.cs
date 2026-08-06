using SboxDirector;

namespace AdaptiveDirectorExamples;

/// <summary>Small direct runtime demo for priority, ducking, blocking, timing tags, and after-death cues.</summary>
public sealed class MusicCatalogFeaturesExample
{
    readonly MusicRuntime _runtime;
    long _decisionId = 1;

    public MusicCatalogFeaturesExample(string participantId) { _runtime = new MusicRuntime(participantId, ExampleMusicSetup.CreateCatalog()); ParticipantId = participantId; }
    public string ParticipantId { get; }

    public MusicRuntimeBatch Play(string eventId, double time)
    {
        var decisions = new MusicDecisionBatch { Time = time };
        decisions.Decisions.Add(new MusicDecision(_decisionId++, MusicDecisionKind.Play, ParticipantId, eventId, "example"));
        return _runtime.Apply(decisions);
    }

    public MusicRuntimeBatch StopTrack(string trackId, float fadeSeconds, double time)
    {
        var decisions = new MusicDecisionBatch { Time = time };
        decisions.Decisions.Add(new MusicDecision(_decisionId++, MusicDecisionKind.StopTrack, ParticipantId, trackId, "example") { FadeSeconds = fadeSeconds });
        return _runtime.Apply(decisions);
    }

    public MusicRuntimeBatch StartRhythmAndQueueAccent(double time)
    {
        var first = new MusicDecision(_decisionId++, MusicDecisionKind.Play, ParticipantId, "game.rhythm.master", "start master");
        var accent = new MusicDecision(_decisionId++, MusicDecisionKind.Play, ParticipantId, "game.rhythm.accent", "sync to tag 2");
        var decisions = new MusicDecisionBatch { Time = time }; decisions.Decisions.Add(first); decisions.Decisions.Add(accent);
        return _runtime.Apply(decisions); // The accent is deferred to the master's four-second tag plus 0.25 seconds.
    }

    public MusicRuntimeBatch PlayDeathCue(double time)
    {
        _runtime.SetLifecycle(participantDead: true, scenarioEnded: false);
        return Play("game.player.death", time); // Allowed because this event has AllowAfterDeath.
    }
}
