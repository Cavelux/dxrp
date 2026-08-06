using System;
using Grains.RazorDesigner.Contracts;
using Grains.RazorDesigner.Document;
using Grains.RazorDesigner.Projection;
using Grains.RazorDesigner.Serialization;
using Sandbox.UI;

namespace Grains.RazorDesigner.Canvas;

public sealed class LiveTreeMirror
{
	private const string LogPrefix = "[Grains.RazorDesigner]";
	// Class on canvas root drives the chrome-hidden CSS branch in PreviewMarkerRules.
	internal const string ChromeHiddenClass = "chrome-hidden";

	private readonly DesignerDocument _document;
	private readonly Func<Panel> _getCanvasRoot;
	private readonly Func<bool> _getChromeHidden;

	/// <summary>The canvas Root panel the mirror last populated into (== RootRecord.LivePanel). Null before the first Repopulate.</summary>
	public Panel CurrentRoot => _document.RootRecord.LivePanel;

	public LiveTreeMirror(
		DesignerDocument document,
		Func<Panel> getCanvasRoot,
		Func<bool> getChromeHidden )
	{
		_document = document;
		_getCanvasRoot = getCanvasRoot;
		_getChromeHidden = getChromeHidden;
		Log.Info( $"{LogPrefix} LiveTreeMirror ctor" );
	}

	public bool Repopulate()
	{
		var root = _getCanvasRoot();
		if ( root is null || !root.IsValid )
		{
			Log.Warning( $"{LogPrefix} RepopulateMirror: root not valid; skipping {_document.RootRecord.Children.Count} root child(ren)" );
			return false;
		}

		// Perf probe (grd-pewf): full wipe+rebuild is an event path (load / hotreload / undo), log every time.
		var probeSw = System.Diagnostics.Stopwatch.StartNew();

		_document.RootRecord.LivePanel = root;
		root.AddClass( "root" );
		root.SetClass( ChromeHiddenClass, _getChromeHidden() );
		root.DeleteChildren( immediate: true );
		var probeWipeMs = probeSw.Elapsed.TotalMilliseconds;

		Log.Info( $"{LogPrefix} RepopulateMirror: wiped subtree, wired RootRecord.LivePanel; re-creating {_document.RootRecord.Children.Count} root child(ren)" );

		var ctx = new ProjectionContext( PreviewTheme.Default, ForPreview: true );
		foreach ( var r in _document.RootRecord.Children )
		{
			Applier.BuildPreview( new RecordNode( r ), root, ctx, OnNodeBuilt );
		}

		Log.Info( $"{LogPrefix} probe: Repopulate took {probeSw.Elapsed.TotalMilliseconds:F3}ms = wipe {probeWipeMs:F3}ms + build {probeSw.Elapsed.TotalMilliseconds - probeWipeMs:F3}ms ({_document.RootRecord.Children.Count} root child(ren))" );
		return true;
	}

	public void Reapply( ControlRecord record )
	{
		if ( record?.LivePanel is null || !record.LivePanel.IsValid ) return;
		var ctx = new ProjectionContext( PreviewTheme.Default, ForPreview: true );
		Applier.ReapplyContent( new RecordNode( record ), record.LivePanel, ctx );
	}

	public bool MirrorInserted( ControlRecord record, ControlRecord parent )
	{
		if ( record is null || parent is null ) return false;
		var liveParent = parent.LivePanel;
		if ( liveParent is null || !liveParent.IsValid ) return false;

		var ctx = new ProjectionContext( PreviewTheme.Default, ForPreview: true );
		Applier.BuildPreview( new RecordNode( record ), liveParent, ctx, OnNodeBuilt );

		var docIndex = parent.Children.IndexOf( record );
		if ( docIndex >= 0 && docIndex < parent.Children.Count - 1 && record.LivePanel is { IsValid: true } live )
			liveParent.SetChildIndex( live, docIndex );

		UpdateChromeLabel( parent );
		return true;
	}

	// Idempotent: empty non-root containers get exactly one .preview-chrome-label child.
	public void UpdateChromeLabel( ControlRecord record )
	{
		if ( record is null ) return;
		if ( record == _document.RootRecord ) return;
		if ( record.LivePanel is null || !record.LivePanel.IsValid ) return;
		if ( !ContractScanner.Table.Get( record.Type ).IsContainer ) return;

		Panel existing = null;
		foreach ( var child in record.LivePanel.Children )
		{
			if ( child.HasClass( "preview-chrome-label" ) ) { existing = child; break; }
		}

		var shouldShow = record.Children.Count == 0;

		if ( shouldShow && existing is null )
		{
			var label = record.LivePanel.AddChild<Sandbox.UI.Label>();
			label.Text = record.ClassName;
			label.AddClass( "preview-chrome-label" );
		}
		else if ( !shouldShow && existing is not null )
		{
			existing.Delete();
		}
	}

	// UpdateChromeLabel creates/removes; this updates the existing label's text after rename.
	public void RefreshChromeLabelText( ControlRecord record )
	{
		if ( record?.LivePanel is null || !record.LivePanel.IsValid ) return;
		foreach ( var child in record.LivePanel.Children )
		{
			if ( child.HasClass( "preview-chrome-label" ) && child is Sandbox.UI.Label lbl )
			{
				lbl.Text = record.ClassName;
				return;
			}
		}
	}

	public System.Collections.Generic.IEnumerable<Panel> TopLevelAuthoredPanels()
	{
		foreach ( var record in _document.RootRecord.Children )
		{
			var live = record?.LivePanel;
			if ( live is null || !live.IsValid ) continue;
			yield return live;
		}
	}

	private void OnNodeBuilt( IReadOnlyNode node )
	{
		if ( node is RecordNode rn )
			UpdateChromeLabel( rn.Backing );
	}

	public void SetPseudoClassForRecord( ControlRecord record, PseudoClass cls, bool on )
	{
		if ( record?.LivePanel is null ) return;

		record.LivePanel.Switch( cls, on );

		Log.Info( $"{LogPrefix} LiveTreeMirror.SetPseudoClassForRecord: record={record.ClassName} cls={cls} on={on}" );
	}
}
