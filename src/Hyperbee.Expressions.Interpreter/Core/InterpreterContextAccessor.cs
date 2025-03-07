namespace Hyperbee.Expressions.Interpreter.Core;

internal static class InterpreterContextAccessor
{
    private static readonly ThreadLocal<InterpreterContext> LocalContext = new();
    public static InterpreterContext Current => LocalContext.Value ??= new InterpreterContext();
    public static void Set(InterpreterContext context) => LocalContext.Value = context;
}
