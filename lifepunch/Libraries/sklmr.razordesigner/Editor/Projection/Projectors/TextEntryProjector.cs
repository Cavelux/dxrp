using System.Collections.Generic;
using Grains.RazorDesigner.Projection.Appearance;
using Grains.RazorDesigner.Projection.Razor;

namespace Grains.RazorDesigner.Projection.Projectors;

[Projector( "TextEntry" )]
public sealed class TextEntryProjector : IControlProjector
{
    public string Kind => "TextEntry";

    public ProjectionResult Project( IReadOnlyNode node, IAppearance a, IPayload p, ProjectionContext ctx )
    {
        var scss = AppearanceScss.Emit(
            a,
            isRoot:       node.ClassName == Document.DesignerDocument.RootClassName,
            isContainer:  false,
            childCount:   0,
            isLabel:      false,
            isCheckbox:   false,
            checkboxSize: default );

        var nodeId = node.Id.ToString();
        var placeholder = p.Placeholder ?? "";
        var ops = new PanelOp[]
        {
            new SetAttribute( "data-grd-node-id", nodeId ),
            new SetAttribute( "placeholder", placeholder ),
        };

        var razorAttrs = new[]
        {
            RazorEmit.Attr( "data-grd-node-id", nodeId ),
            RazorEmit.Attr( "placeholder", placeholder ),
        };

        // RazorInnerText is null — <textentry> is self-closing (no inner text).
        return new ProjectionResult(
            PanelOps:        ops,
            ScssLines:       scss,
            RazorAttributes: razorAttrs,
            RazorInnerText:  null );
    }
}
