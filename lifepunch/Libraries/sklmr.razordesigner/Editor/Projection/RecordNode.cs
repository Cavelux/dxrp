using System;
using System.Collections.Generic;
using Grains.RazorDesigner.Document;

namespace Grains.RazorDesigner.Projection;

public sealed class RecordNode : IReadOnlyNode
{
    private readonly ControlRecord _record;

    private List<IReadOnlyNode> _children;
    private Dictionary<string, IReadOnlyList<IReadOnlyNode>> _slots;
    private List<IReadOnlyStateRule> _stateRules;

    public RecordNode( ControlRecord record )
    {
        _record = record ?? throw new ArgumentNullException( nameof( record ) );
    }

    // Exposed for M2.3 Applier to write LivePanel back without breaking the interface boundary.
    internal ControlRecord Backing => _record;

    // --- IReadOnlyNode ---

    public Guid Id => _record.Id;

    public string Kind => _record.Type.ToString();

    public string ClassName => _record.ClassName;

    public IAppearance Appearance => _record.Appearance;

    public IPayload Payload => _record.Payload;

    public IReadOnlyList<IReadOnlyNode> Children
    {
        get
        {
            if ( _children != null ) return _children;
            _children = new List<IReadOnlyNode>();
            foreach ( var child in _record.Children )
            {
                if ( !child.IsSlot )
                    _children.Add( new RecordNode( child ) );
            }
            return _children;
        }
    }

    public IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyNode>> Slots
    {
        get
        {
            if ( _slots != null ) return _slots;
            _slots = new Dictionary<string, IReadOnlyList<IReadOnlyNode>>();
            foreach ( var child in _record.Children )
            {
                if ( child.IsSlot && !string.IsNullOrEmpty( child.SlotName ) )
                {
                    if ( !_slots.TryGetValue( child.SlotName, out var slotList ) )
                    {
                        slotList = new List<IReadOnlyNode>();
                        _slots[child.SlotName] = slotList;
                    }
                    foreach ( var slotChild in child.Children )
                        ((List<IReadOnlyNode>)slotList).Add( new RecordNode( slotChild ) );
                }
            }
            return _slots;
        }
    }

    public IReadOnlyList<IReadOnlyStateRule> StateRules
    {
        get
        {
            if ( _stateRules != null ) return _stateRules;
            _stateRules = _record.StateRules.ConvertAll( r => (IReadOnlyStateRule) new StateRuleView( r ) );
            return _stateRules;
        }
    }

    private sealed class StateRuleView : IReadOnlyStateRule
    {
        private readonly Document.StateRule _r;
        public StateRuleView( Document.StateRule r ) { _r = r; }
        public Document.PseudoKind State => _r.State;
        public Document.NthChildMode NthChildMode => _r.NthChildMode;
        public int NthChildArg => _r.NthChildArg;
        public IAppearance Delta => _r.Delta;   // Appearance : IAppearance — no wrapper needed
    }
}
