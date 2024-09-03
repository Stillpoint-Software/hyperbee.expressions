using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions;

public class AsyncInvocationExpression : AsyncBaseExpression
{
    private readonly InvocationExpression _invocationExpression;

    public AsyncInvocationExpression( InvocationExpression invocationExpression ) : base( null )
    {
        _invocationExpression = invocationExpression;
    }

    protected override Type GetFinalResultType()
    {
        if ( _invocationExpression.Type == typeof(Task) )
        {
            return typeof(void); // No result to return
        }

        if ( _invocationExpression.Type.IsGenericType && _invocationExpression.Type.GetGenericTypeDefinition() == typeof(Task<>) )
        {
            return _invocationExpression.Type.GetGenericArguments()[0]; // Return T from Task<T>
        }

        throw new InvalidOperationException( "Invocation must return Task or Task<T>" );
    }

    protected override void ConfigureStateMachine<TResult>( StateMachineBuilder<TResult> builder )
    {
        var block = Block( _invocationExpression );
        builder.GenerateMoveNextMethod( block );
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
