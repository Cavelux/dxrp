using System.Collections.Generic;

namespace Grains.RazorDesigner.Document;

// Adapter wrapping StateRule.CompareCanonical as an IComparer<StateRule> for OrderBy callsites.
internal sealed class StateRuleCanonicalComparer : IComparer<StateRule>
{
	public static readonly StateRuleCanonicalComparer Instance = new();
	public int Compare( StateRule a, StateRule b ) => StateRule.CompareCanonical( a, b );
}
