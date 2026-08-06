using Grains.RazorDesigner.Projection.Appearance;
using Grains.RazorDesigner.Projection.Razor;

namespace Grains.RazorDesigner.Projection.Projectors;

[Projector( "Checkbox" )]
public sealed class CheckboxProjector : IControlProjector
{
    public string Kind => "Checkbox";

    public ProjectionResult Project( IReadOnlyNode node, IAppearance a, IPayload p, ProjectionContext ctx )
    {
        var scss = AppearanceScss.Emit(
            a,
            isRoot:       node.ClassName == Document.DesignerDocument.RootClassName,
            isContainer:  false,
            childCount:   0,
            isLabel:      false,
            isCheckbox:   true,
            checkboxSize: p.CheckboxSize );

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
