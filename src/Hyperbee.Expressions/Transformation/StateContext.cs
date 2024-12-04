using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.Transformation.Transitions;

namespace Hyperbee.Expressions.Transformation;

internal sealed class StateContext
{
    private int _stateId;
    private int _groupId;
    private readonly int _initialCapacity;

    private readonly Stack<StateExpression> _joinStates;
    private readonly Stack<int> _scopeIndexes;
    public List<Scope> Scopes { get; }

    public StateExpression TailState { get; private set; }

    private Scope CurrentScope => Scopes[_scopeIndexes.Peek()];

    public StateContext( int initialCapacity )
    {
        _initialCapacity = initialCapacity;

        Scopes = new List<Scope>( _initialCapacity )
        {
            new ( 0, null, null, _initialCapacity ) // root scope
        };

        _joinStates = new Stack<StateExpression>( _initialCapacity );
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

    public Scope EnterScope( StateExpression initialState )
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
    public StateExpression EnterGroup( out StateExpression sourceState )
    {
        var scope = CurrentScope;
        var joinState = new StateExpression( _stateId++, scope.ScopeId, _groupId++ );

        scope.States.Add( joinState );
        _joinStates.Push( joinState );

        sourceState = TailState;
        TailState = joinState;

        return joinState;
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void ExitGroup( StateExpression sourceState, Transition transition )
    {
        sourceState.Transition = transition;
        TailState = _joinStates.Pop();
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public StateExpression AddState()
    {
        var scope = CurrentScope;
        var state = new StateExpression( _stateId++, scope.ScopeId, _groupId );
        scope.States.Add( state );
        TailState = state;

        return state;
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void AddJumpCase( LabelTarget resultLabel, LabelTarget continueLabel, int stateId )
    {
        var scope = CurrentScope;
        var jumpCase = new JumpCase( resultLabel, continueLabel, stateId, scope.ScopeId );
        scope.JumpCases.Add( jumpCase );
    }

    public bool TryGetLabelTarget( LabelTarget target, out IStateNode node )
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

    public sealed class Scope
    {
        public int ScopeId { get; }
        public LabelTarget InitialLabel { get; }
        public Scope Parent { get; }
        public List<IStateNode> States { get; set; }
        public List<JumpCase> JumpCases { get; }

        public Scope( int scopeId, Scope parent, LabelTarget initialLabel, int initialCapacity )
        {
            Parent = parent;
            ScopeId = scopeId;
            InitialLabel = initialLabel;
            States = new List<IStateNode>( initialCapacity );
            JumpCases = [];
        }

        internal List<Expression> GetExpressions( StateMachineContext context )
        {
            var mergedExpressions = new List<Expression>( 32 );

            for ( var index = 0; index < States.Count; index++ )
            {
                var state = States[index];
                var expression = state.GetExpression( context );

                if ( expression is BlockExpression innerBlock )
                    mergedExpressions.AddRange( innerBlock.Expressions.Where( expr => !IsDefaultVoid( expr ) ) );
                else
                    mergedExpressions.Add( expression );
            }

            return mergedExpressions;

            static bool IsDefaultVoid( Expression expression )
            {
                return expression is DefaultExpression defaultExpression &&
                       defaultExpression.Type == typeof(void);
            }
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
