#nullable enable annotations
namespace SboxDirector;

public sealed record PlacementChoice(SpawnCandidate Candidate, float Score, bool IsFallback = false);

public interface IPlacementPolicy
{
    bool IsAllowed(SpawnCandidate candidate, PlacementProfile profile, WorldSnapshot world);
    float ScoreAdjustment(SpawnCandidate candidate, PlacementProfile profile, WorldSnapshot world);
}

/// <summary>Implement this alongside IPlacementPolicy when a custom placement policy owns mutable deterministic state.</summary>
public interface IPersistentPlacementPolicy
{
    string PersistenceId { get; }
    IReadOnlyDictionary<string, string> CapturePlacementState();
    void RestorePlacementState(IReadOnlyDictionary<string, string> state);
}

public sealed class DefaultPlacementPolicy : IPlacementPolicy
{
    public bool IsAllowed(SpawnCandidate candidate, PlacementProfile profile, WorldSnapshot world) => true;
    public float ScoreAdjustment(SpawnCandidate candidate, PlacementProfile profile, WorldSnapshot world) => 0f;
}

public sealed class PlacementSelector
{
    public PlacementChoice? Select(IReadOnlyList<SpawnCandidate> candidates, PlacementProfile profile, DeterministicRandom random, WorldSnapshot? world = null, IPlacementPolicy? policy = null)
    {
        var ordered = OrderCandidates(candidates, profile.SelectionMode, random);
        var strict = BuildChoices(ordered, profile, world, policy, profile.SelectionMode, ignoreRoute: false, broadFallback: false);
        if (strict.Count > 0) return Choose(strict, profile.SelectionMode, random, false);
        if (profile.FallbackMode == PlacementFallbackMode.None) return null;
        ordered = OrderCandidates(candidates, profile.SelectionMode, random);
        var fallback = BuildChoices(ordered, profile, world, policy, profile.SelectionMode, ignoreRoute: true, broadFallback: profile.FallbackMode == PlacementFallbackMode.AnyReachableHidden);
        return fallback.Count == 0 ? null : Choose(fallback, profile.SelectionMode, random, true);
    }

    static List<PlacementChoice> BuildChoices(IReadOnlyList<SpawnCandidate> candidates, PlacementProfile profile, WorldSnapshot? world, IPlacementPolicy? policy, PlacementSelectionMode mode, bool ignoreRoute, bool broadFallback)
    {
        var choices = new List<PlacementChoice>();
        foreach (var candidate in candidates)
        {
            if (!candidate.Reachable || !candidate.Spawnable || (!profile.AllowVisible && candidate.Visible)) continue;
            if (candidate.AreaWidth < profile.MinimumAreaWidth || candidate.AreaHeight < profile.MinimumAreaHeight) continue;
            if (!broadFallback && (candidate.NearestParticipantDistance < profile.MinimumDistance || candidate.NearestParticipantDistance > profile.MaximumDistance)) continue;
            if (!broadFallback && candidate.ThreatSeparation < profile.MinimumThreatSeparation) continue;
            if (profile.RequiredTags.Any(tag => !candidate.Tags.Contains(tag)) || profile.ExcludedTags.Any(candidate.Tags.Contains)) continue;
            if (!ignoreRoute && (candidate.PathDistance < profile.MinimumPathDistance || candidate.IngressDistance < profile.MinimumIngressDistance || candidate.ClearRadius < profile.MinimumClearRadius)) continue;
            if (!ignoreRoute && profile.AllowedRelations.Count > 0 && !profile.AllowedRelations.Contains(candidate.AdvanceRelation)) continue;
            if (world is not null && policy is not null && !policy.IsAllowed(candidate, profile, world)) continue;
            var score = mode == PlacementSelectionMode.HighestProgressFirstTie ? candidate.Progress : 0f;
            if (mode == PlacementSelectionMode.WeightedScore)
            {
                var distanceRange = Math.Max(1f, profile.MaximumDistance - profile.MinimumDistance);
                var distanceScore = 1f - Math.Min(1f, Math.Abs(candidate.NearestParticipantDistance - profile.IdealDistance) / distanceRange);
                score = distanceScore * profile.DistanceWeight + candidate.AreaCapacity * profile.CapacityWeight + Math.Min(1f, candidate.ThreatSeparation / Math.Max(1f, profile.MinimumThreatSeparation * 2f)) * profile.SeparationWeight;
                if (candidate.RelativeProgress < 0) score += Math.Min(1f, -candidate.RelativeProgress) * profile.BehindProgressWeight;
                score += Math.Min(1f, candidate.PathDistance / Math.Max(1f, profile.MinimumPathDistance * 2f)) * profile.PathDistanceWeight;
                score += Math.Min(1f, candidate.IngressDistance / Math.Max(1f, profile.MinimumIngressDistance * 2f)) * profile.IngressWeight;
                score += Math.Min(1f, candidate.ClearRadius / Math.Max(1f, profile.MinimumClearRadius * 2f)) * profile.ClearRadiusWeight;
                if (world is not null && policy is not null)
                {
                    var adjustment = policy.ScoreAdjustment(candidate, profile, world); if (!float.IsFinite(adjustment)) throw new InvalidOperationException("Placement policy returned a non-finite score adjustment."); score += adjustment;
                }
            }
            if (!float.IsFinite(score)) throw new InvalidOperationException("Placement scoring produced a non-finite value.");
            choices.Add(new PlacementChoice(candidate, score));
        }
        return choices;
    }

    static IReadOnlyList<SpawnCandidate> OrderCandidates(IReadOnlyList<SpawnCandidate> candidates, PlacementSelectionMode mode, DeterministicRandom random)
    {
        if (mode != PlacementSelectionMode.NativeRandomTranspositionFirst) return candidates;
        var shuffled = candidates.ToArray();
        for (var index = 0; index < shuffled.Length; index++)
        {
            var other = random.NextInt(shuffled.Length);
            (shuffled[index], shuffled[other]) = (shuffled[other], shuffled[index]);
        }
        return shuffled;
    }

    static PlacementChoice Choose(List<PlacementChoice> choices, PlacementSelectionMode mode, DeterministicRandom random, bool fallback)
    {
        if (mode is PlacementSelectionMode.FirstEligible or PlacementSelectionMode.NativeRandomTranspositionFirst) return choices[0] with { IsFallback = fallback };
        if (mode == PlacementSelectionMode.HighestProgressFirstTie)
        {
            var progressChoice = choices[0];
            for (var index = 1; index < choices.Count; index++) if (choices[index].Candidate.Progress > progressChoice.Candidate.Progress) progressChoice = choices[index];
            return progressChoice with { IsFallback = fallback };
        }
        var best = choices.Max(x => x.Score); var tied = choices.Where(x => Math.Abs(x.Score - best) < 0.00001f).ToArray();
        var selected = tied[random.NextInt(tied.Length)]; return selected with { IsFallback = fallback };
    }
}
