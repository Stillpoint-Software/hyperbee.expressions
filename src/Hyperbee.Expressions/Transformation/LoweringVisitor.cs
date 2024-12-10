using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.Transformation.Transitions;
using Hyperbee.Expressions.Visitors;

namespace Hyperbee.Expressions.Transformation;

internal class LoweringVisitor : ExpressionVisitor
{
    private const int InitialCapacity = 4;

    private ParameterExpression _finalResultVariable;
    private bool _hasFinalResultVariable;

    private int _awaitCount;

    private readonly StateContext _states = new( InitialCapacity );
    private readonly ExpressionMatcher _expressionMatcher = new( expr => expr is AwaitExpression || expr is AsyncBlockExpression );

    private VariableResolver _variableResolver;

    public LoweringInfo Transform( Type resultType, ParameterExpression[] variables, Expression[] expressions, ParameterExpression[] externVariables )
    {
        ArgumentNullException.ThrowIfNull( expressions, nameof( expressions ) );
        ArgumentOutOfRangeException.ThrowIfZero( expressions.Length, nameof( expressions ) );

        _variableResolver = new VariableResolver( variables, _states );
        _finalResultVariable = CreateFinalResultVariable( resultType, _variableResolver );

        VisitExpressions( expressions );

        StateOptimizer.Optimize( _states );

        ThrowIfInvalid();

        return new LoweringInfo
        {
            Scopes = _states.Scopes,
            HasFinalResultVariable = _hasFinalResultVariable,
            AwaitCount = _awaitCount,
            Variables = _variableResolver.GetMappedVariables(),
            ExternVariables = externVariables
        };

        // helpers

        void ThrowIfInvalid()
        {
            if ( _states.Scopes[0].States.Count == 0 )
                throw new LoweringException( $"Evaluation of the {nameof( expressions )} parameter resulted in empty states." );

            if ( _awaitCount == 0 )
                throw new LoweringException( $"The {nameof( expressions )} parameter must contain at least one awaitable." );
        }

        static ParameterExpression CreateFinalResultVariable( Type resultType, VariableResolver resolver )
        {
            var finalResultType = resultType == typeof( void )
                ? typeof( IVoidResult )
                : resultType;

            return resolver.GetFinalResult( finalResultType );
        }
    }

    // Visit methods

    private void VisitExpressions( IEnumerable<Expression> expressions )
    {
        foreach ( var expression in expressions )
        {
            var updateNode = Visit( expression ); // Warning: visitation mutates the tail state.
            UpdateTailState( updateNode );
        }

        // update the final state

        if ( !_hasFinalResultVariable )
        {
            // assign the final result variable if not already assigned for state-machine builder.
            // this is the case when the last expression is not a return statement.
            // this will ensure that the state-machine builder will have a final result field.

            _states.TailState.Result.Variable = _finalResultVariable;
        }

        _states.TailState.Transition = new FinalTransition();
    }

    private StateNode VisitBranch( Expression expression, StateNode joinState, Expression resultVariable = null, Action<StateNode> init = null )
    {
        // Create a new state for the branch

        var branchState = _states.AddState();

        init?.Invoke( branchState );

        // Visit the branch expression

        var updateNode = Visit( expression ); // Warning: visitation mutates the tail state.

        UpdateTailState( updateNode, joinState ?? branchState ); // if no join-state, join to the branch-state (e.g. loops)

        _states.TailState.Result.Variable = resultVariable;

        return branchState;
    }

