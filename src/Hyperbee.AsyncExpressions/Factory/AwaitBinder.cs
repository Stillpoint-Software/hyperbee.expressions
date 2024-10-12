using System.Reflection;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions.Factory;

internal delegate object AwaitBinderGetAwaiterDelegate( object awaitable, bool configureAwait );

internal delegate object AwaitBinderGetResultDelegate( object awaiter );

public class AwaitBinder
{
    public MethodInfo AwaitMethod { get; }
    public MethodInfo GetAwaiterMethod { get; }
    public MethodInfo GetResultMethod { get; }

    private AwaitBinderGetAwaiterDelegate GetAwaiterImpl { get; }
    private AwaitBinderGetResultDelegate GetResultImpl { get; }

    internal AwaitBinder(
        MethodInfo awaitMethod,
        MethodInfo getAwaiterMethod,
        MethodInfo getResultMethod,
        AwaitBinderGetAwaiterDelegate getAwaiterImpl = null,
        AwaitBinderGetResultDelegate getResultImpl = null )
    {
        AwaitMethod = awaitMethod;
        GetAwaiterMethod = getAwaiterMethod;
        GetResultMethod = getResultMethod;
        GetAwaiterImpl = getAwaiterImpl;
        GetResultImpl = getResultImpl;
    }

    // Await methods

    internal void Await<TAwaitable>( TAwaitable awaitable, bool configureAwait )
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
            {
                var awaiter = GetAwaiter( awaitable, configureAwait );
                GetResult( awaiter );
                break;
            }
        }
    }

    internal T AwaitResult<TAwaitable, T>( TAwaitable awaitable, bool configureAwait )
    {
        switch ( awaitable )
        {
            case Task<T> task:
                return task.ConfigureAwait( configureAwait ).GetAwaiter().GetResult();

            case ValueTask<T> valueTask:
                return valueTask.ConfigureAwait( configureAwait ).GetAwaiter().GetResult();

            default:
            {
                var awaiter = GetAwaiter( awaitable, configureAwait );
                return GetResultValue<T>( awaiter );
            }
        }
    }

    // GetAwaiter methods

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static ConfiguredTaskAwaitable.ConfiguredTaskAwaiter GetAwaiter( Task task, bool configureAwait )
    {
        return task.ConfigureAwait( configureAwait ).GetAwaiter();
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static ConfiguredTaskAwaitable<T>.ConfiguredTaskAwaiter GetAwaiter<T>( Task<T> task, bool configureAwait )
    {
        return task.ConfigureAwait( configureAwait ).GetAwaiter();
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter GetAwaiter( ValueTask valueTask, bool configureAwait )
    {
        return valueTask.ConfigureAwait( configureAwait ).GetAwaiter();
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static ConfiguredValueTaskAwaitable<T>.ConfiguredValueTaskAwaiter GetAwaiter<T>( ValueTask<T> valueTask, bool configureAwait )
    {
        return valueTask.ConfigureAwait( configureAwait ).GetAwaiter();
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal object GetAwaiter( object awaitable, bool configureAwait )
    {
        if ( GetAwaiterImpl == null )
            throw new InvalidOperationException( $"The {nameof(GetAwaiterImpl)} is not set for {awaitable.GetType()}." );

        return GetAwaiterImpl( awaitable, configureAwait );
    }

    // GetResult methods
    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static void GetResult( ConfiguredTaskAwaitable.ConfiguredTaskAwaiter awaiter ) => awaiter.GetResult();

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static T GetResult<T>( ConfiguredTaskAwaitable<T>.ConfiguredTaskAwaiter awaiter ) => awaiter.GetResult();

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static void GetResult( ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter awaiter ) => awaiter.GetResult();

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static T GetResult<T>( ConfiguredValueTaskAwaitable<T>.ConfiguredValueTaskAwaiter awaiter ) => awaiter.GetResult();

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal void GetResult( object awaiter )
    {
        if ( GetResultImpl == null )
            throw new InvalidOperationException( $"The {nameof(GetResultImpl)} is not set for {awaiter.GetType()}." );

        GetResultImpl( awaiter );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal T GetResultValue<T>( object awaiter )
    {
        if ( GetResultImpl == null )
            throw new InvalidOperationException( $"The {nameof(GetResultImpl)} is not set for {awaiter.GetType()}." );

        return (T) GetResultImpl( awaiter );
    }
}
