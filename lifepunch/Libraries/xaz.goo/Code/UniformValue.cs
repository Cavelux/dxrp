using System;
using System.Collections.Generic;
using Sandbox;

namespace Goo;

/// <summary>A single shader-uniform value in a <see cref="ShaderEffect"/> bag: either a literal or a
/// per-frame <see cref="Func{T}"/> evaluated each Apply. Construct via implicit conversion.</summary>
public readonly struct UniformValue : IEquatable<UniformValue>
{
    readonly object? _literal;
    readonly Delegate? _func;

    UniformValue( object? literal, Delegate? func )
    {
        _literal = literal;
        _func = func;
    }

    /// <summary>True when this value is a per-frame Func re-evaluated on each Apply.</summary>
    public bool IsDynamic => _func is not null;

    /// <summary>The current value: evaluates the Func for dynamic entries, otherwise returns the literal.</summary>
    public object Resolve() => _func is not null ? _func.DynamicInvoke()! : _literal!;

    public static implicit operator UniformValue( float v )   => new( v, null );
    public static implicit operator UniformValue( bool v )    => new( v, null );
    public static implicit operator UniformValue( Vector2 v ) => new( v, null );
    public static implicit operator UniformValue( Vector3 v ) => new( v, null );
    public static implicit operator UniformValue( Vector4 v ) => new( v, null );
    public static implicit operator UniformValue( Color v )   => new( v, null );
    public static implicit operator UniformValue( Texture v ) => new( v, null );

    /// <summary>Per-frame push of Sandbox.Time.Now; only for attributes needing CPU-side time, most Goo shaders read g_flTime instead. Static field so bag equality holds across rebuilds.</summary>
    public static readonly UniformValue Time = (Func<float>)(() => Sandbox.Time.Now);

    public static implicit operator UniformValue( Func<float> f )   => new( null, f );
    public static implicit operator UniformValue( Func<bool> f )    => new( null, f );
    public static implicit operator UniformValue( Func<Vector2> f ) => new( null, f );
    public static implicit operator UniformValue( Func<Vector3> f ) => new( null, f );
    public static implicit operator UniformValue( Func<Vector4> f ) => new( null, f );
    public static implicit operator UniformValue( Func<Color> f )   => new( null, f );
    public static implicit operator UniformValue( Func<Texture> f ) => new( null, f );

    // Func entries compare by delegate reference: a fresh lambda each Build differs, so the effect
    // re-pushes every frame (intended for per-frame uniforms). Literals compare by value.
    public bool Equals( UniformValue other )
        => (_func is not null || other._func is not null)
            ? ReferenceEquals( _func, other._func )
            : EqualityComparer<object?>.Default.Equals( _literal, other._literal );

    public override bool Equals( object? obj ) => obj is UniformValue other && Equals( other );

    public override int GetHashCode()
        => _func is not null ? _func.GetHashCode() : (_literal?.GetHashCode() ?? 0);

    public static bool operator ==( UniformValue a, UniformValue b ) => a.Equals( b );
    public static bool operator !=( UniformValue a, UniformValue b ) => !a.Equals( b );
}
