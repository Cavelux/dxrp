using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.Rendering;
using Sandbox.UI;

namespace Goo;

/// <summary>
/// A custom shader applied to a Blob. Point it at a compiled .shader; it parses the shader
/// source's Attribute() declarations to validate uniform names and to reset uniforms other
/// panels have set (panels share one CommandList attribute namespace), and pushes the uniform
/// bag every frame. Set uniforms with the collection initializer (<c>["Name"] = value</c>);
/// a value may be a literal or a per-frame Func. Subclass and override <see cref="Apply"/>
/// only for bespoke per-frame CPU logic.
/// </summary>
public record ShaderEffect
{
    static readonly Dictionary<object, Material> _materialCache = new();
    static readonly Dictionary<object, SchemaCacheEntry> _schemaCache = new();

    sealed class SchemaCacheEntry
    {
        public ShaderSchemaInfo? Info;
        public int SourceHash;
        public RealTimeSince SinceCheck;
    }

    // Every uniform name any ShaderEffect has pushed this session, with the last value pushed
    // (its runtime type drives the reset conversion). Panels share one CommandList attribute
    // namespace, so a uniform set by one panel persists into every later panel's draw; an
    // effect that does not set a seen uniform must reset it to its shader's declared default
    // or it inherits the other panel's value (view-5u5m). Touched only from Apply (render thread).
    static readonly Dictionary<string, object> _seenUniforms = new();

    internal sealed class ShaderSchemaInfo
    {
        public required HashSet<string> Names;                       // declared attribute names
        public required Dictionary<string, (Vector4 Floats, Vector4 Ints)> Defaults;
    }

    readonly string? _path;
    readonly Shader? _shader;
    readonly GrabMode _grab;
    readonly Dictionary<string, UniformValue> _bag = new();
    bool _validated;

    /// <summary>For subclasses that supply their own <see cref="Material"/> and <see cref="Apply"/>.</summary>
    protected ShaderEffect() { }

    /// <summary>Apply the shader at <paramref name="shaderPath"/> (e.g. "shaders/ui_dither.shader").</summary>
    public ShaderEffect( string shaderPath, GrabMode grab = GrabMode.None )
    {
        _path = shaderPath;
        _grab = grab;
    }

    /// <summary>Apply a shader resource (drag-droppable in the inspector).</summary>
    public ShaderEffect( Shader shader, GrabMode grab = GrabMode.None )
    {
        _shader = shader;
        _grab = grab;
    }

    /// <summary>The shader asset path, or null when constructed from a <see cref="T:Sandbox.Shader"/> resource.</summary>
    public string? ShaderPath => _path;

    /// <summary>How this effect grabs the framebuffer behind its panel.</summary>
    public GrabMode Grab => _grab;

    /// <summary>Get or set a uniform by its shader attribute name.</summary>
    public UniformValue this[string name]
    {
        get => _bag[name];
        set => _bag[name] = value;
    }

    object CacheKey => (object?)_path ?? _shader!;

    /// <summary>The material this effect draws with, cached so it is excluded from record equality.</summary>
    public virtual Material Material
    {
        get
        {
            var key = CacheKey;
            if ( !_materialCache.TryGetValue( key, out var mat ) )
            {
                mat = _path is not null ? Material.FromShader( _path ) : Material.FromShader( _shader! );
                _materialCache[key] = mat;
            }
            return mat;
        }
    }

    /// <summary>Create the Material now, on the calling thread. Call on the main thread; Draw runs on the render thread and must only read the cache.</summary>
    public void Warm() => _ = Material;

