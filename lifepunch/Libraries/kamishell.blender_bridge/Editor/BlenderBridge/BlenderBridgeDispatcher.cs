using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Editor;
using Editor.MeshEditor;
using HalfEdgeMesh;
using Sandbox;

namespace BlenderBridge
{
	/// <summary>
	/// Handles incoming bridge messages from Blender v2 and applies them to the s&amp;box scene.
	/// Sequence-based echo prevention, idempotent creates, light support, chunked mesh, hierarchy grouping.
	/// </summary>
	internal static class BlenderBridgeDispatcher
	{
		// ── Sequence state (echo prevention) ──────────────────────────────────

		/// <summary>Highest Blender seq we have processed.</summary>
		private static int _lastBlenderSeqProcessed = 0;

		/// <summary>bridgeId -> Blender seq that caused the last write. Used to suppress echo in PollForChanges.</summary>
		private static Dictionary<string, int> _lastWriteSeq = new();

		// ── Object caches ─────────────────────────────────────────────────────

		/// <summary>O(1) lookup cache: bridgeId -> GameObject. Rebuilt on cache miss via tree walk.</summary>
		private static Dictionary<string, GameObject> _bridgeObjectCache = new();

		/// <summary>Idempotency keys: key -> bridgeId. Prevents duplicate creation on network retry.</summary>
		private static Dictionary<string, string> _idempotencyKeys = new();

		/// <summary>Last-known transforms for change detection.</summary>
		private static Dictionary<string, (Vector3 pos, Rotation rot)> _lastKnown = new();

		/// <summary>Last-known mesh hash for geometry change detection.</summary>
		private static Dictionary<string, int> _lastMeshHash = new();

		/// <summary>Last-known light property hash for change detection.</summary>
		private static Dictionary<string, int> _lastLightHash = new();

		/// <summary>
		/// Last grid spacing we broadcast to Blender (in source units). Used to suppress
		/// the echo when Blender just pushed a grid_changed that we mirrored into
		/// Gizmo.Settings.GridSpacing — the poll loop would otherwise re-broadcast it.
		/// </summary>
		private static float _lastBroadcastGridSpacing = -1f;

		// ── Chunked mesh accumulator ──────────────────────────────────────────

		private static Dictionary<string, MeshAccumulator> _pendingChunks = new();

		private struct MeshAccumulator
		{
			public List<float> Vertices;
			public int TotalVertices;
			public int ChunksReceived;
			public int ChunkCount;
			public DateTime StartTime;
		}

		/// <summary>
		/// Bridge IDs we've told Blender to delete. If one reappears in a scan
		/// (Ctrl+Z restores the GameObject with its tag intact), it must be
		/// re-announced as a creation — silently re-tracking it would leave the
		/// engine streaming updates for an id Blender no longer maps.
		/// </summary>
		private static HashSet<string> _recentlyDeleted = new();

		// ── Scan amortization ─────────────────────────────────────────────────

		private const int MeshHashBudget = 12;
		private static int _scanTick;

		// ── Play mode ─────────────────────────────────────────────────────────

		private static bool _wasPlaying = false;

		/// <summary>Reset all state. Called on server start and hot reload.</summary>
		internal static void ResetState()
		{
			_lastBlenderSeqProcessed = 0;
			_lastWriteSeq.Clear();
			_bridgeObjectCache.Clear();
			_idempotencyKeys.Clear();
			_lastKnown.Clear();
			_lastMeshHash.Clear();
			_lastLightHash.Clear();
			_pendingChunks.Clear();
			_recentlyDeleted.Clear();
			_wasPlaying = false;
			_lastBroadcastGridSpacing = -1f;
			BridgePersistence.ClearPendingSaves();
		}

		// ── Dispatch ──────────────────────────────────────────────────────────

		/// <summary>Handle an incoming message. Returns a JSON response string. Must be called on main thread.</summary>
		internal static string Dispatch( JsonElement root )
		{
			var type = root.TryGetProperty( "type", out var t ) ? t.GetString() : null;

			// Extract and update sequence tracking
			if ( root.TryGetProperty( "seq", out var seqEl ) && seqEl.ValueKind == JsonValueKind.Number )
			{
				var seq = seqEl.GetInt32();
				if ( seq > _lastBlenderSeqProcessed )
					_lastBlenderSeqProcessed = seq;
			}

			try
			{
				return type switch
				{
					"create" => HandleCreate( root ),
					"update_transform" => HandleUpdateTransform( root ),
					"update_mesh" => HandleUpdateMesh( root ),
					"delete" => HandleDelete( root ),
					"set_visibility" => HandleSetVisibility( root ),
					"sync" => HandleSync( root ),
					"update_scene_transform" => HandleUpdateSceneTransform( root ),
					"create_light" => HandleCreateLight( root ),
					"update_light" => HandleUpdateLight( root ),
					"mesh_begin" => HandleMeshBegin( root ),
					"mesh_chunk" => HandleMeshChunk( root ),
					"mesh_end" => HandleMeshEnd( root ),
					"get_project_info" => HandleGetProjectInfo( root ),
					"grid_changed" => HandleGridChanged( root ),
					_ => "{\"ok\":true}"
				};
			}
			catch ( Exception ex )
			{
				BlenderBridgeServer.LogError( $"Dispatch error ({type}): {ex.Message}" );
				// Serialize — ex.Message often contains Windows paths whose
				// backslashes would otherwise produce invalid JSON, making
				// exactly these errors invisible to the addon's error surface.
				return JsonSerializer.Serialize( new { error = ex.Message }, BlenderBridgeServer.JsonOptions );
			}
		}

		// ── create ────────────────────────────────────────────────────────────

		private static string HandleCreate( JsonElement root )
		{
			var name = GetString( root, "name", "Blender Object" );
			var blenderSeq = GetInt( root, "seq", 0 );

			// Idempotency check: if this key was already used, return existing bridgeId
			var idemKey = GetString( root, "idempotencyKey" );
			if ( !string.IsNullOrEmpty( idemKey ) && _idempotencyKeys.TryGetValue( idemKey, out var existingId ) )
			{
				// Verify the object still exists
				var existingGo = FindByBridgeTag( existingId );
				if ( existingGo != null )
				{
					if ( BridgeLockPolicy.AllowsInbound( existingGo ) )
					{
						if ( BridgeLockPolicy.AllowsTransformChange( existingGo ) )
							ApplyTransform( existingGo, root );
						if ( BridgeLockPolicy.AllowsGeometryChange( existingGo ) && root.TryGetProperty( "meshData", out var md ) )
							ApplyMeshData( existingGo, md );
						// Only arm the echo guard when something was actually
						// applied — a lock-rejected retry has no echo to suppress.
						_lastWriteSeq[existingId] = blenderSeq;
						_lastKnown[existingId] = (existingGo.WorldPosition, existingGo.WorldRotation);
						StampAppliedMeshHash( existingId, existingGo );
					}
					return JsonSerializer.Serialize( new { bridgeId = existingId }, BlenderBridgeServer.JsonOptions );
				}
				_idempotencyKeys.Remove( idemKey );
			}

			var scene = BridgeSceneHelper.ResolveScene();
			if ( scene == null ) return "{\"error\":\"no scene\"}";

			// Anti-duplication guard: if a GameObject in this scene already
			// shares this name AND is locked from inbound Blender updates
			// (e.g. a Terrain that was sent as a one-way proxy), assume Blender
			// lost the bridge_id on round-trip and is trying to re-create it.
			// Quietly map the new request onto the existing object instead of
			// spawning a duplicate.
			var nameClash = BridgeSceneHelper
				.WalkAll( scene )
				.FirstOrDefault( g => g.Name == name && !BridgeLockPolicy.AllowsInbound( g ) );
			if ( nameClash != null )
			{
				var existingTag = nameClash.Tags.TryGetAll().FirstOrDefault( IsBridgeIdentityTag );
				var resolvedId = existingTag != null ? existingTag.Substring( 7 ) : "locked";
				BlenderBridgeServer.LogInfo( $"Suppressed duplicate create for locked '{name}' (re-binding to {resolvedId})" );
				// Locked object, nothing applied — no echo to suppress, so the
				// guard stays unarmed.
				if ( !string.IsNullOrEmpty( idemKey ) )
					_idempotencyKeys[idemKey] = resolvedId;
				return JsonSerializer.Serialize( new { bridgeId = resolvedId, locked = true }, BlenderBridgeServer.JsonOptions );
			}

			// Honor a Blender-minted ID. The .blend is the durable side of the
			// bridge — engine-minted IDs died with every unsaved engine scene,
			// reissuing identity on each reconnect and orphaning mesh caches.
			// If the id already exists here, treat the create as an idempotent
			// upsert so re-sends after a session mismatch converge onto the
			// existing object instead of duplicating it.
			var requestedId = GetString( root, "bridgeId" );
			if ( IsValidClientBridgeId( requestedId ) )
			{
				var existingByReq = FindByBridgeTag( requestedId );
				if ( existingByReq != null )
				{
					if ( BridgeLockPolicy.AllowsInbound( existingByReq ) )
					{
						if ( BridgeLockPolicy.AllowsTransformChange( existingByReq ) )
							ApplyTransform( existingByReq, root );
						if ( BridgeLockPolicy.AllowsGeometryChange( existingByReq ) && root.TryGetProperty( "meshData", out var upsertMd ) )
							ApplyMeshData( existingByReq, upsertMd );
						_lastWriteSeq[requestedId] = blenderSeq;
						_lastKnown[requestedId] = (existingByReq.WorldPosition, existingByReq.WorldRotation);
						StampAppliedMeshHash( requestedId, existingByReq );
						BridgePersistence.SaveAfterChange( scene, requestedId, existingByReq );
					}
					if ( !string.IsNullOrEmpty( idemKey ) )
						_idempotencyKeys[idemKey] = requestedId;
					BlenderBridgeServer.LogInfo( $"Upserted '{name}' onto existing {requestedId}" );
					return JsonSerializer.Serialize( new { bridgeId = requestedId }, BlenderBridgeServer.JsonOptions );
				}
			}

			var bridgeId = IsValidClientBridgeId( requestedId )
				? requestedId
				: "b_" + Guid.NewGuid().ToString( "N" ).Substring( 0, 8 );

			// Resolve hierarchy: Blender Bridge > [collection path] > object
			var parent = GetOrCreateBridgeGroup( scene );
			if ( root.TryGetProperty( "hierarchy", out var hierEl ) && hierEl.ValueKind == JsonValueKind.Array )
			{
				foreach ( var h in hierEl.EnumerateArray() )
				{
					var hName = h.GetString();
					if ( !string.IsNullOrEmpty( hName ) )
						parent = GetOrCreateChild( parent, hName );
				}
			}

			var go = scene.CreateObject();
			go.Name = name;
			go.Parent = parent;
			ApplyTransform( go, root );

			if ( root.TryGetProperty( "meshData", out var meshData ) )
				ApplyMeshData( go, meshData );

			go.Tags.Add( $"bridge_{bridgeId}" );
			// A re-created id must not linger in the deleted set — the scan
			// would re-announce an object Blender just sent us.
			_recentlyDeleted.Remove( bridgeId );
			_bridgeObjectCache[bridgeId] = go;
			_lastWriteSeq[bridgeId] = blenderSeq;
			_lastKnown[bridgeId] = (go.WorldPosition, go.WorldRotation);
			StampAppliedMeshHash( bridgeId, go );
			if ( !string.IsNullOrEmpty( idemKey ) )
				_idempotencyKeys[idemKey] = bridgeId;

			BlenderBridgeServer.LogInfo( $"Created '{name}' as {bridgeId}" );
			BridgePersistence.SaveAfterChange( scene, bridgeId, go );
			return JsonSerializer.Serialize( new { bridgeId }, BlenderBridgeServer.JsonOptions );
		}

		// ── update_transform ──────────────────────────────────────────────────

