namespace Hyperbee.Expressions.Interpreter.Core;

internal static class InterpretExecutionContext
{
    private static readonly AsyncLocal<InterpretContext> Context = new();

    internal static InterpretContext Current
    {
        get => Context.Value;
        set => Context.Value = value;
    }

    internal static void Run( Action action, InterpretContext context )
    {
        var executionContext = ExecutionContext.Capture();

        if ( executionContext == null )
            return;

        // Clone the context to prevent side effects in different execution contexts
        var localCapture = new InterpretContext( context );

        ExecutionContext.Run( executionContext, _ =>
        {
            Current = localCapture;
            action();
        }, null );
    }
}
