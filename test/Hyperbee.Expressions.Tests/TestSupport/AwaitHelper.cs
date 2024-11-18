using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.Expressions.Tests.TestSupport;

internal static class AsyncHelper
{
    public static Expression Completable( Expression completeImmediatelyExpression, Expression resultExpression )
    {
        var resultType = resultExpression.Type;

        var asyncHelperMethod = typeof( AsyncHelper ).GetMethod(
            nameof( CompletableResultAsync ),
            BindingFlags.Static | BindingFlags.NonPublic
        )!.MakeGenericMethod( resultType );

        return Expression.Call(
            asyncHelperMethod,
            completeImmediatelyExpression,
            resultExpression
        );
    }

    public static Expression Completable( Expression completeImmediatelyExpression )
    {
        var asyncHelperMethod = typeof( AsyncHelper ).GetMethod(
            nameof( CompletableAsync ),
            BindingFlags.Static | BindingFlags.NonPublic
        );

        return Expression.Call(
            asyncHelperMethod!,
            completeImmediatelyExpression
        );
    }

    private static DeferredTaskCompletionSource CompletableAsync( bool completeImmediately )
    {
        var deferredTcs = new DeferredTaskCompletionSource();

        if ( completeImmediately )
        {
            deferredTcs.Complete();
            return deferredTcs;
        }

        Task.Run( () =>
        {
            deferredTcs.WaitForSignal();
            deferredTcs.Complete();
        } );

        return deferredTcs;
    }

    private static DeferredTaskCompletionSource<T> CompletableResultAsync<T>( bool completeImmediately, T result )
    {
        var deferredTcs = new DeferredTaskCompletionSource<T>();

        if ( completeImmediately )
        {
            deferredTcs.Complete( result );
            return deferredTcs;
        }

        Task.Run( () =>
        {
            deferredTcs.WaitForSignal();
            deferredTcs.Complete( result );
        } );

        return deferredTcs;
    }
}

