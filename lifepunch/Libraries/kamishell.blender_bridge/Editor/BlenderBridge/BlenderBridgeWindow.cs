using System;
using System.Collections.Generic;
using System.Linq;
using Editor;
using Sandbox;

namespace BlenderBridge
{
	/// <summary>
	/// Editor dock for the Blender Bridge v2.
	/// Shows connection status, client indicator, manifest status, materials, and activity log.
	/// Summon from the editor's Window/Layout menu — the dock manager creates and parents the instance.
	/// </summary>
	[Dock( "Editor", "Blender Bridge", "sync_alt" )]
	public class BlenderBridgeWindow : Widget
	{
		private Label _statusLabel;
		private Label _portLabel;
		private Label _clientLabel;
		private Label _manifestLabel;
		private Button _toggleButton;
		private Widget _logCanvas;
		private Widget _materialsCanvas;
		private ToggleSwitch _autoStartToggle;
		private bool _lastRunningState = false;
		private readonly List<(Button btn, BridgeLockFlags flag, string baseLabel)> _lockButtons = new();

		private ScrollArea _logScroll;
		private bool _logCollapsed = false;

		private static readonly List<string> _logEntries = new();
		private const int MaxLogEntries = 200;

		// Auto-start lives in BlenderBridgeServer.AutoStartOnEditorLoad — a static
		// constructor here only runs if the dock is in the saved layout.

		public BlenderBridgeWindow( Widget parent ) : base( parent )
		{
			MinimumSize = new Vector2( 450, 400 );
			BuildUI();
			_pollTimer = new System.Threading.Timer( _ => RequestDrain(), null, 500, 500 );
		}

		private System.Threading.Timer _pollTimer;
		private volatile bool _needsDrain = false;

		/// <summary>
		/// Timer tick, on a threadpool thread. Setting the flag alone is not
		/// enough — _needsDrain is only consumed in OnPaint, and an unattended
		/// dock never repaints on its own, so status would sit stale (server
		/// stopped but "● Running", Blender gone but "1 client connected")
		/// until a mouse-over forced a paint. Hop to the main thread and ask
		/// for one explicitly.
		/// </summary>
		private void RequestDrain()
		{
			_needsDrain = true;
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
			base.OnDestroyed();
		}

		protected override void OnPaint()
		{
			base.OnPaint();
			// Cheap state-refresh tied to redraw. Reads tags from the current
			// selection — both fast operations — so it's fine to do per-paint
			// rather than wiring up a SelectionChanged event.
			RefreshLockButtonStates();
			if ( !_needsDrain ) return;
			_needsDrain = false;
			DrainLogQueue();
		}

		private void DrainLogQueue()
		{
			if ( !IsValid || _logCanvas == null ) return;

			// Update status
			if ( BlenderBridgeServer.IsRunning != _lastRunningState )
			{
				_lastRunningState = BlenderBridgeServer.IsRunning;
				if ( _lastRunningState )
				{
					_statusLabel.Text = "● Running";
					_statusLabel.SetStyles( "font-size: 15px; font-weight: bold; color: #4ade80;" );
					_toggleButton.Text = "Stop Bridge";
				}
				else
				{
					_statusLabel.Text = "● Stopped";
					_statusLabel.SetStyles( "font-size: 15px; font-weight: bold; color: #f87171;" );
					_toggleButton.Text = "Start Bridge";
				}
			}

			// Update client indicator
			if ( _clientLabel != null )
			{
				if ( BlenderBridgeServer.HasActiveClient )
				{
					_clientLabel.Text = "1 client connected";
					_clientLabel.SetStyles( "font-size: 11px; color: #4ade80;" );
				}
				else
				{
					_clientLabel.Text = "No clients";
					_clientLabel.SetStyles( "font-size: 11px; color: #9ca3af;" );
				}
			}

			// Update manifest status
			if ( _manifestLabel != null )
			{
				try
				{
					var scene = BridgeSceneHelper.ResolveScene();
					if ( scene != null && BridgePersistence.HasSavedState( scene ) )
					{
						_manifestLabel.Text = "Bridge state saved";
						_manifestLabel.SetStyles( "font-size: 11px; color: #4ade80;" );
					}
					else
					{
						_manifestLabel.Text = "No bridge state";
						_manifestLabel.SetStyles( "font-size: 11px; color: #9ca3af;" );
					}
				}
				catch
				{
					_manifestLabel.Text = "No bridge state";
					_manifestLabel.SetStyles( "font-size: 11px; color: #9ca3af;" );
				}
			}

			// Drain log queue. Cap check must come BEFORE TryDequeue: the other
			// order dequeues a 51st message and then discards it, silently losing
			// one line per over-full drain.
			int count = 0;
			while ( count < 50 && BlenderBridgeServer.LogQueue.TryDequeue( out var msg ) )
			{
				var text = $"[{DateTime.Now:HH:mm:ss}] {msg}";
				_logEntries.Add( text );
				AddLogLabel( text );
				count++;

				if ( _logEntries.Count > MaxLogEntries )
				{
					_logEntries.RemoveAt( 0 );
					var firstChild = _logCanvas?.Children?.FirstOrDefault();
					firstChild?.Destroy();
				}
			}
		}

