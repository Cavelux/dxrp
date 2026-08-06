using System.Collections.Generic;
using Grains.RazorDesigner.Document;
using Sandbox;
using Length = Grains.RazorDesigner.Document.Length;

namespace Grains.RazorDesigner.Templates;

public static class BundledTemplateCatalog
{
	private const string LogPrefix = "[Grains.RazorDesigner]";

	private static Color Hex( string hex ) => Color.Parse( hex ) ?? Color.White;
	private static readonly Color DefaultText      = Hex( "#fafaff" );          // $default-text
	private static readonly Color DefaultTextMuted = Hex( "#C2C9DB" );          // $default-50 / $default-text-light
	private static readonly Color DefaultBg950     = Hex( "#11141D" );          // $default-950 (page background)
	private static readonly Color DefaultBorder    = new( 194f / 255f, 201f / 255f, 219f / 255f, 0.1f ); // rgba($default-50,.1)
	private static readonly Color PrimaryBlueCta   = Hex( "#3273eb" );          // Button.primary background (= --primary-blue; was #3171E6 — fixed via drift survey grd-l5ze)

	private static readonly Color Default100    = Hex( "#B5BED4" );  // $default-100 (label.eyebrow text colour)
	private static readonly Color Default200    = Hex( "#9BA7C4" );  // $default-200
	private static readonly Color Default300    = Hex( "#8291B5" );  // $default-300
	private static readonly Color Default400    = Hex( "#687AA6" );  // $default-400
	private static readonly Color Default500    = Hex( "#556691" );  // $default-500
	private static readonly Color Default800    = Hex( "#283043" );  // $default-800 ($surface-overlay)
	private static readonly Color Default900    = Hex( "#191D2A" );  // $default-900 ($surface-elevated)
	private static readonly Color PrimaryBlue   = Hex( "#3273eb" );  // $primary-blue
	private static readonly Color PrimaryGreen  = Hex( "#63d96b" );  // $primary-green
	private static readonly Color PrimaryYellow = Hex( "#ffbd32" );  // $primary-yellow (HUD ammo bar)
	private static readonly Color PopupGrey     = Hex( "#8e9ec2" );  // PopupItem text colour
	private static readonly Color ToastLeading  = Hex( "#4a9eff" );  // ToastMockup leading-icon accent
	private static readonly Color SettingsBodyBg = Hex( "#1b202c" ); // settings.html `.settings-body` interior background
	private static readonly Color White65       = new( 1f, 1f, 1f, 0.65f );  // rgba(white,.65)
	private static readonly Color White30       = new( 1f, 1f, 1f, 0.30f );  // rgba(white,.30)
	private static readonly Color White04       = new( 1f, 1f, 1f, 0.04f );  // rgba(white,.04)
	private static readonly Color White95       = new( 1f, 1f, 1f, 0.95f );  // rgba(white,.95)
	private static readonly Color White55       = new( 1f, 1f, 1f, 0.55f );  // rgba(white,.55)
	private static readonly Color Black50       = new( 0f, 0f, 0f, 0.5f );   // rgba(black,.5)
	private static readonly Color Black45       = new( 0f, 0f, 0f, 0.45f );  // rgba(black,.45) — toast drop shadow
	private static readonly Color SettingRowBg  = new( 50f / 255f, 59f / 255f, 82f / 255f, 0.2f );    // rgba(50,59,82,.20)
	private static readonly Color SidebarOverlay20 = new( 0f, 0f, 0f, 0.2f ); // rgba(0,0,0,.20) — settings.html sidebar overlay
	private static readonly Color BlueSoft18    = new( 50f / 255f, 115f / 255f, 235f / 255f, 0.18f ); // rgba($primary-blue,.18)
	private static readonly Color ToastBgTint   = new( 17f / 255f, 20f / 255f, 29f / 255f, 0.94f );   // rgba($default-950,.94)
	private static readonly Color ToastLeadTint = new( 74f / 255f, 158f / 255f, 1f, 0.10f );          // rgba(#4a9eff,.10)
	private static readonly Color PausePanelBg  = new( 25f / 255f, 29f / 255f, 42f / 255f, 0.95f );   // rgba(25,29,42,.95) — pause-modal panel (allows backdrop blur)
	private static readonly Color ScrimDark86   = new( 16f / 255f, 17f / 255f, 23f / 255f, 0.86f );   // rgba(16,17,23,.86) — modal scrim

	private const string FontHeading = "Poppins";        // $font-heading — headings, buttons, large labels
	private const string FontMono    = "JetBrains Mono"; // $font-mono — counters, shortcuts, ping/KD
	private const string FontBody    = "Inter";          // $font-default — scoreboard rows

	public static IReadOnlyList<PaletteTemplate> All()
	{
		var list = new List<PaletteTemplate>();
		list.AddRange( LayoutPrimitives() );
		list.AddRange( TypographyPrimitives() );
		list.AddRange( ButtonPrimitives() );
		list.AddRange( IconPrimitives() );
		list.AddRange( CompositeWidgets() );
		list.AddRange( ScreenScaffolds() );
		Log.Info( $"{LogPrefix} BundledTemplateCatalog: {list.Count} templates." );
		return list;
	}

	private static PaletteTemplate One( string name, string icon, ControlRecord root ) =>
		new( Name: name, IconName: icon, WrappedInContainer: false, Roots: new[] { root }, FilePath: "" );

	private static Edges Pad( float top, float right, float bottom, float left ) =>
		new( Length.Px( top ), Length.Px( right ), Length.Px( bottom ), Length.Px( left ) );

