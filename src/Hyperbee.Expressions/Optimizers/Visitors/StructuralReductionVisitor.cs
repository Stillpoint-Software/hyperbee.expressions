using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers.Visitors;

public class StructuralReductionVisitor : ExpressionVisitor, IExpressionTransformer
{
    public int Priority => PriorityGroup.StructuralReductionAndConsolidation + 50;

    public Expression Transform( Expression expression )
    {
        return Visit( expression );
    }

    // Visit BlockExpression, handling block flattening and reduction
    protected override Expression VisitBlock( BlockExpression node )
    {
        var variables = new List<ParameterExpression>( node.Variables );
        var expressions = new List<Expression>();

        foreach ( var expr in node.Expressions )
        {
            var visitedExpr = Visit( expr );

            // Skip over no-op expressions and empty blocks
            if ( IsNoOpExpression( visitedExpr ) )
                continue;

            // Flatten nested blocks
            if ( visitedExpr is BlockExpression nestedBlock )
            {
                variables.AddRange( nestedBlock.Variables );
                expressions.AddRange( nestedBlock.Expressions );
            }
            else
            {
                expressions.Add( visitedExpr );
            }
        }

        // Return the single expression directly if there are no variables
        if ( variables.Count == 0 && expressions.Count == 1 )
        {
            return expressions[0];
        }

        // Construct a new block only if there are variables or multiple expressions
        return Expression.Block( variables, expressions );
    }


    // Visit GotoExpression, eliminating redundant Goto expressions
    protected override Expression VisitGoto( GotoExpression node )
    {
        if ( IsNoOpExpression( node.Value ) )
        {
            return Expression.Empty(); // Skip no-op Gotos
        }

        return base.VisitGoto( node );
    }

    // Visit LoopExpression, optimizing based on loop content
    protected override Expression VisitLoop( LoopExpression node )
    {
        var visitedBody = Visit( node.Body );

        if ( IsFalsyConstant( visitedBody ) || IsNoOpExpression( visitedBody ) || IsEffectivelyInfiniteNoOpLoop( visitedBody ) )
        {
            return node.Type == typeof( void ) ? Expression.Empty() : Expression.Default( node.Type );
        }

        return ReferenceEquals( node.Body, visitedBody )
            ? node
            : Expression.Loop( visitedBody, node.BreakLabel, node.ContinueLabel );
    }

    // Visit ConditionalExpression, simplifying based on constant conditions
    protected override Expression VisitConditional( ConditionalExpression node )
    {
        var test = Visit( node.Test );

        if ( test is ConstantExpression constantTest && constantTest.Value is bool boolTest )
        {
            // Replace based on constant true or false
            return boolTest ? Visit( node.IfTrue ) : Visit( node.IfFalse );
        }

        return node.Update( test, Visit( node.IfTrue ), Visit( node.IfFalse ) );
    }

    // Visit TryCatchExpression, simplifying empty or no-op TryCatch blocks
    protected override Expression VisitTry( TryExpression node )
    {
        var body = Visit( node.Body );

        // Remove Try if catches are no-ops or finally has no effect
        if ( node.Handlers.All( handler => IsNoOpExpression( handler.Body ) ) && (node.Finally == null || IsNoOpExpression( node.Finally )) )
        {
            return body;
        }

        return node.Update( body, node.Handlers, node.Finally, node.Fault );
    }

    // Helper methods for loop reduction and simplification
    private static bool IsFalsyConstant( Expression expression )
    {
        return expression is ConstantExpression constant && constant.Value switch
        {
            null => true,
            bool boolValue => !boolValue,
            int intValue => intValue == 0,
            double doubleValue => doubleValue == 0.0,
            float floatValue => floatValue == 0.0f,
            _ => false
        };
    }

    private static bool IsNoOpExpression( Expression expression )
    {
        return expression is DefaultExpression ||
               (expression is BlockExpression block && block.Expressions.All( IsNoOpExpression ));
    }

    private static bool IsEffectivelyInfiniteNoOpLoop( Expression expression )
    {
        return expression is ConstantExpression && !HasControlFlowOrSideEffects( expression );
    }

    private static bool HasControlFlowOrSideEffects( Expression expression )
    {
        return expression is not ConstantExpression;
    }
}
