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
        else if ( _invocationExpression.Type.IsGenericType && _invocationExpression.Type.GetGenericTypeDefinition() == typeof(Task<>) )
        {
            return _invocationExpression.Type.GetGenericArguments()[0]; // Return T from Task<T>
        }
        else
        {
            throw new InvalidOperationException( "Invocation must return Task or Task<T>" );
        }
    }

    protected override Expression BuildStateMachine<TResult>()
    {
        var builder = new StateMachineBuilder<TResult>();
        var taskType = _invocationExpression.Type;

        if ( taskType == typeof(Task) )
        {
            builder.AddTaskBlock( _invocationExpression ); // Await the single Task
        }
        else if ( taskType.IsGenericType && taskType.GetGenericTypeDefinition() == typeof(Task<>) )
        {
            builder.AddTaskResultBlock( _invocationExpression ); // Await the single Task with a result
        }

        return builder.Build(); // Return the built state machine
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
