namespace Hyperbee.Expressions.Interpreter.Core;

internal sealed class InterpretContext
{
    public InterpretScope Scope { get; init; } = new();
    public Stack<object> Results { get; init; } = new();

    public bool IsTransitioning => Transition != null;

    private Transition _transition;
    public Transition Transition
    {
        get => _transition;
        set 
        {
            _transition?.Reset();
            _transition = value;
        }
    }

    public void Deconstruct( out InterpretScope scope, out Stack<object> results )
    {
        scope = Scope;
        results = Results;
    }

    public void Deconstruct( out InterpretScope scope, out Stack<object> results, out Transition transition )
    {
        scope = Scope;
        results = Results;
        transition = Transition;
    }

    private static readonly ThreadLocal<InterpretContext> ThreadLocal = new();

    public static InterpretContext Current => ThreadLocal.Value ??= new InterpretContext();
    internal static void SetThreadInterpreterContext( InterpretContext context ) => ThreadLocal.Value = context;
}
