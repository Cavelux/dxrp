using System.Text.Json;
using Grains.RazorDesigner.Wiring;

namespace Grains.RazorDesigner.CodeDialog;

public static class InitialValueParser
{
    public static Expression Parse( string declaredType, string rawText )
    {
        rawText ??= "";
        var trimmed = rawText.Trim();

        if ( string.IsNullOrEmpty( trimmed ) )
            return new LiteralExpression { TypeId = declaredType, Value = DefaultFor( declaredType ) };

        switch ( declaredType )
        {
            case "string":
                if ( trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"' )
                {
                    try
                    {
                        var decoded = JsonSerializer.Deserialize<string>( trimmed );
                        return new LiteralExpression { TypeId = "string", Value = ToJson( decoded ?? "" ) };
                    }
                    catch ( JsonException )
                    {
                        // Malformed quoting (e.g. unescaped internal quote) — fall through to raw.
                    }
                }
                return new LiteralExpression { TypeId = "string", Value = ToJson( trimmed ) };

            case "int":
                if ( int.TryParse( trimmed, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out var i ) )
                    return new LiteralExpression { TypeId = "int", Value = ToJson( i ) };
                break;

            case "float":
                if ( float.TryParse( trimmed, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var f ) )
                    return new LiteralExpression { TypeId = "float", Value = ToJson( f ) };
                break;

            case "bool":
                if ( bool.TryParse( trimmed, out var b ) )
                    return new LiteralExpression { TypeId = "bool", Value = ToJson( b ) };
                break;
        }

        return new InlineExpression { TypeId = declaredType, Code = trimmed };
    }

    private static JsonElement DefaultFor( string declaredType ) => declaredType switch
    {
        "string" => ToJson( "" ),
        "int"    => ToJson( 0 ),
        "float"  => ToJson( 0f ),
        "bool"   => ToJson( false ),
        _        => ToJson( (string)null ),   // typed-null sentinel for non-primitive defaults
    };

    private static JsonElement ToJson<T>( T value )
        => JsonSerializer.SerializeToElement( value );
}
