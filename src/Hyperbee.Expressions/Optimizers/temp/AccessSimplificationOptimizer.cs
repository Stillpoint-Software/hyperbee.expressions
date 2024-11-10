using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers;

// AccessSimplificationOptimizer: Access Optimization
//
// This optimizer removes unnecessary null propagation checks and simplifies constant array or list accesses.
// By reducing redundant null checks and simplifying constant indexing, it improves both readability and performance.

public class AccessSimplificationOptimizer : ExpressionVisitor, IExpressionOptimizer
{
    public Expression Optimize( Expression expression )
    {
        return Visit( expression );
    }

    public TExpr Optimize<TExpr>( TExpr expression ) where TExpr : LambdaExpression
    {
        return (TExpr) Visit( expression );
    }

    protected override Expression VisitBinary( BinaryExpression node )
    {
        // Null propagation elimination
        if ( node.NodeType == ExpressionType.Coalesce && node.Left is ConstantExpression leftConst && leftConst.Value == null )
        {
            return Visit( node.Right );
        }

        return base.VisitBinary( node );
    }

    protected override Expression VisitIndex( IndexExpression node )
    {
        // Simplify constant array or list indexing
        if ( node.Object is not ConstantExpression constantArray || constantArray.Value is not Array array || node.Arguments[0] is not ConstantExpression indexExpr )
        {
            return base.VisitIndex( node );
        }

        var index = (int) indexExpr.Value!;
        return Expression.Constant( array.GetValue( index ), node.Type );

    }
}
