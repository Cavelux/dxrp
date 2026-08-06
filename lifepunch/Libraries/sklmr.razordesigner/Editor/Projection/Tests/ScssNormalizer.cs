using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Grains.RazorDesigner.Projection.Tests;

public static class ScssNormalizer
{
    private static readonly HashSet<string> Shapes = new( StringComparer.Ordinal )
    {
        "width",
        "height",
        "flex-grow",
        "flex-shrink",
        "flex-basis",
        "flex-direction",
        "justify-content",
        "align-items",
        "gap",
        "flex-wrap",
        "padding",
        "margin",
        "min-width",
        "max-width",
        "min-height",
        "max-height",
        "font-family",
        "font-size",
        "font-weight",
        "color",
        "text-align",
        "background-color",
        "background-image",
        "background-size",
        "background-position",
        "background-repeat",
        "border-radius",
        "border-width",
        "border-color",
        "box-shadow",
        "opacity",
        "cursor",
        "overflow",
        // CSS Tier 3 (grd-7t2z)
        "position",
        "top",
        "left",
        "right",
        "bottom",
        "font-style",
        "text-transform",
        "letter-spacing",
        "line-height",
        "z-index",
        "pointer-events",
    };

    private static readonly Regex DeclarationPattern =
        new( @"^\s*(?<prop>[\w-]+)\s*:\s*(?<val>.+?)\s*;?\s*$", RegexOptions.Compiled );

    // Matches the start of a checkmark nested block.
    private static readonly Regex CheckmarkOpen =
        new( @"^\s*>\s*\.checkmark\s*\{", RegexOptions.Compiled );

    private static readonly Regex PseudoOpen =
        new( @"^\s*&:(?<sel>[\w-]+(?:\([^)]*\))?)\s*\{", RegexOptions.Compiled );

    // Matches a closing brace.
    private static readonly Regex ClosingBrace =
        new( @"^\s*\}\s*$", RegexOptions.Compiled );

    public static IReadOnlyList<string> Normalize( IReadOnlyList<string> lines )
    {
        var result = new List<string>( lines.Count );
        int i = 0;
        while ( i < lines.Count )
        {
            var line = lines[i];

            // --- Checkmark nested block ---
            if ( CheckmarkOpen.IsMatch( line ) )
            {
                result.Add( "> .checkmark {" );
                i++;
                // Consume inner lines until closing brace
                while ( i < lines.Count )
                {
                    var inner = lines[i];
                    if ( ClosingBrace.IsMatch( inner ) )
                    {
                        result.Add( "}" );
                        i++;
                        break;
                    }
                    // Inner declarations use the same whitelist
                    result.Add( NormalizeSingleLine( inner ) );
                    i++;
                }
                continue;
            }

            // --- Pseudo-class nested block (grd-74lj): &:hover { ... } ---
            var pseudoMatch = PseudoOpen.Match( line );
            if ( pseudoMatch.Success )
            {
                var sel = pseudoMatch.Groups["sel"].Value;
                result.Add( $"&:{sel} {{" );
                i++;
                while ( i < lines.Count )
                {
                    var inner = lines[i];
                    if ( ClosingBrace.IsMatch( inner ) )
                    {
                        result.Add( "}" );
                        i++;
                        break;
                    }
                    result.Add( NormalizeSingleLine( inner ) );
                    i++;
                }
                continue;
            }

            // --- Regular declaration ---
            result.Add( NormalizeSingleLine( line ) );
            i++;
        }
        return result;
    }

    private static string NormalizeSingleLine( string line )
    {
        var m = DeclarationPattern.Match( line );
        if ( !m.Success )
        {
            throw new InvalidOperationException(
                $"ScssNormalizer: unrecognized declaration '{line.Trim()}' — extend the whitelist in ScssNormalizer.Shapes" );
        }

        var prop = m.Groups["prop"].Value;
        var val  = m.Groups["val"].Value.Trim();

        if ( !Shapes.Contains( prop ) )
        {
            throw new InvalidOperationException(
                $"ScssNormalizer: unrecognized declaration '{line.Trim()}' — extend the whitelist in ScssNormalizer.Shapes" );
        }

        return $"{prop}: {val};";
    }
}
