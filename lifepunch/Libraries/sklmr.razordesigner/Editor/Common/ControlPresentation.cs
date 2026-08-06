using Grains.RazorDesigner.Contracts;
using Grains.RazorDesigner.Document;
using Sandbox;

namespace Grains.RazorDesigner.Common;

public static class ControlPresentation
{
	public static readonly Color ContainerTint   = new( 0.247f, 0.663f, 0.961f ); // #3FA9F5
	public static readonly Color LeafTint        = new( 0.302f, 0.816f, 0.882f ); // #4DD0E1
	public static readonly Color PseudoclassTint = new( 0.729f, 0.408f, 0.784f ); // #BA68C8
	public static readonly Color TemplateTint    = new( 0.961f, 0.620f, 0.043f ); // #F59E0B

	public static Color IconTint( ControlType type )
	{
		return ContractScanner.Table.Get( type ).IsContainer ? ContainerTint : LeafTint;
	}
}
