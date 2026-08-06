using Editor;
using Grains.RazorDesigner.Document;
using Sandbox;

namespace Grains.RazorDesigner.Inspector;

[CustomEditor( typeof( Length ) )]
public sealed class LengthControlWidget : ControlWidget
{
	private const string LogPrefix = "[Grains.RazorDesigner]";

	private const int ValueEditWidthSolo    = 60;
	private const int ValueEditWidthCompact = 32;
	private const int UnitWidthSolo         = 96;
	private const int UnitWidthCompact      = 72;

	public override bool SupportsMultiEdit => true;

	private LineEdit _valueEdit;
	private EnumControlWidget _unitWidget;
	private UnitProxy _unitProxy;
	private SerializedObject _unitSerialized;
	// Synchronous change events on both sides; without this guard SetValue would loop.
	private bool _syncing;

	private sealed class UnitProxy
	{
		public LengthUnit Unit { get; set; }
	}

	// Engine-instantiated solo ctor. Width/Height/etc inspector rows go through this.
	public LengthControlWidget( SerializedProperty property ) : this( property, icon: null ) { }

	public LengthControlWidget( SerializedProperty property, string icon ) : base( property )
	{
		Log.Info( $"{LogPrefix} LengthControlWidget ctor for {property.Name} icon={icon ?? "(none)"}" );

		var compact = !string.IsNullOrEmpty( icon );

		Layout = Layout.Row();
		Layout.Spacing = 2;

		if ( compact )
		{
			var iconBox = new IconBoxWidget( this, icon )
			{
				FixedSize = new Vector2( Theme.RowHeight, Theme.RowHeight ),
			};
			Layout.Add( iconBox );
		}

		_valueEdit = new LineEdit( this );
		_valueEdit.MinimumSize = new Vector2( compact ? ValueEditWidthCompact : ValueEditWidthSolo, Theme.RowHeight );
		_valueEdit.MaximumSize = new Vector2( 4096, Theme.RowHeight );
		_valueEdit.SetStyles( "background-color: transparent;" );
		_valueEdit.EditingStarted += OnValueEditStarted;
		_valueEdit.TextEdited += OnValueEditTextEdited;
		_valueEdit.EditingFinished += OnValueEditFinished;
		Layout.Add( _valueEdit, 1 );

		_unitProxy = new UnitProxy { Unit = LengthUnit.Auto };
		_unitSerialized = EditorTypeLibrary.GetSerializedObject( _unitProxy );
		var unitProp = _unitSerialized.GetProperty( nameof( UnitProxy.Unit ) );
		_unitWidget = new EnumControlWidget( unitProp );
		_unitWidget.MinimumWidth = compact ? UnitWidthCompact : UnitWidthSolo;
		_unitWidget.MaximumWidth = compact ? UnitWidthCompact : UnitWidthSolo;
		_unitSerialized.OnPropertyChanged += OnUnitProxyChanged;
		Layout.Add( _unitWidget );

		SyncFromProperty();
	}

	private sealed class IconBoxWidget : Widget
	{
		private readonly string _icon;

		public IconBoxWidget( Widget parent, string icon ) : base( parent )
		{
			_icon = icon;
		}

		protected override void OnPaint()
		{
			var rect = new Rect( 0, Size );
			Paint.SetPen( Theme.TextControl.WithAlpha( 0.7f ) );
			Paint.DrawIcon( rect, _icon, Theme.RowHeight - 4, TextFlag.Center );
		}
	}

	private void SyncFromProperty()
	{
		if ( _syncing ) return;
		_syncing = true;

		try
		{
			var len = SerializedProperty.GetValue<Length>( Length.Auto );

			if ( !_valueEdit.IsFocused )
				_valueEdit.Text = len.Value.ToString( "0.###" );

			// Push through SerializedProperty so EnumControlWidget repaints; direct field assignment skips the event.
			var unitProp = _unitSerialized.GetProperty( nameof( UnitProxy.Unit ) );
			unitProp.SetValue( len.Unit );

			_valueEdit.Enabled = len.Unit != LengthUnit.Auto;
		}
		finally
		{
			_syncing = false;
		}
	}

	private void OnUnitProxyChanged( SerializedProperty property )
	{
		if ( _syncing ) return;
		if ( ReadOnly || !SerializedProperty.IsEditable )
			return;

		_syncing = true;
		try
		{
			var current = SerializedProperty.GetValue<Length>( Length.Auto );
			var newValue = new Length( current.Value, _unitProxy.Unit );

			Log.Info( $"{LogPrefix} LengthControlWidget OnUnitChanged {newValue}" );

			PropertyStartEdit();
			SerializedProperty.SetValue( newValue );
			SignalValuesChanged();
			PropertyFinishEdit();

			_valueEdit.Enabled = _unitProxy.Unit != LengthUnit.Auto;
		}
		finally
		{
			_syncing = false;
		}
	}

	private void OnValueEditStarted()
	{
		if ( ReadOnly || !SerializedProperty.IsEditable )
			return;
		PropertyStartEdit();
	}

	private void OnValueEditTextEdited( string text )
	{
		if ( _syncing ) return;
		if ( ReadOnly || !SerializedProperty.IsEditable )
			return;

		if ( !float.TryParse( text, out var parsed ) )
			return;

		var current = SerializedProperty.GetValue<Length>( Length.Auto );
		var newValue = new Length( parsed, current.Unit );
		if ( newValue == current )
			return;

		_syncing = true;
		try
		{
			SerializedProperty.SetValue( newValue );
			SignalValuesChanged();
		}
		finally
		{
			_syncing = false;
		}
	}

	private void OnValueEditFinished()
	{
		if ( _syncing ) return;
		if ( ReadOnly || !SerializedProperty.IsEditable )
		{
			PropertyFinishEdit();
			return;
		}

		var current = SerializedProperty.GetValue<Length>( Length.Auto );

		if ( !float.TryParse( _valueEdit.Text, out var parsed ) )
		{
			// Restore on bad input; don't commit (avoids spurious ValueChanged).
			_valueEdit.Text = current.Value.ToString( "0.###" );
			Log.Info( $"{LogPrefix} LengthControlWidget OnValueEditFinished: parse failed, restored to {current.Value}" );
			PropertyFinishEdit();
			return;
		}

		var formatted = parsed.ToString( "0.###" );
		if ( _valueEdit.Text != formatted )
			_valueEdit.Text = formatted;

		var newValue = new Length( parsed, current.Unit );
		if ( newValue != current )
		{
			Log.Info( $"{LogPrefix} LengthControlWidget OnValueChanged {newValue}" );
			_syncing = true;
			try
			{
				SerializedProperty.SetValue( newValue );
				SignalValuesChanged();
			}
			finally
			{
				_syncing = false;
			}
		}

		PropertyFinishEdit();
	}

	protected override void OnValueChanged()
	{
		base.OnValueChanged();
		SyncFromProperty();
	}
}
