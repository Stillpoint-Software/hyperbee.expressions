using System.Linq.Expressions;
using Hyperbee.Collections;
using Hyperbee.Expressions.CompilerServices.Transitions;
using Hyperbee.Expressions.Visitors;

namespace Hyperbee.Expressions.CompilerServices.Lowering;

internal class AsyncLoweringVisitor : BaseLoweringVisitor
{
    private ParameterExpression _finalResultVariable;
    private bool _hasFinalResultVariable;

    private int _awaitCount;

    public override LoweringInfo Transform(
        Type resultType,
        ParameterExpression[] localVariables,
        Expression[] expressions,
        LinkedDictionary<ParameterExpression, ParameterExpression> scopedVariables = null )
    {
        ArgumentNullException.ThrowIfNull( expressions, nameof( expressions ) );
        ArgumentOutOfRangeException.ThrowIfZero( expressions.Length, nameof( expressions ) );

        ExpressionMatcher = new ExpressionMatcher( expr => expr is AwaitExpression or AsyncBlockExpression );
        VariableResolver = new VariableResolver( localVariables, scopedVariables, States );
        _finalResultVariable = CreateFinalResultVariable( resultType, VariableResolver );

        VisitExpressions( expressions );

        StateOptimizer.Optimize( States );

        ThrowIfInvalid();

        return new LoweringInfo
        {
            Scopes = States.Scopes,
            HasFinalResultVariable = _hasFinalResultVariable,
            AwaitCount = _awaitCount,
            ScopedVariables = scopedVariables
        };

        // helpers

        void ThrowIfInvalid()
        {
            if ( States.Scopes[0].States.Count == 0 )
                throw new LoweringException(
                    $"Evaluation of the {nameof( expressions )} parameter resulted in empty states." );

            if ( _awaitCount == 0 )
                throw new LoweringException(
                    $"The {nameof( expressions )} parameter must contain at least one awaitable." );
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

    protected override void VisitExpressions( IEnumerable<Expression> expressions )
    {
        base.VisitExpressions( expressions );

        // update the final state

        if ( !_hasFinalResultVariable )
        {
            // assign the final result variable if not already assigned for state-machine builder.
            // this is the case when the last expression is not a return statement.
            // this will ensure that the state-machine builder will have a final result field.

            States.TailState.Result.Variable = _finalResultVariable;
        }
    }

    protected override Expression VisitGoto( GotoExpression node )
    {
        var updateNode = base.VisitGoto( node );

        if ( updateNode is not GotoExpression { Kind: GotoExpressionKind.Return } gotoExpression )
            return updateNode;

        _hasFinalResultVariable = true;

        return Expression.Assign( _finalResultVariable, gotoExpression.Value! );
    }

    // Override method for extension expression types

    protected override Expression VisitExtension( Expression node )
    {
        return node switch
        {
            AwaitExpression awaitExpression => VisitAwaitExtension( awaitExpression ),

            // Nested async blocks should be visited by their own visitor,
            // but nested variables must be replaced
            AsyncBlockExpression => VariableResolver.Resolve( node ),

            // Lowering visitor shouldn't be used by extensions directly
            // since it changes the shape of the code
            _ => base.Visit( node.Reduce() )
        };
    }

    protected Expression VisitAwaitExtension( AwaitExpression node )
    {
        var updatedNode = Visit( node.Target );

        var joinState = States.EnterState( out var sourceState );

        var resultVariable = VariableResolver.GetResultVariable( node, sourceState.StateId );

        _awaitCount++;

        var awaitBinder = node.GetAwaitBinder();

        var awaiterVariable = VariableResolver.GetAwaiterVariable(
            awaitBinder.GetAwaiterMethod.ReturnType,
            sourceState.StateId
        );

        var resumeLabel = Expression.Label( $"{sourceState.NodeLabel.Name}_RESUME" );

        var awaitTransition = new AwaitTransition
        {
            Target = updatedNode,
            StateId = sourceState.StateId,
            ResumeLabel = resumeLabel,
            TargetNode = joinState,
            AwaiterVariable = awaiterVariable,
            ResultVariable = resultVariable,
            AwaitBinder = awaitBinder,
            ConfigureAwait = node.ConfigureAwait
        };

        States.AddJumpCase( resumeLabel, sourceState.StateId );

        sourceState.Result.Variable = resultVariable;
        joinState.Result.Value = resultVariable;

        States.ExitState( sourceState, awaitTransition );

        return ConvertToExpression( sourceState );
    }
}
