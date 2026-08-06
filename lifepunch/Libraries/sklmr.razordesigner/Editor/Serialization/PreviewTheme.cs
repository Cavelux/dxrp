using System;
using System.IO;
using System.Linq;
using Editor;
using Sandbox;

namespace Grains.RazorDesigner.Serialization;

public sealed class PreviewTheme
{
	private const string LogPrefix = "[Grains.RazorDesigner]";

	private const string DefaultThemeFilename = "preview-default.scss";
	private static readonly (string Ident, string Subpath)[] DefaultThemeRoots = new[]
	{
		( "grains_razordesigner", "Assets/RazorDesigner" ),
		( "grains_razordesigner", "Libraries/xaz.razordesigner/Assets/RazorDesigner" ),
		( "razordesigner",        "Assets/RazorDesigner" ),
	};

	public string Name { get; }
	public string Source { get; }   // "default" or absolute file path
	public string Css { get; }

	private PreviewTheme( string name, string source, string css )
	{
		Name = name;
		Source = source;
		Css = css;
	}

	public bool IsDefault => Source == "default";

	private static readonly Lazy<PreviewTheme> _default = new( BuildDefault );
	public static PreviewTheme Default => _default.Value;

	private static PreviewTheme BuildDefault()
	{
		var path = ResolveDefaultThemePath();
		if ( !string.IsNullOrEmpty( path ) )
		{
			try
			{
				var css = File.ReadAllText( path );
				Log.Info( $"{LogPrefix} PreviewTheme.Default loaded from {path} ({css.Length} chars)" );
				return new PreviewTheme( "Default", "default", css );
			}
			catch ( Exception ex )
			{
				Log.Warning( $"{LogPrefix} PreviewTheme.Default failed to read '{path}': {ex.Message}; using baked fallback" );
			}
		}
		else
		{
			Log.Info( $"{LogPrefix} PreviewTheme.Default: no '{DefaultThemeFilename}' asset found in any DefaultThemeRoots; using baked fallback" );
		}
		return new PreviewTheme( "Default", "default", BakedFallbackCss );
	}

	private static string ResolveDefaultThemePath()
	{
		foreach ( var (ident, subpath) in DefaultThemeRoots )
		{
			var root = EditorUtility.Projects.GetAll()
				.FirstOrDefault( p => string.Equals( p.Config?.Ident, ident, StringComparison.OrdinalIgnoreCase ) )
				?.GetRootPath();
			if ( string.IsNullOrEmpty( root ) ) continue;
			var candidate = Path.Combine( root, subpath, DefaultThemeFilename );
			if ( File.Exists( candidate ) ) return candidate;
		}
		return null;
	}

