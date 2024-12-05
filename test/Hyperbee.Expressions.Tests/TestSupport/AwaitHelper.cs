using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.Expressions.Tests.TestSupport;

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

    public static Expression Completable( Expression completeimmediateFlagExpression )
    {
        var asyncHelperMethod = typeof( AsyncHelper ).GetMethod(
            nameof( CompletableAsync ),
            BindingFlags.Static | BindingFlags.NonPublic
        );

        return Expression.Call(
            asyncHelperMethod!,
            completeimmediateFlagExpression
        );
    }

    private static DeferredTaskCompletionSource CompletableAsync( bool completeimmediateFlag )
    {
        var deferredTcs = new DeferredTaskCompletionSource();

        if ( completeimmediateFlag )
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

    private static DeferredTaskCompletionSource<T> CompletableResultAsync<T>( bool completeimmediateFlag, T result )
    {
        var deferredTcs = new DeferredTaskCompletionSource<T>();

        if ( completeimmediateFlag )
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

