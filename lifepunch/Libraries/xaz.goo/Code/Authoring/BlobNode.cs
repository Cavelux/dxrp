using System;
using System.Collections.Generic;
using Goo;
using Sandbox;
using Sandbox.UI;

namespace Goo.Authoring;

/// <summary>Maps a GameObject hierarchy under GooHierarchyHost to an ordered Goo blob tree.</summary>
public abstract class BlobNode : Component
{
	/// <summary>Optional property-name to token-name bindings. Literal values remain stored as fallbacks.</summary>
	[Property, Hide]
	public Dictionary<string, string> TokenBindings { get; set; } = new();

	/// <summary>Horizontal translation applied before the other transform operations.</summary>
	[Property, GooToken, Group( "Transform", StartFolded = true ), Order( 140 ), Editor( "goo-axis-lengths" ), Title( "Translate" )]
	public GooLength TransformTranslateX { get; set; } = new() { Unit = LengthUnit.Pixels };

	/// <summary>Vertical translation applied before the other transform operations.</summary>
	[Property, GooToken, Group( "Transform", StartFolded = true ), Order( 140 ), Hide]
	public GooLength TransformTranslateY { get; set; } = new() { Unit = LengthUnit.Pixels };

	/// <summary>Clockwise rotation in degrees.</summary>
	[Property, GooToken, Group( "Transform", StartFolded = true ), Order( 140 ), Editor( "goo-float" ), Title( "Rotation (Degrees)" ), Step( 0.1f )]
	public float TransformRotation { get; set; }

	/// <summary>Horizontal scale. The untouched axis remains at 1.</summary>
	[Property, GooToken, Group( "Transform", StartFolded = true ), Order( 140 ), Editor( "goo-axis-floats" ), Title( "Scale" ), Step( 0.01f )]
	public float TransformScaleX { get; set; } = 1f;

	/// <summary>Vertical scale. The untouched axis remains at 1.</summary>
	[Property, GooToken, Group( "Transform", StartFolded = true ), Order( 140 ), Hide, Step( 0.01f )]
	public float TransformScaleY { get; set; } = 1f;

	/// <summary>Horizontal skew in degrees.</summary>
	[Property, GooToken, Group( "Transform", StartFolded = true ), Order( 140 ), Editor( "goo-axis-floats" ), Title( "Skew (Degrees)" ), Step( 0.1f )]
	public float TransformSkewX { get; set; }

	/// <summary>Vertical skew in degrees.</summary>
	[Property, GooToken, Group( "Transform", StartFolded = true ), Order( 140 ), Hide, Step( 0.1f )]
	public float TransformSkewY { get; set; }

	/// <summary>Movie-maker mirror of TransformTranslateX in pixels: keyframes tween.</summary>
	public float TransformTranslateXAnim { get => TransformTranslateX.Value; set => TransformTranslateX = new GooLength { Value = value, Unit = LengthUnit.Pixels }; }

	/// <summary>Movie-maker mirror of TransformTranslateY in pixels: keyframes tween.</summary>
	public float TransformTranslateYAnim { get => TransformTranslateY.Value; set => TransformTranslateY = new GooLength { Value = value, Unit = LengthUnit.Pixels }; }

	[Property, SingleAction, Group( "Events", StartFolded = true ), Order( 130 )] public GooPointerAction OnClick { get; set; }
	[Property, SingleAction, Group( "Events", StartFolded = true ), Order( 130 )] public GooPointerAction OnRightClick { get; set; }
	[Property, SingleAction, Group( "Events", StartFolded = true ), Order( 130 )] public GooPointerAction OnMiddleClick { get; set; }
	[Property, SingleAction, Group( "Events", StartFolded = true ), Order( 130 )] public GooPointerAction OnMouseEnter { get; set; }
	[Property, SingleAction, Group( "Events", StartFolded = true ), Order( 130 )] public GooPointerAction OnMouseLeave { get; set; }
	[Property, SingleAction, Group( "Events", StartFolded = true ), Order( 130 )] public GooPointerAction OnMouseDown { get; set; }
	[Property, SingleAction, Group( "Events", StartFolded = true ), Order( 130 )] public GooPointerAction OnMouseUp { get; set; }
	[Property, SingleAction, Group( "Events", StartFolded = true ), Order( 130 )] public GooPointerAction OnMouseMove { get; set; }

	/// <summary>Reconciler key: stable per GameObject, survives reorders.</summary>
	internal string BlobKey => GameObject.Id.ToString();

