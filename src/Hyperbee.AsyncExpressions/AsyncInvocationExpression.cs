using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions;

public class AsyncInvocationExpression : AsyncBaseExpression
{
    internal AsyncInvocationExpression( InvocationExpression body ) : base( body )
    {
    }
}

public static partial class AsyncExpression
{
    public static AsyncBaseExpression InvokeAsync( LambdaExpression lambdaExpression, params Expression[] arguments )
    {
        if ( !AsyncBaseExpression.IsTask( lambdaExpression.ReturnType ) )
            throw new ArgumentException( "The specified lambda does not return a Task.", nameof( lambdaExpression ) );

        return new AsyncInvocationExpression( Expression.Invoke( lambdaExpression, arguments ) );
    }
}
