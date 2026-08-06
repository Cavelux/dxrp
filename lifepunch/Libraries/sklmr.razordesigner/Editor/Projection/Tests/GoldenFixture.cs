using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grains.RazorDesigner.Document;
using Sandbox; // Color (engine-global; matches IReadOnlyNode.cs)

namespace Grains.RazorDesigner.Projection.Tests;


public sealed class GoldenFixture
{
    [JsonPropertyName( "goldenFormatVersion" )]
    public int GoldenFormatVersion { get; set; }

    [JsonPropertyName( "kind" )]
    public string Kind { get; set; } = "";

    [JsonPropertyName( "node" )]
    public NodeDto Node { get; set; } = new();

    [JsonPropertyName( "context" )]
    public ContextDto Context { get; set; } = new();

    [JsonPropertyName( "expected" )]
    public ExpectedDto Expected { get; set; }   // null means "not yet authored"
}

public sealed class ContextDto
{
    [JsonPropertyName( "forPreview" )]
    public bool ForPreview { get; set; } = false;
}

public sealed class NodeDto
{
    [JsonPropertyName( "id" )]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName( "kind" )]
    public string Kind { get; set; } = "";

    [JsonPropertyName( "className" )]
    public string ClassName { get; set; } = "";

    [JsonPropertyName( "appearance" )]
    public Dictionary<string, JsonElement> Appearance { get; set; } = new();

    // Flat map: payload field-name -> json value.
    [JsonPropertyName( "payload" )]
    public Dictionary<string, JsonElement> Payload { get; set; } = new();

    [JsonPropertyName( "children" )]
    public List<NodeDto> Children { get; set; } = new();

    [JsonPropertyName( "slots" )]
    public Dictionary<string, List<NodeDto>> Slots { get; set; } = new();

    [JsonPropertyName( "states" )]
    public List<StateDto> States { get; set; }
}

public sealed class StateDto
{
    [JsonPropertyName( "state" )]
    public string State { get; set; } = "hover";

    [JsonPropertyName( "nthChildMode" )]
    public string NthChildMode { get; set; }   // null → default (Literal)

    [JsonPropertyName( "nthChildArg" )]
    public int NthChildArg { get; set; } = 1;

    [JsonPropertyName( "delta" )]
    public Dictionary<string, JsonElement> Delta { get; set; } = new();
}

public sealed class ExpectedDto
{
    [JsonPropertyName( "panelOps" )]
    public List<OpDto> PanelOps { get; set; } = new();

    [JsonPropertyName( "scssLines" )]
    public List<string> ScssLines { get; set; } = new();

    [JsonPropertyName( "razorAttributes" )]
    public List<string> RazorAttributes { get; set; } = new();

    [JsonPropertyName( "razorInnerText" )]
    public string RazorInnerText { get; set; }
}

// OpDto — union over the four PanelOp variants via a "$type" discriminant.
public sealed class OpDto
{
    [JsonPropertyName( "$type" )]
    public string Type { get; set; } = "";

    // SetClass
    [JsonPropertyName( "class" )]
    public string Class { get; set; }

    // SetStyle
    [JsonPropertyName( "property" )]
    public string Property { get; set; }

    [JsonPropertyName( "value" )]
    public string Value { get; set; }

    // SetAttribute
    [JsonPropertyName( "name" )]
    public string Name { get; set; }

    // SetAttribute shares Value; SetInnerText uses Text
    [JsonPropertyName( "text" )]
    public string Text { get; set; }
}

public static class GoldenJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter( JsonNamingPolicy.CamelCase ) },
    };
}

public static class OpMapping
{
    public static OpDto FromOp( PanelOp op )
    {
        switch ( op )
        {
            case SetClass c:
                return new OpDto { Type = "SetClass", Class = c.Class };
            case SetStyle s:
                return new OpDto { Type = "SetStyle", Property = s.Property, Value = s.Value };
            case SetAttribute a:
                return new OpDto { Type = "SetAttribute", Name = a.Name, Value = a.Value };
            case SetInnerText t:
                return new OpDto { Type = "SetInnerText", Text = t.Text };
            default:
                throw new InvalidOperationException( $"OpMapping.FromOp: unknown PanelOp variant '{op?.GetType().Name}'" );
        }
    }

