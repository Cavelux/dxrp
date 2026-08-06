using System.Collections.Generic;

namespace Goo.Input;

// Curated catalog of keys with engine-name + display + shift-glyph mapping (modifiers, A-Z, digits/symbols on US layout, specials, F1-F12). Not exhaustive; numpad and non-US layouts are out of scope.
public static class KnownKeys
{
    // Declared before All so its initializer runs first; Build() reads this array.
    static readonly (string Engine, string Display)[] s_specials =
    {
        ("space","Space"), ("enter","Enter"), ("tab","Tab"), ("escape","Esc"),
        ("backspace","Backspace"), ("delete","Delete"), ("insert","Insert"),
        ("home","Home"), ("end","End"), ("pageup","PageUp"), ("pagedown","PageDown"),
        // engine button names for arrows are the Source bind names, not "up"/"down"/etc
        ("leftarrow","Left"), ("rightarrow","Right"), ("uparrow","Up"), ("downarrow","Down"),
    };

    public static IReadOnlyList<KeyDescriptor> All { get; } = Build();

    // Shift returns the shifted form of a printable glyph, or the input if not shifted /
    // not in the table. Letters are upcased by arithmetic; symbols use the US layout.
    public static char Shift( char baseGlyph, bool shiftDown )
    {
        if ( !shiftDown ) return baseGlyph;
        if ( baseGlyph >= 'a' && baseGlyph <= 'z' ) return (char)( baseGlyph - 32 );
        return baseGlyph switch
        {
            '1' => '!', '2' => '@', '3' => '#', '4' => '$', '5' => '%',
            '6' => '^', '7' => '&', '8' => '*', '9' => '(', '0' => ')',
            '-' => '_', '=' => '+', '[' => '{', ']' => '}', '\\' => '|',
            ';' => ':', '\'' => '"', ',' => '<', '.' => '>', '/' => '?',
            '`' => '~',
            _   => baseGlyph,
        };
    }

    static KeyDescriptor[] Build()
    {
        var t = new List<KeyDescriptor>( 80 )
        {
            new( "ctrl",  KeyClass.Modifier, '\0', '\0', "Ctrl"  ),
            new( "shift", KeyClass.Modifier, '\0', '\0', "Shift" ),
            new( "alt",   KeyClass.Modifier, '\0', '\0', "Alt"   ),
            new( "win",   KeyClass.Modifier, '\0', '\0', "Meta"  ),
        };
        for ( char c = 'A'; c <= 'Z'; c++ )
        {
            char lower = (char)( c + 32 );
            t.Add( new( c.ToString(), KeyClass.Printable, lower, c, c.ToString() ) );
        }
        foreach ( char c in "0123456789-=[]\\;',./`" )
            t.Add( new( c.ToString(), KeyClass.Printable, c, Shift( c, true ), c.ToString() ) );
        foreach ( var (eng, disp) in s_specials )
            t.Add( new( eng, KeyClass.Special, '\0', '\0', disp ) );
        for ( int i = 1; i <= 12; i++ )
            t.Add( new( $"f{i}", KeyClass.Special, '\0', '\0', $"F{i}" ) );
        return t.ToArray();
    }
}
