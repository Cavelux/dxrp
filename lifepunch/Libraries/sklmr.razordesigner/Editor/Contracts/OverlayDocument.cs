
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Grains.RazorDesigner.Document;

namespace Grains.RazorDesigner.Contracts;

// Top-level document. Corresponds to the root JSON object in contract-overlay.json.
internal sealed record OverlayDocument
{
    // Schema version. Currently 1. Bump when the shape changes incompatibly.
    [JsonPropertyName( "version" )]
    public int Version { get; init; } = 1;

    // Human-readable description embedded in the JSON. Not consumed at runtime.
    [JsonPropertyName( "_readme" )]
    public string Readme { get; init; } = "";

    // The overlay entries, one per ControlType that needs hand-authored data.
    [JsonPropertyName( "entries" )]
    public IReadOnlyList<OverlayEntry> Entries { get; init; } = System.Array.Empty<OverlayEntry>();
}

internal sealed record OverlayEntry
{
    // The ControlType this entry augments.
    [JsonPropertyName( "kind" )]
    public ControlType Kind { get; init; }

    [JsonPropertyName( "_cite" )]
    public string Cite { get; init; } = "";

    [JsonPropertyName( "slots" )]
    public IReadOnlyList<SlotDefinition>? Slots { get; init; }

    // Additional or overriding payload fields. Null = no field changes for this entry.
    [JsonPropertyName( "payloadFields" )]
    public IReadOnlyList<OverlayField>? PayloadFields { get; init; }

    // Override the LibraryTag (razor tag string). Null = keep reflected tag.
    [JsonPropertyName( "libraryTag" )]
    public string? LibraryTag { get; init; }

    // Override the InspectorIcon. Null = keep the reflected / seed-derived icon.
    [JsonPropertyName( "inspectorIcon" )]
    public string? InspectorIcon { get; init; }

    // Override the IsContainer flag. Null = keep the _seed-carried value.
    [JsonPropertyName( "isContainer" )]
    public bool? IsContainer { get; init; }

    // Override the PreviewStrategy. Null = keep the scanner-assigned value.
    [JsonPropertyName( "previewStrategy" )]
    public PreviewStrategy? PreviewStrategy { get; init; }
}

internal sealed record OverlayField
{
    [JsonPropertyName( "name" )]
    public string Name { get; init; } = "";

    // CLR type name (e.g. "String", "Boolean"). Informational only for overlay-only fields.
    [JsonPropertyName( "clrTypeName" )]
    public string ClrTypeName { get; init; } = "String";

    // Inspector group (e.g. "Content", "Icon"). Empty = ungrouped.
    [JsonPropertyName( "group" )]
    public string Group { get; init; } = "";

    // True if this is an Override<Group> gate bool.
    [JsonPropertyName( "isOverrideGate" )]
    public bool IsOverrideGate { get; init; } = false;

    [JsonPropertyName( "gatedByGroup" )]
    public string GatedByGroup { get; init; } = "";
}
