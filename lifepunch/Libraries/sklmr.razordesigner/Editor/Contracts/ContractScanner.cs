
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Editor;
using Microsoft.AspNetCore.Components;
using Sandbox;
using Sandbox.UI;
using Grains.RazorDesigner.Document;

namespace Grains.RazorDesigner.Contracts;

public static class ContractScanner
{
    private const string LogPrefix = "[Grains.RazorDesigner]";

    private static readonly Dictionary<ControlType, (string Tag, string Icon, bool IsContainer)> _seed =
        new()
        {
            // Layout
            { ControlType.Panel,          ( "div",         "crop_square",     true  ) },
            { ControlType.SplitContainer, ( "split",       "vertical_split",  true  ) },
            // Display
            { ControlType.Label,          ( "label",       "text_fields",     false ) },
            { ControlType.Image,          ( "image",       "image",           false ) },
            { ControlType.IconPanel,      ( "i",           "star",            false ) },
            // Input
            { ControlType.Button,         ( "button",      "smart_button",    false ) },
            { ControlType.ButtonGroup,    ( "buttongroup", "view_carousel",   true  ) },
            { ControlType.Checkbox,       ( "checkbox",    "check_box",       false ) },
            { ControlType.TextEntry,      ( "textentry",   "input",           false ) },
            { ControlType.DropDown,       ( "dropdown",    "arrow_drop_down", false ) },
            // Form
            { ControlType.Form,           ( "form",        "list_alt",        true  ) },
            { ControlType.Field,          ( "field",       "label",           true  ) },
            { ControlType.FieldControl,   ( "control",     "widgets",         true  ) },
        };

    // The frozen table. Null until first access. Written once (idempotent after).
    private static IContractTable _table;

    public static IContractTable Table => EnsureLoaded();

    private static IContractTable EnsureLoaded()
    {
        if ( _table is not null ) return _table;

        Log.Info( $"{LogPrefix} ContractScanner: building contract table…" );

        var built = new Dictionary<ControlType, ControlContract>();
        foreach ( var kind in Enum.GetValues<ControlType>() )
        {
            try
            {
                built[kind] = BuildContract( kind );
            }
            catch ( Exception ex )
            {
                Log.Error( $"{LogPrefix} ContractScanner: failed to build contract for {kind}: {ex.Message}" );
                throw;
            }
        }

        ApplyOverlay( built );

        var frozen = new ReadOnlyDictionary<ControlType, ControlContract>( built );
        _table = new ContractTable( frozen );

        Log.Info( $"{LogPrefix} ContractScanner: {built.Count} contracts built. Per-kind field counts: " +
            string.Join( ", ", built.Values.Select( c => $"{c.Kind}:{c.PayloadFields.Count}f" ) ) );

        return _table;
    }


    private static ControlContract BuildContract( ControlType kind )
    {
        // 1. Resolve the Sandbox.UI engine type for this kind.
        var engineType = ResolveEngineType( kind );

        var engineFields = WalkEngineParameters( engineType );

        var payloadType  = PayloadFactory.Default( kind ).GetType();
        var recordFields = WalkRecordGroups( payloadType, kind );

        // 4. Merge: payload-record [Group] wins (Design note 6 / spec §M6.3).
        var fields = MergeFields( engineFields, recordFields );

        // 5. IsContainer — seeded from _seed map (documented limit 6; was ControlMetadata).
        var isContainer = _seed[kind].IsContainer;

        var icon = ResolveInspectorIcon( engineType, kind );

        // 7. Slots — empty until M6.2's overlay adds them (e.g. SplitContainer Left/Right).
        var slots = Array.Empty<SlotDefinition>();

        // 8. PreviewStrategy (reserved-but-inert in v1 — documented limit 8).
        var preview = kind switch
        {
            ControlType.Button or ControlType.TextEntry or ControlType.Checkbox => PreviewStrategy.LabelSubstitute,
            ControlType.IconPanel => PreviewStrategy.IconGlyph,
            _ => PreviewStrategy.Native,
        };

        // 9. LibraryTag — seeded from _seed map (documented limit 7; was ControlMetadata).
        var libraryTag = _seed[kind].Tag;

        Log.Info( $"{LogPrefix} ContractScanner.BuildContract({kind}): engineType={engineType?.FullName ?? "<not found>"} " +
            $"tag='{libraryTag}' container={isContainer} preview={preview} fields={fields.Count}" );

        return new ControlContract(
            Kind:           kind,
            PayloadType:    payloadType,
            LibraryTag:     libraryTag,
            IsContainer:    isContainer,
            InspectorIcon:  icon,
            Slots:          new ReadOnlyCollection<SlotDefinition>( (SlotDefinition[])slots.Clone() ),
            PayloadFields:  new ReadOnlyCollection<ContractField>( fields ),
            PreviewStrategy: preview );
    }


