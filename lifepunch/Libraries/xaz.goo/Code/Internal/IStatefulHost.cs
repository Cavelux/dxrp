using Sandbox;

namespace Goo.Internal;

internal interface IStatefulHost
{
    void ApplyStateVariants(
        Color? baseBg,  Color? baseFg,
        Color? hoverBg, Color? activeBg, Color? focusBg,
        Color? hoverFg, Color? activeFg, Color? focusFg,
        int? transitionMs);

    // Shed any previously-applied variant sheet when the host (possibly recycled by
    // the reconciler) declares no variants this pass. No-op if none were applied.
    void ClearStateVariants();

    // True while a variant sheet is attached; such panels must keep capturing pointer events.
    bool HasActiveStateVariants { get; }
}
