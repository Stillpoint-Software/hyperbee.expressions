using System.Linq.Expressions;
using Hyperbee.Expressions.Transformation.Transitions;

namespace Hyperbee.Expressions.Transformation;

internal class LoweringVisitor : ExpressionVisitor
{
    private const int InitialCapacity = 8;

    private ParameterExpression _returnValue;
    private ParameterExpression[] _definedVariables;
    private readonly HashSet<ParameterExpression> _variables = new( InitialCapacity );
    private int _awaitCount;

    private readonly StateContext _states = new( InitialCapacity );
    private readonly Dictionary<LabelTarget, Expression> _labels = [];

    private static class VariableName
    {
        // use special names to prevent collisions
        public static string Awaiter( int stateId ) => $"__awaiter<{stateId}>";
        public static string Result( int stateId ) => $"__result<{stateId}>";

        public static string Try( int stateId ) => $"__try<{stateId}>";
        public static string Exception( int stateId ) => $"__ex<{stateId}>";

        public const string Return = "return<>";
    }

    public LoweringResult Transform( ParameterExpression[] variables, params Expression[] expressions )
    {
        _definedVariables = variables;

        foreach ( var expr in expressions )
        {
            VisitInternal( expr );
        }

        return new LoweringResult
        {
            Scopes = _states.Scopes,
            ReturnValue = _returnValue,
            AwaitCount = _awaitCount,
            Variables = _variables
        };
    }

    public LoweringResult Transform( params Expression[] expressions )
    {
        return Transform( [], expressions );
    }

    private NodeExpression VisitBranch( Expression expression, NodeExpression joinState,
        ParameterExpression resultVariable = null,
        Action<NodeExpression> init = null,
        bool captureVisit = true )
    {
        // Create a new state for the branch

        var branchState = _states.AddState();

        init?.Invoke( branchState );

        VisitInternal( expression, captureVisit );

        // Set a default Transition if the branch tail didn't join
        var tailState = _states.TailState;
        tailState.ResultVariable = resultVariable;

        if ( tailState.Transition != null )
        {
            return branchState;
        }

        tailState.Transition = new GotoTransition { TargetNode = joinState };

        return branchState;
    }

    protected override Expression VisitBlock( BlockExpression node )
    {
        foreach ( var expression in node.Expressions )
        {
            VisitInternal( expression );
        }

        return node;
    }

    protected override Expression VisitConditional( ConditionalExpression node )
    {
        var updatedTest = VisitInternal( node.Test, captureVisit: false );

        var joinState = _states.EnterGroup( out var sourceState );

        var resultVariable = GetResultVariable( node, sourceState.StateId );

        var conditionalTransition = new ConditionalTransition
        {
            Test = updatedTest,
            IfTrue = VisitBranch( node.IfTrue, joinState, resultVariable ),
            IfFalse = node.IfFalse is not DefaultExpression
                ? VisitBranch( node.IfFalse, joinState, resultVariable )
                : joinState,
        };

        sourceState.ResultVariable = resultVariable;
        joinState.ResultValue = resultVariable;

        _states.ExitGroup( sourceState, conditionalTransition );

        return node;
    }

    protected override Expression VisitSwitch( SwitchExpression node )
    {
        var updatedSwitchValue = VisitInternal( node.SwitchValue, captureVisit: false );

        var joinState = _states.EnterGroup( out var sourceState );

        var resultVariable = GetResultVariable( node, sourceState.StateId );

        var switchTransition = new SwitchTransition { SwitchValue = updatedSwitchValue };

        if ( node.DefaultBody != null )
        {
            switchTransition.DefaultNode = VisitBranch( node.DefaultBody, joinState, resultVariable );
        }

        foreach ( var switchCase in node.Cases )
        {
            switchTransition.AddSwitchCase(
                [.. switchCase.TestValues], // TODO: Visit these because they could be async
                VisitBranch( switchCase.Body, joinState, resultVariable )
            );
        }

        sourceState.ResultVariable = resultVariable;
        joinState.ResultValue = resultVariable;

        _states.ExitGroup( sourceState, switchTransition );

        return node;
    }

