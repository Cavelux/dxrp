using System.Text;
using Grains.RazorDesigner.Document;
using Grains.RazorDesigner.Projection;

namespace Grains.RazorDesigner.Serialization;

public static class DocumentSerializer
{
    private const int SchemaVersion = 4;

    // Sandbox.UI outline shorthand only accepts 'solid'.
    private const string SelectionRule =
        ".selected { outline: 2px solid #3FA9F5; }";

    private const string PreviewMarkerRules =
        ".preview-image-empty { " +
            "background-image: linear-gradient(to bottom right, rgba(120, 120, 160, 0.5), rgba(60, 60, 90, 0.5)); " +
            "border: 1px solid rgba(180, 180, 200, 0.5); " +
            "min-width: 32px; " +
            "min-height: 32px; " +
        "}\n" +
        ".preview-panel { position: relative; }\n" +
        ".preview-chrome-label { " +
            "position: absolute; " +
            "top: 0; left: 0; right: 0; bottom: 0; " +
            "justify-content: center; " +
            "align-items: center; " +
            "color: rgba(255, 255, 255, 0.4); " +
            "font-size: 20px; " +
            "text-align: center; " +
        "}\n" +
        ".chrome-hidden .preview-chrome-label { display: none; }\n" +
        ".chrome-hidden .preview-image-empty { " +
            "background-image: none; " +
            "border: none; " +
            "min-width: 0; " +
            "min-height: 0; " +
        "}\n" +
        ".chrome-hidden .preview-buttongroup { " +
            "background-color: transparent; " +
            "border: none; " +
            "min-height: 0; " +
        "}\n" +
        ".chrome-hidden .preview-dropdown { " +
            "background-image: none; " +
            "background-color: transparent; " +
            "border: none; " +
            "min-height: 0; " +
            "box-shadow: none; " +
        "}\n" +
        ".chrome-hidden .preview-dropdown > .inner { " +
            "background-color: transparent; " +
            "border-left: none; " +
        "}\n" +
        ".chrome-hidden .selected { outline: 0px solid transparent; }";

    public static string GenerateRazorMarkup( DesignerDocument doc, string irHash = null )
    {
        var sb = new StringBuilder();
        sb.AppendLine( $"@* Grains.RazorDesigner schema={SchemaVersion} *@" );
        if ( irHash is not null )
            sb.AppendLine( $"@* generated-from-ir-hash: {irHash} *@" );
        sb.AppendLine( "@* https://sbox.game/xaz/razordesigner *@" );
        sb.AppendLine( "@using Sandbox;" );
        sb.AppendLine( "@using Sandbox.UI;" );
        sb.AppendLine( "@inherits PanelComponent" );
        sb.AppendLine( "<root class=\"root\">" );
        var ctx = new ProjectionContext( PreviewTheme.Default, ForPreview: false );
        var (razorChildren, _) = Applier.BuildSave( new RecordNode( doc.RootRecord ), "", ctx );
        sb.Append( razorChildren );
        sb.AppendLine( "</root>" );
        return sb.ToString();
    }

    public static string GeneratePreviewStylesheet( DesignerDocument doc, PreviewTheme theme )
    {
        var sb = new StringBuilder();
        sb.AppendLine( SelectionRule );
        sb.AppendLine( ( theme ?? PreviewTheme.Default ).Css );
        sb.AppendLine( PreviewMarkerRules );
        var ctx = new ProjectionContext( theme ?? PreviewTheme.Default, ForPreview: true );
        var (_, scss) = Applier.BuildSave( new RecordNode( doc.RootRecord ), "", ctx );
        sb.Append( scss );
        return sb.ToString();
    }

    public static string GenerateCSharp(
        DesignerDocument doc,
        string classNameFallback,
        string namespaceFallback )
    {
        var view = new Projection.CSharp.WiringEnvelopeView(
            doc.Wiring ?? Wiring.WiringEnvelope.Empty,
            namespaceFallback,
            classNameFallback );

        var result = Projection.CSharp.CSharpProjector.Project(
            view, documentHasAnyBindings: doc.HasAnyBindings() );

        return result.Source; // null = elision; caller skips the write
    }

    public static string GenerateSavedScss( DesignerDocument doc, string className, PreviewTheme theme )
    {
        var effectiveTheme = theme ?? PreviewTheme.Default;
        var sb = new StringBuilder();
        sb.AppendLine( $"// designed against theme: {effectiveTheme.Name}" );
        sb.AppendLine( $"{className} {{" );
        var ctx = new ProjectionContext( effectiveTheme, ForPreview: false );
        var (_, scss) = Applier.BuildSave( new RecordNode( doc.RootRecord ), className, ctx );
        sb.Append( scss );
        sb.AppendLine( "}" );
        return sb.ToString();
    }
}
