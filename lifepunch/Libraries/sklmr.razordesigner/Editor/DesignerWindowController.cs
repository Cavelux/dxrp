using System;
using System.Collections.Generic;
using System.Linq;
using Editor;
using Grains.RazorDesigner.Canvas;
using Grains.RazorDesigner.Contracts;
using Grains.RazorDesigner.Diagnostics;
using Grains.RazorDesigner.Document;
using Grains.RazorDesigner.Hierarchy;
using Grains.RazorDesigner.Inspector;
using Grains.RazorDesigner.Palette;
using Grains.RazorDesigner.Selection;
using Grains.RazorDesigner.Templates;
using Sandbox;

namespace Grains.RazorDesigner;

public sealed class DesignerWindowController
{
	private const string LogPrefix = "[Grains.RazorDesigner]";

	private readonly DesignerDocument _document;
	private readonly LiveTreeMirror _mirror;
	private readonly SelectionController _selection;
	private readonly OverlayController _overlay;
	private readonly HierarchyPanel _hierarchy;
	private readonly InspectorPanel _inspector;
	private readonly PalettePanel _palette;
	private readonly Func<float> _getCanvasDpiScale;
	private readonly Action _applyPreviewStylesheet;
	private readonly Widget _ownerWidget;
	private readonly Action _onValidated;

	public DesignerWindowController(
		DesignerDocument document,
		LiveTreeMirror mirror,
		SelectionController selection,
		OverlayController overlay,
		HierarchyPanel hierarchy,
		InspectorPanel inspector,
		PalettePanel palette,
		Func<float> getCanvasDpiScale,
		Action applyPreviewStylesheet,
		Widget ownerWidget,
		Action onValidated = null )
	{
		_document = document;
		_mirror = mirror;
		_selection = selection;
		_overlay = overlay;
		_hierarchy = hierarchy;
		_inspector = inspector;
		_palette = palette;
		_getCanvasDpiScale = getCanvasDpiScale;
		_applyPreviewStylesheet = applyPreviewStylesheet;
		_ownerWidget = ownerWidget;
		_onValidated = onValidated;
		Log.Info( $"{LogPrefix} DesignerWindowController ctor" );
	}

	public CanvasManipulator Manipulator { get; set; }

	public void ApplyWiringChange( Grains.RazorDesigner.Wiring.WiringEnvelope env )
	{
		if ( env is null ) return;
		_document.Wiring = env;
		Log.Info( $"{LogPrefix} ApplyWiringChange: symbols={env.Symbols.Count}, " +
				  $"namespace='{env.Namespace}', class='{env.ClassName}', base='{env.BaseClass}'" );
		_onValidated?.Invoke();
	}

	public void RepopulateMirrorAndRefresh()
	{
		if ( !_mirror.Repopulate() ) return;
		// Repopulate wiped Root's children — re-create the selection overlay under the new tree.
		_overlay?.Reattach( _mirror.CurrentRoot );
		_selection?.Deselect();
		_inspector?.SetTarget( null );
		_applyPreviewStylesheet();
		RefreshHierarchy();
		_onValidated?.Invoke();
	}

	public void RefreshHierarchy()
	{
		if ( _hierarchy is null || _document is null ) return;
		_hierarchy.SetRoot( _document.RootRecord );
		_hierarchy.Highlight( _selection?.Selected );
	}

	// RepopulateMirror clears selection; re-establish focus.
	private void RepopulateAndFocus( ControlRecord record )
	{
		RepopulateMirrorAndRefresh();
		if ( record is null ) return;
		_selection.Select( record );
		_inspector.SetTarget( record );
		_hierarchy.Highlight( record );
	}

	// ─── Palette ───────────────────────────────────────────────────────────

	public void OnPaletteTypeAddRequested( ControlType type )
	{
		// Click-to-add: append under the active selection if it's a container, else under root.
		var sel = _selection?.Selected;
		ControlRecord parent = null;
		if ( sel is not null && ContractScanner.Table.Get( sel.Type ).IsContainer )
			parent = sel;
		Log.Info( $"{LogPrefix} Palette click-to-add {type} under {parent?.ClassName ?? "<root>"}" );
		OnHierarchyAddRequested( type, parent );
	}

	public void OnPaletteTemplateAddRequested( PaletteTemplate template )
	{
		// Click-to-add: append under the active selection if it's a container, else under root.
		var sel = _selection?.Selected;
		ControlRecord parent = null;
		if ( sel is not null && ContractScanner.Table.Get( sel.Type ).IsContainer )
			parent = sel;
		Log.Info( $"{LogPrefix} Palette click-to-add template \"{template.Name}\" under {parent?.ClassName ?? "<root>"}" );
		InsertTemplate( template, parent ?? _document.RootRecord );
	}

