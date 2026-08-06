using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Editor;
using HalfEdgeMesh;
using Sandbox;

namespace BlenderBridge
{
	/// <summary>
	/// Persistence layer for bridge mesh data.
	/// Writes a sidecar manifest (.bridge.json) alongside the scene file
	/// and binary mesh cache files so geometry survives play mode without
	/// bloating the scene JSON.
	/// </summary>
	internal static class BridgePersistence
	{
		private const string CacheDirName = ".sbox_bridge_cache";
		private const int ManifestVersion = 1;

		// ── Public API ────────────────────────────────────────────────────────

		// Deferred saves. Writing cache + manifest synchronously on every
		// inbound message made each Blender edit an editor hitch: the mesh
		// extraction + per-face attribute reflection is per-object work, and
		// SaveManifest re-walks the WHOLE scene (counting every mesh's
		// handles) — during a full resync that's O(n²). Queue instead; the
		// poll tick flushes after a quiet period, and play-mode entry /
		// server stop flush immediately so nothing is lost where it matters.
		private static readonly Dictionary<string, GameObject> _pendingSaves = new();
		private static DateTime _lastQueueTime = DateTime.MinValue;
		private const double SaveQuietSeconds = 1.5;

		/// <summary>Queue a cache+manifest save after a create or mesh update.</summary>
		internal static void SaveAfterChange( Scene scene, string bridgeId, GameObject go )
		{
			if ( scene == null || go == null ) return;
			_pendingSaves[bridgeId] = go;
			_lastQueueTime = DateTime.UtcNow;
		}

		/// <summary>Drop all queued saves (server restart — stale GameObjects).</summary>
		internal static void ClearPendingSaves()
		{
			_pendingSaves.Clear();
		}

		/// <summary>Write queued caches + one manifest. Called every poll tick
		/// (respects the quiet period) and with force=true on play-mode entry
		/// and server stop.</summary>
		internal static void FlushPendingSaves( Scene scene, bool force = false )
		{
			if ( scene == null || _pendingSaves.Count == 0 ) return;
			if ( !force && (DateTime.UtcNow - _lastQueueTime).TotalSeconds < SaveQuietSeconds ) return;

			foreach ( var (bridgeId, go) in _pendingSaves )
			{
				try
				{
					if ( go == null || !go.IsValid ) continue;
					var meshComp = go.Components.Get<MeshComponent>();
					if ( meshComp?.Mesh == null ) continue;

					var extracted = BlenderBridgeDispatcher.ExtractMeshData( meshComp.Mesh );
					if ( extracted == null ) continue;

					var cachePath = GetCachePath( scene, bridgeId );
					if ( cachePath != null )
					{
						var (faceMats, faceUVs) = ExtractFaceAttributes( meshComp.Mesh );
						SaveMeshCache( cachePath, extracted.Value.Vertices, extracted.Value.Faces, faceMats, faceUVs );
					}
				}
				catch ( Exception ex )
				{
					BlenderBridgeServer.LogInfo( $"Persistence save error ({bridgeId}): {ex.Message}" );
				}
			}
			_pendingSaves.Clear();

			try
			{
				SaveManifest( scene );
			}
			catch ( Exception ex )
			{
				BlenderBridgeServer.LogInfo( $"Manifest save error: {ex.Message}" );
			}
		}

		/// <summary>Remove cache file for a deleted bridge object.</summary>
		internal static void RemoveFromCache( string bridgeId )
		{
			// A queued save for a deleted object must not resurrect its cache.
			_pendingSaves.Remove( bridgeId );
			try
			{
				var scene = BridgeSceneHelper.ResolveScene();
				if ( scene == null ) return;

				var cachePath = GetCachePath( scene, bridgeId );
				if ( cachePath != null && File.Exists( cachePath ) )
					File.Delete( cachePath );

				SaveManifest( scene );
			}
			catch { }
		}