	private static IEnumerable<PaletteTemplate> LayoutPrimitives()
	{
		yield return One( "Row · gap 8", "view_column", new ControlRecord
		{
			Type = ControlType.Panel, ClassName = "row",
			Direction = FlexDirection.Row, Gap = 8f, FlexShrink = 0,
		} );
		yield return One( "Column · gap 16", "view_stream", new ControlRecord
		{
			Type = ControlType.Panel, ClassName = "column",
			Direction = FlexDirection.Column, Gap = 16f, FlexShrink = 0,
		} );
		yield return One( "Row · tight (gap 4)", "view_column", new ControlRecord
		{
			Type = ControlType.Panel, ClassName = "row-tight",
			Direction = FlexDirection.Row, Gap = 4f, FlexShrink = 0,
		} );
		yield return One( "Section · gap 32", "view_agenda", new ControlRecord
		{
			Type = ControlType.Panel, ClassName = "section",
			Direction = FlexDirection.Column, Gap = 32f, FlexShrink = 0,
		} );
		yield return One( "Separator · horizontal", "horizontal_rule", new ControlRecord
		{
			Type = ControlType.Panel, ClassName = "separator",
			Height = Length.Px( 1 ),
			OverrideBackground = true, BackgroundColor = DefaultBorder,
			OverrideConstraints = true,
			Margin = new Edges( Length.Px( 4 ), Length.Px( 0 ), Length.Px( 4 ), Length.Px( 0 ) ),
		} );
		yield return One( "Card shell", "crop_square", new ControlRecord
		{
			Type = ControlType.Panel, ClassName = "card",
			Direction = FlexDirection.Column, Gap = 12f,
			Padding = Edges.Uniform( Length.Px( 16 ) ),
			OverrideBackground = true, BackgroundColor = DefaultBg950,
			OverrideBorder = true,
			BorderWidth = Length.Px( 1 ), BorderColor = DefaultBorder, BorderRadius = Length.Px( 6 ),
			OverrideInteraction = true, Overflow = OverflowKind.Hidden,
		} );
	}

	private static IEnumerable<PaletteTemplate> TypographyPrimitives()
	{
		yield return One( "Label · eyebrow (10px)", "text_fields", new ControlRecord
		{
			Type = ControlType.Label, ClassName = "eyebrow", Content = "EYEBROW",
			OverrideTypography = true,
			FontFamily = FontHeading, FontSize = Length.Px( 10 ), FontWeight = 700,
			Color = Default100, TextTransform = TextTransformKind.Uppercase,
			LetterSpacing = Length.Em( 0.1f ),
		} );
		yield return One( "Label · small (12px muted)", "text_fields", new ControlRecord
		{
			Type = ControlType.Label, ClassName = "small", Content = "Small label",
			OverrideTypography = true,
			FontSize = Length.Px( 12 ), FontWeight = 600, Color = DefaultTextMuted,
		} );
		yield return One( "Label · medium (16px)", "text_fields", new ControlRecord
		{
			Type = ControlType.Label, ClassName = "medium", Content = "Medium label",
			OverrideTypography = true,
			FontFamily = FontHeading, FontSize = Length.Px( 16 ), FontWeight = 600, Color = DefaultText,
		} );
		yield return One( "Label · large action (16px caps)", "text_fields", new ControlRecord
		{
			Type = ControlType.Label, ClassName = "large", Content = "LARGE ACTION",
			OverrideTypography = true,
			FontFamily = FontHeading, FontSize = Length.Px( 16 ), FontWeight = 700,
			Color = DefaultText, TextTransform = TextTransformKind.Uppercase,
			LetterSpacing = Length.Em( 0.02f ),
		} );
		yield return One( "Label · heading (16px)", "title", new ControlRecord
		{
			Type = ControlType.Label, ClassName = "heading", Content = "Heading",
			OverrideTypography = true,
			FontFamily = FontHeading, FontSize = Length.Px( 16 ), FontWeight = 700, Color = DefaultTextMuted,
		} );
	}

	private static IEnumerable<PaletteTemplate> ButtonPrimitives()
	{
		// hover/active brightness filters NOT baked — Appearance has no pseudo-class support yet (bead grd-ojc9).
		yield return One( "Button · primary (CTA)", "smart_button", new ControlRecord
		{
			Type = ControlType.Button, ClassName = "primary", Content = "Button",
			Align = AlignItems.Center, Gap = 8f,
			Padding = new Edges( Length.Px( 8 ), Length.Px( 18 ), Length.Px( 8 ), Length.Px( 18 ) ),
			OverrideTypography = true,
			FontFamily = FontHeading, FontSize = Length.Px( 14 ), FontWeight = 700, Color = Color.White,
			OverrideBackground = true, BackgroundColor = PrimaryBlueCta,
			OverrideBorder = true,
			BorderWidth = Length.Px( 3 ), BorderColor = Color.Transparent, BorderRadius = Length.Px( 8 ),
		} );
		yield return One( "Button · default", "smart_button", new ControlRecord
		{
			Type = ControlType.Button, ClassName = "default", Content = "Button",
			Align = AlignItems.Center, Gap = 8f,
			Padding = new Edges( Length.Px( 8 ), Length.Px( 16 ), Length.Px( 8 ), Length.Px( 16 ) ),
			OverrideTypography = true,
			FontSize = Length.Px( 14 ), Color = DefaultTextMuted,
			OverrideBackground = true, BackgroundColor = Color.Transparent,
			OverrideBorder = true,
			BorderWidth = Length.Px( 1 ), BorderColor = Color.Transparent, BorderRadius = Length.Px( 6 ),
		} );
		yield return One( "Button · ghost", "smart_button", new ControlRecord
		{
			Type = ControlType.Button, ClassName = "ghost", Content = "Cancel",
			Align = AlignItems.Center, Gap = 8f,
			Padding = new Edges( Length.Px( 8 ), Length.Px( 16 ), Length.Px( 8 ), Length.Px( 16 ) ),
			OverrideTypography = true,
			FontSize = Length.Px( 14 ), Color = DefaultTextMuted,
			OverrideBackground = true, BackgroundColor = Color.Transparent,
			OverrideBorder = true,
			BorderWidth = Length.Px( 1 ), BorderColor = DefaultBorder, BorderRadius = Length.Px( 6 ),
		} );
	}

