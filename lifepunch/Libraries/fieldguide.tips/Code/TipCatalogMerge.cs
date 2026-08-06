using System.Collections.Generic;

namespace FieldGuide.Tips;

/// <summary>
/// The merged catalog as one value: the ordered tips and the label saying where each id came from, built
/// together so a reader can never pair a list from one build with labels from another.
/// </summary>
public sealed class TipCatalogView
{
	/// <summary>The deduped, precedence-ordered tips.</summary>
	public IReadOnlyList<TipDefinition> Tips { get; init; } = new List<TipDefinition>();

	/// <summary>Tip id to source label ("code" / "asset" / "draft" / "example").</summary>
	public IReadOnlyDictionary<string, string> SourceById { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// The pure merge rule behind <see cref="TipsCatalog.Active"/>: which source wins an id collision, what order
/// the survivors come out in, and when the shipped example stands in. Lifted out of the catalog so it has no
/// <c>Sandbox</c> reference and the harness can assert precedence and dedupe without a running engine, which
/// is the half of the catalog that a typo actually breaks.
/// </summary>
public static class TipCatalogMerge
{
	/// <summary>
	/// Merge the three sources into one view, highest precedence first: CODE, then ASSETS, then DRAFTS. The
	/// first tip seen for an id wins and later ones are dropped, so a draft never shadows the real tip it is
	/// a draft of. When all three come back empty, <paramref name="fallback"/> is used and labelled
	/// "example". Null sources are treated as empty; a null tip, or one with a blank id, is skipped.
	/// </summary>
	public static TipCatalogView Merge(
		IEnumerable<TipDefinition> code,
		IEnumerable<TipDefinition> assets,
		IEnumerable<TipDefinition> drafts,
		IEnumerable<TipDefinition> fallback )
	{
		var order = new List<TipDefinition>();
		var sources = new Dictionary<string, string>( System.StringComparer.Ordinal );

		Add( code, "code", order, sources );
		Add( assets, "asset", order, sources );
		Add( drafts, "draft", order, sources );

		if ( order.Count == 0 )
			Add( fallback, "example", order, sources );

		return new TipCatalogView { Tips = order, SourceById = sources };
	}

	// The source map IS the dedupe guard, and it is filled in the same pass as the list it guards. Guarding
	// inserts to one collection by querying a different, longer-lived one is how these two drift apart.
	private static void Add( IEnumerable<TipDefinition> source, string label,
		List<TipDefinition> order, Dictionary<string, string> sources )
	{
		if ( source is null )
			return;

		foreach ( var def in source )
		{
			if ( def is null || string.IsNullOrEmpty( def.Id ) )
				continue;
			if ( sources.ContainsKey( def.Id ) )
				continue; // a higher-precedence source already claimed this id
			order.Add( def );
			sources[def.Id] = label;
		}
	}
}

/// <summary>
/// What a built catalog view was built FROM: the code list it saw, and the draft / asset revision numbers at
/// the time. A read compares the stamp against the live sources; anything that moved means the view is stale
/// and gets rebuilt. Comparing is three field reads with no allocation, which is what lets the coach ask for
/// the catalog several times a frame without a rescan.
///
/// The code source is compared BY REFERENCE, not by content: registering is a whole-list swap, so a new list
/// is a new catalog, and a caller mutating a list it already registered is expected to say so
/// (<see cref="TipsCatalog.Rebuild"/>) the same way it always was.
/// </summary>
public readonly struct TipCatalogStamp
{
	/// <summary>The code catalog this view merged.</summary>
	public object Code { get; }

	/// <summary>The draft revision this view merged.</summary>
	public int DraftRevision { get; }

	/// <summary>The asset revision this view merged.</summary>
	public int AssetRevision { get; }

	public TipCatalogStamp( object code, int draftRevision, int assetRevision )
	{
		Code = code;
		DraftRevision = draftRevision;
		AssetRevision = assetRevision;
	}

	/// <summary>True when nothing has moved since this view was built.</summary>
	public bool Matches( object code, int draftRevision, int assetRevision )
		=> ReferenceEquals( Code, code ) && DraftRevision == draftRevision && AssetRevision == assetRevision;
}