		/// <summary>Restore mesh data from cache for bridge objects missing geometry.
		/// Called on server start and after exiting play mode.</summary>
		internal static void RestoreFromCache( Scene scene )
		{
			if ( scene == null ) return;

			var cacheDir = GetCacheDir( scene );
			if ( cacheDir == null || !Directory.Exists( cacheDir ) ) return;

			int restored = 0;

			foreach ( var root in scene.Children )
			{
				RestoreSubtree( root, cacheDir, ref restored );
			}

			if ( restored > 0 )
				BlenderBridgeServer.LogInfo( $"Restored {restored} mesh(es) from cache" );
		}

		/// <summary>Check if any bridge state has been saved.</summary>
		internal static bool HasSavedState( Scene scene )
		{
			if ( scene == null ) return false;
			var manifestPath = GetManifestPath( scene );
			return manifestPath != null && File.Exists( manifestPath );
		}

		// ── Manifest ──────────────────────────────────────────────────────────

		private static void SaveManifest( Scene scene )
		{
			var manifestPath = GetManifestPath( scene );
			if ( manifestPath == null ) return;

			var objects = new Dictionary<string, object>();

			foreach ( var root in scene.Children )
				CollectManifestEntries( root, objects );

			var manifest = new
			{
				version = ManifestVersion,
				savedAt = DateTime.UtcNow.ToString( "o" ),
				objects
			};

			var json = JsonSerializer.Serialize( manifest, new JsonSerializerOptions
			{
				WriteIndented = true,
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase
			} );

			// Write-to-temp + rename: truncating in place leaves a corrupt
			// manifest behind if a crash or full disk interrupts the write,
			// which then fails restore silently on every cycle.
			var tmpPath = manifestPath + ".tmp";
			File.WriteAllText( tmpPath, json );
			File.Move( tmpPath, manifestPath, overwrite: true );
		}

		private static void CollectManifestEntries( GameObject node, Dictionary<string, object> entries )
		{
			foreach ( var tag in node.Tags.TryGetAll() )
			{
				if ( tag.StartsWith( "bridge_" ) && tag != "bridge_group" )
				{
					var bridgeId = tag.Substring( 7 );
					var pos = node.WorldPosition;
					var rot = node.WorldRotation.Angles();

					var meshComp = node.Components.Get<MeshComponent>();
					int vertCount = 0, faceCount = 0;
					if ( meshComp?.Mesh != null )
					{
						vertCount = meshComp.Mesh.VertexHandles?.Count() ?? 0;
						faceCount = meshComp.Mesh.FaceHandles?.Count() ?? 0;
					}

					entries[bridgeId] = new
					{
						name = node.Name,
						vertexCount = vertCount,
						faceCount = faceCount,
						position = new { x = pos.x, y = pos.y, z = pos.z },
						rotation = new { pitch = rot.pitch, yaw = rot.yaw, roll = rot.roll }
					};
					break;
				}
			}

			foreach ( var child in node.Children )
				CollectManifestEntries( child, entries );
		}

		// ── Binary Cache ──────────────────────────────────────────────────────

		// v2 cache layout, magic-prefixed ("BMC2"). v1 files start with the raw
		// vert count instead — no real mesh reaches ~845M verts, so the first
		// uint32 disambiguates and old geometry-only caches keep restoring.
		//   [uint32 magic][uint32 vertCount][uint32 faceDataLen]
		//   [float32[] verts][int32[] faces]
		//   [int32 matCount][string[] matPaths]           — indexed table
		//   [int32 faceCount][int32[] perFaceMatIdx]      — -1 = no material
		//   [bool hasUVs][per face: int32 n, float32 u,v * n]
		private const uint CacheMagicV2 = 0x32434D42; // "BMC2"

