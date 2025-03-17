using System.Linq.Expressions;

namespace Hyperbee.Expressions.Interpreter.Core;

internal sealed class InterpretDelegateClosure
{
    private readonly InterpretContext _context;
    private readonly LambdaExpression _lambda;

    public InterpretDelegateClosure( InterpretContext context, LambdaExpression lambda )
    {
        // clone current context with current variable closure at the time of creation
        _context = new InterpretContext( context );
        _lambda = lambda;
    }

    internal T Interpret<T>( params object[] values )
    {
        return Run<T>( values );
    }

    internal void Interpret( params object[] values )
    {
        Run<object>( values );
    }

    private T Run<T>( params object[] values )
    {
        var executionContext = ExecutionContext.Capture();

        if ( executionContext == null )
            return default;

        // Clone the context before running to capture current closure
        var local = new XsInterpreter( new InterpretContext( _context ) );
        T result = default;

        ExecutionContext.Run( executionContext, _ =>
        {
            // Ensures the AsyncLocal is correct for this execution context
            result = local.Interpret<T>( _lambda, true, values );
        }, null );

        return result;
    }
}
