using Goo;
using Sandbox;

namespace Goo.Authoring;

/// <summary>Mounts child BlobNodes on the ScreenPanel or WorldPanel sharing this GameObject.</summary>
[Title( "Goo Hierarchy Host" )]
[Category( "Goo" )]
[Icon( "account_tree" )]
public sealed class GooHierarchyHost : GooPanel<Container>
{
	/// <summary>Rebuilds every frame for the realtime editor experience, not live games; disable it for normal runtime.</summary>
	[Property, Title( "Live Update" )]
	public bool LiveUpdate { get; set; } = true;

	protected override bool Tick( float dt ) => LiveUpdate;

	protected override Container Build()
	{
		var root = Hud.Overlay();
		if ( GameObject.Components.Get<GooTokenScope>() is { } scope )
			scope.Apply( () => BlobNode.AddChildNodes( GameObject, root.Children ) );
		else
			BlobNode.AddChildNodes( GameObject, root.Children );
		return root;
	}
}
