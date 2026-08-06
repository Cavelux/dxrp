using System;
using System.Collections.Generic;
using Sandbox; // [Hide] etc. not needed here, but keep namespace style consistent

namespace Grains.RazorDesigner.Document;

public enum PseudoKind
{
	Hover,
	Active,
	Focus,
	Empty,
	FirstChild,
	LastChild,
	OnlyChild,
	NthChild,
}

public enum NthChildMode
{
	Literal,
	Odd,
	Even,
}

public sealed record StateRule
{
	public PseudoKind State { get; init; }
	public NthChildMode NthChildMode { get; init; } = NthChildMode.Literal;
	public int NthChildArg { get; init; } = 1;
	public Appearance Delta { get; init; } = Appearance.Default;

	public static int CompareCanonical( StateRule a, StateRule b )
	{
		int c = ((int)a.State).CompareTo( (int)b.State );
		if ( c != 0 ) return c;
		c = ((int)a.NthChildMode).CompareTo( (int)b.NthChildMode );
		if ( c != 0 ) return c;
		return a.NthChildArg.CompareTo( b.NthChildArg );
	}
}
