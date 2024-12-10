
using System.Runtime.CompilerServices;

namespace Hyperbee.Expressions.Tests.TestSupport;

internal sealed class DeferredTaskCompletionSource : ICriticalNotifyCompletion
{
    private readonly ManualResetEventSlim _completedEvent = new();
    private readonly TaskCompletionSource _tcs = new();

    public Task Task => _tcs.Task;

    public bool IsCompleted
    {
        get
        {
            // We set the event after the first time the property is accessed.
            // This effectively defers the completion of the task until later
            // by ensuring that the first call to the property getter returns false.

            if ( _completedEvent.IsSet )
                return _tcs.Task.IsCompleted;

            _completedEvent.Set();
            return false;
        }
    }

    public void OnCompleted( Action continuation )
    {
        _tcs.Task.GetAwaiter().OnCompleted( continuation );
    }

    public void UnsafeOnCompleted( Action continuation )
    {
        _tcs.Task.GetAwaiter().UnsafeOnCompleted( continuation );
    }

    public void GetResult()
    {
        _tcs.Task.GetAwaiter().GetResult();
    }

    public void WaitForSignal()
    {
        _completedEvent.Wait();
    }

    public void SetResult()
    {
        if ( !_completedEvent.IsSet )
            _completedEvent.Set();

        _tcs.SetResult();
    }

    public void SetException( Exception exception )
    {
        if ( !_completedEvent.IsSet )
            _completedEvent.Set();

        _tcs.SetException( exception );
    }

    public DeferredTaskCompletionSource GetAwaiter() => this;
}

internal class DeferredTaskCompletionSource<T> : ICriticalNotifyCompletion
{
    private readonly ManualResetEventSlim _completedEvent = new();
    private readonly TaskCompletionSource<T> _tcs = new();

    public Task<T> Task => _tcs.Task;

    public bool IsCompleted
    {
        get
        {
            // We set the event after the first time the property is accessed.
            // This effectively defers the completion of the task until later
            // by ensuring that the first call to the property getter returns false.

            if ( _completedEvent.IsSet )
                return _tcs.Task.IsCompleted;

            _completedEvent.Set();
            return false;
        }
    }

    public void OnCompleted( Action continuation )
    {
        _tcs.Task.GetAwaiter().OnCompleted( continuation );
    }

    public void UnsafeOnCompleted( Action continuation )
    {
        _tcs.Task.GetAwaiter().UnsafeOnCompleted( continuation );
    }

    public T GetResult()
    {
        return _tcs.Task.GetAwaiter().GetResult();
    }

    public void WaitForSignal()
    {
        _completedEvent.Wait();
    }

    public void SetResult( T result )
    {
        if ( !_completedEvent.IsSet )
            _completedEvent.Set();

        _tcs.SetResult( result );
    }

    public void SetException( Exception exception )
    {
        if ( !_completedEvent.IsSet )
            _completedEvent.Set();

        _tcs.SetException( exception );
    }

    public DeferredTaskCompletionSource<T> GetAwaiter() => this;
}
