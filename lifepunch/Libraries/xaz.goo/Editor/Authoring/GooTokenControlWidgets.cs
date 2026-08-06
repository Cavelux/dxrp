using Editor;
using Sandbox;

namespace Goo.Authoring;

/// <summary>Token-aware stock color field. The affordance overlays the field and consumes no row width.</summary>
[CustomEditor( typeof( Color ), WithAllAttributes = [typeof( GooTokenAttribute )] )]
public sealed class GooTokenColorControlWidget : ColorControlWidget
{
	public GooTokenColorControlWidget( SerializedProperty property ) : base( property )
	{
		GooTokenBindingButton.Attach( this );
	}
}

/// <summary>Token-aware stock boolean field.</summary>
[CustomEditor( typeof( bool ), WithAllAttributes = [typeof( GooTokenAttribute )] )]
public sealed class GooTokenBoolControlWidget : BoolControlWidget
{
	public GooTokenBoolControlWidget( SerializedProperty property ) : base( property )
	{
		GooTokenBindingButton.Attach( this );
	}
}
