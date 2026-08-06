using System.Collections.Generic;
using System.Text;
using Editor;
using Sandbox;

namespace Grains.RazorDesigner.Inspector;

public sealed class InspectorExpandStateStore : IExpandStateStore
{
	private const string LogPrefix = "[Grains.RazorDesigner]";

	private readonly string _cookieName;
	private readonly Dictionary<string, bool> _state = new();
	private bool _loaded;

	public InspectorExpandStateStore( string cookieName )
	{
		_cookieName = cookieName;
	}

	public bool Get( string key, bool fallback )
	{
		LoadOnce();
		return _state.TryGetValue( key, out var v ) ? v : fallback;
	}

	public void Set( string key, bool expanded )
	{
		LoadOnce();
		_state[key] = expanded;
		Save();
	}

	private void LoadOnce()
	{
		if ( _loaded ) return;
		_loaded = true;

		var raw = EditorCookie.Get<string>( _cookieName, null );
		if ( string.IsNullOrEmpty( raw ) ) return;

		foreach ( var pair in raw.Split( ',', System.StringSplitOptions.RemoveEmptyEntries ) )
		{
			var eq = pair.IndexOf( '=' );
			if ( eq <= 0 ) continue;
			var key = pair.Substring( 0, eq );
			var val = pair.Substring( eq + 1 );
			_state[key] = val == "1";
		}
		Log.Info( $"{LogPrefix} InspectorExpandStateStore loaded: {_state.Count} entries from {_cookieName}" );
	}

	private void Save()
	{
		var sb = new StringBuilder();
		foreach ( var kv in _state )
		{
			if ( sb.Length > 0 ) sb.Append( ',' );
			sb.Append( kv.Key ).Append( '=' ).Append( kv.Value ? '1' : '0' );
		}
		EditorCookie.Set( _cookieName, sb.ToString() );
	}
}
