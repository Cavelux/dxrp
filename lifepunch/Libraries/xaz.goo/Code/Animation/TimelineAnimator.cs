namespace Goo.Animation;

public record struct TimelineAnimator
{
    public Timeline Timeline;
    public float    Elapsed;
    public bool     Paused;
    public float    Speed;

    public TimelineAnimator(Timeline timeline)
    {
        Timeline = timeline;
        Elapsed  = 0f;
        Paused   = false;
        Speed    = 1f;
    }

    public void Update(float dt) { if (!Paused) Elapsed += dt * Speed; }
    public void Pause()          { Paused = true; }
    public void Resume()         { Paused = false; }
    public void Restart()        { Elapsed = 0f; Paused = false; }
    public void Seek(float t)    { Elapsed = t; }

    public readonly TimelineSample Sample => Timeline.Eval(Elapsed);

    public readonly bool IsFinished
    {
        get
        {
            if (Timeline.Iterations <= 0) return false;
            if (Timeline.Duration   <= 0f) return true;
            return Elapsed >= Timeline.Duration * Timeline.Iterations;
        }
    }
}
