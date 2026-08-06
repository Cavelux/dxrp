using System;
using System.Collections.Generic;
using Editor;
using Grains.RazorDesigner.Common;
using Grains.RazorDesigner.Contracts;
using Grains.RazorDesigner.Document;
using Sandbox;

namespace Grains.RazorDesigner.Inspector;

public sealed class StateSectionWidget : CollapsibleSection
{
	private const string LogPrefix = "[Grains.RazorDesigner]";

	public Action<StateRule> RuleChanged;   // Mode/arg/order change — caller re-sorts + refreshes.
	public Action RemoveRequested;          // × clicked (UI-spec §Decision 5).

	public StateRule Rule { get; private set; }
	public ControlRecord Target { get; }

	private static readonly IReadOnlyList<AppearanceGroupBuilder.GroupSpec> StylingGroups =
		new[]
		{
			new AppearanceGroupBuilder.GroupSpec( "Typography",  false, "format_size" ),
			new AppearanceGroupBuilder.GroupSpec( "Background",  false, "palette" ),
			new AppearanceGroupBuilder.GroupSpec( "Border",      false, "border_style" ),
			new AppearanceGroupBuilder.GroupSpec( "Effects",     false, "auto_awesome" ),
			new AppearanceGroupBuilder.GroupSpec( "Constraints", false, "fit_screen" ),
			new AppearanceGroupBuilder.GroupSpec( "Interaction", false, "touch_app" ),
		};

	private AppearanceGroupBuilder _builder;
	private StateRuleAppearanceProxy _proxy;
	// Stored to prevent GC of the SerializedObject (engine pattern — keep the reference live).
	private SerializedObject _serialized;

	private static readonly HashSet<ControlType> FocusableTypes = new()
	{
		ControlType.TextEntry,
		ControlType.Button,
		ControlType.Checkbox,
		ControlType.DropDown,
	};

	private ComboBox _nthMode;
	private LineEdit _nthArg;

	public StateSectionWidget( Widget parent, ControlRecord target, StateRule rule )
		: base( parent, BuildTitle( rule ), BuildIcon( rule.State ) )
	{
		Target = target;
		Rule = rule;
		HeaderColor = ControlPresentation.PseudoclassTint;

		Log.Info( $"{LogPrefix} StateSectionWidget ctor: target={target.ClassName} state={rule.State}" );

		if ( rule.State == PseudoKind.NthChild )
		{
			Title = "";  // we paint our own text via the header-right widgets.
			BuildNthChildHeaderWidgets();
		}

		MaybeAddFocusableWarning();
		AddRemoveButton();
	}

	public void BuildBody( DesignerDocument document, Action onValueChanged )
	{
		Log.Info( $"{LogPrefix} StateSectionWidget.BuildBody: target={Target.ClassName} state={Rule.State}" );

		var keyState = Rule.State;
		var keyMode = Rule.NthChildMode;
		var keyArg = Rule.NthChildArg;
		_proxy = new StateRuleAppearanceProxy( Target, () =>
		{
			for ( int i = 0; i < Target.StateRules.Count; i++ )
			{
				var r = Target.StateRules[i];
				if ( r.State == keyState && r.NthChildMode == keyMode && r.NthChildArg == keyArg )
					return i;
			}
			return -1;
		} );
		_proxy.Changed += () => onValueChanged?.Invoke();
		_serialized = EditorTypeLibrary.GetSerializedObject( _proxy );

		_builder = new AppearanceGroupBuilder( StylingGroups, store: null );
		_builder.Build( BodyLayout, _serialized, filterText: "" );

		_proxy.Changed += () => _builder?.ApplyReadOnlyStates( _proxy, ContractScanner.Table, Target.Type );
		_builder.ApplyReadOnlyStates( _proxy, ContractScanner.Table, Target.Type );
	}

