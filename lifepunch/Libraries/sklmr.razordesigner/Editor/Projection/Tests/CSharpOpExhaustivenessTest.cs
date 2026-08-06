using System.Linq;
using System.Reflection;
using Grains.RazorDesigner.Projection.CSharp;

namespace Grains.RazorDesigner.Projection.Tests;

public static class CSharpOpExhaustivenessTest
{
    public static (bool pass, string message) Run()
    {
        var ops = new CSharpOp[]
        {
            // File-level scaffold (5)
            new HeaderBanner( "Foo", "Bar.Baz" ),
            new UsingDirective( "Sandbox.UI" ),
            new NamespaceOpen( "My.Namespace" ),
            new ClassOpen( "MyPanel", "PanelComponent" ),
            new ClassClose(),

            // Member-level (3)
            new FieldDecl( "private", "int", "_count", "0", false ),
            new MethodOpen( "public", true, false, "void", "OnAfterTreeRender", "bool firstRender" ),
            new MethodClose(),

            // Body-level (5)
            new Statement( "x = 1" ),
            new BlockOpen( "if ( x > 0 )" ),
            new BlockClose(),
            new BlankLine(),
            new Comment( "Auto-generated — do not edit." ),
        };

        int arrayCount = ops.Length;

        var reflectedTypes = typeof( CSharpOp ).Assembly
            .GetTypes()
            .Where( t => t != typeof( CSharpOp ) && typeof( CSharpOp ).IsAssignableFrom( t ) && !t.IsAbstract )
            .ToArray();

        int reflectedCount = reflectedTypes.Length;

        if ( arrayCount != reflectedCount )
        {
            var reflectedNames = string.Join( ", ", reflectedTypes.Select( t => t.Name ) );
            return (false,
                $"CSharpOpExhaustivenessTest MISMATCH: inline array has {arrayCount} ops " +
                $"but reflection found {reflectedCount} CSharpOp-derived types: {reflectedNames}. " +
                "Add the missing leaf to the inline array AND to CSharpApplier's switch in the same commit.");
        }

        foreach ( var op in ops )
        {
            try
            {
                CSharpApplier.ApplyOpToScratch( op );
            }
            catch ( System.Diagnostics.UnreachableException ex )
            {
                return (false,
                    $"CSharpOpExhaustivenessTest: CSharpApplier.ApplyOpToScratch threw for " +
                    $"'{op.GetType().Name}': {ex.Message}");
            }
        }

        return (true,
            $"13/13 known leaves accepted by CSharpApplier.ApplyOpToScratch.");
    }
}
