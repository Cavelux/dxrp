using Editor;
using Sandbox;

namespace Goo.Authoring;

[CustomEditor( typeof( float ), NamedEditor = "goo-float" )]
public sealed class GooFloatControlWidget : FloatControlWidget
{
	public GooFloatControlWidget( SerializedProperty property ) : base( property )
	{
		HideStepButtons();
		GooTokenBindingButton.Attach( this );
	}

	void HideStepButtons()
	{
		foreach ( var button in GetDescendants<IconButton>() )
		{
			button.Visible = false;
			button.Enabled = false;
		}
	}

	protected override void DoLayout()
	{
		base.DoLayout();
		LineEdit.Size = new Vector2( Width - LineEdit.Position.x, Height );
	}
}

[CustomEditor( typeof( int ), NamedEditor = "goo-integer" )]
public sealed class GooIntegerControlWidget : IntegerControlWidget
{
	public GooIntegerControlWidget( SerializedProperty property ) : base( property )
	{
		HideStepButtons();
		GooTokenBindingButton.Attach( this );
	}

	void HideStepButtons()
	{
		foreach ( var button in GetDescendants<IconButton>() )
		{
			button.Visible = false;
			button.Enabled = false;
		}
	}

	protected override void DoLayout()
	{
		base.DoLayout();
		LineEdit.Size = new Vector2( Width - LineEdit.Position.x, Height );
	}
}
