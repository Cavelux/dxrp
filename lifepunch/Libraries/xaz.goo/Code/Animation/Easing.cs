namespace Goo.Animation;

public static class Easing
{
    public static readonly Sandbox.Utility.Easing.Function Linear      = Sandbox.Utility.Easing.Linear;
    // CSS "ease" is cubic-bezier(0.25,0.1,0.25,1.0); the engine exposes it via name lookup only.
    public static readonly Sandbox.Utility.Easing.Function Ease        = Sandbox.Utility.Easing.GetFunction("ease");
    public static readonly Sandbox.Utility.Easing.Function EaseIn      = Sandbox.Utility.Easing.EaseIn;
    public static readonly Sandbox.Utility.Easing.Function EaseOut     = Sandbox.Utility.Easing.EaseOut;
    public static readonly Sandbox.Utility.Easing.Function EaseInOut   = Sandbox.Utility.Easing.EaseInOut;
    public static readonly Sandbox.Utility.Easing.Function ExpoIn      = Sandbox.Utility.Easing.ExpoIn;
    public static readonly Sandbox.Utility.Easing.Function ExpoOut     = Sandbox.Utility.Easing.ExpoOut;
    public static readonly Sandbox.Utility.Easing.Function ExpoInOut   = Sandbox.Utility.Easing.ExpoInOut;
    public static readonly Sandbox.Utility.Easing.Function BounceIn    = Sandbox.Utility.Easing.BounceIn;
    public static readonly Sandbox.Utility.Easing.Function BounceOut   = Sandbox.Utility.Easing.BounceOut;
    public static readonly Sandbox.Utility.Easing.Function BounceInOut = Sandbox.Utility.Easing.BounceInOut;
    public static readonly Sandbox.Utility.Easing.Function SineIn      = Sandbox.Utility.Easing.SineEaseIn;
    public static readonly Sandbox.Utility.Easing.Function SineOut     = Sandbox.Utility.Easing.SineEaseOut;
    public static readonly Sandbox.Utility.Easing.Function SineInOut   = Sandbox.Utility.Easing.SineEaseInOut;
    public static readonly Sandbox.Utility.Easing.Function StepStart   = Sandbox.Utility.Easing.StepStart;
    public static readonly Sandbox.Utility.Easing.Function StepEnd     = Sandbox.Utility.Easing.StepEnd;

    public static Sandbox.Utility.Easing.Function? FromName(string name) =>
        Sandbox.Utility.Easing.TryGetFunction(name, out var fn) ? fn : null;

    public static Sandbox.Utility.Easing.Function Steps(int count, bool atStart) =>
        Sandbox.Utility.Easing.Steps(count, atStart);

    public static Sandbox.Utility.Easing.Function CubicBezier(float x1, float y1, float x2, float y2) =>
        Sandbox.Utility.Easing.CubicBezier(x1, y1, x2, y2);

    // Bridge for editor-authored curves: a [Property] Curve gets the X/Y graph widget for free.
    public static Sandbox.Utility.Easing.Function FromCurve(Sandbox.Curve curve) =>
        t => curve.Evaluate(t);
}
