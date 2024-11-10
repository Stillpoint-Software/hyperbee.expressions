using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers;

// ExpressionReductionOptimizer: Arithmetic and Logical Reduction
//
// This optimizer removes trivial arithmetic and logical expressions that
// have no effect, simplifies nested expressions, and combines sequential
// expressions where possible.
//
// Examples:
// Before:
//   .Add(.Parameter(x), .Constant(0))
//
// After:
//   .Parameter(x)
//
// Before:
//   .Multiply(.Parameter(x), .Constant(1))
//
// After:
//   .Parameter(x)
//
public class ExpressionReductionOptimizer : ExpressionVisitor, IExpressionOptimizer
{
    public Expression Optimize( Expression expression )
    {
        return Visit( expression );
    }

    public TExpr Optimize<TExpr>( TExpr expression ) where TExpr : LambdaExpression
    {
        var optimizedBody = Optimize( expression.Body );
        return !ReferenceEquals( expression.Body, optimizedBody )
            ? (TExpr) Expression.Lambda( expression.Type, optimizedBody, expression.Parameters )
            : expression;
    }

    // Combined VisitBinary: Handles arithmetic and logical reductions, as well as nested expression simplification.
    //
    // This visitor eliminates trivial arithmetic expressions (like adding zero, multiplying by one),
    // simplifies logical identities (like x && true or x || false), and flattens nested expressions
    // where applicable.
    //
    // Examples:
    //
    // Before:
    //   .Add(.Parameter(x), .Constant(0))
    //
    // After:
    //   .Parameter(x)
    //
    // Before:
    //   .Multiply(.Parameter(x), .Constant(1))
    //
    // After:
    //   .Parameter(x)
    //
    // Before:
    //   .Add(.Add(.Parameter(x), .Parameter(y)), .Parameter(z))
    //
    // After:
    //   .Add(.Parameter(x), .Parameter(y), .Parameter(z))
    //
    protected override Expression VisitBinary( BinaryExpression node )
    {
        // Visit left and right nodes first
        var left = Visit( node.Left );
        var right = Visit( node.Right );

        switch ( node.NodeType )
        {
            // Handle trivial arithmetic simplifications
            case ExpressionType.Add:
            case ExpressionType.Subtract:
                {
                    // x + 0 or x - 0
                    if ( right is ConstantExpression rightConst && rightConst.Value is int val && val == 0 )
                        return left;

                    // 0 + x
                    if ( node.NodeType == ExpressionType.Add && left is ConstantExpression leftConst && leftConst.Value is int leftVal && leftVal == 0 )
                        return right;
                    break;
                }

            // x * 1
            case ExpressionType.Multiply
                when right is ConstantExpression rightConst && rightConst.Value is int val && val == 1:
                return left;

            // 1 * x
            case ExpressionType.Multiply
                when left is ConstantExpression leftConst && leftConst.Value is int leftVal && leftVal == 1:
                return right;

            // x * 0 or 0 * x
            case ExpressionType.Multiply
                when (right is ConstantExpression rZero && rZero.Value is int rVal && rVal == 0) ||
                     (left is ConstantExpression lZero && lZero.Value is int lVal && lVal == 0):
                return Expression.Constant( 0 );

            // Logical short-circuiting for `&& true`, `|| false`, etc.
            // x && true
            case ExpressionType.AndAlso
                when right is ConstantExpression rConst && rConst.Value is bool boolVal && boolVal:
                return left;

            // false && x
            case ExpressionType.AndAlso
                when left is ConstantExpression lConst && lConst.Value is bool leftVal && !leftVal:
                return Expression.Constant( false );

            // x || false
            case ExpressionType.OrElse
                when right is ConstantExpression rConst && rConst.Value is bool boolVal && !boolVal:
                return left;

            // true || x
            case ExpressionType.OrElse
                when left is ConstantExpression lConst && lConst.Value is bool leftVal && leftVal:
                return Expression.Constant( true );
        }

        // Nested expression flattening for `Add` and `Multiply`
        if ( node.NodeType != ExpressionType.Add && node.NodeType != ExpressionType.Multiply )
        {
            return node.Update( left, node.Conversion, right );
        }

        // Check for nested add/multiply expressions that can be flattened
        if ( left is not BinaryExpression leftBinary || leftBinary.NodeType != node.NodeType )
        {
            return node.Update( left, node.Conversion, right );
        }

        // Example: (x + y) + z -> x + y + z
        var leftTerms = FlattenBinaryExpression( leftBinary );
        var rightTerms = FlattenBinaryExpression( right );
        var allTerms = leftTerms.Concat( rightTerms );

        return allTerms.Aggregate( ( acc, term ) => Expression.MakeBinary( node.NodeType, acc, term ) );
    }

    private static IEnumerable<Expression> FlattenBinaryExpression( Expression expr )
    {
        if ( expr is BinaryExpression binary && (binary.NodeType == ExpressionType.Add || binary.NodeType == ExpressionType.Multiply) )
        {
            return FlattenBinaryExpression( binary.Left ).Concat( FlattenBinaryExpression( binary.Right ) );
        }

        return [expr];
    }
}
