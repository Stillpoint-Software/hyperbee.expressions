using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Collections;
using Hyperbee.Expressions.CompilerServices.Transitions;
using Hyperbee.Expressions.Visitors;

namespace Hyperbee.Expressions.CompilerServices.Lowering;

internal abstract class BaseLoweringVisitor : ExpressionVisitor
{
    protected readonly StateContext States = new( 4 );
    protected ExpressionMatcher ExpressionMatcher;
    protected VariableResolver VariableResolver;

    public abstract LoweringInfo Transform(
        Type resultType,
        ParameterExpression[] localVariables,
        Expression[] expressions,
        LinkedDictionary<ParameterExpression, ParameterExpression> scopedVariables = null );

    protected virtual void VisitExpressions( IEnumerable<Expression> expressions )
    {
        foreach ( var expression in expressions )
        {
            var updateNode = Visit( expression ); // Warning: visitation mutates the tail state.
            UpdateTailState( updateNode );
        }

        States.TailState.Transition = new FinalTransition();
    }

    private StateNode VisitBranch( Expression expression, StateNode joinState, Expression resultVariable = null, Action<StateNode> init = null )
    {
        // Create a new state for the branch

        var branchState = States.AddState();

        init?.Invoke( branchState );

        // Visit the branch expression

        var updateNode = Visit( expression ); // Warning: visitation mutates the tail state.

        UpdateTailState( updateNode, joinState ?? branchState ); // if no join-state, join to the branch-state (e.g. loops)

        States.TailState.Result.Variable = resultVariable;

        return branchState;
    }