		private static string HandleUpdateTransform( JsonElement root )
		{
			var bridgeId = GetString( root, "bridgeId" );
			if ( string.IsNullOrEmpty( bridgeId ) ) return "{\"error\":\"missing bridgeId\"}";
			var blenderSeq = GetInt( root, "seq", 0 );

			var go = FindByBridgeTag( bridgeId );
			if ( go == null ) return "{\"error\":\"not found\"}";

			// Lock-rejected: nothing was applied, so there is no echo to
			// suppress — arming the guard here would swallow the next real
			// s&box-side edit instead.
			if ( !BridgeLockPolicy.AllowsTransformChange( go ) )
				return "{\"ok\":true,\"locked\":true}";

			ApplyTransform( go, root );
			_lastWriteSeq[bridgeId] = blenderSeq;
			_lastKnown[bridgeId] = (go.WorldPosition, go.WorldRotation);

			return "{\"ok\":true}";
		}

		// ── update_mesh ───────────────────────────────────────────────────────

		// All lock decisions go through BridgeLockPolicy. The legacy bridge_locked
		// tag is still recognized there for backward compatibility, and Terrain
		// components are auto-locked (Inbound | Geometry | Materials) so the
		// one-way proxy mesh never clobbers the heightmap-driven terrain.
		internal const string LockTag = BridgeLockPolicy.LegacyLockTag;

		internal static bool IsLockedFromBlender( GameObject go )
			=> !BridgeLockPolicy.AllowsInbound( go );

		private static string HandleUpdateMesh( JsonElement root )
		{
			var bridgeId = GetString( root, "bridgeId" );
			if ( string.IsNullOrEmpty( bridgeId ) ) return "{\"error\":\"missing bridgeId\"}";
			var blenderSeq = GetInt( root, "seq", 0 );

			var go = FindByBridgeTag( bridgeId );
			if ( go == null ) return "{\"error\":\"not found\"}";

			// Lock-rejected: nothing applied, no echo to suppress — do not arm
			// the guard (see HandleUpdateTransform).
			if ( !BridgeLockPolicy.AllowsInbound( go ) )
				return "{\"ok\":true,\"locked\":true}";

			if ( BridgeLockPolicy.AllowsTransformChange( go ) )
				ApplyTransform( go, root );

			if ( BridgeLockPolicy.AllowsGeometryChange( go ) && root.TryGetProperty( "meshData", out var meshData ) )
				ApplyMeshData( go, meshData );

			_lastWriteSeq[bridgeId] = blenderSeq;
			_lastKnown[bridgeId] = (go.WorldPosition, go.WorldRotation);
			StampAppliedMeshHash( bridgeId, go );

			var scene = BridgeSceneHelper.ResolveScene();
			if ( scene != null )
				BridgePersistence.SaveAfterChange( scene, bridgeId, go );

			return "{\"ok\":true}";
		}

		/// <summary>
		/// Record the hash of a mesh we JUST applied from a Blender write.
		/// Mandatory with amortized scanning: the round-robin check may not
		/// reach this object until many ticks after the echo guard was
		/// consumed, and without the stamp our own write would read as a
		/// user edit and get broadcast back to Blender.
		/// </summary>
		private static void StampAppliedMeshHash( string bridgeId, GameObject go )
		{
			var mesh = go?.Components.Get<MeshComponent>()?.Mesh;
			if ( mesh != null )
				_lastMeshHash[bridgeId] = ComputeMeshGeometryHash( mesh );
		}

		// ── delete ────────────────────────────────────────────────────────────

		private static string HandleDelete( JsonElement root )
		{
			var bridgeId = GetString( root, "bridgeId" );
			if ( string.IsNullOrEmpty( bridgeId ) ) return "{\"error\":\"missing bridgeId\"}";

			var go = FindByBridgeTag( bridgeId );
			if ( go != null )
			{
				go.Destroy();
				// If Ctrl+Z later restores this GameObject, the scan must
				// re-announce it — Blender no longer maps this id.
				_recentlyDeleted.Add( bridgeId );
			}

			_bridgeObjectCache.Remove( bridgeId );
			_lastKnown.Remove( bridgeId );
			_lastWriteSeq.Remove( bridgeId );
			_lastMeshHash.Remove( bridgeId );
			_idempotencyKeys.Where( kv => kv.Value == bridgeId ).Select( kv => kv.Key ).ToList()
				.ForEach( k => _idempotencyKeys.Remove( k ) );

			BridgePersistence.RemoveFromCache( bridgeId );
			BlenderBridgeServer.LogInfo( $"Deleted {bridgeId}" );
			return "{\"ok\":true}";
		}

		// ── set_visibility ────────────────────────────────────────────────────

		/// <summary>Blender hid/unhid an object — toggle Enabled instead of
		/// destroying, so the GameObject keeps its components and materials.</summary>
		private static string HandleSetVisibility( JsonElement root )
		{
			var bridgeId = GetString( root, "bridgeId" );
			if ( string.IsNullOrEmpty( bridgeId ) ) return "{\"error\":\"missing bridgeId\"}";

			var go = FindByBridgeTag( bridgeId );
			if ( go == null ) return JsonSerializer.Serialize( new { error = $"'{bridgeId}' not found" }, BlenderBridgeServer.JsonOptions );

			if ( !BridgeLockPolicy.AllowsInbound( go ) )
				return "{\"ok\":true,\"locked\":true}";

			var visible = !root.TryGetProperty( "visible", out var v ) || v.ValueKind != JsonValueKind.False;
			go.Enabled = visible;
			BlenderBridgeServer.LogInfo( $"{(visible ? "Enabled" : "Disabled")} {bridgeId}" );
			return "{\"ok\":true}";
		}

		// ── create_light ──────────────────────────────────────────────────────

		private static string HandleCreateLight( JsonElement root )
		{
			var name = GetString( root, "name", "Blender Light" );
			var lightType = GetString( root, "lightType", "point" );
			var blenderSeq = GetInt( root, "seq", 0 );

			// Idempotency
			var idemKey = GetString( root, "idempotencyKey" );
			if ( !string.IsNullOrEmpty( idemKey ) && _idempotencyKeys.TryGetValue( idemKey, out var existingId ) )
			{
				var existingGo = FindByBridgeTag( existingId );
				if ( existingGo != null )
				{
					// Same lock policy as the mesh handlers — the idempotent
					// re-apply must not be a side door around "Lock In".
					if ( BridgeLockPolicy.AllowsInbound( existingGo ) )
					{
						if ( BridgeLockPolicy.AllowsTransformChange( existingGo ) )
							ApplyTransform( existingGo, root );
						ApplyLightProperties( existingGo, root );
						_lastWriteSeq[existingId] = blenderSeq;
						_lastKnown[existingId] = (existingGo.WorldPosition, existingGo.WorldRotation);
					}
					return JsonSerializer.Serialize( new { bridgeId = existingId }, BlenderBridgeServer.JsonOptions );
				}
				_idempotencyKeys.Remove( idemKey );
			}

			// Honor a Blender-minted ID; upsert if the id already exists here
			// (same rationale as HandleCreate).
			var requestedId = GetString( root, "bridgeId" );
			if ( IsValidClientBridgeId( requestedId ) )
			{
				var existingByReq = FindByBridgeTag( requestedId );
				if ( existingByReq != null )
				{
					if ( BridgeLockPolicy.AllowsInbound( existingByReq ) )
					{
						if ( BridgeLockPolicy.AllowsTransformChange( existingByReq ) )
							ApplyTransform( existingByReq, root );
						ApplyLightProperties( existingByReq, root );
						_lastWriteSeq[requestedId] = blenderSeq;
						_lastKnown[requestedId] = (existingByReq.WorldPosition, existingByReq.WorldRotation);
					}
					if ( !string.IsNullOrEmpty( idemKey ) )
						_idempotencyKeys[idemKey] = requestedId;
					return JsonSerializer.Serialize( new { bridgeId = requestedId }, BlenderBridgeServer.JsonOptions );
				}
			}

			var bridgeId = IsValidClientBridgeId( requestedId )
				? requestedId
				: "b_" + Guid.NewGuid().ToString( "N" ).Substring( 0, 8 );

			var scene = BridgeSceneHelper.ResolveScene();
			if ( scene == null ) return "{\"error\":\"no scene\"}";

			var parent = GetOrCreateBridgeGroup( scene );
			var go = scene.CreateObject();
			go.Name = name;
			go.Parent = parent;
			ApplyTransform( go, root );

			// Create appropriate light component
			switch ( lightType )
			{
				case "spot":
					go.Components.Create<SpotLight>();
					break;
				case "directional":
					go.Components.Create<DirectionalLight>();
					break;
				default:
					go.Components.Create<PointLight>();
					break;
			}

			ApplyLightProperties( go, root );

			go.Tags.Add( $"bridge_{bridgeId}" );
			_recentlyDeleted.Remove( bridgeId );
			_bridgeObjectCache[bridgeId] = go;
			_lastWriteSeq[bridgeId] = blenderSeq;
			_lastKnown[bridgeId] = (go.WorldPosition, go.WorldRotation);
			if ( !string.IsNullOrEmpty( idemKey ) )
				_idempotencyKeys[idemKey] = bridgeId;

			BlenderBridgeServer.LogInfo( $"Created light '{name}' as {bridgeId} ({lightType})" );
			return JsonSerializer.Serialize( new { bridgeId }, BlenderBridgeServer.JsonOptions );
		}

		// ── update_light ──────────────────────────────────────────────────────

		private static string HandleUpdateLight( JsonElement root )
		{
			var bridgeId = GetString( root, "bridgeId" );
			if ( string.IsNullOrEmpty( bridgeId ) ) return "{\"error\":\"missing bridgeId\"}";
			var blenderSeq = GetInt( root, "seq", 0 );

			var go = FindByBridgeTag( bridgeId );
			if ( go == null ) return "{\"error\":\"not found\"}";

			// Same lock policy as every other mutation handler — "Lock In"
			// must cover lights too. Nothing applied, so the echo guard
			// stays unarmed.
			if ( !BridgeLockPolicy.AllowsInbound( go ) )
				return "{\"ok\":true,\"locked\":true}";

			if ( BridgeLockPolicy.AllowsTransformChange( go ) )
				ApplyTransform( go, root );
			ApplyLightProperties( go, root );
			_lastWriteSeq[bridgeId] = blenderSeq;
			_lastKnown[bridgeId] = (go.WorldPosition, go.WorldRotation);

			return "{\"ok\":true}";
		}

		// ── Chunked mesh handlers ─────────────────────────────────────────────

		private static string HandleMeshBegin( JsonElement root )
		{
			var bridgeId = GetString( root, "bridgeId" );
			if ( string.IsNullOrEmpty( bridgeId ) ) return "{\"error\":\"missing bridgeId\"}";

			var totalVerts = GetInt( root, "totalVertices", 0 );
			var chunkCount = GetInt( root, "chunkCount", 0 );

			_pendingChunks[bridgeId] = new MeshAccumulator
			{
				Vertices = new List<float>( totalVerts * 3 ),
				TotalVertices = totalVerts,
				ChunksReceived = 0,
				ChunkCount = chunkCount,
				StartTime = DateTime.UtcNow,
			};

			return "{\"ok\":true}";
		}

		private static string HandleMeshChunk( JsonElement root )
		{
			var bridgeId = GetString( root, "bridgeId" );
			if ( string.IsNullOrEmpty( bridgeId ) ) return "{\"error\":\"missing bridgeId\"}";

			if ( !_pendingChunks.TryGetValue( bridgeId, out var accum ) )
				return "{\"error\":\"no pending mesh_begin\"}";

			if ( root.TryGetProperty( "vertices", out var vertsEl ) )
			{
				foreach ( var v in vertsEl.EnumerateArray() )
					accum.Vertices.Add( v.GetSingle() );
			}

			accum.ChunksReceived++;
			_pendingChunks[bridgeId] = accum;
			return "{\"ok\":true}";
		}

