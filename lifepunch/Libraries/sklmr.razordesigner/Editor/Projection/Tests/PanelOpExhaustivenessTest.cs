using System;

namespace Grains.RazorDesigner.Projection.Tests;

public static class PanelOpExhaustivenessTest
{
    public static (bool pass, string message) Run()
    {
        var ops = new PanelOp[]
        {
            new SetClass( "" ),
            new SetStyle( "", "" ),
            new SetAttribute( "", "" ),
            new SetInnerText( "" ),
        };
        try
        {
            foreach ( var op in ops )
                Applier.ApplyOpToScratch( op );
            return (true, $"PanelOpExhaustivenessTest: {ops.Length} variants OK");
        }
        catch ( Exception e )
        {
            return (false, $"PanelOpExhaustivenessTest FAILED: {e.GetType().Name}: {e.Message}");
        }
    }
}
