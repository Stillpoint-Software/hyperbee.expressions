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

public static class ExpressionCompilerExtensions
{
    public static Action Compile( this Expression<Action> expression, CompilerType compilerType = CompilerType.System )
    {
#if FAST_COMPILER
        if ( compilerType == CompilerType.Fast )
        {
            var compiledExpression = expression.CompileFast( false, CompilerFlags.EnableDelegateDebugInfo | CompilerFlags.ThrowOnNotSupportedExpression );
            var target = compiledExpression.Target; // keep for debugging
            return compiledExpression;
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
            var compiledExpression = expression.CompileFast( false, CompilerFlags.EnableDelegateDebugInfo | CompilerFlags.ThrowOnNotSupportedExpression );
            var target = compiledExpression.Target; // keep for debugging
            return compiledExpression;
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
            var compiledExpression = expression.CompileFast( false, CompilerFlags.EnableDelegateDebugInfo | CompilerFlags.ThrowOnNotSupportedExpression );
            var target = compiledExpression.Target; // keep for debugging
            return compiledExpression;
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
            var compiledExpression = expression.CompileFast( false, CompilerFlags.EnableDelegateDebugInfo | CompilerFlags.ThrowOnNotSupportedExpression );
            var target = compiledExpression.Target; // keep for debugging
            return compiledExpression;
        }
#endif
        if ( compilerType == CompilerType.Interpret )
        {
            return expression.Compile( preferInterpretation: true );
        }

        return expression.Compile();
    }
}