    protected override Expression VisitTry( TryExpression node )
    {
        var joinState = _states.EnterGroup( out var sourceState );

        var resultVariable = GetResultVariable( node, sourceState.StateId );

        var tryStateVariable = CreateVariable( typeof( int ), VariableName.Try( sourceState.StateId ) );
        var exceptionVariable = CreateVariable( typeof( object ), VariableName.Exception( sourceState.StateId ) );

        // If there is a finally block then that is the join for a try/catch.
        NodeExpression finalExpression = null;
        if ( node.Finally != null )
        {
            finalExpression = VisitBranch( node.Finally, joinState );
            joinState = finalExpression;
        }

        var nodeScope = _states.EnterScope();

        var tryCatchTransition = new TryCatchTransition
        {
            TryStateVariable = tryStateVariable,
            ExceptionVariable = exceptionVariable,
            TryNode = VisitBranch( node.Body, joinState, resultVariable ),
            FinallyNode = finalExpression,
            StateScope = nodeScope,
            Scopes = _states.Scopes
        };

        _states.ExitScope();

        for ( var index = 0; index < node.Handlers.Count; index++ )
        {
            var catchBlock = node.Handlers[index];
            tryCatchTransition.AddCatchBlock(
                catchBlock,
                VisitBranch( catchBlock.Body, joinState ),
                index );
        }

        sourceState.ResultVariable = resultVariable;
        joinState.ResultValue = resultVariable;

        _states.ExitGroup( sourceState, tryCatchTransition );

        return node;
    }

    protected override Expression VisitLoop( LoopExpression node )
    {
        var joinState = _states.EnterGroup( out var sourceState );

        var resultVariable = GetResultVariable( node, sourceState.StateId );

        var loopTransition = new LoopTransition
        {
            BodyNode = VisitBranch( node.Body, joinState, resultVariable, InitializeLabels )
        };

        sourceState.ResultVariable = resultVariable;
        joinState.ResultValue = resultVariable;

        // TODO: This seems wrong, I shouldn't have to cast to GotoTransition (maybe all types of a TargetNode?)

        if ( _states.TailState.Transition is GotoTransition gotoTransition )
            gotoTransition.TargetNode = loopTransition.BodyNode;

        _states.ExitGroup( sourceState, loopTransition );

        return node;

        // Helper function for fixing loop labels
        void InitializeLabels( NodeExpression branchState )
        {
            if ( node.ContinueLabel != null )
                _labels[node.ContinueLabel] = Expression.Goto( branchState.NodeLabel );

            if ( node.BreakLabel != null )
                _labels[node.BreakLabel] = Expression.Goto( joinState.NodeLabel );
        }
    }

    protected static Expression VisitAsyncBlock( AsyncBlockExpression node )
    {
        return node.Reduce();
    }

    protected Expression VisitAwait( AwaitExpression node )
    {
        var joinState = _states.EnterGroup( out var sourceState );

        var resultVariable = GetResultVariable( node, sourceState.StateId );

        var completionState = VisitBranch( node.Target, joinState, resultVariable, captureVisit: false );

        _awaitCount++;

        var awaitBinder = node.GetAwaitBinder();

        var awaiterType = awaitBinder.GetAwaiterMethod.ReturnType;

        var awaiterVariable = CreateVariable( awaiterType, VariableName.Awaiter( sourceState.StateId ) );

        completionState.Transition = new AwaitResultTransition
        {
            TargetNode = joinState,
            AwaiterVariable = awaiterVariable,
            ResultVariable = resultVariable,
            GetResultMethod = awaitBinder.GetResultMethod
        };

        _states.AddJumpCase( completionState.NodeLabel, joinState.NodeLabel, sourceState.StateId );

        var awaitTransition = new AwaitTransition
        {
            Target = node.Target,
            StateId = sourceState.StateId,
            AwaiterVariable = awaiterVariable,
            CompletionNode = completionState,
            GetAwaiterMethod = awaitBinder.GetAwaiterMethod,
            ConfigureAwait = node.ConfigureAwait
        };

        sourceState.ResultVariable = resultVariable;
        joinState.ResultValue = resultVariable;

        _states.ExitGroup( sourceState, awaitTransition );

        return (Expression) resultVariable ?? Expression.Empty();
    }