		private void BuildUI()
		{
			var root = Layout.Column();
			root.Margin = 8;
			root.Spacing = 6;

			// ── Status Row ──────────────────────────────────────────────
			var statusRow = Layout.Row();
			statusRow.Spacing = 16;

			var statusCol = Layout.Column();
			var statusTitle = new Label( "STATUS" );
			statusTitle.SetStyles( "font-size: 10px; color: #888;" );
			statusCol.Add( statusTitle );
			_statusLabel = new Label( "● Stopped" );
			_statusLabel.SetStyles( "font-size: 15px; font-weight: bold; color: #f87171;" );
			statusCol.Add( _statusLabel );
			statusRow.Add( statusCol );

			var portCol = Layout.Column();
			var portTitle = new Label( "PORT" );
			portTitle.SetStyles( "font-size: 10px; color: #888;" );
			portCol.Add( portTitle );
			_portLabel = new Label( BlenderBridgeServer.Port.ToString() );
			_portLabel.SetStyles( "font-size: 15px; font-weight: bold;" );
			portCol.Add( _portLabel );
			statusRow.Add( portCol );

			statusRow.AddStretchCell();

			_toggleButton = new Button( "Start Bridge", "play_arrow" );
			_toggleButton.Clicked += ToggleBridge;
			statusRow.Add( _toggleButton );

			root.Add( statusRow );

			// ── Client + Manifest Indicators ────────────────────────────
			var infoRow = Layout.Row();
			infoRow.Spacing = 24;

			var clientCol = Layout.Column();
			var clientTitle = new Label( "CLIENT" );
			clientTitle.SetStyles( "font-size: 10px; color: #888;" );
			clientCol.Add( clientTitle );
			_clientLabel = new Label( "No clients" );
			_clientLabel.SetStyles( "font-size: 11px; color: #9ca3af;" );
			clientCol.Add( _clientLabel );
			infoRow.Add( clientCol );

			var manifestCol = Layout.Column();
			var manifestTitle = new Label( "PERSISTENCE" );
			manifestTitle.SetStyles( "font-size: 10px; color: #888;" );
			manifestCol.Add( manifestTitle );
			_manifestLabel = new Label( "No bridge state" );
			_manifestLabel.SetStyles( "font-size: 11px; color: #9ca3af;" );
			manifestCol.Add( _manifestLabel );
			infoRow.Add( manifestCol );

			root.Add( infoRow );
			root.AddSeparator();

			// ── Sync Controls ───────────────────────────────────────────
			var syncBox = Layout.Column();
			syncBox.Spacing = 4;
			var syncTitle = new Label( "Sync Controls" );
			syncTitle.SetStyles( "font-weight: bold; font-size: 13px;" );
			syncBox.Add( syncTitle );

			var syncBtnRow = Layout.Row();
			syncBtnRow.Spacing = 4;

			var sendToBlenderBtn = new Button( "Send to Blender", "upload" );
			sendToBlenderBtn.ToolTip = "Push selected objects to Blender (adopts native MeshComponents if needed)";
			sendToBlenderBtn.Clicked += () =>
			{
				if ( !BlenderBridgeServer.IsRunning ) return;
				var scene = BridgeSceneHelper.ResolveScene();
				if ( scene == null ) return;

				// Find selected objects in the editor
				var session = SceneEditorSession.Active;
				if ( session == null ) return;

				int count = 0;
				foreach ( var sel in session.Selection )
				{
					if ( sel is not GameObject go ) continue;

					// Find existing bridge tag or adopt native MeshComponents.
					// Identity tags only — lock tags (bridge_lock_*) share the
					// prefix, and picking one up would broadcast the object
					// under a phantom id shared by every locked object.
					var bridgeTag = go.Tags.TryGetAll().FirstOrDefault( BlenderBridgeDispatcher.IsBridgeIdentityTag );
					string bridgeId;

					if ( bridgeTag != null )
					{
						bridgeId = bridgeTag.Substring( 7 );
					}
					else
					{
						// Adopt native MeshComponent or Terrain if present.
						var meshComp = go.Components.Get<MeshComponent>();
						var terrain = go.Components.Get<Terrain>();
						bool hasMesh = meshComp?.Mesh != null;
						bool hasTerrain = terrain?.Storage != null;
						if ( !hasMesh && !hasTerrain ) continue;

						bridgeId = "b_" + Guid.NewGuid().ToString( "N" ).Substring( 0, 8 );
						go.Tags.Add( $"bridge_{bridgeId}" );
						var kind = hasTerrain ? "terrain" : "mesh";
						BlenderBridgeServer.LogInfo( $"Adopted '{go.Name}' as {bridgeId} ({kind})" );
					}

					// Build and broadcast as an object_created message
					var message = BlenderBridgeDispatcher.BuildObjectCreatedMessage( bridgeId, go );
					if ( message != null )
					{
						BlenderBridgeServer.BroadcastWithSeq( message );
						count++;
					}
				}

				if ( count > 0 )
					BlenderBridgeServer.LogInfo( $"Sent {count} object(s) to Blender" );
				else
					BlenderBridgeServer.LogInfo( "No mesh objects selected" );
			};
			syncBtnRow.Add( sendToBlenderBtn );

			var requestFromBlenderBtn = new Button( "Request from Blender", "download" );
			requestFromBlenderBtn.ToolTip = "Ask Blender to re-send mesh data for all bridge objects";
			requestFromBlenderBtn.Clicked += () =>
			{
				if ( !BlenderBridgeServer.IsRunning ) return;
				// Trigger a full sync — Blender will send all its objects
				BlenderBridgeServer.BroadcastWithSeq( new { type = "sync_response", objects = new object[0] } );
				BlenderBridgeServer.LogInfo( "Requested resync from Blender" );
			};
			syncBtnRow.Add( requestFromBlenderBtn );

			AddLockButton( syncBtnRow, "Lock In", "lock", BridgeLockFlags.Inbound,
				"Toggle: block all Blender → s&box updates for the selected object(s)." );

			AddLockButton( syncBtnRow, "Lock Out", "lock_outline", BridgeLockFlags.Outbound,
				"Toggle: stop pushing s&box-side changes for the selected object(s) to Blender." );

			AddLockButton( syncBtnRow, "Lock Mat", "palette", BridgeLockFlags.Materials,
				"Toggle: allow geometry/transform updates from Blender, but never overwrite materials. Use for objects with custom s&box vmats (water shaders, etc)." );

			root.Add( syncBox );
			root.Add( syncBtnRow );
			root.AddSeparator();

			// ── Materials Section ───────────────────────────────────────
			var matHeader = Layout.Row();
			var matTitle = new Label( "Generated Materials" );
			matTitle.SetStyles( "font-weight: bold; font-size: 13px;" );
			matHeader.Add( matTitle );
			matHeader.AddStretchCell();

			var openFolderBtn = new Button( "", "folder_open" );
			openFolderBtn.ToolTip = "Open materials folder";
			openFolderBtn.FixedWidth = 26;
			openFolderBtn.FixedHeight = 26;
			openFolderBtn.Clicked += OpenMaterialsFolder;
			matHeader.Add( openFolderBtn );

			var refreshBtn = new Button( "", "refresh" );
			refreshBtn.ToolTip = "Refresh list";
			refreshBtn.FixedWidth = 26;
			refreshBtn.FixedHeight = 26;
			refreshBtn.Clicked += RefreshMaterialsList;
			matHeader.Add( refreshBtn );
			root.Add( matHeader );

			var matScroll = new ScrollArea( null );
			matScroll.MaximumHeight = 150;
			_materialsCanvas = new Widget();
			_materialsCanvas.Layout = Layout.Column();
			matScroll.Canvas = _materialsCanvas;
			root.Add( matScroll );

			RefreshMaterialsList();
			root.AddSeparator();

			// ── Log Header ──────────────────────────────────────────────
			var logHeader = Layout.Row();
			var logTitle = new Label( "Activity Log" );
			logTitle.SetStyles( "font-weight: bold; font-size: 13px;" );
			logHeader.Add( logTitle );
			logHeader.AddStretchCell();

			var collapseBtn = new Button( "", "unfold_less" );
			collapseBtn.ToolTip = "Hide / show log";
			collapseBtn.FixedWidth = 26;
			collapseBtn.FixedHeight = 26;
			collapseBtn.Clicked += () =>
			{
				_logCollapsed = !_logCollapsed;
				if ( _logScroll == null ) return;
				if ( _logCollapsed )
				{
					_logScroll.MinimumHeight = 0;
					_logScroll.MaximumHeight = 0;
				}
				else
				{
					_logScroll.MinimumHeight = 250;
					_logScroll.MaximumHeight = int.MaxValue;
				}
			};
			logHeader.Add( collapseBtn );

			var clearBtn = new Button( "", "delete_sweep" );
			clearBtn.ToolTip = "Clear Log";
			clearBtn.FixedWidth = 26;
			clearBtn.FixedHeight = 26;
			clearBtn.Clicked += () => { _logEntries.Clear(); _logCanvas.DestroyChildren(); };
			logHeader.Add( clearBtn );
			root.Add( logHeader );

			// ── Log Area ────────────────────────────────────────────────
			_logScroll = new ScrollArea( null );
			_logScroll.MinimumHeight = 250;
			_logCanvas = new Widget();
			_logCanvas.Layout = Layout.Column();
			_logScroll.Canvas = _logCanvas;

			foreach ( var entry in _logEntries )
				AddLogLabel( entry );

			root.Add( _logScroll, 1 );
			root.AddSeparator();

			// ── Footer: settings + setup links ──────────────────────────
			var footerRow = Layout.Row();
			footerRow.Spacing = 8;

			_autoStartToggle = new ToggleSwitch( "Auto-start on editor load" );
			_autoStartToggle.Value = BlenderBridgeServer.AutoStart;
			_autoStartToggle.MouseClick += () =>
			{
				BlenderBridgeServer.AutoStart = _autoStartToggle.Value;
			};
			footerRow.Add( _autoStartToggle );
			footerRow.AddStretchCell();

			var addonUrl = "https://github.com/SanicTehHedgehog/blender-sbox-bridge/releases";
			var addonLink = new Button( "Get Blender Addon", "open_in_new" );
			addonLink.ToolTip = "Open the Blender addon download page (companion v2.0+ required to connect).";
			addonLink.Clicked += () =>
			{
				System.Diagnostics.Process.Start( new System.Diagnostics.ProcessStartInfo( addonUrl ) { UseShellExecute = true } );
			};
			footerRow.Add( addonLink );

			root.Add( footerRow );

			Layout = root;
		}