    private static Type ResolveEngineType( ControlType kind )
    {
        var tag = _seed[kind].Tag;

        var found = EditorTypeLibrary.GetTypes()
            .FirstOrDefault( td =>
            {
                var lib = td.GetAttribute<LibraryAttribute>();
                if ( lib == null ) return false;
                // Match the Name (the tag string passed to [Library("tag")]) case-insensitively.
                return string.Equals( lib.Name, tag, StringComparison.OrdinalIgnoreCase );
            } );

        if ( found is not null )
            return found.TargetType;

        var fallback = FallbackEngineTypeMap( kind );
        if ( fallback is not null )
        {
            Log.Warning( $"{LogPrefix} ContractScanner.ResolveEngineType({kind}): [Library('{tag}')] " +
                $"reverse-lookup found nothing — using hardcoded fallback {fallback.FullName}. " +
                $"Check if the engine [Library] tag changed." );
            return fallback;
        }

        Log.Warning( $"{LogPrefix} ContractScanner.ResolveEngineType({kind}): no type found for tag '{tag}' " +
            $"and no hardcoded fallback. Engine parameters for this kind will be empty." );
        return null;
    }

    private static Type FallbackEngineTypeMap( ControlType kind ) => kind switch
    {
        ControlType.Panel          => typeof( Sandbox.UI.Panel ),
        ControlType.SplitContainer => typeof( Sandbox.UI.SplitContainer ),
        ControlType.Label          => typeof( Sandbox.UI.Label ),
        ControlType.Image          => typeof( Sandbox.UI.Image ),
        ControlType.IconPanel      => typeof( Sandbox.UI.IconPanel ),
        ControlType.Button         => typeof( Sandbox.UI.Button ),
        ControlType.ButtonGroup    => typeof( Sandbox.UI.ButtonGroup ),
        ControlType.Checkbox       => typeof( Sandbox.UI.Checkbox ),
        ControlType.TextEntry      => typeof( Sandbox.UI.TextEntry ),
        ControlType.DropDown       => typeof( Sandbox.UI.DropDown ),
        ControlType.Form           => typeof( Sandbox.UI.Form ),
        ControlType.Field          => typeof( Sandbox.UI.Field ),
        ControlType.FieldControl   => typeof( Sandbox.UI.FieldControl ),
        _ => null,
    };


    private static List<ContractField> WalkEngineParameters( Type engineType )
    {
        if ( engineType is null ) return new List<ContractField>();

        var results  = new List<ContractField>();
        var seenNames = new HashSet<string>( StringComparer.Ordinal );

        // Walk from the concrete type up through the inheritance chain.
        var current = engineType;
        while ( current != null && current != typeof( object ) )
        {
            foreach ( var prop in current.GetProperties( BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly ) )
            {
                // Already collected from a more-derived override — skip.
                if ( !seenNames.Add( prop.Name ) ) continue;

                // Must have [Parameter] (in Microsoft.AspNetCore.Components).
                var paramAttr = prop.GetCustomAttribute<ParameterAttribute>( false );
                if ( paramAttr is null ) continue;

                // Skip RenderFragment / RenderFragment<T> (documented limit 1).
                if ( IsRenderFragment( prop.PropertyType ) ) continue;

                // Skip Func<> / Action<> delegates (documented limit 2).
                if ( IsFuncOrAction( prop.PropertyType ) ) continue;

                // Engine [Parameter] properties don't carry [Group] — that's our authoring-side.
                results.Add( new ContractField(
                    Name:         prop.Name,
                    ClrType:      prop.PropertyType,
                    Group:        "",
                    IsOverrideGate: false,
                    GatedByGroup: "" ) );
            }

            current = current.BaseType;
        }

        return results;
    }

