namespace Goo;

/// <summary>Declared transition for layout position moves: when this blob's layout slot changes, it glides there instead of snapping. Null easing resolves to EaseOut.</summary>
public readonly record struct LayoutTransition(float Ms, Sandbox.Utility.Easing.Function? Easing = null);
