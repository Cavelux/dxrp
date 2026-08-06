using Goo.Animation;

namespace Goo.Authoring;

/// <summary>Serializable easing choices for hierarchy-authored layout transitions.</summary>
public enum GooLayoutEasing
{
	Linear,
	Ease,
	EaseIn,
	EaseOut,
	EaseInOut,
	ExpoIn,
	ExpoOut,
	ExpoInOut,
	BounceIn,
	BounceOut,
	BounceInOut,
	SineIn,
	SineOut,
	SineInOut,
	StepStart,
	StepEnd,
}

internal static class GooLayoutEasingExtensions
{
	internal static Sandbox.Utility.Easing.Function Resolve( this GooLayoutEasing easing ) => easing switch
	{
		GooLayoutEasing.Linear => Easing.Linear,
		GooLayoutEasing.Ease => Easing.Ease,
		GooLayoutEasing.EaseIn => Easing.EaseIn,
		GooLayoutEasing.EaseInOut => Easing.EaseInOut,
		GooLayoutEasing.ExpoIn => Easing.ExpoIn,
		GooLayoutEasing.ExpoOut => Easing.ExpoOut,
		GooLayoutEasing.ExpoInOut => Easing.ExpoInOut,
		GooLayoutEasing.BounceIn => Easing.BounceIn,
		GooLayoutEasing.BounceOut => Easing.BounceOut,
		GooLayoutEasing.BounceInOut => Easing.BounceInOut,
		GooLayoutEasing.SineIn => Easing.SineIn,
		GooLayoutEasing.SineOut => Easing.SineOut,
		GooLayoutEasing.SineInOut => Easing.SineInOut,
		GooLayoutEasing.StepStart => Easing.StepStart,
		GooLayoutEasing.StepEnd => Easing.StepEnd,
		_ => Easing.EaseOut,
	};
}