	// ─── Canvas ────────────────────────────────────────────────────────────

	private static Vector2 ToLogicalPx( Vector2 widgetPx ) => widgetPx;

	public void OnCanvasClicked( Vector2 widgetPx, bool isRightClick, Sandbox.KeyboardModifiers mods )
	{
		if ( (mods & Sandbox.KeyboardModifiers.Alt) != 0 && !isRightClick )
		{
			var deepest = _selection.FindDeepestAt( widgetPx.x, widgetPx.y, _getCanvasDpiScale() );
			if ( deepest is null ) return;
			var parent = _document.FindParent( deepest );
			var target = (parent is null || parent == _document.RootRecord) ? deepest : parent;
			_selection.Select( target );
			_inspector.SetTarget( target );
			_hierarchy.Highlight( target );
			Log.Info( $"{LogPrefix} alt-walk: deepest={deepest.ClassName} parent={parent?.ClassName ?? "<root>"} → select {target.ClassName}" );
			return;
		}

		if ( Manipulator is not null && Manipulator.OnPress( widgetPx, isRightClick ) )
		{
			_overlay?.SetHoverTarget( null ); // drag started → clear hover the same frame
			return;
		}

		if ( isRightClick )
		{
			OpenCanvasContextMenu( widgetPx );
			return;
		}

		var logical = ToLogicalPx( widgetPx );
		_selection.TrySelectAt( logical.x, logical.y, _getCanvasDpiScale() );
		_inspector.SetTarget( _selection.Selected );
		_hierarchy.Highlight( _selection.Selected );
	}

	private void OpenCanvasContextMenu( Vector2 widgetPx )
	{
		var logical = ToLogicalPx( widgetPx );
		var dpi = _getCanvasDpiScale();
		var hit = _selection?.FindDeepestAt( logical.x, logical.y, dpi );

		var multi = _hierarchy?.SelectedRecords;
		IReadOnlyList<ControlRecord> targets;
		ControlRecord menuHit;
		bool isSlot = false;

		if ( hit is null || hit == _document.RootRecord )
		{
			menuHit = null;
			targets = System.Array.Empty<ControlRecord>();
		}
		else if ( hit.IsSlot )
		{
			menuHit = hit;
			targets = new[] { hit };
			isSlot = true;
		}
		else if ( multi is { Count: > 1 } && multi.Contains( hit ) )
		{
			menuHit = hit;
			targets = multi;
		}
		else
		{
			_selection.Select( hit );
			_inspector.SetTarget( hit );
			_hierarchy.Highlight( hit );
			menuHit = hit;
			targets = new[] { hit };
		}

		var hasClipboard = _document?.Clipboard is not null;

		CanvasContextMenu.Open(
			parent: _ownerWidget,
			hit: menuHit,
			targets: targets,
			hasClipboard: hasClipboard,
			isSlot: isSlot,
			pasteRootTarget: _document.RootRecord,
			callbacks: new CanvasContextMenu.Callbacks
			{
				Cut = OnHierarchyCutRequested,
				Copy = OnHierarchyCopyRequested,
				Paste = OnHierarchyPasteRequested,
				Delete = OnHierarchyDeleteRequested,
				SaveAsTemplate = OnHierarchySaveAsTemplateRequested,
				Duplicate = DuplicateRecords,
				CustomStyles = OpenCustomStylesDialog,
			} );
	}

	public void OnCanvasMoved( Vector2 widgetPx, Sandbox.KeyboardModifiers mods )
	{
		Manipulator?.OnMove( widgetPx, mods );
		if ( Manipulator is { IsDragging: true } ) return;
		var hover = _selection.FindDeepestAt( widgetPx.x, widgetPx.y, _getCanvasDpiScale() );
		_overlay?.SetHoverTarget( hover );
	}

	public void OnCanvasHoverEnded()
	{
		_overlay?.SetHoverTarget( null );
	}

	public void OnCanvasReleased( Vector2 widgetPx ) => Manipulator?.OnRelease( widgetPx );

	public void CommitCanvasManipulation( ControlRecord record )
	{
		if ( record is null ) return;
		_applyPreviewStylesheet();
		_onValidated?.Invoke();
		if ( _selection?.Selected == record )
			_inspector?.SetTarget( record );
		_hierarchy?.NodeChanged( record );
	}

