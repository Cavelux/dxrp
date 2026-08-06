using Sandbox;

namespace Goo.Animation;

/// <summary>Banked-offset glide for layout position transitions; pure value type, caller owns the clock.</summary>
public record struct GlideState
{
    Vector2 _banked;
    float _elapsed;
    Tween _tween;
    bool _active;

    const float SettleSq = 0.25f;

    /// <summary>Creates a glide with duration in milliseconds and optional easing (defaults to EaseOut).</summary>
    public GlideState(float ms, Sandbox.Utility.Easing.Function? easing = null)
    {
        _tween = new Tween(easing ?? Easing.EaseOut, ms / 1000f);
        _banked = Vector2.Zero;
        _elapsed = 0f;
        _active = false;
    }

    /// <summary>Current visual displacement from the layout slot; exactly zero when settled.</summary>
    public readonly Vector2 Offset => _active ? _banked * (1f - _tween.Eval(_elapsed)) : Vector2.Zero;

    /// <summary>True while the glide is still animating toward zero.</summary>
    public readonly bool IsActive => _active;

    /// <summary>Banks a layout jump; a mid-flight bank preserves the current visual offset and restarts the clock.</summary>
    public void Bank(Vector2 jump)
    {
        if (_tween.Duration <= 0f) return;
        var next = Offset + jump;
        _elapsed = 0f;
        if (next.LengthSquared < SettleSq) { _banked = Vector2.Zero; _active = false; return; }
        _banked = next;
        _active = true;
    }

    /// <summary>Steps the animation forward by dt seconds; settles when the offset drops below 0.5px.</summary>
    public void Advance(float dt)
    {
        if (!_active) return;
        _elapsed += dt;
        if (Offset.LengthSquared < SettleSq) { _banked = Vector2.Zero; _active = false; }
    }
}