	private static IEnumerable<PaletteTemplate> IconPrimitives()
	{
		yield return One( "Icon · medium (16px)", "favorite", new ControlRecord
		{
			Type = ControlType.IconPanel, ClassName = "icon-medium", IconName = "favorite",
			OverrideTypography = true, FontSize = Length.Px( 16 ),
		} );
		yield return One( "Icon · small (12px)", "favorite_border", new ControlRecord
		{
			Type = ControlType.IconPanel, ClassName = "icon-small", IconName = "favorite",
			OverrideTypography = true, FontSize = Length.Px( 12 ),
		} );
		yield return One( "Icon · eyebrow (10px)", "favorite_border", new ControlRecord
		{
			Type = ControlType.IconPanel, ClassName = "icon-eyebrow", IconName = "favorite",
			OverrideTypography = true, FontSize = Length.Px( 10 ), Color = Default100,
		} );
	}

	private static IEnumerable<PaletteTemplate> CompositeWidgets()
	{
		yield return One( "Badge · status pill", "label", new ControlRecord
		{
			Type = ControlType.Panel, ClassName = "badge",
			Direction = FlexDirection.Row, Align = AlignItems.Center, Gap = 4f, FlexShrink = 0,
			Padding = Pad( 3, 6, 3, 6 ),
			OverrideBackground = true, BackgroundColor = Default800,
			OverrideBorder = true, BorderRadius = Length.Px( 4 ),
			Children =
			{
				new ControlRecord
				{
					Type = ControlType.IconPanel, IconName = "schedule",
					OverrideTypography = true, FontSize = Length.Px( 10 ), Color = DefaultTextMuted,
				},
				new ControlRecord
				{
					Type = ControlType.Label, Content = "BADGE",
					OverrideTypography = true, FontFamily = FontHeading, FontSize = Length.Px( 10 ),
					FontWeight = 700, Color = DefaultTextMuted,
					TextTransform = TextTransformKind.Uppercase, LetterSpacing = Length.Em( 0.1f ),
				},
				new ControlRecord
				{
					Type = ControlType.Label, ClassName = "number", Content = "3",
					Padding = Pad( 1, 4, 1, 4 ),
					OverrideTypography = true, Color = Color.White,
					OverrideBackground = true, BackgroundColor = Black50,
					OverrideBorder = true, BorderRadius = Length.Px( 2 ),
					OverrideConstraints = true, Margin = Pad( 0, 0, 0, 2 ),
				},
			},
		} );

		// MenuOption — context-menu row: 32px tall, icon slot + bold text + right-aligned shortcut.
		yield return One( "Menu option row", "menu", new ControlRecord
		{
			Type = ControlType.Panel, ClassName = "menu-option",
			Direction = FlexDirection.Row, Align = AlignItems.Center, Gap = 4f, FlexShrink = 0,
			Height = Length.Px( 32 ), Padding = Pad( 0, 8, 0, 8 ),
			Children =
			{
				new ControlRecord
				{
					Type = ControlType.Panel, ClassName = "icon-slot",
					Align = AlignItems.Center, Justify = JustifyContent.Center, FlexShrink = 0,
					Padding = Edges.Uniform( Length.Px( 8 ) ),
					Children =
					{
						new ControlRecord
						{
							Type = ControlType.IconPanel, IconName = "content_copy",
							OverrideTypography = true, Color = White65,
						},
					},
				},
				new ControlRecord
				{
					Type = ControlType.Label, ClassName = "text", Content = "Menu item", FlexShrink = 0,
					Padding = Edges.Uniform( Length.Px( 8 ) ),
					OverrideTypography = true, FontFamily = FontHeading, FontWeight = 700, Color = White65,
				},
				new ControlRecord
				{
					Type = ControlType.Label, ClassName = "meta", Content = "Ctrl+K", FlexGrow = 1,
					Padding = Edges.Uniform( Length.Px( 8 ) ),
					OverrideTypography = true, FontFamily = FontMono, FontWeight = 500,
					TextAlign = TextAlignment.Right, Color = White30,
				},
			},
		} );

		// PauseItem — pause-menu / list row: 22px leading icon, name+hint column, monospace shortcut chip.
		yield return One( "Pause menu item", "pause_circle", new ControlRecord
		{
			Type = ControlType.Panel, ClassName = "pause-item",
			Direction = FlexDirection.Row, Align = AlignItems.Center, Gap = 14f,
			Padding = Pad( 12, 16, 12, 16 ),
			OverrideBorder = true, BorderRadius = Length.Px( 6 ), BorderWidth = Length.Px( 1 ), BorderColor = Color.Transparent,
			Children =
			{
				new ControlRecord
				{
					Type = ControlType.IconPanel, IconName = "play_arrow",
					OverrideTypography = true, FontSize = Length.Px( 22 ), Color = Default200,
				},
				new ControlRecord
				{
					Type = ControlType.Panel, ClassName = "label-col",
					Direction = FlexDirection.Column, Gap = 2f, FlexGrow = 1,
					Children =
					{
						new ControlRecord
						{
							Type = ControlType.Label, ClassName = "name", Content = "Resume",
							OverrideTypography = true, FontFamily = FontHeading, FontWeight = 600,
							FontSize = Length.Px( 15 ), Color = DefaultText,
						},
						new ControlRecord
						{
							Type = ControlType.Label, ClassName = "hint", Content = "Return to the game",
							OverrideTypography = true, FontSize = Length.Px( 12 ), Color = Default400,
						},
					},
				},
				new ControlRecord
				{
					Type = ControlType.Label, ClassName = "shortcut", Content = "ESC", FlexShrink = 0,
					Padding = Pad( 2, 8, 2, 8 ),
					OverrideTypography = true, FontFamily = FontMono, FontSize = Length.Px( 11 ), Color = Default500,
					OverrideBackground = true, BackgroundColor = White04,
					OverrideBorder = true, BorderRadius = Length.Px( 4 ),
				},
			},
		} );

		// SettingRow — settings form row: tinted bar, 340px label column (name+hint), 350px control slot.
		yield return One( "Setting row", "tune", new ControlRecord
		{
			Type = ControlType.Panel, ClassName = "setting-row",
			Direction = FlexDirection.Row, Align = AlignItems.Center, FlexShrink = 0,
			Padding = Pad( 8, 12, 8, 12 ),
			OverrideBackground = true, BackgroundColor = SettingRowBg,
			OverrideBorder = true, BorderRadius = Length.Px( 10 ),
			Children =
			{
				new ControlRecord
				{
					Type = ControlType.Panel, ClassName = "row-label",
					Direction = FlexDirection.Column, Gap = 2f, FlexGrow = 0, FlexShrink = 0,
					Width = Length.Px( 340 ),
					OverrideConstraints = true, Margin = Pad( 0, 0, 0, 24 ),
					Children =
					{
						new ControlRecord
						{
							Type = ControlType.Label, ClassName = "name", Content = "Setting name",
							OverrideTypography = true, FontFamily = FontHeading, FontWeight = 600, Color = DefaultText,
						},
						new ControlRecord
						{
							Type = ControlType.Label, ClassName = "hint", Content = "What this setting does",
							OverrideTypography = true, FontSize = Length.Px( 12 ), Color = Default400,
						},
					},
				},
				new ControlRecord
				{
					Type = ControlType.Panel, ClassName = "control",
					Align = AlignItems.Center, Justify = JustifyContent.End, Gap = 8f,
					FlexGrow = 0, FlexShrink = 0, Width = Length.Px( 350 ), Height = Length.Px( 32 ),
					OverrideConstraints = true, MaxWidth = Length.Px( 350 ),
				},
			},
		} );

		yield return One( "Checkbox row", "check_box_outline_blank", new ControlRecord
		{
			Type = ControlType.Panel, ClassName = "checkbox",
			Direction = FlexDirection.Row, Align = AlignItems.Center, Gap = 8f, FlexShrink = 0,
			Children =
			{
				new ControlRecord
				{
					Type = ControlType.Panel, ClassName = "checkmark",
					Width = Length.Px( 22 ), Height = Length.Px( 22 ), FlexShrink = 0,
					OverrideBackground = true, BackgroundColor = Color.Transparent,
					OverrideBorder = true,
					BorderWidth = Length.Px( 3 ), BorderColor = PrimaryBlue, BorderRadius = Length.Px( 4 ),
				},
				new ControlRecord
				{
					Type = ControlType.Label, ClassName = "label", Content = "Setting label",
					OverrideTypography = true, Color = DefaultTextMuted,
				},
			},
		} );

		// StatRow — HUD stat bar: 22px icon, fill bar inside a recessed track, monospace value cell.
		yield return One( "Stat bar row", "monitor_heart", new ControlRecord
		{
			Type = ControlType.Panel, ClassName = "stat-row",
			Direction = FlexDirection.Row, Align = AlignItems.Center, Gap = 10f,
			Children =
			{
				new ControlRecord
				{
					Type = ControlType.IconPanel, IconName = "favorite", FlexShrink = 0,
					OverrideTypography = true, FontSize = Length.Px( 22 ), Color = PrimaryGreen,
				},
				new ControlRecord
				{
					Type = ControlType.Panel, ClassName = "vals",
					Direction = FlexDirection.Column, FlexGrow = 1, Gap = 4f,
					Children =
					{
						new ControlRecord
						{
							Type = ControlType.Panel, ClassName = "bar", Height = Length.Px( 8 ),
							OverrideBackground = true, BackgroundColor = Default900,
							OverrideBorder = true, BorderRadius = Length.Px( 4 ),
							OverrideInteraction = true, Overflow = OverflowKind.Hidden,
							Children =
							{
								new ControlRecord
								{
									Type = ControlType.Panel, ClassName = "fill",
									Width = Length.Percent( 72 ), Height = Length.Percent( 100 ),
									OverrideBackground = true, BackgroundColor = PrimaryGreen,
								},
							},
						},
					},
				},
				new ControlRecord
				{
					Type = ControlType.Label, ClassName = "num", Content = "100 HP",
					OverrideTypography = true, FontFamily = FontMono, FontSize = Length.Px( 13 ), FontWeight = 700,
					TextAlign = TextAlignment.Right, Color = DefaultText,
					OverrideConstraints = true, MinWidth = Length.Px( 50 ),
				},
			},
		} );

		// ScoreRow — scoreboard row: name (grows) + monospace K/D + monospace ping.
		yield return One( "Scoreboard row", "leaderboard", new ControlRecord
		{
			Type = ControlType.Panel, ClassName = "score-row",
			Direction = FlexDirection.Row, Align = AlignItems.Center, Gap = 8f,
			Padding = Pad( 6, 8, 6, 8 ),
			OverrideBorder = true, BorderRadius = Length.Px( 4 ),
			Children =
			{
				new ControlRecord
				{
					Type = ControlType.Label, ClassName = "name", Content = "PlayerName", FlexGrow = 1,
					OverrideTypography = true, FontFamily = FontBody, FontSize = Length.Px( 12 ),
					FontWeight = 600, Color = DefaultText,
				},
				new ControlRecord
				{
					Type = ControlType.Label, ClassName = "kd", Content = "12 / 4",
					OverrideTypography = true, FontFamily = FontMono, FontSize = Length.Px( 11 ),
					TextAlign = TextAlignment.Right, Color = Default300,
					OverrideConstraints = true, MinWidth = Length.Px( 36 ),
				},
				new ControlRecord
				{
					Type = ControlType.Label, ClassName = "ping", Content = "24ms",
					OverrideTypography = true, FontFamily = FontMono, FontSize = Length.Px( 10 ),
					TextAlign = TextAlignment.Right, Color = Default400,
					OverrideConstraints = true, MinWidth = Length.Px( 30 ),
				},
			},
		} );

		// SectionHeader — showcase section heading: 22px Poppins title + monospace caption.
		yield return One( "Section header", "title", new ControlRecord
		{
			Type = ControlType.Panel, ClassName = "section-header",
			Direction = FlexDirection.Row, Align = AlignItems.Center, Gap = 12f,
			Children =
			{
				new ControlRecord
				{
					Type = ControlType.Label, ClassName = "title", Content = "Section",
					OverrideTypography = true, FontFamily = FontHeading, FontWeight = 700,
					FontSize = Length.Px( 22 ), Color = DefaultText,
				},
				new ControlRecord
				{
					Type = ControlType.Label, ClassName = "caption", Content = "optional caption",
					OverrideTypography = true, FontFamily = FontMono, FontWeight = 500,
					FontSize = Length.Px( 13 ), Color = Default500,
				},
			},
		} );

		// SettingsNavItem — sidebar nav row: icon + name (grows) + soft pill badge. Resting (inactive) look.
		yield return One( "Settings nav item", "chevron_right", new ControlRecord
		{
			Type = ControlType.Panel, ClassName = "settings-nav-item",
			Direction = FlexDirection.Row, Align = AlignItems.Center, Gap = 10f, FlexShrink = 0,
			Padding = Pad( 8, 10, 8, 10 ),
			OverrideBorder = true, BorderRadius = Length.Px( 6 ), BorderWidth = Length.Px( 1 ), BorderColor = Color.Transparent,
			Children =
			{
				new ControlRecord
				{
					Type = ControlType.IconPanel, IconName = "settings",
					OverrideTypography = true, Color = Default200,
				},
				new ControlRecord
				{
					Type = ControlType.Label, ClassName = "name", Content = "Gameplay", FlexGrow = 1,
					OverrideTypography = true, FontFamily = FontHeading, FontWeight = 600,
					FontSize = Length.Px( 14 ), Color = Default200,
				},
				new ControlRecord
				{
					Type = ControlType.Label, ClassName = "badge-soft", Content = "NEW",
					Padding = Pad( 1, 6, 1, 6 ),
					OverrideTypography = true, FontFamily = FontMono, FontSize = Length.Px( 10 ), Color = PrimaryBlue,
					OverrideBackground = true, BackgroundColor = BlueSoft18,
					OverrideBorder = true, BorderRadius = Length.Px( 999 ),
				},
			},
		} );

		yield return One( "DropDown control", "arrow_drop_down", new ControlRecord
		{
			Type = ControlType.Panel, ClassName = "dropdown",
			Direction = FlexDirection.Row, Align = AlignItems.Center,
			Justify = JustifyContent.SpaceBetween, Gap = 4f, FlexShrink = 0,
			Padding = new Edges( Length.Px( 6 ), Length.Px( 12 ), Length.Px( 6 ), Length.Px( 12 ) ),
			OverrideConstraints = true, MinWidth = Length.Px( 140 ), MinHeight = Length.Px( 32 ),
			OverrideBackground = true, BackgroundColor = Black45,
			OverrideBorder = true,
			BorderWidth = Length.Px( 1 ), BorderColor = DefaultBorder, BorderRadius = Length.Px( 8 ),
			Children =
			{
				new ControlRecord
				{
					Type = ControlType.Label, ClassName = "value", Content = "Option",
					OverrideTypography = true, Color = DefaultText,
				},
				new ControlRecord
				{
					Type = ControlType.IconPanel, ClassName = "chevron", IconName = "expand_more",
					FlexShrink = 0,
					OverrideTypography = true, FontSize = Length.Px( 16 ), Color = DefaultText,
				},
			},
		} );

		// PopupItem — dropdown / popup option: 18px icon + semibold label. Resting (unselected) look.
		yield return One( "Popup option", "list", new ControlRecord
		{
			Type = ControlType.Panel, ClassName = "popup-item",
			Direction = FlexDirection.Row, Align = AlignItems.Center, Gap = 8f, FlexShrink = 0,
			Padding = Pad( 8, 16, 8, 16 ),
			Children =
			{
				new ControlRecord
				{
					Type = ControlType.IconPanel, IconName = "check_circle",
					OverrideTypography = true, FontSize = Length.Px( 18 ), Color = PopupGrey,
				},
				new ControlRecord
				{
					Type = ControlType.Label, Content = "Option",
					OverrideTypography = true, FontFamily = FontHeading, FontWeight = 600,
					FontSize = Length.Px( 14 ), Color = PopupGrey,
				},
			},
		} );

		// ToastMockup — toast card: tinted leading-icon disc + title/subtitle column + round close affordance.
		yield return One( "Toast notification", "notifications", new ControlRecord
		{
			Type = ControlType.Panel, ClassName = "toast",
			Direction = FlexDirection.Row, Align = AlignItems.Center, Gap = 12f, FlexShrink = 0,
			Padding = Pad( 16, 20, 16, 20 ),
			OverrideConstraints = true, MinWidth = Length.Px( 320 ), MaxWidth = Length.Px( 420 ),
			OverrideBackground = true, BackgroundColor = ToastBgTint,
			OverrideBorder = true, BorderWidth = Length.Px( 1 ), BorderColor = DefaultBorder, BorderRadius = Length.Px( 14 ),
			OverrideEffects = true, BoxShadowY = Length.Px( 12 ), BoxShadowBlur = Length.Px( 32 ), BoxShadowColor = Black45,
			Children =
			{
				new ControlRecord
				{
					Type = ControlType.IconPanel, ClassName = "leading", IconName = "notifications", FlexShrink = 0,
					Padding = Edges.Uniform( Length.Px( 6 ) ),
					OverrideTypography = true, FontSize = Length.Px( 24 ), Color = ToastLeading,
					OverrideBackground = true, BackgroundColor = ToastLeadTint,
					OverrideBorder = true, BorderRadius = Length.Px( 999 ),
				},
				new ControlRecord
				{
					Type = ControlType.Panel, ClassName = "content",
					Direction = FlexDirection.Column, FlexGrow = 1, Gap = 2f,
					Children =
					{
						new ControlRecord
						{
							Type = ControlType.Label, ClassName = "title", Content = "Notification",
							OverrideTypography = true, FontFamily = FontHeading, FontSize = Length.Px( 14 ),
							FontWeight = 600, Color = White95,
						},
						new ControlRecord
						{
							Type = ControlType.Label, ClassName = "subtitle", Content = "Subtitle text",
							OverrideTypography = true, FontSize = Length.Px( 12 ), Color = White55,
						},
					},
				},
				new ControlRecord
				{
					Type = ControlType.Panel, ClassName = "close-x",
					Align = AlignItems.Center, Justify = JustifyContent.Center, FlexShrink = 0,
					Width = Length.Px( 28 ), Height = Length.Px( 28 ),
					OverrideBorder = true, BorderRadius = Length.Px( 999 ),
					Children =
					{
						new ControlRecord
						{
							Type = ControlType.IconPanel, IconName = "close",
							OverrideTypography = true, Color = Default300,
						},
					},
				},
			},
		} );
	}

