
using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.UI;
using Grains.RazorDesigner.Contracts;
using Grains.RazorDesigner.Diagnostics;
using Grains.RazorDesigner.Document;
using Grains.RazorDesigner.Projection.Appearance;

namespace Grains.RazorDesigner.Projection;

public static class Applier
{
    private const string LogPrefix = "[Grains.RazorDesigner]";


    public static void ApplyOp( Panel panel, PanelOp op )
    {
        switch ( op )
        {
            case SetClass sc:
                if ( !string.IsNullOrEmpty( sc.Class ) )
                    panel.AddClass( sc.Class );
                break;

            case SetStyle ss:
                panel.Style.Set( ss.Property, ss.Value );
                break;

            case SetAttribute:
                break;

            case SetInnerText st:
                if ( panel is Label lbl )          lbl.Text  = st.Text;
                else if ( panel is Button btn )    btn.Text  = st.Text;
                else if ( panel is IconPanel icon ) icon.Text = st.Text;
                break;

            default:
                throw new System.Diagnostics.UnreachableException(
                    $"Applier.ApplyOp: unhandled PanelOp variant '{op?.GetType().Name}'. " +
                    "Add the arm here AND add the variant to PanelOpExhaustivenessTest's inline " +
                    "array in the same commit — that is the forcing function." );
        }
    }


    public static void ApplyOpToScratch( PanelOp op )
    {
        // Construct a throwaway Panel so the switch can run without a live scene.
        var scratch = new Panel();
        ApplyOp( scratch, op );
    }


    public static Panel BuildPreview(
        IReadOnlyNode node,
        Panel liveParent,
        ProjectionContext ctx,
        Action<IReadOnlyNode> onNodeBuilt )
    {
        if ( liveParent is null || !liveParent.IsValid )
        {
            Log.Warning( $"{LogPrefix} MirrorRecord: liveParent not valid; record {node.ClassName} has no live mirror" );
            return null;
        }

        Panel live;
        switch ( node.Kind )
        {
            case "Label":
                live = liveParent.AddChild<Label>( node.ClassName );
                break;

            case "Button":
                live = liveParent.AddChild<Button>( node.ClassName );
                break;

            case "Image":
                live = liveParent.AddChild<Image>( node.ClassName );
                break;

            case "TextEntry":
                live = liveParent.AddChild<TextEntry>( node.ClassName );
                break;

            case "Checkbox":
                live = liveParent.AddChild<Checkbox>( node.ClassName );
                break;

            case "IconPanel":
                live = liveParent.AddChild<IconPanel>( node.ClassName );
                break;

            case "Panel":
                live = liveParent.AddChild<Panel>( node.ClassName );
                break;

            case "SplitContainer":
                return BuildPreviewSplitContainer( node, liveParent, ctx, onNodeBuilt );

            case "Form":
                live = liveParent.AddChild<Form>( node.ClassName );
                break;

            case "Field":
                live = liveParent.AddChild<Field>( node.ClassName );
                break;

            case "FieldControl":
                live = liveParent.AddChild<FieldControl>( node.ClassName );
                break;

            default:
                if ( !ProjectorRegistry.Has( node.Kind ) )
                    Log.Warning( $"{LogPrefix} BuildPreview: no projector registered for kind '{node.Kind}'; treating as placeholder" );

                var fallback = liveParent.AddChild<Panel>( node.ClassName );
                live = fallback;
                break;
        }

        // --- Step 2: Apply projector ops ---
        if ( ProjectorRegistry.Has( node.Kind ) )
        {
            var result = ProjectorRegistry.For( node.Kind ).Project(
                node, node.Appearance, node.Payload, ctx );
            foreach ( var op in result.PanelOps )
                ApplyOp( live, op );
        }

        // --- Step 3: Typed special-cases (applied after op loop) ---
        ApplyTypedSpecialCases( node, live, ctx );

        if ( PreviewStrategies.For( node.Kind ) == PreviewStrategy.Placeholder )
        {
            var isContainer = false;
            if ( System.Enum.TryParse<ControlType>( node.Kind, out var parsedKind ) )
                isContainer = ContractScanner.Table.Get( parsedKind ).IsContainer;
            if ( !isContainer )
            {
                var inner = live.AddChild<Panel>();
                inner.AddClass( "inner" );
                Log.Info( $"{LogPrefix} MirrorRecord({node.ClassName}) added .inner child for non-container stand-in" );
            }
        }

        // --- Step 5: Recurse non-slot children ---
        foreach ( var child in node.Children )
            BuildPreview( child, live, ctx, onNodeBuilt );

        if ( node is RecordNode rn )
            rn.Backing.LivePanel = live;

        // --- Step 7: Fire chrome-label hook ---
        onNodeBuilt?.Invoke( node );

        // --- Step 8: Tail log (ported verbatim from MirrorRecord) ---
        Log.Info( $"{LogPrefix} MirrorRecord({node.ClassName}) under {liveParent.ElementName ?? "root"} -> live {live.GetType().Name}" );

        return live;
    }