		private void AddLogLabel( string text )
		{
			var lbl = new Label( text );
			lbl.WordWrap = true;

			string color = "#e5e7eb";
			string weight = "normal";

			if ( text.Contains( "[ERROR]" ) )
			{
				color = "#f87171";
				weight = "bold";
			}
			else if ( text.Contains( "Created" ) || text.Contains( "Sync" ) || text.Contains( "Started" ) || text.Contains( "Restored" ) )
			{
				color = "#4ade80";
			}
			else if ( text.Contains( "Deleted" ) || text.Contains( "Stopped" ) )
			{
				color = "#9ca3af";
			}
			else if ( text.Contains( "mesh" ) || text.Contains( "Model" ) || text.Contains( "Chunked" ) )
			{
				color = "#60a5fa";
			}
			else if ( text.Contains( "light" ) || text.Contains( "Light" ) )
			{
				color = "#fbbf24";
			}

			lbl.SetStyles( $"font-family: monospace; font-size: 11px; padding: 2px; color: {color}; font-weight: {weight};" );
			_logCanvas.Layout.Add( lbl );
		}

		private string FindBridgeMaterialsDir()
		{
			try
			{
				// Must resolve to the SAME project the dispatcher writes materials
				// into (Project.Current). Scanning Documents\s&box projects\* here
				// binds the panel — and its delete buttons — to whichever other
				// project happens to sort first.
				var assetsDir = BlenderBridgeDispatcher.GetProjectAssetsDir();
				if ( assetsDir == null ) return null;

				var candidate = System.IO.Path.Combine( assetsDir, "materials", "blender_bridge" );
				if ( System.IO.Directory.Exists( candidate ) )
					return candidate;
			}
			catch { }
			return null;
		}

