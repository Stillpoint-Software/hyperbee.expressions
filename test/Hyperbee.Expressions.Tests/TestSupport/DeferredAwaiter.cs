using System.Runtime.CompilerServices;

namespace Hyperbee.Expressions.Tests.TestSupport;

internal class DeferredAwaiter : INotifyCompletion
{
    private readonly Task _task;
    private readonly ManualResetEventSlim _completedEvent;

    public DeferredAwaiter( Task task, ManualResetEventSlim completedEvent )
    {
        _task = task ?? throw new ArgumentNullException( nameof(task) );
        _completedEvent = completedEvent ?? throw new ArgumentNullException( nameof(completedEvent) );
    }

    public bool IsCompleted
    {
        get
        {
            // If the event is not set, we are not completed
            
            if ( _completedEvent.IsSet )
            {
                return _task.IsCompleted;
            }

            _completedEvent.Set();
            return false;
        }
    }

    public void OnCompleted( Action continuation )
    {
        _task.GetAwaiter().OnCompleted( continuation ); // Forward
    }

    public void GetResult()
    {
        _task.GetAwaiter().GetResult(); // Forward
    }

    public DeferredAwaiter GetAwaiter() => this;
}
