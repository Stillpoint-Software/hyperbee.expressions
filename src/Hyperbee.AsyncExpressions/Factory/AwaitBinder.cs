using System.Reflection;
using System.Runtime.CompilerServices;
using Hyperbee.AsyncExpressions.Transformation;

namespace Hyperbee.AsyncExpressions.Factory;

internal delegate TAwaiter AwaitBinderGetAwaiterDelegate<in TAwaitable, out TAwaiter>( TAwaitable awaitable, bool configureAwait );
internal delegate TResult AwaitBinderGetResultDelegate<in TAwaiter, out TResult>( TAwaiter awaiter );

public class AwaitBinder
{
    public MethodInfo AwaitMethod { get; }
    public MethodInfo GetAwaiterMethod { get; }
    public MethodInfo GetResultMethod { get; }

    private Delegate GetAwaiterImplDelegate { get; }
    private Delegate GetResultImplDelegate { get; }

    internal AwaitBinder(
        MethodInfo awaitMethod,
        MethodInfo getAwaiterMethod,
        MethodInfo getResultMethod,
        Delegate getAwaiterImplDelegate = null,
        Delegate getResultImplDelegate = null )
    {
        AwaitMethod = awaitMethod;
        GetAwaiterMethod = getAwaiterMethod;
        GetResultMethod = getResultMethod;
        GetAwaiterImplDelegate = getAwaiterImplDelegate;
        GetResultImplDelegate = getResultImplDelegate;
    }

    // Await methods

    internal void Await<TAwaitable,TAwaiter>( TAwaitable awaitable, bool configureAwait )
    {
        switch ( awaitable )
        {
            case Task task:
                task.ConfigureAwait( configureAwait ).GetAwaiter().GetResult();
                return;

            case ValueTask valueTask:
                valueTask.ConfigureAwait( configureAwait ).GetAwaiter().GetResult();
                return;

            default:
                var awaiter = GetAwaiter<TAwaitable, TAwaiter>( awaitable, configureAwait );
                GetResult( awaiter );
                break;
        }
    }

    internal TResult AwaitResult<TAwaitable, TAwaiter, TResult>( TAwaitable awaitable, bool configureAwait )
    {
        switch ( awaitable )
        {
            case Task<TResult> task:
                return task.ConfigureAwait( configureAwait ).GetAwaiter().GetResult();

            case ValueTask<TResult> valueTask:
                return valueTask.ConfigureAwait( configureAwait ).GetAwaiter().GetResult();

            default:
                var awaiter = GetAwaiter<TAwaitable, TAwaiter>( awaitable, configureAwait );
                return GetResultValue<TAwaiter,TResult>( awaiter );
        }
    }

    // GetAwaiter methods

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static ConfiguredTaskAwaitable.ConfiguredTaskAwaiter GetAwaiter( Task task, bool configureAwait )
    {
        return task.ConfigureAwait( configureAwait ).GetAwaiter();
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter GetAwaiter( ValueTask valueTask, bool configureAwait )
    {
        return valueTask.ConfigureAwait( configureAwait ).GetAwaiter();
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static ConfiguredTaskAwaitable<TResult>.ConfiguredTaskAwaiter GetAwaiter<TResult>( Task<TResult> task, bool configureAwait )
    {
        return task.ConfigureAwait( configureAwait ).GetAwaiter();
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static ConfiguredValueTaskAwaitable<TResult>.ConfiguredValueTaskAwaiter GetAwaiter<TResult>( ValueTask<TResult> valueTask, bool configureAwait )
    {
        return valueTask.ConfigureAwait( configureAwait ).GetAwaiter();
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal TAwaiter GetAwaiter<TAwaitable, TAwaiter>( TAwaitable awaitable, bool configureAwait )
    {
        if ( GetAwaiterImplDelegate == null )
            throw new InvalidOperationException( $"The {nameof(GetAwaiterImplDelegate)} is not set for {awaitable.GetType()}." );

        var getAwaiter = (AwaitBinderGetAwaiterDelegate<TAwaitable, TAwaiter>) GetAwaiterImplDelegate;
        return getAwaiter( awaitable, configureAwait );
    }

    // GetResult methods
    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static void GetResult( ConfiguredTaskAwaitable.ConfiguredTaskAwaiter awaiter ) => awaiter.GetResult();

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static void GetResult( ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter awaiter ) => awaiter.GetResult();

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static TResult GetResult<TResult>( ConfiguredTaskAwaitable<TResult>.ConfiguredTaskAwaiter awaiter ) => awaiter.GetResult();

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static TResult GetResult<TResult>( ConfiguredValueTaskAwaitable<TResult>.ConfiguredValueTaskAwaiter awaiter ) => awaiter.GetResult();

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal void GetResult<TAwaiter>( TAwaiter awaiter )
    {
        if ( GetResultImplDelegate == null )
            throw new InvalidOperationException( $"The {nameof(GetResultImplDelegate)} is not set for {awaiter.GetType()}." );

        var getResult = (AwaitBinderGetResultDelegate<TAwaiter, IVoidTaskResult>) GetResultImplDelegate;
        getResult( awaiter );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal TResult GetResultValue<TAwaiter, TResult>( TAwaiter awaiter )
    {
        if ( GetResultImplDelegate == null )
            throw new InvalidOperationException( $"The {nameof(GetResultImplDelegate)} is not set for {awaiter.GetType()}." );

        var getResult = (AwaitBinderGetResultDelegate<TAwaiter, TResult>) GetResultImplDelegate;
        var result = getResult( awaiter );

        return result;
    }
}
