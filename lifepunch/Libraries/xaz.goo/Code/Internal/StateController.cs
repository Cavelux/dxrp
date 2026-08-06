using System.Text;
using System.Threading;
using Sandbox;
using Sandbox.UI;

namespace Goo.Internal;

// The full variant input set for one apply pass; record equality gates redundant re-emits.
internal readonly record struct VariantInputs(
    Color? BaseBg,  Color? BaseFg,
    Color? HoverBg, Color? ActiveBg, Color? FocusBg,
    Color? HoverFg, Color? ActiveFg, Color? FocusFg,
    int? TransitionMs);

internal sealed class StateController
{
    static int _nextKey;

    readonly Panel _host;
    readonly string _key;
    StyleSheet? _sheet;
    string? _lastCss;
    VariantInputs? _lastInputs;

    public StateController(Panel host)
    {
        _host = host;
        _key = $"goo-v-{Interlocked.Increment(ref _nextKey)}";
        _host.AddClass(_key);
    }

    public void ApplyVariants(
        Color? baseBg,  Color? baseFg,
        Color? hoverBg, Color? activeBg, Color? focusBg,
        Color? hoverFg, Color? activeFg, Color? focusFg,
        int? transitionMs)
    {
        // Engine routes input to the nearest Panel whose ComputedStyle.PointerEvents is All
        // (Panel.WantsMouseInput). Without this the :hover/:active pseudoclasses never flip.
        _host.Style.PointerEvents = PointerEvents.All;

        if (focusBg.HasValue || focusFg.HasValue)
            _host.AcceptsFocus = true;

        // BuildCss is deterministic, so identical inputs cannot need a re-emit; skip the string
        // build entirely (per-frame style writes on a variant-carrying panel land here every pass).
        var inputs = new VariantInputs(baseBg, baseFg, hoverBg, activeBg, focusBg, hoverFg, activeFg, focusFg, transitionMs);
        if (inputs == _lastInputs) return;
        _lastInputs = inputs;

        var css = BuildCss(
            _key,
            baseBg, baseFg,
            hoverBg, activeBg, focusBg,
            hoverFg, activeFg, focusFg,
            transitionMs);

        // Skip the swap when nothing changed so a per-frame rebuild does not thrash the stylesheet
        // (engine-fact #4); PointerEvents/AcceptsFocus above are idempotent, so early return is safe.
        if (!SheetChanged(css, _lastCss)) return;
        _lastCss = css;

        // Remove by instance, not by filename glob: FromString leaves FileName null (Facepunch
        // #10884, open) so Remove("...") never matches and sheets accumulate. Correct either way.
        if (_sheet is not null) _host.StyleSheet.Remove(_sheet);
        _sheet = StyleSheet.FromString(css, "goo-state-variants");
        _host.StyleSheet.Add(_sheet);

        // StyleSheet add/remove doesn't enqueue the panel for rule re-match, so the new base rule
        // sits unmatched until a later selector change (the "base color only on hover" bug). Toggle
        // our marker class to force StyleSelectorsChanged now; net class state is unchanged.
        _host.RemoveClass(_key);
        _host.AddClass(_key);
    }

    // Drop the variant sheet when a recycled host no longer declares any variant.
    // The reconciler reuses host panels positionally, so a slot that once held a
    // variant component (e.g. a Button with an Accent base + :hover) can be reused
    // for one with no variants; without this the stale base/:hover rules survive and
    // the panel renders the prior occupant's background. Keep the _key class so a
    // later ApplyVariants on the same reused host can reattach a sheet for that key.
    // True while a sheet is attached (used by the Applier's pointer-events apply-or-clear).
    public bool HasActiveVariants => _sheet is not null;

    public void ClearVariants()
    {
        if (_sheet is not null)
        {
            _host.StyleSheet.Remove(_sheet);
            _sheet = null;
        }
        // Drop the caches too, so a later ApplyVariants on this reused host re-emits and re-dirties
        // even if the new variant set matches the pre-clear inputs or css.
        _lastCss = null;
        _lastInputs = null;
        _host.AcceptsFocus = false;
    }

    // Splits the declared base colors between the inline style and the variant sheet.
    // The engine cascade merges inline styles after all stylesheet rules, so an inline
    // base would permanently shadow the sheet's :hover/:active rules; when a channel
    // has a variant its base moves into the sheet and the inline slot goes null.
    // Channels are independent. Shape panels never carry background-color on either
    // side (their background is the mask tint, written elsewhere).
    internal static void SplitBaseColors(
        Color? declaredBg, Color? declaredFg,
        bool hasBgVariant, bool hasFgVariant, bool isShape,
        out Color? inlineBg, out Color? inlineFg,
        out Color? sheetBg, out Color? sheetFg)
    {
        inlineBg = (isShape || hasBgVariant) ? null : declaredBg;
        sheetBg  = (!isShape && hasBgVariant) ? declaredBg : (Color?)null;
        inlineFg = hasFgVariant ? null : declaredFg;
        sheetFg  = hasFgVariant ? declaredFg : (Color?)null;
    }

    // Gates the sheet swap: true when the new css differs from what is applied (or nothing is yet).
    // An unchanged variant set is a no-op (no thrash); a changed base color re-emits and restyles.
    internal static bool SheetChanged(string newCss, string? appliedCss)
        => appliedCss is null || newCss != appliedCss;

    internal static string BuildCss(
        string key,
        Color? baseBg, Color? baseFg,
        Color? hoverBg, Color? activeBg, Color? focusBg,
        Color? hoverFg, Color? activeFg, Color? focusFg,
        int? transitionMs)
    {
        var sb = new StringBuilder();
        // Source order is the tiebreaker for same-specificity selectors: later wins.
        // Emitting base, :focus, :hover, :active gives Active > Hover > Focus > Base.
        AppendRule(sb, $".{key}",        baseBg,   baseFg,   transitionMs);
        AppendRule(sb, $".{key}:focus",  focusBg,  focusFg,  null);
        AppendRule(sb, $".{key}:hover",  hoverBg,  hoverFg,  null);
        AppendRule(sb, $".{key}:active", activeBg, activeFg, null);
        return sb.ToString();
    }

    static void AppendRule(StringBuilder sb, string selector, Color? bg, Color? fg, int? transitionMs)
    {
        bool hasTransition = transitionMs is int ms0 && ms0 > 0;
        if (bg is null && fg is null && !hasTransition) return;

        sb.Append(selector).Append(" { ");
        if (bg.HasValue) sb.Append("background-color: ").Append(CssColor(bg.Value)).Append("; ");
        if (fg.HasValue) sb.Append("color: ").Append(CssColor(fg.Value)).Append("; ");
        if (hasTransition)
        {
            int ms = transitionMs!.Value;
            sb.Append("transition: background-color ").Append(ms).Append("ms, color ").Append(ms).Append("ms; ");
        }
        sb.Append("}\n");
    }

    static string CssColor(Color c)
    {
        int r = (int)(c.r * 255);
        int g = (int)(c.g * 255);
        int b = (int)(c.b * 255);
        return $"rgba({r},{g},{b},{c.a:0.###})";
    }
}
