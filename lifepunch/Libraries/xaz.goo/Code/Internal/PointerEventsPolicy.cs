using Sandbox.UI;

namespace Goo.Internal;

// Pure resolution of effective PointerEvents. An explicit value wins. Otherwise:
//   - intrinsically-interactive panels (WebPanel, TextEntry, Embed-hosted) or any event handler gate to All;
//   - a panel relying on a :hover/:active/:focus state variant stays capturing (unset/null,
//     preserving the engine default) so the variant can still trigger;
//   - a fully inert panel resolves to None, so it is pointer-transparent and does not eat
//     clicks meant for siblings or panels below it (removes the inert-wrapper idiom).
internal static class PointerEventsPolicy
{
    public static PointerEvents? Resolve( PointerEvents? declared, bool isWebPanel, bool hasHandlers, bool hasStateVariant = false, bool isTextEntry = false, bool isEmbed = false )
    {
        if ( declared.HasValue ) return declared;
        if ( isWebPanel || isTextEntry || hasHandlers || isEmbed ) return PointerEvents.All;
        if ( hasStateVariant ) return null;
        return PointerEvents.None;
    }
}
