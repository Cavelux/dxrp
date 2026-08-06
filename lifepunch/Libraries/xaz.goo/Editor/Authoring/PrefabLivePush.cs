using System.Collections.Generic;
using System.Text.Json.Nodes;
using Editor;
using Sandbox;

namespace Goo.Authoring.Editor;

/// <summary>Propagates external prefab file changes to every open editor session.</summary>
public static class PrefabLivePush
{
	static readonly Dictionary<string, JsonObject> _seen = new();

	[EditorEvent.Frame]
	static void Tick()
	{
		foreach ( var prefab in ResourceLibrary.GetAll<PrefabFile>() )
		{
			var path = prefab.ResourcePath;
			if ( _seen.TryGetValue( path, out var root ) && ReferenceEquals( root, prefab.RootObject ) )
				continue;

			var known = _seen.ContainsKey( path );
			_seen[path] = prefab.RootObject;

			// Never push play-mode root replacements because they corrupt prefab-session references.
			if ( SceneEditorSession.Resolve( prefab ) is { IsPrefabSession: true, IsPlaying: true } )
				continue;

			if ( known )
				EditorScene.UpdatePrefabInstances( prefab );
		}
	}
}
