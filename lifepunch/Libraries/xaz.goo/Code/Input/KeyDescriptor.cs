namespace Goo.Input;

public enum KeyClass { Modifier, Printable, Special }

// One row of Goo.Input.KnownKeys. BaseGlyph and ShiftGlyph are '\0' for non-printable keys.
// EngineName is the literal string accepted by Sandbox.Input.Keyboard.Down, lowercase for
// modifiers (ctrl/shift/alt/win) and function keys (f1..f12), uppercase for letter keys.
public readonly record struct KeyDescriptor(
    string   EngineName,
    KeyClass Class,
    char     BaseGlyph,
    char     ShiftGlyph,
    string   DisplayName );

public readonly record struct ModifierState( bool Ctrl, bool Shift, bool Alt, bool Meta )
{
    public bool AnyNonShift => Ctrl || Alt || Meta;
    public bool Any         => Ctrl || Shift || Alt || Meta;
}