    protected void UpdateTailState( Expression visited, StateNode defaultTarget = null )
    {
        var tailState = States.TailState;

        // add unhandled the expressions to the tail state

        AppendToState( tailState, visited );

        // transition handling

        if ( tailState.Transition != null )
        {
            return;
        }

        if ( visited is GotoExpression gotoExpression && States.TryGetLabelTarget( gotoExpression.Target, out var targetNode ) )
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
        return ExpressionMatcher.HasMatch( node );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    protected static ResultExpression ConvertToExpression( StateNode stateNode )
    {
        return new ResultExpression( stateNode.Result );
    }

    // Override methods for specific expression types

    protected override Expression VisitLambda<T>( Expression<T> node )
    {
        // Lambda expressions should not be lowered with this visitor.
        // But we still need to track the variables used in the lambda.
        return VariableResolver.Resolve( node );
    }

    protected override Expression VisitBlock( BlockExpression node )
    {
        if ( !RequiresLowering( node ) )
            return VariableResolver.Resolve( node );

        var joinState = States.EnterGroup( out var sourceState );

        var resultVariable = VariableResolver.GetResultVariable( node, sourceState.StateId );

        VariableResolver.AddLocalVariables( node.Variables );

        var currentSource = sourceState;
        var previousVariable = resultVariable;

        StateNode firstGoto = null;
        StateNode previousTail = null;

        var count = node.Expressions.Count;

        for ( var index = 0; index < count; index++ )
        {
            var expression = node.Expressions[index];

            if ( RequiresLowering( expression ) )
            {
                var updated =
                    VisitBranch( expression, joinState, resultVariable ); // Warning: visitation mutates the tail state.

                // handle last expression in the block
                if ( index == count - 1 )
                    previousVariable = updated.Result.Variable;

                joinState.Result.Variable = previousVariable;

                // Fix tail linked list of Transitions.
                if ( previousTail != null )
                    previousTail.Transition = new GotoTransition { TargetNode = updated };

                firstGoto ??= updated;
                currentSource = States.TailState;
                previousTail = States.TailState;
            }
            else
            {
                AppendToState( currentSource, VariableResolver.Resolve( Visit( expression ) ) );
            }
        }

        var blockTransition = new GotoTransition { TargetNode = firstGoto ?? joinState };

        sourceState.Result.Variable = previousVariable;
        joinState.Result.Value = previousVariable;

        States.ExitGroup( sourceState, blockTransition );

        return ConvertToExpression( sourceState );
    }

    protected override Expression VisitConditional( ConditionalExpression node )
    {
        if ( !RequiresLowering( node ) )
            return VariableResolver.Resolve( node );

        var updatedTest = Visit( node.Test );

        var joinState = States.EnterGroup( out var sourceState );

        var resultVariable = VariableResolver.GetResultVariable( node, sourceState.StateId );

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

        States.ExitGroup( sourceState, conditionalTransition );

        return ConvertToExpression( sourceState );
    }

    protected override Expression VisitGoto( GotoExpression node )
    {
        return VariableResolver.TryResolveLabel( node, out var label )
            ? label
            : base.VisitGoto( node );
    }

    protected override Expression VisitLoop( LoopExpression node )
    {
        if ( !RequiresLowering( node ) )
            return VariableResolver.Resolve( node );

        var joinState = States.EnterGroup( out var sourceState );

        var resultVariable = VariableResolver.GetResultVariable( node, sourceState.StateId );

        var newBody = VisitBranch( node.Body, default, resultVariable, InitializeLabels );

        var loopTransition = new LoopTransition
        {
            BodyNode = newBody, // pass default to join back to the branch-state 
            ContinueLabel = node.ContinueLabel != null ? newBody.NodeLabel : null,
            BreakLabel = node.BreakLabel != null ? joinState.NodeLabel : null,
        };

        sourceState.Result.Variable = resultVariable;
        joinState.Result.Value = resultVariable;

        States.ExitGroup( sourceState, loopTransition );

        return ConvertToExpression( sourceState );

        // Helper function for assigning loop labels

        void InitializeLabels( StateNode branchState )
        {
            VariableResolver.ResolveLabel( node.ContinueLabel, branchState.NodeLabel );
            VariableResolver.ResolveLabel( node.BreakLabel, joinState.NodeLabel );
        }
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        return VariableResolver.Resolve( node );
    }

    protected override Expression VisitSwitch( SwitchExpression node )
    {
        if ( !RequiresLowering( node ) )
            return VariableResolver.Resolve( node );

        var updatedSwitchValue = Visit( node.SwitchValue );

        var joinState = States.EnterGroup( out var sourceState );

        var resultVariable = VariableResolver.GetResultVariable( node, sourceState.StateId );

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

        States.ExitGroup( sourceState, switchTransition );

        return ConvertToExpression( sourceState );
    }

    protected override Expression VisitTry( TryExpression node )
    {
        if ( !RequiresLowering( node ) )
            return VariableResolver.Resolve( node );

        var joinState = States.EnterGroup( out var sourceState );

        var resultVariable = VariableResolver.GetResultVariable( node, sourceState.StateId );
        var tryStateVariable = VariableResolver.GetTryVariable( sourceState.StateId );
        var exceptionVariable = VariableResolver.GetExceptionVariable( sourceState.StateId );

        // if there is a finally block then that is the join for a try/catch.

        StateNode finalExpression = null;

        if ( node.Finally != null )
        {
            finalExpression = VisitBranch( node.Finally, joinState );
            joinState = finalExpression;
        }

        var nodeScope = States.EnterTryScope( sourceState );

        var tryCatchTransition = new TryCatchTransition
        {
            TryStateVariable = tryStateVariable,
            ExceptionVariable = exceptionVariable,
            TryNode = VisitBranch( node.Body, joinState, resultVariable ),
            FinallyNode = finalExpression,
            StateScope = nodeScope,
            Scopes = States.Scopes
        };

        States.ExitTryScope();

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

        States.ExitGroup( sourceState, tryCatchTransition );

        return ConvertToExpression( sourceState );
    }

    protected sealed class ResultExpression( StateResult result ) : Expression
    {
        public override ExpressionType NodeType => ExpressionType.Extension;
        public override Type Type => result.Variable?.Type ?? typeof( void );
        public override bool CanReduce => true;

        public override Expression Reduce() => result.Variable ?? Empty();
    }
}