    /// <summary>Set this effect's shader attributes for the frame, then grab the framebuffer per <see cref="Grab"/>.</summary>
    protected internal virtual void Apply( CommandList cl, Rect rect )
    {
        cl.Attributes.Set( "BoxSize", new Vector2( rect.Width, rect.Height ) );

        // Subclass escape hatch: a derived effect calling base.Apply gets only BoxSize.
        if ( _path is null && _shader is null ) return;

        Validate();

        // Reset seen-but-unset uniforms to this shader's declared defaults so another panel's
        // attribute writes do not bleed into this draw (view-5u5m).
        var schema = SchemaFor( CacheKey );
        if ( schema is not null )
        {
            foreach ( var (name, sample) in _seenUniforms )
            {
                if ( _bag.ContainsKey( name ) ) continue;
                if ( !schema.Defaults.TryGetValue( name, out var def ) ) continue;
                if ( ResetValue( sample, def.Floats, def.Ints ) is { } reset )
                    SetAttribute( cl, name, reset );
            }
        }

        foreach ( var (name, value) in _bag )
        {
            var resolved = value.Resolve();
            SetAttribute( cl, name, resolved );
            if ( resolved is not Texture )
                _seenUniforms[name] = resolved;
        }

        switch ( _grab )
        {
            case GrabMode.Sharp:
                cl.Attributes.GrabFrameTexture( "FrameBufferCopyTexture" );
                break;
            case GrabMode.Blurred:
                cl.Attributes.GrabFrameTexture( "FrameBufferCopyTexture", Graphics.DownsampleMethod.GaussianBlur );
                break;
        }
    }

    static void SetAttribute( CommandList cl, string name, object value )
    {
        switch ( value )
        {
            case float f:   cl.Attributes.Set( name, f ); break;
            case bool b:    cl.Attributes.Set( name, b ); break;
            case Vector2 v: cl.Attributes.Set( name, v ); break;
            case Vector3 v: cl.Attributes.Set( name, v ); break;
            case Vector4 v: cl.Attributes.Set( name, v ); break;
            case Color c:   cl.Attributes.Set( name, c ); break;
            case Texture t: cl.Attributes.Set( name, t ); break;
        }
    }

    // Reads the shader's declared attribute names once per instance and warns on uniform names
    // the shader does not declare. Skipped silently when the source is unavailable.
    void Validate()
    {
        if ( _validated ) return;
        _validated = true;

        var valid = SchemaFor( CacheKey )?.Names;
        if ( valid is null || valid.Count == 0 ) return;

        foreach ( var name in _bag.Keys )
            if ( !valid.Contains( name ) )
                Sandbox.Internal.GlobalSystemNamespace.Log.Warning(
                    $"ShaderEffect for \"{_path ?? _shader?.ResourcePath}\": uniform \"{name}\" is not declared by the shader " +
                    $"(valid: {string.Join( ", ", valid )}). Ignored." );
    }

    // Names + defaults come from parsing the .shader SOURCE, which ships with the project and
    // declares every Attribute() with its Default(). The engine's Shader.Schema is editor-only
    // plumbing: at game runtime it throws ("Load must be called on the main thread!" — Apply
    // runs on the render thread) so it is not consulted at all (view-5u5m probe, 2026-06-11).
    // Null when the source is unreadable (e.g. unit tests, published build without raw
    // .shader files); reset and validation both skip then. The source is re-read at most once
    // per second so live shader edits refresh the schema without a restart.
    static ShaderSchemaInfo? SchemaFor( object key )
    {
        if ( _schemaCache.TryGetValue( key, out var entry ) )
        {
            if ( entry.SinceCheck < 1f ) return entry.Info;
            entry.SinceCheck = 0f;
            string source = ReadSource( key );
            int hash = source?.GetHashCode() ?? 0;
            if ( hash != entry.SourceHash )
            {
                entry.SourceHash = hash;
                entry.Info = source is null ? null : ParseShaderSource( source );
            }
            return entry.Info;
        }

        string src = ReadSource( key );
        entry = new SchemaCacheEntry
        {
            Info = src is null ? null : ParseShaderSource( src ),
            SourceHash = src?.GetHashCode() ?? 0,
            SinceCheck = 0f,
        };
        _schemaCache[key] = entry;
        return entry.Info;
    }

    static string? ReadSource( object key )
    {
        if ( (key as string ?? (key as Shader)?.ResourcePath) is not { } srcPath ) return null;
        try { return FileSystem.Mounted.ReadAllText( srcPath ); }
        catch { return null; }
    }

