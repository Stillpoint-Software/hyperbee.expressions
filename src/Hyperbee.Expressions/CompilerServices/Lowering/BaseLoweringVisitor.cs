using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Collections;
using Hyperbee.Expressions.CompilerServices.Transitions;
using Hyperbee.Expressions.Visitors;

namespace Hyperbee.Expressions.CompilerServices.Lowering;

internal abstract class BaseLoweringVisitor<TInfo> : ExpressionVisitor
    where TInfo : LoweringInfo
{
    protected readonly StateContext States = new( 4 );
    protected ExpressionMatcher ExpressionMatcher;

    // The finally blocks a disposing resume still owes, innermost last.
    private readonly Stack<StateNode> _enclosingFinally = new();

    /// <summary>
    /// What a finally state does once it has run while the machine is being disposed: hand
    /// off to the next finally out, or leave the machine.
    /// </summary>
    /// <remarks>
    /// Only an enumerable is disposed part way through. An async state machine runs to
    /// completion or faults, so there is nothing to guard and this is not emitted.
    /// </remarks>
    private static Expression DisposeGuard( StateMachineContext context, StateNode enclosingFinally )
    {
        if ( context.StateMachineInfo is not EnumerableStateMachineInfo info )
            return Expression.Empty();

        return Expression.IfThen(
            info.DisposingField,
            enclosingFinally != null
                ? Expression.Goto( enclosingFinally.NodeLabel )
                : Expression.Block(
                    Expression.Assign( info.Success, Expression.Constant( true ) ),
                    Expression.Return( info.ExitLabel, Expression.Constant( false ), typeof( bool ) ) ) );
    }
    protected VariableResolver VariableResolver;

    public abstract TInfo Transform(
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

        // A switch without a default body still needs a fall-through target so the
        // transition graph (FallThroughNode / Optimize) has a node to point at. When no
        // default is supplied, fall through to the join state (the continuation after the
        // switch), matching the runtime behavior of a Switch expression with no default.

        switchTransition.DefaultNode = node.DefaultBody != null
            ? VisitBranch( node.DefaultBody, joinState, resultVariable )
            : joinState;

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

        if ( node.Fault != null )
            throw new LoweringException( "Fault handlers are not supported when lowering a try expression." );

        var joinState = States.EnterGroup( out var sourceState );

        // A finally re-points joinState at the finally state, but the group still falls
        // through to the state EnterGroup created, and ExitGroup makes that one the tail.
        // Keep it so that it can be given the result as well.

        var groupJoinState = joinState;

        var resultVariable = VariableResolver.GetResultVariable( node, sourceState.StateId );
        var tryStateVariable = VariableResolver.GetTryVariable( sourceState.StateId );
        var exceptionVariable = VariableResolver.GetExceptionVariable( sourceState.StateId );

        // if there is a finally block then that is the join for a try/catch.

        StateNode finalExpression = null;

        // The finally a disposing resume should run before leaving, if this try is nested
        // inside one that has a finally of its own.

        var enclosingFinally = _enclosingFinally.Count > 0 ? _enclosingFinally.Peek() : null;

        if ( node.Finally != null )
        {
            finalExpression = VisitBranch( node.Finally, joinState );

            // Reached only while disposing, where falling through to the code after the try
            // would run body the caller abandoned. Leave, or hand off to the next finally out.

            finalExpression.Guard = context => DisposeGuard( context, enclosingFinally );

            // Lowering turns the try into a `catch-all` that records the exception and
            // dispatches to the finally state. Nothing else re-throws, so an exception
            // that no catch block handled must be re-thrown once the finally completes.

            States.TailState.Expressions.Add(
                Expression.IfThen(
                    Expression.NotEqual( exceptionVariable, Expression.Constant( null, exceptionVariable.Type ) ),
                    Expression.Call( exceptionVariable, ReflectionHelper.ExceptionDispatchInfoThrow )
                )
            );

            joinState = finalExpression;
        }

        var nodeScope = States.EnterTryScope();

        // A resume that lands in this region while disposing goes to this try's finally, or
        // past it to the next one out when this try has none of its own.

        _enclosingFinally.Push( finalExpression ?? enclosingFinally );

        var tryCatchTransition = new TryCatchTransition
        {
            TryStateVariable = tryStateVariable,
            ExceptionVariable = exceptionVariable,
            TryNode = VisitBranch( node.Body, joinState, resultVariable ),
            FinallyNode = finalExpression,
            DisposeNode = finalExpression ?? enclosingFinally,
            StateScope = nodeScope,
            Scopes = States.Scopes
        };

        _enclosingFinally.Pop();

        States.ExitTryScope();

        for ( var index = 0; index < node.Handlers.Count; index++ )
        {
            // use a non-zero based index for catch states to avoid
            // conflicts with default catch state value (zero).

            var catchState = index + 1;
            var catchBlock = node.Handlers[index];

            // The handler body runs in its own state, outside of the try, so the catch
            // variable must be hoisted to stay in scope. Resolve the filter after the
            // variable is registered so that it binds to the same hoisted variable.

            var catchVariable = catchBlock.Variable != null
                ? VariableResolver.AddLocalVariable( catchBlock.Variable )
                : null;

            // `throw;` is only valid inside a catch block, and the handler body is no
            // longer one. Rewrite it to re-throw the caught exception, which needs a
            // variable to read it from. Hoist one when the source handler declared none.

            var caughtVariable = catchBlock.Variable;

            var catchBody = RethrowRewriter.Rewrite( catchBlock.Body, () =>
            {
                if ( caughtVariable != null )
                    return caughtVariable;

                caughtVariable = Expression.Parameter( catchBlock.Test, "rethrow" );
                catchVariable = VariableResolver.AddLocalVariable( caughtVariable );

                return caughtVariable;
            } );

            // The handler runs in its own state, outside the generated try, so an exception
            // it raises would leave the machine without running the finally. Give the body
            // a try of its own that records the exception and falls through to the finally,
            // which re-throws it once it has run.

            if ( finalExpression != null )
                catchBody = CaptureHandlerFault( catchBody, exceptionVariable );

            if ( catchBlock.Filter != null && RequiresLowering( catchBlock.Filter ) )
                throw new LoweringException( "Await is not supported in an exception filter." );

            var catchFilter = catchBlock.Filter != null
                ? VariableResolver.Resolve( catchBlock.Filter )
                : null;

            tryCatchTransition.AddCatchBlock(
                catchBlock,
                catchVariable,
                catchFilter,
                VisitBranch( catchBody, joinState, resultVariable ),
                catchState );
        }

        sourceState.Result.Variable = resultVariable;
        joinState.Result.Value = resultVariable;

        // Without this, a try that is the result-producing tail of the block has no value
        // to hand back, and the final transition falls back to a void result.

        groupJoinState.Result.Value = resultVariable;

        States.ExitGroup( sourceState, tryCatchTransition );

        return ConvertToExpression( sourceState );
    }

    // Records an exception raised by a catch handler so that the finally state can run and
    // re-throw it, the way it already does for an exception no handler took. The handler
    // value is unused on this path, because the capture always ends in a re-throw.

    private static Expression CaptureHandlerFault( Expression handlerBody, Expression exceptionVariable )
    {
        var fault = Expression.Parameter( typeof( Exception ), "__fault<>" );

        var capture = Expression.Assign(
            exceptionVariable,
            Expression.Call( ReflectionHelper.ExceptionDispatchInfoCapture, fault ) );

        var handled = handlerBody.Type == typeof( void )
            ? Expression.Block( typeof( void ), capture )
            : Expression.Block( handlerBody.Type, capture, Expression.Default( handlerBody.Type ) );

        return Expression.TryCatch( handlerBody, Expression.Catch( fault, handled ) );
    }

    // A bare `throw;` (Expression.Rethrow) is only valid inside a catch block. Lowering
    // moves the handler body into its own state, outside of the try, so the rethrow must
    // be rewritten to throw the caught exception explicitly. ExceptionDispatchInfo is used
    // to preserve the original stack trace, which throwing the exception again would reset.

    private sealed class RethrowRewriter( Func<ParameterExpression> caughtVariable ) : ExpressionVisitor
    {
        public static Expression Rewrite( Expression body, Func<ParameterExpression> caughtVariable )
        {
            return new RethrowRewriter( caughtVariable ).Visit( body );
        }

        protected override Expression VisitUnary( UnaryExpression node )
        {
            if ( node.NodeType != ExpressionType.Throw || node.Operand != null )
                return base.VisitUnary( node );

            var rethrow = Expression.Call(
                Expression.Call( ReflectionHelper.ExceptionDispatchInfoCapture, caughtVariable() ),
                ReflectionHelper.ExceptionDispatchInfoThrow );

            // A rethrow carries the type of the expression it stands in for. The call is
            // void, so the block needs a value of that type to stay well-typed. The value
            // is never reached, because the line above it always throws.

            return node.Type == typeof( void )
                ? rethrow
                : Expression.Block( node.Type, rethrow, Expression.Default( node.Type ) );
        }

        // A rethrow inside a nested handler belongs to that handler, not to this one. If
        // that try is lowered too, its own visit rewrites it; if it is not, the handler
        // survives as a real catch block and the rethrow is already valid.

        protected override CatchBlock VisitCatchBlock( CatchBlock node ) => node;

        // A lambda has an exception context of its own.

        protected override Expression VisitLambda<T>( Expression<T> node ) => node;
    }

    protected sealed class ResultExpression( StateResult result ) : Expression
    {
        public override ExpressionType NodeType => ExpressionType.Extension;
        public override Type Type => result.Variable?.Type ?? typeof( void );
        public override bool CanReduce => true;

        public override Expression Reduce() => result.Variable ?? Empty();
    }
}