	private static IEnumerable<PaletteTemplate> ScreenScaffolds()
	{
		// --- Settings page shell: 400px sidebar ($default-900) + 50px-padded body ---
		yield return One( "Settings page shell", "settings_applications", new ControlRecord
		{
			Type = ControlType.Panel, ClassName = "settings-page",
			Direction = FlexDirection.Row, Width = Length.Percent( 100 ), Height = Length.Percent( 100 ),
			Children =
			{
				new ControlRecord
				{
					Type = ControlType.Panel, ClassName = "sidebar",
					Direction = FlexDirection.Column, Gap = 2f, FlexShrink = 0, Width = Length.Px( 220 ),
					Padding = Pad( 16, 12, 16, 12 ),
					OverrideBackground = true, BackgroundColor = SidebarOverlay20,
					Children =
					{
						SettingsNavRow( "settings", "Gameplay" ),
						SettingsNavRow( "sports_esports", "Video" ),
						SettingsNavRow( "tv", "Audio" ),
						SettingsNavRow( "volume_up", "Controls" ),
					},
				},
				new ControlRecord
				{
					Type = ControlType.Panel, ClassName = "pagebody",
					Direction = FlexDirection.Column, Gap = 8f, FlexGrow = 1,
					Padding = Pad( 16, 24, 16, 24 ),
					OverrideBackground = true, BackgroundColor = SettingsBodyBg,
					Children =
					{
						new ControlRecord
						{
							Type = ControlType.Label, ClassName = "page-title", Content = "Gameplay",
							OverrideTypography = true, FontFamily = FontHeading, FontWeight = 700,
							FontSize = Length.Px( 22 ), Color = DefaultText,
						},
						PageSettingRow(),
						PageSettingRow(),
						PageSettingRow(),
					},
				},
			},
		} );

		// --- Card grid browser: search bar + wrapping grid of content cards ---
		yield return One( "Card grid browser", "grid_view", new ControlRecord
		{
			Type = ControlType.Panel, ClassName = "browser",
			Direction = FlexDirection.Column, Gap = 24f, Width = Length.Percent( 100 ),
			Padding = Edges.Uniform( Length.Px( 50 ) ),
			Children =
			{
				new ControlRecord
				{
					Type = ControlType.Panel, ClassName = "searchbar",
					Direction = FlexDirection.Row, Align = AlignItems.Center, Gap = 8f, FlexShrink = 0,
					Height = Length.Px( 40 ), Padding = Pad( 0, 16, 0, 16 ),
					OverrideBackground = true, BackgroundColor = Default800,
					OverrideBorder = true, BorderRadius = Length.Px( 8 ),
					Children =
					{
						new ControlRecord
						{
							Type = ControlType.IconPanel, IconName = "search",
							OverrideTypography = true, Color = Default300,
						},
						new ControlRecord
						{
							Type = ControlType.TextEntry, ClassName = "search-input",
							Placeholder = "Search…", FlexGrow = 1,
						},
					},
				},
				new ControlRecord
				{
					Type = ControlType.Panel, ClassName = "card-grid",
					Direction = FlexDirection.Row, Wrap = FlexWrap.Wrap, Gap = 16f,
					Children = { CardCell(), CardCell(), CardCell(), CardCell() },
				},
			},
		} );

		yield return One( "HUD cluster", "monitor_heart", new ControlRecord
		{
			Type = ControlType.Panel, ClassName = "hud",
			Width = Length.Percent( 100 ), Height = Length.Percent( 100 ),
			Children =
			{
				new ControlRecord
				{
					Type = ControlType.Panel, ClassName = "hud-bottom-left",
					OverrideConstraints = true, Position = PositionKind.Absolute,
					Left = Length.Px( 24 ), Bottom = Length.Px( 24 ),
					Direction = FlexDirection.Column, Gap = 8f, Width = Length.Px( 280 ),
					Children =
					{
						HudStatRow( "favorite", PrimaryGreen, 72f, "100 HP" ),
						HudStatRow( "bolt", PrimaryYellow, 40f, "30 / 90" ),
					},
				},
				new ControlRecord
				{
					Type = ControlType.Panel, ClassName = "hud-top-right",
					OverrideConstraints = true, Position = PositionKind.Absolute,
					Right = Length.Px( 24 ), Top = Length.Px( 24 ),
					Direction = FlexDirection.Row, Gap = 8f,
					Children = { BadgePill( "WAVE 3" ), BadgePill( "01:24" ) },
				},
			},
		} );

		// --- Pause menu column: dim backdrop + centered panel with a column of list rows ---
		yield return One( "Pause menu column", "pause_circle", new ControlRecord
		{
			Type = ControlType.Panel, ClassName = "pause-menu",
			Width = Length.Percent( 100 ), Height = Length.Percent( 100 ),
			Align = AlignItems.Center, Justify = JustifyContent.Center,
			OverrideBackground = true, BackgroundColor = ScrimDark86,
			Children =
			{
				new ControlRecord
				{
					Type = ControlType.Panel, ClassName = "pause-panel",
					Direction = FlexDirection.Column, Gap = 4f, Width = Length.Px( 480 ),
					Padding = Edges.Uniform( Length.Px( 12 ) ),
					OverrideBackground = true, BackgroundColor = PausePanelBg,
					OverrideBorder = true,
					BorderWidth = Length.Px( 1 ), BorderColor = DefaultBorder, BorderRadius = Length.Px( 16 ),
					Children =
					{
						new ControlRecord
						{
							Type = ControlType.Label, ClassName = "pause-title", Content = "Paused",
							OverrideTypography = true, FontFamily = FontHeading, FontWeight = 700,
							FontSize = Length.Px( 18 ), Color = DefaultText,
						},
						PauseRow( "play_arrow", "Resume", "Return to the game", "ESC" ),
						PauseRow( "tune", "Settings", "Audio, video, controls", "F1" ),
						PauseRow( "group_add", "Invite Friends", "Bring friends into your game", "F2" ),
						PauseRow( "logout", "Quit to Menu", "Leave to the main menu", "Q" ),
					},
				},
			},
		} );
	}

