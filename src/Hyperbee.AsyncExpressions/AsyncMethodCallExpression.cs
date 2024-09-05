using System.Reflection;
using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions;

public class AsyncMethodCallExpression : AsyncBaseExpression
{
    private readonly MethodCallExpression _methodCallExpression;

    public AsyncMethodCallExpression( MethodCallExpression methodCallExpression )
    {
        _methodCallExpression = methodCallExpression ?? throw new ArgumentNullException( nameof(methodCallExpression) );
    }

    protected override Type GetResultType()
    {
        var returnType = _methodCallExpression.Type;

        if ( IsTask( returnType ) && returnType.IsGenericType )
        {
            return returnType.GetGenericArguments()[0];
        }

        return typeof(void);
    }

    protected override void ConfigureStateMachine<TResult>( StateMachineBuilder<TResult> builder )
    {
        var block = Block( _methodCallExpression );
        builder.SetSource( block );
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
