using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.Transformation.Transitions;

namespace Hyperbee.Expressions.Transformation;

public class LoweringVisitor : ExpressionVisitor
{
    private const int InitialCapacity = 4;

    private ParameterExpression _returnValue;

    private int _awaitCount;

    private readonly StateContext _states = new( InitialCapacity );
    private readonly Dictionary<LabelTarget, Expression> _labels = [];

    private VariableResolver _variableResolver;

    public LoweringResult Transform( ParameterExpression[] variables, Expression[] expressions )
    {
        _variableResolver = new VariableResolver( variables, _states );

        VisitExpressions( expressions );

        return new LoweringResult
        {
            Scopes = _states.Scopes,
            ReturnValue = _returnValue,
            AwaitCount = _awaitCount,
            Variables = _variableResolver.GetLocalVariables()
        };
    }

    internal LoweringResult Transform( ReadOnlyCollection<ParameterExpression> variables, IReadOnlyCollection<Expression> expressions )
    {
        return Transform( [.. variables], [.. expressions] );
    }

    public LoweringResult Transform( params Expression[] expressions )
    {
        return Transform( [], expressions );
    }

    // Visit methods

    private NodeExpression VisitBranch( Expression expression, NodeExpression joinState,
        Expression resultVariable = null,
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
                if ( _states.TryGetLabelTarget( gotoExpression.Target, out var targetNode ) )
                {
                    tailState.Transition = new GotoTransition { TargetNode = targetNode };
                }
            }
            else if ( visited is not NodeExpression ) // TODO: Not adding NodeExpression seems like a hack
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

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    private static bool IsExplicitlyHandledType( Expression expr )
    {
        // These expression types are explicitly handled by the visitor.

        return expr
            is BlockExpression
            or ConditionalExpression
            or SwitchExpression
            or TryExpression
            or AwaitExpression
            or LoopExpression;
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
        var joinState = _states.EnterGroup( out var sourceState );

        var resultVariable = _variableResolver.GetResultVariable( node, sourceState.StateId );

        _variableResolver.AddLocalVariables( node.Variables );

        var currentSource = sourceState;
        NodeExpression firstGoto = null;

        NodeExpression previousTail = null;
        Expression previousVariable = resultVariable;

        foreach ( var expression in node.Expressions )
        {
            var handlingVisitor = new HandlingVisitor();
            handlingVisitor.Visit( expression );

            if ( handlingVisitor.Handled )
            {
                var updated = VisitBranch( expression, joinState, resultVariable ); // Warning: visitation mutates the tail state.

                previousVariable = updated.ResultVariable;
                joinState.ResultVariable = previousVariable;

                // Fix tail link list of Transitions.
                if ( previousTail != null )
                    previousTail.Transition = new GotoTransition { TargetNode = updated };

                firstGoto ??= updated;
                currentSource = _states.TailState; // updated;
                previousTail = _states.TailState;
            }
            else
            {
                currentSource.Expressions.Add( _variableResolver.Resolve( Visit( expression ) ) );
            }

        }

        var blockTransition = new GotoTransition { TargetNode = firstGoto ?? joinState };

        sourceState.ResultVariable = previousVariable;
        joinState.ResultValue = previousVariable;

        _states.ExitGroup( sourceState, blockTransition );

        return sourceState;
    }

    protected override Expression VisitConditional( ConditionalExpression node )
    {
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

        _returnValue ??= _variableResolver.GetReturnVariable( gotoExpression.Value!.Type );

        return Expression.Assign( _returnValue, gotoExpression.Value! );
    }

