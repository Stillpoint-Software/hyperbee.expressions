using System.Reflection;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions.Factory;

internal delegate object AwaitBinderDelegate( object instance );

public class AwaitBinder
{
    public MethodInfo AwaitMethod { get; }
    public MethodInfo GetAwaiterMethod { get; }
    public MethodInfo GetResultMethod { get; }

    private AwaitBinderDelegate GetAwaiterImpl { get; }
    private AwaitBinderDelegate GetResultImpl { get; }

    internal AwaitBinder(
        MethodInfo awaitMethod,
        MethodInfo getAwaiterMethod,
        MethodInfo getResultMethod,
        AwaitBinderDelegate getAwaiterImpl = null,
        AwaitBinderDelegate getResultImpl = null )
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
                var awaiter = GetAwaiter( awaitable );
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
                var awaiter = GetAwaiter( awaitable );
                return GetResultValue<T>( awaiter );
            }
        }
    }

    // GetAwaiter methods

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static TaskAwaiter GetAwaiter( Task task ) => task.GetAwaiter();

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static TaskAwaiter<T> GetAwaiter<T>( Task<T> task ) => task.GetAwaiter();

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static ValueTaskAwaiter GetAwaiter( ValueTask valueTask ) => valueTask.GetAwaiter();

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static ValueTaskAwaiter<T> GetAwaiter<T>( ValueTask<T> valueTask ) => valueTask.GetAwaiter();

    internal object GetAwaiter( object awaitable )
    {
        if ( GetAwaiterImpl == null )
            throw new InvalidOperationException( $"The {nameof(GetAwaiterImpl)} is not set for {awaitable.GetType()}." );

        return GetAwaiterImpl( awaitable );
    }

    // GetResult methods
    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static void GetResult( TaskAwaiter awaiter ) => awaiter.GetResult();

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static T GetResult<T>( TaskAwaiter<T> awaiter ) => awaiter.GetResult();

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static void GetResult( ValueTaskAwaiter awaiter ) => awaiter.GetResult();

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal static T GetResult<T>( ValueTaskAwaiter<T> awaiter ) => awaiter.GetResult();

    internal void GetResult( object awaiter )
    {
        if ( GetResultImpl == null )
            throw new InvalidOperationException( $"The {nameof(GetResultImpl)} is not set for {awaiter.GetType()}." );

        GetResultImpl( awaiter );
    }

    internal T GetResultValue<T>( object awaiter )
    {
        if ( GetResultImpl == null )
            throw new InvalidOperationException( $"The {nameof(GetResultImpl)} is not set for {awaiter.GetType()}." );

        return (T) GetResultImpl( awaiter );
    }
}