    private static bool IsRenderFragment( Type t )
    {
        if ( t == typeof( Microsoft.AspNetCore.Components.RenderFragment ) ) return true;
        if ( t.IsGenericType && t.GetGenericTypeDefinition() == typeof( Microsoft.AspNetCore.Components.RenderFragment<> ) ) return true;
        return false;
    }

    private static bool IsFuncOrAction( Type t )
    {
        if ( t == typeof( System.Action ) ) return true;

        if ( !t.IsGenericType )
        {
            // Any other non-generic delegate (rare, but possible via inheritance).
            return typeof( System.MulticastDelegate ).IsAssignableFrom( t )
                   && t != typeof( System.MulticastDelegate )
                   && t != typeof( System.Delegate );
        }

        // Generic delegates: Func<TResult>, Func<T,TResult>, ..., Action<T>, Action<T,T2>, ...
        var def  = t.GetGenericTypeDefinition();
        var name = def.Name; // "Func`1", "Action`2", etc.
        return name.StartsWith( "Func`", StringComparison.Ordinal )
            || name.StartsWith( "Action`", StringComparison.Ordinal );
    }


    private static readonly HashSet<string> PayloadContentPropNames = new( StringComparer.Ordinal )
    {
        "Content", "Placeholder", "CheckboxSize", "Source", "IconName",
    };

    private static HashSet<string> PayloadContentNamesForKind( ControlType kind )
    {
        return kind switch
        {
            ControlType.Label          => new HashSet<string>( StringComparer.Ordinal ) { "Content" },
            ControlType.Image          => new HashSet<string>( StringComparer.Ordinal ) { "Source" },
            ControlType.IconPanel      => new HashSet<string>( StringComparer.Ordinal ) { "IconName" },
            ControlType.Button         => new HashSet<string>( StringComparer.Ordinal ) { "Content" },
            ControlType.Checkbox       => new HashSet<string>( StringComparer.Ordinal ) { "Content", "CheckboxSize" },
            ControlType.TextEntry      => new HashSet<string>( StringComparer.Ordinal ) { "Placeholder" },

            // Panel, SplitContainer, ButtonGroup, DropDown, Form, Field, FieldControl — none.
            ControlType.Panel or ControlType.SplitContainer or
            ControlType.ButtonGroup or ControlType.DropDown or
            ControlType.Form or ControlType.Field or ControlType.FieldControl
                => new HashSet<string>( StringComparer.Ordinal ),

            _ => new HashSet<string>( StringComparer.Ordinal ),
        };
    }

    private static List<ContractField> WalkRecordGroups( Type payloadType, ControlType kind )
    {
        var results   = new List<ContractField>();
        var seenNames = new HashSet<string>( StringComparer.Ordinal );

        // Determine which payload-content names this kind actually uses.
        var kindPayloadContentNames = PayloadContentNamesForKind( kind );

        CollectPropsWithGroups( payloadType, results, seenNames, kindPayloadContentNames );

        CollectPropsWithGroups( typeof( Payload ), results, seenNames, kindPayloadContentNames );

        CollectPropsWithGroups( typeof( ControlRecord ), results, seenNames, kindPayloadContentNames );

        var gateGroups = new HashSet<string>(
            results
                .Where( f => f.IsOverrideGate )
                .Select( f => f.Group ),
            StringComparer.Ordinal );

        for ( int i = 0; i < results.Count; i++ )
        {
            var f = results[i];
            if ( f.IsOverrideGate || string.IsNullOrEmpty( f.Group ) ) continue;
            if ( gateGroups.Contains( f.Group ) )
                results[i] = f with { GatedByGroup = f.Group };
        }

        return results;
    }