    protected override Expression VisitExtension( Expression node )
    {
        return node switch
        {
            AsyncBlockExpression asyncBlockExpression => VisitAsyncBlock( asyncBlockExpression ),
            AwaitExpression awaitExpression => VisitAwait( awaitExpression ),
            _ => base.VisitExtension( node )
        };
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        if ( _definedVariables.Contains( node ) )
            _variables.Add( node );

        return base.VisitParameter( node );
    }

    protected override Expression VisitGoto( GotoExpression node )
    {
        if ( _labels.TryGetValue( node.Target, out var labelExpression ) )
        {
            return labelExpression;
        }

        var updateNode = base.VisitGoto( node );

        if ( updateNode is not GotoExpression { Kind: GotoExpressionKind.Return } gotoExpression )
            return updateNode;

        _returnValue ??= CreateVariable( gotoExpression.Value!.Type, VariableName.Return );

        // update this to assign to a return value versus a goto
        return Expression.Assign( _returnValue, gotoExpression.Value! );
    }

    private Expression VisitInternal( Expression expr, bool captureVisit = true )
    {
        var result = Visit( expr );

        switch ( expr )
        {
            case BlockExpression:
            case ConditionalExpression:
            case SwitchExpression:
            case TryExpression:
            case AwaitExpression:
            case AsyncBlockExpression:
            case LoopExpression:
                break;

            default:
                // Warning: visitation mutates the tail state.
                if ( captureVisit )
                    _states.TailState.Expressions.Add( result );
                break;
        }

        return result;
    }

    private ParameterExpression GetResultVariable( Expression node, int stateId )
    {
        if ( node.Type == typeof( void ) )
            return null;

        ParameterExpression returnVariable =
            Expression.Parameter( node.Type, VariableName.Result( stateId ) );
        _variables.Add( returnVariable );

        return returnVariable;
    }

    private ParameterExpression CreateVariable( Type type, string name )
    {
        var variable = Expression.Variable( type, name );
        _variables.Add( variable );
        return variable;
    }

    private class StateContext
    {
        private int _stateId;
        private readonly Stack<int> _scopeIndexes;
        private readonly int _initialCapacity;

        public List<StateScope> Scopes { get; }

        public StateContext( int initialCapacity )
        {
            _initialCapacity = initialCapacity;
            _scopeIndexes = new Stack<int>( _initialCapacity );
            _scopeIndexes.Push( 0 );

            Scopes =
            [
                new StateScope( 0, parent: null, _initialCapacity )
            ];

            CurrentScope.AddState( _stateId++ );
        }

        private StateScope CurrentScope => Scopes[_scopeIndexes.Peek()];

        public NodeExpression TailState => CurrentScope.TailState;

        public NodeExpression AddState() => CurrentScope.AddState( _stateId++ );

        public NodeExpression EnterGroup( out NodeExpression sourceState )
        {
            return CurrentScope.EnterGroup( _stateId++, out sourceState );
        }

        public void ExitGroup( NodeExpression sourceState, Transition transition )
        {
            CurrentScope.ExitGroup( sourceState, transition );
        }

        public StateScope EnterScope()
        {
            var scope = new StateScope( Scopes.Count, CurrentScope, _initialCapacity );

            Scopes.Add( scope );

            _scopeIndexes.Push( scope.ScopeId );

            return scope;
        }

        public void ExitScope()
        {
            _scopeIndexes.Pop();
        }

        public void AddJumpCase( LabelTarget resultLabel, LabelTarget continueLabel, int stateId )
        {
            CurrentScope.AddJumpCase( resultLabel, continueLabel, stateId );
        }
    }
}
