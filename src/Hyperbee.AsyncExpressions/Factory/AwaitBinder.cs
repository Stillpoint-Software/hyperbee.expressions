using System.Reflection;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions.Factory;

public class AwaitBinder
{
    public MethodInfo AwaitMethod { get; }
    public MethodInfo GetAwaiterMethod { get; }
    public MethodInfo GetResultMethod { get; }

    private MethodInfo GetAwaiterImplMethod { get; }
    private MethodInfo GetResultImplMethod { get; }

    internal AwaitBinder(
        MethodInfo awaitMethod,
        MethodInfo getAwaiterMethod,
        MethodInfo getResultMethod,
        MethodInfo getAwaiterImplMethod = null,
        MethodInfo getResultImplMethod = null )
    {
        AwaitMethod = awaitMethod;
        GetAwaiterMethod = getAwaiterMethod;
        GetResultMethod = getResultMethod;
        GetAwaiterImplMethod = getAwaiterImplMethod;
        GetResultImplMethod = getResultImplMethod;
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
        if ( GetAwaiterImplMethod == null )
            throw new InvalidOperationException( $"The {nameof(GetAwaiterImplMethod)} is not set for {awaitable.GetType()}." );

        return GetAwaiterImplMethod.IsStatic
            ? GetAwaiterImplMethod.Invoke( null, [awaitable] )
            : GetAwaiterImplMethod.Invoke( awaitable, null );
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
        if ( GetResultImplMethod == null )
            throw new InvalidOperationException( $"The {nameof(GetResultImplMethod)} is not set for {awaiter.GetType()}." );

        if ( GetResultImplMethod.IsStatic )
            GetResultImplMethod.Invoke( null, [awaiter] );
        else
            GetResultImplMethod.Invoke( awaiter, null );
    }

    internal T GetResultValue<T>( object awaiter )
    {
        if ( GetResultImplMethod == null )
            throw new InvalidOperationException( $"The GetResultImplMethod is not set for {awaiter.GetType()}." );

        return GetResultImplMethod.IsStatic
            ? (T) GetResultImplMethod.Invoke( null, [awaiter] )
            : (T) GetResultImplMethod.Invoke( awaiter, null );
    }
}
