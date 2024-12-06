using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.Expressions.Tests.TestSupport;
public enum CompletableType
{
    Immediate,
    Deferred
}

internal static class AsyncHelper
{
    public static Expression Completable( Expression completeimmediateFlagExpression, Expression resultExpression )
    {
        var resultType = resultExpression.Type;

        var asyncHelperMethod = typeof( AsyncHelper ).GetMethod(
            nameof( CompletableResultAsync ),
            BindingFlags.Static | BindingFlags.NonPublic
        )!.MakeGenericMethod( resultType );

        return Expression.Call(
            asyncHelperMethod,
            completeimmediateFlagExpression,
            resultExpression
        );
    }

    public static Expression Completable( Expression completableTypeExpression )
    {
        var asyncHelperMethod = typeof( AsyncHelper ).GetMethod(
            nameof( CompletableAsync ),
            BindingFlags.Static | BindingFlags.NonPublic
        );

        return Expression.Call(
            asyncHelperMethod!,
            completableTypeExpression
        );
    }

    private static DeferredTaskCompletionSource CompletableAsync( CompletableType completable )
    {
        var deferredTcs = new DeferredTaskCompletionSource();

        if ( completable == CompletableType.Immediate )
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

    private static DeferredTaskCompletionSource<T> CompletableResultAsync<T>( CompletableType completable, T result )
    {
        var deferredTcs = new DeferredTaskCompletionSource<T>();

        if ( completable == CompletableType.Immediate )
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