	private void AddRemoveButton()
	{
		var remove = new IconButton( "close", OnRemoveClicked )
		{
			ToolTip = "Remove this state rule",
			Background = Color.Transparent,
			FixedSize = new Vector2( Theme.RowHeight, Theme.RowHeight ),
		};
		HeaderRightLayout.Add( remove );
		HeaderRightReservedWidth += 24f;

		Log.Info( $"{LogPrefix} StateSectionWidget: × remove button added for state={Rule.State}" );
	}

	private void OnRemoveClicked()
	{
		// Empty Delta? Remove silently. Non-empty? Confirm.
		if ( IsDeltaEmpty( Rule.Delta ) )
		{
			Log.Info( $"{LogPrefix} StateSectionWidget remove (empty delta): state={Rule.State}" );
			RemoveRequested?.Invoke();
			return;
		}

		ShowRemoveConfirmDialog();
	}

	private void ShowRemoveConfirmDialog()
	{
		var stateLabel = Rule.State switch
		{
			PseudoKind.Hover      => ":hover",
			PseudoKind.Active     => ":active",
			PseudoKind.Focus      => ":focus",
			PseudoKind.Empty      => ":empty",
			PseudoKind.FirstChild => ":first-child",
			PseudoKind.LastChild  => ":last-child",
			PseudoKind.OnlyChild  => ":only-child",
			PseudoKind.NthChild   => ":nth-child(...)",
			_                     => Rule.State.ToString()
		};

		var dialog = new Editor.Dialog( this );
		dialog.Window.WindowTitle = "Remove state rule?";
		dialog.Window.SetWindowIcon( "warning" );
		dialog.Window.SetModal( true, true );
		dialog.Window.MinimumWidth = 360;

		dialog.Layout = Layout.Column();
		dialog.Layout.Margin = 16;
		dialog.Layout.Spacing = 12;

		var msg = new Editor.Label( dialog )
		{
			Text = $"Remove {stateLabel} rule with edits?",
			WordWrap = true,
		};
		dialog.Layout.Add( msg );

		var buttonRow = dialog.Layout.Add( Layout.Row() );
		buttonRow.Spacing = 8;
		buttonRow.AddStretchCell();

		var cancelBtn = new Editor.Button( dialog ) { Text = "Cancel", MinimumWidth = 72 };
		cancelBtn.MouseLeftPress += () => dialog.Close();
		buttonRow.Add( cancelBtn );

		var removeBtn = new Editor.Button( dialog ) { Text = "Remove", MinimumWidth = 72 };
		removeBtn.MouseLeftPress += () =>
		{
			Log.Info( $"{LogPrefix} StateSectionWidget remove (confirmed): state={Rule.State}" );
			dialog.Close();
			RemoveRequested?.Invoke();
		};
		buttonRow.Add( removeBtn );

		dialog.Window.AdjustSize();
		dialog.Show();

		Log.Info( $"{LogPrefix} StateSectionWidget ShowRemoveConfirmDialog: state={Rule.State} stateLabel={stateLabel}" );
	}

	private static bool IsDeltaEmpty( Appearance delta )
	{
		return !delta.OverrideTypography
			&& !delta.OverrideBackground
			&& !delta.OverrideBorder
			&& !delta.OverrideEffects
			&& !delta.OverrideConstraints
			&& !delta.OverrideInteraction;
	}

	private void MaybeAddFocusableWarning()
	{
		if ( Rule.State != PseudoKind.Focus && Rule.State != PseudoKind.Active ) return;
		if ( FocusableTypes.Contains( Target.Type ) ) return;

		var warn = new IconButton( "warning", () => { /* no-op; tooltip is the affordance */ } )
		{
			ToolTip = "This state usually only fires on focusable controls (TextEntry, Button, Checkbox, DropDown). The rule will save, but may not trigger at runtime.",
			Background = Color.Transparent,
			FixedSize = new Vector2( Theme.RowHeight, Theme.RowHeight ),
		};
		HeaderRightLayout.Add( warn );
		HeaderRightReservedWidth += 24f;

		Log.Info( $"{LogPrefix} StateSectionWidget: ⚠ added for state={Rule.State} on non-focusable type={Target.Type}" );
	}

