using System.Linq.Expressions;

namespace Hyperbee.Expressions.Interpreter.Core;

internal sealed class InterpretDelegateClosure
{
    private readonly InterpretContext _context;
    private readonly LambdaExpression _lambda;

    public InterpretDelegateClosure( InterpretContext context, LambdaExpression lambda )
    {
        // clone current context with current variable closure at the time of creation (like a display class)
        _context = new InterpretContext( context );
        _lambda = lambda;
    }

    public T Interpret<T>( params object[] values )
    {
        // Clone the context before running to ensure each interpreter has its own execution flow
        return new XsInterpreter( new InterpretContext( _context ) )
            .Interpret<T>( _lambda, true, values );
    }

    public void Interpret( params object[] values )
    {
        // Clone the context before running to ensure each interpreter has its own execution flow
        new XsInterpreter( new InterpretContext( _context ) )
            .Interpret<object>( _lambda, true, values );
    }
}
