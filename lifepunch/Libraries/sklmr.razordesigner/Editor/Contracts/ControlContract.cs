using System;
using System.Collections.Generic;
using System.Linq;
using Grains.RazorDesigner.Document;

namespace Grains.RazorDesigner.Contracts;

public readonly record struct ControlContract(
    ControlType Kind,
    Type PayloadType,
    string LibraryTag,
    bool IsContainer,
    string InspectorIcon,
    IReadOnlyList<SlotDefinition> Slots,
    IReadOnlyList<ContractField> PayloadFields,
    PreviewStrategy PreviewStrategy )
{
    // Distinct, non-empty [Group] names across all PayloadFields, in first-seen order.
    public IEnumerable<string> Groups()
    {
        var seen = new HashSet<string>( StringComparer.Ordinal );
        foreach ( var f in PayloadFields )
        {
            if ( !string.IsNullOrEmpty( f.Group ) && seen.Add( f.Group ) )
                yield return f.Group;
        }
    }

    public IReadOnlyList<string> PropertyNamesIn( string group ) =>
        PayloadFields
            .Where( f => string.Equals( f.Group, group, StringComparison.Ordinal ) && !f.IsOverrideGate )
            .Select( f => f.Name )
            .ToList();

    public string GatedByGroupOf( string propertyName )
    {
        var field = PayloadFields.FirstOrDefault( f =>
            string.Equals( f.Name, propertyName, StringComparison.Ordinal ) );
        return field.Name is null ? "" : field.GatedByGroup;
    }
}

public readonly record struct ContractField(
    string Name,
    Type ClrType,
    string Group,
    bool IsOverrideGate,
    string GatedByGroup );

public readonly record struct SlotDefinition(
    string Name,
    string CssClass,
    string Description );

public enum PreviewStrategy
{
    // Rendered via the real Sandbox.UI engine type (most controls).
    Native,
    LabelSubstitute,
    // Rendered as an icon glyph (IconPanel — the glyph string is the content).
    IconGlyph,
}
