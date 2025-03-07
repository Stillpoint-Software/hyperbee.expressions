namespace Hyperbee.Expressions.Interpreter.Core;

internal sealed class InterpreterContext
{
    public InterpretScope Scope { get; init; } = new();
    public Stack<object> ResultStack { get; init; } = new();
    public InterpreterMode Mode { get; set; } = InterpreterMode.Evaluating;
    public Navigation Navigation { get; set; }
}