	// On read failure returns Default and logs. Caller handles missing-file gracefully on cookie restore.
	public static PreviewTheme FromFile( string path )
	{
		try
		{
			var css = File.ReadAllText( path );
			Log.Info( $"{LogPrefix} PreviewTheme.FromFile: {path} ({css.Length} chars)" );
			return new PreviewTheme( Path.GetFileName( path ), path, css );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"{LogPrefix} PreviewTheme.FromFile failed for '{path}': {ex.Message}; falling back to Default" );
			return Default;
		}
	}

	private const string BakedFallbackCss =
		".root { color: #fafaff; font-family: Inter; }\n" +
		".button { " +
			"background-image: linear-gradient(to bottom, #37425D 0%, #283043 100%); " +
			"border: 1px solid rgba(194, 201, 219, 0.1); " +
			"border-radius: 8px; " +
			"padding: 6px 16px; " +
			"color: #fafaff; " +
			"align-items: center; " +
			"justify-content: center; " +
			"min-height: 28px; " +
			"font-weight: 500; " +
			// sbox CSS box-shadow: single layer only, 'inset' is a trailing keyword.
			"box-shadow: 0 1px 2px rgba(0, 0, 0, 0.4); " +
		"}\n" +
		".button:hover { " +
			"background-image: linear-gradient(to bottom, #465477 0%, #37425D 100%); " +
			"border-color: rgba(63, 169, 245, 1); " +
			"box-shadow: 0 2px 4px rgba(0, 0, 0, 0.5); " +
		"}\n" +
		// TextEntry (and NumberEntry which inherits the .textentry class) — recessed dark.
		".textentry { " +
			"background-color: #11141D; " +
			"border: 1px solid rgba(194, 201, 219, 0.1); " +
			"border-radius: 4px; " +
			"padding: 4px 10px; " +
			"color: #fafaff; " +
			"min-height: 26px; " +
			"align-items: center; " +
			"box-shadow: 0 1px 2px rgba(0, 0, 0, 0.5) inset; " +
		"}\n" +
		".textentry .placeholder { color: #687AA6; }\n" +
		// Checkbox — square checkmark + accent fill when checked.
		".checkbox { " +
			"flex-direction: row; " +
			"align-items: center; " +
			"color: #fafaff; " +
			"cursor: pointer; " +
		"}\n" +
		".checkbox > .checkmark { " +
			"width: 16px; " +
			"height: 16px; " +
			"border: 1px solid rgba(194, 201, 219, 0.2); " +
			"border-radius: 4px; " +
			"margin-right: 6px; " +
			"color: transparent; " +
			"align-items: center; " +
			"justify-content: center; " +
			"font-size: 12px; " +
			"background-color: #11141D; " +
			"box-shadow: 0 1px 1px rgba(0, 0, 0, 0.4) inset; " +
		"}\n" +
		".checkbox.is-checked > .checkmark { " +
			"color: white; " +
			"background-image: linear-gradient(to bottom, #5BB8F8 0%, #3FA9F5 100%); " +
			"border-color: #2E8FD9; " +
			"box-shadow: 0 1px 2px rgba(63, 169, 245, 0.25); " +
		"}\n" +
		// IconPanel — Material Icons font (the [Library('icon')] alias 'i' too).
		".iconpanel, i { " +
			"font-family: Material Icons; " +
			"font-size: 18px; " +
			"color: #fafaff; " +
			"align-items: center; " +
			"justify-content: center; " +
		"}\n" +
		".preview-panel { " +
			"border: 1px solid rgba(194, 201, 219, 0.1); " +
			"background-color: rgba(40, 48, 67, 0.3); " +
			"min-height: 28px; " +
		"}\n" +
		// ButtonGroup — minimal container slot.
		".preview-buttongroup { " +
			"background-color: rgba(40, 48, 67, 0.4); " +
			"border: 1px solid rgba(194, 201, 219, 0.1); " +
			"border-radius: 8px; " +
			"min-height: 32px; " +
		"}\n" +
		// DropDown — raised button surface + .inner caret zone (right-anchored).
		".preview-dropdown { " +
			"background-image: linear-gradient(to bottom, #37425D 0%, #283043 100%); " +
			"background-color: rgba(0, 0, 0, 0); " +
			"border: 1px solid rgba(194, 201, 219, 0.1); " +
			"border-radius: 8px; " +
			"color: #fafaff; " +
			"min-height: 32px; " +
			"position: relative; " +
			"box-shadow: 0 1px 2px rgba(0, 0, 0, 0.4); " +
		"}\n" +
		".preview-dropdown > .inner { " +
			"position: absolute; " +
			"top: 0; right: 0; bottom: 0; " +
			"width: 32px; " +
			"background-color: rgba(17, 20, 29, 0.4); " +
			"border-left: 1px solid rgba(194, 201, 219, 0.1); " +
			"align-items: center; " +
			"justify-content: center; " +
		"}\n" +
		".form { " +
			"background-image: linear-gradient(to bottom, #283043 0%, #1F2535 100%); " +
			"background-color: rgba(0, 0, 0, 0); " +
			"border: 1px solid rgba(194, 201, 219, 0.1); " +
			"border-radius: 6px; " +
			"min-height: 60px; " +
			"padding: 8px; " +
			"box-shadow: 0 1px 2px rgba(0, 0, 0, 0.3); " +
		"}\n" +
		".field { " +
			"background-color: rgba(0, 0, 0, 0); " +
			"border: none; " +
			"border-bottom: 1px solid rgba(194, 201, 219, 0.06); " +
			"min-height: 28px; " +
			"padding: 4px 0; " +
		"}\n" +
		".field-control { " +
			"background-color: #191D2A; " +
			"border: 1px solid rgba(194, 201, 219, 0.08); " +
			"border-radius: 4px; " +
			"min-height: 28px; " +
			"flex-grow: 1; " +
			"box-shadow: 0 1px 2px rgba(0, 0, 0, 0.3) inset; " +
		"}\n";
}
