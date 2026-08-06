using System;

namespace Grains.RazorDesigner.Document;

public readonly record struct Edges( Length Top, Length Right, Length Bottom, Length Left )
{
	public static Edges Zero => new( Length.Px( 0 ), Length.Px( 0 ), Length.Px( 0 ), Length.Px( 0 ) );

	public static Edges Uniform( Length value ) => new( value, value, value, value );

	public bool IsUniform => Top == Right && Right == Bottom && Bottom == Left;

	// Top==Bottom AND Left==Right but not all four — the 2-value shorthand case.
	public bool IsSymmetric => Top == Bottom && Right == Left && !IsUniform;

	public bool IsDefaultZero => IsUniform && Top.Unit == LengthUnit.Px && Top.Value == 0f;

	public bool IsAllAuto =>
		Top.Unit == LengthUnit.Auto && Right.Unit == LengthUnit.Auto &&
		Bottom.Unit == LengthUnit.Auto && Left.Unit == LengthUnit.Auto;

	public string ToCss()
	{
		if ( IsUniform ) return Top.ToCss();
		if ( IsSymmetric ) return $"{Top.ToCss()} {Right.ToCss()}";
		return $"{Top.ToCss()} {Right.ToCss()} {Bottom.ToCss()} {Left.ToCss()}";
	}

	public static Edges Parse( string s )
	{
		if ( !TryParse( s, out var v ) )
			throw new FormatException( $"Edges.Parse: cannot parse \"{s}\"" );
		return v;
	}

	public static bool TryParse( string s, out Edges result )
	{
		result = Zero;
		if ( string.IsNullOrWhiteSpace( s ) ) return false;
		var parts = s.Trim().Split( (char[])null, StringSplitOptions.RemoveEmptyEntries );
		switch ( parts.Length )
		{
			case 1:
				if ( !Length.TryParse( parts[0], out var u ) ) return false;
				result = Uniform( u );
				return true;
			case 2:
				if ( !Length.TryParse( parts[0], out var v ) ) return false;
				if ( !Length.TryParse( parts[1], out var h ) ) return false;
				result = new Edges( v, h, v, h );
				return true;
			case 4:
				if ( !Length.TryParse( parts[0], out var t ) ) return false;
				if ( !Length.TryParse( parts[1], out var r ) ) return false;
				if ( !Length.TryParse( parts[2], out var b ) ) return false;
				if ( !Length.TryParse( parts[3], out var l ) ) return false;
				result = new Edges( t, r, b, l );
				return true;
			default:
				return false;
		}
	}

	public override string ToString() => ToCss();
}