    // SplitContainer case extracted to keep BuildPreview readable.
    private static Panel BuildPreviewSplitContainer(
        IReadOnlyNode node,
        Panel liveParent,
        ProjectionContext ctx,
        Action<IReadOnlyNode> onNodeBuilt )
    {
        var split = liveParent.AddChild<SplitContainer>( node.ClassName );

        // Apply projector ops to the SplitContainer.
        if ( ProjectorRegistry.Has( node.Kind ) )
        {
            var result = ProjectorRegistry.For( node.Kind ).Project(
                node, node.Appearance, node.Payload, ctx );
            foreach ( var op in result.PanelOps )
                ApplyOp( split, op );
        }

        // Write back LivePanel for the SplitContainer record immediately.
        if ( node is RecordNode rnSplit )
            rnSplit.Backing.LivePanel = split;

        foreach ( var slotKv in node.Slots )
        {
            var slotName = slotKv.Key;
            Panel slotPanel = slotName == "right" ? split.Right : split.Left;

            // Find the slot Panel record for its ClassName.
            ControlRecord slotRecord = null;
            if ( node is RecordNode rnForSlot )
            {
                foreach ( var child in rnForSlot.Backing.Children )
                {
                    if ( child.IsSlot && child.SlotName == slotName )
                    {
                        slotRecord = child;
                        break;
                    }
                }
            }

            if ( slotRecord != null && !string.IsNullOrEmpty( slotRecord.ClassName ) )
                slotPanel.AddClass( slotRecord.ClassName );

            // Write LivePanel for the slot record itself.
            if ( slotRecord != null )
                slotRecord.LivePanel = slotPanel;

            Log.Info( $"{LogPrefix} MirrorRecord: bound slot '{(slotRecord?.ClassName ?? slotName)}' (SlotName={slotName}) to engine .{(slotName == "right" ? "Right" : "Left")}" );

            // Recurse grandchildren into the slot panel.
            foreach ( var grandchild in slotKv.Value )
                BuildPreview( grandchild, slotPanel, ctx, onNodeBuilt );

            // Fire chrome-label hook for the slot record.
            if ( slotRecord != null )
                onNodeBuilt?.Invoke( new RecordNode( slotRecord ) );
        }

        // Chrome-label hook for the SplitContainer itself.
        onNodeBuilt?.Invoke( node );

        Log.Info( $"{LogPrefix} MirrorRecord({node.ClassName}) under {liveParent.ElementName ?? "root"} -> live {split.GetType().Name}" );
        return split;
    }

    private static readonly System.Reflection.FieldInfo _panelYogaNodeField =
        typeof( Panel ).GetField( "YogaNode",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic );

    private static void MarkMeasureDirty( Panel panel )
    {
        if ( panel is null || !panel.IsValid || _panelYogaNodeField is null ) return;
        try
        {
            var yoga = _panelYogaNodeField.GetValue( panel );
            yoga?.GetType()
                 .GetMethod( "MarkDirty", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic )
                 ?.Invoke( yoga, null );
        }
        catch ( System.Exception e )
        {
            Log.Warning( e, $"{LogPrefix} MarkMeasureDirty failed: {e.Message}" );
        }
    }

