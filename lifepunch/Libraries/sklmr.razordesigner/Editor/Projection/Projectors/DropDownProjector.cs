using System.Collections.Generic;
using Grains.RazorDesigner.Projection.Appearance;
using Grains.RazorDesigner.Projection.Razor;

namespace Grains.RazorDesigner.Projection.Projectors;

[Projector( "DropDown" )]
public sealed class DropDownProjector : IControlProjector
{
    public string Kind => "DropDown";

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
        var ops = new List<PanelOp>
        {
            new SetAttribute( "data-grd-node-id", nodeId ),
        };
        if ( ctx.ForPreview )
        {
            ops.Add( new SetClass( "preview-panel" ) );
            ops.Add( new SetClass( "preview-dropdown" ) );
        }

        var razorAttrs = new[] { RazorEmit.Attr( "data-grd-node-id", nodeId ) };

        // RazorInnerText is null — DropDown is non-container; self-close tag, no inner text.
        return new ProjectionResult(
            PanelOps:        ops,
            ScssLines:       scss,
            RazorAttributes: razorAttrs,
            RazorInnerText:  null );
    }
}
