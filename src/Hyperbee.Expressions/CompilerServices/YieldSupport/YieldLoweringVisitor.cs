using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Collections;
using Hyperbee.Expressions.CompilerServices.Transitions;
using Hyperbee.Expressions.Visitors;

namespace Hyperbee.Expressions.CompilerServices.YieldSupport;



/*
 *  RULES:
 *
 *  - Every "yield break" is a
 *    1. return false
 *
 *  - Every "yield return <value>" is a
 *    1. Set state to next state or -1
 *    2. Set current to value
 *    3. return true
 *
 *  - Every parameter expressions are replaced with a field in the state machine
 *
 *  - All arguments to the lambda are fields and initialized in the replacement body
 *
 *  - Each yield return <value> is a new state
 *  - Need a JumpTable for each state
 * 
 *
 *  NOT ALLOWED:
 *  - yields in Try/Catch/Finally
 *  - yields in lambdas (This may mean we don't have nested scope complexity)
 */


internal class YieldLoweringVisitor : ExpressionVisitor
{
    private const int InitialCapacity = 4;

    private readonly StateContext _states = new( InitialCapacity );
    private readonly ExpressionMatcher _expressionMatcher = new( expr => expr is YieldExpression || expr is YieldBlockExpression );

    private YieldVariableResolver _variableResolver;

