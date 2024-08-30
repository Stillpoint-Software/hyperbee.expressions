using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.AsyncExpressions;

public class AsyncMethodCallExpression : AsyncBaseExpression
{
    internal AsyncMethodCallExpression( MethodCallExpression body ) : base( body )
    {
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
