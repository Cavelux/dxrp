using System;
using System.Collections.Generic;
using Editor;
using Grains.RazorDesigner.Document;
using Sandbox;

namespace Grains.RazorDesigner.Canvas;

public static class CanvasContextMenu
{
	private const string LogPrefix = "[Grains.RazorDesigner]";

	public readonly struct Callbacks
	{
		public Action<IReadOnlyList<ControlRecord>> Cut { get; init; }
		public Action<IReadOnlyList<ControlRecord>> Copy { get; init; }
		public Action<ControlRecord> Paste { get; init; }
		public Action<IReadOnlyList<ControlRecord>> Delete { get; init; }
		public Action<IReadOnlyList<ControlRecord>> SaveAsTemplate { get; init; }
		
		public Action<IReadOnlyList<ControlRecord>> Duplicate { get; init; }

		public Action<ControlRecord> CustomStyles { get; init; }
	}

	// Returns false when the menu was suppressed (root + empty clipboard), true otherwise.
	public static bool Open(
		Widget parent,
		ControlRecord hit,
		IReadOnlyList<ControlRecord> targets,
		bool hasClipboard,
		bool isSlot,
		ControlRecord pasteRootTarget,
		Callbacks callbacks )
	{
		if ( parent is null ) return false;

		// Root / empty-canvas variant: Paste-only, suppressed when no clipboard.
		if ( hit is null )
		{
			if ( !hasClipboard ) return false;

			var rootMenu = new Menu( parent );
			rootMenu.AddOption( "Paste", "content_paste", () =>
			{
				Log.Info( $"{LogPrefix} CanvasContextMenu.Paste: into Canvas" );
				callbacks.Paste?.Invoke( pasteRootTarget );
			} );
			rootMenu.OpenAtCursor( false );
			return true;
		}

		// Slot variant: structural row — only Paste makes sense, gate on hasClipboard.
		if ( isSlot )
		{
			var slotMenu = new Menu( parent );
			var slotPaste = slotMenu.AddOption( "Paste", "content_paste", () =>
			{
				Log.Info( $"{LogPrefix} CanvasContextMenu.Paste: into slot {hit.ClassName}" );
				callbacks.Paste?.Invoke( hit );
			} );
			slotPaste.Enabled = hasClipboard;
			slotMenu.OpenAtCursor( false );
			return true;
		}

		if ( targets is null || targets.Count == 0 )
			targets = new[] { hit };

		var suffix = targets.Count > 1 ? $" ({targets.Count} items)" : "";

		var m = new Menu( parent );

		m.AddOption( $"Save as Template{suffix}…", "bookmark_add", () =>
		{
			Log.Info( $"{LogPrefix} CanvasContextMenu.SaveAsTemplate: {targets.Count} record(s)" );
			callbacks.SaveAsTemplate?.Invoke( targets );
		} );
		m.AddSeparator();

		m.AddOption( $"Cut{suffix}", "content_cut", () =>
		{
			Log.Info( $"{LogPrefix} CanvasContextMenu.Cut: {targets.Count} record(s)" );
			callbacks.Cut?.Invoke( targets );
		} );

		m.AddOption( $"Copy{suffix}", "content_copy", () =>
		{
			Log.Info( $"{LogPrefix} CanvasContextMenu.Copy: {targets.Count} record(s)" );
			callbacks.Copy?.Invoke( targets );
		} );

		var pasteOpt = m.AddOption( "Paste", "content_paste", () =>
		{
			Log.Info( $"{LogPrefix} CanvasContextMenu.Paste: onto {hit.ClassName}" );
			callbacks.Paste?.Invoke( hit );
		} );
		// Disabled-not-hidden: users learn Paste exists even before they copy.
		pasteOpt.Enabled = hasClipboard;

		var duplicateOpt = m.AddOption( $"Duplicate{suffix}", "file_copy", () =>
		{
			Log.Info( $"{LogPrefix} CanvasContextMenu.Duplicate: {targets.Count} record(s)" );
			callbacks.Duplicate?.Invoke( targets );
		} );

		m.AddOption( "Custom Styles…", "palette", () =>
		{
			Log.Info( $"{LogPrefix} CanvasContextMenu.CustomStyles: {hit.ClassName}" );
			callbacks.CustomStyles?.Invoke( hit );
		} );

		m.AddSeparator();

		m.AddOption( $"Delete{suffix}", "delete", () =>
		{
			Log.Info( $"{LogPrefix} CanvasContextMenu.Delete: {targets.Count} record(s)" );
			callbacks.Delete?.Invoke( targets );
		} );

		m.OpenAtCursor( false );
		return true;
	}
}