    public static PanelOp ToOp( OpDto dto )
    {
        switch ( dto.Type )
        {
            case "SetClass":     return new SetClass( dto.Class ?? "" );
            case "SetStyle":     return new SetStyle( dto.Property ?? "", dto.Value ?? "" );
            case "SetAttribute": return new SetAttribute( dto.Name ?? "", dto.Value ?? "" );
            case "SetInnerText": return new SetInnerText( dto.Text ?? "" );
            default:
                throw new InvalidOperationException( $"OpMapping.ToOp: unknown $type '{dto.Type}'" );
        }
    }
}


public sealed class FixtureNode : IReadOnlyNode
{
    private readonly NodeDto _dto;
    private Guid _id;
    private FixtureAppearance _appearance;
    private FixturePayload _payload;
    private List<IReadOnlyNode> _children;
    private Dictionary<string, IReadOnlyList<IReadOnlyNode>> _slots;

    public FixtureNode( NodeDto dto )
    {
        _dto = dto ?? throw new ArgumentNullException( nameof( dto ) );
        _id = Guid.TryParse( dto.Id, out var g ) ? g : Guid.NewGuid();
    }

    public Guid Id => _id;
    public string Kind => _dto.Kind;
    public string ClassName => _dto.ClassName;

    public IAppearance Appearance =>
        _appearance ??= new FixtureAppearance( _dto.Appearance );

    public IPayload Payload =>
        _payload ??= new FixturePayload( _dto.Payload );

    public IReadOnlyList<IReadOnlyNode> Children
    {
        get
        {
            if ( _children != null ) return _children;
            _children = new List<IReadOnlyNode>();
            foreach ( var child in _dto.Children )
                _children.Add( new FixtureNode( child ) );
            return _children;
        }
    }

    public IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyNode>> Slots
    {
        get
        {
            if ( _slots != null ) return _slots;
            _slots = new Dictionary<string, IReadOnlyList<IReadOnlyNode>>();
            foreach ( var kv in _dto.Slots )
            {
                var list = new List<IReadOnlyNode>();
                foreach ( var child in kv.Value )
                    list.Add( new FixtureNode( child ) );
                _slots[kv.Key] = list;
            }
            return _slots;
        }
    }

    // Lazily built list of state rules from NodeDto.States (grd-74lj Task 7a).
    private List<IReadOnlyStateRule> _stateRules;

    public IReadOnlyList<IReadOnlyStateRule> StateRules
    {
        get
        {
            if ( _stateRules != null ) return _stateRules;
            _stateRules = new List<IReadOnlyStateRule>();
            if ( _dto.States != null )
            {
                foreach ( var s in _dto.States )
                    _stateRules.Add( new FixtureStateRule( s ) );
            }
            return _stateRules;
        }
    }

    private sealed class FixtureStateRule : IReadOnlyStateRule
    {
        private readonly Document.PseudoKind _state;
        private readonly Document.NthChildMode _nthChildMode;
        private readonly int _nthChildArg;
        private readonly IAppearance _delta;

        public FixtureStateRule( StateDto dto )
        {
            if ( !System.Enum.TryParse<Document.PseudoKind>( dto.State, ignoreCase: true, out _state ) )
                throw new System.ArgumentException( $"Golden state \"{dto.State}\" is not a valid PseudoKind." );

            // Parse NthChildMode (optional — null means Literal default).
            if ( dto.NthChildMode != null &&
                 System.Enum.TryParse<Document.NthChildMode>( dto.NthChildMode, ignoreCase: true, out var nm ) )
                _nthChildMode = nm;
            else
                _nthChildMode = Document.NthChildMode.Literal;

            _nthChildArg = dto.NthChildArg >= 1 ? dto.NthChildArg : 1;
            _delta = new FixtureAppearance( dto.Delta ?? new Dictionary<string, JsonElement>() );
        }

        public Document.PseudoKind State => _state;
        public Document.NthChildMode NthChildMode => _nthChildMode;
        public int NthChildArg => _nthChildArg;
        public IAppearance Delta => _delta;
    }
}

public sealed class FixtureAppearance : IAppearance
{
    private readonly Dictionary<string, JsonElement> _m;

    public FixtureAppearance( Dictionary<string, JsonElement> map )
    {
        _m = map ?? new Dictionary<string, JsonElement>();
    }

