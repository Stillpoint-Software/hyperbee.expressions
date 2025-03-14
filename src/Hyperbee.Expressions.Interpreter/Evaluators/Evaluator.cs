using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.Interpreter.Core;

namespace Hyperbee.Expressions.Interpreter.Evaluators;

internal static class Evaluator
{
    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public static object Unary( InterpretContext context, UnaryExpression unary ) => UnaryEvaluator.Unary( context, unary );

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public static object Binary( InterpretContext context, BinaryExpression binary ) => BinaryEvaluator.Binary( context, binary );
}
