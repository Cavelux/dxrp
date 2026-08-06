HEADER
{
	DevShader = true;
	Version = 1;
}

MODES
{
	Default();
	Forward();
}

FEATURES
{
	#include "ui/features.hlsl"
}

COMMON
{
	#include "ui/common.hlsl"
}

VS
{
	#include "ui/vertex.hlsl"
}

PS
{
	#include "ui/pixel.hlsl"

	// Sector and Arc both render through this shader (kind 0); C# pre-expands an Arc (centre radius
	// + stroke) into inner/outer. Computed in unit-square UV (centre 0.5) so it stretches with the panel.
	float4 ShapeColor  < Attribute( "ShapeColor" );  Default4( 1.0, 1.0, 1.0, 1.0 ); >;
	float  ShapeStart  < Attribute( "ShapeStart" );  Default( 0.0 ); >;   // StartAngle, degrees (0 = up, cw+)
	float  ShapeEnd    < Attribute( "ShapeEnd" );    Default( 0.0 ); >;   // EndAngle, degrees
	float  ShapeInner  < Attribute( "ShapeInner" );  Default( 0.0 ); >;   // InnerRadius, 0..1 of half-extent
	float  ShapeOuter  < Attribute( "ShapeOuter" );  Default( 1.0 ); >;   // OuterRadius, 0..1 of half-extent
	float  ShapeCorner < Attribute( "ShapeCorner" ); Default( 0.0 ); >;   // CornerRadius, 0..1 of half-extent
	float  ShapeKind   < Attribute( "ShapeKind" );   Default( 0.0 ); >;   // 0 sector, 4 polygon

		Texture2D    g_tPoints < Attribute( "PointsTex" ); SrgbRead( false ); >;
		float        PointCount < Attribute( "PointCount" ); Default( 0.0 ); >;
		#define SHAPE_MAXPTS 64

	RenderState( SrgbWriteEnable0, true );
	RenderState( ColorWriteEnable0, RGBA );
	RenderState( FillMode, SOLID );
	RenderState( CullMode, NONE );
	RenderState( DepthWriteEnable, false );

	// The IPanelDraw custom-draw path does not set the D_BLENDMODE combo, so without an explicit
	// blend state the quad draws fully opaque and the SDF alpha is ignored (every shape becomes a
	// solid square). Straight alpha-over so the coverage actually composites against the background.
	RenderState( BlendEnable, true );
	RenderState( SrcBlend, SRC_ALPHA );
	RenderState( DstBlend, INV_SRC_ALPHA );
	RenderState( BlendOp, ADD );
	RenderState( SrcBlendAlpha, ONE );
	RenderState( DstBlendAlpha, INV_SRC_ALPHA );
	RenderState( BlendOpAlpha, ADD );

	#define SHAPE_PI  3.14159265358979
	#define SHAPE_TAU 6.28318530717959

	// Coverage 0..1 for the sector/ring at this UV: radial band + angular sweep with AA, optional
	// rounded corners. AA bands use fwidth(dist) so the edge stays ~1 screen pixel at any panel size.
	float SectorCoverage( float2 uv )
	{
		float2 d = uv - 0.5;
		float dist = length( d );
		float aa = max( fwidth( dist ), 1e-5 );

		float outerR = 0.5 * ShapeOuter;
		float innerR = 0.5 * ShapeInner;

		float outerA = 1.0 - smoothstep( outerR - aa, outerR + aa, dist );
		float innerA = smoothstep( innerR - aa, innerR + aa, dist );
		float a = outerA * innerA;

		float startRad = radians( ShapeStart );
		float endRad   = radians( ShapeEnd );
		float arcRad   = endRad - startRad;
		if ( arcRad < 0.0 ) arcRad += SHAPE_TAU;
		arcRad = min( arcRad, SHAPE_TAU );
		bool fullSweep = arcRad >= SHAPE_TAU - 1e-4;

		if ( !fullSweep && a > 0.0 )
		{
			// 0 = up, cw+: atan2(dx, -dy) puts 0 at 12 o'clock, increasing clockwise.
			float angle = atan2( d.x, -d.y );
			if ( angle < 0.0 ) angle += SHAPE_TAU;
			float rel = angle - startRad;
			rel -= SHAPE_TAU * floor( rel / SHAPE_TAU );   // wrap into [0, TAU)

			float angAa = aa / max( dist, 1e-3 );
			float angA = smoothstep( -angAa, angAa, rel );
			float angB = 1.0 - smoothstep( arcRad - angAa, arcRad + angAa, rel );
			a *= angA * angB;

			if ( ShapeCorner > 0.0 && a > 0.0 )
			{
				float cornerR = 0.5 * ShapeCorner;
				float dInner = dist - innerR;        // past the inner arc
				float dOuter = outerR - dist;        // before the outer arc
				float dStart = rel * dist;           // arc length from the start radial
				float dEnd   = ( arcRad - rel ) * dist;

				float ca = 1.0;
				if ( dInner < cornerR && dStart < cornerR ) { float2 c = cornerR - float2( dInner, dStart ); ca = min( ca, 1.0 - smoothstep( cornerR - aa, cornerR + aa, length( c ) ) ); }
				if ( dInner < cornerR && dEnd   < cornerR ) { float2 c = cornerR - float2( dInner, dEnd   ); ca = min( ca, 1.0 - smoothstep( cornerR - aa, cornerR + aa, length( c ) ) ); }
				if ( dOuter < cornerR && dStart < cornerR ) { float2 c = cornerR - float2( dOuter, dStart ); ca = min( ca, 1.0 - smoothstep( cornerR - aa, cornerR + aa, length( c ) ) ); }
				if ( dOuter < cornerR && dEnd   < cornerR ) { float2 c = cornerR - float2( dOuter, dEnd   ); ca = min( ca, 1.0 - smoothstep( cornerR - aa, cornerR + aa, length( c ) ) ); }
				a *= ca;
			}
		}

		return saturate( a );
	}

	float2 ShapeLoadPoint( int i )
	{
		float4 t = g_tPoints.Load( int3( i, 0, 0 ) );
		float x = ( t.r * 255.0 * 256.0 + t.g * 255.0 ) / 65535.0;
		float y = ( t.b * 255.0 * 256.0 + t.a * 255.0 ) / 65535.0;
		return float2( x, y );
	}

	// Signed distance to the polygon outline (<0 inside). uv in [0,1], points in [0,1]. IQ sdPolygon.
	float ShapePolygonSdf( float2 p, int n )
	{
		float2 v0 = ShapeLoadPoint( 0 );
		float d = dot( p - v0, p - v0 );
		float s = 1.0;
		float2 vj = ShapeLoadPoint( n - 1 );
		[loop] for ( int i = 0; i < n && i < SHAPE_MAXPTS; i++ )
		{
			float2 vi = ShapeLoadPoint( i );
			float2 e = vj - vi;
			float2 w = p - vi;
			float2 b = w - e * clamp( dot( w, e ) / dot( e, e ), 0.0, 1.0 );
			d = min( d, dot( b, b ) );
			bool3 c = bool3( p.y >= vi.y, p.y < vj.y, e.x * w.y > e.y * w.x );
			if ( all( c ) || all( !c ) ) s = -s;
			vj = vi;
		}
		return s * sqrt( d );
	}

	// Dispatch on ShapeKind: kind 0 = Sector/Arc, kind 4 = arbitrary polygon (SDF over the points-texture).
	float ShapeCoverage( float2 uv )
	{
		if ( ShapeKind < 0.5 ) return SectorCoverage( uv );

		// kind 4: arbitrary polygon
		int n = (int)( PointCount + 0.5 );
		if ( n < 3 ) return 0.0;
		float f = ShapePolygonSdf( uv, n );           // <0 inside
		float aa = max( fwidth( f ), 1e-5 );
		return saturate( 0.5 - f / ( 2.0 * aa ) );
	}

	PS_OUTPUT MainPs( PS_INPUT i )
	{
		PS_OUTPUT o;
		UI_CommonProcessing_Pre( i );

		float a = ShapeCoverage( i.vTexCoord.xy );

		float4 col = ShapeColor;
		col.rgb = SrgbGammaToLinear( col.rgb );
		col.a *= a * i.vColor.a;

		o.vColor = col;
		return UI_CommonProcessing_Post( i, o );
	}
}
