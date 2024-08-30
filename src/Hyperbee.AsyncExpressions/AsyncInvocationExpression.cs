using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions;

public class AsyncInvocationExpression : AsyncInvokeExpression
{
    internal AsyncInvocationExpression( InvocationExpression body ) : base( body )
    {
    }

    public static AsyncInvokeExpression InvokeAsync( LambdaExpression lambdaExpression, params Expression[] arguments )
    {
        if ( !IsAsync( lambdaExpression.ReturnType ) )
            throw new ArgumentException( "The specified lambda is not an async.", nameof( lambdaExpression ) );

        return new AsyncInvokeExpression( Invoke( lambdaExpression, arguments ) );
    }
}
