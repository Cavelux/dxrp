using System.Collections.Generic;

namespace Grains.RazorDesigner.Inspector;

public interface IExpandStateStore
{
	// Returns the saved expand state for `key`, or `fallback` if not present.
	bool Get( string key, bool fallback );

	// Records that `key`'s section is `expanded`. Persisted at the store's discretion.
	void Set( string key, bool expanded );
}
