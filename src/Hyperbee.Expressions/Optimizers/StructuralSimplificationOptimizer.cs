using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers;

// StructuralSimplificationOptimizer: Structural Simplification
//
// This optimizer flattens nested `BlockExpression` nodes and removes redundant blocks.
// It consolidates expressions into a single block where possible, reducing unnecessary nesting,
// which makes the expression tree more concise and easier to traverse.

public class StructuralSimplificationOptimizer : ExpressionVisitor, IExpressionOptimizer
{
    public Expression Optimize( Expression expression, OptimizationOptions options )
    {
        return options.EnableStructuralSimplification ? Visit( expression ) : expression;
    }

    protected override Expression VisitBlock( BlockExpression node )
    {
        // Flatten nested blocks and eliminate unnecessary ones
        var flattenedExpressions = new List<Expression>();

        foreach ( var expr in node.Expressions )
        {
            if ( expr is BlockExpression innerBlock )
            {
                // Flatten inner block by adding its expressions directly
                flattenedExpressions.AddRange( innerBlock.Expressions );
            }
            else
            {
                flattenedExpressions.Add( Visit( expr ) );
            }
        }

        // If only one expression remains, no need for a block
        if ( flattenedExpressions.Count == 1 && node.Variables.Count == 0 )
        {
            return flattenedExpressions[0];
        }

        return Expression.Block( node.Variables, flattenedExpressions );
    }
}
