using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.Interpreter.Core;

namespace Hyperbee.Expressions.Interpreter.Evaluators;

internal sealed class Evaluator
{
    private readonly UnaryEvaluator _unary;
    private readonly BinaryEvaluator _binary;

    public Evaluator( InterpretContext context )
    {
        _unary = new( context );
        _binary = new( context );
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public object Unary( UnaryExpression unary ) => _unary.Unary( unary );

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public object Binary( BinaryExpression binary ) => _binary.Binary( binary );
}
