using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.Transformation.Transitions;

namespace Hyperbee.Expressions.Transformation;

internal class LoweringVisitor : ExpressionVisitor
{
    private const int InitialCapacity = 8;

    private ParameterExpression _returnValue;
    private ParameterExpression[] _definedVariables;
    private readonly Dictionary<int, ParameterExpression> _variables = new( InitialCapacity );
    private int _awaitCount;

    private readonly StateContext _states = new( InitialCapacity );
    private readonly Dictionary<LabelTarget, Expression> _labels = [];

    private int _variableId;

    private static class VariableName
    {
        // use special names to prevent collisions
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static string Awaiter( int stateId ) => $"__awaiter<{stateId}>";

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static string Result( int stateId ) => $"__result<{stateId}>";

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static string Try( int stateId ) => $"__try<{stateId}>";

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static string Exception( int stateId ) => $"__ex<{stateId}>";

        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static string Variable( string name, int stateId, ref int variableId ) => $"__{name}<{stateId}_{variableId++}>";

        public const string Return = "return<>";
    }

    public LoweringResult Transform( ParameterExpression[] variables, params Expression[] expressions )
    {
        _definedVariables = variables;

        VisitExpressions( expressions );

        return new LoweringResult
        {
            Scopes = _states.Scopes,
            ReturnValue = _returnValue,
            AwaitCount = _awaitCount,
            Variables = _variables.Select( x => x.Value ).ToArray()
        };
    }

    public LoweringResult Transform( params Expression[] expressions )
    {
        return Transform( [], expressions );
    }

    // Visit methods

    private NodeExpression VisitBranch( Expression expression, NodeExpression joinState,
        ParameterExpression resultVariable = null,
        Action<NodeExpression> init = null )
    {
        // Create a new state for the branch

        var branchState = _states.AddState();

        init?.Invoke( branchState );

        // Visit the branch expression

        var updateNode = Visit( expression ); // Warning: visitation mutates the tail state.

        UpdateTailState( expression, updateNode, joinState ?? branchState ); // if no join-state, join to the branch-state (e.g. loops)

        _states.TailState.ResultVariable = resultVariable;

        return branchState;
    }

    private void VisitExpressions( IEnumerable<Expression> expressions )
    {
        foreach ( var expression in expressions )
        {
            var updateNode = Visit( expression ); // Warning: visitation mutates the tail state.
            UpdateTailState( expression, updateNode );
        }
    }

    private void UpdateTailState( Expression expression, Expression visited, NodeExpression defaultTransitionTarget = null )
    {
        var tailState = _states.TailState;

        if ( !IsExplicitlyHandledType( expression ) )
        {
            // goto expressions should _never_ be added to the expressions list.
            // instead, they should always be represented as a transition.
            //
            // goto expressions should set the transition - the first goto should win

            if ( tailState.Transition == null && visited is GotoExpression gotoExpression )
            {
                var targetNode = _states.Nodes.FirstOrDefault( x => x.NodeLabel == gotoExpression.Target );

                if ( targetNode != null )
                {
                    tailState.Transition = new GotoTransition { TargetNode = targetNode };
                }
            }
            else
            {
                tailState.Expressions.Add( visited );
            }
        }

        // default transition handling

        if ( tailState.Transition == null && defaultTransitionTarget != null )
        {
            tailState.Transition = new GotoTransition { TargetNode = defaultTransitionTarget };
        }
    }

    private static bool IsExplicitlyHandledType( Expression expr )
    {
        // These expression types are explicitly handled by the visitor.

        return expr
            is BlockExpression
            or ConditionalExpression
            or SwitchExpression
            or TryExpression
            or AwaitExpression
            or AsyncBlockExpression
            or LoopExpression;
    }

    // Override methods for specific expression types

    protected override Expression VisitBlock( BlockExpression node )
    {
        VisitExpressions( node.Expressions );
        return node;
    }