    // Typed special-cases applied after the op loop (per-kind property sets that ops don't cover).
    private static void ApplyTypedSpecialCases( IReadOnlyNode node, Panel live, ProjectionContext ctx )
    {
        switch ( node.Kind )
        {
            case "Checkbox":
                // LabelText is set directly — projector doesn't emit SetInnerText for Checkbox.
                if ( live is Checkbox check )
                    check.LabelText = node.Payload.Content;
                break;

            case "TextEntry":
                if ( live is TextEntry entry )
                    entry.Placeholder = node.Payload.Placeholder;
                break;

            case "Image":
                // Texture loading only in preview mode.
                if ( ctx.ForPreview && live is Image img )
                {
                    var source = node.Payload.Source ?? "";
                    if ( !string.IsNullOrEmpty( source ) )
                    {
                        var tex = ImageTextureLoader.Load( source );
                        if ( tex != null )
                        {
                            img.Texture = tex;
                            img.MarkRenderDirty();
                            MarkMeasureDirty( img );
                        }
                        else
                        {
                            img.AddClass( "preview-image-empty" );
                        }
                        Log.Info( $"{LogPrefix} ReapplyLiveContent.Image '{node.ClassName}' -> Source=\"{source}\" tex={(img.Texture is null ? "null" : "ok")}" );
                    }
                }
                break;
        }
    }


    public static void ReapplyContent( IReadOnlyNode node, Panel existing, ProjectionContext ctx )
    {
        if ( existing is null || !existing.IsValid ) return;

        switch ( node.Kind )
        {
            case "Label":
                if ( existing is Label lbl ) lbl.Text = node.Payload.Content;
                break;

            case "Button":
                if ( existing is Button btn ) btn.Text = node.Payload.Content;
                break;

            case "IconPanel":
                if ( existing is IconPanel icon ) icon.Text = node.Payload.IconName;
                break;

            case "Checkbox":
                if ( existing is Checkbox check ) check.LabelText = node.Payload.Content;
                break;

            case "TextEntry":
                if ( existing is TextEntry entry ) entry.Placeholder = node.Payload.Placeholder;
                break;

            case "Image":
                if ( existing is Image img )
                {
                    var source = node.Payload.Source ?? "";
                    var tex = ImageTextureLoader.Load( source );
                    img.SetClass( "preview-image-empty", tex is null );
                    img.Texture = tex;
                    img.MarkRenderDirty();
                    MarkMeasureDirty( img );
                    Log.Info( $"{LogPrefix} ReapplyLiveContent.Image '{node.ClassName}' -> Source=\"{source}\" tex={(tex is null ? "null" : "ok")}" );
                }
                break;

        }
    }


    public static (string razor, string scss) BuildSave(
        IReadOnlyNode root,
        string wrapperClassName,
        ProjectionContext ctx )
    {
        var razorSb = new StringBuilder();
        var scssSb  = new StringBuilder();

        var probeSw = PerfProbes.Enabled ? System.Diagnostics.Stopwatch.StartNew() : null;

        foreach ( var child in root.Children )
            EmitRazorNode( razorSb, child, "    ", ctx );

        var probeRazorMs = probeSw?.Elapsed.TotalMilliseconds ?? 0.0;
        probeSw?.Restart();

        // --- SCSS walk ---
        bool saveMode = !string.IsNullOrEmpty( wrapperClassName );
        if ( saveMode )
        {
            var rootScss = GetScssLines( root, ctx );
            var inner = "    ";
            if ( rootScss.Count > 0 )
                EmitScssLines( scssSb, rootScss, inner );
            foreach ( var child in root.Children )
                EmitRuleTree( scssSb, child, inner, ctx );
        }
        else
        {
            EmitRuleTree( scssSb, root, "", ctx );
        }

        if ( probeSw != null )
        {
            var probeScssMs = probeSw.Elapsed.TotalMilliseconds;
            var mode = saveMode ? "save" : "preview";
            Log.Info( $"{LogPrefix} Applier.BuildSave[{mode}]: razor={probeRazorMs:F2}ms scss={probeScssMs:F2}ms total={(probeRazorMs + probeScssMs):F2}ms" );
        }

        return (razorSb.ToString(), scssSb.ToString());
    }


    private static void EmitRazorNode( StringBuilder sb, IReadOnlyNode node, string indent, ProjectionContext ctx )
    {
        string tag;
        if ( System.Enum.TryParse<ControlType>( node.Kind, out var ct ) )
            tag = ContractScanner.Table.Get( ct ).LibraryTag;
        else
            tag = node.Kind.ToLowerInvariant();

        EmitRazorNodeCore( sb, node, tag, indent, "", ctx );
    }

