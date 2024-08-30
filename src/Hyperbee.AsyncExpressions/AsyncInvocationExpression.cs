using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.AsyncExpressions;

public class AsyncInvocationExpression : AsyncInvokeExpression
{
    internal AsyncInvocationExpression( InvocationExpression body ) : base( body )
    {
    }

}
public static partial class AsyncExpression
{
    public static AsyncInvokeExpression InvokeAsync( LambdaExpression lambdaExpression, params Expression[] arguments )
    {
        if ( !AsyncInvokeExpression.IsAsync( lambdaExpression.ReturnType ) )
            throw new ArgumentException( "The specified lambda is not an async.", nameof( lambdaExpression ) );

        return new AsyncInvokeExpression( Expression.Invoke( lambdaExpression, arguments ) );
    }
}
