using System;
using Editor;
using Sandbox;

namespace BlenderBridge
{
	/// <summary>
	/// Small colored dot that mirrors BlenderBridgeServer state.
	/// Red = stopped. Yellow = running, no client. Green = client connected.
	/// Designed to be embedded next to a title label; functionality (click, tooltip, popover) can grow on this class.
	/// </summary>
	public class BridgeStatusDot : Widget
	{
		private Label _dot;
		private System.Threading.Timer _pollTimer;
		private volatile bool _needsRefresh = true;

		private bool _lastRunning;
		private bool _lastClient;

		public BridgeStatusDot() : base( null )
		{
			ToolTip = "Bridge status";
			FixedWidth = 16;
			FixedHeight = 16;

			_dot = new Label( "●" );
			_dot.SetStyles( "color: #f87171; font-size: 14px;" );

			var row = Layout.Row();
			row.Add( _dot );
			Layout = row;

			_pollTimer = new System.Threading.Timer( _ => RequestRefresh(), null, 500, 500 );
		}

		/// <summary>
		/// Timer tick, on a threadpool thread. _needsRefresh is only consumed
		/// in OnPaint and nothing else ever requests a repaint, so an
		/// unattended dot would show stale state indefinitely. Hop to the main
		/// thread and ask for one explicitly.
		/// </summary>
		private void RequestRefresh()
		{
			_needsRefresh = true;
			GameTask.RunInThreadAsync( async () =>
			{
				await GameTask.MainThread();
				if ( IsValid )
					Update();
			} );
		}

		public override void OnDestroyed()
		{
			_pollTimer?.Dispose();
			_pollTimer = null;
			base.OnDestroyed();
		}

		protected override void OnPaint()
		{
			base.OnPaint();
			if ( !_needsRefresh ) return;
			_needsRefresh = false;
			Refresh();
		}

		private void Refresh()
		{
			if ( _dot == null ) return;

			var running = BlenderBridgeServer.IsRunning;
			var client = BlenderBridgeServer.HasActiveClient;

			if ( running == _lastRunning && client == _lastClient ) return;
			_lastRunning = running;
			_lastClient = client;

			string color;
			string tip;
			if ( !running )      { color = "#f87171"; tip = "Bridge: stopped"; }
			else if ( client )   { color = "#4ade80"; tip = $"Bridge: 1 client connected · port {BlenderBridgeServer.Port}"; }
			else                 { color = "#fbbf24"; tip = $"Bridge: idle · port {BlenderBridgeServer.Port}"; }

			_dot.SetStyles( $"color: {color}; font-size: 14px;" );
			ToolTip = tip;
		}
	}
}