		/// <summary>Write binary mesh cache including per-face materials and UVs,
		/// so a restore doesn't silently downgrade everything to the dev
		/// checkerboard with grid UVs.</summary>
		private static void SaveMeshCache( string path, float[] vertices, int[] faces, string[] faceMaterials, Vector2[][] faceUVs )
		{
			var dir = Path.GetDirectoryName( path );
			if ( dir != null )
				Directory.CreateDirectory( dir );

			// Write-to-temp + rename so a crash or full disk mid-write can't
			// leave a truncated cache that fails restore on every cycle.
			var tmpPath = path + ".tmp";
			using ( var fs = new FileStream( tmpPath, FileMode.Create ) )
			using ( var bw = new BinaryWriter( fs ) )
			{
				bw.Write( CacheMagicV2 );
				bw.Write( (uint)(vertices.Length / 3) );
				bw.Write( (uint)faces.Length );

				foreach ( var v in vertices )
					bw.Write( v );

				foreach ( var f in faces )
					bw.Write( f );

				// Per-face materials as an indexed string table.
				var matTable = new List<string>();
				var matLookup = new Dictionary<string, int>();
				var faceMatIdx = new int[faceMaterials?.Length ?? 0];
				for ( int i = 0; i < faceMatIdx.Length; i++ )
				{
					var matPath = faceMaterials[i];
					if ( string.IsNullOrEmpty( matPath ) )
					{
						faceMatIdx[i] = -1;
						continue;
					}
					if ( !matLookup.TryGetValue( matPath, out var mi ) )
					{
						mi = matTable.Count;
						matTable.Add( matPath );
						matLookup[matPath] = mi;
					}
					faceMatIdx[i] = mi;
				}

				bw.Write( matTable.Count );
				foreach ( var m in matTable )
					bw.Write( m );
				bw.Write( faceMatIdx.Length );
				foreach ( var mi in faceMatIdx )
					bw.Write( mi );

				bw.Write( faceUVs != null );
				if ( faceUVs != null )
				{
					bw.Write( faceUVs.Length );
					foreach ( var uv in faceUVs )
					{
						bw.Write( uv?.Length ?? 0 );
						if ( uv == null ) continue;
						foreach ( var p in uv )
						{
							bw.Write( p.x );
							bw.Write( p.y );
						}
					}
				}
			}

			File.Move( tmpPath, path, overwrite: true );
		}

		/// <summary>
		/// Pull per-face material paths and texture coords off a live mesh so a
		/// cache restore can rebuild more than bare geometry. Face order matches
		/// ExtractMeshData — both enumerate FaceHandles. Either result may be
		/// null (engine API unavailable via reflection); the cache format and
		/// restore path tolerate that by falling back to current behavior.
		/// </summary>
		private static (string[] MatPaths, Vector2[][] FaceUVs) ExtractFaceAttributes( PolygonMesh mesh )
		{
			try
			{
				var faceHandles = mesh.FaceHandles.ToList();
				// The handle type isn't directly nameable from here — derive it
				// from the list's generic argument for the reflection lookups.
				var handleType = faceHandles.GetType().GetGenericArguments()[0];
				var getMat = mesh.GetType().GetMethod( "GetFaceMaterial", new[] { handleType } );
				var getUVs = mesh.GetType().GetMethod( "GetFaceTextureCoords", new[] { handleType } );

				var mats = getMat != null ? new string[faceHandles.Count] : null;
				var uvs = getUVs != null ? new Vector2[faceHandles.Count][] : null;
				bool anyUV = false;

				for ( int i = 0; i < faceHandles.Count; i++ )
				{
					if ( mats != null )
					{
						try
						{
							var m = getMat.Invoke( mesh, new object[] { faceHandles[i] } ) as Material;
							var matPath = m?.ResourcePath;
							// Compiled path -> source path, so Material.Load
							// resolves it on restore.
							if ( matPath != null && matPath.EndsWith( "_c", StringComparison.OrdinalIgnoreCase ) )
								matPath = matPath.Substring( 0, matPath.Length - 2 );
							mats[i] = matPath;
						}
						catch { mats = null; }
					}

					if ( uvs != null )
					{
						try
						{
							if ( getUVs.Invoke( mesh, new object[] { faceHandles[i] } ) is Vector2[] coords && coords.Length > 0 )
							{
								uvs[i] = coords;
								anyUV = true;
							}
						}
						catch { uvs = null; }
					}
				}

				return (mats, anyUV ? uvs : null);
			}
			catch
			{
				return (null, null);
			}
		}

