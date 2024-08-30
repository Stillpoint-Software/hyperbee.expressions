using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.AsyncExpressions;

public class AsyncMethodCallExpression : AsyncInvokeExpression
{
    internal AsyncMethodCallExpression( MethodCallExpression body ) : base( body )
    {
    }

    public static AsyncInvokeExpression CallAsync( MethodInfo methodInfo, params Expression[] arguments )
    {
        if ( !IsAsync( methodInfo.ReturnType ) )
            throw new ArgumentException( "The specified method is not an async.", nameof( methodInfo ) );

        return new AsyncInvokeExpression( Call( methodInfo, arguments ) );
    }

    public static AsyncInvokeExpression CallAsync( Expression instance, MethodInfo methodInfo,
        params Expression[] arguments )
    {
        if ( !IsAsync( methodInfo.ReturnType ) )
            throw new ArgumentException( "The specified method is not an async.", nameof( methodInfo ) );

        return new AsyncInvokeExpression( Call( instance, methodInfo, arguments ) );
    }
}

