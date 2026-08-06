namespace Grains.RazorDesigner.Projection;

public abstract record PanelOp;
public sealed record SetClass( string Class ) : PanelOp;
public sealed record SetStyle( string Property, string Value ) : PanelOp;
public sealed record SetAttribute( string Name, string Value ) : PanelOp;
public sealed record SetInnerText( string Text ) : PanelOp;
