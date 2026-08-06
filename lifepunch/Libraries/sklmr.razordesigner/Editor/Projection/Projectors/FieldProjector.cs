using System.Collections.Generic;
using Grains.RazorDesigner.Projection.Appearance;
using Grains.RazorDesigner.Projection.Razor;

namespace Grains.RazorDesigner.Projection.Projectors;

[Projector( "Field" )]
public sealed class FieldProjector : IControlProjector
{
    public string Kind => "Field";

    public ProjectionResult Project( IReadOnlyNode node, IAppearance a, IPayload p, ProjectionContext ctx )
    {
        var scss = AppearanceScss.Emit(
            a,
            isRoot:       node.ClassName == Document.DesignerDocument.RootClassName,
            isContainer:  true,
            childCount:   node.Children.Count,
            isLabel:      false,
            isCheckbox:   false,
            checkboxSize: default );

        var nodeId = node.Id.ToString();
        var ops = new PanelOp[]
        {
            new SetAttribute( "data-grd-node-id", nodeId ),
        };

        var razorAttrs = new[] { RazorEmit.Attr( "data-grd-node-id", nodeId ) };

        return new ProjectionResult(
            PanelOps:        ops,
            ScssLines:       scss,
            RazorAttributes: razorAttrs,
            RazorInnerText:  null );
    }
}