    public LoweringInfo Transform(
        Type resultType,
        ParameterExpression[] localVariables,
        Expression[] expressions,
        LinkedDictionary<ParameterExpression, ParameterExpression> scopedVariables = null )
    {
        ArgumentNullException.ThrowIfNull( expressions, nameof( expressions ) );
        ArgumentOutOfRangeException.ThrowIfZero( expressions.Length, nameof( expressions ) );

        _variableResolver = new YieldVariableResolver( localVariables, scopedVariables, _states );

        VisitExpressions( expressions );

        StateOptimizer.Optimize( _states );

        ThrowIfInvalid();

        return new LoweringInfo
        {
            Scopes = _states.Scopes,
            ScopedVariables = scopedVariables
        };

        // helpers

        void ThrowIfInvalid()
        {
            if ( _states.Scopes[0].States.Count == 0 )
                throw new LoweringException( $"Evaluation of the {nameof( expressions )} parameter resulted in empty states." );
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

        // Add Tail as last state

        _states.AddJumpCase( _states.TailState.NodeLabel, _states.TailState.StateId );

        _states.TailState.Transition = new FinalTransition();
    }

    private StateNode VisitBranch( Expression expression, StateNode joinState/*, Expression resultVariable = null*/, Action<StateNode> init = null )
    {
        // Create a new state for the branch

        var branchState = _states.AddState();

        init?.Invoke( branchState );

        // Visit the branch expression

        var updateNode = Visit( expression ); // Warning: visitation mutates the tail state.

        UpdateTailState( updateNode, joinState ?? branchState ); // if no join-state, join to the branch-state (e.g. loops)

        //_states.TailState.Result.Variable = resultVariable;

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

        //var resultVariable = _variableResolver.GetResultVariable( node, sourceState.StateId );

        _variableResolver.AddLocalVariables( node.Variables );

        var currentSource = sourceState;
        //var previousVariable = resultVariable;

        StateNode firstGoto = null;
        StateNode previousTail = null;

        foreach ( var expression in node.Expressions )
        {
            if ( RequiresLowering( expression ) )
            {
                var updated = VisitBranch( expression, joinState ); // Warning: visitation mutates the tail state.

                //previousVariable = updated.Result.Variable;
                //joinState.Result.Variable = previousVariable;

                // Fix tail linked list of Transitions.
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

        //sourceState.Result.Variable = previousVariable;
        //joinState.Result.Value = previousVariable;

        _states.ExitGroup( sourceState, blockTransition );

        return ConvertToExpression( sourceState );
    }

    protected override Expression VisitConditional( ConditionalExpression node )
    {
        if ( !RequiresLowering( node ) )
            return _variableResolver.Resolve( node );

        var updatedTest = Visit( node.Test );

        var joinState = _states.EnterGroup( out var sourceState );

        //var resultVariable = _variableResolver.GetResultVariable( node, sourceState.StateId );

        var conditionalTransition = new ConditionalTransition
        {
            Test = updatedTest,
            IfTrue = VisitBranch( node.IfTrue, joinState ),
            IfFalse = node.IfFalse is not DefaultExpression
                ? VisitBranch( node.IfFalse, joinState )
                : joinState,
        };

        //sourceState.Result.Variable = resultVariable;
        //joinState.Result.Value = resultVariable;

        _states.ExitGroup( sourceState, conditionalTransition );

        return ConvertToExpression( sourceState );
    }

    protected override Expression VisitGoto( GotoExpression node )
    {
        if ( _variableResolver.TryResolveLabel( node, out var label ) )
            return label;

        var updateNode = base.VisitGoto( node );

        //if ( updateNode is not GotoExpression { Kind: GotoExpressionKind.Return } gotoExpression )
        return updateNode;

        //_hasFinalResultVariable = true;

        //return Expression.Assign( _finalResultVariable, gotoExpression.Value! );
    }

    protected override Expression VisitLoop( LoopExpression node )
    {
        if ( !RequiresLowering( node ) )
            return _variableResolver.Resolve( node );

        var joinState = _states.EnterGroup( out var sourceState );

        //var resultVariable = _variableResolver.GetResultVariable( node, sourceState.StateId );

        var newBody = VisitBranch( node.Body, null, InitializeLabels );

        var loopTransition = new LoopTransition
        {
            BodyNode = newBody, // pass default to join back to the branch-state 
            ContinueLabel = node.ContinueLabel != null ? newBody.NodeLabel : null,
            BreakLabel = node.BreakLabel != null ? joinState.NodeLabel : null,
        };

        //sourceState.Result.Variable = resultVariable;
        //joinState.Result.Value = resultVariable;

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
        return _variableResolver.AddVariable( node );

        //return _variableResolver.Resolve( node );
    }

    protected override Expression VisitSwitch( SwitchExpression node )
    {
        if ( !RequiresLowering( node ) )
            return _variableResolver.Resolve( node );

        var updatedSwitchValue = Visit( node.SwitchValue );

        var joinState = _states.EnterGroup( out var sourceState );

        //var resultVariable = _variableResolver.GetResultVariable( node, sourceState.StateId );

        var switchTransition = new SwitchTransition { SwitchValue = updatedSwitchValue };

        if ( node.DefaultBody != null )
        {
            switchTransition.DefaultNode = VisitBranch( node.DefaultBody, joinState );
        }

        foreach ( var switchCase in node.Cases )
        {
            switchTransition.AddSwitchCase(
                [.. switchCase.TestValues],
                VisitBranch( switchCase.Body, joinState )
            );
        }

        //sourceState.Result.Variable = resultVariable;
        //joinState.Result.Value = resultVariable;

        _states.ExitGroup( sourceState, switchTransition );

        return ConvertToExpression( sourceState );
    }

    protected override Expression VisitTry( TryExpression node )
    {
        if ( !RequiresLowering( node ) )
            return _variableResolver.Resolve( node );

        var joinState = _states.EnterGroup( out var sourceState );

        // var resultVariable = _variableResolver.GetResultVariable( node, sourceState.StateId );
        //  var tryStateVariable = _variableResolver.GetTryVariable( sourceState.StateId );
        //  var exceptionVariable = _variableResolver.GetExceptionVariable( sourceState.StateId );

        // if there is a finally block then that is the join for a try/catch.

        StateNode finalExpression = null;

        if ( node.Finally != null )
        {
            finalExpression = VisitBranch( node.Finally, joinState );
            joinState = finalExpression;
        }

        var nodeScope = _states.EnterTryScope( sourceState );

        var tryCatchTransition = new TryCatchTransition
        {
            // TryStateVariable = tryStateVariable,
            // ExceptionVariable = exceptionVariable,
            //  TryNode = VisitBranch( node.Body, joinState, resultVariable ),
            FinallyNode = finalExpression,
            StateScope = nodeScope,
            Scopes = _states.Scopes
        };

        _states.ExitTryScope();

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

        //sourceState.Result.Variable = resultVariable;
        //joinState.Result.Value = resultVariable;

        _states.ExitGroup( sourceState, tryCatchTransition );

        return ConvertToExpression( sourceState );
    }

    // Override method for extension expression types

    protected override Expression VisitExtension( Expression node )
    {
        return node switch
        {
            YieldExpression yieldExpression => VisitYieldExtension( yieldExpression ),

            // Nested async blocks should be visited by their own visitor,
            // but nested variables must be replaced
            YieldBlockExpression => _variableResolver.Resolve( node ),

            // Lowering visitor shouldn't be used by extensions directly
            // since it changes the shape of the code
            _ => Visit( node.Reduce() )
        };
    }

    protected Expression VisitYieldExtension( YieldExpression node )
    {
        var updatedNode = Visit( node.Value );

        var joinState = _states.EnterState( out var sourceState );

        //var resultVariable = _variableResolver.GetResultVariable( node, sourceState.StateId );

        //var resumeLabel = Expression.Label( $"{sourceState.NodeLabel.Name}_YIELD" );

        var yieldTransition = new YieldTransition
        {
            Value = updatedNode,
            StateId = sourceState.StateId,
            ResumeLabel = sourceState.NodeLabel,
            TargetNode = joinState,
        };

        _states.AddJumpCase( sourceState.NodeLabel, sourceState.StateId );

        //sourceState.Result.Variable = resultVariable;
        //joinState.Result.Value = resultVariable;

        _states.ExitState( sourceState, yieldTransition );

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
