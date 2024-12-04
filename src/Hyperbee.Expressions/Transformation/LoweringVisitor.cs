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
        _variableResolver = new VariableResolver( variables, _states );
        _finalResultVariable = CreateFinalResultVariable( resultType, _variableResolver );

        VisitExpressions( expressions );

        return new LoweringInfo
        {
            Scopes = _states.Scopes,
            HasFinalResultVariable = _hasFinalResultVariable,
            AwaitCount = _awaitCount,
            Variables = _variableResolver.GetMappedVariables(),
            ExternVariables = externVariables
        };

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

        var tailState = _states.TailState;

        if ( !_hasFinalResultVariable )
        {
            // assign the final result variable if not already assigned for state-machine builder.
            // this is the case when the last expression is not a return statement.
            // this will ensure that the state-machine builder will have a final result field.

            tailState.Result.Variable = _finalResultVariable;
        }

        tailState.Transition = new FinalTransition();
    }

    private StateExpression VisitBranch( Expression expression, StateExpression joinState, Expression resultVariable = null, Action<StateExpression> init = null )
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

    private void UpdateTailState( Expression visited, StateExpression defaultTarget = null )
    {
        var tailState = _states.TailState;

        // add unhandled the expressions to the tail state

        if ( visited is not StateExpression )
        {
            tailState.Expressions.Add( visited );
        }

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
    private bool RequiresLowering( Expression node )
    {
        return _expressionMatcher.HasMatch( node );
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
        StateExpression firstGoto = null;

        StateExpression previousTail = null;
        Expression previousVariable = resultVariable;

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
                currentSource.Expressions.Add( _variableResolver.Resolve( Visit( expression ) ) );
            }
        }

        var blockTransition = new GotoTransition { TargetNode = firstGoto ?? joinState };

        sourceState.Result.Variable = previousVariable;
        joinState.Result.Value = previousVariable;

        _states.ExitGroup( sourceState, blockTransition );

        return sourceState;
    }

    protected override Expression VisitConditional( ConditionalExpression node )
    {
        if ( !RequiresLowering( node ) )
            return _variableResolver.Resolve( node );

        var updatedTest = base.Visit( node.Test );

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

        return sourceState;
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

        return sourceState;

        // Helper function for assigning loop labels
        void InitializeLabels( StateExpression branchState )
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

        var updatedSwitchValue = base.Visit( node.SwitchValue );

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

        return sourceState;
    }

    protected override Expression VisitTry( TryExpression node )
    {
        if ( !RequiresLowering( node ) )
            return _variableResolver.Resolve( node );

        var joinState = _states.EnterGroup( out var sourceState );

        var resultVariable = _variableResolver.GetResultVariable( node, sourceState.StateId );
        var tryStateVariable = _variableResolver.GetTryVariable( sourceState.StateId );
        var exceptionVariable = _variableResolver.GetExceptionVariable( sourceState.StateId );

        // If there is a finally block then that is the join for a try/catch.
        StateExpression finalExpression = null;

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

        return sourceState;
    }

    protected override Expression VisitBinary( BinaryExpression node )
    {
        var updatedLeft = Visit( node.Left );
        var updatedRight = Visit( node.Right );

        if ( updatedRight is not StateExpression nodeExpression )
        {
            return node.Update( updatedLeft, node.Conversion, updatedRight );
        }

        return node.Update( updatedLeft, node.Conversion, nodeExpression.Result.Variable );
    }

    // Override method for extension expression types

    protected override Expression VisitExtension( Expression node )
    {
        switch ( node )
        {
            case AwaitExpression awaitExpression:
                return VisitAwaitExtension( awaitExpression );

            case AsyncBlockExpression:
                // Nested blocks should be visited by their own visitor,
                // but nested variables must be replaced
                return _variableResolver.Resolve( node );

            default:
                // Lowering visitor shouldn't be used by extensions directly
                // since it changes the shape of the code
                return Visit( node.Reduce() );
        }
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

        // If we already visited a branching node we only want to use the result variable
        // else it is most likely directly awaitable (e.g. Task)

        var targetNode = updatedNode is StateExpression nodeExpression
            ? nodeExpression.Result.Variable
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

        sourceState.Result.Variable = resultVariable;
        joinState.Result.Value = resultVariable;

        _states.ExitGroup( sourceState, awaitTransition );

        return resultVariable ?? Expression.Empty();
    }
}
