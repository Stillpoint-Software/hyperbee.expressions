using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.Expressions.Interpreter.Core;

internal sealed class InterpretCaller( XsInterpreter instance, LambdaExpression lambda )
{
    private static readonly AsyncLocal<InterpretContext> Context = new();

    internal T Interpret<T>( params object[] values )
    {
        return CreateInterpreter()
            .Interpret<T>( lambda, true, values );
    }

    internal void Interpret( params object[] values )
    {
        CreateInterpreter()
            .Interpret<object>( lambda, false, values );
    }

    internal static object Invoke( Delegate target, InterpretContext context, object[] arguments )
    {
        object result = null;
        Run( () =>
        {
            result = target?.DynamicInvoke( arguments );
        }, context );

        return result;
    }

    internal static object Invoke( MethodInfo target, object instance, InterpretContext context, object[] arguments )
    {
        object result = null;
        Run( () =>
        {
            result = target.Invoke( instance, arguments );
        }, context );

        return result;
    }

    private static void Run( Action action, InterpretContext context )
    {
        var executionContext = ExecutionContext.Capture();

        if ( executionContext == null )
            return;

        // Clone the context before running to capture current closure
        var localCapture = new InterpretContext( context );

        ExecutionContext.Run( executionContext, _ =>
        {
            // Ensures the AsyncLocal is correct for this execution context
            Context.Value = localCapture;
            action();
        }, null );
    }

    private XsInterpreter CreateInterpreter()
    {
        // Clone the interpreter so each thread runs independently
        return new XsInterpreter(
            instance,
            Context.Value ?? new InterpretContext( instance.Context ) );
    }

}
