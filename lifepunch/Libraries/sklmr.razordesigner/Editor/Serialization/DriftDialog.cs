using System;
using Editor;
using Sandbox;

namespace Grains.RazorDesigner.Serialization;

public sealed class DriftDialog
{
	private const string LogPrefix = "[Grains.RazorDesigner]";

	private readonly Widget _parent;

	public DriftDialog( Widget parent )
	{
		_parent = parent;
	}

	public enum Choice { ReImport, Discard, Cancel }

	public void Show( string razorPath, string jsonPath, Action<Choice> onChoice )
	{
		var dialog = new Editor.Dialog( _parent );
		dialog.Window.WindowTitle = "Razor File Edited Outside Designer";
		dialog.Window.SetWindowIcon( "warning" );
		dialog.Window.SetModal( true, true );
		dialog.Window.MinimumWidth = 460;

		dialog.Layout = Layout.Column();
		dialog.Layout.Margin = 16;
		dialog.Layout.Spacing = 10;

		// Explanation
		var body = new Editor.Label( dialog )
		{
			Text = "The .razor file has been edited outside the designer — it is newer " +
			       "than the canonical .razor.json.\n\n" +
			       "Choose how to proceed:",
			WordWrap = true,
		};
		dialog.Layout.Add( body );

		// File paths for context
		var pathLabel = new Editor.Label( dialog )
		{
			Text = $".razor:      {razorPath}\n.razor.json: {jsonPath}",
			WordWrap = true,
		};
		pathLabel.SetStyles( "color: #888; font-size: 10px; font-family: monospace;" );
		dialog.Layout.Add( pathLabel );

		// Separator
		dialog.Layout.AddSeparator();

		// Button row
		var buttonRow = dialog.Layout.Add( Layout.Row() );
		buttonRow.Spacing = 6;
		buttonRow.AddStretchCell();

		// Cancel (leftmost — least prominent)
		var cancelBtn = new Editor.Button( dialog ) { Text = "Cancel", MinimumWidth = 80 };
		cancelBtn.MouseLeftPress += () =>
		{
			Log.Info( $"{LogPrefix} DriftDialog: user chose Cancel" );
			dialog.Close();
			onChoice?.Invoke( Choice.Cancel );
		};
		buttonRow.Add( cancelBtn );

		var discardBtn = new Editor.Button( dialog ) { Text = "Discard external edits", MinimumWidth = 140 };
		discardBtn.MouseLeftPress += () =>
		{
			Log.Info( $"{LogPrefix} DriftDialog: user chose Discard" );
			dialog.Close();
			onChoice?.Invoke( Choice.Discard );
		};
		buttonRow.Add( discardBtn );

		// Re-import is the default / recommended action — rightmost (highest visual priority).
		var reimportBtn = new Editor.Button( dialog ) { Text = "Re-import changes", MinimumWidth = 130 };
		reimportBtn.MouseLeftPress += () =>
		{
			Log.Info( $"{LogPrefix} DriftDialog: user chose Re-import" );
			dialog.Close();
			onChoice?.Invoke( Choice.ReImport );
		};
		buttonRow.Add( reimportBtn );

		dialog.Window.AdjustSize();
		dialog.Show();

		// Default focus on Re-import (spec: "Default focused button: Re-import").
		reimportBtn.Focus();

		Log.Info( $"{LogPrefix} DriftDialog: shown for {razorPath}" );
	}
}
