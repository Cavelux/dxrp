using System;

namespace FieldGuide.DayNight;

/// <summary>Weather kinds (visual-only). Rolled per in-game day as a PURE hash of (seed, dayIndex), so a host
/// and every observer agree on the day's weather from the replicated seed + clock alone, with no extra
/// networking. The int order is the wire/override value: an authority pin publishes the forced kind as its
/// int, and any deserializer maps back through this enum.</summary>
public enum WeatherKind
{
	Clear = 0,
	Cloudy = 1,
	Rain = 2,
}

/// <summary>The deterministic per-day weather roll. Pure and self-contained: no DateTime, no System.Random,
/// just an FNV-1a hash of the world seed and the day index bucketed into the three kinds. Same inputs always
/// yield the same kind, so two peers deriving weather from the same seed never disagree and the roll never
/// flaps mid-day.</summary>
public static class WeatherRoll
{
	/// <summary>Roll the weather for a given (world seed, day index). Distribution: ~60% Clear, ~25% Cloudy,
	/// ~15% Rain. Byte-stable and deterministic; the hash (offset basis, prime, salt) is ported unchanged from
	/// the source game, so a save that recorded a WB day rolls the same kind here.</summary>
	public static WeatherKind For( int seed, int dayIndex )
	{
		ulong h = 1469598103934665603UL;   // FNV-1a offset basis
		void Mix( long v )
		{
			for ( int i = 0; i < 8; i++ ) { h ^= (byte)(v >> (i * 8)); h *= 1099511628211UL; }
		}
		Mix( seed );
		Mix( dayIndex );
		Mix( 0x5713_9A2FL );   // fixed salt so dayIndex 0 isn't a bare seed hash (ported verbatim)
		int r = (int)(h % 100);
		if ( r < 60 ) return WeatherKind.Clear;   // ~60% clear, ~25% cloudy, ~15% rain
		if ( r < 85 ) return WeatherKind.Cloudy;
		return WeatherKind.Rain;
	}
}
