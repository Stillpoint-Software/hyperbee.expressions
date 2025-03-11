using System.Linq.Expressions;
using Hyperbee.Collections;

namespace Hyperbee.Expressions.Interpreter.Core;

internal sealed class InterpretSynchronizationContext : SynchronizationContext
{
    public override void Post( SendOrPostCallback callback, object state )
    {
        var capturedContext = InterpretContext.Current.Clone();

        base.Post( _ =>
        {
            InterpretContext.Current = capturedContext;
            callback( state );
        }, null );
    }
}