		private static string HandleMeshEnd( JsonElement root )
		{
			var bridgeId = GetString( root, "bridgeId" );
			if ( string.IsNullOrEmpty( bridgeId ) ) return "{\"error\":\"missing bridgeId\"}";
			var blenderSeq = GetInt( root, "seq", 0 );

			if ( !_pendingChunks.TryGetValue( bridgeId, out var accum ) )
				return "{\"error\":\"no pending mesh_begin\"}";

			_pendingChunks.Remove( bridgeId );

			// Validate stream completeness before applying. A dropped chunk
			// shifts every later vertex — faces then resolve to the wrong
			// vertices and the scrambled mesh would be persisted to the mesh
			// cache while Blender, having stored the geometry hash at stream
			// start, considers it synced and never resends.
			var receivedVerts = accum.Vertices.Count / 3;
			if ( (accum.ChunkCount > 0 && accum.ChunksReceived != accum.ChunkCount)
				|| (accum.TotalVertices > 0 && receivedVerts != accum.TotalVertices) )
			{
				var detail = $"incomplete mesh stream for {bridgeId}: "
					+ $"{accum.ChunksReceived}/{accum.ChunkCount} chunks, "
					+ $"{receivedVerts}/{accum.TotalVertices} vertices";
				BlenderBridgeServer.LogError( detail );
				return JsonSerializer.Serialize( new { error = detail }, BlenderBridgeServer.JsonOptions );
			}

			// Build a complete meshData JsonElement from accumulated vertices + face data from this message
			var vertArray = accum.Vertices;

			// Parse faces from this message
			var faces = new List<int>();
			if ( root.TryGetProperty( "faces", out var facesEl ) )
				foreach ( var f in facesEl.EnumerateArray() )
					faces.Add( f.GetInt32() );

			int[] faceMaterials = null;
			if ( root.TryGetProperty( "faceMaterials", out var fmEl ) )
			{
				var fmList = new List<int>();
				foreach ( var fm in fmEl.EnumerateArray() )
					fmList.Add( fm.GetInt32() );
				faceMaterials = fmList.ToArray();
			}

			List<MaterialDef> materials = null;
			if ( root.TryGetProperty( "materials", out var matsEl ) )
				materials = ParseMaterialDefs( matsEl );

			// Build ParsedMesh
			var vertCount = vertArray.Count / 3;
			var vertices = new Vector3[vertCount];
			for ( int i = 0; i < vertCount; i++ )
				vertices[i] = new Vector3( vertArray[i * 3], vertArray[i * 3 + 1], vertArray[i * 3 + 2] );

			var faceGroups = new List<int[]>();
			int idx = 0;
			while ( idx < faces.Count )
			{
				int fvc = faces[idx++];
				if ( idx + fvc > faces.Count ) break;
				var face = new int[fvc];
				for ( int i = 0; i < fvc; i++ )
					face[i] = faces[idx++];
				faceGroups.Add( face );
			}

			// Blender-authored UVs ride on mesh_end in the same flat format as
			// update_mesh. Without parsing them here, every chunked (20k+ vertex)
			// mesh silently loses its UV layout to the grid-align fallback.
			List<Vector2[]> faceUVs = null;
			if ( root.TryGetProperty( "faceUVs", out var uvEl ) )
				faceUVs = ParseFaceUVs( uvEl, faceGroups );

			var parsed = new ParsedMesh
			{
				Vertices = vertices,
				FaceGroups = faceGroups,
				FaceMaterials = faceMaterials,
				Materials = materials,
				FaceUVs = faceUVs
			};

			// Find the object and apply mesh
			var go = FindByBridgeTag( bridgeId );
			if ( go == null )
				return "{\"error\":\"not found\"}";

			// Same lock policy as update_mesh — the chunked path must not be a
			// side door around per-object locks. Nothing applied, so do not arm
			// the echo guard.
			if ( !BridgeLockPolicy.AllowsInbound( go ) )
				return "{\"ok\":true,\"locked\":true}";

			if ( BridgeLockPolicy.AllowsTransformChange( go ) )
				ApplyTransform( go, root );

			if ( BridgeLockPolicy.AllowsGeometryChange( go ) )
				ApplyParsedMeshData( go, parsed );

			_lastWriteSeq[bridgeId] = blenderSeq;
			_lastKnown[bridgeId] = (go.WorldPosition, go.WorldRotation);
			StampAppliedMeshHash( bridgeId, go );

			var scene = BridgeSceneHelper.ResolveScene();
			if ( scene != null )
				BridgePersistence.SaveAfterChange( scene, bridgeId, go );

			BlenderBridgeServer.LogInfo( $"Chunked mesh assembled for {bridgeId} ({vertCount} verts)" );
			return "{\"ok\":true}";
		}

		// ── update_scene_transform ────────────────────────────────────────────

		private static string HandleUpdateSceneTransform( JsonElement root )
		{
			var sceneId = GetString( root, "sceneId" );
			if ( string.IsNullOrEmpty( sceneId ) ) return "{\"error\":\"missing sceneId\"}";
			var blenderSeq = GetInt( root, "seq", 0 );

			if ( !Guid.TryParse( sceneId, out var guid ) ) return "{\"error\":\"invalid sceneId\"}";

			var scene = BridgeSceneHelper.ResolveScene();
			if ( scene == null ) return "{\"error\":\"no scene\"}";

			GameObject go = null;
			foreach ( var root2 in scene.Children )
			{
				go = SearchTree( root2, g => g.Id == guid );
				if ( go != null ) break;
			}
			if ( go == null ) return "{\"error\":\"not found\"}";

			ApplyTransform( go, root );

			var key = $"scene_{sceneId}";
			_lastWriteSeq[key] = blenderSeq;
			_lastKnown[key] = (go.WorldPosition, go.WorldRotation);

			return "{\"ok\":true}";
		}

		// ── grid_changed ──────────────────────────────────────────────────────

		private static string HandleGridChanged( JsonElement root )
		{
			var gridSize = GetInt( root, "gridSize", 0 );
			if ( gridSize <= 0 ) return "{\"ok\":true}";

			// GridSpacing range is [0.125, 128]. Bridge range is [1, 256].
			// Clamp at the engine ceiling; the bridge property keeps the original value
			// so users see what they asked for, but the engine grid can't go above 128.
			float spacing = gridSize > 128 ? 128f : gridSize;
			_lastBroadcastGridSpacing = spacing;
			try
			{
				// EditorScene.GizmoSettings is the editor's live settings object — the
				// scene viewport assigns this exact instance into its GizmoInstance, and
				// the editor's own grid menu/[ ]/snap widgets read and write it.
				// Gizmo.Settings must NOT be used here: it resolves Active?.Settings,
				// and Gizmo.Active only exists inside a gizmo scope — from the
				// dispatcher it is null, so writes through it silently do nothing.
				EditorScene.GizmoSettings.GridSpacing = spacing;
			}
			catch ( Exception ex )
			{
				BlenderBridgeServer.LogInfo( $"grid_changed apply skipped: {ex.Message}" );
			}
			return "{\"ok\":true}";
		}

		/// <summary>
		/// Broadcast the editor's grid spacing to Blender as grid_updated and stamp
		/// the echo guard so the poll loop doesn't re-send the same value.
		/// </summary>
		private static void BroadcastGridSpacing( float spacing )
		{
			_lastBroadcastGridSpacing = spacing;
			int gridSize = (int)MathF.Round( spacing );
			if ( gridSize < 1 ) gridSize = 1;
			if ( gridSize > 256 ) gridSize = 256;
			BlenderBridgeServer.BroadcastWithSeq( new
			{
				type = "grid_updated",
				gridSize
			} );
		}

		// ── sync ──────────────────────────────────────────────────────────────

		private static string HandleSync( JsonElement root )
		{
			var scene = BridgeSceneHelper.ResolveScene();
			if ( scene == null ) return "{\"ok\":true}";

			// Parse what Blender knows about
			var blenderKnown = new HashSet<string>();
			if ( root.TryGetProperty( "knownObjects", out var known ) )
			{
				foreach ( var item in known.EnumerateArray() )
				{
					var bid = item.TryGetProperty( "bridgeId", out var b ) ? b.GetString() : null;
					if ( bid != null ) blenderKnown.Add( bid );
				}
			}

			var bridgeObjects = FindAllBridgeObjects( scene );
			var sboxIds = new HashSet<string>( bridgeObjects.Select( x => x.bridgeId ) );
			var objects = new List<object>();

			// Sync re-baselines _lastKnown to the objects' current state below,
			// so any armed echo guard is stale — clearing here means the first
			// edit after a manual sync always broadcasts instead of being
			// swallowed by a leftover guard.
			_lastWriteSeq.Clear();

			foreach ( var (bridgeId, go) in bridgeObjects )
			{
				_lastKnown[bridgeId] = (go.WorldPosition, go.WorldRotation);
				if ( !BridgeLockPolicy.AllowsOutbound( go ) ) continue;
				objects.Add( BuildObjectPayload( bridgeId, go ) );
			}

			// Include scene lights, models, and native MeshComponents
			foreach ( var go in BridgeSceneHelper.WalkAll( scene, true ) )
			{
				if ( go.Tags.TryGetAll().Any( tag => tag.StartsWith( "bridge_" ) ) )
					continue;

				var light = go.Components.GetAll().FirstOrDefault( c => c is Light ) as Light;
				if ( light != null )
				{
					objects.Add( BuildLightPayload( go, light ) );
					continue;
				}

				// Native MeshComponents placed in s&box — adopt as bridge objects
				var meshComp = go.Components.Get<MeshComponent>();
				if ( meshComp?.Mesh != null )
				{
					var adoptId = "b_" + Guid.NewGuid().ToString( "N" ).Substring( 0, 8 );
					go.Tags.Add( $"bridge_{adoptId}" );
					_bridgeObjectCache[adoptId] = go;
					_lastKnown[adoptId] = (go.WorldPosition, go.WorldRotation);
					objects.Add( BuildObjectPayload( adoptId, go ) );
					sboxIds.Add( adoptId );
					continue;
				}

				var anyModel = go.Components.GetAll()
					.FirstOrDefault( c => c.GetType().Name.Contains( "ModelRenderer" ) );
				if ( anyModel != null )
					objects.Add( BuildModelPayload( go, anyModel ) );
			}

			// Tell Blender to remove objects it has that s&box doesn't.
			// Only when the resolved scene is the one the change-scanner has
			// settled on — during a tab switch Blender's list reflects another
			// scene, and emitting deletes here would wipe the Blender side.
			if ( ReferenceEquals( scene, _lastScannedScene ) )
			{
				var staleInBlender = blenderKnown.Except( sboxIds );
				foreach ( var staleId in staleInBlender )
					objects.Add( new { type = "deleted", bridgeId = staleId } );
			}
			else
			{
				BlenderBridgeServer.LogInfo( "Sync: scene not settled — skipped stale-object reconciliation" );
			}

			BlenderBridgeServer.BroadcastWithSeq( new { type = "sync_response", objects } );

			// Grid handshake: Blender requests sync on connect, reconnect, and session
			// change. Push the editor's current grid spacing alongside the response so
			// both sides start aligned — change-detection alone never fires for a
			// client that reconnected after the last change.
			try
			{
				BroadcastGridSpacing( EditorScene.GizmoSettings.GridSpacing );
			}
			catch { }

			BlenderBridgeServer.LogInfo( $"Sync: {bridgeObjects.Count} bridge, {objects.Count} total" );
			return "{\"ok\":true}";
		}

		// ── Poll for s&box-side changes ───────────────────────────────────────

		internal static void PollForChanges()
		{
			try
			{
				PollForChangesInternal();
			}
			catch ( Exception ex )
			{
				BlenderBridgeServer.LogInfo( $"Poll cycle skipped: {ex.Message}" );
			}
		}

		// Last observed Game.IsPlaying, stamped on the main thread each scan tick.
		// Read by HandlePoll (background thread) so every poll response carries
		// play-mode ground truth — Blender syncs its play flag level-triggered
		// instead of trusting edge-triggered messages that can be missed.
		internal static bool IsPlayingSnapshot;

		// Scene the change-scanner ran against last tick. ResolveScene() follows
		// the ACTIVE editor tab — with multiple scenes open, a tab switch makes
		// every tracked object "vanish" from the scan and the deletion detector
		// would broadcast deletes for all of them (Blender then wipes its side).
		private static Scene _lastScannedScene;