	private static ControlRecord SettingsNavRow( string icon, string name ) => new()
	{
		Type = ControlType.Panel, ClassName = "settings-nav-item",
		Direction = FlexDirection.Row, Align = AlignItems.Center, Gap = 10f, FlexShrink = 0,
		Padding = Pad( 8, 10, 8, 10 ),
		OverrideBorder = true, BorderRadius = Length.Px( 6 ), BorderWidth = Length.Px( 1 ), BorderColor = Color.Transparent,
		Children =
		{
			new ControlRecord
			{
				Type = ControlType.IconPanel, IconName = icon,
				OverrideTypography = true, Color = Default200,
			},
			new ControlRecord
			{
				Type = ControlType.Label, ClassName = "name", Content = name, FlexGrow = 1,
				OverrideTypography = true, FontFamily = FontHeading, FontWeight = 600,
				FontSize = Length.Px( 14 ), Color = Default200,
			},
		},
	};

	private static ControlRecord PageSettingRow() => new()
	{
		Type = ControlType.Panel, ClassName = "setting-row",
		Direction = FlexDirection.Row, Align = AlignItems.Center, FlexShrink = 0,
		Padding = Pad( 8, 12, 8, 12 ),
		OverrideBackground = true, BackgroundColor = SettingRowBg,
		OverrideBorder = true, BorderRadius = Length.Px( 10 ),
		Children =
		{
			new ControlRecord
			{
				Type = ControlType.Panel, ClassName = "row-label",
				Direction = FlexDirection.Column, Gap = 2f, FlexGrow = 0, FlexShrink = 0,
				Width = Length.Px( 340 ),
				OverrideConstraints = true, Margin = Pad( 0, 0, 0, 16 ),
				Children =
				{
					new ControlRecord
					{
						Type = ControlType.Label, ClassName = "name", Content = "Setting name",
						OverrideTypography = true, FontFamily = FontHeading, FontWeight = 600, Color = DefaultText,
					},
					new ControlRecord
					{
						Type = ControlType.Label, ClassName = "hint", Content = "What this setting does",
						OverrideTypography = true, FontSize = Length.Px( 12 ), Color = Default400,
					},
				},
			},
			new ControlRecord
			{
				Type = ControlType.Panel, ClassName = "control",
				Direction = FlexDirection.Row, Align = AlignItems.Center, Justify = JustifyContent.End, Gap = 8f,
				FlexGrow = 0, FlexShrink = 0, Width = Length.Px( 350 ), Height = Length.Px( 32 ),
				OverrideConstraints = true, MaxWidth = Length.Px( 350 ),
			},
		},
	};

