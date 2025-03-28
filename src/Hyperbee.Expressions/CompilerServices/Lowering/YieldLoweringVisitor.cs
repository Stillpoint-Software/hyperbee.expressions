using System.Linq.Expressions;
using Hyperbee.Collections;
using Hyperbee.Expressions.CompilerServices.Transitions;
using Hyperbee.Expressions.Visitors;

namespace Hyperbee.Expressions.CompilerServices.Lowering;

internal class YieldLoweringVisitor : BaseLoweringVisitor<YieldLoweringInfo>
{
    public override YieldLoweringInfo Transform(
        Type resultType,
        ParameterExpression[] localVariables,
        Expression[] expressions,
        LinkedDictionary<ParameterExpression, ParameterExpression> scopedVariables = null )
    {
        ArgumentNullException.ThrowIfNull( expressions, nameof( expressions ) );
        ArgumentOutOfRangeException.ThrowIfZero( expressions.Length, nameof( expressions ) );

        ExpressionMatcher = new ExpressionMatcher( expr => expr is YieldExpression or EnumerableBlockExpression );
        VariableResolver = new VariableResolver( localVariables, scopedVariables, States );

        VisitExpressions( expressions );

        StateOptimizer.Optimize( States );

        ThrowIfInvalid();

        return new YieldLoweringInfo
        {
            Scopes = States.Scopes,
            ScopedVariables = scopedVariables,
            Variables = localVariables
        };

        // helpers

        void ThrowIfInvalid()
        {
            if ( States.Scopes[0].States.Count == 0 )
                throw new LoweringException( $"Evaluation of the {nameof( expressions )} parameter resulted in empty states." );
        }

    }

    // Visit methods

    protected override Expression VisitExtension( Expression node )
    {
        return node switch
        {
            YieldExpression yieldExpression => VisitYieldExtension( yieldExpression ),

            // Nested yield blocks should be visited by their own visitor,
            // but nested variables must be replaced
            EnumerableBlockExpression => VariableResolver.Resolve( node ),

            // Lowering visitor shouldn't be used by extensions directly
            // since it changes the shape of the code
            _ => Visit( node.Reduce() )
        };
    }

    protected Expression VisitYieldExtension( YieldExpression node )
    {
        var updatedNode = Visit( node.Value );

        var joinState = States.EnterState( out var sourceState );

        var resultVariable = VariableResolver.GetResultVariable( node, sourceState.StateId );

        var yieldTransition = new YieldTransition
        {
            Value = updatedNode,
            StateId = sourceState.StateId,
            TargetNode = joinState,
        };

        States.AddJumpCase( joinState.NodeLabel, joinState.StateId );

        sourceState.Result.Variable = resultVariable;
        joinState.Result.Value = resultVariable;

        States.ExitState( sourceState, yieldTransition );

        return ConvertToExpression( sourceState );
    }
}