    private static void EmitRazorNodeCore(
        StringBuilder sb,
        IReadOnlyNode node,
        string tag,
        string indent,
        string slotAttr,
        ProjectionContext ctx )
    {
        // Build class attribute first, then RazorAttributes (node-id etc.) space-joined.
        var sb2 = new StringBuilder();
        sb2.Append( $"class=\"{Escape.Html( node.ClassName )}\"" );

        ProjectionResult result = ProjectionResult.Empty;
        if ( ProjectorRegistry.Has( node.Kind ) )
        {
            result = ProjectorRegistry.For( node.Kind ).Project(
                node, node.Appearance, node.Payload, ctx );
            foreach ( var attr in result.RazorAttributes )
            {
                sb2.Append( ' ' );
                sb2.Append( attr );
            }
        }

        var attrStr = sb2.ToString();

        if ( node.Kind == "SplitContainer" )
        {
            // Collect slot records.
            var slotRecords = new List<ControlRecord>();
            if ( node is RecordNode rnSc )
            {
                foreach ( var child in rnSc.Backing.Children )
                {
                    if ( child.IsSlot )
                        slotRecords.Add( child );
                }
            }

            if ( slotRecords.Count == 0 )
            {
                sb.AppendLine( $"{indent}<{tag} {attrStr}{slotAttr} />" );
                return;
            }

            sb.AppendLine( $"{indent}<{tag} {attrStr}{slotAttr}>" );
            foreach ( var slotRec in slotRecords )
            {
                // Slot record is a Panel with IsSlot=true; emit as <panel class="..." slot="...">
                var slotNode = new RecordNode( slotRec );
                var slotTagStr = ContractScanner.Table.Get( ControlType.Panel ).LibraryTag;
                var slotAttrStr = !string.IsNullOrEmpty( slotRec.SlotName )
                    ? $" slot=\"{Escape.Html( slotRec.SlotName )}\""
                    : "";
                EmitRazorNodeCore( sb, slotNode, slotTagStr, indent + "    ", slotAttrStr, ctx );
            }
            sb.AppendLine( $"{indent}</{tag}>" );
            return;
        }

        // Determine isContainer.
        bool isContainer = false;
        if ( System.Enum.TryParse<ControlType>( node.Kind, out var ctKind ) )
            isContainer = ContractScanner.Table.Get( ctKind ).IsContainer;

        if ( isContainer )
        {
            if ( node.Children.Count == 0 )
            {
                // Self-close empty containers (matches hand-authored razor convention).
                sb.AppendLine( $"{indent}<{tag} {attrStr}{slotAttr} />" );
                return;
            }
            sb.AppendLine( $"{indent}<{tag} {attrStr}{slotAttr}>" );
            foreach ( var child in node.Children )
                EmitRazorNode( sb, child, indent + "    ", ctx );
            sb.AppendLine( $"{indent}</{tag}>" );
            return;
        }

        // Leaf: inner text or self-close.
        var innerText = result.RazorInnerText;
        if ( innerText != null )
            sb.AppendLine( $"{indent}<{tag} {attrStr}{slotAttr}>{innerText}</{tag}>" );
        else
            sb.AppendLine( $"{indent}<{tag} {attrStr}{slotAttr} />" );
    }


    // Port of EmitRuleTree. Handles the .root flat-emit special case and nested children.
    private static void EmitRuleTree( StringBuilder sb, IReadOnlyNode node, string indent, ProjectionContext ctx )
    {
        var inner = indent + "    ";
        var scssLines = GetScssLines( node, ctx );

        // .root is compound with the wrapper class, not a descendant — emit flat.
        bool isRoot = node.ClassName == DesignerDocument.RootClassName;

        if ( isRoot )
        {
            if ( scssLines.Count > 0 )
            {
                sb.AppendLine( $"{indent}.{node.ClassName} {{" );
                EmitScssLines( sb, scssLines, inner );
                sb.AppendLine( $"{indent}}}" );
            }

            // Recurse children of root (they're peers at the same indent level).
            bool isContainer = false;
            if ( System.Enum.TryParse<ControlType>( node.Kind, out var ctForRoot ) )
                isContainer = ContractScanner.Table.Get( ctForRoot ).IsContainer;
            if ( isContainer )
            {
                foreach ( var child in GetAllChildrenForScss( node ) )
                    EmitRuleTree( sb, child, indent, ctx );
            }
            return;
        }

        bool nodeIsContainer = false;
        if ( System.Enum.TryParse<ControlType>( node.Kind, out var ctNonRoot ) )
            nodeIsContainer = ContractScanner.Table.Get( ctNonRoot ).IsContainer;
        var childrenForScss = nodeIsContainer ? GetAllChildrenForScss( node ) : new List<IReadOnlyNode>();
        bool hasChildren = childrenForScss.Count > 0;
        if ( scssLines.Count == 0 && !hasChildren ) return;

        sb.AppendLine( $"{indent}.{node.ClassName} {{" );
        if ( scssLines.Count > 0 )
            EmitScssLines( sb, scssLines, inner );
        if ( hasChildren )
        {
            foreach ( var child in childrenForScss )
                EmitRuleTree( sb, child, inner, ctx );
        }
        sb.AppendLine( $"{indent}}}" );
    }

