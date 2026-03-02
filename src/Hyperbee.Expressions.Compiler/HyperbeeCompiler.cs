using System.Linq.Expressions;

namespace Hyperbee.Expressions.Compiler;

/// <summary>
/// High-performance IR-based expression compiler.
/// Drop-in replacement for Expression.Compile().
/// </summary>
public static class HyperbeeCompiler
{
    /// <summary>Compiles the expression. Throws on unsupported patterns.</summary>
    public static TDelegate Compile<TDelegate>( Expression<TDelegate> lambda )
        where TDelegate : Delegate
        => (TDelegate) Compile( (LambdaExpression) lambda );

    /// <summary>Compiles the expression. Throws on unsupported patterns.</summary>
    public static Delegate Compile( LambdaExpression lambda )
        => throw new NotImplementedException( "Hyperbee.Expressions.Compiler is not yet implemented." );

    /// <summary>Compiles the expression. Returns null on unsupported patterns.</summary>
    public static TDelegate? TryCompile<TDelegate>( Expression<TDelegate> lambda )
        where TDelegate : Delegate
        => null;

    /// <summary>Compiles the expression. Returns null on unsupported patterns.</summary>
    public static Delegate? TryCompile( LambdaExpression lambda )
        => null;

    /// <summary>Compiles the expression. Falls back to system compiler on failure.</summary>
    public static TDelegate CompileWithFallback<TDelegate>( Expression<TDelegate> lambda )
        where TDelegate : Delegate
        => (TDelegate) CompileWithFallback( (LambdaExpression) lambda );

    /// <summary>Compiles the expression. Falls back to system compiler on failure.</summary>
    public static Delegate CompileWithFallback( LambdaExpression lambda )
        => TryCompile( lambda ) ?? lambda.Compile();
}