    protected override Expression VisitConditional( ConditionalExpression node )
    {
        var updatedTest = base.Visit( node.Test );

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

        return sourceState;
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

    protected override Expression VisitLoop( LoopExpression node )
    {
        var joinState = _states.EnterGroup( out var sourceState );

        var resultVariable = GetResultVariable( node, sourceState.StateId );

        var loopTransition = new LoopTransition
        {
            BodyNode = VisitBranch( node.Body, default, resultVariable, InitializeLabels ) // pass default to join back to the branch-state 
        };

        sourceState.ResultVariable = resultVariable;
        joinState.ResultValue = resultVariable;

        _states.ExitGroup( sourceState, loopTransition );

        return sourceState;

        // Helper function for fixing loop labels
        void InitializeLabels( NodeExpression branchState )
        {
            if ( node.ContinueLabel != null )
                _labels[node.ContinueLabel] = Expression.Goto( branchState.NodeLabel );

            if ( node.BreakLabel != null )
                _labels[node.BreakLabel] = Expression.Goto( joinState.NodeLabel );
        }
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        if ( !_definedVariables.Contains( node ) )
            return base.VisitParameter( node );

        var hash = node.GetHashCode();

        if ( _variables.TryGetValue( hash, out var existingNode ) )
            return existingNode;

        var updateNode = Expression.Parameter(
            node.Type,
            VariableName.Variable( node.Name, _states.TailState.StateId, ref _variableId ) );

        _variables[hash] = updateNode;

        return updateNode;
    }

    protected override Expression VisitSwitch( SwitchExpression node )
    {
        var updatedSwitchValue = base.Visit( node.SwitchValue );

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

        return sourceState;
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
            // non-zero index for catch states to avoid conflicts
            // with default catch state value (zero).

            var catchState = index + 1;

            var catchBlock = node.Handlers[index];
            tryCatchTransition.AddCatchBlock(
                catchBlock,
                VisitBranch( catchBlock.Body, joinState ),
                catchState );
        }

        sourceState.ResultVariable = resultVariable;
        joinState.ResultValue = resultVariable;

        _states.ExitGroup( sourceState, tryCatchTransition );

        return sourceState;
    }

    // Override method for extension expression types

    protected override Expression VisitBinary( BinaryExpression node )
    {
        var updatedLeft = Visit( node.Left );
        var updatedRight = Visit( node.Right );

        if ( updatedRight is NodeExpression nodeExpression )
        {
            return node.Update( updatedLeft, node.Conversion, nodeExpression.ResultVariable );
        }

        return node.Update( updatedLeft, node.Conversion, updatedRight );
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

    protected static Expression VisitAsyncBlock( AsyncBlockExpression node )
    {
        return node.Reduce();
    }

    protected Expression VisitAwait( AwaitExpression node )
    {
        var updatedNode = Visit( node.Target );

        var joinState = _states.EnterGroup( out var sourceState );

        var resultVariable = GetResultVariable( node, sourceState.StateId );

        var completionState = _states.AddState();
        _states.TailState.ResultVariable = resultVariable;

        _awaitCount++;

        var awaitBinder = node.GetAwaitBinder();

        var awaiterVariable = CreateVariable(
            awaitBinder.GetAwaiterMethod.ReturnType,
            VariableName.Awaiter( sourceState.StateId )
        );

        completionState.Transition = new AwaitResultTransition
        {
            TargetNode = joinState,
            AwaiterVariable = awaiterVariable,
            ResultVariable = resultVariable,
            AwaitBinder = awaitBinder
        };

        _states.AddJumpCase( completionState.NodeLabel, joinState.NodeLabel, sourceState.StateId );

        // If we already visited a branching node we only want to use the result variable
        // else it is most likely direct awaitable (e.g. Task)
        var targetNode = updatedNode is NodeExpression nodeExpression
            ? nodeExpression.ResultVariable
            : updatedNode;

        var awaitTransition = new AwaitTransition
        {
            Target = targetNode,
            StateId = sourceState.StateId,
            AwaiterVariable = awaiterVariable,
            CompletionNode = completionState,
            AwaitBinder = awaitBinder,
            ConfigureAwait = node.ConfigureAwait
        };

        sourceState.ResultVariable = resultVariable;
        joinState.ResultValue = resultVariable;

        _states.ExitGroup( sourceState, awaitTransition );

        return (Expression) resultVariable ?? Expression.Empty();
    }

    // Helpers

    private ParameterExpression GetResultVariable( Expression node, int stateId )
    {
        if ( node.Type == typeof( void ) )
            return null;

        ParameterExpression returnVariable =
            Expression.Parameter( node.Type, VariableName.Result( stateId ) );
        _variables[returnVariable.GetHashCode()] = returnVariable;

        return returnVariable;
    }

    private ParameterExpression CreateVariable( Type type, string name )
    {
        var variable = Expression.Variable( type, name );
        _variables[variable.GetHashCode()] = variable;
        return variable;
    }

    // State management

    private sealed class StateContext
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

        public IReadOnlyList<NodeExpression> Nodes => CurrentScope.Nodes;

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
