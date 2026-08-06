namespace Grains.RazorDesigner.Canvas;

public enum PreviewTargetMode
{
	None,
	ScreenPanel,
	WorldPanel,
}

public static class PreviewTargetModeExtensions
{
	public static bool IsPinned( this PreviewTargetMode mode ) => mode != PreviewTargetMode.None;

	public static float PinnedScale( this PreviewTargetMode mode ) => mode switch
	{
		PreviewTargetMode.ScreenPanel => 1.0f,
		PreviewTargetMode.WorldPanel  => 2.0f,
		_ => 0f,
	};

	public static string DisplayLabel( this PreviewTargetMode mode ) => mode switch
	{
		PreviewTargetMode.ScreenPanel => "ScreenPanel  (×1.0)",
		PreviewTargetMode.WorldPanel  => "WorldPanel  (×2.0)",
		_ => "None  (fit-to-widget)",
	};
}
