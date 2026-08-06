using System.Collections.Generic;
using Grains.RazorDesigner.Projection.Appearance;
using Grains.RazorDesigner.Projection.Razor;

namespace Grains.RazorDesigner.Projection.Projectors;

[Projector( "Image" )]
public sealed class ImageProjector : IControlProjector
{
    public string Kind => "Image";

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
        var source = p.Source ?? "";
        var ops = new List<PanelOp>
        {
            new SetAttribute( "data-grd-node-id", nodeId ),
            new SetAttribute( "src", source ),
        };
        if ( ctx.ForPreview && string.IsNullOrEmpty( p.Source ) )
            ops.Add( new SetClass( "preview-image-empty" ) );

        var razorAttrs = new[]
        {
            RazorEmit.Attr( "data-grd-node-id", nodeId ),
            RazorEmit.Attr( "src", source ),
        };

        // RazorInnerText is null — <image> is self-closing (no inner text).
        return new ProjectionResult(
            PanelOps:        ops,
            ScssLines:       scss,
            RazorAttributes: razorAttrs,
            RazorInnerText:  null );
    }
}
