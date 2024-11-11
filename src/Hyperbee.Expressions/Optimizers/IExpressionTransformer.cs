using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers;

public interface IExpressionTransformer
{
    Expression Transform( Expression expression );
}
