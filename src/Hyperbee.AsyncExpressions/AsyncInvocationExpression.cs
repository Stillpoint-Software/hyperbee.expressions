using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions;

public class AsyncInvocationExpression : AsyncBaseExpression
{
    private readonly InvocationExpression _invocationExpression;

    public AsyncInvocationExpression( InvocationExpression invocationExpression )
    {
        _invocationExpression = invocationExpression ?? throw new ArgumentNullException( nameof(invocationExpression) );
    }

    protected override Type GetResultType()
    {
        var returnType = _invocationExpression.Type;

        if ( IsTask( returnType ) && returnType.IsGenericType )
        {
            return returnType.GetGenericArguments()[0];
        }

        return typeof(void);
    }

    protected override void ConfigureStateMachine<TResult>( StateMachineBuilder<TResult> builder )
    {
        var block = Block( _invocationExpression );
        builder.SetSource( block );
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