	public bool NudgeSelected( float dx, float dy, float dw, float dh )
	{
		if ( Manipulator is { IsDragging: true } ) return false;
		var rec = _selection?.Selected;
		if ( rec is null || rec == _document.RootRecord ) return false;

		const float MinSize = 4f; // matches CanvasManipulator.OnMove's MinSize
		bool changed = false;

		if ( dw != 0f )
		{
			if ( rec.Width.Unit == LengthUnit.Px )
			{
				rec.Width = Length.Px( System.MathF.Max( MinSize, rec.Width.Value + dw ) );
				changed = true;
			}
			else Log.Info( $"{LogPrefix} NudgeSelected: {rec.ClassName} Width is not a px size; keyboard-resize ignored (set a px size via the inspector or a drag-resize first)" );
		}
		if ( dh != 0f )
		{
			if ( rec.Height.Unit == LengthUnit.Px )
			{
				rec.Height = Length.Px( System.MathF.Max( MinSize, rec.Height.Value + dh ) );
				changed = true;
			}
			else Log.Info( $"{LogPrefix} NudgeSelected: {rec.ClassName} Height is not a px size; keyboard-resize ignored (set a px size via the inspector or a drag-resize first)" );
		}

		if ( dx != 0f || dy != 0f )
		{
			if ( rec.Position == PositionKind.Absolute )
			{
				if ( dx != 0f ) { rec.Left = Length.Px( (rec.Left.Unit == LengthUnit.Px ? rec.Left.Value : 0f) + dx ); changed = true; }
				if ( dy != 0f ) { rec.Top  = Length.Px( (rec.Top.Unit  == LengthUnit.Px ? rec.Top.Value  : 0f) + dy ); changed = true; }
			}
			else Log.Info( $"{LogPrefix} NudgeSelected: {rec.ClassName} is not Position=Absolute; arrow-move ignored (drag it once to place it)" );
		}

		if ( !changed ) return false;
		Log.Info( $"{LogPrefix} NudgeSelected: {rec.ClassName} dx={dx} dy={dy} dw={dw} dh={dh}" );
		CommitCanvasManipulation( rec );
		return true;
	}

	public void OnCanvasRecordDropped( ControlType type, Vector2 widgetPx )
	{
		Log.Info( $"{LogPrefix} OnCanvasRecordDropped: {type} at widget ({widgetPx.x:F0}, {widgetPx.y:F0})" );
		var logical = ToLogicalPx( widgetPx );
		var dpi = _getCanvasDpiScale();
		var fbPos = new Vector2( logical.x * dpi, logical.y * dpi );
		var parent = _document.FindDeepestContainerAt( fbPos );
		var record = _document.Add( type, parent );
		if ( !_mirror.MirrorInserted( record, parent ) )
		{
			RepopulateAndFocus( record );
			return;
		}
		_mirror.UpdateChromeLabel( parent );
		_applyPreviewStylesheet();
		_selection.Select( record );
		_inspector.SetTarget( record );
		RefreshHierarchy();
	}

	public void OnCanvasTemplateDropped( PaletteTemplate template, Vector2 widgetPx )
	{
		Log.Info( $"{LogPrefix} OnCanvasTemplateDropped: \"{template.Name}\" at widget ({widgetPx.x:F0}, {widgetPx.y:F0})" );
		var logical = ToLogicalPx( widgetPx );
		var dpi = _getCanvasDpiScale();
		var fbPos = new Vector2( logical.x * dpi, logical.y * dpi );
		var parent = _document.FindDeepestContainerAt( fbPos );
		InsertTemplate( template, parent );
	}

	// ─── Hierarchy ─────────────────────────────────────────────────────────

	public void OnHierarchyTemplateDropRequested( PaletteTemplate template, ControlRecord parent, int index )
	{
		if ( parent is null ) parent = _document.RootRecord;
		Log.Info( $"{LogPrefix} OnHierarchyTemplateDropRequested: \"{template.Name}\" -> {parent.ClassName}[{index}]" );

		var clones = _document.AddTemplate( template, parent );
		if ( clones.Count == 0 ) return;

		// AddTemplate appends; shift the contiguous block of clones to the requested slot.
		var firstAppendedAt = parent.Children.Count - clones.Count;
		if ( index < firstAppendedAt )
		{
			for ( int i = 0; i < clones.Count; i++ )
				_document.MoveTo( clones[i], parent, index + i );
		}

		var focus = clones[clones.Count - 1];
		foreach ( var clone in clones )
		{
			if ( !_mirror.MirrorInserted( clone, parent ) )
			{
				RepopulateAndFocus( focus );
				return;
			}
		}
		_applyPreviewStylesheet();
		_selection.Select( focus );
		_inspector.SetTarget( focus );
		_hierarchy.Highlight( focus );
		RefreshHierarchy();
	}