	private void BuildNthChildHeaderWidgets()
	{
		var openLabel = new Label( ":nth-child(", null );
		HeaderRightLayout.Add( openLabel );

		_nthMode = new ComboBox( null );
		_nthMode.AddItem( "Literal", null, onSelected: () => OnNthModeChanged( NthChildMode.Literal ) );
		_nthMode.AddItem( "Odd",     null, onSelected: () => OnNthModeChanged( NthChildMode.Odd ) );
		_nthMode.AddItem( "Even",    null, onSelected: () => OnNthModeChanged( NthChildMode.Even ) );
		_nthMode.CurrentIndex = (int)Rule.NthChildMode;
		HeaderRightLayout.Add( _nthMode );

		_nthArg = new LineEdit( null );
		_nthArg.Text = Rule.NthChildArg.ToString();
		_nthArg.MaximumWidth = 48;
		_nthArg.TextEdited += OnNthArgEdited;
		_nthArg.ReadOnly = Rule.NthChildMode != NthChildMode.Literal;
		HeaderRightLayout.Add( _nthArg );

		var closeLabel = new Label( ")", null );
		HeaderRightLayout.Add( closeLabel );

		// Reserve enough header width for the inline widgets so the title-paint doesn't overlap.
		HeaderRightReservedWidth = 240f;
	}

	private void OnNthModeChanged( NthChildMode mode )
	{
		var oldRule = Rule;                       // capture BEFORE mutating field
		Rule = Rule with { NthChildMode = mode };
		_nthArg.ReadOnly = mode != NthChildMode.Literal;
		ReplaceRuleInDocument( oldRule, Rule );   // pass old explicitly
		RuleChanged?.Invoke( Rule );
	}

	private void OnNthArgEdited( string text )
	{
		if ( !int.TryParse( text, out var n ) || n < 1 ) return;
		var oldRule = Rule;
		Rule = Rule with { NthChildArg = n };
		ReplaceRuleInDocument( oldRule, Rule );
		RuleChanged?.Invoke( Rule );
	}

	private void ReplaceRuleInDocument( StateRule oldRule, StateRule newRule )
	{
		var i = Target.StateRules.IndexOf( oldRule );
		if ( i < 0 )
		{
			Log.Warning( $"{LogPrefix} ReplaceRuleInDocument: rule no longer in list, skipping" );
			return;
		}
		Target.StateRules[i] = newRule;
		Log.Info( $"{LogPrefix} ReplaceRuleInDocument: i={i} state={newRule.State} mode={newRule.NthChildMode} arg={newRule.NthChildArg}" );
	}

	private static string BuildTitle( StateRule rule )
	{
		return rule.State switch
		{
			PseudoKind.Hover      => ":hover",
			PseudoKind.Active     => ":active",
			PseudoKind.Focus      => ":focus",
			PseudoKind.Empty      => ":empty",
			PseudoKind.FirstChild => ":first-child",
			PseudoKind.LastChild  => ":last-child",
			PseudoKind.OnlyChild  => ":only-child",
			PseudoKind.NthChild   => ":nth-child",   // overridden by inline widgets in F3.
			_                     => rule.State.ToString()
		};
	}

	private static string BuildIcon( PseudoKind state )
	{
		return state switch
		{
			PseudoKind.Hover      => "mouse",
			PseudoKind.Active     => "bolt",
			PseudoKind.Focus      => "center_focus_strong",
			PseudoKind.Empty      => "unfold_less",
			PseudoKind.FirstChild => "first_page",
			PseudoKind.LastChild  => "last_page",
			PseudoKind.OnlyChild  => "looks_one",
			PseudoKind.NthChild   => "filter_9_plus",
			_                     => "category"
		};
	}
}
