using System.Runtime.CompilerServices;

namespace Hyperbee.Expressions.CompilerServices;

public class AsyncInterpreterTaskBuilder<TResult>
{
    public AsyncTaskMethodBuilder<TResult> Builder;

    public Task<TResult> Task => Builder.Task;

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void SetStateMachine( IAsyncStateMachine stateMachine )
    {
        Builder.SetStateMachine( stateMachine );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void Start<TStateMachine>( ref TStateMachine stateMachine ) where TStateMachine : IAsyncStateMachine
    {
        Builder.Start( ref stateMachine );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void SetResult( TResult result )
    {
        Builder.SetResult( result );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void SetException( Exception exception )
    {
        Builder.SetException( exception );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void AwaitOnCompleted<TAwaiter, TStateMachine>(
        ref TAwaiter awaiter,
        ref TStateMachine stateMachine
    ) where TAwaiter : INotifyCompletion where TStateMachine : IAsyncStateMachine
    {
        Builder.AwaitOnCompleted( ref awaiter, ref stateMachine );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
        ref TAwaiter awaiter,
        ref TStateMachine stateMachine
    ) where TAwaiter : ICriticalNotifyCompletion where TStateMachine : IAsyncStateMachine
    {
        Builder.AwaitUnsafeOnCompleted( ref awaiter, ref stateMachine );
    }
}