    protected override Expression VisitLoop( LoopExpression node )
    {
        var joinState = _states.EnterGroup( out var sourceState );

        var resultVariable = _variableResolver.GetResultVariable( node, sourceState.StateId );

        var newBody = VisitBranch( node.Body, default, resultVariable, InitializeLabels );

        var loopTransition = new LoopTransition
        {
            BodyNode = newBody, // pass default to join back to the branch-state 
            ContinueLabel = node.ContinueLabel != null ? newBody.NodeLabel : null,
            BreakLabel = node.BreakLabel != null ? joinState.NodeLabel : null,
        };

        sourceState.ResultVariable = resultVariable;
        joinState.ResultValue = resultVariable;

        _states.ExitGroup( sourceState, loopTransition );

        return sourceState;

        // Helper function for assigning loop labels
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
        return _variableResolver.Resolve( node );
    }

    protected override Expression VisitSwitch( SwitchExpression node )
    {
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

        var resultVariable = _variableResolver.GetResultVariable( node, sourceState.StateId );
        var tryStateVariable = _variableResolver.GetTryVariable( sourceState.StateId );
        var exceptionVariable = _variableResolver.GetExceptionVariable( sourceState.StateId );

        // If there is a finally block then that is the join for a try/catch.
        NodeExpression finalExpression = null;

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

        sourceState.ResultVariable = resultVariable;
        joinState.ResultValue = resultVariable;

        _states.ExitGroup( sourceState, tryCatchTransition );

        return sourceState;
    }

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

    // Override method for extension expression types

    protected override Expression VisitExtension( Expression node )
    {
        switch ( node )
        {
            case AwaitExpression awaitExpression:
                return VisitAwaitExtension( awaitExpression );

            case AsyncBlockExpression asyncBlockExpression:
                return VisitAsyncBlockExtension( asyncBlockExpression );

            default:
                return VisitDefaultExtension( node );
        }
    }

    protected Expression VisitAsyncBlockExtension( AsyncBlockExpression node )
    {
        // Nested blocks should be visited by their own visitor,
        // but nested variables need to be replaced

        return _variableResolver.Resolve( node );
    }

    protected Expression VisitAwaitExtension( AwaitExpression node )
    {
        var updatedNode = Visit( node.Target );

        var joinState = _states.EnterGroup( out var sourceState );

        var resultVariable = _variableResolver.GetResultVariable( node, sourceState.StateId );
        var completionState = _states.AddState();
        _states.TailState.ResultVariable = resultVariable;

        _awaitCount++;

        var awaitBinder = node.GetAwaitBinder();

        var awaiterVariable = _variableResolver.GetAwaiterVariable(
            awaitBinder.GetAwaiterMethod.ReturnType,
            sourceState.StateId
        );

        completionState.Transition = new AwaitResultTransition { TargetNode = joinState, AwaiterVariable = awaiterVariable, ResultVariable = resultVariable, AwaitBinder = awaitBinder };

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

        return resultVariable ?? Expression.Empty();
    }

    protected Expression VisitDefaultExtension( Expression node )
    {
        // Lowering visitor shouldn't be used by extensions directly
        // since it changes the shape of the code

        var reduced = node.Reduce();
        var resolved = _variableResolver.Resolve( reduced );

        var updatedExpression = Visit( resolved );

        // TODO: not sure if this is always valid, might help with clean up of NodeExpression's ReduceFinalBlock()

        if ( updatedExpression is NodeExpression nodeExpression )
            return nodeExpression.ResultVariable ?? nodeExpression;

        return node;
    }

    // Helpers

    internal class HandlingVisitor : ExpressionVisitor
    {
        public bool Handled { get; set; }

        public override Expression Visit( Expression node )
        {
            if ( !IsHandled( node ) )
                return base.Visit( node );

            Handled = true;
            return node;
        }

        private static bool IsHandled( Expression expr )
        {
            return expr
                is BlockExpression
                or ConditionalExpression
                or SwitchExpression
                or TryExpression
                or AwaitExpression
                or LoopExpression;

            // TODO: would like to only lower if async/await exists and ignore internal lower,
            //       there seems to be issues with hoisting and it's hacky reduce

            // TODO: should also look into caching and quick return if already handled
            //return expr is AwaitExpression;
        }

    }

}
