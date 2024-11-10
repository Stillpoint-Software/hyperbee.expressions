using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers;

// ControlFlowSimplifierVisitor

public class ControlFlowSimplificationOptimizer : ExpressionVisitor, IExpressionOptimizer
{
    public Expression Optimize( Expression expression )
    {
        return Visit( expression );
    }

    public TExpr Optimize<TExpr>( TExpr expression ) where TExpr : LambdaExpression
    {
        return (TExpr) Visit( expression );
    }

    protected override Expression VisitConditional( ConditionalExpression node )
    {
        if ( node.Test is not ConstantExpression testConst )
        {
            return base.VisitConditional( node );
        }

        var condition = (bool) testConst.Value!;
        var result = Visit( condition ? node.IfTrue : node.IfFalse );

        if ( result.Type != node.Type )
        {
            result = Expression.Convert( result, node.Type );
        }

        return result;

    }

    protected override Expression VisitBlock( BlockExpression node )
    {
        var expressions = new List<Expression>();
        var hasControlFlowExit = false;

        foreach ( var expr in node.Expressions )
        {
            if ( hasControlFlowExit )
                continue;

            if ( expr is GotoExpression ||
                 (expr is UnaryExpression unary && unary.NodeType == ExpressionType.Throw) )
            {
                hasControlFlowExit = true;
            }

            expressions.Add( Visit( expr ) );
        }

        if ( expressions.Count == 0 && node.Type != typeof( void ) )
        {
            expressions.Add( Expression.Default( node.Type ) );
        }

        return Expression.Block( node.Variables, expressions );
    }

    protected override Expression VisitLoop( LoopExpression node )
    {
        if ( node.Body is not ConditionalExpression conditional || !IsConstantFalse( conditional.Test ) )
        {
            return base.VisitLoop( node );
        }

        return node.Type == typeof( void ) ? Expression.Empty() : Expression.Default( node.Type );
    }

    private static bool IsConstantFalse( Expression expression )
    {
        return expression is ConstantExpression constant && constant.Type == typeof( bool ) && !(bool) constant.Value!;
    }
}
