using System.Collections.Generic;
using Grains.RazorDesigner.Projection.Appearance;
using Grains.RazorDesigner.Projection.Razor;

namespace Grains.RazorDesigner.Projection.Projectors;

[Projector( "SplitContainer" )]
public sealed class SplitContainerProjector : IControlProjector
{
    public string Kind => "SplitContainer";

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

        // --- RazorAttributes ---
        var razorAttrs = new[] { RazorEmit.Attr( "data-grd-node-id", nodeId ) };

        return new ProjectionResult(
            PanelOps:        ops,
            ScssLines:       scss,
            RazorAttributes: razorAttrs,
            RazorInnerText:  null );  // container — inner text is child elements, not text content
    }
}
