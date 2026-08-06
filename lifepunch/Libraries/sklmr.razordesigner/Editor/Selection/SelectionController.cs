using System;
using Grains.RazorDesigner.Diagnostics;
using Grains.RazorDesigner.Document;
using Sandbox;

namespace Grains.RazorDesigner.Selection;

// Hit-test: last hit wins (child-over-parent on overlap). Selection visual is .selected CSS class.
public sealed class SelectionController
{
	private const string LogPrefix = "[Grains.RazorDesigner]";
	private const string SelectedClass = "selected";

	private readonly DesignerDocument _document;

	// Raised after Selected changes. Subscribers should read Selected directly.
	public event Action Changed;

	public ControlRecord Selected { get; private set; }

	public SelectionController( DesignerDocument document )
	{
		_document = document;
	}

	public ControlRecord FindDeepestAt( float widgetX, float widgetY, float dpiScale )
	{
		if ( dpiScale < 0.01f ) dpiScale = 1f;
		var fbPos = new Vector2( widgetX * dpiScale, widgetY * dpiScale );

		// Perf probe (grd-z82h): O(N) WalkAll + IsInside per click. Surfaces document size cost.
		var probeSw = PerfProbes.Enabled ? System.Diagnostics.Stopwatch.StartNew() : null;
		var probeScanned = 0;
		var probeHitTested = 0;

		ControlRecord deepest = null;
		foreach ( var r in _document.WalkAll() )
		{
			probeScanned++;
			if ( r.LivePanel is null || !r.LivePanel.IsValid ) continue;
			probeHitTested++;
			if ( r.LivePanel.IsInside( fbPos ) ) deepest = r;
		}

		if ( probeSw != null )
			Log.Info( $"{LogPrefix} SelectionController.FindDeepestAt: {probeSw.Elapsed.TotalMilliseconds:F2}ms (scanned={probeScanned}, hit-tested={probeHitTested}, deepest={(deepest?.ClassName ?? "<none>")})" );

		return deepest;
	}

	public bool TrySelectAt( float widgetX, float widgetY, float dpiScale )
	{
		if ( dpiScale < 0.01f ) dpiScale = 1f;
		var fbPos = new Vector2( widgetX * dpiScale, widgetY * dpiScale );
		var hit = FindDeepestAt( widgetX, widgetY, dpiScale );

		if ( hit is not null )
		{
			Log.Info( $"{LogPrefix} TrySelectAt widget=({widgetX:F0},{widgetY:F0}) fb=({fbPos.x:F0},{fbPos.y:F0}) hit {hit.ClassName} (rect={hit.LivePanel.Box.Rect})" );
			Select( hit );
			return true;
		}

		Log.Info( $"{LogPrefix} TrySelectAt widget=({widgetX:F0},{widgetY:F0}) fb=({fbPos.x:F0},{fbPos.y:F0}) miss" );
		Deselect();
		return false;
	}

	public void Select( ControlRecord record )
	{
		if ( Selected == record ) return;

		if ( Selected?.LivePanel is { IsValid: true } prev )
			prev.RemoveClass( SelectedClass );

		Selected = record;

		if ( Selected?.LivePanel is { IsValid: true } cur )
			cur.AddClass( SelectedClass );

		Log.Info( $"{LogPrefix} Select({Selected?.ClassName ?? "<none>"})" );
		Changed?.Invoke();
	}

	public void Deselect()
	{
		if ( Selected is null ) return;
		if ( Selected.LivePanel is { IsValid: true } prev )
			prev.RemoveClass( SelectedClass );
		Log.Info( $"{LogPrefix} Deselect (was {Selected.ClassName})" );
		Selected = null;
		Changed?.Invoke();
	}

	public void DeleteSelected()
	{
		if ( Selected is null ) return;
		if ( Selected == _document.RootRecord )
		{
			Log.Warning( $"{LogPrefix} Delete: RootRecord is not deletable; ignored" );
			return;
		}
		var rec = Selected;
		Selected = null;
		_document.Remove( rec );
		Log.Info( $"{LogPrefix} DeleteSelected: removed {rec.ClassName}" );
		Changed?.Invoke();
	}
}
