using Sandbox;

namespace Grains.RazorDesigner.Canvas;

public enum ArtboardFillMode
{
	Dark,
	Mid,
	Light,
	Checker,
	Custom,
}

public static class ArtboardFillModeExtensions
{
	public static Color PresetColor( this ArtboardFillMode mode ) => mode switch
	{
		ArtboardFillMode.Dark    => new Color( 0.137f, 0.165f, 0.200f ),  // ~#232a33 — current default
		ArtboardFillMode.Mid     => new Color( 0.353f, 0.373f, 0.392f ),  // ~#5a5f64
		ArtboardFillMode.Light   => new Color( 0.910f, 0.910f, 0.910f ),  // ~#e8e8e8 (off-white)
		ArtboardFillMode.Checker => new Color( 0.165f, 0.176f, 0.200f ),  // ~#2a2d33 — checker base
		_                        => new Color( 0.137f, 0.165f, 0.200f ),  // Custom falls back to Dark if no color set
	};

	public static string DisplayLabel( this ArtboardFillMode mode ) => mode switch
	{
		ArtboardFillMode.Dark    => "Dark",
		ArtboardFillMode.Mid     => "Mid",
		ArtboardFillMode.Light   => "Light",
		ArtboardFillMode.Checker => "Checker",
		ArtboardFillMode.Custom  => "Custom",
		_                        => mode.ToString(),
	};
}