	private static ControlRecord CardCell() => new()
	{
		Type = ControlType.Panel, ClassName = "card",
		Direction = FlexDirection.Column, Gap = 12f, Width = Length.Px( 240 ),
		Padding = Edges.Uniform( Length.Px( 16 ) ),
		OverrideBackground = true, BackgroundColor = DefaultBg950,
		OverrideBorder = true,
		BorderWidth = Length.Px( 1 ), BorderColor = DefaultBorder, BorderRadius = Length.Px( 6 ),
		Children =
		{
			new ControlRecord
			{
				Type = ControlType.Image, ClassName = "thumb", Height = Length.Px( 120 ),
				OverrideBorder = true, BorderRadius = Length.Px( 4 ),
			},
			new ControlRecord
			{
				Type = ControlType.Label, ClassName = "heading", Content = "Card title",
				OverrideTypography = true, FontFamily = FontHeading, FontWeight = 700,
				FontSize = Length.Px( 16 ), Color = DefaultText,
			},
			new ControlRecord
			{
				Type = ControlType.Label, ClassName = "small", Content = "Supporting copy goes here.",
				OverrideTypography = true, FontSize = Length.Px( 12 ), FontWeight = 600, Color = DefaultTextMuted,
			},
		},
	};

	private static ControlRecord HudStatRow( string icon, Color accent, float fillPct, string valueText ) => new()
	{
		Type = ControlType.Panel, ClassName = "stat-row",
		Direction = FlexDirection.Row, Align = AlignItems.Center, Gap = 10f,
		Children =
		{
			new ControlRecord
			{
				Type = ControlType.IconPanel, IconName = icon, FlexShrink = 0,
				OverrideTypography = true, FontSize = Length.Px( 22 ), Color = accent,
			},
			new ControlRecord
			{
				Type = ControlType.Panel, ClassName = "vals",
				Direction = FlexDirection.Column, FlexGrow = 1, Gap = 4f,
				Children =
				{
					new ControlRecord
					{
						Type = ControlType.Panel, ClassName = "bar", Height = Length.Px( 8 ),
						OverrideBackground = true, BackgroundColor = Default900,
						OverrideBorder = true, BorderRadius = Length.Px( 4 ),
						OverrideInteraction = true, Overflow = OverflowKind.Hidden,
						Children =
						{
							new ControlRecord
							{
								Type = ControlType.Panel, ClassName = "fill",
								Width = Length.Percent( fillPct ), Height = Length.Percent( 100 ),
								OverrideBackground = true, BackgroundColor = accent,
							},
						},
					},
				},
			},
			new ControlRecord
			{
				Type = ControlType.Label, ClassName = "num", Content = valueText,
				OverrideTypography = true, FontFamily = FontMono, FontSize = Length.Px( 13 ), FontWeight = 700,
				TextAlign = TextAlignment.Right, Color = DefaultText,
				OverrideConstraints = true, MinWidth = Length.Px( 50 ),
			},
		},
	};