	public void OnHierarchySaveAsTemplateRequested( IReadOnlyList<ControlRecord> records )
	{
		if ( records is null || records.Count == 0 )
		{
			Log.Warning( $"{LogPrefix} OnHierarchySaveAsTemplateRequested: empty selection; ignored" );
			return;
		}

		var lca = ComputeLowestCommonAncestor( records );
		var dialog = new SaveTemplateDialog( _ownerWidget, _palette.TemplateStore, records, lca );
		dialog.Show( onConfirm: template =>
		{
			Log.Info( $"{LogPrefix} OnHierarchySaveAsTemplateRequested: saving \"{template.Name}\"" );
			_palette.TemplateStore.Save( template );
		} );
	}

	public void OnHierarchyRecordCreated( ControlRecord record )
	{
		Log.Info( $"{LogPrefix} OnHierarchyRecordCreated: {record.ClassName}" );
		var parent = _document.FindParent( record );
		if ( !_mirror.MirrorInserted( record, parent ) )
		{
			RepopulateAndFocus( record );
			return;
		}
		_applyPreviewStylesheet();
		_selection.Select( record );
		_inspector.SetTarget( record );
		_hierarchy.Highlight( record );
		RefreshHierarchy();
	}

	public void OnHierarchyRowClicked( ControlRecord record )
	{
		_selection.Select( record );
		_inspector.SetTarget( record );
		// Don't Highlight: row is already selected by the click; would feed back via ItemSelected.
	}

	public void OnHierarchySelectionChanged( IReadOnlyList<ControlRecord> records )
	{
		Log.Info( $"{LogPrefix} OnHierarchySelectionChanged: {records?.Count ?? 0} record(s)" );
	}

	public void OnRecordMoved( ControlRecord moved )
	{
		Log.Info( $"{LogPrefix} OnRecordMoved: {moved?.ClassName ?? "<null>"} (surgical)" );
		if ( moved is null ) { RepopulateMirrorAndRefresh(); return; }

		var newParent = _document.FindParent( moved );
		var newLiveParent = newParent?.LivePanel;
		var live = moved.LivePanel;

		if ( newParent is null || newLiveParent is null || !newLiveParent.IsValid
			|| live is null || !live.IsValid )
		{
			Log.Warning( $"{LogPrefix} OnRecordMoved: surgical re-parent unavailable; falling back to RepopulateMirror" );
			RepopulateMirrorAndRefresh();
			return;
		}

		live.Parent = newLiveParent;
		var docIndex = newParent.Children.IndexOf( moved );
		if ( docIndex >= 0 && docIndex < newParent.Children.Count - 1 )
			newLiveParent.SetChildIndex( live, docIndex );

		foreach ( var r in _document.WalkAll() )
			_mirror.UpdateChromeLabel( r );

		_applyPreviewStylesheet();
		RefreshHierarchy();
	}

	public void OnHierarchyCutRequested( IReadOnlyList<ControlRecord> records )
	{
		if ( records is null || records.Count == 0 ) return;
		var operable = new List<ControlRecord>( records.Count );
		foreach ( var r in records )
			if ( r is not null && r != _document.RootRecord ) operable.Add( r );
		if ( operable.Count == 0 ) return;

		// Stash references first; delete after so the records still exist when stored.
		_document.Clipboard = operable;
		Log.Info( $"{LogPrefix} Cut {operable.Count} record(s) -> clipboard" );
		foreach ( var r in operable )
			DeleteRecordCascade( r );
	}

	// Clone at copy time so subsequent edits to source don't bleed into pastes.
	public void OnHierarchyCopyRequested( IReadOnlyList<ControlRecord> records )
	{
		if ( records is null || records.Count == 0 ) return;
		var snapshots = new List<ControlRecord>( records.Count );
		foreach ( var r in records )
		{
			if ( r is null || r == _document.RootRecord ) continue;
			snapshots.Add( _document.Clone( r ) );
		}
		if ( snapshots.Count == 0 ) return;
		_document.Clipboard = snapshots;
		Log.Info( $"{LogPrefix} Copy {snapshots.Count} record(s) -> clipboard" );
	}

