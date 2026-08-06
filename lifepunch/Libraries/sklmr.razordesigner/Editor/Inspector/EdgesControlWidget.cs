using Editor;
using Grains.RazorDesigner.Document;
using Sandbox;

namespace Grains.RazorDesigner.Inspector;

[CustomEditor( typeof( Edges ) )]
public sealed class EdgesControlWidget : ControlWidget
{
	private const string LogPrefix = "[Grains.RazorDesigner]";

	public override bool SupportsMultiEdit => true;

	private readonly EdgesProxy _proxy;
	private readonly SerializedObject _proxySerialized;
	// Synchronous change events on both sides; without this guard SetValue would loop.
	private bool _syncing;

	private sealed class EdgesProxy
	{
		public Length Top    { get; set; } = Length.Px( 0 );
		public Length Right  { get; set; } = Length.Px( 0 );
		public Length Bottom { get; set; } = Length.Px( 0 );
		public Length Left   { get; set; } = Length.Px( 0 );
	}

	public EdgesControlWidget( SerializedProperty property ) : base( property )
	{
		Log.Info( $"{LogPrefix} EdgesControlWidget ctor for {property.Name}" );

		Layout = Layout.Column();
		Layout.Spacing = 2;

		_proxy = new EdgesProxy();
		_proxySerialized = EditorTypeLibrary.GetSerializedObject( _proxy );

		var topRow = Layout.Add( Layout.Row() );
		topRow.Spacing = 2;
		AddSide( topRow, nameof( EdgesProxy.Top    ), "border_top"    );
		AddSide( topRow, nameof( EdgesProxy.Right  ), "border_right"  );

		var bottomRow = Layout.Add( Layout.Row() );
		bottomRow.Spacing = 2;
		AddSide( bottomRow, nameof( EdgesProxy.Bottom ), "border_bottom" );
		AddSide( bottomRow, nameof( EdgesProxy.Left   ), "border_left"   );

		_proxySerialized.OnPropertyChanged += OnProxyChanged;

		SyncFromProperty();
	}

	private void AddSide( Layout row, string propName, string icon )
	{
		var prop = _proxySerialized.GetProperty( propName );
		var lengthWidget = new LengthControlWidget( prop, icon );
		row.Add( lengthWidget, 1 );
	}

	protected override void PaintControl()
	{
		// nothing
	}

	private void SyncFromProperty()
	{
		if ( _syncing ) return;
		_syncing = true;

		try
		{
			var e = SerializedProperty.GetValue<Edges>( Edges.Zero );

			_proxySerialized.GetProperty( nameof( EdgesProxy.Top    ) ).SetValue( e.Top );
			_proxySerialized.GetProperty( nameof( EdgesProxy.Right  ) ).SetValue( e.Right );
			_proxySerialized.GetProperty( nameof( EdgesProxy.Bottom ) ).SetValue( e.Bottom );
			_proxySerialized.GetProperty( nameof( EdgesProxy.Left   ) ).SetValue( e.Left );
		}
		finally
		{
			_syncing = false;
		}
	}

	private void OnProxyChanged( SerializedProperty property )
	{
		if ( _syncing ) return;
		if ( ReadOnly || !SerializedProperty.IsEditable )
			return;

		_syncing = true;
		try
		{
			var newValue = new Edges( _proxy.Top, _proxy.Right, _proxy.Bottom, _proxy.Left );

			Log.Info( $"{LogPrefix} EdgesControlWidget OnProxyChanged {newValue}" );

			PropertyStartEdit();
			SerializedProperty.SetValue( newValue );
			SignalValuesChanged();
			PropertyFinishEdit();
		}
		finally
		{
			_syncing = false;
		}
	}

	protected override void OnValueChanged()
	{
		base.OnValueChanged();
		SyncFromProperty();
	}
}
