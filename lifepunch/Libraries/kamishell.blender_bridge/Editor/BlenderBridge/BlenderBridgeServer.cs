using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Editor;
using Sandbox;

namespace BlenderBridge
{
	/// <summary>
	/// HTTP server for bidirectional scene sync with Blender (v2).
	/// Sequence-based echo prevention, session tracking, play mode detection.
	///
	/// Routes:
	///   POST /message — Blender sends scene changes to s&amp;box
	///   GET  /poll    — Blender polls for scene changes (returns {sessionId, sboxSeq, messages})
	///   GET  /status  — Health check with session info
	/// </summary>
	public static class BlenderBridgeServer
	{
		[ConVar( "bridge_port", ConVarFlags.Saved )]
		public static int Port { get; set; } = 8099;

		[ConVar( "bridge_autostart", ConVarFlags.Saved )]
		public static bool AutoStart { get; set; } = true;

		[ConCmd( "bridge_start" )]
		public static void CmdStart() => StartServer();

		[ConCmd( "bridge_stop" )]
		public static void CmdStop() => StopServer();

		[ConCmd( "bridge_status" )]
		public static void CmdStatus()
		{
			if ( IsRunning )
				Log.Info( $"[BlenderBridge] Running on port {Port}, session={SessionId}, seq={SboxSeq}, {_outbox.Count} queued" );
			else
				Log.Info( "[BlenderBridge] Not running. Type bridge_start to start." );
		}

		// ── Public state ──────────────────────────────────────────────────────
		public static bool IsRunning => _listener != null && _listener.IsListening;

		/// <summary>Session ID — changes on every server start, enables Blender reconnection detection.</summary>
		public static string SessionId { get; private set; } = "";

		/// <summary>Monotonic sequence counter for outgoing messages.</summary>
		public static int SboxSeq { get; internal set; } = 0;

		/// <summary>True if a Blender client has polled within the last 5 seconds.</summary>
		public static bool HasActiveClient => (DateTime.UtcNow - _lastPollTime).TotalSeconds < 5.0;

		internal static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			WriteIndented = false
		};

		// ── Internal state ────────────────────────────────────────────────────
		private static HttpListener _listener;
		private static CancellationTokenSource _cts;
		private static readonly ConcurrentQueue<string> _outbox = new();
		private static DateTime _lastPollTime = DateTime.MinValue;

		/// <summary>Serializes /poll handlers so concurrent pollers can't interleave outbox drains.</summary>
		private static readonly SemaphoreSlim _pollGate = new( 1, 1 );

		/// <summary>Messages drained from the outbox but whose response write failed —
		/// redelivered (oldest first) on the next poll instead of being lost. Guarded by _pollGate.</summary>
		private static readonly List<string> _undelivered = new();

		// ── Lifecycle ─────────────────────────────────────────────────────────

		private static bool _autoStartAttempted;

		/// <summary>
		/// Auto-start hook that fires whether or not the dock is in the saved
		/// layout. Previously this lived in the dock's static constructor, so a
		/// layout without the dock silently disabled auto-start — and opening
		/// the dock to investigate started the server, masking the failure.
		/// Runs on the editor main thread; one-shot.
		/// </summary>
		[EditorEvent.Frame]
		private static void AutoStartOnEditorLoad()
		{
			if ( _autoStartAttempted ) return;
			_autoStartAttempted = true;

			if ( AutoStart && !IsRunning )
				StartServer();
		}

		public static void StartServer()
		{
			if ( _listener != null && _listener.IsListening )
			{
				LogInfo( "Blender Bridge is already running." );
				return;
			}

			try
			{
				// Generate new session ID (Blender detects this change and resyncs)
				SessionId = Guid.NewGuid().ToString( "N" ).Substring( 0, 8 );
				SboxSeq = 0;
				BlenderBridgeDispatcher.ResetState();

				// Drop any undelivered messages from a previous session — Blender
				// resyncs on session change, so replaying stale deletes/updates
				// into the new session would only cause harm.
				_pollGate.Wait();
				try { _undelivered.Clear(); }
				finally { _pollGate.Release(); }

				_listener = new HttpListener();
				_listener.Prefixes.Add( $"http://localhost:{Port}/" );
				_listener.Prefixes.Add( $"http://127.0.0.1:{Port}/" );
				_listener.Start();

				_cts = new CancellationTokenSource();
				Task.Run( () => ListenLoop( _cts.Token ) );
				Task.Run( () => PollLoop( _cts.Token ) );

				LogInfo( $"Blender Bridge started on port {Port} (session={SessionId})" );

				// Restore cached meshes if available
				try { BridgePersistence.RestoreFromCache( BridgeSceneHelper.ResolveScene() ); }
				catch ( Exception ex ) { LogInfo( $"Cache restore skipped: {ex.Message}" ); }
			}
			catch ( Exception ex )
			{
				LogError( $"Failed to start Blender Bridge: {ex.Message}" );
			}
		}

