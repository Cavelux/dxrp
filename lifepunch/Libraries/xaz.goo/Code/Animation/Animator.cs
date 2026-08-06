namespace Goo.Animation;

public record struct Animator
{
    public Tween Tween;
    public float Elapsed;
    public bool  Paused;
    public float Speed;

    public Animator(Tween tween)
    {
        Tween   = tween;
        Elapsed = 0f;
        Paused  = false;
        Speed   = 1f;
    }

    public void Update(float dt) { if (!Paused) Elapsed += dt * Speed; }
    public void Pause()          { Paused = true; }
    public void Resume()         { Paused = false; }
    public void Restart()        { Elapsed = 0f; Paused = false; }
    public void Seek(float t)    { Elapsed = t; }

    public readonly float Value => Tween.Eval(Elapsed);

    public readonly bool IsFinished
    {
        get
        {
            if (Tween.Iterations <= 0) return false;
            if (Tween.Duration <= 0f)  return true;
            float t     = (Elapsed - Tween.Delay) * Tween.SpeedScale;
            float cycle = Tween.PingPong ? 2f * Tween.Duration : Tween.Duration;
            return t >= cycle * Tween.Iterations;
        }
    }
}
