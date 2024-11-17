using System.Reflection;
using System.Runtime.CompilerServices;

namespace Hyperbee.Expressions.Transformation;

internal delegate TAwaiter AwaitBinderGetAwaiterDelegate<TAwaitable, out TAwaiter>( ref TAwaitable awaitable, bool configureAwait );
internal delegate TResult AwaitBinderGetResultDelegate<TAwaiter, out TResult>( ref TAwaiter awaiter );

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

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal void Await<TAwaitable, TAwaiter>( ref TAwaitable awaitable, bool configureAwait )
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
                var awaiter = GetAwaiter<TAwaitable, TAwaiter>( ref awaitable, configureAwait );
                GetResult( ref awaiter );
                break;
        }
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal TResult AwaitResult<TAwaitable, TAwaiter, TResult>( ref TAwaitable awaitable, bool configureAwait )
    {
        switch ( awaitable )
        {
            case Task<TResult> task:
                return task.ConfigureAwait( configureAwait ).GetAwaiter().GetResult();

            case ValueTask<TResult> valueTask:
                return valueTask.ConfigureAwait( configureAwait ).GetAwaiter().GetResult();

            default:
                var awaiter = GetAwaiter<TAwaitable, TAwaiter>( ref awaitable, configureAwait );
                return GetResult<TAwaiter, TResult>( ref awaiter );
        }
    }

    // GetAwaiter methods

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static ConfiguredTaskAwaitable.ConfiguredTaskAwaiter GetAwaiter( ref Task task, bool configureAwait )
    {
        return task.ConfigureAwait( configureAwait ).GetAwaiter();
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter GetAwaiter( ref ValueTask valueTask, bool configureAwait )
    {
        return valueTask.ConfigureAwait( configureAwait ).GetAwaiter();
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static ConfiguredTaskAwaitable<TResult>.ConfiguredTaskAwaiter GetAwaiter<TResult>( ref Task<TResult> task, bool configureAwait )
    {
        return task.ConfigureAwait( configureAwait ).GetAwaiter();
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static ConfiguredValueTaskAwaitable<TResult>.ConfiguredValueTaskAwaiter GetAwaiter<TResult>( ref ValueTask<TResult> valueTask, bool configureAwait )
    {
        return valueTask.ConfigureAwait( configureAwait ).GetAwaiter();
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal TAwaiter GetAwaiter<TAwaitable, TAwaiter>( ref TAwaitable awaitable, bool configureAwait )
    {
        if ( GetAwaiterImplDelegate == null )
            throw new InvalidOperationException( $"The {nameof( GetAwaiterImplDelegate )} is not set for {awaitable.GetType()}." );

        var getAwaiter = (AwaitBinderGetAwaiterDelegate<TAwaitable, TAwaiter>) GetAwaiterImplDelegate;
        return getAwaiter( ref awaitable, configureAwait );
    }

    // GetResult methods

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static void GetResult( ref ConfiguredTaskAwaitable.ConfiguredTaskAwaiter awaiter ) => awaiter.GetResult();

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static void GetResult( ref ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter awaiter ) => awaiter.GetResult();

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal void GetResult<TAwaiter>( ref TAwaiter awaiter )
    {
        if ( GetResultImplDelegate == null )
            throw new InvalidOperationException( $"The {nameof( GetResultImplDelegate )} is not set for {awaiter.GetType()}." );

        var getResult = (AwaitBinderGetResultDelegate<TAwaiter, IVoidResult>) GetResultImplDelegate;
        getResult( ref awaiter );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static TResult GetResult<TResult>( ref ConfiguredTaskAwaitable<TResult>.ConfiguredTaskAwaiter awaiter ) => awaiter.GetResult();

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static TResult GetResult<TResult>( ref ConfiguredValueTaskAwaitable<TResult>.ConfiguredValueTaskAwaiter awaiter ) => awaiter.GetResult();

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal TResult GetResult<TAwaiter, TResult>( ref TAwaiter awaiter )
    {
        if ( GetResultImplDelegate == null )
            throw new InvalidOperationException( $"The {nameof( GetResultImplDelegate )} is not set for {awaiter.GetType()}." );

        var getResult = (AwaitBinderGetResultDelegate<TAwaiter, TResult>) GetResultImplDelegate;
        var result = getResult( ref awaiter );

        return result;
    }
}