    private static void CollectPropsWithGroups(
        Type sourceType,
        List<ContractField> results,
        HashSet<string> seenNames,
        HashSet<string> kindPayloadContentNames )
    {
        foreach ( var prop in sourceType.GetProperties(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly ) )
        {
            // [Hide] properties: skip (they're internal/non-inspector).
            if ( prop.GetCustomAttribute<HideAttribute>() is not null ) continue;

            if ( PayloadContentPropNames.Contains( prop.Name )
                 && !kindPayloadContentNames.Contains( prop.Name ) ) continue;

            // Collect the [Group] attribute (Sandbox.GroupAttribute).
            var groupAttr = prop.GetCustomAttribute<GroupAttribute>();
            var group     = groupAttr?.Value ?? "";

            var isOverrideGate = prop.Name.StartsWith( "Override", StringComparison.Ordinal )
                && prop.PropertyType == typeof( bool );

            if ( string.IsNullOrEmpty( group ) && !isOverrideGate ) continue;

            if ( !seenNames.Add( prop.Name ) ) continue;

            results.Add( new ContractField(
                Name:          prop.Name,
                ClrType:       prop.PropertyType,
                Group:         group,
                IsOverrideGate: isOverrideGate,
                GatedByGroup:  "" /* filled in after the full list is built */ ) );
        }
    }


    private static List<ContractField> MergeFields(
        List<ContractField> engineFields,
        List<ContractField> recordFields )
    {
        var result   = new List<ContractField>( recordFields );
        var nameSet  = new HashSet<string>(
            recordFields.Select( f => f.Name ), StringComparer.Ordinal );

        // Add engine fields not already covered by the record side.
        foreach ( var ef in engineFields )
        {
            if ( nameSet.Add( ef.Name ) )
                result.Add( ef );
        }

        return result;
    }


    private static string ResolveInspectorIcon( Type engineType, ControlType kind )
    {
        // Try the engine type's [Icon] attribute first (may not be present).
        if ( engineType is not null )
        {
            var typeDesc = EditorTypeLibrary.GetType( engineType );
            if ( typeDesc is not null )
            {
                var iconAttr = typeDesc.GetAttribute<IconAttribute>();
                if ( iconAttr is not null && !string.IsNullOrEmpty( iconAttr.Value ) )
                {
                    Log.Info( $"{LogPrefix} ContractScanner.ResolveInspectorIcon({kind}): found [Icon] = '{iconAttr.Value}'" );
                    return iconAttr.Value;
                }
            }
        }

        // Fall back to the _seed map (v1 default; was ControlMetadata.IconName).
        var icon = _seed[kind].Icon;
        Log.Info( $"{LogPrefix} ContractScanner.ResolveInspectorIcon({kind}): using seed fallback = '{icon}'" );
        return icon;
    }


