using System;
using System.Collections.Generic;
using Sandbox;

namespace Goo;

/// <summary>Per-particle draw callback for <see cref="Hud.Particles"/> (named, not Action, because Canvas is a ref struct).</summary>
public delegate void ParticleDraw( Canvas canvas, ParticleField.Particle p );

/// <summary>Per-particle force run each <see cref="ParticleField.Step"/> before integration: steer Vel, or set anything on the particle.</summary>
public delegate void ParticleForce( ref ParticleField.Particle p, float dt );

/// <summary>
/// A tiny CPU particle simulation for UI.
/// You own it as a field, spawn into it (<see cref="Burst"/> for a radial pop, <see cref="Emit"/> for any pattern),
/// and mount it with <see cref="Hud.Particles"/> which steps and renders it from the paint callback (no Tick).
/// Any KIND of particle is the same three knobs: where it spawns (Emit/Burst), how it moves (<see cref="Gravity"/>,
/// <see cref="Drag"/>, <see cref="Force"/>), how it looks (<see cref="Draw"/>).
/// </summary>
public sealed class ParticleField
{
    public struct Particle
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float   Age;
        public float   Life;
        public float   Size;
        /// <summary>Stable per-particle random 0..1, set at spawn. Phase for desynced sway/twinkle/flicker.</summary>
        public float   Seed;
        public Color   Color;
    }

    readonly List<Particle> _live = new();
    readonly Random _rng = new();

    /// <summary>Downward acceleration, px/s^2. default(Vector2) = drifting particles (no fall).</summary>
    public Vector2 Gravity = new( 0f, 900f );

    /// <summary>Per-second velocity damping (0 = none).</summary>
    public float Drag = 0f;

    /// <summary>Optional custom force, applied per particle before gravity/drag/integration. Null = plain ballistic.</summary>
    public ParticleForce? Force;

    /// <summary>Per-particle look. Defaults to a fading, shrinking dot; reassign for confetti, smoke, sparks, etc.</summary>
    public ParticleDraw Draw = DefaultDot;

    public int Count => _live.Count;

    /// <summary>Read-only view of the live particles (for rendering / tests / inspection).</summary>
    public IReadOnlyList<Particle> Live => _live;

    /// <summary>Spawn <paramref name="count"/> particles from <paramref name="origin"/> with random omnidirectional spread.</summary>
    public void Burst( Vector2 origin, int count, float speed, Color color, float life = 1.2f, float size = 6f )
    {
        for ( int i = 0; i < count; i++ )
        {
            float ang = (float)( _rng.NextDouble() * Math.Tau );
            float spd = speed * ( 0.4f + 0.6f * (float)_rng.NextDouble() );
            Add( new Particle
            {
                Pos   = origin,
                Vel   = new Vector2( MathF.Cos( ang ), MathF.Sin( ang ) ) * spd,
                Age   = 0f,
                Life  = life * ( 0.7f + 0.6f * (float)_rng.NextDouble() ),
                Size  = size * ( 0.6f + 0.8f * (float)_rng.NextDouble() ),
                Seed  = (float)_rng.NextDouble(),
                Color = color,
            } );
        }
    }

    /// <summary>General spawn: add <paramref name="count"/> particles, each built by <paramref name="make"/> (index in). Any pattern - a row, a line, a jet, a ring.</summary>
    public void Emit( int count, Func<int, Particle> make )
    {
        for ( int i = 0; i < count; i++ ) Add( make( i ) );
    }

    /// <summary>Add a single fully-specified particle.</summary>
    public void Add( Particle p ) => _live.Add( p );

    /// <summary>Advance the simulation by <paramref name="dt"/> seconds: age, cull dead, run Force, then gravity/drag/integrate.</summary>
    public void Step( float dt )
    {
        for ( int i = _live.Count - 1; i >= 0; i-- )
        {
            var p = _live[i];
            p.Age += dt;
            if ( p.Age >= p.Life ) { _live.RemoveAt( i ); continue; }
            Force?.Invoke( ref p, dt );
            p.Vel += Gravity * dt;
            if ( Drag > 0f ) p.Vel *= MathF.Max( 0f, 1f - Drag * dt );
            p.Pos += p.Vel * dt;
            _live[i] = p;
        }
    }

    /// <summary>Default <see cref="Draw"/>: a dot that fades and shrinks over its life.</summary>
    static void DefaultDot( Canvas canvas, Particle p )
    {
        float t = p.Age / p.Life;
        float r = p.Size * ( 1f - 0.4f * t );
        canvas.Circle( p.Pos, r, new Color( p.Color.r, p.Color.g, p.Color.b, p.Color.a * ( 1f - t ) ) );
    }
}
