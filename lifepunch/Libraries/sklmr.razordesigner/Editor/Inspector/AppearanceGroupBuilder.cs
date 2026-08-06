using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Editor;
using Grains.RazorDesigner.Common;
using Grains.RazorDesigner.Contracts;
using Grains.RazorDesigner.Document;
using Sandbox;
using Margin = Sandbox.UI.Margin;

namespace Grains.RazorDesigner.Inspector;

public sealed class AppearanceGroupBuilder
{
	private const string LogPrefix = "[Grains.RazorDesigner]";

	public readonly record struct GroupSpec( string Group, bool DefaultExpanded, string Icon );

	private readonly IReadOnlyList<GroupSpec> _spec;
	private readonly IExpandStateStore _store;       // null = in-memory only.
	private readonly Dictionary<string, bool> _memoryState = new();

	private readonly Dictionary<string, CollapsibleSection> _sections = new();
	private readonly Dictionary<string, ControlWidget> _propertyWidgets = new();

	public IReadOnlyDictionary<string, CollapsibleSection> Sections => _sections;
	public IReadOnlyDictionary<string, ControlWidget> PropertyWidgets => _propertyWidgets;

	public AppearanceGroupBuilder( IReadOnlyList<GroupSpec> spec, IExpandStateStore store )
	{
		_spec = spec;
		_store = store;
	}

	public IReadOnlySet<string> Build(
		Layout host,
		SerializedObject obj,
		string filterText,
		Func<SerializedProperty, bool> predicate = null )
	{
		_sections.Clear();
		_propertyWidgets.Clear();

		var rendered = new HashSet<string>();
		var filterActive = !string.IsNullOrEmpty( filterText );

		foreach ( var (groupName, defaultExpanded, icon) in _spec )
		{
			var sheet = new ControlSheet();
			sheet.IncludePropertyNames = true;
			var addedAny = false;

			foreach ( var prop in obj )
			{
				if ( !string.Equals( prop.GroupName, groupName, StringComparison.Ordinal ) ) continue;
				if ( predicate != null && !predicate( prop ) ) continue;
				if ( filterActive )
				{
					var nameHit = prop.Name?.Contains( filterText, StringComparison.OrdinalIgnoreCase ) == true;
					var displayHit = prop.DisplayName?.Contains( filterText, StringComparison.OrdinalIgnoreCase ) == true;
					var groupHit = prop.GroupName?.Contains( filterText, StringComparison.OrdinalIgnoreCase ) == true;
					if ( !nameHit && !displayHit && !groupHit ) continue;
				}

				var widget = sheet.AddRow( prop );
				if ( widget is null ) continue;
				_propertyWidgets[prop.Name] = widget;
				rendered.Add( prop.Name );
				addedAny = true;
			}

			if ( !addedAny ) continue;

			var preferred = GetExpand( groupName, defaultExpanded );

			var section = new CollapsibleSection( null, groupName, icon );
			section.Expanded = filterActive || preferred;
			section.BodyLayout.Add( sheet );

			var capturedFilterActive = filterActive;
			var capturedGroupName = groupName;
			section.ExpandedChanged = expanded =>
			{
				if ( capturedFilterActive ) return;
				SetExpand( capturedGroupName, expanded );
			};

			host.Add( section );
			_sections[groupName] = section;
		}

		return rendered;
	}

	public void ApplyReadOnlyStates( object gateSource, IContractTable contracts, ControlType targetType )
	{
		if ( gateSource is null ) return;

		var contract = contracts.Get( targetType );
		var gateSourceType = gateSource.GetType();

		foreach ( var group in contract.Groups() )
		{
			var gateField = contract.PayloadFields.FirstOrDefault( f => f.IsOverrideGate && f.Group == group );
			if ( gateField.Name is null or "" ) continue;

			var gateProp = gateSourceType.GetProperty( gateField.Name, BindingFlags.Public | BindingFlags.Instance );
			if ( gateProp is null )
			{
				Log.Warning( $"{LogPrefix} AppearanceGroupBuilder.ApplyReadOnlyStates: Override gate '{gateField.Name}' " +
					$"not found on {gateSourceType.Name} for group '{group}'. Skipping gray-out." );
				continue;
			}

			var gateOn = gateProp.GetValue( gateSource ) is true;
			var gateOff = !gateOn;

			foreach ( var name in contract.PropertyNamesIn( group ) )
			{
				if ( _propertyWidgets.TryGetValue( name, out var w ) ) w.ReadOnly = gateOff;
			}
		}
	}

	private bool GetExpand( string key, bool fallback )
	{
		if ( _store != null ) return _store.Get( key, fallback );
		return _memoryState.TryGetValue( key, out var v ) ? v : fallback;
	}

	private void SetExpand( string key, bool expanded )
	{
		if ( _store != null ) { _store.Set( key, expanded ); return; }
		_memoryState[key] = expanded;
	}
}
