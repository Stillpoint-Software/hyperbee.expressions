using System.Linq.Expressions;

#if FAST_COMPILER
using FastExpressionCompiler;
#endif

namespace Hyperbee.Expressions.Tests.TestSupport;

public enum CompilerType
{
    Fast,
    System,
    Interpret
}

/// <summary>
/// Compiles a test expression with the chosen compiler.
/// </summary>
/// <remarks>
/// <para>
/// The Fast path falls back to the System compiler deliberately, unlike the helper of
/// the same name in Hyperbee.Expressions.Compiler.Tests, which fails instead. The two
/// projects want different things: that one tests FEC conformance, so a pattern FEC
/// declines has to be recorded as a gap; this one tests the expression types, so a
/// pattern FEC declines should still be exercised through whatever compiler can run it.
/// </para>
/// <para>
/// ifFastFailedReturnNull has to be true for the fallback below to be the one that runs.
/// Left at its default of false, CompileFast falls back to Expression.Compile() inside
/// FEC and never returns null, which made the null check, the catch and the explicit
/// fallback here unreachable and made it impossible to tell which compiler produced the
/// delegate. Verified against FEC 6.0.0.
/// </para>
/// </remarks>
public static class ExpressionCompilerExtensions
{
    public static Action Compile( this Expression<Action> expression, CompilerType compilerType = CompilerType.System )
    {
#if FAST_COMPILER
        if ( compilerType == CompilerType.Fast )
        {
            try
            {
                var compiledExpression = expression.CompileFast( ifFastFailedReturnNull: true, CompilerFlags.EnableDelegateDebugInfo );
                if ( compiledExpression != null )
                {
                    var target = compiledExpression.Target; // keep for debugging
                    return compiledExpression;
                }
            }
            catch ( NotSupportedExpressionException )
            {
                // Fall back to system compiler when FastExpressionCompiler doesn't support the expression
            }
            
            // Fall back to system compiler
            return expression.Compile();
        }
#endif
        if ( compilerType == CompilerType.Interpret )
        {
            return expression.Compile( preferInterpretation: true );
        }

        return expression.Compile();
    }

    public static Func<T> Compile<T>( this Expression<Func<T>> expression, CompilerType compilerType = CompilerType.System )
    {
#if FAST_COMPILER
        if ( compilerType == CompilerType.Fast )
        {
            try
            {
                var compiledExpression = expression.CompileFast( ifFastFailedReturnNull: true, CompilerFlags.EnableDelegateDebugInfo );
                if ( compiledExpression != null )
                {
                    var target = compiledExpression.Target; // keep for debugging
                    return compiledExpression;
                }
            }
            catch ( NotSupportedExpressionException )
            {
                // Fall back to system compiler when FastExpressionCompiler doesn't support the expression
            }
            
            // Fall back to system compiler
            return expression.Compile();
        }
#endif
        if ( compilerType == CompilerType.Interpret )
        {
            return expression.Compile( preferInterpretation: true );
        }

        return expression.Compile();
    }

    public static Func<T1, T2> Compile<T1, T2>( this Expression<Func<T1, T2>> expression, CompilerType compilerType = CompilerType.System )
    {
#if FAST_COMPILER
        if ( compilerType == CompilerType.Fast )
        {
            try
            {
                var compiledExpression = expression.CompileFast( ifFastFailedReturnNull: true, CompilerFlags.EnableDelegateDebugInfo );
                if ( compiledExpression != null )
                {
                    var target = compiledExpression.Target; // keep for debugging
                    return compiledExpression;
                }
            }
            catch ( NotSupportedExpressionException )
            {
                // Fall back to system compiler when FastExpressionCompiler doesn't support the expression
            }
            
            // Fall back to system compiler
            return expression.Compile();
        }
#endif
        if ( compilerType == CompilerType.Interpret )
        {
            return expression.Compile( preferInterpretation: true );
        }

        return expression.Compile();
    }

    public static Func<T1, T2, T3> Compile<T1, T2, T3>( this Expression<Func<T1, T2, T3>> expression, CompilerType compilerType = CompilerType.System )
    {
#if FAST_COMPILER
        if ( compilerType == CompilerType.Fast )
        {
            try
            {
                var compiledExpression = expression.CompileFast( ifFastFailedReturnNull: true, CompilerFlags.EnableDelegateDebugInfo );
                if ( compiledExpression != null )
                {
                    var target = compiledExpression.Target; // keep for debugging
                    return compiledExpression;
                }
            }
            catch ( NotSupportedExpressionException )
            {
                // Fall back to system compiler when FastExpressionCompiler doesn't support the expression
            }
            
            // Fall back to system compiler
            return expression.Compile();
        }
#endif
        if ( compilerType == CompilerType.Interpret )
        {
            return expression.Compile( preferInterpretation: true );
        }

        return expression.Compile();
    }
}