		private static void PollForChangesInternal()
		{
			var scene = BridgeSceneHelper.ResolveScene();
			if ( scene == null ) return;

			if ( !ReferenceEquals( scene, _lastScannedScene ) )
			{
				bool firstScan = _lastScannedScene == null;
				_lastScannedScene = scene;
				if ( !firstScan )
				{
					_lastKnown.Clear();
					_lastMeshHash.Clear();
					_bridgeObjectCache.Clear();
					// Echo guards belong to the old scene's write history — a
					// stale armed key would swallow the first real edit made to
					// a same-ID object after switching back.
					_lastWriteSeq.Clear();
					BlenderBridgeServer.LogInfo( $"Scene changed to '{scene.Name}' — bridge tracking reset, deletes suppressed this tick" );
					return;
				}
			}

			// Play mode detection
			bool isPlaying = Game.IsPlaying;
			IsPlayingSnapshot = isPlaying;
			if ( isPlaying != _wasPlaying )
			{
				_wasPlaying = isPlaying;
				BlenderBridgeServer.BroadcastWithSeq( new
				{
					type = "play_mode",
					state = isPlaying ? "started" : "stopped"
				} );

				if ( isPlaying )
				{
					// Entering play mode wipes editor meshes — everything
					// queued must hit disk NOW or the restore has stale data.
					try { BridgePersistence.FlushPendingSaves( scene, force: true ); }
					catch { }
				}

				if ( !isPlaying )
				{
					// Exiting play mode — restore cached meshes
					try { BridgePersistence.RestoreFromCache( scene ); }
					catch { }
				}
			}

			// Clean up timed-out chunk accumulators
			var timedOut = _pendingChunks
				.Where( kv => (DateTime.UtcNow - kv.Value.StartTime).TotalSeconds > 30 )
				.Select( kv => kv.Key ).ToList();
			foreach ( var key in timedOut )
				_pendingChunks.Remove( key );

			// Grid-spacing change detection: if the editor user changed the grid spacing
			// (menu, [ ] shortcuts, snap widget), broadcast it to Blender.
			// _lastBroadcastGridSpacing is stamped on inbound grid_changed too, so a
			// bridge-driven write doesn't echo back. Read EditorScene.GizmoSettings —
			// NOT Gizmo.Settings, which is null outside a gizmo scope.
			try
			{
				float spacing = EditorScene.GizmoSettings.GridSpacing;
				if ( MathF.Abs( spacing - _lastBroadcastGridSpacing ) > 0.001f )
					BroadcastGridSpacing( spacing );
			}
			catch { }

			var bridgeObjects = FindAllBridgeObjects( scene );
			var currentIds = new HashSet<string>();

			// Mesh-hash amortization: hashing every mesh every tick is
			// per-vertex work × object count × 5Hz on the editor main thread —
			// the scan's scale cliff. Round-robin so at most MeshHashBudget
			// objects hash per tick; each object is still checked every
			// ceil(n/budget) ticks (~1s at 60 objects). Safe because every
			// mesh-apply handler stamps _lastMeshHash itself, so a delayed
			// check can never mistake our own write for a user edit after the
			// echo guard was consumed.
			_scanTick++;
			int hashSlices = Math.Max( 1, (bridgeObjects.Count + MeshHashBudget - 1) / MeshHashBudget );
			int objIdx = -1;

			foreach ( var (bridgeId, go) in bridgeObjects )
			{
				objIdx++;
				if ( go == null || !go.IsValid ) continue;

				// Editor-side duplication (shift-drag, Ctrl+D) copies the GameObject
				// together with its bridge tag, leaving two objects claiming one ID.
				// The scan then flip-flops _lastKnown between their transforms and
				// broadcasts an "updated" storm every tick — which also keeps
				// Blender's echo-suppression window open so Blender->engine moves
				// stop applying. Re-tag the copy with a fresh ID and announce it
				// to Blender as a new object.
				if ( !currentIds.Add( bridgeId ) )
				{
					go.Tags.Remove( $"bridge_{bridgeId}" );
					var dupId = "b_" + Guid.NewGuid().ToString( "N" ).Substring( 0, 8 );
					go.Tags.Add( $"bridge_{dupId}" );
					currentIds.Add( dupId );
					_bridgeObjectCache.Remove( bridgeId ); // may point at the re-tagged copy
					_bridgeObjectCache[dupId] = go;
					_lastKnown[dupId] = (go.WorldPosition, go.WorldRotation);

					var dupMeshComp = go.Components.Get<MeshComponent>();
					if ( dupMeshComp?.Mesh != null )
					{
						_lastMeshHash[dupId] = ComputeMeshGeometryHash( dupMeshComp.Mesh );
						var dupExtracted = ExtractMeshData( dupMeshComp.Mesh );
						object dupMd = null;
						if ( dupExtracted != null )
							dupMd = new { vertices = dupExtracted.Value.Vertices, faces = dupExtracted.Value.Faces };
						var dupPos = go.WorldPosition;
						var dupRot = go.WorldRotation.Angles();
						// sourceBridgeId: the wire meshData is geometry-only, so
						// Blender clones materials/UVs from its own copy of the
						// source object instead of creating the dup bare — a bare
						// dup used to echo a material-less mesh update back and
						// wipe THIS object's textures.
						BlenderBridgeServer.BroadcastWithSeq( new
						{
							type = "object_created",
							bridgeId = dupId,
							sourceBridgeId = bridgeId,
							name = go.Name,
							position = new { x = dupPos.x, y = dupPos.y, z = dupPos.z },
							rotation = new { pitch = dupRot.pitch, yaw = dupRot.yaw, roll = dupRot.roll },
							meshData = dupMd
						} );
					}
					BlenderBridgeServer.LogInfo( $"Duplicated {bridgeId} re-tagged as {dupId}" );
					continue;
				}

				// Undo of a delete restores the GameObject with its bridge tag
				// intact — Blender was already told to delete this id, so
				// re-announce it as a creation instead of silently re-tracking.
				if ( _recentlyDeleted.Remove( bridgeId ) )
				{
					_bridgeObjectCache[bridgeId] = go;
					_lastKnown[bridgeId] = (go.WorldPosition, go.WorldRotation);
					var undoMesh = go.Components.Get<MeshComponent>();
					if ( undoMesh?.Mesh != null )
						_lastMeshHash[bridgeId] = ComputeMeshGeometryHash( undoMesh.Mesh );
					BlenderBridgeServer.BroadcastWithSeq( BuildObjectCreatedMessage( bridgeId, go ) );
					BlenderBridgeServer.LogInfo( $"Restored {bridgeId} ('{go.Name}') — re-announced to Blender" );
					continue;
				}

				// Outbound lock: skip pushing changes to Blender for this object,
				// but keep tracking it (so deletes still propagate, etc).
				if ( !BridgeLockPolicy.AllowsOutbound( go ) )
				{
					_lastKnown[bridgeId] = (go.WorldPosition, go.WorldRotation);
					continue;
				}

				var pos = go.WorldPosition;
				var rot = go.WorldRotation;

				bool posChanged = false;
				bool rotChanged = false;

				if ( _lastKnown.TryGetValue( bridgeId, out var prev ) )
				{
					posChanged = MathF.Abs( pos.x - prev.pos.x ) > 0.01f
						|| MathF.Abs( pos.y - prev.pos.y ) > 0.01f
						|| MathF.Abs( pos.z - prev.pos.z ) > 0.01f;
					rotChanged = !rot.Equals( prev.rot );
				}

				// Check mesh changes — hash vertex positions so moves/pulls are
				// detected. Only this object's round-robin slice hashes this tick.
				bool meshChanged = false;
				var meshComp = go.Components.Get<MeshComponent>();
				if ( meshComp?.Mesh != null && (objIdx % hashSlices) == (_scanTick % hashSlices) )
				{
					var meshHash = ComputeMeshGeometryHash( meshComp.Mesh );
					if ( _lastMeshHash.TryGetValue( bridgeId, out var prevHash ) && prevHash != meshHash )
						meshChanged = true;
					_lastMeshHash[bridgeId] = meshHash;
				}

				// Echo suppression: consume the guard every tick, delta or not.
				// Handlers stamp _lastKnown from the object AFTER applying a
				// Blender write, so the echo usually produces no delta here —
				// consuming only inside the delta branch leaves the key armed
				// indefinitely, where it silently swallows the user's next
				// genuine s&box edit (undo, typed coordinate, snap). Dispatch
				// and poll are serialized on the main thread, so any echo delta
				// that does surface (mesh hash, transform quantization) is
				// visible by the tick right after the write — a one-tick
				// lifetime is enough.
				bool suppressEcho = _lastWriteSeq.Remove( bridgeId );

				if ( (posChanged || rotChanged || meshChanged) && !suppressEcho )
				{
					var angles = rot.Angles();
					if ( meshChanged )
					{
						var extracted = ExtractMeshData( meshComp.Mesh );
						object md = null;
						if ( extracted != null )
							md = new { vertices = extracted.Value.Vertices, faces = extracted.Value.Faces };

						BlenderBridgeServer.BroadcastWithSeq( new
						{
							type = "mesh_updated",
							bridgeId,
							position = new { x = pos.x, y = pos.y, z = pos.z },
							rotation = new { pitch = angles.pitch, yaw = angles.yaw, roll = angles.roll },
							meshData = md
						} );
					}
					else
					{
						BlenderBridgeServer.BroadcastWithSeq( new
						{
							type = "updated",
							bridgeId,
							position = new { x = pos.x, y = pos.y, z = pos.z },
							rotation = new { pitch = angles.pitch, yaw = angles.yaw, roll = angles.roll }
						} );
					}
				}

				_lastKnown[bridgeId] = (pos, rot);
			}

			// Detect deletions
			foreach ( var oldId in _lastKnown.Keys.ToList() )
			{
				if ( oldId.StartsWith( "scene_" ) ) continue;
				if ( !currentIds.Contains( oldId ) )
				{
					_lastKnown.Remove( oldId );
					_lastWriteSeq.Remove( oldId );
					_lastMeshHash.Remove( oldId );
					_bridgeObjectCache.Remove( oldId );
					_recentlyDeleted.Add( oldId );
					BlenderBridgeServer.BroadcastWithSeq( new { type = "deleted", bridgeId = oldId } );
				}
			}

			// Track scene objects (models/lights) and auto-adopt native MeshComponents
			foreach ( var go in BridgeSceneHelper.WalkAll( scene, true ) )
			{
				if ( go == null || !go.IsValid ) continue;
				if ( go.Tags.TryGetAll().Any( tag => tag.StartsWith( "bridge_" ) ) )
					continue;

				// Auto-adopt native MeshComponents that aren't bridge-tagged yet
				var nativeMesh = go.Components.Get<MeshComponent>();
				if ( nativeMesh?.Mesh != null )
				{
					var adoptId = "b_" + Guid.NewGuid().ToString( "N" ).Substring( 0, 8 );
					go.Tags.Add( $"bridge_{adoptId}" );
					_bridgeObjectCache[adoptId] = go;
					_lastKnown[adoptId] = (go.WorldPosition, go.WorldRotation);
					_lastMeshHash[adoptId] = ComputeMeshGeometryHash( nativeMesh.Mesh );

					// Broadcast the new object to Blender as a creation event
					var adoptPos = go.WorldPosition;
					var adoptRot = go.WorldRotation.Angles();
					var extracted = ExtractMeshData( nativeMesh.Mesh );
					object meshData = null;
					if ( extracted != null )
						meshData = new { vertices = extracted.Value.Vertices, faces = extracted.Value.Faces };

					BlenderBridgeServer.BroadcastWithSeq( new
					{
						type = "object_created",
						bridgeId = adoptId,
						name = go.Name,
						position = new { x = adoptPos.x, y = adoptPos.y, z = adoptPos.z },
						rotation = new { pitch = adoptRot.pitch, yaw = adoptRot.yaw, roll = adoptRot.roll },
						meshData
					} );
					BlenderBridgeServer.LogInfo( $"Auto-adopted native mesh '{go.Name}' as {adoptId}" );
					continue;
				}

				var hasModel = go.Components.GetAll().Any( c => c.GetType().Name.Contains( "ModelRenderer" ) );
				var hasLight = go.Components.GetAll().Any( c => c is Light );
				if ( !hasModel && !hasLight ) continue;

				var key = $"scene_{go.Id}";
				var pos = go.WorldPosition;
				var rot = go.WorldRotation;

				if ( _lastKnown.TryGetValue( key, out var prevScene ) )
				{
					bool sceneChanged = MathF.Abs( pos.x - prevScene.pos.x ) > 0.01f
						|| MathF.Abs( pos.y - prevScene.pos.y ) > 0.01f
						|| MathF.Abs( pos.z - prevScene.pos.z ) > 0.01f;

					if ( sceneChanged && !_lastWriteSeq.ContainsKey( key ) )
					{
						var angles = rot.Angles();
						BlenderBridgeServer.BroadcastWithSeq( new
						{
							type = "scene_updated",
							sceneId = go.Id.ToString(),
							position = new { x = pos.x, y = pos.y, z = pos.z },
							rotation = new { pitch = angles.pitch, yaw = angles.yaw, roll = angles.roll }
						} );
					}
					else if ( _lastWriteSeq.ContainsKey( key ) )
					{
						_lastWriteSeq.Remove( key );
					}
				}

				_lastKnown[key] = (pos, rot);
			}

			// Write out queued mesh caches + manifest once things go quiet.
			BridgePersistence.FlushPendingSaves( scene );
		}

