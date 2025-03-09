namespace Hyperbee.Expressions.Interpreter.Core;

internal sealed class InterpretContext
{
    public InterpretScope Scope { get; init; } = new();
    public Stack<object> Results { get; init; } = new();

    public bool IsNavigating => Navigation != null;

    private Navigation _navigation;
    public Navigation Navigation
    {
        get => _navigation;
        set 
        {
            _navigation?.Reset();
            _navigation = value;
        }
    }

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

    private static readonly ThreadLocal<InterpretContext> ThreadLocal = new();

    public static InterpretContext Current => ThreadLocal.Value ??= new InterpretContext();
    internal static void SetThreadInterpreterContext( InterpretContext context ) => ThreadLocal.Value = context;
}