	private static ControlRecord BadgePill( string text ) => new()
	{
		Type = ControlType.Panel, ClassName = "badge",
		Direction = FlexDirection.Row, Align = AlignItems.Center, Gap = 4f, FlexShrink = 0,
		Padding = Pad( 3, 6, 2, 6 ),
		OverrideBackground = true, BackgroundColor = Default800,
		OverrideBorder = true, BorderRadius = Length.Px( 4 ),
		Children =
		{
			new ControlRecord
			{
				Type = ControlType.Label, Content = text,
				OverrideTypography = true, FontFamily = FontHeading, FontSize = Length.Px( 10 ),
				FontWeight = 700, Color = DefaultTextMuted,
				TextTransform = TextTransformKind.Uppercase, LetterSpacing = Length.Em( 0.1f ),
			},
		},
	};

	private static ControlRecord PauseRow( string icon, string name, string hint, string shortcut ) => new()
	{
		Type = ControlType.Panel, ClassName = "pause-item",
		Direction = FlexDirection.Row, Align = AlignItems.Center, Gap = 14f,
		Padding = Pad( 12, 16, 12, 16 ),
		OverrideBorder = true, BorderRadius = Length.Px( 6 ), BorderWidth = Length.Px( 1 ), BorderColor = Color.Transparent,
		Children =
		{
			new ControlRecord
			{
				Type = ControlType.IconPanel, IconName = icon,
				OverrideTypography = true, FontSize = Length.Px( 22 ), Color = Default200,
			},
			new ControlRecord
			{
				Type = ControlType.Panel, ClassName = "label-col",
				Direction = FlexDirection.Column, Gap = 2f, FlexGrow = 1,
				Children =
				{
					new ControlRecord
					{
						Type = ControlType.Label, ClassName = "name", Content = name,
						OverrideTypography = true, FontFamily = FontHeading, FontWeight = 600,
						FontSize = Length.Px( 15 ), Color = DefaultText,
					},
					new ControlRecord
					{
						Type = ControlType.Label, ClassName = "hint", Content = hint,
						OverrideTypography = true, FontSize = Length.Px( 12 ), Color = Default400,
					},
				},
			},
			new ControlRecord
			{
				Type = ControlType.Label, ClassName = "shortcut", Content = shortcut, FlexShrink = 0,
				Padding = Pad( 2, 8, 2, 8 ),
				OverrideTypography = true, FontFamily = FontMono, FontSize = Length.Px( 11 ), Color = Default500,
				OverrideBackground = true, BackgroundColor = White04,
				OverrideBorder = true, BorderRadius = Length.Px( 4 ),
			},
		},
	};
}
