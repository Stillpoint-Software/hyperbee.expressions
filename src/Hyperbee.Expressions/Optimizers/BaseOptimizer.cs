using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers;

public abstract class BaseOptimizer : IExpressionOptimizer
{
    public abstract IExpressionTransformer[] Dependencies { get; }

    public virtual Expression Optimize( Expression expression )
    {
        foreach ( var dependency in Dependencies )
        {
            expression = dependency.Transform( expression );
        }

        return expression;
    }

    public TExpr Optimize<TExpr>( TExpr expression ) where TExpr : LambdaExpression
    {
        foreach ( var dependency in Dependencies )
        {
            expression = (TExpr) dependency.Transform( expression );
        }

        return expression;
    }
}