    // Helpers
    private bool Bool( string key, bool def = false ) =>
        _m.TryGetValue( key, out var el ) ? el.GetBoolean() : def;

    private int Int( string key, int def = 0 ) =>
        _m.TryGetValue( key, out var el ) ? el.GetInt32() : def;

    private float Float( string key, float def = 0f ) =>
        _m.TryGetValue( key, out var el ) ? el.GetSingle() : def;

    private string Str( string key, string def = "" ) =>
        _m.TryGetValue( key, out var el ) ? el.GetString() ?? def : def;

    private Length Len( string key, Length def = default )
    {
        if ( !_m.TryGetValue( key, out var el ) ) return def;
        // Stored as "100px", "50%", "auto", etc.
        if ( el.ValueKind == JsonValueKind.String )
            return Length.TryParse( el.GetString(), out var v ) ? v : def;
        // Or stored as number (bare px)
        if ( el.ValueKind == JsonValueKind.Number )
            return Length.Px( el.GetSingle() );
        return def;
    }

    private Color Col( string key, Color def = default )
    {
        if ( !_m.TryGetValue( key, out var el ) ) return def;
        if ( el.ValueKind == JsonValueKind.String )
        {
            // Accept "#RRGGBB" or "#RRGGBBAA". Color.Parse returns Color? (matches PaletteTemplateSerializer pattern).
            var s = el.GetString() ?? "";
            var parsed = Color.Parse( s );
            if ( parsed.HasValue ) return parsed.Value;
        }
        return def;
    }

    private Edges EdgesVal( string key )
    {
        if ( !_m.TryGetValue( key, out var el ) ) return Edges.Zero;
        if ( el.ValueKind == JsonValueKind.String )
        {
            // Delegate to Edges.TryParse which handles 1/2/4-value CSS shorthand forms.
            return Edges.TryParse( el.GetString() ?? "", out var parsed ) ? parsed : Edges.Zero;
        }
        if ( el.ValueKind == JsonValueKind.Object )
        {
            // { top, right, bottom, left }
            var top    = el.TryGetProperty( "top",    out var t ) && Length.TryParse( t.GetString(), out var tv ) ? tv : Length.Auto;
            var right  = el.TryGetProperty( "right",  out var r ) && Length.TryParse( r.GetString(), out var rv ) ? rv : Length.Auto;
            var bottom = el.TryGetProperty( "bottom", out var b ) && Length.TryParse( b.GetString(), out var bv ) ? bv : Length.Auto;
            var left   = el.TryGetProperty( "left",   out var l ) && Length.TryParse( l.GetString(), out var lv ) ? lv : Length.Auto;
            return new Edges( top, right, bottom, left );
        }
        return Edges.Zero;
    }

    private T EnumVal<T>( string key, T def ) where T : struct, System.Enum
    {
        if ( !_m.TryGetValue( key, out var el ) ) return def;
        if ( el.ValueKind == JsonValueKind.String )
        {
            if ( System.Enum.TryParse<T>( el.GetString(), out var v ) ) return v;
        }
        return def;
    }

    // --- IAppearance implementation (defaults mirror ControlRecord field declarations) ---

    public Length Width  => Len( "width",  Length.Auto );
    public Length Height => Len( "height", Length.Auto );

    public FlexDirection  Direction => EnumVal<FlexDirection>( "direction", FlexDirection.Row );
    public JustifyContent Justify   => EnumVal<JustifyContent>( "justify",  JustifyContent.Start );
    public AlignItems     Align     => EnumVal<AlignItems>( "align",       AlignItems.Stretch );
    public float          Gap       => Float( "gap",   8f );
    public Edges          Padding   => EdgesVal( "padding" );
    public FlexWrap       Wrap      => EnumVal<FlexWrap>( "wrap",          FlexWrap.NoWrap );

    public PositionKind   Position  => EnumVal<PositionKind>( "position", PositionKind.Relative );
    public Length         Top       => Len( "top",    Length.Auto );
    public Length         Left      => Len( "left",   Length.Auto );
    public Length         Right     => Len( "right",  Length.Auto );
    public Length         Bottom    => Len( "bottom", Length.Auto );

