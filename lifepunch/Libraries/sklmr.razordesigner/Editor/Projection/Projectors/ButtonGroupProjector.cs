using System.Collections.Generic;
using Grains.RazorDesigner.Projection.Appearance;
using Grains.RazorDesigner.Projection.Razor;

namespace Grains.RazorDesigner.Projection.Projectors;

[Projector( "ButtonGroup" )]
public sealed class ButtonGroupProjector : IControlProjector
{
    public string Kind => "ButtonGroup";

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
        var ops = new List<PanelOp>
        {
            new SetAttribute( "data-grd-node-id", nodeId ),
        };
        if ( ctx.ForPreview )
        {
            ops.Add( new SetClass( "preview-panel" ) );
            ops.Add( new SetClass( "preview-buttongroup" ) );
        }

        var razorAttrs = new[] { RazorEmit.Attr( "data-grd-node-id", nodeId ) };

        // RazorInnerText is null — ButtonGroup is a container; child elements are the Applier's job.
        return new ProjectionResult(
            PanelOps:        ops,
            ScssLines:       scss,
            RazorAttributes: razorAttrs,
            RazorInnerText:  null );
    }
}
