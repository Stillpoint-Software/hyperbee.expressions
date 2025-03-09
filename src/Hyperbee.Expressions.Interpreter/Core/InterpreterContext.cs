namespace Hyperbee.Expressions.Interpreter.Core;

internal sealed class InterpreterContext
{
    public InterpretScope Scope { get; init; } = new();
    public Stack<object> Results { get; init; } = new();
    public InterpreterMode Mode { get; set; } = InterpreterMode.Evaluating;
    public Navigation Navigation { get; set; }

    public void Deconstruct( out InterpretScope scope, out Stack<object> results )
    {
        scope = Scope;
        results = Results;
    }

    public void Deconstruct( out InterpretScope scope, out Stack<object> results, out Navigation navigation )
    {
        scope = Scope;
        results = Results;
        navigation = Navigation;
    }

    private static readonly ThreadLocal<InterpreterContext> ThreadContext = new();
    
    public static InterpreterContext Current => ThreadContext.Value ??= new InterpreterContext();
    internal static void SetThreadContext( InterpreterContext context ) => ThreadContext.Value = context;
}
