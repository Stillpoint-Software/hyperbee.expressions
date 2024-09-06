using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions;

public abstract class AsyncBaseExpression : Expression
{
    public override ExpressionType NodeType => ExpressionType.Extension;

    internal static bool IsTask( Type type )
    {
        return typeof( Task ).IsAssignableFrom( type );
    }
}
