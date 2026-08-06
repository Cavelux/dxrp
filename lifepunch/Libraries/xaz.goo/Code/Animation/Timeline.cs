using System;
using System.Collections.Immutable;

namespace Goo.Animation;

public readonly record struct Segment
{
    public float StartTime { get; init; }
    public float Duration  { get; init; }
    public Tween Tween     { get; init; }
}

public readonly record struct TimelineSample
{
    public int   SegmentIndex { get; init; }
    public float Value        { get; init; }
}

public readonly record struct Timeline
{
    public ImmutableArray<Segment> Segments { get; init; }
    public int   Iterations { get; init; }
    public float Duration   { get; init; }

    /// <summary>Auto-concatenated: segment N starts where N-1 ends. Skips the internal sort because the input is ascending by construction.</summary>
    public static Timeline Sequence(params Tween[] tweens)
    {
        if (tweens == null || tweens.Length == 0)
            throw new ArgumentException("Timeline requires at least one segment.", nameof(tweens));

        var builder  = ImmutableArray.CreateBuilder<Segment>(tweens.Length);
        float cursor = 0f;
        for (int i = 0; i < tweens.Length; i++)
        {
            if (tweens[i].Duration <= 0f)
                throw new ArgumentException(
                    $"Timeline segment {i} has duration {tweens[i].Duration}; durations must be > 0.",
                    nameof(tweens));
            builder.Add(new Segment
            {
                StartTime = cursor,
                Duration  = tweens[i].Duration,
                Tween     = tweens[i],
            });
            cursor += tweens[i].Duration;
        }
        return new Timeline
        {
            Segments   = builder.MoveToImmutable(),
            Iterations = 1,
            Duration   = cursor,
        };
    }

    /// <summary>Explicit timing: each entry is (absolute startTime, tween), gaps allowed; sorts internally so stored Segments are ascending by StartTime.</summary>
    public static Timeline At(params (float startTime, Tween tween)[] entries)
    {
        if (entries == null || entries.Length == 0)
            throw new ArgumentException("Timeline requires at least one segment.", nameof(entries));

        var arr = new Segment[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].startTime < 0f)
                throw new ArgumentException(
                    $"Timeline segment {i} has StartTime {entries[i].startTime}; StartTime must be >= 0.",
                    nameof(entries));
            if (entries[i].tween.Duration <= 0f)
                throw new ArgumentException(
                    $"Timeline segment {i} has duration {entries[i].tween.Duration}; durations must be > 0.",
                    nameof(entries));
            arr[i] = new Segment
            {
                StartTime = entries[i].startTime,
                Duration  = entries[i].tween.Duration,
                Tween     = entries[i].tween,
            };
        }
        Array.Sort(arr, static (x, y) => x.StartTime.CompareTo(y.StartTime));

        for (int i = 1; i < arr.Length; i++)
        {
            float prevEnd = arr[i - 1].StartTime + arr[i - 1].Duration;
            if (prevEnd > arr[i].StartTime)
                throw new ArgumentException(
                    $"Timeline segments {i - 1} and {i} overlap: segment {i - 1} ends at {prevEnd} but segment {i} starts at {arr[i].StartTime}.",
                    nameof(entries));
        }

        float duration = arr[arr.Length - 1].StartTime + arr[arr.Length - 1].Duration;
        return new Timeline
        {
            Segments   = ImmutableArray.Create(arr),
            Iterations = 1,
            Duration   = duration,
        };
    }

    public TimelineSample Eval(float elapsedSec)
    {
        if (Iterations <= 0)
        {
            // Loop forever. Wrap into [0, Duration) using positive-result modulo.
            if (elapsedSec < 0f) elapsedSec = 0f;
            elapsedSec %= Duration;
        }
        else
        {
            float totalPlayTime = Duration * Iterations;
            if (elapsedSec >= totalPlayTime) elapsedSec = Duration;
            else if (elapsedSec > Duration)
            {
                // Mid-play, cycle 2..Iterations: wrap into the canonical [0, Duration) range.
                elapsedSec %= Duration;
            }
        }

        int idx = -1;
        for (int i = 0; i < Segments.Length; i++)
        {
            if (Segments[i].StartTime <= elapsedSec) idx = i;
            else break;
        }

        if (idx < 0)
            return new TimelineSample { SegmentIndex = -1, Value = 0f };

        var seg     = Segments[idx];
        float local = elapsedSec - seg.StartTime;
        if (local > seg.Duration) local = seg.Duration;

        return new TimelineSample
        {
            SegmentIndex = idx,
            Value        = seg.Tween.Eval(local),
        };
    }
}

public static class TimelineExtensions
{
    public static Timeline Loop(this Timeline tl)         => tl with { Iterations = -1 };
    public static Timeline Times(this Timeline tl, int n) => tl with { Iterations = n };
}