    public static List<string> GetNodeScssLines( IReadOnlyNode node, ProjectionContext ctx )
	{
		if ( !ProjectorRegistry.Has( node.Kind ) )
			return new List<string>();
		var result = ProjectorRegistry.For( node.Kind ).Project(
			node, node.Appearance, node.Payload, ctx );
		var lines = new List<string>( result.ScssLines.Count );
		foreach ( var l in result.ScssLines )
			lines.Add( l );

		if ( node is RecordNode rn && rn.Backing.CustomStyles != null )
		{
			foreach ( var kvp in rn.Backing.CustomStyles )
			{
				if ( !string.IsNullOrWhiteSpace( kvp.Key ) && !string.IsNullOrWhiteSpace( kvp.Value ) )
				{
					lines.Add( $"{kvp.Key.Trim()}: {kvp.Value.Trim()};" );
				}
			}
		}

		if ( node.StateRules.Count == 0 )
			return lines;

		// Derive per-kind flags (same logic the projectors use for the base call).
		bool isRoot = node.ClassName == DesignerDocument.RootClassName;
		bool isContainer = false;
		bool isLabel     = node.Kind == "Label";
		bool isCheckbox  = node.Kind == "Checkbox";
		int  childCount  = node.Children.Count;
		if ( System.Enum.TryParse<ControlType>( node.Kind, out var ct ) )
			isContainer = ContractScanner.Table.Get( ct ).IsContainer;

		// Sort state rules in canonical order.
		var sorted = new List<IReadOnlyStateRule>( node.StateRules );
		sorted.Sort( IReadOnlyStateRule.CompareCanonical );

		foreach ( var sr in sorted )
		{
			var deltaLines = AppearanceScss.Emit(
				sr.Delta,
				isRoot:       isRoot,
				isContainer:  isContainer,
				childCount:   childCount,
				isLabel:      isLabel,
				isCheckbox:   isCheckbox,
				checkboxSize: default,    // Length.Auto — state Delta is Appearance only; Checkbox box size
				baseForDiff:  node.Appearance ); // Only emit lines where delta differs from base

			if ( deltaLines.Count == 0 ) continue;

			var sel = ScssEnums.PseudoSelector( sr.State, sr.NthChildMode, sr.NthChildArg );
			lines.Add( $"&:{sel} {{" );
			foreach ( var dl in deltaLines )
				lines.Add( $"    {dl}" );
			lines.Add( "}" );
		}

		return lines;
	}

    // Private wrapper used by EmitRuleTree.
    private static List<string> GetScssLines( IReadOnlyNode node, ProjectionContext ctx ) =>
        GetNodeScssLines( node, ctx );

    private static void EmitScssLines( StringBuilder sb, IReadOnlyList<string> lines, string innerIndent )
    {
        foreach ( var line in lines )
            sb.AppendLine( $"{innerIndent}{line}" );
    }

    private static List<IReadOnlyNode> GetAllChildrenForScss( IReadOnlyNode node )
    {
        var result = new List<IReadOnlyNode>();

        if ( node.Kind == "SplitContainer" && node is RecordNode rnSc )
        {
            // Slot Panel records own the user-facing CSS (e.g. .left1 { padding: ... }).
            foreach ( var child in rnSc.Backing.Children )
            {
                if ( child.IsSlot )
                    result.Add( new RecordNode( child ) );
            }
        }
        else
        {
            foreach ( var child in node.Children )
                result.Add( child );
        }

        return result;
    }
}
