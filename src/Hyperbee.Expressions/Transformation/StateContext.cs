using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.Transformation.Transitions;

namespace Hyperbee.Expressions.Transformation;

public sealed class StateContext
{
    private int _stateId;
    private int _groupId;
    private readonly int _initialCapacity;

    private readonly Stack<NodeExpression> _joinStates;
    private readonly Stack<int> _scopeIndexes;
    public List<Scope> Scopes { get; }

    public NodeExpression TailState { get; private set; }

    private Scope CurrentScope => Scopes[_scopeIndexes.Peek()];

    public StateContext( int initialCapacity )
    {
        _initialCapacity = initialCapacity;

        Scopes = new List<Scope>( _initialCapacity )
        {
            new ( 0, null, _initialCapacity ) // root scope
        };

        _joinStates = new Stack<NodeExpression>( _initialCapacity );
        _scopeIndexes = new Stack<int>( _initialCapacity );
        _scopeIndexes.Push( 0 ); // root scope index

        AddState();
    }

    public Scope EnterScope()
    {
        var parentScope = CurrentScope;

        var newScopeId = Scopes.Count;
        var newScope = new Scope( newScopeId, parentScope, _initialCapacity );

        Scopes.Add( newScope );
        _scopeIndexes.Push( newScopeId );

        return newScope;
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void ExitScope()
    {
        _scopeIndexes.Pop();
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public NodeExpression EnterGroup( out NodeExpression sourceState )
    {
        var scope = CurrentScope;
        var joinState = new NodeExpression( _stateId++, scope.ScopeId, _groupId++ );

        scope.Nodes.Add( joinState );
        _joinStates.Push( joinState );

        sourceState = TailState;
        TailState = joinState;

        return joinState;
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void ExitGroup( NodeExpression sourceState, Transition transition )
    {
        sourceState.Transition = transition;
        TailState = _joinStates.Pop();
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public NodeExpression AddState()
    {
        var scope = CurrentScope;
        var node = new NodeExpression( _stateId++, scope.ScopeId, _groupId );
        scope.Nodes.Add( node );
        TailState = node;

        return node;
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void AddJumpCase( LabelTarget resultLabel, LabelTarget continueLabel, int stateId )
    {
        var scope = CurrentScope;
        var jumpCase = new JumpCase( resultLabel, continueLabel, stateId, scope.ScopeId );
        scope.JumpCases.Add( jumpCase );
    }

    public bool TryGetLabelTarget( LabelTarget target, out NodeExpression node )
    {
        node = null;

        for ( var index = 0; index < CurrentScope.Nodes.Count; index++ )
        {
            var check = CurrentScope.Nodes[index];
            if ( check.NodeLabel != target )
                continue;

            node = check;
            break;
        }

        return node != null;
    }

    public sealed class Scope
    {
        public int ScopeId { get; }
        public Scope Parent { get; }
        public List<NodeExpression> Nodes { get; set; }
        public List<JumpCase> JumpCases { get; }

        public Scope( int scopeId, Scope parent, int initialCapacity )
        {
            Parent = parent;
            ScopeId = scopeId;
            Nodes = new List<NodeExpression>( initialCapacity );
            JumpCases = [];
        }
    }

    public readonly struct JumpCase
    {
        public LabelTarget ResultLabel { get; }
        public LabelTarget ContinueLabel { get; }
        public int StateId { get; }
        public int? ParentId { get; }

        public JumpCase( LabelTarget resultLabel, LabelTarget continueLabel, int stateId, int? parentId )
        {
            ResultLabel = resultLabel;
            ContinueLabel = continueLabel;
            StateId = stateId;
            ParentId = parentId;
        }
    }
}