	internal Goo.PanelTransform? BuildStyleTransform()
	{
		var translateX = ResolveStyleValue( nameof( TransformTranslateX ), TransformTranslateX );
		var translateY = ResolveStyleValue( nameof( TransformTranslateY ), TransformTranslateY );
		var rotation = ResolveStyleValue( nameof( TransformRotation ), TransformRotation );
		var scaleX = ResolveStyleValue( nameof( TransformScaleX ), TransformScaleX );
		var scaleY = ResolveStyleValue( nameof( TransformScaleY ), TransformScaleY );
		var skewX = ResolveStyleValue( nameof( TransformSkewX ), TransformSkewX );
		var skewY = ResolveStyleValue( nameof( TransformSkewY ), TransformSkewY );
		Goo.PanelTransform? transform = null;

		if ( translateX.Value != 0f || translateY.Value != 0f )
		{
			transform = Goo.PanelTransform.Translate( translateX, translateY );
		}

		if ( rotation != 0f )
			transform = transform is { } currentRotation ? currentRotation.Rotate( rotation ) : Goo.PanelTransform.Rotate( rotation );

		if ( scaleX != 1f || scaleY != 1f )
		{
			var scale = new Vector3( scaleX, scaleY, 1f );
			transform = transform is { } currentScale ? currentScale.Scale( scale ) : Goo.PanelTransform.Scale( scale );
		}

		if ( skewX != 0f || skewY != 0f )
		{
			transform = transform is { } currentSkew
				? currentSkew.Skew( skewX, skewY )
				: Goo.PanelTransform.Skew( skewX, skewY );
		}

		return transform;
	}

	internal T ResolveStyleValue<T>( string propertyName, T literal )
	{
		if ( TokenBindings is null || !TokenBindings.TryGetValue( propertyName, out var tokenName )
			|| string.IsNullOrWhiteSpace( tokenName ) || !Goo.Tokens.TryGet<object>( tokenName, out var raw )
			|| raw is null )
			return literal;

		if ( raw is T typed )
			return typed;

		if ( raw is Length length && (typeof( T ) == typeof( GooLength ) || typeof( T ) == typeof( GooLength? )) )
			return (T)(object)(GooLength)length;

		return literal;
	}

	internal Action<MousePanelEvent> BlobOnClick => OnClick is null ? null : e => OnClick( this, e.LocalPosition, e.MouseButton );
	internal Action<MousePanelEvent> BlobOnRightClick => OnRightClick is null ? null : e => OnRightClick( this, e.LocalPosition, e.MouseButton );
	internal Action<MousePanelEvent> BlobOnMiddleClick => OnMiddleClick is null ? null : e => OnMiddleClick( this, e.LocalPosition, e.MouseButton );
	internal Action<MousePanelEvent> BlobOnMouseEnter => OnMouseEnter is null ? null : e => OnMouseEnter( this, e.LocalPosition, e.MouseButton );
	internal Action<MousePanelEvent> BlobOnMouseLeave => OnMouseLeave is null ? null : e => OnMouseLeave( this, e.LocalPosition, e.MouseButton );
	internal Action<MousePanelEvent> BlobOnMouseDown => OnMouseDown is null ? null : e => OnMouseDown( this, e.LocalPosition, e.MouseButton );
	internal Action<MousePanelEvent> BlobOnMouseUp => OnMouseUp is null ? null : e => OnMouseUp( this, e.LocalPosition, e.MouseButton );
	internal Action<MousePanelEvent> BlobOnMouseMove => OnMouseMove is null ? null : e => OnMouseMove( this, e.LocalPosition, e.MouseButton );

	/// <summary>Build this node's blob and add it to the parent's children.</summary>
	internal abstract void AddTo( Children children );

	/// <summary>Add every enabled BlobNode on direct children of <paramref name="parent"/>, in hierarchy order.</summary>
	internal static void AddChildNodes( GameObject parent, Children children )
	{
		foreach ( var go in parent.Children )
		{
			if ( !go.Enabled ) continue;
			AddGameObject( go, children );
		}
	}

	static void AddGameObject( GameObject gameObject, Children children )
	{
		void AddUnscoped()
		{
			if ( gameObject.Components.Get<BlobNode>() is { } node )
				node.AddTo( children );
			else
				AddChildNodes( gameObject, children ); // node-less GO (e.g. prefab wrapper root) is transparent
		}

		if ( gameObject.Components.Get<GooTokenScope>() is { } scope )
			scope.Apply( AddUnscoped );
		else
			AddUnscoped();
	}
}
