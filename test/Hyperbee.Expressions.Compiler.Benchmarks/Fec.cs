using System.Linq.Expressions;
using FastExpressionCompiler;

namespace Hyperbee.Expressions.Compiler.Benchmarks;

/// <summary>
/// Compiles a benchmark expression with FastExpressionCompiler, refusing to report a
/// number FEC did not produce.
/// </summary>
/// <remarks>
/// ifFastFailedReturnNull has to be passed. CompileFast() defaults it to false, which
/// falls back to Expression.Compile() and returns a working delegate, so any tier FEC
/// declined would be timed with the System compiler doing the work and printed in the
/// FEC column. The published tables happen not to have been affected -- a fallback
/// would have shown up at System cost, and the FEC times were an order of magnitude
/// below it -- but that was luck, not method. Verified against FEC 6.0.0.
/// </remarks>
internal static class Fec
{
    public static TDelegate Compile<TDelegate>( Expression<TDelegate> expression )
        where TDelegate : Delegate
        => expression.CompileFast( ifFastFailedReturnNull: true )
           ?? throw new NotSupportedException(
               "FastExpressionCompiler declined this benchmark expression, so there is no FEC "
               + "number to report for this tier. Remove the tier or drop its FEC column." );
}