		private void RefreshMaterialsList()
		{
			if ( _materialsCanvas == null ) return;
			_materialsCanvas.DestroyChildren();
			MaterialThumb.PruneCache();

			var dir = FindBridgeMaterialsDir();
			if ( dir == null || !System.IO.Directory.Exists( dir ) )
			{
				var lbl = new Label( "No materials generated yet" );
				lbl.SetStyles( "font-size: 11px; color: #888; padding: 4px;" );
				_materialsCanvas.Layout.Add( lbl );
				return;
			}

			var vmats = System.IO.Directory.GetFiles( dir, "*.vmat" );
			if ( vmats.Length == 0 )
			{
				var lbl = new Label( "No materials generated yet" );
				lbl.SetStyles( "font-size: 11px; color: #888; padding: 4px;" );
				_materialsCanvas.Layout.Add( lbl );
				return;
			}

			foreach ( var vmat in vmats.OrderBy( f => f ) )
			{
				var fileName = System.IO.Path.GetFileNameWithoutExtension( vmat );
				var filePath = vmat;

				var row = Layout.Row();
				row.Spacing = 4;

				row.Add( new MaterialThumb( dir, fileName ) );

				var nameLbl = new Label( fileName );
				nameLbl.SetStyles( "font-family: monospace; font-size: 11px; color: #e5e7eb;" );
				row.Add( nameLbl );

				row.AddStretchCell();

				try
				{
					var info = new System.IO.FileInfo( filePath );
					var sizeLbl = new Label( $"{info.Length / 1024f:F1}kb" );
					sizeLbl.SetStyles( "font-size: 10px; color: #666; padding-right: 4px;" );
					row.Add( sizeLbl );
				}
				catch { }

				var deleteBtn = new Button( "", "delete" );
				deleteBtn.ToolTip = $"Delete {fileName}";
				deleteBtn.FixedWidth = 22;
				deleteBtn.FixedHeight = 22;
				deleteBtn.Clicked += () =>
				{
					try
					{
						DeleteMaterialFiles( dir, fileName );
					}
					catch { }
					RefreshMaterialsList();
				};
				row.Add( deleteBtn );

				var container = new Widget();
				container.Layout = row;
				container.SetStyles( "padding: 1px 0;" );
				_materialsCanvas.Layout.Add( container );
			}

			// Delete All button
			var deleteAllRow = Layout.Row();
			deleteAllRow.AddStretchCell();
			var deleteAllBtn = new Button( "Delete All", "delete_sweep" );
			deleteAllBtn.ToolTip = "Delete all generated bridge materials";
			deleteAllBtn.Clicked += () =>
			{
				try
				{
					foreach ( var f in System.IO.Directory.GetFiles( dir ) )
						System.IO.File.Delete( f );
				}
				catch { }
				RefreshMaterialsList();
			};
			deleteAllRow.Add( deleteAllBtn );

			var allContainer = new Widget();
			allContainer.Layout = deleteAllRow;
			_materialsCanvas.Layout.Add( allContainer );
		}