    private static readonly JsonSerializerOptions OverlayJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static void ApplyOverlay( Dictionary<ControlType, ControlContract> built )
    {
        var path = ResolveOverlayPath();
        if ( !File.Exists( path ) )
        {
            var msg = $"{LogPrefix} ContractScanner.ApplyOverlay: contract-overlay.json not found at '{path}'. " +
                      $"This file is a required addon asset — ensure it exists in Editor/Contracts/.";
            Log.Error( msg );
            throw new FileNotFoundException( msg, path );
        }

        Log.Info( $"{LogPrefix} ContractScanner.ApplyOverlay: loading overlay from '{path}'" );
        string text;
        try
        {
            text = File.ReadAllText( path );
        }
        catch ( IOException ex )
        {
            var msg = $"{LogPrefix} ContractScanner.ApplyOverlay: failed to read '{path}': {ex.Message}";
            Log.Error( msg );
            throw;
        }

        OverlayDocument doc;
        try
        {
            doc = JsonSerializer.Deserialize<OverlayDocument>( text, OverlayJsonOptions );
        }
        catch ( JsonException ex )
        {
            var msg = $"{LogPrefix} ContractScanner.ApplyOverlay: JSON parse error in '{path}': {ex.Message}";
            Log.Error( msg );
            throw;
        }

        if ( doc is null || doc.Entries is null || doc.Entries.Count == 0 )
        {
            Log.Info( $"{LogPrefix} ContractScanner.ApplyOverlay: overlay loaded (version={doc?.Version}) — no entries, nothing to merge." );
            return;
        }

        Log.Info( $"{LogPrefix} ContractScanner.ApplyOverlay: overlay version={doc.Version}, {doc.Entries.Count} entries to apply." );

        var applied = 0;
        foreach ( var entry in doc.Entries )
        {
            // Governance tripwire: every entry MUST carry a non-empty _cite.
            if ( string.IsNullOrWhiteSpace( entry.Cite ) )
            {
                var msg = $"{LogPrefix} ContractScanner.ApplyOverlay: contract-overlay.json entry for " +
                          $"'{entry.Kind}' is missing a non-empty \"_cite\" — every overlay entry must cite " +
                          $"the engine type / method / behaviour it encodes. Remove the entry or add a _cite.";
                Log.Error( msg );
                throw new InvalidDataException( msg );
            }

            if ( !built.TryGetValue( entry.Kind, out var existing ) )
            {
                Log.Warning( $"{LogPrefix} ContractScanner.ApplyOverlay: overlay entry kind '{entry.Kind}' " +
                             $"is not in the built contract table (unrecognised ControlType?). Skipping." );
                continue;
            }

            // Merge each optional field — null means "leave the reflected value unchanged".
            var slots         = entry.Slots ?? existing.Slots;
            var payloadFields = entry.PayloadFields is null
                ? existing.PayloadFields
                : MergePayloadFields( existing.PayloadFields, entry.PayloadFields );
            var libraryTag    = entry.LibraryTag    ?? existing.LibraryTag;
            var inspectorIcon = entry.InspectorIcon ?? existing.InspectorIcon;
            var isContainer   = entry.IsContainer   ?? existing.IsContainer;
            var preview       = entry.PreviewStrategy ?? existing.PreviewStrategy;

            built[entry.Kind] = existing with
            {
                Slots           = new ReadOnlyCollection<SlotDefinition>( slots.ToList() ),
                PayloadFields   = payloadFields,
                LibraryTag      = libraryTag,
                InspectorIcon   = inspectorIcon,
                IsContainer     = isContainer,
                PreviewStrategy = preview,
            };

            Log.Info( $"{LogPrefix} ContractScanner.ApplyOverlay: merged '{entry.Kind}' — " +
                      $"slots={slots.Count} payloadFields={payloadFields.Count} " +
                      $"tag='{libraryTag}' icon='{inspectorIcon}' container={isContainer} preview={preview}" );
            applied++;
        }

        Log.Info( $"{LogPrefix} ContractScanner.ApplyOverlay: {applied}/{doc.Entries.Count} entries applied." );
    }