    public float         FlexGrow   => Float( "flexGrow",   0f );
    public float         FlexShrink => Float( "flexShrink", 1f );
    public Length        FlexBasis  => Len( "flexBasis", Length.Auto );
    public AlignSelfKind AlignSelf  => EnumVal<AlignSelfKind>( "alignSelf", AlignSelfKind.Auto );

    public bool           OverrideTypography => Bool( "overrideTypography", false );
    public string         FontFamily         => Str( "fontFamily", "" );
    public Length         FontSize           => Len( "fontSize", Length.Px( 14 ) );
    public int            FontWeight         => Int( "fontWeight", 400 );
    public Color          Color              => Col( "color", Color.White );
    public TextAlignment  TextAlign          => EnumVal<TextAlignment>( "textAlign", TextAlignment.Left );
    public bool              FontStyleItalic => Bool( "fontStyleItalic", false );
    public TextTransformKind TextTransform   => EnumVal<TextTransformKind>( "textTransform", TextTransformKind.None );
    public Length            LetterSpacing   => Len( "letterSpacing", Length.Auto );
    public Length            LineHeight      => Len( "lineHeight",    Length.Auto );

    public bool           OverrideBackground => Bool( "overrideBackground", false );
    public Color          BackgroundColor    => Col( "backgroundColor", Color.White );
    public string         BackgroundImage    => Str( "backgroundImage", "" );
    public string         BackgroundSize     => Str( "backgroundSize", "" );
    public string         BackgroundPosition => Str( "backgroundPosition", "" );
    public string         BackgroundRepeat   => Str( "backgroundRepeat", "" );

    public bool           OverrideBorder  => Bool( "overrideBorder", false );
    public Length         BorderRadius    => Len( "borderRadius", Length.Px( 0 ) );
    public Color          BorderColor     => Col( "borderColor", Color.Transparent );
    public Length         BorderWidth     => Len( "borderWidth", Length.Px( 0 ) );

    public bool           OverrideEffects  => Bool( "overrideEffects", false );
    public Length         BoxShadowX       => Len( "boxShadowX", Length.Px( 0 ) );
    public Length         BoxShadowY       => Len( "boxShadowY", Length.Px( 2 ) );
    public Length         BoxShadowBlur    => Len( "boxShadowBlur", Length.Px( 4 ) );
    public Color          BoxShadowColor   => Col( "boxShadowColor", Color.Black );
    public bool           BoxShadowInset   => Bool( "boxShadowInset", false );
    public float          Opacity          => Float( "opacity", 1f );

    public bool           OverrideConstraints => Bool( "overrideConstraints", false );
    public Edges          Margin           => EdgesVal( "margin" );
    public Length         MinWidth         => Len( "minWidth",  Length.Auto );
    public Length         MaxWidth         => Len( "maxWidth",  Length.Auto );
    public Length         MinHeight        => Len( "minHeight", Length.Auto );
    public Length         MaxHeight        => Len( "maxHeight", Length.Auto );

    public bool           OverrideInteraction => Bool( "overrideInteraction", false );
    public CursorKind     Cursor              => EnumVal<CursorKind>( "cursor",   CursorKind.Auto );
    public OverflowKind   Overflow            => EnumVal<OverflowKind>( "overflow", OverflowKind.Visible );
    public int            ZIndex              => Int( "zIndex", 0 );
    public bool           PointerEvents       => Bool( "pointerEvents", true );
}

public sealed class FixturePayload : IPayload
{
    private readonly Dictionary<string, JsonElement> _m;

    public FixturePayload( Dictionary<string, JsonElement> map )
    {
        _m = map ?? new Dictionary<string, JsonElement>();
    }

    private string Str( string key, string def = "" ) =>
        _m.TryGetValue( key, out var el ) ? el.GetString() ?? def : def;

    private Length Len( string key, Length def = default )
    {
        if ( !_m.TryGetValue( key, out var el ) ) return def;
        if ( el.ValueKind == JsonValueKind.String )
            return Length.TryParse( el.GetString(), out var v ) ? v : def;
        if ( el.ValueKind == JsonValueKind.Number )
            return Length.Px( el.GetSingle() );
        return def;
    }

    public string Content      => Str( "content",      "" );
    public string Placeholder  => Str( "placeholder",  "" );
    public string Source       => Str( "source",       "" );
    public string IconName     => Str( "iconName",     "" );
    public Length CheckboxSize => Len( "checkboxSize", Length.Px( 16 ) );
}