		/// <summary>
		/// Delete exactly one generated material's files. A bare "{name}*" glob
		/// takes prefix-collided neighbours with it — deleting "metal" would also
		/// remove metal_rough.vmat and metal_001.vmat, and those prefixes are
		/// normal Blender naming ("Metal" / "Metal Rough" / "Metal.001"). So:
		/// the vmat by exact name, textures only via the generator's four known
		/// suffixes, and never a .vmat/.vmat_c that merely shares a texture's
		/// name ("metal"'s rough map metal_rough.png vs material metal_rough).
		/// </summary>
		private static void DeleteMaterialFiles( string dir, string name )
		{
			foreach ( var ext in new[] { ".vmat", ".vmat_c" } )
			{
				var path = System.IO.Path.Combine( dir, name + ext );
				if ( System.IO.File.Exists( path ) )
					System.IO.File.Delete( path );
			}

			foreach ( var suffix in new[] { "color", "rough", "metal", "normal" } )
			{
				foreach ( var f in System.IO.Directory.GetFiles( dir, $"{name}_{suffix}.*" ) )
				{
					if ( f.EndsWith( ".vmat", StringComparison.OrdinalIgnoreCase ) ||
						 f.EndsWith( ".vmat_c", StringComparison.OrdinalIgnoreCase ) )
						continue;
					System.IO.File.Delete( f );
				}
			}
		}

