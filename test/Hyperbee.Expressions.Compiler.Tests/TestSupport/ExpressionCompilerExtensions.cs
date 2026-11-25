using System.Linq.Expressions;
using FastExpressionCompiler;

namespace Hyperbee.Expressions.Compiler.Tests.TestSupport;

public enum CompilerType
{
    Fast,
    System,
    Interpret,
    Hyperbee
}

public static class ExpressionCompilerExtensions
{
    public static TDelegate Compile<TDelegate>(
        this Expression<TDelegate> expression,
        CompilerType compilerType )
        where TDelegate : Delegate
    {
        return compilerType switch
        {
            CompilerType.System    => expression.Compile(),
            CompilerType.Interpret => expression.Compile( preferInterpretation: true ),
            CompilerType.Hyperbee  => HyperbeeCompiler.Compile( expression ),
            CompilerType.Fast      => CompileFast( expression ),
            _ => throw new ArgumentOutOfRangeException( nameof( compilerType ) )
        };
    }

    private static TDelegate CompileFast<TDelegate>( Expression<TDelegate> expression )
        where TDelegate : Delegate
    {
        // ifFastFailedReturnNull is required, not optional. CompileFast() defaults to
        // ifFastFailedReturnNull: false, which falls back to Expression.Compile() and
        // returns a working delegate, so a pattern FEC declines would pass this test
        // with the System compiler doing the work under FEC's name. Verified against
        // FEC 6.0.0: a catch filter (FEC NotSupported 1010) returns a delegate from
        // CompileFast() and null from CompileFast( ifFastFailedReturnNull: true ).
        //
        // Known FEC limitations are guarded with Assert.Inconclusive at the call site
        // and recorded in FecKnownIssues.cs.
        return expression.CompileFast( ifFastFailedReturnNull: true )
               ?? throw new NotSupportedException(
                   "FastExpressionCompiler declined this expression. If this is a known "
                   + "FEC limitation, guard the Fast case at the call site and record it "
                   + "in FecKnownIssues.cs." );
    }
}
