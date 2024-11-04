using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers;

// ConstantSimplificationOptimizer: Expression Simplification
//
// This optimizer performs constant folding and constant propagation.
// It simplifies expressions by precomputing constant values and replacing variables with known constants.
// For example, expressions like "2 + 3" are reduced to "5".

public class ConstantSimplificationOptimizer : ExpressionVisitor, IExpressionOptimizer
{
    public Expression Optimize( Expression expression, OptimizationOptions options )
    {
        return options.EnableConstantSimplification ? Visit( expression ) : expression;
    }

    protected override Expression VisitBinary( BinaryExpression node )
    {
        // Constant folding
        if ( node.Left is not ConstantExpression leftConst || node.Right is not ConstantExpression rightConst )
        {
            return base.VisitBinary( node );
        }

        var leftValue = leftConst.Value;
        var rightValue = rightConst.Value;

        var result = node.NodeType switch
        {
            ExpressionType.Add => (int) leftValue! + (int) rightValue!,
            ExpressionType.Subtract => (int) leftValue! - (int) rightValue!,
            ExpressionType.Multiply => (int) leftValue! * (int) rightValue!,
            ExpressionType.Divide => (int) leftValue! / (int) rightValue!,

            _ => throw new NotSupportedException( $"Operation {node.NodeType} is not supported" )
        };

        return Expression.Constant( result, node.Type );

    }
}
