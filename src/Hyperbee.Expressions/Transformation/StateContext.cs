using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.Transformation.Transitions;

namespace Hyperbee.Expressions.Transformation;

internal sealed class StateContext
{
    private int _stateId;
    private int _groupId;
    private readonly int _initialCapacity;

    private readonly Stack<StateNode> _joinStates;
    private readonly Stack<int> _scopeIndexes;
    public List<Scope> Scopes { get; }

    public StateNode TailState { get; private set; }

    private Scope CurrentScope => Scopes[_scopeIndexes.Peek()];

    public StateContext( int initialCapacity )
    {
        _initialCapacity = initialCapacity;

        Scopes = new List<Scope>( _initialCapacity )
        {
            new ( 0, null, null, _initialCapacity ) // root scope
        };

        _joinStates = new Stack<StateNode>( _initialCapacity );
        _scopeIndexes = new Stack<int>( _initialCapacity );
        _scopeIndexes.Push( 0 ); // the root scope index

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

    public Scope EnterScope( StateNode initialState )
    {
        var parentScope = CurrentScope;

        var newScopeId = Scopes.Count;
        var newScope = new Scope( newScopeId, parentScope, initialState?.NodeLabel, _initialCapacity );

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
    public StateNode EnterGroup( out StateNode sourceState )
    {
        var scope = CurrentScope;
        var joinState = new StateNode( _stateId++, scope.ScopeId, _groupId++ );

        scope.States.Add( joinState );
        _joinStates.Push( joinState );

        sourceState = TailState;
        TailState = joinState;

        return joinState;
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void ExitGroup( StateNode sourceState, Transition transition )
    {
        sourceState.Transition = transition;
        TailState = _joinStates.Pop();
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public StateNode AddState()
    {
        var scope = CurrentScope;
        var newState = new StateNode( _stateId++, scope.ScopeId, _groupId );
        scope.States.Add( newState );
        TailState = newState;

        return newState;
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void AddJumpCase( LabelTarget resultLabel, int stateId )
    {
        var scope = CurrentScope;
        var jumpCase = new JumpCase( resultLabel, stateId, scope.ScopeId );
        scope.JumpCases.Add( jumpCase );
    }

    public bool TryGetLabelTarget( LabelTarget target, out StateNode node )
    {
        node = null;

        for ( var index = 0; index < CurrentScope.States.Count; index++ )
        {
            var check = CurrentScope.States[index];

            if ( check.NodeLabel != target )
                continue;

            node = check;
            break;
        }

        return node != null;
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public StateNode EnterState( out StateNode sourceState )
    {
        sourceState = TailState;
        return AddState();
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void ExitState( StateNode sourceState, Transition transition )
    {
        sourceState.Transition = transition;
    }

    public sealed class Scope
    {
        public int ScopeId { get; }
        public LabelTarget InitialLabel { get; }
        public Scope Parent { get; }
        public List<StateNode> States { get; set; }
        public List<JumpCase> JumpCases { get; }

        public Scope( int scopeId, Scope parent, LabelTarget initialLabel, int initialCapacity )
        {
            Parent = parent;
            ScopeId = scopeId;
            InitialLabel = initialLabel;
            States = new List<StateNode>( initialCapacity );
            JumpCases = [];
        }

        internal IReadOnlyList<Expression> GetExpressions( StateMachineContext context )
        {
            var expressions = new List<Expression>( 32 );

            for ( var index = 0; index < States.Count; index++ )
            {
                var node = States[index];
                var expression = node.GetExpression( context );

                if ( expression is BlockExpression innerBlock )
                    expressions.AddRange( innerBlock.Expressions.Where( expr => !IsDefaultVoid( expr ) ) );
                else
                    expressions.Add( expression );
            }

            return expressions;

            static bool IsDefaultVoid( Expression expression )
            {
                return expression is DefaultExpression defaultExpression &&
                       defaultExpression.Type == typeof( void );
            }
        }
    }

    public readonly record struct JumpCase( LabelTarget ResultLabel, int StateId, int? ParentId );

}