    static readonly System.Text.RegularExpressions.Regex _declRegex = new(
        @"\b(?<type>float[234]?|bool|int|Texture2D)\s+\w+\s*<(?<block>[^>]*)>",
        System.Text.RegularExpressions.RegexOptions.Compiled );
    static readonly System.Text.RegularExpressions.Regex _attrRegex = new(
        @"Attribute\s*\(\s*""(?<name>[^""]+)""\s*\)",
        System.Text.RegularExpressions.RegexOptions.Compiled );
    static readonly System.Text.RegularExpressions.Regex _defaultRegex = new(
        @"Default[234]?\s*\(\s*(?<args>[^)]*)\)",
        System.Text.RegularExpressions.RegexOptions.Compiled );

    // Pure: extracts Attribute()-bound uniform declarations and their Default() values from
    // .shader source. Defaults are stored in both vector slots so ResetValue can convert by the
    // bled value's runtime type. Texture declarations contribute a name (for validation) but no
    // default. Only the main file is scanned; #include'd uniforms (BoxSize, DpiScale) are
    // framework-managed and excluded anyway. Missing Default() = zeros, matching the engine's
    // unset-attribute behavior.
    internal static ShaderSchemaInfo? ParseShaderSource( string source )
    {
        var names    = new HashSet<string>();
        var defaults = new Dictionary<string, (Vector4 Floats, Vector4 Ints)>();

        foreach ( System.Text.RegularExpressions.Match m in _declRegex.Matches( source ) )
        {
            var block = m.Groups["block"].Value;
            var attr  = _attrRegex.Match( block );
            if ( !attr.Success ) continue;

            var name = attr.Groups["name"].Value;
            names.Add( name );

            if ( name is "BoxSize" or "DpiScale" ) continue;     // framework-managed per draw
            if ( m.Groups["type"].Value == "Texture2D" ) continue; // no resettable default

            Vector4 v = default;
            var def = _defaultRegex.Match( block );
            if ( def.Success )
            {
                var parts = def.Groups["args"].Value.Split( ',' );
                for ( int i = 0; i < parts.Length && i < 4; i++ )
                {
                    if ( !float.TryParse( parts[i].Trim(), System.Globalization.NumberStyles.Float,
                             System.Globalization.CultureInfo.InvariantCulture, out var f ) ) continue;
                    switch ( i )
                    {
                        case 0: v.x = f; break;
                        case 1: v.y = f; break;
                        case 2: v.z = f; break;
                        case 3: v.w = f; break;
                    }
                }
            }
            defaults[name] = (v, v);
        }

        return names.Count > 0 ? new ShaderSchemaInfo { Names = names, Defaults = defaults } : null;
    }

    // Pure: converts a shader's declared default (schema FloatDefault/IntDefault vectors) to the
    // runtime type of the value that bled in, so the reset lands in the same attribute slot type.
    // Null = no safe reset (unsupported type; textures are never recorded so never reach this).
    internal static object? ResetValue( object sample, Vector4 floats, Vector4 ints ) => sample switch
    {
        float   => floats.x,
        bool    => ints.x != 0f,
        Vector2 => new Vector2( floats.x, floats.y ),
        Vector3 => new Vector3( floats.x, floats.y, floats.z ),
        Color   => new Color( floats.x, floats.y, floats.z, floats.w ),
        Vector4 => floats,
        _       => null,
    };

    public virtual bool Equals( ShaderEffect? other )
    {
        if ( other is null ) return false;
        if ( ReferenceEquals( this, other ) ) return true;
        if ( EqualityContract != other.EqualityContract ) return false;
        return _path == other._path
            && ReferenceEquals( _shader, other._shader )
            && _grab == other._grab
            && BagEquals( _bag, other._bag );
    }

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add( EqualityContract );
        hc.Add( _path );
        hc.Add( _grab );
        hc.Add( _bag.Count );
        return hc.ToHashCode();
    }

    static bool BagEquals( Dictionary<string, UniformValue> a, Dictionary<string, UniformValue> b )
    {
        if ( a.Count != b.Count ) return false;
        foreach ( var (k, v) in a )
            if ( !b.TryGetValue( k, out var bv ) || !v.Equals( bv ) ) return false;
        return true;
    }
}