		public static void StopServer()
		{
			// Queued mesh-cache saves must land before shutdown — the next
			// session's restore reads them.
			try { BridgePersistence.FlushPendingSaves( BridgeSceneHelper.ResolveScene(), force: true ); }
			catch { }

			try
			{
				_cts?.Cancel();
				try { _listener?.Stop(); } catch { }
				try { _listener?.Close(); } catch { }
				_listener = null;
				while ( _outbox.TryDequeue( out _ ) ) { }
				LogInfo( "Blender Bridge stopped." );
			}
			catch ( Exception ex )
			{
				try { LogError( $"Error stopping Blender Bridge: {ex.Message}" ); } catch { }
			}
		}

		// ── HTTP listen loop ──────────────────────────────────────────────────

		private static async Task ListenLoop( CancellationToken token )
		{
			while ( !token.IsCancellationRequested && _listener != null && _listener.IsListening )
			{
				try
				{
					var context = await _listener.GetContextAsync();
					_ = Task.Run( () => HandleRequest( context, token ), token );
				}
				catch ( Exception ex ) when ( ex is not ObjectDisposedException )
				{
					if ( !token.IsCancellationRequested )
						LogError( $"Bridge listen loop error: {ex.Message}" );
				}
			}
		}

		private static async Task HandleRequest( HttpListenerContext context, CancellationToken token )
		{
			var req = context.Request;
			var res = context.Response;

			res.Headers.Add( "Access-Control-Allow-Origin", "*" );
			res.Headers.Add( "Access-Control-Allow-Methods", "GET, POST, OPTIONS" );
			res.Headers.Add( "Access-Control-Allow-Headers", "*" );
			// Browsers may cache GET responses (/debug, /status) and show stale
			// state during diagnosis — forbid caching outright.
			res.Headers.Add( "Cache-Control", "no-store" );

			if ( req.HttpMethod == "OPTIONS" ) { res.StatusCode = 200; res.Close(); return; }

			try
			{
				var path = req.Url.AbsolutePath.TrimEnd( '/' );

				switch ( path )
				{
					case "/message" when req.HttpMethod == "POST":
						await HandleIncomingMessage( req, res );
						break;

					case "/poll" when req.HttpMethod == "GET":
						await HandlePoll( res );
						break;

					case "/debug" when req.HttpMethod == "GET":
						await HandleDebug( res );
						break;

					default:
						await HandleStatus( res );
						break;
				}
			}
			catch ( Exception ex )
			{
				LogError( $"Request error: {ex.Message}" );
				try { res.StatusCode = 500; res.Close(); } catch { }
			}
		}

		// ── POST /message ─────────────────────────────────────────────────────

		private static async Task HandleIncomingMessage( HttpListenerRequest req, HttpListenerResponse res )
		{
			using var reader = new StreamReader( req.InputStream, Encoding.UTF8 );
			var body = await reader.ReadToEndAsync();

			try
			{
				using var doc = JsonDocument.Parse( body );
				var root = doc.RootElement;

				// Reject echoed messages from s&box itself
				var origin = root.TryGetProperty( "origin", out var o ) ? o.GetString() : null;
				if ( origin == "sbox" )
				{
					res.StatusCode = 200;
					res.Close();
					return;
				}

				// Dispatch on the main thread and capture response
				string responseJson = "{\"ok\":true}";
				await GameTask.RunInThreadAsync( async () =>
				{
					await GameTask.MainThread();
					responseJson = BlenderBridgeDispatcher.Dispatch( root );
				} );

				res.StatusCode = 200;
				res.ContentType = "application/json";
				var responseBytes = Encoding.UTF8.GetBytes( responseJson );
				await res.OutputStream.WriteAsync( responseBytes, 0, responseBytes.Length );
				res.Close();
			}
			catch ( Exception ex )
			{
				LogError( $"Error handling message: {ex.Message}" );
				res.StatusCode = 400;
				// Serialize — ex.Message often contains Windows paths whose
				// backslashes would otherwise produce invalid JSON.
				var err = Encoding.UTF8.GetBytes( JsonSerializer.Serialize( new { error = ex.Message }, JsonOptions ) );
				await res.OutputStream.WriteAsync( err, 0, err.Length );
				res.Close();
			}
		}

		// ── GET /poll ─────────────────────────────────────────────────────────

