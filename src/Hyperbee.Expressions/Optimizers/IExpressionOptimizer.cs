using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers;


public interface IExpressionOptimizer
{
    IExpressionTransformer[] Dependencies { get; }

    Expression Optimize( Expression expression );

    TExpr Optimize<TExpr>( TExpr expression ) where TExpr : LambdaExpression;
}
