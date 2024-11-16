using System.Linq.Expressions;

namespace Hyperbee.Expressions.Tests.TestSupport;

internal static class AsyncHelper
{
    public static Expression Completable( Expression completeImmediatelyExpression, Expression resultExpression )
    {
        var resultType = resultExpression.Type;

        var taskResultExpression = Expression.Call(
            typeof(Task),
            nameof(Task.FromResult),
            [resultType],
            resultExpression
        );

        var asyncHelperMethod = typeof(AsyncHelper).GetMethod(
            nameof(CompletableResultAsync),
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public
        )!.MakeGenericMethod( resultType );

        return Expression.Call(
            asyncHelperMethod,
            completeImmediatelyExpression,
            taskResultExpression
        );
    }

    public static Expression Completable( Expression completeImmediatelyExpression )
    {
        var asyncHelperMethod = typeof(AsyncHelper).GetMethod(
            nameof(CompletableAsync),
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public
        );

        return Expression.Call(
            asyncHelperMethod!,
            completeImmediatelyExpression
        );
    }

    public static Task<T> CompletableResultAsync<T>( bool completeImmediately, Task<T> task )
    {
        if ( completeImmediately )
        {
            return task;
        }

        var tcs = new TaskCompletionSource<T>();
        var completedEvent = new ManualResetEventSlim();

        Task.Run( async () =>
        {
            var awaiter = new DeferredAwaiter( task, completedEvent );
            await awaiter;
            tcs.SetResult( await task );
        } );

        return tcs.Task;
    }

    public static Task CompletableAsync( bool completeImmediately )
    {
        if ( completeImmediately )
        {
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource();
        var completedEvent = new ManualResetEventSlim();

        Task.Run( async () =>
        {
            var awaiter = new DeferredAwaiter( Task.CompletedTask, completedEvent );
            await awaiter;
            tcs.SetResult();
        } );

        return tcs.Task;
    }
}
