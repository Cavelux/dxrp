using System;
using System.Collections.Generic;
using Sandbox;

namespace Goo.Input;

// Polls a curated key catalog once per frame for rising-edge presses + modifier state. ReemitHeldOnModifierRise re-emits a held key when a modifier rises (so "hold W, press Ctrl" reads as a chord).
public sealed class KeyTracker
{
    public bool ReemitHeldOnModifierRise { get; set; } = false;

    public ModifierState Modifiers { get; private set; }
    public IReadOnlyList<KeyDescriptor> JustPressed => _justPressed;

    readonly IReadOnlyList<KeyDescriptor> _catalog;
    readonly HashSet<string>     _downLastFrame = new();
    readonly List<KeyDescriptor> _justPressed   = new();

    public KeyTracker() : this( KnownKeys.All ) { }

    public KeyTracker( IReadOnlyList<KeyDescriptor> catalog )
    {
        _catalog = catalog;
    }

    public void Reset()
    {
        _downLastFrame.Clear();
        _justPressed.Clear();
        Modifiers = default;
    }

    static readonly Func<string, bool> s_engineDown = Sandbox.Input.Keyboard.Down;

    public void Poll() => Poll( s_engineDown );

    // isDown seam exists because engine Input statics throw outside a running engine process.
    public void Poll( Func<string, bool> isDown )
    {
        _justPressed.Clear();
        var prev = Modifiers;

        for ( int i = 0; i < _catalog.Count; i++ )
        {
            var  d    = _catalog[i];
            bool down = isDown( d.EngineName );
            if ( down && !_downLastFrame.Contains( d.EngineName ) )
                _justPressed.Add( d );
            if ( down ) _downLastFrame.Add( d.EngineName );
            else        _downLastFrame.Remove( d.EngineName );
        }

        Modifiers = new ModifierState(
            _downLastFrame.Contains( "ctrl"  ),
            _downLastFrame.Contains( "shift" ),
            _downLastFrame.Contains( "alt"   ),
            _downLastFrame.Contains( "win"   ) );

        if ( !ReemitHeldOnModifierRise ) return;

        bool modRose = ( Modifiers.Ctrl  && !prev.Ctrl  ) || ( Modifiers.Shift && !prev.Shift )
                    || ( Modifiers.Alt   && !prev.Alt   ) || ( Modifiers.Meta  && !prev.Meta  );
        if ( !modRose ) return;

        for ( int i = 0; i < _catalog.Count; i++ )
        {
            var d = _catalog[i];
            if ( d.Class == KeyClass.Modifier ) continue;
            if ( !_downLastFrame.Contains( d.EngineName ) ) continue;
            if ( _justPressed.Contains( d ) ) continue;
            _justPressed.Add( d );
        }
    }

    /// <summary>True if the named key had a rising edge (just-pressed, not held) this frame; call Poll first.</summary>
    public bool Pressed( string engineName )
    {
        for ( int i = 0; i < _justPressed.Count; i++ )
            if ( _justPressed[i].EngineName == engineName ) return true;
        return false;
    }
}