		// ── Hierarchy grouping ────────────────────────────────────────────────

		/// <summary>Find or create the "Blender Bridge" parent object identified by tag.</summary>
		private static GameObject GetOrCreateBridgeGroup( Scene scene )
		{
			// Search by tag (survives renames)
			foreach ( var root in scene.Children )
			{
				var found = SearchTree( root, g => g.Tags.Has( "bridge_group" ) );
				if ( found != null ) return found;
			}

			// Create new group
			var go = scene.CreateObject();
			go.Name = "Blender Bridge";
			go.Tags.Add( "bridge_group" );
			return go;
		}

		/// <summary>Find or create a child GameObject by name under a parent.
		/// Used to build hierarchy from Blender collection paths.</summary>
		private static GameObject GetOrCreateChild( GameObject parent, string name )
		{
			// Search existing children
			foreach ( var child in parent.Children )
			{
				if ( child.Name == name )
					return child;
			}

			// Create new empty child
			var scene = BridgeSceneHelper.ResolveScene();
			if ( scene == null ) return parent;

			var go = scene.CreateObject();
			go.Name = name;
			go.Parent = parent;
			return go;
		}

		// ── Object finders ────────────────────────────────────────────────────

		/// <summary>True if this tag carries a bridge identity (bridge_&lt;id&gt;).
		/// The group marker and the lock tags (bridge_locked, bridge_lock_&lt;flags&gt;)
		/// share the prefix but are not identity — treating them as one yields
		/// phantom ids like "lock_4" shared by every object with the same flags.</summary>
		internal static bool IsBridgeIdentityTag( string tag )
			=> tag.StartsWith( "bridge_" ) && tag != "bridge_group"
				&& tag != BridgeLockPolicy.LegacyLockTag
				&& !tag.StartsWith( BridgeLockPolicy.FlagTagPrefix );