    private void UpdateTailState( Expression visited, StateNode defaultTarget = null )
    {
        var tailState = _states.TailState;

        // add unhandled the expressions to the tail state

        AppendToState( tailState, visited );

        // transition handling

        if ( tailState.Transition != null )
        {
            return;
        }

        if ( visited is GotoExpression gotoExpression && _states.TryGetLabelTarget( gotoExpression.Target, out var targetNode ) )
        {
            tailState.Transition = new GotoTransition { TargetNode = targetNode };
        }

        if ( defaultTarget != null )
        {
            tailState.Transition = new GotoTransition { TargetNode = defaultTarget };
        }
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    private static void AppendToState( StateNode targetState, Expression value )
    {
        if ( value is not ResultExpression )
            targetState.Expressions.Add( value );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    private bool RequiresLowering( Expression node )
    {
        return _expressionMatcher.HasMatch( node );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    private static ResultExpression ConvertToExpression( StateNode stateNode )
    {
        return new ResultExpression( stateNode.Result );
    }

    // Override methods for specific expression types

    protected override Expression VisitLambda<T>( Expression<T> node )
    {
        // Lambda expressions should not be lowered with this visitor.
        // But we still need to track the variables used in the lambda.
        return _variableResolver.Resolve( node );
    }

    protected override Expression VisitBlock( BlockExpression node )
    {
        if ( !RequiresLowering( node ) )
            return _variableResolver.Resolve( node );

        var joinState = _states.EnterGroup( out var sourceState );

        var resultVariable = _variableResolver.GetResultVariable( node, sourceState.StateId );

        _variableResolver.AddLocalVariables( node.Variables );

        var currentSource = sourceState;
        var previousVariable = resultVariable;

        StateNode firstGoto = null;
        StateNode previousTail = null;

        foreach ( var expression in node.Expressions )
        {
            if ( RequiresLowering( expression ) )
            {
                var updated = VisitBranch( expression, joinState, resultVariable ); // Warning: visitation mutates the tail state.

                previousVariable = updated.Result.Variable;
                joinState.Result.Variable = previousVariable;

                // Fix tail link list of Transitions.
                if ( previousTail != null )
                    previousTail.Transition = new GotoTransition { TargetNode = updated };

                firstGoto ??= updated;
                currentSource = _states.TailState;
                previousTail = _states.TailState;
            }
            else
            {
                AppendToState( currentSource, _variableResolver.Resolve( Visit( expression ) ) );
            }
        }

        var blockTransition = new GotoTransition { TargetNode = firstGoto ?? joinState };

        sourceState.Result.Variable = previousVariable;
        joinState.Result.Value = previousVariable;

        _states.ExitGroup( sourceState, blockTransition );

        return ConvertToExpression( sourceState );
    }

    protected override Expression VisitConditional( ConditionalExpression node )
    {
        if ( !RequiresLowering( node ) )
            return _variableResolver.Resolve( node );

        var updatedTest = Visit( node.Test );

        var joinState = _states.EnterGroup( out var sourceState );

        var resultVariable = _variableResolver.GetResultVariable( node, sourceState.StateId );

        var conditionalTransition = new ConditionalTransition
        {
            Test = updatedTest,
            IfTrue = VisitBranch( node.IfTrue, joinState, resultVariable ),
            IfFalse = node.IfFalse is not DefaultExpression
                ? VisitBranch( node.IfFalse, joinState, resultVariable )
                : joinState,
        };

        sourceState.Result.Variable = resultVariable;
        joinState.Result.Value = resultVariable;

        _states.ExitGroup( sourceState, conditionalTransition );

        return ConvertToExpression( sourceState );
    }

    protected override Expression VisitGoto( GotoExpression node )
    {
        if ( _variableResolver.TryResolveLabel( node, out var label ) )
            return label;

        var updateNode = base.VisitGoto( node );

        if ( updateNode is not GotoExpression { Kind: GotoExpressionKind.Return } gotoExpression )
            return updateNode;

        _hasFinalResultVariable = true;

        return Expression.Assign( _finalResultVariable, gotoExpression.Value! );
    }

    protected override Expression VisitLoop( LoopExpression node )
    {
        if ( !RequiresLowering( node ) )
            return _variableResolver.Resolve( node );

        var joinState = _states.EnterGroup( out var sourceState );

        var resultVariable = _variableResolver.GetResultVariable( node, sourceState.StateId );

        var newBody = VisitBranch( node.Body, default, resultVariable, InitializeLabels );

        var loopTransition = new LoopTransition
        {
            BodyNode = newBody, // pass default to join back to the branch-state 
            ContinueLabel = node.ContinueLabel != null ? newBody.NodeLabel : null,
            BreakLabel = node.BreakLabel != null ? joinState.NodeLabel : null,
        };

        sourceState.Result.Variable = resultVariable;
        joinState.Result.Value = resultVariable;

        _states.ExitGroup( sourceState, loopTransition );

        return ConvertToExpression( sourceState );

        // Helper function for assigning loop labels

        void InitializeLabels( StateNode branchState )
        {
            _variableResolver.ResolveLabel( node.ContinueLabel, branchState.NodeLabel );
            _variableResolver.ResolveLabel( node.BreakLabel, joinState.NodeLabel );
        }
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        return _variableResolver.Resolve( node );
    }

    protected override Expression VisitSwitch( SwitchExpression node )
    {
        if ( !RequiresLowering( node ) )
            return _variableResolver.Resolve( node );

        var updatedSwitchValue = Visit( node.SwitchValue );

        var joinState = _states.EnterGroup( out var sourceState );

        var resultVariable = _variableResolver.GetResultVariable( node, sourceState.StateId );

        var switchTransition = new SwitchTransition { SwitchValue = updatedSwitchValue };

        if ( node.DefaultBody != null )
        {
            switchTransition.DefaultNode = VisitBranch( node.DefaultBody, joinState, resultVariable );
        }

        foreach ( var switchCase in node.Cases )
        {
            switchTransition.AddSwitchCase(
                [.. switchCase.TestValues],
                VisitBranch( switchCase.Body, joinState, resultVariable )
            );
        }

        sourceState.Result.Variable = resultVariable;
        joinState.Result.Value = resultVariable;

        _states.ExitGroup( sourceState, switchTransition );

        return ConvertToExpression( sourceState );
    }

    protected override Expression VisitTry( TryExpression node )
    {
        if ( !RequiresLowering( node ) )
            return _variableResolver.Resolve( node );

        var joinState = _states.EnterGroup( out var sourceState );

        var resultVariable = _variableResolver.GetResultVariable( node, sourceState.StateId );
        var tryStateVariable = _variableResolver.GetTryVariable( sourceState.StateId );
        var exceptionVariable = _variableResolver.GetExceptionVariable( sourceState.StateId );

        // if there is a finally block then that is the join for a try/catch.

        StateNode finalExpression = null;

        if ( node.Finally != null )
        {
            finalExpression = VisitBranch( node.Finally, joinState );
            joinState = finalExpression;
        }

        var nodeScope = _states.EnterScope( sourceState );

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
            // use a non-zero based index for catch states to avoid
            // conflicts with default catch state value (zero).

            var catchState = index + 1;
            var catchBlock = node.Handlers[index];

            tryCatchTransition.AddCatchBlock(
                catchBlock,
                VisitBranch( catchBlock.Body, joinState ),
                catchState );
        }

        sourceState.Result.Variable = resultVariable;
        joinState.Result.Value = resultVariable;

        _states.ExitGroup( sourceState, tryCatchTransition );

        return ConvertToExpression( sourceState );
    }

    // Override method for extension expression types

    protected override Expression VisitExtension( Expression node )
    {
        return node switch
        {
            AwaitExpression awaitExpression => VisitAwaitExtension( awaitExpression ),

            // Nested async blocks should be visited by their own visitor,
            // but nested variables must be replaced
            AsyncBlockExpression => _variableResolver.Resolve( node ),

            // Lowering visitor shouldn't be used by extensions directly
            // since it changes the shape of the code
            _ => Visit( node.Reduce() )
        };
    }

    protected Expression VisitAwaitExtension( AwaitExpression node )
    {
        var updatedNode = Visit( node.Target );

        var joinState = _states.EnterGroup( out var sourceState );

        var resultVariable = _variableResolver.GetResultVariable( node, sourceState.StateId );
        var completionState = _states.AddState();
        _states.TailState.Result.Variable = resultVariable;

        _awaitCount++;

        var awaitBinder = node.GetAwaitBinder();

        var awaiterVariable = _variableResolver.GetAwaiterVariable(
            awaitBinder.GetAwaiterMethod.ReturnType,
            sourceState.StateId
        );

        completionState.Transition = new AwaitResultTransition
        {
            TargetNode = joinState,
            AwaiterVariable = awaiterVariable,
            ResultVariable = resultVariable,
            AwaitBinder = awaitBinder
        };

        _states.AddJumpCase( completionState.NodeLabel, joinState.NodeLabel, sourceState.StateId );

        var awaitTransition = new AwaitTransition
        {
            Target = updatedNode,
            StateId = sourceState.StateId,
            AwaiterVariable = awaiterVariable,
            CompletionNode = completionState,
            AwaitBinder = awaitBinder,
            ConfigureAwait = node.ConfigureAwait
        };

        sourceState.Result.Variable = resultVariable;
        joinState.Result.Value = resultVariable;

        _states.ExitGroup( sourceState, awaitTransition );

        return ConvertToExpression( sourceState );
    }

    private sealed class ResultExpression( StateResult result ) : Expression
    {
        public override ExpressionType NodeType => ExpressionType.Extension;
        public override Type Type => result.Variable?.Type ?? typeof( void );
        public override bool CanReduce => true;

        public override Expression Reduce() => result.Variable ?? Empty();
    }
}
