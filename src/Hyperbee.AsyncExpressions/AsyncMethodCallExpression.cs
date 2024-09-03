using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.AsyncExpressions;

public class AsyncMethodCallExpression : AsyncBaseExpression
{
    private readonly MethodCallExpression _methodCallExpression;

    public AsyncMethodCallExpression( MethodCallExpression methodCallExpression ) : base( [methodCallExpression] )
    {
        _methodCallExpression = methodCallExpression;
    }

    protected override Type GetFinalResultType()
    {
        if ( _methodCallExpression.Type == typeof(Task) )
        {
            return typeof(void); // No result to return
        }

        if ( _methodCallExpression.Type.IsGenericType && _methodCallExpression.Type.GetGenericTypeDefinition() == typeof(Task<>) )
        {
            return _methodCallExpression.Type.GetGenericArguments()[0]; // Return T from Task<T>
        }

        throw new InvalidOperationException( "Method call must return Task or Task<T>" );
    }

    protected override void ConfigureStateMachine<TResult>( StateMachineBuilder<TResult> builder )
    {
        var block = Block( _methodCallExpression );
        builder.GenerateMoveNextMethod( block );
    }
}


public static partial class AsyncExpression
{
    public static AsyncBaseExpression CallAsync( MethodInfo methodInfo, params Expression[] arguments )
    {
        if ( !AsyncBaseExpression.IsTask( methodInfo.ReturnType ) )
            throw new ArgumentException( "The specified method does not return a Task.", nameof( methodInfo ) );

        return new AsyncMethodCallExpression( Expression.Call( methodInfo, arguments ) );
    }

    public static AsyncBaseExpression CallAsync( Expression instance, MethodInfo methodInfo,
        params Expression[] arguments )
    {
        if ( !AsyncBaseExpression.IsTask( methodInfo.ReturnType ) )
            throw new ArgumentException( "The specified method does not return a Task.", nameof( methodInfo ) );

        return new AsyncMethodCallExpression( Expression.Call( instance, methodInfo, arguments ) );
    }
}