		private static async Task HandlePoll( HttpListenerResponse res )
		{
			_lastPollTime = DateTime.UtcNow;

			// Serialize pollers so a concurrent /poll (e.g. a debug probe racing
			// the addon) can't interleave a drain, and hold drained messages in
			// _undelivered until the response write succeeds — a failed write
			// otherwise loses deleted/object_created/mesh_updated messages
			// permanently, since state was already updated at broadcast time.
			await _pollGate.WaitAsync();
			try
			{
				// Snapshot seq BEFORE draining: a broadcast landing between the
				// drain and response serialization would otherwise make this
				// response claim a seq whose message it doesn't contain, misfiring
				// client-side gap detection. Under-claiming (message included, seq
				// not yet claimed) is the safe direction.
				var seqSnapshot = SboxSeq;

				// Oldest first: redeliveries from a failed write, then new messages.
				var messages = new List<string>( _undelivered );
				_undelivered.Clear();
				while ( _outbox.TryDequeue( out var msg ) )
					messages.Add( msg );

				res.StatusCode = 200;
				res.ContentType = "application/json";

				// New v2 format: {sessionId, sboxSeq, messages}
				string messagesJson = messages.Count == 0
					? "[]"
					: "[" + string.Join( ",", messages ) + "]";

				var playing = BlenderBridgeDispatcher.IsPlayingSnapshot ? "true" : "false";
				var json = $"{{\"sessionId\":\"{SessionId}\",\"sboxSeq\":{seqSnapshot},\"playing\":{playing},\"messages\":{messagesJson}}}";

				var bytes = Encoding.UTF8.GetBytes( json );
				try
				{
					await res.OutputStream.WriteAsync( bytes, 0, bytes.Length );
					res.Close();
				}
				catch
				{
					// The poller never received these — keep them for the next poll.
					_undelivered.AddRange( messages );
					throw;
				}
			}
			finally
			{
				_pollGate.Release();
			}
		}

		// ── GET /debug ────────────────────────────────────────────────────────

		/// <summary>Read-only diagnostic dump: scene, bridge objects with their
		/// full tag lists, and tracking-dictionary keys. Never mutates state.</summary>
		private static async Task HandleDebug( HttpListenerResponse res )
		{
			string json = "{\"error\":\"debug dump failed\"}";
			await GameTask.RunInThreadAsync( async () =>
			{
				await GameTask.MainThread();
				json = BlenderBridgeDispatcher.DumpDebugState();
			} );

			res.ContentType = "application/json";
			res.StatusCode = 200;
			var bytes = Encoding.UTF8.GetBytes( json );
			await res.OutputStream.WriteAsync( bytes, 0, bytes.Length );
			res.Close();
		}

		// ── GET /status ───────────────────────────────────────────────────────

		private static async Task HandleStatus( HttpListenerResponse res )
		{
			res.ContentType = "application/json";
			res.StatusCode = 200;
			var status = JsonSerializer.Serialize( new
			{
				service = "BlenderBridge",
				running = true,
				sessionId = SessionId,
				sboxSeq = SboxSeq
			}, JsonOptions );
			var bytes = Encoding.UTF8.GetBytes( status );
			await res.OutputStream.WriteAsync( bytes, 0, bytes.Length );
			res.Close();
		}

		// ── Poll loop: detect s&box-side changes ──────────────────────────────

		private static async Task PollLoop( CancellationToken token )
		{
			while ( !token.IsCancellationRequested )
			{
				try
				{
					await Task.Delay( 200, token );
					if ( !HasActiveClient ) continue;

					await GameTask.RunInThreadAsync( async () =>
					{
						await GameTask.MainThread();
						BlenderBridgeDispatcher.PollForChanges();
					} );
				}
				catch ( OperationCanceledException ) { break; }
				catch ( Exception ex )
				{
					LogError( $"Poll error: {ex.Message}" );
				}
			}
		}

		// ── Outbox ────────────────────────────────────────────────────────────

		/// <summary>Queue a raw JSON message for Blender to pick up on next poll.</summary>
		internal static void SendToAll( string json )
		{
			_outbox.Enqueue( json );
		}

		/// <summary>Serialize an object with auto-incrementing seq and queue it.</summary>
		internal static void Broadcast( object message )
		{
			var json = JsonSerializer.Serialize( message, JsonOptions );
			SendToAll( json );
		}

		/// <summary>Broadcast with explicit seq increment.</summary>
		internal static void BroadcastWithSeq( object message )
		{
			SboxSeq++;
			Broadcast( message );
		}

		// ── Logging ───────────────────────────────────────────────────────────

		internal static readonly ConcurrentQueue<string> LogQueue = new();

		internal static void LogInfo( string msg )
		{
			LogQueue.Enqueue( msg );
		}

		internal static void LogError( string msg )
		{
			Log.Error( $"[BlenderBridge] {msg}" );
			LogQueue.Enqueue( $"[ERROR] {msg}" );
		}
	}
}
