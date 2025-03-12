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

        var localCapture = context.Clone();

        ExecutionContext.Run( executionContext, _ =>
        {
            Current = localCapture;
            action();
        }, null );
    }
}