    private static IReadOnlyList<ContractField> MergePayloadFields(
        IReadOnlyList<ContractField> existing,
        IReadOnlyList<OverlayField> overlayFields )
    {
        // Index overlay fields by name for O(1) lookup.
        var overlayByName = new Dictionary<string, OverlayField>( StringComparer.Ordinal );
        foreach ( var overlayField in overlayFields )
        {
            if ( !string.IsNullOrEmpty( overlayField.Name ) )
                overlayByName[overlayField.Name] = overlayField;
        }

        var result    = new List<ContractField>();
        var seenNames = new HashSet<string>( StringComparer.Ordinal );

        // Pass 1: walk existing (reflected) fields; apply overlay overrides where present.
        foreach ( var reflectedField in existing )
        {
            seenNames.Add( reflectedField.Name );
            if ( overlayByName.TryGetValue( reflectedField.Name, out var overlayOverride ) )
            {
                // Overlay override: keep reflected CLR type; take overlay group/gate data.
                result.Add( reflectedField with
                {
                    Group          = string.IsNullOrEmpty( overlayOverride.Group ) ? reflectedField.Group : overlayOverride.Group,
                    IsOverrideGate = overlayOverride.IsOverrideGate,
                    GatedByGroup   = string.IsNullOrEmpty( overlayOverride.GatedByGroup ) ? reflectedField.GatedByGroup : overlayOverride.GatedByGroup,
                } );
            }
            else
            {
                // No overlay entry for this field — keep reflected as-is.
                result.Add( reflectedField );
            }
        }

        // Pass 2: add overlay-only fields (not seen in reflection).
        foreach ( var addField in overlayFields )
        {
            if ( string.IsNullOrEmpty( addField.Name ) ) continue;
            if ( !seenNames.Add( addField.Name ) ) continue; // already processed above

            // Resolve CLR type from the name string; fall back to string when unknown.
            var clrType = ResolveClrType( addField.ClrTypeName );

            result.Add( new ContractField(
                Name:           addField.Name,
                ClrType:        clrType,
                Group:          addField.Group,
                IsOverrideGate: addField.IsOverrideGate,
                GatedByGroup:   addField.GatedByGroup ) );
        }

        return new ReadOnlyCollection<ContractField>( result );
    }

    private static Type ResolveClrType( string typeName )
    {
        if ( string.IsNullOrEmpty( typeName ) ) return typeof( string );
        return typeName switch
        {
            "String"  or "string"  => typeof( string ),
            "Boolean" or "bool"    => typeof( bool ),
            "Int32"   or "int"     => typeof( int ),
            "Single"  or "float"   => typeof( float ),
            "Double"  or "double"  => typeof( double ),
            _ => typeof( string ), // safe fallback — overlay-only fields are informational
        };
    }

    private static readonly (string Ident, string Subpath)[] OverlayFileRoots = new[]
    {
        ( "razordesigner",        "Editor/Contracts/contract-overlay.json" ),
        ( "grains_razordesigner", "Editor/Contracts/contract-overlay.json" ),
        // Also check directly inside the dev-workspace library subdirectory.
        ( "grains_razordesigner", "Libraries/xaz.razordesigner/Editor/Contracts/contract-overlay.json" ),
    };

    private static string ResolveOverlayPath()
    {
        foreach ( var (ident, subpath) in OverlayFileRoots )
        {
            var root = System.Linq.Enumerable
                .FirstOrDefault( EditorUtility.Projects.GetAll(),
                    p => string.Equals( p.Config?.Ident, ident, StringComparison.OrdinalIgnoreCase ) )
                ?.GetRootPath();
            if ( string.IsNullOrEmpty( root ) ) continue;
            var candidate = Path.Combine( root, subpath );
            if ( File.Exists( candidate ) )
            {
                Log.Info( $"{LogPrefix} ContractScanner.ResolveOverlayPath: resolved via ident='{ident}' → '{candidate}'" );
                return candidate;
            }
        }

        var assemblyDir = Path.GetDirectoryName( typeof( ContractScanner ).Assembly.Location ) ?? "";
        var lastResort  = Path.Combine( assemblyDir, "contract-overlay.json" );
        Log.Warning( $"{LogPrefix} ContractScanner.ResolveOverlayPath: project-list lookup found nothing — " +
                     $"falling back to assembly-relative path '{lastResort}'. " +
                     $"This likely means EditorUtility.Projects.GetAll() returned no projects matching " +
                     $"'razordesigner' or 'grains_razordesigner'." );
        return lastResort;
    }


    private sealed class ContractTable : IContractTable
    {
        private readonly ReadOnlyDictionary<ControlType, ControlContract> _data;

        public ContractTable( ReadOnlyDictionary<ControlType, ControlContract> data )
        {
            _data = data;
        }

        public ControlContract Get( ControlType kind )
        {
            if ( _data.TryGetValue( kind, out var contract ) )
                return contract;
            throw new KeyNotFoundException(
                $"ContractTable.Get: no contract registered for ControlType.{kind}. " +
                $"This should never happen if all 13 types were built successfully." );
        }

        public IEnumerable<ControlContract> All => _data.Values;
    }
}
