using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers;

// ExpressionSimplificationOptimizer: Expression Simplification
//
// This optimizer combines adjacent expressions and simplifies arithmetic expressions.
// It removes trivial operations like "x + 0" or "x * 1" and merges sequential expressions,
// reducing the number of operations and making the expression tree more efficient.

public class ExpressionSimplificationOptimizer : ExpressionVisitor, IExpressionOptimizer
{
    public Expression Optimize( Expression expression, OptimizationOptions options )
    {
        return options.EnableExpressionSimplification ? Visit( expression ) : expression;
    }

    protected override Expression VisitBinary( BinaryExpression node )
    {
        // Combine adjacent expressions (e.g., x = x + 1; x = x * 2)

        return node.NodeType switch
        {
            // Simplify arithmetic expressions like x + 0 or x * 1
            ExpressionType.Add when IsZero( node.Right ) => Visit( node.Left ),
            ExpressionType.Add when IsZero( node.Left ) => Visit( node.Right ),
            ExpressionType.Multiply when IsOne( node.Right ) => Visit( node.Left ),
            ExpressionType.Multiply when IsOne( node.Left ) => Visit( node.Right ),
            _ => base.VisitBinary( node )
        };
    }

    private static bool IsZero( Expression expression )
    {
        return expression is ConstantExpression constant && constant.Value is int value && value == 0;
    }

    private static bool IsOne( Expression expression )
    {
        return expression is ConstantExpression constant && constant.Value is int value && value == 1;
    }
}