	public void OnHierarchyPasteRequested( ControlRecord target )
	{
		if ( _document.Clipboard is null || _document.Clipboard.Count == 0 || target is null ) return;

		ControlRecord parent;
		int index;
		if ( ContractScanner.Table.Get( target.Type ).IsContainer )
		{
			parent = target;
			index = target.Children.Count;
		}
		else
		{
			parent = _document.FindParent( target ) ?? _document.RootRecord;
			index = parent.Children.IndexOf( target ) + 1;
		}

		var pastedRoots = new List<ControlRecord>( _document.Clipboard.Count );
		foreach ( var entry in _document.Clipboard )
		{
			if ( entry is null ) continue;
			var pasted = _document.Clone( entry );
			parent.Children.Insert( index, pasted );
			Log.Info( $"{LogPrefix} Paste {pasted.ClassName} into {parent.ClassName}[{index}]" );
			index++;
			pastedRoots.Add( pasted );
		}
		if ( pastedRoots.Count == 0 ) return;

		var lastPasted = pastedRoots[pastedRoots.Count - 1];
		foreach ( var pasted in pastedRoots )
		{
			if ( !_mirror.MirrorInserted( pasted, parent ) )
			{
				RepopulateAndFocus( lastPasted );
				return;
			}
		}
		_applyPreviewStylesheet();
		_selection.Select( lastPasted );
		_inspector.SetTarget( lastPasted );
		_hierarchy.Highlight( lastPasted );
		RefreshHierarchy();
	}

	public void DuplicateRecords( IReadOnlyList<ControlRecord> records )
	{
		if ( records is null || records.Count == 0 ) return;

		Log.Info( $"{LogPrefix} DuplicateRequested: {records.Count} record(s)" );

		var duplicatedRoots = new List<ControlRecord>();

		foreach ( var target in records )
		{
			if ( target is null || target == _document.RootRecord ) continue;

			var parent = _document.FindParent( target ) ?? _document.RootRecord;
			
			int insertIndex = parent.Children.IndexOf( target ) + 1;

			var duplicate = _document.Clone( target );

			parent.Children.Insert( insertIndex, duplicate );
			Log.Info( $"{LogPrefix} Duplicate {duplicate.ClassName} into {parent.ClassName}[{insertIndex}]" );
			
			duplicatedRoots.Add( duplicate );
		}

		if ( duplicatedRoots.Count == 0 ) return;

		var lastDuplicated = duplicatedRoots[duplicatedRoots.Count - 1];
		foreach ( var duplicated in duplicatedRoots )
		{
			var parent = _document.FindParent( duplicated ) ?? _document.RootRecord;
			if ( !_mirror.MirrorInserted( duplicated, parent ) )
			{
				RepopulateAndFocus( lastDuplicated );
				return;
			}
		}

		_applyPreviewStylesheet();
		
		_selection.Select( lastDuplicated );
		_inspector.SetTarget( lastDuplicated );
		_hierarchy.Highlight( lastDuplicated );
		RefreshHierarchy();
	}

	public void OpenCustomStylesDialog( ControlRecord target )
	{
		if ( target is null || target == _document.RootRecord ) return;
	
		var dialog = new Grains.RazorDesigner.CustomStylesDialog.CustomStylesDialog( _ownerWidget, target, () =>
		{
			OnInspectorValueChanged();
		} );
		dialog.Show();
	}

	public void OnHierarchyRenameRequested( ControlRecord record, string newName )
	{
		if ( record is null ) return;
		newName = newName?.Trim() ?? "";
		if ( newName == record.ClassName ) return;

		var error = _document.ValidateClassName( newName, record );
		if ( error is not null )
		{
			Log.Warning( $"{LogPrefix} Hierarchy rename '{newName}' rejected: {error}" );
			return;
		}

		var oldName = record.ClassName;
		record.ClassName = newName;
		Log.Info( $"{LogPrefix} Hierarchy rename '{oldName}' -> '{newName}'" );

		var live = record.LivePanel;
		if ( live is not null && live.IsValid )
		{
			if ( !string.IsNullOrEmpty( oldName ) )
				live.RemoveClass( oldName );
			if ( !string.IsNullOrEmpty( newName ) )
				live.AddClass( newName );
		}

		if ( ContractScanner.Table.Get( record.Type ).IsContainer )
			_mirror.RefreshChromeLabelText( record );

		_applyPreviewStylesheet();
		_hierarchy.NodeChanged( record );

		// Inspector caches _priorClassName + binds a LineEdit to the property; re-target syncs both.
		if ( _selection.Selected == record )
			_inspector.SetTarget( record );
	}