		/// <summary>Client-supplied bridge IDs become GameObject tags — accept
		/// only the addon's own "b_" + lowercase-hex format so a malformed
		/// value can't smuggle arbitrary tag content (or collide with the
		/// group/lock tag namespace).</summary>
		private static bool IsValidClientBridgeId( string id )
		{
			if ( string.IsNullOrEmpty( id ) || id.Length < 4 || id.Length > 40 ) return false;
			if ( !id.StartsWith( "b_" ) ) return false;
			for ( int i = 2; i < id.Length; i++ )
			{
				var c = id[i];
				if ( !((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')) ) return false;
			}
			return true;
		}

		/// <summary>Find a bridge object by ID. Uses cache, falls back to tree walk.</summary>
		private static GameObject FindByBridgeTag( string bridgeId )
		{
			// Cache hit
			if ( _bridgeObjectCache.TryGetValue( bridgeId, out var cached ) && cached != null && cached.IsValid )
				return cached;

			// Cache miss — walk the scene
			var scene = BridgeSceneHelper.ResolveScene();
			if ( scene == null ) return null;

			var tag = $"bridge_{bridgeId}";
			foreach ( var root in scene.Children )
			{
				var found = SearchTree( root, tag );
				if ( found != null )
				{
					_bridgeObjectCache[bridgeId] = found;
					return found;
				}
			}
			return null;
		}

		private static GameObject SearchTree( GameObject node, string tag )
		{
			if ( node.Tags.Has( tag ) ) return node;
			foreach ( var child in node.Children )
			{
				var found = SearchTree( child, tag );
				if ( found != null ) return found;
			}
			return null;
		}

		private static GameObject SearchTree( GameObject node, Func<GameObject, bool> predicate )
		{
			if ( predicate( node ) ) return node;
			foreach ( var child in node.Children )
			{
				var found = SearchTree( child, predicate );
				if ( found != null ) return found;
			}
			return null;
		}

		private static List<(string bridgeId, GameObject go)> FindAllBridgeObjects( Scene scene )
		{
			var result = new List<(string, GameObject)>();
			foreach ( var root in scene.Children )
				CollectBridgeObjects( root, result );
			return result;
		}

		private static void CollectBridgeObjects( GameObject node, List<(string, GameObject)> result )
		{
			foreach ( var tag in node.Tags.TryGetAll() )
			{
				if ( IsBridgeIdentityTag( tag ) )
				{
					var bridgeId = tag.Substring( 7 );
					result.Add( (bridgeId, node) );
					_bridgeObjectCache[bridgeId] = node; // Keep cache warm
					break;
				}
			}
			foreach ( var child in node.Children )
				CollectBridgeObjects( child, result );
		}

		/// <summary>Read-only diagnostic snapshot for GET /debug.</summary>
		internal static string DumpDebugState()
		{
			try
			{
				var scene = BridgeSceneHelper.ResolveScene();
				var objs = new List<object>();
				if ( scene != null )
				{
					foreach ( var go in BridgeSceneHelper.WalkAll( scene, true ) )
					{
						if ( go == null || !go.IsValid ) continue;
						var bridgeTags = go.Tags.TryGetAll()
							.Where( t => t.StartsWith( "bridge_" ) ).ToList();
						if ( bridgeTags.Count == 0 ) continue;
						var p = go.WorldPosition;
						objs.Add( new
						{
							name = go.Name,
							tags = bridgeTags,
							position = new { x = p.x, y = p.y, z = p.z }
						} );
					}
				}
				return JsonSerializer.Serialize( new
				{
					scene = scene?.Name,
					playing = Game.IsPlaying,
					objects = objs,
					lastKnown = _lastKnown.Keys.ToList(),
					lastWriteSeq = _lastWriteSeq.Keys.ToList(),
					lastMeshHash = _lastMeshHash.Keys.ToList(),
					idempotencyKeys = _idempotencyKeys.Count,
				}, BlenderBridgeServer.JsonOptions );
			}
			catch ( Exception ex )
			{
				return JsonSerializer.Serialize( new { error = ex.Message }, BlenderBridgeServer.JsonOptions );
			}
		}

		// ── Light properties ──────────────────────────────────────────────────

		private static void ApplyLightProperties( GameObject go, JsonElement root )
		{
			if ( !root.TryGetProperty( "properties", out var propsEl ) )
				return;

			var light = go.Components.GetAll().FirstOrDefault( c => c is Light ) as Light;
			if ( light == null ) return;

			if ( propsEl.TryGetProperty( "color", out var colorEl ) )
			{
				var r = GetFloat( colorEl, "r", 1f );
				var g = GetFloat( colorEl, "g", 1f );
				var b = GetFloat( colorEl, "b", 1f );
				light.LightColor = new Color( r, g, b );
			}

			if ( light is PointLight point )
			{
				if ( propsEl.TryGetProperty( "radius", out var radiusEl ) )
					point.Radius = radiusEl.GetSingle();
			}
			else if ( light is SpotLight spot )
			{
				if ( propsEl.TryGetProperty( "radius", out var radiusEl ) )
					spot.Radius = radiusEl.GetSingle();
				if ( propsEl.TryGetProperty( "coneOuter", out var outerEl ) )
					spot.ConeOuter = outerEl.GetSingle();
				if ( propsEl.TryGetProperty( "coneInner", out var innerEl ) )
					spot.ConeInner = innerEl.GetSingle();
			}
		}

		// ── Mesh handling ─────────────────────────────────────────────────────

		private struct ParsedMesh
		{
			public Vector3[] Vertices;
			public List<int[]> FaceGroups;
			public int[] FaceMaterials;
			public List<MaterialDef> Materials;
			/// <summary>Per-face Blender-authored UVs, parallel to FaceGroups.
			/// Each entry has one Vector2 per face vertex. Null when Blender
			/// didn't send a UV layer; the apply path falls back to grid-align.</summary>
			public List<Vector2[]> FaceUVs;
		}

		private struct MaterialDef
		{
			public string Name;
			public float[] BaseColor;
			public float Metallic;
			public float Roughness;
			public string BaseColorTexture;
			public string RoughnessTexture;
			public string MetallicTexture;
			public string NormalTexture;
			public float NormalStrength;
			public float[] EmissionColor;
			public float EmissionStrength;
			public string VmatPath;
		}

		private static void ApplyMeshData( GameObject go, JsonElement meshData )
		{
			var parsed = ParseMeshData( meshData );
			if ( parsed == null ) return;
			ApplyParsedMeshData( go, parsed.Value );
		}

		/// <summary>
		/// Pulls the material off the first face of the GameObject's existing
		/// mesh. Used to preserve manually-authored materials (water shaders,
		/// custom vmats) when Blender resyncs without supplying material info.
		/// Returns null if there's no mesh, no faces, or only the dev default
		/// material is present (in which case the caller's fallback is fine).
		/// </summary>
		private static Material TryGetExistingDominantMaterial( GameObject go )
		{
			try
			{
				var mesh = go?.Components.Get<MeshComponent>()?.Mesh;
				if ( mesh == null ) return null;

				var getFaceMatMeth = mesh.GetType().GetMethod( "GetFaceMaterial", new[] { typeof( FaceHandle ) } );
				if ( getFaceMatMeth == null ) return null;

				foreach ( var fh in mesh.FaceHandles )
				{
					var result = getFaceMatMeth.Invoke( mesh, new object[] { fh } );
					if ( result is Material m && m != null )
					{
						// Skip the dev placeholder — using it as the "preserve"
						// fallback means we'd never escape it once it's been set.
						var path = m.ResourcePath ?? "";
						if ( path.EndsWith( "reflectivity_30.vmat", StringComparison.OrdinalIgnoreCase ) ||
							 path.EndsWith( "reflectivity_30.vmat_c", StringComparison.OrdinalIgnoreCase ) )
							return null;
						return m;
					}
					break;
				}
			}
			catch { }
			return null;
		}

		private static void ApplyParsedMeshData( GameObject go, ParsedMesh parsed )
		{
			var faceMaterials = new Material[parsed.FaceGroups.Count];
			var defaultMaterial = LoadMaterialSafe( "materials/dev/reflectivity_30.vmat" );

			// Materials lock: preserve whatever's already on the object regardless
			// of what Blender supplied. Skips the entire material-resolution path
			// and falls through to the existing-material preservation branch.
			bool forceMaterialPreserve = !BridgeLockPolicy.AllowsMaterialChange( go );

			if ( !forceMaterialPreserve && parsed.Materials != null && parsed.FaceMaterials != null )
			{
				var materialCache = new Dictionary<int, Material>();
				for ( int fi = 0; fi < parsed.FaceGroups.Count && fi < parsed.FaceMaterials.Length; fi++ )
				{
					var matIdx = parsed.FaceMaterials[fi];
					if ( !materialCache.ContainsKey( matIdx ) )
					{
						Material mat = null;
						if ( matIdx < parsed.Materials.Count )
						{
							var vmatPath = parsed.Materials[matIdx].VmatPath;
							if ( !string.IsNullOrEmpty( vmatPath ) )
								mat = LoadMaterialSafe( vmatPath );
							if ( mat == null )
								mat = GenerateOrLoadMaterial( parsed.Materials[matIdx] );
						}
						materialCache[matIdx] = mat ?? defaultMaterial;
					}
					faceMaterials[fi] = materialCache[matIdx];
				}
			}
			else
			{
				// Blender sent geometry but no material info. Before stamping
				// the dev placeholder over every face, try to preserve whatever
				// material is already on this GameObject's mesh — water shaders,
				// custom vmats, anything authored on the s&box side.
				var preserved = TryGetExistingDominantMaterial( go );
				var fallback = preserved ?? defaultMaterial;
				for ( int i = 0; i < faceMaterials.Length; i++ )
					faceMaterials[i] = fallback;
				if ( preserved != null )
					BlenderBridgeServer.LogInfo( $"Preserved existing material on '{go.Name}' (Blender sent no materials)" );
			}

			var mesh = new PolygonMesh();
			var hVertices = mesh.AddVertices( parsed.Vertices );

			bool hasBlenderUVs = parsed.FaceUVs != null && parsed.FaceUVs.Count == parsed.FaceGroups.Count;

			int faceIdx = 0;
			foreach ( var faceGroup in parsed.FaceGroups )
			{
				var faceVerts = faceGroup
					.Where( fi => fi >= 0 && fi < hVertices.Length )
					.Select( fi => hVertices[fi] )
					.ToArray();
				if ( faceVerts.Length >= 3 )
				{
					var hFace = mesh.AddFace( faceVerts );
					mesh.SetFaceMaterial( hFace, faceIdx < faceMaterials.Length ? faceMaterials[faceIdx] : defaultMaterial );
					if ( hasBlenderUVs && faceIdx < parsed.FaceUVs.Count )
					{
						// Use Blender-authored UVs verbatim. SetFaceTextureCoords
						// also recomputes the face's texture parameters from the
						// new coords, so the planar projection follows the UV
						// layout instead of fighting it.
						mesh.SetFaceTextureCoords( hFace, parsed.FaceUVs[faceIdx] );
					}
				}
				faceIdx++;
			}

			// Only fall back to grid-aligned auto-UVs if Blender didn't send a
			// UV layer. Calling TextureAlignToGrid when we have real UVs would
			// stomp them.
			if ( !hasBlenderUVs )
				mesh.TextureAlignToGrid( mesh.Transform );
			mesh.SetSmoothingAngle( 40.0f );

			// Defense in depth: never replace a Terrain component with a
			// MeshComponent. If the lock check earlier in the dispatch flow was
			// bypassed (e.g. an addon bug, a missing tag), this still blocks the
			// destructive path.
			if ( go.Components.Get<Terrain>() != null )
			{
				BlenderBridgeServer.LogInfo( $"Skipped ApplyMeshData on '{go.Name}' — has Terrain component" );
				return;
			}

			var existingMr = go.Components.Get<ModelRenderer>();
			if ( existingMr != null )
				existingMr.Destroy();

			var meshComp = go.Components.Get<MeshComponent>();
			if ( meshComp == null )
				meshComp = go.Components.Create<MeshComponent>();

			meshComp.Mesh = mesh;
		}

		// ── Material generation ───────────────────────────────────────────────

		private static Material GenerateOrLoadMaterial( MaterialDef def )
		{
			var safeName = System.Text.RegularExpressions.Regex.Replace(
				def.Name ?? "default", @"[^a-zA-Z0-9_\-]", "_" ).ToLower();
			var vmatRelPath = $"materials/blender_bridge/{safeName}.vmat";

			var assetsDir = GetProjectAssetsDir();
			if ( assetsDir == null ) return null;

			var bridgeMatDir = System.IO.Path.Combine( assetsDir, "materials", "blender_bridge" );
			System.IO.Directory.CreateDirectory( bridgeMatDir );

			var colorTexRef = CopyTextureToAssets( def.BaseColorTexture, safeName, "color", assetsDir );
			var roughTexRef = CopyTextureToAssets( def.RoughnessTexture, safeName, "rough", assetsDir );
			var metalTexRef = CopyTextureToAssets( def.MetallicTexture, safeName, "metal", assetsDir );
			var normalTexRef = CopyTextureToAssets( def.NormalTexture, safeName, "normal", assetsDir );

			var sb = new System.Text.StringBuilder();
			sb.AppendLine( "// AUTO-GENERATED BY BLENDER BRIDGE" );
			sb.AppendLine();
			sb.AppendLine( "Layer0" );
			sb.AppendLine( "{" );
			sb.AppendLine( "\tshader \"shaders/complex.shader\"" );
			sb.AppendLine();
			if ( metalTexRef != null ) sb.AppendLine( "\tF_METALNESS_TEXTURE 1" );
			sb.AppendLine( "\tF_SPECULAR 1" );
			sb.AppendLine();

			var r = def.BaseColor?.Length >= 3 ? def.BaseColor[0] : 0.8f;
			var g = def.BaseColor?.Length >= 3 ? def.BaseColor[1] : 0.8f;
			var b = def.BaseColor?.Length >= 3 ? def.BaseColor[2] : 0.8f;
			// A color texture drives albedo — Blender's Base Color value is
			// whatever was set before the texture was linked, and tinting by
			// it multiplies the texture by that stale color.
			if ( colorTexRef != null ) { r = 1f; g = 1f; b = 1f; }
			sb.AppendLine( $"\tg_flModelTintAmount \"1.000\"" );
			sb.AppendLine( $"\tg_vColorTint \"[{r:F6} {g:F6} {b:F6} 0.000000]\"" );
			if ( colorTexRef != null ) sb.AppendLine( $"\tTextureColor \"{colorTexRef}\"" );
			sb.AppendLine();
			sb.AppendLine( $"\tg_flMetalness \"{def.Metallic:F3}\"" );
			if ( metalTexRef != null ) sb.AppendLine( $"\tTextureMetalness \"{metalTexRef}\"" );
			sb.AppendLine();
			sb.AppendLine( $"\tg_flRoughnessScaleFactor \"{def.Roughness:F3}\"" );
			if ( roughTexRef != null ) sb.AppendLine( $"\tTextureRoughness \"{roughTexRef}\"" );
			sb.AppendLine();
			if ( normalTexRef != null ) { sb.AppendLine( $"\tTextureNormal \"{normalTexRef}\"" ); sb.AppendLine(); }
			if ( def.EmissionStrength > 0.001f )
			{
				var er = def.EmissionColor?.Length >= 3 ? def.EmissionColor[0] : 0f;
				var eg = def.EmissionColor?.Length >= 3 ? def.EmissionColor[1] : 0f;
				var eb = def.EmissionColor?.Length >= 3 ? def.EmissionColor[2] : 0f;
				sb.AppendLine( $"\tg_vSelfIllumTint \"[{er:F6} {eg:F6} {eb:F6} 0.000000]\"" );
				sb.AppendLine( $"\tg_flSelfIllumScale \"{def.EmissionStrength:F3}\"" );
				sb.AppendLine();
			}
			sb.AppendLine( "\tg_vTexCoordScale \"[1.000 1.000]\"" );
			sb.AppendLine( "\tg_vTexCoordOffset \"[0.000 0.000]\"" );
			sb.AppendLine( "}" );

			var vmatPath = System.IO.Path.Combine( assetsDir, vmatRelPath.Replace( "/", "\\" ) );
			System.IO.File.WriteAllText( vmatPath, sb.ToString() );

			// Register AND compile, otherwise Material.Load returns null on the
			// first call (no .vmat_c on disk yet) and we silently fall through
			// to the dev placeholder. Compile(false) is incremental and
			// synchronous — by the time it returns, the compiled resource
			// exists and Material.Load can resolve it. Re-registering an
			// already-known path is a no-op.
			try
			{
				var asset = AssetSystem.RegisterFile( vmatPath );
				asset?.Compile( full: false );
			}
			catch ( Exception ex )
			{
				BlenderBridgeServer.LogInfo( $"Vmat register/compile failed for {vmatPath}: {ex.Message}" );
			}

			return LoadMaterialSafe( vmatRelPath ) ?? LoadMaterialSafe( "materials/dev/reflectivity_30.vmat" );
		}

		private static string GenerateVmatPath( MaterialDef def )
		{
			var safeName = System.Text.RegularExpressions.Regex.Replace(
				def.Name ?? "default", @"[^a-zA-Z0-9_\-]", "_" ).ToLower();
			return $"materials/blender_bridge/{safeName}.vmat";
		}

		private static string CopyTextureToAssets( string srcPath, string matName, string suffix, string assetsDir )
		{
			if ( string.IsNullOrEmpty( srcPath ) || !System.IO.File.Exists( srcPath ) )
				return null;
			var ext = System.IO.Path.GetExtension( srcPath );
			var destName = $"{matName}_{suffix}{ext}";
			var destRelPath = $"materials/blender_bridge/{destName}";
			var destAbsPath = System.IO.Path.Combine( assetsDir, destRelPath.Replace( "/", "\\" ) );
			try
			{
				System.IO.File.Copy( srcPath, destAbsPath, overwrite: true );
				// Register + compile so the vmat that references this texture
				// can find a resolved .vtex_c on disk. Without compile, the
				// material loads but renders untextured.
				try
				{
					var asset = AssetSystem.RegisterFile( destAbsPath );
					asset?.Compile( full: false );
				}
				catch ( Exception cex )
				{
					BlenderBridgeServer.LogInfo( $"Texture register/compile failed for {destAbsPath}: {cex.Message}" );
				}
				return destRelPath;
			}
			catch ( Exception ex )
			{
				BlenderBridgeServer.LogInfo( $"Texture copy failed ({srcPath}): {ex.Message}" );
				return null;
			}
		}

		// ── Mesh parsing ──────────────────────────────────────────────────────

		private static ParsedMesh? ParseMeshData( JsonElement meshData )
		{
			if ( !meshData.TryGetProperty( "vertices", out var vertsEl ) ) return null;
			if ( !meshData.TryGetProperty( "faces", out var facesEl ) ) return null;

			var vertFloats = new List<float>();
			foreach ( var v in vertsEl.EnumerateArray() ) vertFloats.Add( v.GetSingle() );
			if ( vertFloats.Count < 9 ) return null;

			var vertCount = vertFloats.Count / 3;
			var vertices = new Vector3[vertCount];
			for ( int i = 0; i < vertCount; i++ )
				vertices[i] = new Vector3( vertFloats[i * 3], vertFloats[i * 3 + 1], vertFloats[i * 3 + 2] );

			var rawFaces = new List<int>();
			foreach ( var f in facesEl.EnumerateArray() ) rawFaces.Add( f.GetInt32() );

			var faceGroups = new List<int[]>();
			int idx = 0;
			while ( idx < rawFaces.Count )
			{
				int faceVertCount = rawFaces[idx++];
				if ( idx + faceVertCount > rawFaces.Count ) break;
				var face = new int[faceVertCount];
				for ( int i = 0; i < faceVertCount; i++ ) face[i] = rawFaces[idx++];
				faceGroups.Add( face );
			}

			int[] faceMaterials = null;
			if ( meshData.TryGetProperty( "faceMaterials", out var fmEl ) )
			{
				var fmList = new List<int>();
				foreach ( var fm in fmEl.EnumerateArray() ) fmList.Add( fm.GetInt32() );
				faceMaterials = fmList.ToArray();
			}

			List<MaterialDef> materials = null;
			if ( meshData.TryGetProperty( "materials", out var matsEl ) )
				materials = ParseMaterialDefs( matsEl );

			// Optional per-face-corner UVs. Flat [u,v,u,v,...] aligned with
			// faceGroups: face N's UVs are the next (face.Length) pairs.
			List<Vector2[]> faceUVs = null;
			if ( meshData.TryGetProperty( "faceUVs", out var uvEl ) )
				faceUVs = ParseFaceUVs( uvEl, faceGroups );

			return new ParsedMesh
			{
				Vertices = vertices,
				FaceGroups = faceGroups,
				FaceMaterials = faceMaterials,
				Materials = materials,
				FaceUVs = faceUVs
			};
		}

		/// <summary>
		/// Parse a flat [u,v,u,v,...] array into per-face UV sets aligned with
		/// faceGroups: face N's UVs are the next (face.Length) pairs.
		/// </summary>
		private static List<Vector2[]> ParseFaceUVs( JsonElement uvEl, List<int[]> faceGroups )
		{
			var uvFloats = new List<float>();
			foreach ( var u in uvEl.EnumerateArray() ) uvFloats.Add( u.GetSingle() );
			var faceUVs = new List<Vector2[]>( faceGroups.Count );
			int uvIdx = 0;
			foreach ( var face in faceGroups )
			{
				var uvs = new Vector2[face.Length];
				for ( int i = 0; i < face.Length && uvIdx + 1 < uvFloats.Count; i++ )
				{
					uvs[i] = new Vector2( uvFloats[uvIdx], uvFloats[uvIdx + 1] );
					uvIdx += 2;
				}
				faceUVs.Add( uvs );
			}
			return faceUVs;
		}

		private static List<MaterialDef> ParseMaterialDefs( JsonElement matsEl )
		{
			var materials = new List<MaterialDef>();
			foreach ( var matEl in matsEl.EnumerateArray() )
			{
				var def = new MaterialDef
				{
					Name = matEl.TryGetProperty( "name", out var n ) ? n.GetString() : "default",
					Metallic = matEl.TryGetProperty( "metallic", out var met ) ? met.GetSingle() : 0f,
					Roughness = matEl.TryGetProperty( "roughness", out var rough ) ? rough.GetSingle() : 0.5f,
					NormalStrength = matEl.TryGetProperty( "normalStrength", out var ns ) ? ns.GetSingle() : 1f,
					EmissionStrength = matEl.TryGetProperty( "emissionStrength", out var es ) ? es.GetSingle() : 0f,
					BaseColorTexture = matEl.TryGetProperty( "baseColorTexture", out var bct ) && bct.ValueKind == JsonValueKind.String ? bct.GetString() : null,
					RoughnessTexture = matEl.TryGetProperty( "roughnessTexture", out var rt ) && rt.ValueKind == JsonValueKind.String ? rt.GetString() : null,
					MetallicTexture = matEl.TryGetProperty( "metallicTexture", out var mt ) && mt.ValueKind == JsonValueKind.String ? mt.GetString() : null,
					NormalTexture = matEl.TryGetProperty( "normalTexture", out var nt ) && nt.ValueKind == JsonValueKind.String ? nt.GetString() : null,
					VmatPath = matEl.TryGetProperty( "vmatPath", out var vp ) && vp.ValueKind == JsonValueKind.String ? vp.GetString() : null,
				};

				if ( matEl.TryGetProperty( "baseColor", out var bcEl ) && bcEl.ValueKind == JsonValueKind.Array )
				{
					var arr = new List<float>();
					foreach ( var c in bcEl.EnumerateArray() ) arr.Add( c.GetSingle() );
					def.BaseColor = arr.ToArray();
				}

				if ( matEl.TryGetProperty( "emissionColor", out var ecEl ) && ecEl.ValueKind == JsonValueKind.Array )
				{
					var arr = new List<float>();
					foreach ( var c in ecEl.EnumerateArray() ) arr.Add( c.GetSingle() );
					def.EmissionColor = arr.ToArray();
				}

				materials.Add( def );
			}
			return materials;
		}

		/// <summary>
		/// Compute a geometry hash that includes vertex positions,
		/// so moving/pulling vertices is detected as a change.
		/// </summary>
		// Reflection hot-path cache. GetMethod + Invoke PER VERTEX (boxing every
		// argument and return) was fine at small scenes but became the "editor
		// lags on everything" stall once the scan ran it thousands of times per
		// tick. Resolve once into an open instance delegate — a direct call
		// thereafter.
		private static Func<PolygonMesh, VertexHandle, Vector3> _getVertexPositionFn;
		private static bool _getVertexPositionTried;

		private static Func<PolygonMesh, VertexHandle, Vector3> ResolveGetVertexPosition( PolygonMesh mesh )
		{
			if ( !_getVertexPositionTried )
			{
				_getVertexPositionTried = true;
				try
				{
					var m = mesh.GetType().GetMethod( "GetVertexPosition", new[] { typeof( VertexHandle ) } );
					if ( m != null && m.ReturnType == typeof( Vector3 ) && !m.IsStatic )
						_getVertexPositionFn = (Func<PolygonMesh, VertexHandle, Vector3>)Delegate.CreateDelegate(
							typeof( Func<PolygonMesh, VertexHandle, Vector3> ), m );
				}
				catch { }
			}
			return _getVertexPositionFn;
		}

		private static int ComputeMeshGeometryHash( PolygonMesh mesh )
		{
			unchecked
			{
				int hash = 17;
				var vertHandles = mesh.VertexHandles;
				if ( vertHandles == null ) return 0;

				hash = hash * 31 + vertHandles.Count();

				var getPos = ResolveGetVertexPosition( mesh );
				if ( getPos != null )
				{
					foreach ( var vh in vertHandles )
					{
						var pos = getPos( mesh, vh );
						// Quantize to 0.001 precision to avoid float noise
						hash = hash * 31 + (int)( pos.x * 1000 );
						hash = hash * 31 + (int)( pos.y * 1000 );
						hash = hash * 31 + (int)( pos.z * 1000 );
					}
				}
				else
				{
					var getPosMeth = mesh.GetType().GetMethod( "GetVertexPosition", new[] { typeof( VertexHandle ) } );
					if ( getPosMeth != null )
					{
						foreach ( var vh in vertHandles )
						{
							try
							{
								var result = getPosMeth.Invoke( mesh, new object[] { vh } );
								if ( result is Vector3 pos )
								{
									hash = hash * 31 + (int)( pos.x * 1000 );
									hash = hash * 31 + (int)( pos.y * 1000 );
									hash = hash * 31 + (int)( pos.z * 1000 );
								}
							}
							catch { break; }
						}
					}
				}

				var faceHandles = mesh.FaceHandles;
				if ( faceHandles != null )
					hash = hash * 31 + faceHandles.Count();

				return hash;
			}
		}

		// ── Mesh extraction (s&box -> Blender) ────────────────────────────────

		internal struct ExtractedMesh
		{
			public float[] Vertices;
			public int[] Faces;
		}

		internal static ExtractedMesh? ExtractMeshData( PolygonMesh mesh )
		{
			try
			{
				var vertHandles = mesh.VertexHandles.ToList();
				if ( vertHandles.Count < 3 ) return null;

				var vertMap = new Dictionary<VertexHandle, int>();
				int vertIdx = 0;
				foreach ( var vh in vertHandles ) vertMap[vh] = vertIdx++;

				var verts = new List<float>();
				var getPos = ResolveGetVertexPosition( mesh );
				if ( getPos != null )
				{
					foreach ( var vh in vertHandles )
					{
						var pos = getPos( mesh, vh );
						verts.Add( pos.x );
						verts.Add( pos.y );
						verts.Add( pos.z );
					}
				}
				else
				{
					var getPosMeth = mesh.GetType().GetMethod( "GetVertexPosition", new[] { typeof( VertexHandle ) } );
					if ( getPosMeth != null )
					{
						foreach ( var vh in vertHandles )
						{
							try
							{
								var result = getPosMeth.Invoke( mesh, new object[] { vh } );
								if ( result is Vector3 pos )
								{
									verts.Add( pos.x );
									verts.Add( pos.y );
									verts.Add( pos.z );
								}
							}
							catch { break; }
						}
					}
				}

				if ( verts.Count < 9 ) return null;

				var faces = new List<int>();
				var getFaceVertsMeth = mesh.GetType().GetMethod( "GetFaceVertices", new[] { typeof( FaceHandle ) } );
				if ( getFaceVertsMeth != null )
				{
					foreach ( var fh in mesh.FaceHandles )
					{
						var result = getFaceVertsMeth.Invoke( mesh, new object[] { fh } );
						if ( result is VertexHandle[] faceVerts )
						{
							faces.Add( faceVerts.Length );
							foreach ( var fv in faceVerts )
								faces.Add( vertMap.TryGetValue( fv, out var i ) ? i : 0 );
						}
						else if ( result is IEnumerable<VertexHandle> faceVertsEnum )
						{
							var fvList = faceVertsEnum.ToList();
							faces.Add( fvList.Count );
							foreach ( var fv in fvList )
								faces.Add( vertMap.TryGetValue( fv, out var i ) ? i : 0 );
						}
					}
				}

				if ( faces.Count == 0 ) return null;
				return new ExtractedMesh { Vertices = verts.ToArray(), Faces = faces.ToArray() };
			}
			catch ( Exception ex )
			{
				BlenderBridgeServer.LogError( $"ExtractMeshData failed: {ex.Message}" );
				return null;
			}
		}

		// ── Terrain proxy extraction (s&box -> Blender) ──────────────────────
		//
		// The Sandbox.Terrain component isn't a PolygonMesh — its data lives in
		// a TerrainStorage with a Resolution² ushort[] heightmap. Sending the
		// full heightmap to Blender is impractical (a 1024² terrain = 1M verts),
		// so we downsample to a fixed PROXY_RESOLUTION grid for a low-poly
		// reference mesh in mesh-local space. Triangulation: each cell is a
		// quad face (the wire format already supports n-gons).

		private const int TerrainProxyResolution = 128;

		internal static ExtractedMesh? ExtractTerrainProxyMesh( Terrain terrain )
		{
			try
			{
				var storage = terrain?.Storage;
				if ( storage == null ) return null;
				if ( storage.HeightMap == null || storage.HeightMap.Length == 0 ) return null;

				int srcRes = storage.Resolution;
				if ( srcRes <= 0 || storage.HeightMap.Length < srcRes * srcRes ) return null;

				int proxyRes = Math.Min( TerrainProxyResolution, srcRes );
				int vertsPerSide = proxyRes + 1;

				float terrainSize = storage.TerrainSize;
				float terrainHeight = storage.TerrainHeight;
				float cellSize = terrainSize / proxyRes;
				float invMaxHeight = 1.0f / 65535f;

				var verts = new float[vertsPerSide * vertsPerSide * 3];
				for ( int gy = 0; gy < vertsPerSide; gy++ )
				{
					int srcY = (int)((long)gy * (srcRes - 1) / proxyRes);
					for ( int gx = 0; gx < vertsPerSide; gx++ )
					{
						int srcX = (int)((long)gx * (srcRes - 1) / proxyRes);
						ushort h = storage.HeightMap[srcY * srcRes + srcX];

						int vi = (gy * vertsPerSide + gx) * 3;
						verts[vi + 0] = gx * cellSize;
						verts[vi + 1] = gy * cellSize;
						verts[vi + 2] = h * invMaxHeight * terrainHeight;
					}
				}

				// Faces: one quad per cell, [4, v00, v10, v11, v01].
				var faces = new int[proxyRes * proxyRes * 5];
				int fi = 0;
				for ( int gy = 0; gy < proxyRes; gy++ )
				{
					int row0 = gy * vertsPerSide;
					int row1 = (gy + 1) * vertsPerSide;
					for ( int gx = 0; gx < proxyRes; gx++ )
					{
						faces[fi++] = 4;
						faces[fi++] = row0 + gx;
						faces[fi++] = row0 + gx + 1;
						faces[fi++] = row1 + gx + 1;
						faces[fi++] = row1 + gx;
					}
				}

				return new ExtractedMesh { Vertices = verts, Faces = faces };
			}
			catch ( Exception ex )
			{
				BlenderBridgeServer.LogError( $"ExtractTerrainProxyMesh failed: {ex.Message}" );
				return null;
			}
		}

		/// <summary>
		/// Pulls mesh data out of either a MeshComponent or (as a downsampled
		/// proxy) a Terrain. Used by the payload builders so terrain shows up
		/// in Blender alongside regular meshes.
		/// </summary>
		private static object TryBuildMeshData( GameObject go )
		{
			var meshComp = go.Components.Get<MeshComponent>();
			if ( meshComp?.Mesh != null )
			{
				var extracted = ExtractMeshData( meshComp.Mesh );
				if ( extracted != null )
					return new { vertices = extracted.Value.Vertices, faces = extracted.Value.Faces };
			}

			var terrain = go.Components.Get<Terrain>();
			if ( terrain?.Storage != null )
			{
				var extracted = ExtractTerrainProxyMesh( terrain );
				if ( extracted != null )
					return new { vertices = extracted.Value.Vertices, faces = extracted.Value.Faces };
			}

			return null;
		}

		// ── Payload builders ──────────────────────────────────────────────────

		/// <summary>Public access to BuildObjectPayload for the editor window's Send to Blender button.</summary>
		internal static object BuildObjectPayloadPublic( string bridgeId, GameObject go )
		{
			return BuildObjectPayload( bridgeId, go );
		}

		/// <summary>Build a complete object_created message with type field for broadcasting.</summary>
		internal static object BuildObjectCreatedMessage( string bridgeId, GameObject go )
		{
			var pos = go.WorldPosition;
			var rot = go.WorldRotation.Angles();
			var meshData = TryBuildMeshData( go );

			return new
			{
				type = "object_created",
				bridgeId,
				name = go.Name,
				position = new { x = pos.x, y = pos.y, z = pos.z },
				rotation = new { pitch = rot.pitch, yaw = rot.yaw, roll = rot.roll },
				meshData
			};
		}

		private static object BuildObjectPayload( string bridgeId, GameObject go )
		{
			var pos = go.WorldPosition;
			var rot = go.WorldRotation.Angles();
			var meshData = TryBuildMeshData( go );

			// Build hierarchy path from parent chain (excluding bridge group root)
			var hierarchy = new List<string>();
			var parent = go.Parent;
			while ( parent != null && !parent.Tags.Has( "bridge_group" ) )
			{
				hierarchy.Insert( 0, parent.Name );
				parent = parent.Parent;
			}

			return new
			{
				bridgeId,
				name = go.Name,
				position = new { x = pos.x, y = pos.y, z = pos.z },
				rotation = new { pitch = rot.pitch, yaw = rot.yaw, roll = rot.roll },
				meshData,
				hierarchy
			};
		}

		private static object BuildLightPayload( GameObject go, Light light )
		{
			var pos = go.WorldPosition;
			var rot = go.WorldRotation.Angles();
			string lightType = "point";
			if ( light is SpotLight ) lightType = "spot";
			else if ( light is DirectionalLight ) lightType = "directional";

			float radius = 0f;
			float coneOuter = 0f;
			float coneInner = 0f;
			if ( light is PointLight pl ) radius = pl.Radius;
			if ( light is SpotLight spl ) { radius = spl.Radius; coneOuter = spl.ConeOuter; coneInner = spl.ConeInner; }

			object properties = new
			{
				color = new { r = light.LightColor.r, g = light.LightColor.g, b = light.LightColor.b },
				radius,
				coneOuter,
				coneInner,
			};

			return new
			{
				objectType = "light",
				lightType,
				name = go.Name,
				sceneId = go.Id.ToString(),
				position = new { x = pos.x, y = pos.y, z = pos.z },
				rotation = new { pitch = rot.pitch, yaw = rot.yaw, roll = rot.roll },
				properties
			};
		}

		private static object BuildModelPayload( GameObject go, Component modelComp )
		{
			var pos = go.WorldPosition;
			var rot = go.WorldRotation.Angles();
			var modelProp = modelComp.GetType().GetProperty( "Model" );
			var model = modelProp?.GetValue( modelComp ) as Model;
			var modelPath = model?.ResourcePath ?? "unknown";

			object bounds = null;
			if ( model != null )
			{
				try
				{
					bounds = new
					{
						mins = new { x = model.Bounds.Mins.x, y = model.Bounds.Mins.y, z = model.Bounds.Mins.z },
						maxs = new { x = model.Bounds.Maxs.x, y = model.Bounds.Maxs.y, z = model.Bounds.Maxs.z }
					};
				}
				catch { }
			}

			string fbxSourcePath = null;
			try
			{
				if ( modelPath != "unknown" )
					fbxSourcePath = ResolveFbxPath( modelPath );
			}
			catch { }

			return new
			{
				objectType = "model",
				name = go.Name,
				sceneId = go.Id.ToString(),
				modelPath,
				fbxSourcePath,
				bounds,
				position = new { x = pos.x, y = pos.y, z = pos.z },
				rotation = new { pitch = rot.pitch, yaw = rot.yaw, roll = rot.roll }
			};
		}

		private static string ResolveFbxPath( string modelPath )
		{
			try
			{
				var assetsDir = GetProjectAssetsDir();
				if ( assetsDir == null ) return null;
				var projectRoot = System.IO.Path.GetDirectoryName( assetsDir );
				var basePaths = new List<string> { assetsDir, projectRoot };
				var relativePath = modelPath.Replace( "\\", "/" );
				foreach ( var prefix in new[] { "assets/", "models/" } )
					if ( relativePath.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) )
					{ relativePath = relativePath.Substring( prefix.Length ); break; }

				string vmdlPath = null;
				string vmdlDir = null;
				foreach ( var basePath in basePaths )
				{
					if ( string.IsNullOrEmpty( basePath ) ) continue;
					var candidate = System.IO.Path.Combine( basePath, relativePath );
					if ( System.IO.File.Exists( candidate ) ) { vmdlPath = candidate; vmdlDir = System.IO.Path.GetDirectoryName( candidate ); break; }
					candidate = System.IO.Path.Combine( basePath, modelPath.Replace( "/", "\\" ) );
					if ( System.IO.File.Exists( candidate ) ) { vmdlPath = candidate; vmdlDir = System.IO.Path.GetDirectoryName( candidate ); break; }
				}
				if ( vmdlPath == null ) return null;

				var content = System.IO.File.ReadAllText( vmdlPath );
				var match = System.Text.RegularExpressions.Regex.Match( content,
					@"_class\s*=\s*""RenderMeshFile""[\s\S]*?filename\s*=\s*""([^""]+)""" );
				if ( !match.Success ) return null;

				var fbxRelative = match.Groups[1].Value.Replace( "\\", "/" );
				var fbxPath = System.IO.Path.GetFullPath( System.IO.Path.Combine( vmdlDir, fbxRelative ) );
				if ( System.IO.File.Exists( fbxPath ) ) return fbxPath;
				foreach ( var basePath in basePaths )
				{
					if ( string.IsNullOrEmpty( basePath ) ) continue;
					fbxPath = System.IO.Path.GetFullPath( System.IO.Path.Combine( basePath, fbxRelative ) );
					if ( System.IO.File.Exists( fbxPath ) ) return fbxPath;
				}
				return null;
			}
			catch { return null; }
		}

		// ── Helpers ───────────────────────────────────────────────────────────

		private static void ApplyTransform( GameObject go, JsonElement root )
		{
			if ( root.TryGetProperty( "position", out var pos ) )
			{
				go.WorldPosition = new Vector3(
					GetFloat( pos, "x", 0f ),
					GetFloat( pos, "y", 0f ),
					GetFloat( pos, "z", 0f )
				);
			}
			if ( root.TryGetProperty( "rotation", out var rot ) )
			{
				go.WorldRotation = Rotation.From(
					GetFloat( rot, "pitch", 0f ),
					GetFloat( rot, "yaw", 0f ),
					GetFloat( rot, "roll", 0f )
				);
			}
		}

		private static string GetString( JsonElement el, string prop, string fallback = null )
		{
			return el.TryGetProperty( prop, out var v ) && v.ValueKind == JsonValueKind.String ? v.GetString() : fallback;
		}

		private static float GetFloat( JsonElement el, string prop, float fallback )
		{
			if ( el.TryGetProperty( prop, out var v ) && v.ValueKind == JsonValueKind.Number ) return v.GetSingle();
			return fallback;
		}

		private static int GetInt( JsonElement el, string prop, int fallback )
		{
			if ( el.TryGetProperty( prop, out var v ) && v.ValueKind == JsonValueKind.Number ) return v.GetInt32();
			return fallback;
		}

		/// <summary>
		/// Reply with this s&box project's Assets folder so Blender can
		/// auto-populate its addon path on connect. Carries diagnostic
		/// fields too so the Blender side can log what failed when
		/// assetsDir comes back null.
		/// </summary>
		private static string HandleGetProjectInfo( JsonElement root )
		{
			string discoveryMethod = "none";
			string projectCurrentName = null;
			string projectCurrentRoot = null;
			string sessionState = "missing";
			string sceneState = "missing";
			string sceneResourcePath = null;
			string fullScenePath = null;
			string discoveryError = null;
			string assetsDir = null;

			// Primary: Project.Current — scene-independent.
			try
			{
				if ( Sandbox.Project.Current != null )
				{
					projectCurrentName = Sandbox.Project.Current.Config?.Title;
					projectCurrentRoot = Sandbox.Project.Current.GetRootPath();
					if ( Sandbox.Project.Current.HasAssetsPath() )
					{
						assetsDir = Sandbox.Project.Current.GetAssetsPath();
						discoveryMethod = "Project.Current";
					}
				}
			}
			catch ( Exception ex )
			{
				discoveryError = "Project.Current: " + ex.GetType().Name + ": " + ex.Message;
			}

			// Fallback: scene-walk (only if Project.Current didn't resolve).
			if ( assetsDir == null )
			{
				try
				{
					var session = SceneEditorSession.Active;
					if ( session != null )
					{
						sessionState = "active";
						if ( session.Scene != null )
						{
							sceneState = "active";
							sceneResourcePath = session.Scene.Source?.ResourcePath;
							if ( !string.IsNullOrEmpty( sceneResourcePath ) )
							{
								fullScenePath = Sandbox.FileSystem.Mounted.GetFullPath( sceneResourcePath );
								if ( !string.IsNullOrEmpty( fullScenePath ) )
								{
									var dir = System.IO.Path.GetDirectoryName( fullScenePath );
									while ( dir != null )
									{
										var candidate = System.IO.Path.Combine( dir, "Assets" );
										if ( System.IO.Directory.Exists( candidate ) )
										{
											assetsDir = candidate;
											discoveryMethod = "scene-walk";
											break;
										}
										dir = System.IO.Path.GetDirectoryName( dir );
									}
								}
							}
						}
					}
				}
				catch ( Exception ex )
				{
					discoveryError = (discoveryError == null ? "" : discoveryError + "; ")
						+ "scene-walk: " + ex.GetType().Name + ": " + ex.Message;
				}
			}

			string projectName = projectCurrentName;
			if ( projectName == null && !string.IsNullOrEmpty( assetsDir ) )
			{
				var projectDir = System.IO.Path.GetDirectoryName( assetsDir );
				if ( !string.IsNullOrEmpty( projectDir ) )
					projectName = System.IO.Path.GetFileName( projectDir );
			}

			static string J( string s ) => s == null ? "null" : System.Text.Json.JsonSerializer.Serialize( s );

			var sb = new System.Text.StringBuilder();
			sb.Append( "{\"ok\":true" );
			sb.Append( ",\"assetsDir\":" ).Append( J( assetsDir ) );
			sb.Append( ",\"projectName\":" ).Append( J( projectName ) );
			sb.Append( ",\"diag\":{" );
			sb.Append( "\"discoveryMethod\":" ).Append( J( discoveryMethod ) );
			sb.Append( ",\"projectCurrentRoot\":" ).Append( J( projectCurrentRoot ) );
			sb.Append( ",\"sessionState\":" ).Append( J( sessionState ) );
			sb.Append( ",\"sceneState\":" ).Append( J( sceneState ) );
			sb.Append( ",\"sceneResourcePath\":" ).Append( J( sceneResourcePath ) );
			sb.Append( ",\"fullScenePath\":" ).Append( J( fullScenePath ) );
			sb.Append( ",\"discoveryError\":" ).Append( J( discoveryError ) );
			sb.Append( "}}" );
			return sb.ToString();
		}

		internal static string GetProjectAssetsDir()
		{
			// Primary: Project.Current — works regardless of whether a scene is
			// loaded or saved. The editor sets this when the project opens.
			try
			{
				if ( Sandbox.Project.Current != null && Sandbox.Project.Current.HasAssetsPath() )
					return Sandbox.Project.Current.GetAssetsPath();
			}
			catch { }

			// Fallback: walk up from the active scene's resource path. Useful in
			// edge cases where Project.Current isn't set but a scene is.
			try
			{
				var session = SceneEditorSession.Active;
				if ( session?.Scene != null )
				{
					var scenePath = session.Scene.Source?.ResourcePath;
					if ( !string.IsNullOrEmpty( scenePath ) )
					{
						var fullScenePath = Sandbox.FileSystem.Mounted.GetFullPath( scenePath );
						if ( !string.IsNullOrEmpty( fullScenePath ) )
						{
							var dir = System.IO.Path.GetDirectoryName( fullScenePath );
							while ( dir != null )
							{
								var candidate = System.IO.Path.Combine( dir, "Assets" );
								if ( System.IO.Directory.Exists( candidate ) ) return candidate;
								dir = System.IO.Path.GetDirectoryName( dir );
							}
						}
					}
				}
			}
			catch { }

			return null;
		}

		private static Material LoadMaterialSafe( string path )
		{
			if ( string.IsNullOrEmpty( path ) ) return null;
			try
			{
				// The addon rewrites generated vmats IN PLACE (same path) when a
				// material is edited. Material.Load can win the race against the
				// asset watcher and return the stale compiled version — live
				// material edits then only show up on the NEXT load (a dup, a
				// restart). Nudge the compile first; no-op when up to date.
				try { AssetSystem.FindByPath( path )?.Compile( false ); }
				catch { }
				return Material.Load( path );
			}
			catch { return null; }
		}
	}
}
