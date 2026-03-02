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
        // No fallback. If FEC fails, the test fails.
        // Use Assert.Inconclusive guards for known FEC limitations. See FecKnownIssues.cs.
        return expression.CompileFast()!;
    }
}
