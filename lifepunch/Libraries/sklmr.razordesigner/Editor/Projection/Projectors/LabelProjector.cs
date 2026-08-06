using System.Collections.Generic;
using Grains.RazorDesigner.Projection.Appearance;
using Grains.RazorDesigner.Projection.Razor;

namespace Grains.RazorDesigner.Projection.Projectors;

[Projector( "Label" )]
public sealed class LabelProjector : IControlProjector
{
    public string Kind => "Label";

    public ProjectionResult Project( IReadOnlyNode node, IAppearance a, IPayload p, ProjectionContext ctx )
    {
        var scss = AppearanceScss.Emit(
            a,
            isRoot:       node.ClassName == Document.DesignerDocument.RootClassName,
            isContainer:  false,
            childCount:   0,
            isLabel:      true,
            isCheckbox:   false,
            checkboxSize: default );

        var nodeId = node.Id.ToString();
        var ops = new PanelOp[]
        {
            new SetAttribute( "data-grd-node-id", nodeId ),
            new SetInnerText( p.Content ?? "" ),
        };

        var razorAttrs = new[] { RazorEmit.Attr( "data-grd-node-id", nodeId ) };

        return new ProjectionResult(
            PanelOps:       ops,
            ScssLines:      scss,
            RazorAttributes: razorAttrs,
            RazorInnerText: Escape.Html( p.Content ?? "" ) );
    }
}
