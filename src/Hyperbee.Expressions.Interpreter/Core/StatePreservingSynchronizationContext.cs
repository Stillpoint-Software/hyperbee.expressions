using System.Linq.Expressions;
using Hyperbee.Collections;

namespace Hyperbee.Expressions.Interpreter.Core;

internal sealed class StatePreservingSynchronizationContext : SynchronizationContext
{
    public override void Post( SendOrPostCallback callback, object state )
    {
        // collections are reference types, so we need to copy them

        var currentContext = InterpreterContext.Current;

        var capturedContext = new InterpreterContext()
        {
            Scope = CopyScope( currentContext.Scope ),
            Results = new( currentContext.Results ),
            Mode = currentContext.Mode,
            Navigation = currentContext.Navigation,
        };

        base.Post( _ =>
        {
            InterpreterContext.SetThreadContext( capturedContext );
            callback( state );
        }, null );

        return;

        static InterpretScope CopyScope( InterpretScope originalScope )
        {
            var newVariables = new LinkedDictionary<string, ParameterExpression>();

            foreach ( var node in originalScope.Variables.Nodes().Reverse() )
            {
                newVariables.Push( node.Name, node.Dictionary );
            }

            var newValues = new LinkedDictionary<ParameterExpression, object>();

            foreach ( var node in originalScope.Values.Nodes().Reverse() )
            {
                newValues.Push( node.Name, node.Dictionary );
            }

            var newScope = new InterpretScope
            {
                Depth = originalScope.Depth,
                Variables = newVariables,
                Values = newValues
            };

            return newScope;
        }
    }

    public override void Send( SendOrPostCallback callback, object state )
    {
        var capturedContext = InterpreterContext.Current;
        InterpreterContext.SetThreadContext( capturedContext );
        callback( state );
    }
}
