using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.Expressions.Interpreter.Core;

internal sealed class InterpretDelegateClosure( XsInterpreter instance, LambdaExpression lambda )
{
    private static readonly AsyncLocal<InterpretContext> Context = new();

    internal T Evaluate<T>( params object[] values )
    {
        return CreateInterpreter()
            .EvaluateInternal<T>( lambda, true, values );
    }

    internal void Evaluate( params object[] values )
    {
        CreateInterpreter()
            .EvaluateInternal<object>( lambda, false, values );
    }

    internal static object Invoke( Delegate del, InterpretContext context, object[] arguments )
    {
        object result = null;
        Run( () =>
        {
            result = del?.DynamicInvoke( arguments );
        }, context );

        return result;
    }

    internal static object Invoke( MethodInfo methodInfo, object instance, InterpretContext context, object[] arguments )
    {
        object result = null;
        Run( () =>
        {
            result = methodInfo.Invoke( instance, arguments );
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