	public void OnHierarchyAddRequested( ControlType type, ControlRecord parent )
	{
		var actualParent = parent ?? _document.RootRecord;
		Log.Info( $"{LogPrefix} OnHierarchyAddRequested: {type} under {actualParent.ClassName}" );

		var record = _document.Add( type, actualParent );
		if ( !_mirror.MirrorInserted( record, actualParent ) )
		{
			RepopulateAndFocus( record );
			return;
		}

		_mirror.UpdateChromeLabel( actualParent );
		_applyPreviewStylesheet();
		_selection.Select( record );
		_inspector.SetTarget( record );
		RefreshHierarchy();
	}

	public void OnHierarchyDeleteRequested( IReadOnlyList<ControlRecord> records )
	{
		if ( records is null || records.Count == 0 ) return;
		Log.Info( $"{LogPrefix} OnHierarchyDeleteRequested: {records.Count} record(s)" );
		foreach ( var r in records )
		{
			if ( r is null || r == _document.RootRecord ) continue;
			DeleteRecordCascade( r );
		}
	}

	public void DeleteRecordCascade( ControlRecord record )
	{
		var parent = _document.FindParent( record );
		_selection.Select( record );
		_selection.DeleteSelected();
		_mirror.UpdateChromeLabel( parent );
		_inspector.SetTarget( null );
		_applyPreviewStylesheet();
		RefreshHierarchy();
	}

	// ─── Inspector ─────────────────────────────────────────────────────────

	public void OnInspectorClassNameChanged( ControlRecord record, string oldClassName )
	{
		if ( record is null ) return;

		Log.Info( $"{LogPrefix} OnInspectorClassNameChanged: '{oldClassName}' -> '{record.ClassName}' (surgical)" );

		var live = record.LivePanel;
		if ( live is not null && live.IsValid )
		{
			if ( !string.IsNullOrEmpty( oldClassName ) )
				live.RemoveClass( oldClassName );
			if ( !string.IsNullOrEmpty( record.ClassName ) )
				live.AddClass( record.ClassName );
		}

		if ( ContractScanner.Table.Get( record.Type ).IsContainer )
		{
			_mirror.RefreshChromeLabelText( record );
		}

		_applyPreviewStylesheet();
		_hierarchy?.NodeChanged( record );
	}

	public void OnInspectorValueChanged()
	{
		var probeSw = PerfProbes.Enabled ? System.Diagnostics.Stopwatch.StartNew() : null;

		var sel = _selection?.Selected;
		if ( sel is not null )
			_mirror.Reapply( sel );

		var probeReapplyMs = probeSw?.Elapsed.TotalMilliseconds ?? 0.0;
		probeSw?.Restart();

		_applyPreviewStylesheet();

		if ( probeSw != null )
		{
			var probeStyleMs = probeSw.Elapsed.TotalMilliseconds;
			Log.Info( $"{LogPrefix} OnInspectorValueChanged: reapply={probeReapplyMs:F2}ms style={probeStyleMs:F2}ms total={(probeReapplyMs + probeStyleMs):F2}ms" );
		}
	}

	// ─── Helpers ───────────────────────────────────────────────────────────

	// Common AddTemplate path used by click-to-add and canvas drop.
	private void InsertTemplate( PaletteTemplate template, ControlRecord parent )
	{
		var actualParent = parent ?? _document.RootRecord;
		var clones = _document.AddTemplate( template, actualParent );
		if ( clones.Count == 0 ) return;

		var focus = clones[clones.Count - 1];
		foreach ( var clone in clones )
		{
			if ( !_mirror.MirrorInserted( clone, actualParent ) )
			{
				RepopulateAndFocus( focus );
				return;
			}
		}
		_applyPreviewStylesheet();
		_selection.Select( focus );
		_inspector.SetTarget( focus );
		_hierarchy.Highlight( focus );
		RefreshHierarchy();
	}

	private ControlRecord ComputeLowestCommonAncestor( IReadOnlyList<ControlRecord> records )
	{
		if ( records is null || records.Count == 0 ) return null;
		if ( records.Count == 1 ) return null; // wrap meaningless for single root

		ControlRecord shared = _document.FindParent( records[0] );
		for ( int i = 1; i < records.Count; i++ )
		{
			var p = _document.FindParent( records[i] );
			if ( p != shared ) return null; // not all siblings -> wrap not meaningful
		}
		return shared;
	}
}
