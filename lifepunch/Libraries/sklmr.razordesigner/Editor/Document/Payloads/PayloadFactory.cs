using System.Diagnostics;

namespace Grains.RazorDesigner.Document;

public static class PayloadFactory
{
    public static Payload Default( ControlType kind ) => kind switch
    {
        ControlType.Panel        => new PanelPayload(),
        ControlType.SplitContainer => new SplitContainerPayload(),
        ControlType.Label        => new LabelPayload(),
        ControlType.Image        => new ImagePayload(),
        ControlType.IconPanel    => new IconPanelPayload(),
        ControlType.Button       => new ButtonPayload(),
        ControlType.ButtonGroup  => new ButtonGroupPayload(),
        ControlType.Checkbox     => new CheckboxPayload(),
        ControlType.TextEntry    => new TextEntryPayload(),
        ControlType.DropDown     => new DropDownPayload(),
        ControlType.Form         => new FormPayload(),
        ControlType.Field        => new FieldPayload(),
        ControlType.FieldControl => new FieldControlPayload(),
        _                        => throw new UnreachableException( $"PayloadFactory.Default: no payload defined for ControlType.{kind}" ),
    };

    public static Payload WithFields(
        ControlType kind,
        string content,
        string placeholder,
        string source,
        string iconName,
        Length checkboxSize ) => kind switch
    {
        ControlType.Panel          => new PanelPayload(),
        ControlType.SplitContainer => new SplitContainerPayload(),
        ControlType.Label          => new LabelPayload        { Content = content },
        ControlType.Image          => new ImagePayload        { Source = source },
        ControlType.IconPanel      => new IconPanelPayload    { IconName = iconName },
        ControlType.Button         => new ButtonPayload       { Content = content },
        ControlType.ButtonGroup    => new ButtonGroupPayload(),
        ControlType.Checkbox       => new CheckboxPayload     { Content = content, CheckboxSize = checkboxSize },
        ControlType.TextEntry      => new TextEntryPayload    { Placeholder = placeholder },
        ControlType.DropDown       => new DropDownPayload(),
        ControlType.Form           => new FormPayload(),
        ControlType.Field          => new FieldPayload(),
        ControlType.FieldControl   => new FieldControlPayload(),
        _                          => throw new UnreachableException( $"PayloadFactory.WithFields: no payload defined for ControlType.{kind}" ),
    };
}
