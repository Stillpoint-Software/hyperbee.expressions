using System.Linq.Expressions;
using Hyperbee.Collections;

namespace Hyperbee.Expressions.Interpreter.Core;

internal sealed class InterpretSynchronizationContext : SynchronizationContext
{
    public override void Post( SendOrPostCallback callback, object state )
    {
        // collections are reference types, so we need to copy them
        var currentContext = InterpretContext.Current;

        var capturedContext = new InterpretContext()
        {
            Scope = CopyScope( currentContext.Scope ),
            Results = new( currentContext.Results ),
            Transition = currentContext.Transition,
        };

        base.Post( _ =>
        {
            InterpretContext.SetThreadInterpreterContext( capturedContext );
            callback( state );
        }, null );

        return;

        static InterpretScope CopyScope( InterpretScope originalScope )
        {
            var newValues = new LinkedDictionary<ParameterExpression, object>();

            foreach ( var node in originalScope.Values.Nodes().Reverse() )
            {
                newValues.Push( node.Name, node.Dictionary );
            }

            var newScope = new InterpretScope
            {
                Depth = originalScope.Depth,
                Values = newValues
            };

            return newScope;
        }
    }

    public override void Send( SendOrPostCallback callback, object state )
    {
        var capturedContext = InterpretContext.Current;
        InterpretContext.SetThreadInterpreterContext( capturedContext );
        callback( state );
    }
}
