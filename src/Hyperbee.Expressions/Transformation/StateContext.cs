using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.Collections;
using Hyperbee.Expressions.Transformation.Transitions;

namespace Hyperbee.Expressions.Transformation;

public sealed class StateContext : IDisposable
{
    private int _stateId;
    private readonly int _initialCapacity;

    private readonly PooledStack<NodeExpression> _joinStates;
    private readonly PooledStack<int> _scopeIndexes;
    public PooledArray<Scope> Scopes { get; }

    public NodeExpression TailState { get; private set; }

    private Scope CurrentScope => Scopes[_scopeIndexes.Peek()];

    public StateContext( int initialCapacity )
    {
        _initialCapacity = initialCapacity;

        Scopes = new PooledArray<Scope>( _initialCapacity )
        {
            new ( 0, null, _initialCapacity ) // root scope
        };

        _joinStates = new PooledStack<NodeExpression>( _initialCapacity );
        _scopeIndexes = new PooledStack<int>( _initialCapacity );
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
        var joinState = new NodeExpression( _stateId++, scope.ScopeId );

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
        var node = new NodeExpression( _stateId++, scope.ScopeId );
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

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public bool TryGetLabelTarget( LabelTarget target, out NodeExpression node )
    {
        var nodes = CurrentScope.Nodes;
        node = null;

        for ( var index = 0; index < nodes.Count; index++ )
        {
            var check = nodes[index];

            if ( check.NodeLabel != target )
                continue;

            node = check;
            break;
        }

        return node != null;
    }

    public sealed class Scope : IDisposable
    {
        public int ScopeId { get; }
        public Scope Parent { get; }
        public PooledArray<NodeExpression> Nodes { get; set; }
        public PooledArray<JumpCase> JumpCases { get; }

        public Scope( int scopeId, Scope parent, int initialCapacity )
        {
            Parent = parent;
            ScopeId = scopeId;
            Nodes = new PooledArray<NodeExpression>( initialCapacity );
            JumpCases = [];
        }

        public void Dispose()
        {
            Nodes?.Dispose();
            JumpCases?.Dispose();
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

    public void Dispose()
    {
        _joinStates?.Dispose();
        _scopeIndexes?.Dispose();

        if ( Scopes == null )
            return;

        foreach ( var scope in Scopes )
        {
            scope.Dispose();
        }

        Scopes.Dispose();
    }
}
