using System.Collections.Generic;

namespace Grains.RazorDesigner.Projection;

public static class PreviewStrategies
{
    private static readonly Dictionary<string, PreviewStrategy> _map = new()
    {
        ["Panel"]        = PreviewStrategy.Native,
        ["SplitContainer"] = PreviewStrategy.Native,
        ["Label"]        = PreviewStrategy.Native,
        ["Image"]        = PreviewStrategy.Native,
        ["IconPanel"]    = PreviewStrategy.Native,
        ["Button"]       = PreviewStrategy.Native,
        ["Checkbox"]     = PreviewStrategy.Native,
        ["TextEntry"]    = PreviewStrategy.Native,
        ["Form"]         = PreviewStrategy.Native,
        ["Field"]        = PreviewStrategy.Native,
        ["FieldControl"] = PreviewStrategy.Native,
        ["ButtonGroup"]  = PreviewStrategy.Placeholder,
        ["DropDown"]     = PreviewStrategy.Placeholder,
    };

    public static PreviewStrategy For( string kind ) =>
        _map.TryGetValue( kind, out var s ) ? s : PreviewStrategy.Placeholder;
}
