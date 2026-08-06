using System;
using System.Collections.Generic;
using System.Linq;
using Editor;
using Sandbox;

namespace Goo.Authoring;

[CustomEditor( typeof( BlobNode ) )]
public sealed class BlobNodeEditorWidget : ComponentEditorWidget
{
	public BlobNodeEditorWidget( SerializedObject obj ) : base( obj )
	{
		Layout = global::Editor.Layout.Column();

		var sheet = new ControlSheet { IncludePropertyNames = true };
		sheet.AddObject( obj, ShouldInclude );
		Layout.Add( sheet );
	}

	static bool ShouldInclude( SerializedProperty property )
	{
		if ( property.PropertyType is null ) return false;
		if ( property.PropertyType.IsAssignableTo( typeof( System.Delegate ) )
			&& property.Name.StartsWith( "OnComponent" ) ) return false;
		if ( property.IsMethod ) return true;
		return property.HasAttribute<PropertyAttribute>();
	}
}

sealed class GooTokenBindingButton : IconButton
{
	readonly ControlWidget _control;
	readonly BlobNode? _node;
	readonly float _trailingInset;

	SerializedProperty Property => _control.SerializedProperty;

	public static void Attach( ControlWidget control, float trailingInset = 2f )
	{
		if ( control.SerializedProperty?.HasAttribute<GooTokenAttribute>() != true ) return;
		_ = new GooTokenBindingButton( control, trailingInset );
	}

	GooTokenBindingButton( ControlWidget control, float trailingInset ) : base( "data_object", parent: control )
	{
		_control = control;
		_node = control.SerializedProperty?.Parent?.Targets?.OfType<BlobNode>().FirstOrDefault();
		_trailingInset = trailingInset;
		FixedWidth = 16;
		FixedHeight = 16;
		IconSize = 12;
		Background = Color.Transparent;
		OnClick = OpenMenu;
	}

	[EditorEvent.Frame]
	public void RefreshFrame() => Refresh();

	public void Refresh()
	{
		var binding = CurrentBinding();
		var candidates = Candidates();
		var isBound = !string.IsNullOrWhiteSpace( binding );
		var resolves = isBound && candidates.Any( candidate => candidate.Name.Trim() == binding );
		var isHovered = _control.IsUnderMouse
			|| _control.GetDescendants<Widget>().Any( child => child.IsUnderMouse );
		Visible = isBound || (isHovered && HasScope());
		if ( !Visible ) return;

		Position = new Vector2( Math.Max( 0f, _control.Width - Width - _trailingInset ), 2f );
		var accent = resolves ? Theme.Blue : Theme.Yellow;
		Foreground = isBound ? accent : Theme.TextControl.WithAlpha( 0.75f );
		Background = isBound ? accent.WithAlpha( 0.16f ) : Color.Transparent;
		ToolTip = !isBound
			? candidates.Count > 0
				? $"Bind {Property.DisplayName} to a Goo token"
				: $"No compatible Goo tokens are defined for {Property.DisplayName}"
			: resolves
				? $"{Property.DisplayName} uses @{binding}. The literal remains as its fallback."
				: $"@{binding} is missing or has the wrong type. {Property.DisplayName} is using its literal fallback.";
		Raise();
	}

	void OpenMenu()
	{
		var menu = new ContextMenu( this );
		var binding = CurrentBinding();
		var candidates = Candidates();
		if ( !string.IsNullOrWhiteSpace( binding ) )
		{
			menu.AddOption( "Use literal value", "link_off", ClearBinding );
			menu.AddSeparator();
		}

		foreach ( var token in candidates )
		{
			var candidate = token;
			var name = candidate.Name.Trim();
			var icon = name == binding ? "check" : "data_object";
			menu.AddOption( $"@{name}", icon, () => SetBinding( name ) );
		}

		if ( candidates.Count == 0 )
			menu.AddOption( "No compatible tokens in scope", "info", () => { } ).Enabled = false;

		menu.OpenAtCursor( false );
	}

	string? CurrentBinding()
	{
		var bindings = BindingProperty()?.GetValue<Dictionary<string, string>>();
		return bindings is not null && bindings.TryGetValue( Property.Name, out var name ) ? name : null;
	}

	void SetBinding( string tokenName ) => ChangeBindings( bindings => bindings[Property.Name] = tokenName );

	void ClearBinding() => ChangeBindings( bindings => bindings.Remove( Property.Name ) );

	void ChangeBindings( Action<Dictionary<string, string>> change )
	{
		var property = BindingProperty();
		if ( property is null ) return;

		var bindings = property.GetValue<Dictionary<string, string>>() is { } current
			? new Dictionary<string, string>( current )
			: new Dictionary<string, string>();
		change( bindings );

		property.Parent.NoteStartEdit( property );
		property.SetValue( bindings );
		property.Parent.NoteFinishEdit( property );
		Refresh();
	}

	SerializedProperty? BindingProperty() => Property.Parent?.GetProperty( nameof( BlobNode.TokenBindings ) );

	List<GooTokenEntry> Candidates()
	{
		var result = new List<GooTokenEntry>();
		var seen = new HashSet<string>( StringComparer.Ordinal );
		foreach ( var scope in Scopes() )
		{
			foreach ( var token in scope.Tokens )
			{
				if ( token is null || string.IsNullOrWhiteSpace( token.Name ) ) continue;
				var name = token.Name.Trim();
				if ( !seen.Add( name ) ) continue;
				if ( Supports( token.Kind, Property.PropertyType ) ) result.Add( token );
			}
		}
		return result;
	}

	bool HasScope() => Scopes().Any();

	IEnumerable<GooTokenScope> Scopes()
	{
		for ( var gameObject = _node?.GameObject; gameObject is not null; gameObject = gameObject.Parent )
		{
			if ( gameObject.Components.Get<GooTokenScope>() is { } scope )
				yield return scope;
		}
	}

	static bool Supports( GooTokenKind kind, Type propertyType )
	{
		propertyType = Nullable.GetUnderlyingType( propertyType ) ?? propertyType;
		return kind switch
		{
			GooTokenKind.Color => propertyType == typeof( Color ),
			GooTokenKind.Length => propertyType == typeof( GooLength ) || propertyType == typeof( Sandbox.UI.Length ),
			GooTokenKind.Float => propertyType == typeof( float ),
			GooTokenKind.Integer => propertyType == typeof( int ),
			GooTokenKind.Boolean => propertyType == typeof( bool ),
			GooTokenKind.String => propertyType == typeof( string ),
			GooTokenKind.Texture => propertyType == typeof( Texture ),
			_ => false,
		};
	}
}