		/// <summary>
		/// Square preview for a generated material row: the copied color
		/// texture if one exists next to the .vmat, else a flat chip in the
		/// vmat's tint color.
		/// </summary>
		private class MaterialThumb : Widget
		{
			// Pixmap has no Dispose, so loading the full-resolution source per
			// row on every list refresh abandons the previous set to
			// finalization. Cache one Pixmap per texture path instead; write
			// time invalidates when Blender regenerates the texture. Only ever
			// touched from the UI thread.
			private static readonly Dictionary<string, (DateTime WriteTime, Pixmap Pix)> _thumbCache = new();

			/// <summary>Drop cache entries whose texture file was deleted.</summary>
			internal static void PruneCache()
			{
				foreach ( var key in _thumbCache.Keys.Where( k => !System.IO.File.Exists( k ) ).ToList() )
					_thumbCache.Remove( key );
			}

			private readonly Pixmap _pix;
			private readonly Color _tint = Color.Transparent;

			public MaterialThumb( string dir, string matName )
			{
				FixedWidth = 26;
				FixedHeight = 26;
				ToolTip = matName;

				try
				{
					var tex = System.IO.Directory
						.GetFiles( dir, matName + "_color.*" )
						.FirstOrDefault( f => !f.EndsWith( ".meta" ) );
					if ( tex != null )
					{
						var writeTime = System.IO.File.GetLastWriteTimeUtc( tex );
						if ( _thumbCache.TryGetValue( tex, out var cached ) && cached.WriteTime == writeTime )
						{
							_pix = cached.Pix;
							return;
						}

						_pix = Pixmap.FromFile( tex );
						_thumbCache[tex] = (writeTime, _pix);
						return;
					}

					// No color texture — parse the tint so untextured
					// materials still get a meaningful color chip.
					var text = System.IO.File.ReadAllText(
						System.IO.Path.Combine( dir, matName + ".vmat" ) );
					var m = System.Text.RegularExpressions.Regex.Match(
						text, "g_vColorTint\\s+\"\\[([0-9.]+) ([0-9.]+) ([0-9.]+)" );
					if ( m.Success )
					{
						var inv = System.Globalization.CultureInfo.InvariantCulture;
						_tint = new Color(
							float.Parse( m.Groups[1].Value, inv ),
							float.Parse( m.Groups[2].Value, inv ),
							float.Parse( m.Groups[3].Value, inv ) );
					}
				}
				catch { }
			}

