#nullable enable annotations
using System.Text.Json;

namespace SboxDirector;

public static class MusicStateCodec
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static string DirectorToJson(MusicDirectorState state) => JsonSerializer.Serialize(state, Options);
    public static MusicDirectorState DirectorFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Music Director state JSON is required.");
        var state = JsonSerializer.Deserialize<MusicDirectorState>(json, Options) ?? throw new InvalidOperationException("Music Director state is incomplete.");
        if (string.IsNullOrWhiteSpace(state.ConfigurationVersion) || state.Participants is null || state.NextDecisionId <= 0) throw new InvalidOperationException("Music Director state is incomplete.");
        return state;
    }
    public static string RuntimeToJson(MusicRuntimeState state) => JsonSerializer.Serialize(state, Options);
    public static MusicRuntimeState RuntimeFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Music runtime state JSON is required.");
        var state = JsonSerializer.Deserialize<MusicRuntimeState>(json, Options) ?? throw new InvalidOperationException("Music runtime state is incomplete.");
        if (string.IsNullOrWhiteSpace(state.ConfigurationVersion) || string.IsNullOrWhiteSpace(state.ParticipantId) || state.Active is null || state.Queued is null || state.MixerLayers is null || state.TrackVolumes is null || state.NextActionId <= 0 || state.NextInstanceId <= 0 || !double.IsFinite(state.LastTime)) throw new InvalidOperationException("Music runtime state is incomplete.");
        return state;
    }
}
