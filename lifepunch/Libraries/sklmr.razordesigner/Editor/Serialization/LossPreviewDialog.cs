using System;
using System.Collections.Generic;
using Editor;
using Sandbox;

namespace Grains.RazorDesigner.Serialization;

public sealed class LossPreviewDialog
{
	private const string LogPrefix = "[Grains.RazorDesigner]";

	private readonly Widget _parent;

	public LossPreviewDialog( Widget parent )
	{
		_parent = parent;
	}

	public void Show(
		IReadOnlyList<string> lossLines,
		string razorPath,
		Action onConfirm,
		Action onCancel = null )
	{
		var dialog = new Editor.Dialog( _parent );
		dialog.Window.WindowTitle = "Re-import from .razor — Data Loss Preview";
		dialog.Window.SetWindowIcon( "warning" );
		dialog.Window.SetModal( true, true );
		dialog.Window.MinimumWidth = 480;

		dialog.Layout = Layout.Column();
		dialog.Layout.Margin = 16;
		dialog.Layout.Spacing = 10;

		var header = new Editor.Label( dialog )
		{
			Text = $"Re-importing from:\n{razorPath}\n\n" +
			       "The following differences were detected between the current IR and the " +
			       ".razor file. These items may be lost if you proceed:",
			WordWrap = true,
		};
		dialog.Layout.Add( header );

		// Scrollable list of loss lines.
		var scroll = new ScrollArea( dialog );
		scroll.Canvas = new Widget();
		scroll.Canvas.Layout = Layout.Column();
		scroll.Canvas.Layout.Margin = 4;
		scroll.Canvas.Layout.Spacing = 2;
		scroll.MinimumHeight = 120;
		scroll.MaximumHeight = 280;

		foreach ( var line in lossLines )
		{
			var lbl = new Editor.Label( scroll.Canvas )
			{
				Text = $"• {line}",
				WordWrap = true,
			};
			lbl.SetStyles( "font-size: 11px; color: #e0a060;" );
			scroll.Canvas.Layout.Add( lbl );
		}

		dialog.Layout.Add( scroll );

		dialog.Layout.AddSeparator();

		var buttonRow = dialog.Layout.Add( Layout.Row() );
		buttonRow.Spacing = 6;
		buttonRow.AddStretchCell();

		var cancelBtn = new Editor.Button( dialog ) { Text = "Cancel", MinimumWidth = 80 };
		cancelBtn.MouseLeftPress += () =>
		{
			Log.Info( $"{LogPrefix} LossPreviewDialog: user chose Cancel" );
			dialog.Close();
			onCancel?.Invoke();
		};
		buttonRow.Add( cancelBtn );

		var confirmBtn = new Editor.Button( dialog ) { Text = "Re-import anyway", MinimumWidth = 120 };
		confirmBtn.MouseLeftPress += () =>
		{
			Log.Info( $"{LogPrefix} LossPreviewDialog: user confirmed Re-import" );
			dialog.Close();
			onConfirm?.Invoke();
		};
		buttonRow.Add( confirmBtn );

		dialog.Window.AdjustSize();
		dialog.Show();

		Log.Info( $"{LogPrefix} LossPreviewDialog: shown ({lossLines.Count} loss line(s))" );
	}
}
