using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.Expressions.Tests.TestSupport;
public enum CompleterType
{
    Immediate,
    Deferred
}

internal static class AsyncHelper
{
    public static Expression Completer( Expression completerFlagExpression, Expression resultExpression )
    {
        var resultType = resultExpression.Type;

        var asyncHelperMethod = typeof( AsyncHelper ).GetMethod(
            nameof( CompleterResultAsync ),
            BindingFlags.Static | BindingFlags.NonPublic
        )!.MakeGenericMethod( resultType );

        return Expression.Call(
            asyncHelperMethod,
            completerFlagExpression,
            resultExpression
        );
    }

    public static Expression Completer( Expression completerFlagExpression )
    {
        var asyncHelperMethod = typeof( AsyncHelper ).GetMethod(
            nameof( CompleterAsync ),
            BindingFlags.Static | BindingFlags.NonPublic
        );

        return Expression.Call(
            asyncHelperMethod!,
            completerFlagExpression
        );
    }

    private static DeferredTaskCompletionSource CompleterAsync( CompleterType completer )
    {
        var deferredTcs = new DeferredTaskCompletionSource();

        if ( completer == CompleterType.Immediate )
        {
            deferredTcs.SetResult();
            return deferredTcs;
        }

        Task.Run( () =>
        {
            deferredTcs.WaitForSignal();
            deferredTcs.SetResult();
        } );

        return deferredTcs;
    }

    private static DeferredTaskCompletionSource<T> CompleterResultAsync<T>( CompleterType completer, T result )
    {
        var deferredTcs = new DeferredTaskCompletionSource<T>();

        if ( completer == CompleterType.Immediate )
        {
            deferredTcs.SetResult( result );
            return deferredTcs;
        }

        Task.Run( () =>
        {
            deferredTcs.WaitForSignal();
            deferredTcs.SetResult( result );
        } );

        return deferredTcs;
    }
}

