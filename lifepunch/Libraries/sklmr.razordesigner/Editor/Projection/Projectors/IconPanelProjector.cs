using System.Collections.Generic;
using Grains.RazorDesigner.Projection.Appearance;
using Grains.RazorDesigner.Projection.Razor;

namespace Grains.RazorDesigner.Projection.Projectors;

[Projector( "IconPanel" )]
public sealed class IconPanelProjector : IControlProjector
{
    public string Kind => "IconPanel";

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
        var ops = new PanelOp[]
        {
            new SetAttribute( "data-grd-node-id", nodeId ),
            new SetInnerText( p.IconName ?? "" ),
        };

        var razorAttrs = new[] { RazorEmit.Attr( "data-grd-node-id", nodeId ) };

        return new ProjectionResult(
            PanelOps:        ops,
            ScssLines:       scss,
            RazorAttributes: razorAttrs,
            RazorInnerText:  Escape.Html( p.IconName ?? "" ) );
    }
}