			protected override void OnPaint()
			{
				var r = LocalRect.Shrink( 1 );
				if ( _pix != null )
				{
					Paint.Draw( r, _pix );
					return;
				}
				Paint.ClearPen();
				Paint.SetBrush( _tint );
				Paint.DrawRect( r, 3 );
			}
		}

		private void OpenMaterialsFolder()
		{
			var dir = FindBridgeMaterialsDir();
			if ( dir != null && System.IO.Directory.Exists( dir ) )
				System.Diagnostics.Process.Start( "explorer", $"\"{dir}\"" );
		}

		private void AddLockButton( Layout row, string label, string icon, BridgeLockFlags flag, string tooltip )
		{
			var btn = new Button( label, icon );
			btn.ToolTip = tooltip;
			btn.Clicked += () =>
			{
				var session = SceneEditorSession.Active;
				if ( session == null ) return;
				int set = 0, cleared = 0;
				foreach ( var sel in session.Selection )
				{
					if ( sel is not GameObject go ) continue;
					var next = BridgeLockPolicy.ToggleExplicit( go, flag );
					if ( next.HasFlag( flag ) ) set++; else cleared++;
				}
				if ( set > 0 )     BlenderBridgeServer.LogInfo( $"Set {flag} on {set} object(s)" );
				if ( cleared > 0 ) BlenderBridgeServer.LogInfo( $"Cleared {flag} on {cleared} object(s)" );
				if ( set == 0 && cleared == 0 ) BlenderBridgeServer.LogInfo( "No objects selected" );
				RefreshLockButtonStates();
			};
			row.Add( btn );
			_lockButtons.Add( (btn, flag, label) );
		}

		/// <summary>
		/// Update each lock button's label to reflect whether the flag is set on
		/// the current selection. Three states: all-on -> "Label [ON]",
		/// none-on -> "Label", mixed -> "Label [~]". Tooltip stays static.
		///
		/// Called on every click and from the main OnPaint above, so selection
		/// changes surface the right state without explicit event subscription.
		/// </summary>
		private void RefreshLockButtonStates()
		{
			var session = SceneEditorSession.Active;
			var selected = session?.Selection?.OfType<GameObject>().ToList();

			foreach ( var (btn, flag, baseLabel) in _lockButtons )
			{
				if ( selected == null || selected.Count == 0 )
				{
					btn.Text = baseLabel;
					continue;
				}

				int onCount = selected.Count( g => BridgeLockPolicy.GetFlags( g ).HasFlag( flag ) );
				if ( onCount == selected.Count )
					btn.Text = baseLabel + " [ON]";
				else if ( onCount == 0 )
					btn.Text = baseLabel;
				else
					btn.Text = baseLabel + " [~]";
			}
		}

		private void ToggleBridge()
		{
			if ( BlenderBridgeServer.IsRunning )
				BlenderBridgeServer.StopServer();
			else
				BlenderBridgeServer.StartServer();
		}
	}
}