		/// <summary>Read binary mesh cache and rebuild PolygonMesh on a GameObject.</summary>
		private static bool RestoreMeshFromCache( string cachePath, GameObject go )
		{
			if ( !File.Exists( cachePath ) ) return false;

			try
			{
				using var fs = new FileStream( cachePath, FileMode.Open );
				using var br = new BinaryReader( fs );

				// v1 files have no magic — the first uint32 is the vert count.
				var first = br.ReadUInt32();
				bool isV2 = first == CacheMagicV2;
				var vertCount = isV2 ? br.ReadUInt32() : first;
				var faceDataLen = br.ReadUInt32();

				var vertices = new Vector3[vertCount];
				for ( int i = 0; i < vertCount; i++ )
				{
					float x = br.ReadSingle();
					float y = br.ReadSingle();
					float z = br.ReadSingle();
					vertices[i] = new Vector3( x, y, z );
				}

				var faceData = new int[faceDataLen];
				for ( int i = 0; i < faceDataLen; i++ )
					faceData[i] = br.ReadInt32();

				// v2 trailer: material table, per-face material indices, UVs.
				string[] matTable = null;
				int[] faceMatIdx = null;
				Vector2[][] faceUVs = null;
				if ( isV2 )
				{
					int matCount = br.ReadInt32();
					matTable = new string[matCount];
					for ( int i = 0; i < matCount; i++ )
						matTable[i] = br.ReadString();

					int faceCount = br.ReadInt32();
					faceMatIdx = new int[faceCount];
					for ( int i = 0; i < faceCount; i++ )
						faceMatIdx[i] = br.ReadInt32();

					if ( br.ReadBoolean() )
					{
						int uvFaceCount = br.ReadInt32();
						faceUVs = new Vector2[uvFaceCount][];
						for ( int i = 0; i < uvFaceCount; i++ )
						{
							int n = br.ReadInt32();
							var uv = new Vector2[n];
							for ( int j = 0; j < n; j++ )
								uv[j] = new Vector2( br.ReadSingle(), br.ReadSingle() );
							faceUVs[i] = uv;
						}
					}
				}

				// Parse face groups
				var faceGroups = new List<int[]>();
				int idx = 0;
				while ( idx < faceData.Length )
				{
					int fvc = faceData[idx++];
					if ( idx + fvc > faceData.Length ) break;
					var face = new int[fvc];
					for ( int i = 0; i < fvc; i++ )
						face[i] = faceData[idx++];
					faceGroups.Add( face );
				}

				// Build PolygonMesh
				var mesh = new PolygonMesh();
				var hVertices = mesh.AddVertices( vertices );

				var defaultMat = Material.Load( "materials/dev/reflectivity_30.vmat" );

				// Attribute arrays are only trusted when they line up with the
				// parsed faces — a mismatch (partial extraction at save time)
				// degrades to the old geometry-only behavior.
				Material[] mats = null;
				if ( matTable != null )
				{
					mats = new Material[matTable.Length];
					for ( int i = 0; i < matTable.Length; i++ )
						mats[i] = TryLoadMaterial( matTable[i] ) ?? defaultMat;
				}
				bool hasMats = mats != null && faceMatIdx != null && faceMatIdx.Length == faceGroups.Count;
				bool hasUVs = faceUVs != null && faceUVs.Length == faceGroups.Count;

				int faceIdx = 0;
				foreach ( var faceGroup in faceGroups )
				{
					var faceVerts = faceGroup
						.Where( fi => fi >= 0 && fi < hVertices.Length )
						.Select( fi => hVertices[fi] )
						.ToArray();
					if ( faceVerts.Length >= 3 )
					{
						var hFace = mesh.AddFace( faceVerts );

						var mat = defaultMat;
						if ( hasMats )
						{
							var mi = faceMatIdx[faceIdx];
							if ( mi >= 0 && mi < mats.Length )
								mat = mats[mi];
						}
						mesh.SetFaceMaterial( hFace, mat );

						if ( hasUVs && faceUVs[faceIdx]?.Length == faceVerts.Length )
							mesh.SetFaceTextureCoords( hFace, faceUVs[faceIdx] );
					}
					faceIdx++;
				}

				// Grid-align only when no authored UVs were cached — running it
				// over restored UVs would stomp them, which is the exact bug
				// this format exists to fix.
				if ( !hasUVs )
					mesh.TextureAlignToGrid( mesh.Transform );
				mesh.SetSmoothingAngle( 40.0f );

				var existingMr = go.Components.Get<ModelRenderer>();
				if ( existingMr != null )
					existingMr.Destroy();

				var meshComp = go.Components.Get<MeshComponent>();
				if ( meshComp == null )
					meshComp = go.Components.Create<MeshComponent>();

				meshComp.Mesh = mesh;
				return true;
			}
			catch ( Exception ex )
			{
				BlenderBridgeServer.LogInfo( $"Cache restore failed for {cachePath}: {ex.Message}" );
				return false;
			}
		}

