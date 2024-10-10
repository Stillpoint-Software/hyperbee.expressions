using System.Reflection;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions;

public class AwaiterInfo
{
    public MethodInfo AwaitMethod { get; }
    public MethodInfo GetAwaiterMethod { get; }
    public MethodInfo GetResultMethod { get; }

    public AwaiterInfo( MethodInfo awaitMethod, MethodInfo getAwaiterMethod, MethodInfo getResultMethod )
    {
        AwaitMethod = awaitMethod;
        GetAwaiterMethod = getAwaiterMethod;
        GetResultMethod = getResultMethod;
    }

    // Await methods
    public void Await<TAwaitable>( TAwaitable awaitable, bool configureAwait )
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

    public T AwaitResult<TAwaitable, T>( TAwaitable awaitable, bool configureAwait )
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
    public static TaskAwaiter GetAwaiter( Task task ) => task.GetAwaiter();

    public static TaskAwaiter<T> GetAwaiter<T>( Task<T> task ) => task.GetAwaiter();

    public static ValueTaskAwaiter GetAwaiter( ValueTask valueTask ) => valueTask.GetAwaiter();

    public static ValueTaskAwaiter<T> GetAwaiter<T>( ValueTask<T> valueTask ) => valueTask.GetAwaiter();

    public object GetAwaiter( object awaitable )
    {
        return GetAwaiterMethod.IsStatic 
            ? GetAwaiterMethod.Invoke( null, [awaitable] ) 
            : GetAwaiterMethod.Invoke( awaitable, null );
    }

    // GetResult methods
    public static void GetResult( TaskAwaiter awaiter ) => awaiter.GetResult();

    public static T GetResult<T>( TaskAwaiter<T> awaiter ) => awaiter.GetResult();

    public static void GetResult( ValueTaskAwaiter awaiter ) => awaiter.GetResult();

    public static T GetResult<T>( ValueTaskAwaiter<T> awaiter ) => awaiter.GetResult();

    public void GetResult( object awaiter ) => GetResultMethod.Invoke( awaiter, null );

    public T GetResultValue<T>( object awaiter ) => (T) GetResultMethod.Invoke( awaiter, null );
}
