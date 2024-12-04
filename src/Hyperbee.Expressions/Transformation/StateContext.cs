using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.Transformation.Transitions;

namespace Hyperbee.Expressions.Transformation;

internal sealed class StateContext
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
            new ( 0, null, null, _initialCapacity ) // root scope
        };

        _joinStates = new Stack<NodeExpression>( _initialCapacity );
        _scopeIndexes = new Stack<int>( _initialCapacity );
        _scopeIndexes.Push( 0 ); // root scope index

        AddState();
    }

    public void Clear()
    {
        _stateId = 0;
        _groupId = 0;
        TailState = null;

        Scopes.Clear();
        Scopes.Add( new Scope( 0, null, null, _initialCapacity ) );

        _joinStates.Clear();
        _scopeIndexes.Clear();
        _scopeIndexes.Push( 0 );

        AddState();
    }

    public Scope EnterScope( NodeExpression initialNode )
    {
        var parentScope = CurrentScope;

        var newScopeId = Scopes.Count;
        var newScope = new Scope( newScopeId, parentScope, initialNode?.NodeLabel, _initialCapacity );

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

            node = check as NodeExpression;
            break;
        }

        return node != null;
    }

    public sealed class Scope
    {
        public int ScopeId { get; }
        public LabelTarget InitialLabel { get; }
        public Scope Parent { get; }
        public List<IStateNode> Nodes { get; set; }
        public List<JumpCase> JumpCases { get; }

        public Scope( int scopeId, Scope parent, LabelTarget initialLabel, int initialCapacity )
        {
            Parent = parent;
            ScopeId = scopeId;
            InitialLabel = initialLabel;
            Nodes = new List<IStateNode>( initialCapacity );
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