		private static Material TryLoadMaterial( string path )
		{
			if ( string.IsNullOrEmpty( path ) ) return null;
			try { return Material.Load( path ); }
			catch { return null; }
		}

		private static void RestoreSubtree( GameObject node, string cacheDir, ref int restored )
		{
			foreach ( var tag in node.Tags.TryGetAll() )
			{
				if ( tag.StartsWith( "bridge_" ) && tag != "bridge_group" )
				{
					var bridgeId = tag.Substring( 7 );

					// Only restore if the object has no mesh currently
					var meshComp = node.Components.Get<MeshComponent>();
					if ( meshComp?.Mesh == null || (meshComp.Mesh.VertexHandles?.Count() ?? 0) == 0 )
					{
						var cachePath = Path.Combine( cacheDir, $"{bridgeId}.meshcache" );
						if ( RestoreMeshFromCache( cachePath, node ) )
							restored++;
					}
					break;
				}
			}

			foreach ( var child in node.Children )
				RestoreSubtree( child, cacheDir, ref restored );
		}

		// ── Path helpers ──────────────────────────────────────────────────────

		private static string GetManifestPath( Scene scene )
		{
			try
			{
				var scenePath = scene.Source?.ResourcePath;
				if ( string.IsNullOrEmpty( scenePath ) ) return null;

				var fullPath = Sandbox.FileSystem.Mounted.GetFullPath( scenePath );
				if ( string.IsNullOrEmpty( fullPath ) ) return null;

				var dir = Path.GetDirectoryName( fullPath );
				var name = Path.GetFileNameWithoutExtension( fullPath );
				return Path.Combine( dir, $"{name}.bridge.json" );
			}
			catch
			{
				return null;
			}
		}

		private static string GetCacheDir( Scene scene )
		{
			try
			{
				var assetsDir = BlenderBridgeDispatcher.GetProjectAssetsDir();
				if ( assetsDir == null ) return null;

				var projectRoot = Path.GetDirectoryName( assetsDir );
				if ( projectRoot == null ) return null;

				return Path.Combine( projectRoot, CacheDirName );
			}
			catch
			{
				return null;
			}
		}

		private static string GetCachePath( Scene scene, string bridgeId )
		{
			var dir = GetCacheDir( scene );
			if ( dir == null ) return null;
			return Path.Combine( dir, $"{bridgeId}.meshcache" );
		}
	}
}
